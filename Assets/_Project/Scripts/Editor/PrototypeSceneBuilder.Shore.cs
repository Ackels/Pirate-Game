using System.Collections.Generic;
using System.IO;
using Pirates.Ocean;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Costa do oceano: um mapa por cena (<c>Textures/Ocean/&lt;cena&gt;_Costa.asset</c>, RG de 8 bits, até 512 px por lado)
    /// com a altura do chão (R) e a distância até o litoral (G) em volta das ilhas geradas nesta cena, e um ruído pequeno
    /// que repete para recortar a espuma. O shader do oceano lê os dois (OceanShore.hlsl) para pintar turquesa, raso e
    /// espuma sem textura de profundidade. A altura vem da mesma conta que faz a malha da ilha (IslandHeight).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string OceanTexturesFolder = Root + "/Textures/Ocean";
        const int ShoreMapMaxSize = 512;
        /// <summary>
        /// Mapa grande (cena 07 espaçada, 16/10: ~1,3 km de ilhas) passaria de ~1,8 m por pixel com 512: até 1024 px para não perder a
        /// espuma e o raso. As cenas menores ficam com 512 (mesmo mapa de antes).
        /// </summary>
        const int ShoreMapLargeSize = 1024;
        const float ShoreMaxTexel = 1.8f;
        /// <summary>Altura do chão no R (mínima e faixa) e distância máxima no G: o mesmo que o OceanSurface manda ao shader.</summary>
        static readonly Vector3 ShoreEncoding = new(-6f, 8f, 64f);

        /// <summary>Ilhas geradas na cena atual (CreateIslandTerrain registra; CreateOcean zera no começo de cada cena).</summary>
        static readonly List<IslandSpec> sceneIslands = new();

        static void BakeShoreMap(OceanSurface ocean, string scenePath)
        {
            if (!ocean) return;
            if (sceneIslands.Count == 0)
            {
                ocean.ConfigureShore(null, Vector4.zero, ShoreEncoding, null);
                return;
            }

            // Área: todas as ilhas, com folga para o turquesa e as faixas de espuma.
            float margin = ShoreEncoding.z + 8f;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var s in sceneIslands)
            {
                float reach = 1f + s.coastNoise;
                foreach (var a in s.anchors) reach = Mathf.Max(reach, a.radius / Mathf.Min(s.radiusX, s.radiusZ));
                var c = new Vector2(s.center.x, s.center.z);
                var half = new Vector2(s.radiusX * reach + margin, s.radiusZ * reach + margin);
                min = Vector2.Min(min, c - half);
                max = Vector2.Max(max, c + half);
            }
            var size = max - min;
            int maxSize = Mathf.Max(size.x, size.y) / ShoreMapMaxSize > ShoreMaxTexel ? ShoreMapLargeSize : ShoreMapMaxSize;
            float texel = Mathf.Max(size.x, size.y) / maxSize;
            int width = Mathf.Clamp(Mathf.CeilToInt(size.x / texel), 8, maxSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(size.y / texel), 8, maxSize);
            size = new Vector2(width * texel, height * texel);

            // Altura do chão (a maior entre as ilhas: longe de todas, o fundo de −6 m). Ponto fora do alcance de uma ilha
            // nem calcula (IslandHeight sorteia o litoral a cada chamada: é o que custa).
            var reachSq = new float[sceneIslands.Count];
            for (int k = 0; k < reachSq.Length; k++)
            {
                var s = sceneIslands[k];
                float reach = 1f + s.coastNoise;
                foreach (var a in s.anchors) reach = Mathf.Max(reach, a.radius / Mathf.Min(s.radiusX, s.radiusZ));
                float r = Mathf.Max(s.radiusX, s.radiusZ) * reach + 12f;
                reachSq[k] = r * r;
            }
            var ground = new float[width * height];
            for (int z = 0, i = 0; z < height; z++)
            for (int x = 0; x < width; x++, i++)
            {
                var p = min + new Vector2((x + 0.5f) * texel, (z + 0.5f) * texel);
                float h = ShoreEncoding.x;
                for (int k = 0; k < sceneIslands.Count; k++)
                {
                    var s = sceneIslands[k];
                    var local = p - new Vector2(s.center.x, s.center.z);
                    if (local.sqrMagnitude > reachSq[k]) continue;
                    h = Mathf.Max(h, IslandHeight(s, local) + s.center.y);
                }
                ground[i] = h;
            }

            var distance = CoastDistance(ground, width, height, texel);
            var pixels = new byte[width * height * 2];
            for (int i = 0; i < ground.Length; i++)
            {
                pixels[i * 2] = (byte)Mathf.RoundToInt(Mathf.Clamp01((ground[i] - ShoreEncoding.x) / ShoreEncoding.y) * 255f);
                pixels[i * 2 + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance[i] / ShoreEncoding.z) * 255f);
            }

            string name = Path.GetFileNameWithoutExtension(scenePath);
            var map = SaveTexture($"{OceanTexturesFolder}/{name}_Costa.asset", width, height, TextureFormat.RG16, pixels,
                TextureWrapMode.Clamp, mipmaps: false);
            ocean.ConfigureShore(map, new Vector4(min.x, min.y, size.x, size.y), ShoreEncoding, FoamNoise());
        }

        /// <summary>
        /// Distância (m) de cada ponto de água até a terra mais perto (chão acima do mar), por chanfro de duas passadas
        /// (vizinhos 3–4, erro de poucos %): terra = 0.
        /// </summary>
        static float[] CoastDistance(float[] ground, int width, int height, float texel)
        {
            const float Straight = 1f, Diagonal = 1.41421356f;
            var d = new float[ground.Length];
            for (int i = 0; i < d.Length; i++) d[i] = ground[i] > 0f ? 0f : float.MaxValue;
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                float v = d[i];
                if (x > 0) v = Mathf.Min(v, d[i - 1] + Straight);
                if (z > 0)
                {
                    v = Mathf.Min(v, d[i - width] + Straight);
                    if (x > 0) v = Mathf.Min(v, d[i - width - 1] + Diagonal);
                    if (x < width - 1) v = Mathf.Min(v, d[i - width + 1] + Diagonal);
                }
                d[i] = v;
            }
            for (int z = height - 1; z >= 0; z--)
            for (int x = width - 1; x >= 0; x--)
            {
                int i = z * width + x;
                float v = d[i];
                if (x < width - 1) v = Mathf.Min(v, d[i + 1] + Straight);
                if (z < height - 1)
                {
                    v = Mathf.Min(v, d[i + width] + Straight);
                    if (x < width - 1) v = Mathf.Min(v, d[i + width + 1] + Diagonal);
                    if (x > 0) v = Mathf.Min(v, d[i + width - 1] + Diagonal);
                }
                d[i] = v;
            }
            for (int i = 0; i < d.Length; i++) d[i] = d[i] == float.MaxValue ? float.MaxValue : d[i] * texel;
            return d;
        }

        /// <summary>Ruído da espuma: 64², repete nas bordas (ruído de valor periódico, duas oitavas), com mipmap.</summary>
        static Texture2D FoamNoise()
        {
            const int Size = 64;
            var rng = new System.Random(7);
            float[] Lattice(int cells)
            {
                var v = new float[cells * cells];
                for (int i = 0; i < v.Length; i++) v[i] = (float)rng.NextDouble();
                return v;
            }
            float Sample(float[] lattice, int cells, float u, float v)
            {
                float x = u * cells, y = v * cells;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = Mathf.SmoothStep(0f, 1f, x - x0), fy = Mathf.SmoothStep(0f, 1f, y - y0);
                int xa = ((x0 % cells) + cells) % cells, xb = (xa + 1) % cells;
                int ya = ((y0 % cells) + cells) % cells, yb = (ya + 1) % cells;
                float a = Mathf.Lerp(lattice[ya * cells + xa], lattice[ya * cells + xb], fx);
                float b = Mathf.Lerp(lattice[yb * cells + xa], lattice[yb * cells + xb], fx);
                return Mathf.Lerp(a, b, fy);
            }
            var coarse = Lattice(8);
            var fine = Lattice(16);
            var pixels = new byte[Size * Size];
            for (int y = 0, i = 0; y < Size; y++)
            for (int x = 0; x < Size; x++, i++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float n = 0.65f * Sample(coarse, 8, u, v) + 0.35f * Sample(fine, 16, u, v);
                pixels[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(n) * 255f);
            }
            return SaveTexture($"{OceanTexturesFolder}/Ocean_RuidoEspuma.asset", Size, Size, TextureFormat.R8, pixels,
                TextureWrapMode.Repeat, mipmaps: true);
        }

        /// <summary>Textura linear (dado, não cor) salva como asset; atualiza a que existe no lugar (mesmo GUID).</summary>
        static Texture2D SaveTexture(string path, int width, int height, TextureFormat format, byte[] pixels,
            TextureWrapMode wrap, bool mipmaps)
        {
            EnsureFolder(Root, "Textures");
            EnsureFolder(Root + "/Textures", "Ocean");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool created = !tex;
            if (created) tex = new Texture2D(width, height, format, mipmaps, linear: true);
            else tex.Reinitialize(width, height, format, mipmaps);
            tex.SetPixelData(pixels, 0);
            tex.wrapMode = wrap;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 0;
            tex.Apply(mipmaps, false);
            if (created) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }
    }
}
