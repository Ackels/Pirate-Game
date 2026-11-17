using System.Collections.Generic;
using System.Linq;
using Pirates.Controls;
using Pirates.Progression;
using Pirates.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Pintura do navio (16/11, "onde gastar o ouro"): o navio do jogador ganha cores compradas no estaleiro. As malhas dos três cascos
    /// (casco, velas, bandeiras) são copiadas com os triângulos que se pintam levados para faixas vazias da paleta do Kenney (mesmo x,
    /// mais acima: o costado de fora da madeira clara e da escura, o pano cinza das velas, as bandeiras); o <see cref="ShipLivery"/> pinta
    /// essas faixas numa cópia da paleta. Com as cores "originais" (de graça) o navio fica igual ao de antes.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string LiveryFolder = MeshesFolder + "/Pintura";
        const string LiveryPalettePath = Root + "/Textures/Navio_Pintura_Base.png";
        const string LiveryCatalogPath = DataFolder + "/Navio_Pinturas.asset";
        const string KenneyPaletteLivery = "Assets/ThirdParty/Kenney_PirateKit/Models/Textures/colormap.png";

        [MenuItem("Pirates/Protótipo/Navio: refazer a pintura")]
        static void MenuLivery() => Debug.Log(AddLiveryToOpenScene());

        /// <summary>Põe (ou refaz) a pintura no navio do jogador da cena aberta e salva, sem regerar.</summary>
        public static string AddLiveryToOpenScene()
        {
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>(FindObjectsInactive.Include);
            ShipController ship = switcher ? switcher.Ship : null;
            if (!ship)
                foreach (var s in Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include))
                    if (s.GetComponent<Pirates.Controls.PlayerShipInput>()) ship = s;
            if (!ship) return "sem o navio do jogador";
            string r = AddLivery(ship);
            var scene = ship.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }

        static string AddLivery(ShipController ship)
        {
            EnsureFolder(MeshesFolder, "Pintura");
            var palette = LiveryPalette();
            var catalog = LiveryCatalogAsset(palette);
            var parts = new List<Renderer>();
            var ornaments = new List<Transform>();
            Material kenney = null;
            int meshes = 0;
            foreach (var rigName in new[] { "Visual", "Visual_Escuna", "Visual_Galeao" })
            {
                var rig = ship.transform.Find(rigName);
                if (!rig) continue;
                var hull = rig.Find("Casco");
                var hullFilter = hull ? hull.GetComponent<MeshFilter>() : null;
                if (!hullFilter || !hullFilter.sharedMesh) continue;
                string model = OriginalMeshName(hullFilter.sharedMesh);
                foreach (var r in rig.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh) continue;
                    var kind = r.transform == hull ? PaintKind.Hull
                        : r.name.StartsWith("flag") ? PaintKind.Flag
                        : r.name.StartsWith("sail") ? PaintKind.Sail
                        : PaintKind.None;
                    if (kind == PaintKind.None) continue;
                    var source = OriginalMesh(model, mf.sharedMesh);
                    if (!source) continue;
                    if (!kenney && r.sharedMaterial && r.sharedMaterial.name.StartsWith("M_Kenney")) kenney = r.sharedMaterial;
                    mf.sharedMesh = PaintableMesh(model, source, kind, r.transform, ship.transform);
                    parts.Add(r);
                    meshes++;
                }
                ornaments.Add(LiveryOrnaments(ship.transform, rig, hull, hullFilter.sharedMesh));
            }
            meshes += AddBoardingLivery(parts, ornaments);
            if (!kenney) kenney = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Kenney.mat");
            var livery = ship.GetComponent<ShipLivery>();
            if (!livery) livery = ship.gameObject.AddComponent<ShipLivery>();
            livery.Configure(catalog, palette, kenney, parts.ToArray(), ornaments.ToArray());
            EditorUtility.SetDirty(livery);
            AssetDatabase.SaveAssets();
            return $"ok: pintura em {parts.Count} peças ({meshes} malhas), {catalog.options.Length} cores";
        }

        enum PaintKind { None, Hull, Sail, Flag }

        /// <summary>
        /// Pintura também no palco da abordagem (17/11): os conveses do seu navio lá ("Navio_Chalupa/Escuna/Galeao", o modelo × 3/2) usam
        /// cópias próprias do casco (Abordagem_&lt;modelo&gt;_Casco): elas passam pela mesma troca de faixas, as bandeiras também, e ganham
        /// carranca e lanternas na escala do palco. Os mastros ficam (têm a transparência da câmera, que troca o material).
        /// </summary>
        static int AddBoardingLivery(List<Renderer> parts, List<Transform> ornaments)
        {
            var stage = GameObject.Find("Abordagem");
            if (!stage) return 0;
            int meshes = 0;
            foreach (var deckName in new[] { "Navio_Chalupa", "Navio_Escuna", "Navio_Galeao" })
            {
                var deck = stage.transform.Find(deckName);
                var modelo = deck ? deck.Find("Modelo") : null;
                var hullFilter = modelo ? modelo.GetComponent<MeshFilter>() : null;
                if (!hullFilter || !hullFilter.sharedMesh) continue;
                var hullSource = OriginalMesh("", hullFilter.sharedMesh);
                string hullName = hullSource.name; // Abordagem_<modelo>_Casco
                if (!hullName.StartsWith("Abordagem_") || !hullName.EndsWith("_Casco")) continue;
                string kitModel = hullName.Substring("Abordagem_".Length, hullName.Length - "Abordagem_".Length - "_Casco".Length);
                foreach (var r in modelo.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh) continue;
                    PaintKind kind = r.transform == modelo ? PaintKind.Hull
                        : r.name.StartsWith("flag") ? PaintKind.Flag
                        : r.name.StartsWith("sail") ? PaintKind.Sail
                        : PaintKind.None;
                    if (kind == PaintKind.None) continue;
                    var source = kind == PaintKind.Hull ? hullSource : OriginalMesh(kitModel, mf.sharedMesh);
                    if (!source) continue;
                    mf.sharedMesh = PaintableMesh(kind == PaintKind.Hull ? "Abordagem" : kitModel, source, kind, r.transform, deck);
                    parts.Add(r);
                    meshes++;
                }
                ornaments.Add(LiveryOrnaments(deck, deck, modelo, hullFilter.sharedMesh, BoardingScale, BoardingHeightScale, 2f));
            }
            return meshes;
        }

        // ---------- Malhas ----------

        /// <summary>Nome da malha do kit (as cópias pintáveis se chamam "modelo__malha").</summary>
        static string OriginalMeshName(Mesh mesh)
        {
            // O último trecho, sem "(Clone)" (o casco da chalupa da cena 07 é uma cópia salva na cena; antes cada refeita somava um prefixo).
            int i = mesh.name.LastIndexOf("__", System.StringComparison.Ordinal);
            return (i >= 0 ? mesh.name.Substring(i + 2) : mesh.name).Replace("(Clone)", "");
        }

        /// <summary>A malha original no FBX do Pirate Kit (refazer não copia a cópia).</summary>
        static Mesh OriginalMesh(string model, Mesh current)
        {
            string name = OriginalMeshName(current);
            string path = AssetDatabase.GetAssetPath(current);
            if (path.EndsWith(".fbx") || path.EndsWith(".FBX")) return current;
            string fbx = $"Assets/ThirdParty/Kenney_PirateKit/Models/{model}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name == name)
                ?? AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/{name}.asset") ?? current;
        }

        /// <summary>
        /// Cópia da malha com os triângulos pintáveis subidos na paleta: no casco, o costado de fora (madeira clara e escura, faces de lado
        /// viradas para fora, longe da linha do meio; e as pontas) vai para cima (+0,75) e o pano cinza da vela da proa também; nas velas o
        /// pano cinza (+0,75); nas bandeiras (+0,5). As faixas de destino estão vazias na paleta do kit.
        /// </summary>
        static Mesh PaintableMesh(string model, Mesh source, PaintKind kind, Transform part, Transform ship)
        {
            if (kind == PaintKind.Flag) return FlagMesh(model, source, part, ship);
            string path = $"{LiveryFolder}/{model}__{OriginalMeshName(source)}.asset";
            var uv = source.uv;
            var v = source.vertices;
            var n = source.normals;
            var tri = source.triangles;
            var b = source.bounds;
            var outV = new List<Vector3>(v);
            var outN = new List<Vector3>(n);
            var outUv = new List<Vector2>(uv);
            var copies = new Dictionary<long, int>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                var c = (uv[tri[i]] + uv[tri[i + 1]] + uv[tri[i + 2]]) / 3f;
                int px = (int)(c.x * 512f), py = (int)(c.y * 512f);
                if (py >= 128) continue;
                bool wood = (px >= 352 && px < 384) || (px >= 416 && px < 448);
                bool cloth = px >= 160 && px < 192;
                bool mark = px >= 224 && px < 256;
                float shift = 0f;
                if (kind == PaintKind.Hull)
                {
                    if (cloth) shift = 0.75f;
                    else if (wood)
                    {
                        var p0 = v[tri[i]];
                        var p1 = v[tri[i + 1]];
                        var p2 = v[tri[i + 2]];
                        var normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                        var centre = (p0 + p1 + p2) / 3f;
                        float dx = centre.x - b.center.x, dz = centre.z - b.center.z;
                        bool side = Mathf.Abs(normal.y) < 0.8f && normal.x * dx > 0f && Mathf.Abs(dx) > 0.25f * b.extents.x && Mathf.Abs(normal.x) > 0.4f;
                        bool end = Mathf.Abs(normal.y) < 0.8f && normal.z * dz > 0f && Mathf.Abs(dz) > 0.55f * b.extents.z && Mathf.Abs(normal.z) > 0.5f;
                        if (side || end) shift = 0.75f;
                    }
                }
                else if (cloth || mark) shift = 0.75f;
                if (shift == 0f) continue;
                for (int k = 0; k < 3; k++)
                {
                    int vi = tri[i + k];
                    long key = vi * 8L + (long)(shift * 4f);
                    if (!copies.TryGetValue(key, out int ni))
                    {
                        ni = outV.Count;
                        outV.Add(v[vi]);
                        outN.Add(vi < n.Length ? n[vi] : Vector3.up);
                        outUv.Add(uv[vi] + new Vector2(0f, shift));
                        copies[key] = ni;
                    }
                    tri[i + k] = ni;
                }
            }
            return SaveLiveryMesh(path, $"{model}__{OriginalMeshName(source)}", outV, outN, outUv, tri);
        }

        /// <summary>
        /// Bandeira: toda ela vai para o quadrado de 128 px da paleta (x 224, linha 256), projetada de lado (o eixo comprido da bandeira
        /// = horizontal, o que aponta para cima no navio = vertical, mesma escala nos dois): o ShipLivery pinta ali o pano e o emblema.
        /// </summary>
        static Mesh FlagMesh(string model, Mesh source, Transform part, Transform ship)
        {
            string path = $"{LiveryFolder}/{model}__{OriginalMeshName(source)}.asset";
            var v = source.vertices;
            var n = source.normals;
            var b = source.bounds;
            var up = part.InverseTransformDirection(ship.up);
            var ax = new[] { Vector3.right, Vector3.up, Vector3.forward };
            int vert = 0;
            for (int i = 1; i < 3; i++) if (Mathf.Abs(Vector3.Dot(up, ax[i])) > Mathf.Abs(Vector3.Dot(up, ax[vert]))) vert = i;
            int hor = -1;
            for (int i = 0; i < 3; i++) if (i != vert && (hor < 0 || b.size[i] > b.size[hor])) hor = i;
            float sign = Vector3.Dot(up, ax[vert]) >= 0f ? 1f : -1f;
            float width = Mathf.Max(0.01f, b.size[hor]);
            var uv = new List<Vector2>(v.Length);
            const float Cell = 128f / 512f, X0 = 224f / 512f, Y0 = 256f / 512f, Inset = 1.5f / 512f;
            foreach (var p in v)
            {
                float u = (p[hor] - b.min[hor]) / width;
                float h = 0.5f + sign * (p[vert] - b.center[vert]) / width;
                uv.Add(new Vector2(X0 + Inset + u * (Cell - 2f * Inset), Y0 + Inset + h * (Cell - 2f * Inset)));
            }
            return SaveLiveryMesh(path, $"{model}__{OriginalMeshName(source)}", new List<Vector3>(v), new List<Vector3>(n), uv, source.triangles);
        }

        static Mesh SaveLiveryMesh(string path, string name, List<Vector3> outV, List<Vector3> outN, List<Vector2> outUv, int[] tri)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = !mesh;
            if (created) mesh = new Mesh();
            else mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = outV.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(outV);
            mesh.SetNormals(outN);
            mesh.SetUVs(0, outUv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- Paleta e catálogo ----------

        /// <summary>Cópia legível da paleta do kit (a do kit não é legível; o ShipLivery pinta uma cópia dela em tempo de jogo).</summary>
        static Texture2D LiveryPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(LiveryPalettePath);
            if (palette) return palette;
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyPaletteLivery);
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(LiveryPalettePath, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(LiveryPalettePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LiveryPalettePath);
            var kit = (TextureImporter)AssetImporter.GetAtPath(KenneyPaletteLivery);
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = kit ? kit.filterMode : FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LiveryPalettePath);
        }

        /// <summary>Cor média de uma faixa do kit (a amostra da cor "original" no estaleiro).</summary>
        static Color PaletteBand(Texture2D palette, int x)
        {
            var px = palette.GetPixels(x, 0, 32, 128);
            var sum = Color.clear;
            foreach (var c in px) sum += c;
            var mean = sum / px.Length;
            mean.a = 1f;
            return mean;
        }

        /// <summary>Cores à venda (criado uma vez: depois o asset é do Jhonata; cor nova = mais uma linha aqui e apagar o asset, ou no Inspector).</summary>
        static LiveryCatalog LiveryCatalogAsset(Texture2D palette)
        {
            var catalog = LoadOrCreateAsset<LiveryCatalog>(LiveryCatalogPath, c =>
            {
                LiveryOption O(string id, string name, LiverySlot slot, Color color, int price, int rank = 0, bool original = false) => new()
                {
                    id = id, displayName = name, slot = slot, color = color, price = price, requiredRank = rank, original = original,
                };
                c.options = new[]
                {
                    O("casco_madeira", "Madeira natural", LiverySlot.Hull, PaletteBand(palette, 352), 0, original: true),
                    O("casco_preto", "Preto alcatrão", LiverySlot.Hull, new Color(0.16f, 0.15f, 0.16f), 400),
                    O("casco_vermelho", "Vermelho sangue", LiverySlot.Hull, new Color(0.62f, 0.13f, 0.1f), 600),
                    O("casco_azul", "Azul da Coroa", LiverySlot.Hull, new Color(0.16f, 0.3f, 0.62f), 600),
                    O("casco_verde", "Verde-mar", LiverySlot.Hull, new Color(0.13f, 0.46f, 0.4f), 600),
                    O("casco_branco", "Branco osso", LiverySlot.Hull, new Color(0.88f, 0.84f, 0.74f), 800),
                    O("casco_dourado", "Dourado", LiverySlot.Hull, new Color(0.9f, 0.66f, 0.2f), 2500, 3),

                    O("faixa_madeira", "Madeira escura", LiverySlot.Trim, PaletteBand(palette, 416), 0, original: true),
                    O("faixa_preta", "Preta", LiverySlot.Trim, new Color(0.14f, 0.13f, 0.14f), 300),
                    O("faixa_branca", "Branca", LiverySlot.Trim, new Color(0.9f, 0.88f, 0.8f), 300),
                    O("faixa_vermelha", "Vermelha", LiverySlot.Trim, new Color(0.66f, 0.14f, 0.1f), 400),
                    O("faixa_azul", "Azul", LiverySlot.Trim, new Color(0.16f, 0.3f, 0.62f), 400),
                    O("faixa_dourada", "Dourada", LiverySlot.Trim, new Color(0.92f, 0.7f, 0.22f), 1200, 2),

                    O("velas_cinza", "Cinza pirata", LiverySlot.Sails, PaletteBand(palette, 160), 0, original: true),
                    O("velas_brancas", "Brancas", LiverySlot.Sails, new Color(0.94f, 0.92f, 0.86f), 500),
                    O("velas_negras", "Negras", LiverySlot.Sails, new Color(0.12f, 0.11f, 0.12f), 600),
                    O("velas_vermelhas", "Vermelhas", LiverySlot.Sails, new Color(0.66f, 0.14f, 0.12f), 800),
                    O("velas_azuis", "Azuis", LiverySlot.Sails, new Color(0.2f, 0.34f, 0.66f), 700),
                    O("velas_roxas", "Roxas de Lázaro", LiverySlot.Sails, new Color(0.38f, 0.18f, 0.5f), 1500, 4),

                    O("bandeira_original", "Preta do kit", LiverySlot.Flag, PaletteBand(palette, 160), 0, original: true),
                };
            });
            AddLiveryExtras(catalog, palette);
            return catalog;
        }
    }
}
