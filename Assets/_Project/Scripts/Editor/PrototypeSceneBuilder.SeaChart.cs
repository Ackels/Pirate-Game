using System.Collections.Generic;
using System.IO;
using Pirates.Controls;
using Pirates.Ocean;
using Pirates.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Carta náutica (mapa no M, 12/11; cena 07): uma imagem de pergaminho do mar inteiro (até a borda do mundo), com as costas tiradas do
    /// mapa da costa do mar (`<cena>_Costa`: altura do chão e distância do litoral), e os lugares com nome (ilhas e covis), descobertos ao
    /// chegar perto. Ilha nova: ponha na tabela <see cref="ChartPlaces"/> (o nome do objeto da ilha na cena) e rode de novo.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string SeaChartPath = "Assets/_Project/UI/Mapa_Mar.png";
        const int SeaChartPixels = 1024;

        // Objeto da cena, nome na carta, tipo, raio da descoberta (m), conhecido desde o começo.
        static readonly (string obj, string name, SeaChart.PlaceKind kind, float radius, bool known)[] ChartPlaces =
        {
            ("Ilha_Sul", "Porto Alento", SeaChart.PlaceKind.Port, 200f, true),
            ("IlhaGrande", "Ilha da Caveira", SeaChart.PlaceKind.Island, 300f, false),
            ("Ilha_Norte", "Ilha Norte", SeaChart.PlaceKind.Island, 260f, false),
            ("Ilha_Oeste", "Ilha Oeste", SeaChart.PlaceKind.Island, 260f, false),
            ("Ilha_Ermida", "Ilha da Ermida", SeaChart.PlaceKind.Island, 230f, false),
            // Dentro da muralha de névoa (230 m): só se descobre com a rota aberta (capítulo 6).
            ("Ilha_Amaldicoada", "Ilha Amaldiçoada", SeaChart.PlaceKind.Island, 200f, false),
            ("LulaGigante", "Águas Negras", SeaChart.PlaceKind.Lair, 200f, false),
            ("Megalodonte", "Fossa do Megalodonte", SeaChart.PlaceKind.Lair, 200f, false),
        };

        static void AddSeaChart(PlayerModeSwitcher switcher, GameObject hud)
        {
            var edge = Object.FindAnyObjectByType<WorldEdge>(FindObjectsInactive.Include);
            Vector3 center = edge ? edge.transform.position : WorldEdgeCenter;
            float radius = edge ? edge.Radius : WorldEdgeRadius;
            // Um quadrado um pouco maior que o círculo da borda.
            float side = radius * 2f + 120f;
            var area = new Vector3(center.x - side * 0.5f, center.z - side * 0.5f, side);
            var image = BakeSeaChartImage(area, center, radius);

            var places = new List<SeaChart.Place>();
            foreach (var (obj, name, kind, r, known) in ChartPlaces)
            {
                var go = GameObject.Find(obj);
                if (!go) continue;
                places.Add(new SeaChart.Place { id = obj, name = name, kind = kind, position = go.transform.position, discoverRadius = r, known = known });
            }
            var root = new GameObject("CartaNautica");
            var chart = root.AddComponent<SeaChart>();
            chart.Configure(switcher, image, area, places.ToArray());
            hud.AddComponent<SeaChartHUD>().Bind(chart, switcher);
        }

        /// <summary>Põe (ou refaz) a carta na cena aberta (07) sem regerar e salva.</summary>
        public static string AddSeaChartToOpenScene()
        {
            foreach (var old in Object.FindObjectsByType<SeaChart>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            foreach (var old in Object.FindObjectsByType<SeaChartHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(old);
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (!switcher || !hud) return "sem navio ou HUD";
            AddSeaChart(switcher, hud.gameObject);
            var scene = switcher.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "ok";
        }

        /// <summary>
        /// A imagem: pergaminho; mar com o raso mais claro e uma linha pontilhada onde o raso acaba; terra areia na beira e verde para dentro,
        /// contornada a tinta; grade a cada 250 m; fora da borda do mundo, a névoa hachurada.
        /// </summary>
        static Texture2D BakeSeaChartImage(Vector3 area, Vector3 edgeCenter, float edgeRadius)
        {
            var ocean = Object.FindAnyObjectByType<OceanSurface>();
            Texture2D shore = null;
            Vector4 rect = Vector4.zero;
            if (ocean)
            {
                var so = new SerializedObject(ocean);
                shore = so.FindProperty("shoreMap").objectReferenceValue as Texture2D;
                rect = so.FindProperty("shoreRect").vector4Value;
            }
            int n = SeaChartPixels;
            float texel = area.z / n;
            var height = new float[n * n];
            var dist = new float[n * n];
            for (int y = 0, i = 0; y < n; y++)
                for (int x = 0; x < n; x++, i++)
                {
                    float wx = area.x + (x + 0.5f) * texel, wz = area.y + (y + 0.5f) * texel;
                    height[i] = ShoreEncoding.x;
                    dist[i] = ShoreEncoding.z;
                    if (!shore || rect.z <= 0f) continue;
                    float u = (wx - rect.x) / rect.z, v = (wz - rect.y) / rect.w;
                    if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                    var c = shore.GetPixelBilinear(u, v);
                    height[i] = ShoreEncoding.x + c.r * ShoreEncoding.y;
                    dist[i] = c.g * ShoreEncoding.z;
                }

            var inland = LandDistance(height, n, texel);
            var paper = new Color(0.88f, 0.80f, 0.62f);
            var deep = Color.Lerp(paper, new Color(0.30f, 0.52f, 0.60f), 0.6f);
            var shallow = Color.Lerp(paper, new Color(0.50f, 0.80f, 0.78f), 0.6f);
            var hill = new Color(0.47f, 0.55f, 0.33f);
            var sand = new Color(0.93f, 0.84f, 0.60f);
            var green = new Color(0.62f, 0.68f, 0.42f);
            var ink = new Color(0.28f, 0.19f, 0.11f);
            var fog = new Color(0.62f, 0.62f, 0.58f);
            var pixels = new Color[n * n];
            for (int y = 0, i = 0; y < n; y++)
                for (int x = 0; x < n; x++, i++)
                {
                    float wx = area.x + (x + 0.5f) * texel, wz = area.y + (y + 0.5f) * texel;
                    bool land = height[i] > 0f;
                    Color c;
                    if (land)
                    {
                        float k = inland[i];
                        c = k < 14f ? Color.Lerp(sand, green, Mathf.InverseLerp(6f, 14f, k)) : Color.Lerp(green, hill, Mathf.InverseLerp(20f, 70f, k));
                    }
                    else
                    {
                        c = Color.Lerp(shallow, deep, Mathf.InverseLerp(4f, 40f, dist[i]));
                        // Onde o raso acaba: linha pontilhada.
                        if (Mathf.Abs(dist[i] - 34f) < texel * 0.6f && ((x + y) / 3) % 2 == 0) c = Color.Lerp(c, ink, 0.35f);
                    }
                    // Contorno da costa.
                    if (land && Coast(height, n, x, y)) c = ink;
                    // Grade a cada 250 m.
                    if (Mathf.Repeat(wx, 250f) < texel || Mathf.Repeat(wz, 250f) < texel) c = Color.Lerp(c, ink, 0.12f);
                    // Fora da borda do mundo: névoa hachurada.
                    float r = new Vector2(wx - edgeCenter.x, wz - edgeCenter.z).magnitude;
                    if (r > edgeRadius - 40f)
                    {
                        float k = Mathf.InverseLerp(edgeRadius - 40f, edgeRadius + 20f, r);
                        c = Color.Lerp(c, fog, k * 0.85f);
                        if (k > 0.5f && ((x - y) & 7) == 0) c = Color.Lerp(c, ink, 0.25f);
                    }
                    if (Mathf.Abs(r - edgeRadius) < texel * 0.8f) c = Color.Lerp(c, ink, 0.55f);
                    // Papel: manchas leves e as bordas queimadas.
                    float grain = Mathf.PerlinNoise(x * 0.045f, y * 0.045f) * 0.6f + Mathf.PerlinNoise(x * 0.31f, y * 0.31f) * 0.4f;
                    c *= 0.94f + 0.1f * grain;
                    float ex = Mathf.Min(x, n - 1 - x) / (float)n, ey = Mathf.Min(y, n - 1 - y) / (float)n;
                    float burn = Mathf.Clamp01(1f - Mathf.Min(ex, ey) / 0.045f);
                    c = Color.Lerp(c, new Color(0.42f, 0.27f, 0.13f), burn * burn * 0.8f);
                    c.a = 1f;
                    pixels[i] = c;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(SeaChartPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SeaChartPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(SeaChartPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = SeaChartPixels;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SeaChartPath);
        }

        /// <summary>Distância (m) de cada ponto de terra até o mar, pela imagem (duas passadas de chanfro): a beira em areia, o meio verde.</summary>
        static float[] LandDistance(float[] height, int n, float texel)
        {
            var d = new float[n * n];
            const float big = 1e9f;
            for (int i = 0; i < d.Length; i++) d[i] = height[i] > 0f ? big : 0f;
            float diag = texel * 1.4142f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    if (d[i] == 0f) continue;
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i - 1] + texel);
                    if (y > 0) d[i] = Mathf.Min(d[i], d[i - n] + texel);
                    if (x > 0 && y > 0) d[i] = Mathf.Min(d[i], d[i - n - 1] + diag);
                    if (x < n - 1 && y > 0) d[i] = Mathf.Min(d[i], d[i - n + 1] + diag);
                }
            for (int y = n - 1; y >= 0; y--)
                for (int x = n - 1; x >= 0; x--)
                {
                    int i = y * n + x;
                    if (d[i] == 0f) continue;
                    if (x < n - 1) d[i] = Mathf.Min(d[i], d[i + 1] + texel);
                    if (y < n - 1) d[i] = Mathf.Min(d[i], d[i + n] + texel);
                    if (x < n - 1 && y < n - 1) d[i] = Mathf.Min(d[i], d[i + n + 1] + diag);
                    if (x > 0 && y < n - 1) d[i] = Mathf.Min(d[i], d[i + n - 1] + diag);
                }
            return d;
        }

        static bool Coast(float[] height, int n, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                    if (height[yy * n + xx] <= 0f) return true;
                }
            return false;
        }
    }
}
