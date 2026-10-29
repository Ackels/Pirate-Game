using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ícones low-poly pintados (28/10, pedido do Jhonata: "tudo nesse estilo meio low poly, que nem a siba"; os desenhos do gerador de
    /// imagem pareciam artificiais). Cada ícone sai de um modelo 3D do jogo (ou montado com primitivas), mas não é fotografado com luz:
    /// a malha vira chapada (cada triângulo com os seus vértices) e cada face ganha uma cor = material × textura no meio do triângulo,
    /// com a luz já pintada (de cima à esquerda, pela câmera), um pouco mais de saturação e uma variação leve por face, como pincelada.
    /// Câmera ortográfica justa na caixa do modelo; fundo preto e branco para o recorte; 512 px reduzidos para 128.
    /// PNG feito em outro estilo (userData do importador ≠ <see cref="IconStyle"/>) é refeito no lugar (mesmo GUID).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string IconStyle = "lowpoly-3";
        const int IconRender = 512;

        /// <summary>O que o pintor sabe da peça: a caixa de tudo e o meio dos olhos (material com "Eye" no nome).</summary>
        struct IconShape
        {
            public Bounds bounds;
            public Vector3 eyes;
            public bool hasEyes;
        }

        /// <summary>Troca a cor de uma face (antes da luz): ponto e normal no mundo, material e a cor que o material daria.</summary>
        delegate Color IconPaint(IconShape shape, Vector3 point, Vector3 normal, Material material, Color color);

        static readonly Dictionary<Texture, (Color32[] px, int w, int h)> iconTextures = new();
        static readonly Dictionary<int, Mesh> prismMeshes = new();

        /// <summary>
        /// Ícone de um objeto montado por <paramref name="build"/>, visto de <paramref name="view"/> (direção do objeto para a câmera),
        /// girado <paramref name="roll"/> graus na tela. <paramref name="frameFrom"/> = fração da altura de onde começa o enquadramento
        /// (0,5 = busto). Já existe no estilo atual = só carrega.
        /// </summary>
        static Texture2D Icon(string file, System.Action<Transform> build, Vector3? view = null, float roll = 0f, IconPaint paint = null,
            float frameFrom = 0f)
        {
            string path = $"{IconsFolder}/{file}.png";
            if (File.Exists(path) && AssetImporter.GetAtPath(path) is TextureImporter done && done.userData == IconStyle)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(IconRender, IconRender, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            Mesh mesh = null;
            Material material = null;
            try
            {
                var holder = new GameObject("Icone");
                SceneManager.MoveGameObjectToScene(holder, scene);
                build(holder.transform);
                var dir = (view ?? new Vector3(0f, 0.15f, -1f)).normalized;
                var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                var camRot = Quaternion.LookRotation(-dir, up) * Quaternion.Euler(0f, 0f, roll);
                mesh = PaintedMesh(holder.transform, camRot, paint);
                if (!mesh) return null;
                foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var painted = new GameObject("Pintado");
                SceneManager.MoveGameObjectToScene(painted, scene);
                painted.AddComponent<MeshFilter>().sharedMesh = mesh;
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetColor("_BaseColor", Color.white);
                painted.AddComponent<MeshRenderer>().sharedMaterial = material;

                // Enquadramento justo: a caixa dos vértices no plano da câmera (abaixo de frameFrom não conta: busto).
                var verts = mesh.vertices;
                float bottom = float.MaxValue, top = float.MinValue;
                foreach (var v in verts) { bottom = Mathf.Min(bottom, v.y); top = Mathf.Max(top, v.y); }
                float cut = bottom + (top - bottom) * frameFrom;
                var inv = Quaternion.Inverse(camRot);
                Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
                float near = float.MaxValue, far = float.MinValue;
                foreach (var v in verts)
                {
                    var c = inv * v;
                    near = Mathf.Min(near, c.z);
                    far = Mathf.Max(far, c.z);
                    if (v.y < cut) continue;
                    min = Vector2.Min(min, c);
                    max = Vector2.Max(max, c);
                }
                var camGo = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f * 1.08f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = far - near + 2f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.targetTexture = rt;
                camGo.transform.SetPositionAndRotation(camRot * new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, near - 1f), camRot);

                var black = Render(cam, rt, Color.black);
                var white = Render(cam, rt, Color.white);
                cam.targetTexture = null;
                int f = IconRender / IconSize;
                var pixels = new Color[IconSize * IconSize];
                for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    Color sum = Color.clear;
                    for (int dy = 0; dy < f; dy++)
                    for (int dx = 0; dx < f; dx++)
                    {
                        int k = (y * f + dy) * IconRender + x * f + dx;
                        Color b = black[k], w = white[k];
                        float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                        Color fg = alpha > 0.01f ? b / alpha : Color.black;
                        sum += new Color(fg.r * alpha, fg.g * alpha, fg.b * alpha, alpha);
                    }
                    sum /= f * f;
                    pixels[y * IconSize + x] = sum.a > 0.001f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
                }
                var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                if (mesh) Object.DestroyImmediate(mesh);
                if (material) Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.userData = IconStyle;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Todas as malhas do objeto juntas no mundo, chapadas e com a cor de cada face (luz já pintada).</summary>
        static Mesh PaintedMesh(Transform root, Quaternion camRot, IconPaint paint)
        {
            var faces = new List<(Vector3 a, Vector3 b, Vector3 c, Color color, Material mat)>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                Mesh m;
                bool baked = false;
                if (r is SkinnedMeshRenderer skinned)
                {
                    m = new Mesh();
                    skinned.BakeMesh(m);
                    baked = true;
                }
                else if (r.TryGetComponent(out MeshFilter filter) && filter.sharedMesh) m = filter.sharedMesh;
                else continue;
                if (m.name.Contains("_LOD") && !m.name.Contains("_LOD0")) continue;
                var l2w = r.localToWorldMatrix;
                bool mirrored = l2w.determinant < 0f;
                var v = m.vertices;
                var uv = m.uv;
                bool hasUv = uv != null && uv.Length == v.Length;
                var mats = r.sharedMaterials;
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    var mat = mats.Length > 0 ? mats[Mathf.Min(sub, mats.Length - 1)] : null;
                    var idx = m.GetTriangles(sub);
                    for (int i = 0; i + 2 < idx.Length; i += 3)
                    {
                        var a = l2w.MultiplyPoint3x4(v[idx[i]]);
                        var b = l2w.MultiplyPoint3x4(v[idx[i + 1]]);
                        var c = l2w.MultiplyPoint3x4(v[idx[i + 2]]);
                        var mid = hasUv ? (uv[idx[i]] + uv[idx[i + 1]] + uv[idx[i + 2]]) / 3f : Vector2.zero;
                        var color = IconBaseColor(mat, mid, hasUv);
                        if (mirrored) faces.Add((a, c, b, color, mat));
                        else faces.Add((a, b, c, color, mat));
                    }
                }
                if (baked) Object.DestroyImmediate(m);
            }
            if (faces.Count == 0) return null;

            var shape = new IconShape { bounds = new Bounds(faces[0].a, Vector3.zero) };
            Vector3 eyeSum = Vector3.zero;
            int eyeCount = 0;
            foreach (var face in faces)
            {
                shape.bounds.Encapsulate(face.a);
                shape.bounds.Encapsulate(face.b);
                shape.bounds.Encapsulate(face.c);
                if (face.mat && face.mat.name.Contains("Eye"))
                {
                    eyeSum += (face.a + face.b + face.c) / 3f;
                    eyeCount++;
                }
            }
            shape.hasEyes = eyeCount > 0;
            if (shape.hasEyes) shape.eyes = eyeSum / eyeCount;

            // Luz pintada: de cima à esquerda, vinda da câmera; um pouco de luz de volta pela direita.
            var key = (camRot * new Vector3(-0.45f, 0.75f, -0.55f)).normalized;
            var fill = (camRot * new Vector3(0.75f, 0.15f, -0.4f)).normalized;
            var verts = new List<Vector3>(faces.Count * 3);
            var colors = new List<Color>(faces.Count * 3);
            var tris = new List<int>(faces.Count * 3);
            foreach (var face in faces)
            {
                var n = Vector3.Cross(face.b - face.a, face.c - face.a);
                if (n.sqrMagnitude < 1e-14f) continue;
                n.Normalize();
                var mid = (face.a + face.b + face.c) / 3f;
                var color = paint != null ? paint(shape, mid, n, face.mat, face.color) : face.color;
                float shade = 0.45f + 0.6f * Mathf.Max(0f, Vector3.Dot(n, key)) + 0.14f * Mathf.Max(0f, Vector3.Dot(n, fill));
                float jitter = 1f + 0.07f * (Mathf.Repeat(Mathf.Sin(mid.x * 91.7f + mid.y * 47.3f + mid.z * 13.1f) * 4375.85f, 1f) - 0.5f);
                Color.RGBToHSV(color, out float h, out float s, out float val);
                // Escuro sobe um pouco (o aço do kit sumia no encaixe escuro do HUD).
                color = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.12f), 0.2f + 0.8f * val);
                float k = Mathf.Min(shade, 1.25f) * jitter;
                // A cor da malha é linear no projeto: a escolhida (sRGB) converte antes.
                var final = new Color(Mathf.Clamp01(color.r * k), Mathf.Clamp01(color.g * k), Mathf.Clamp01(color.b * k), 1f).linear;
                tris.Add(verts.Count); verts.Add(face.a); colors.Add(final);
                tris.Add(verts.Count); verts.Add(face.b); colors.Add(final);
                tris.Add(verts.Count); verts.Add(face.c); colors.Add(final);
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Cor do material no ponto (cor × textura, em sRGB como foi escolhida).</summary>
        static Color IconBaseColor(Material m, Vector2 uv, bool hasUv)
        {
            if (!m) return new Color(0.6f, 0.6f, 0.6f);
            var color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            string prop = m.HasProperty("_BaseMap") ? "_BaseMap" : m.HasProperty("_MainTex") ? "_MainTex" : null;
            var tex = prop != null ? m.GetTexture(prop) : null;
            if (tex && hasUv)
            {
                var st = m.GetTextureScale(prop);
                var off = m.GetTextureOffset(prop);
                color *= SampleIconTexture(tex, new Vector2(uv.x * st.x + off.x, uv.y * st.y + off.y));
            }
            color.a = 1f;
            return color;
        }

        /// <summary>Lê a textura (cópia legível pela placa, guardada) no ponto do UV.</summary>
        static Color SampleIconTexture(Texture tex, Vector2 uv)
        {
            if (!iconTextures.TryGetValue(tex, out var data))
            {
                var tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, tmp);
                var previous = RenderTexture.active;
                RenderTexture.active = tmp;
                var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
                copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tmp);
                data = (copy.GetPixels32(), tex.width, tex.height);
                Object.DestroyImmediate(copy);
                iconTextures[tex] = data;
            }
            int x = Mathf.Clamp((int)(Mathf.Repeat(uv.x, 1f) * data.w), 0, data.w - 1);
            int y = Mathf.Clamp((int)(Mathf.Repeat(uv.y, 1f) * data.h), 0, data.h - 1);
            return data.px[y * data.w + x];
        }

        /// <summary>Refaz todos os ícones no estilo atual sem gerar cena (menu e eval).</summary>
        [MenuItem("Pirates/Protótipo/Refazer ícones dos itens")]
        public static void RebakeIcons()
        {
            BakeItemIcons();
            BakeFishIcons();
            BakeToolIcons();
            AssetDatabase.SaveAssets();
        }

        static void BakeToolIcons()
        {
            SetItemIcon("Equip_Machadinha", Icon("Item_Machadinha", t => Model(Kit.Survival, "tool-axe", t, Vector3.zero, Quaternion.Euler(0f, 0f, -35f), 1f, "Machadinha"),
                new Vector3(0f, 0.3f, 1f)));
            SetItemIcon("Equip_Picareta", Icon("Item_Picareta", t => Model(Kit.Survival, "tool-pickaxe", t, Vector3.zero, Quaternion.Euler(0f, 0f, -35f), 1f, "Picareta"),
                new Vector3(0f, 0.3f, 1f)));
        }

        // ---------- Peças ----------

        /// <summary>Prisma de <paramref name="sides"/> lados (raio 0,5, altura 1, de pé): moedas, rolos, cartuchos facetados.</summary>
        static GameObject PrismPart(int sides, string name, Transform parent, Vector3 localPos, Vector3 scale, Quaternion rotation, Material mat)
        {
            if (!prismMeshes.TryGetValue(sides, out var mesh) || !mesh)
            {
                var v = new List<Vector3> { new(0f, 0.5f, 0f), new(0f, -0.5f, 0f) };
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0.5f, Mathf.Sin(a) * 0.5f));
                    v.Add(new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f));
                }
                var t = new List<int>();
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    int ti = 2 + i * 2, bi = ti + 1, tj = 2 + j * 2, bj = tj + 1;
                    t.AddRange(new[] { 0, tj, ti });
                    t.AddRange(new[] { 1, bi, bj });
                    t.AddRange(new[] { ti, tj, bi });
                    t.AddRange(new[] { bi, tj, bj });
                }
                mesh = new Mesh { name = $"Prisma{sides}" };
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                // Em asset: o barril de pólvora das ilhas usa as mesmas peças, e malha só na memória some ao salvar a cena.
                mesh = SaveIslandMesh(mesh, $"{MeshesFolder}/Prisma_{sides}.asset");
                prismMeshes[sides] = mesh;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPos, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Escala o objeto para o maior lado ter <paramref name="size"/> e põe o meio de baixo em <paramref name="bottom"/> (local do pai).</summary>
        static Bounds FitIcon(GameObject go, float size, Vector3 bottom)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return default;
            var b = RendererBounds(renderers);
            float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (biggest > 1e-5f) go.transform.localScale *= size / biggest;
            b = RendererBounds(renderers);
            var parent = go.transform.parent;
            var want = parent ? parent.TransformPoint(bottom) : bottom;
            go.transform.position += want - new Vector3(b.center.x, b.min.y, b.center.z);
            return RendererBounds(renderers);
        }

        static void SetMaterial(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
            }
        }

        // ---------- Os itens ----------

        static void GoldCoins(Transform t)
        {
            var gold = GetMaterial("M_Icone_Ouro", IconGold, 0.7f);
            for (int i = 0; i < 6; i++)
                PrismPart(10, "Moeda", t, new Vector3(0.012f * (i % 2), 0.02f + i * 0.04f, 0.008f * i), new Vector3(0.32f, 0.036f, 0.32f),
                    Quaternion.Euler(0f, i * 13f, 0f), gold);
            PrismPart(10, "Moeda", t, new Vector3(0.3f, 0.15f, -0.02f), new Vector3(0.32f, 0.036f, 0.32f), Quaternion.Euler(10f, 0f, 68f), gold);
            PrismPart(10, "Moeda", t, new Vector3(-0.3f, 0.02f, 0.06f), new Vector3(0.32f, 0.036f, 0.32f), Quaternion.Euler(0f, 20f, 0f), gold);
            PrismPart(10, "Moeda", t, new Vector3(-0.15f, 0.02f, -0.24f), new Vector3(0.32f, 0.036f, 0.32f), Quaternion.Euler(0f, 5f, 0f), gold);
        }

        static void IronOre(Transform t)
        {
            var rock = Model(Kit.Survival, "rock-a", t, Vector3.zero, 30f, 1f, "Pedra");
            SetMaterial(rock, VeinRockMaterial());
            var rb = FitIcon(rock, 0.9f, Vector3.zero);
            var ore = OreMaterial();
            var spots = new[] { (new Vector3(-0.12f, 0.75f, 0.02f), 0.42f, -18f, 0), (new Vector3(0.18f, 0.6f, -0.05f), 0.32f, 24f, 1),
                (new Vector3(0.02f, 0.55f, 0.2f), 0.26f, 8f, 2), (new Vector3(-0.3f, 0.45f, -0.12f), 0.2f, -40f, 3) };
            foreach (var (p, size, tilt, variant) in spots)
            {
                var go = new GameObject("Cristal");
                go.transform.SetParent(t, false);
                go.transform.SetLocalPositionAndRotation(new Vector3(p.x, rb.size.y * p.y, p.z), Quaternion.Euler(tilt * 0.5f, variant * 50f, tilt));
                go.transform.localScale = Vector3.one * size;
                go.AddComponent<MeshFilter>().sharedMesh = OreCrystalMesh(variant);
                go.AddComponent<MeshRenderer>().sharedMaterial = ore;
            }
        }

        static void Ration(Transform t)
        {
            var bottle = Kenney("bottle", t, Vector3.zero, Quaternion.Euler(0f, 0f, -12f), 1f, "Garrafa");
            FitIcon(bottle, 0.62f, new Vector3(-0.12f, 0f, 0.05f));
            var apple = StorePrefab("Assets/ThirdParty/Loja/Fruits_LowPolyLite", "Apple_LP");
            var banana = StorePrefab("Assets/ThirdParty/Loja/Fruits_LowPolyLite", "Banana_LP");
            if (apple) FitIcon((GameObject)Object.Instantiate(apple, t, false), 0.26f, new Vector3(0.2f, 0f, -0.14f));
            if (banana)
            {
                var b = (GameObject)Object.Instantiate(banana, t, false);
                b.transform.localRotation = Quaternion.Euler(0f, 60f, 0f);
                FitIcon(b, 0.36f, new Vector3(0.12f, 0f, 0.18f));
            }
            foreach (var mb in t.GetComponentsInChildren<MonoBehaviour>()) Object.DestroyImmediate(mb);
        }

        /// <summary>Cartuchos de papel (pólvora e bala embrulhadas) amarrados com barbante.</summary>
        static void Cartridges(Transform t)
        {
            var paper = GetMaterial("M_Icone_Cartucho", new Color(0.9f, 0.82f, 0.62f), 0.1f);
            var twine = GetMaterial("M_Icone_Barbante", new Color(0.55f, 0.36f, 0.2f), 0.1f);
            var lead = GetMaterial("M_Icone_Chumbo", IconLead, 0.6f);
            var at = new[] { new Vector2(-0.08f, 0f), new Vector2(0.08f, 0f), new Vector2(0.24f, 0f), new Vector2(0f, 0.13f), new Vector2(0.16f, 0.13f) };
            var lying = Quaternion.Euler(0f, 0f, 90f);
            foreach (var p in at)
            {
                var pos = new Vector3(0f, 0.08f + p.y, p.x);
                PrismPart(8, "Cartucho", t, pos, new Vector3(0.15f, 0.5f, 0.15f), lying, paper);
                PrismPart(8, "Ponta", t, pos + new Vector3(0.27f, 0f, 0f), new Vector3(0.11f, 0.05f, 0.11f), lying, lead);
            }
            PrismPart(10, "Barbante", t, new Vector3(-0.02f, 0.15f, 0.08f), new Vector3(0.5f, 0.05f, 0.5f), lying, twine);
        }

        static void LeadBalls(Transform t)
        {
            var at = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0f, 0.05f), new Vector3(0.1f, 0f, -0.17f), new Vector3(-0.15f, 0f, -0.1f),
                new Vector3(-0.12f, 0f, 0.15f), new Vector3(0.08f, 0.15f, -0.03f), new Vector3(-0.04f, 0.15f, 0.07f) };
            foreach (var p in at)
            {
                var ball = Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Chumbo");
                FitIcon(ball, 0.2f, p);
            }
        }

        static void Bomb(Transform t)
        {
            var ball = Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Bomba");
            var b = FitIcon(ball, 0.6f, Vector3.zero);
            PrismPart(6, "Bocal", t, new Vector3(0f, b.size.y * 0.97f, 0f), new Vector3(0.14f, 0.07f, 0.14f), Quaternion.identity,
                GetMaterial("M_Icone_Ferro", IconIron, 0.5f));
            PrismPart(6, "Pavio", t, new Vector3(0.05f, b.size.y + 0.1f, 0f), new Vector3(0.04f, 0.17f, 0.04f), Quaternion.Euler(0f, 0f, -28f),
                GetMaterial("M_Icone_Pavio", new Color(0.82f, 0.7f, 0.45f), 0.1f));
            Part(PrimitiveType.Cube, "Faisca", t, new Vector3(0.1f, b.size.y + 0.18f, 0f), Vector3.one * 0.06f, Quaternion.Euler(30f, 20f, 45f),
                GetMaterial("M_Icone_Faisca", new Color(1f, 0.62f, 0.15f), 0.1f));
        }

        static void ChainShot(Transform t)
        {
            var iron = GetMaterial("M_Icone_Ferro", IconIron, 0.5f);
            FitIcon(Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Bala1"), 0.34f, new Vector3(-0.36f, 0f, 0f));
            FitIcon(Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Bala2"), 0.34f, new Vector3(0.36f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.18f + i * 0.12f;
                var rot = i % 2 == 0 ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                Part(PrimitiveType.Cube, "Elo", t, new Vector3(x, 0.17f + Mathf.Sin(i * 1.3f) * 0.03f, 0f), new Vector3(0.13f, 0.035f, 0.06f), rot, iron);
            }
        }

        static void GrapeShot(Transform t)
        {
            var bucket = Model(Kit.Survival, "bucket", t, Vector3.zero, 0f, 1f, "Balde");
            var b = FitIcon(bucket, 0.62f, Vector3.zero);
            var at = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.1f, -0.02f, 0.06f), new Vector3(-0.1f, -0.02f, 0.04f), new Vector3(0.04f, -0.02f, -0.11f),
                new Vector3(-0.06f, -0.02f, -0.08f), new Vector3(0.02f, 0.07f, 0.01f) };
            foreach (var p in at) FitIcon(Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Balinha"), 0.13f, new Vector3(p.x, b.max.y - 0.06f + p.y, p.z));
        }

        /// <summary>
        /// Barril de pólvora como o do Sea of Thieves (pedido do Jhonata, 28/10, com a foto): vermelho, deitado num cavalete, quatro aros
        /// pretos, etiqueta de papel com os ossos cruzados na frente e o pavio em cima.
        /// </summary>
        static void PowderKegIcon(Transform t)
        {
            var red = GetMaterial("M_Icone_BarrilVermelho", new Color(0.64f, 0.11f, 0.09f), 0.2f);
            var iron = GetMaterial("M_Icone_Aro", new Color(0.11f, 0.1f, 0.09f), 0.2f);
            var wood = GetMaterial("M_Icone_Cavalete", new Color(0.24f, 0.16f, 0.11f), 0.1f);
            var paper = GetMaterial("M_Icone_Etiqueta", new Color(0.86f, 0.74f, 0.52f), 0.1f);
            var ink = GetMaterial("M_Icone_Tinta", new Color(0.13f, 0.11f, 0.1f), 0.1f);
            var lying = Quaternion.Euler(0f, 0f, 90f);
            const float y = 0.44f;   // eixo do barril, em cima do cavalete
            // Bojo: o meio mais gordo que as pontas (o prisma fica de pé em Y; deitado, o comprido vai em X).
            PrismPart(12, "Bojo", t, new Vector3(0f, y, 0f), new Vector3(0.66f, 0.44f, 0.66f), lying, red);
            foreach (float x in new[] { -0.33f, 0.33f })
                PrismPart(12, "Ponta", t, new Vector3(x, y, 0f), new Vector3(0.6f, 0.24f, 0.6f), lying, red);
            foreach (float x in new[] { -0.42f, -0.19f, 0.19f, 0.42f })
            {
                float d = Mathf.Abs(x) > 0.3f ? 0.625f : 0.685f;
                PrismPart(12, "Aro", t, new Vector3(x, y, 0f), new Vector3(d, 0.05f, d), lying, iron);
            }
            // Etiqueta com os ossos cruzados, na frente (+Z, o lado da câmera).
            Part(PrimitiveType.Cube, "Etiqueta", t, new Vector3(0f, y + 0.01f, 0.335f), new Vector3(0.3f, 0.34f, 0.012f), Quaternion.Euler(0f, 0f, -4f), paper);
            foreach (float a in new[] { 45f, -45f })
            {
                var rot = Quaternion.Euler(0f, 0f, a - 4f);
                Part(PrimitiveType.Cube, "Osso", t, new Vector3(0f, y + 0.01f, 0.344f), new Vector3(0.24f, 0.045f, 0.01f), rot, ink);
                foreach (float end in new[] { -0.12f, 0.12f })
                    Part(PrimitiveType.Cube, "Ponta do osso", t, new Vector3(0f, y + 0.01f, 0.344f) + rot * new Vector3(end, 0f, 0f),
                        new Vector3(0.05f, 0.075f, 0.01f), rot, ink);
            }
            // Cavalete: dois pares de pés cruzados e uma travessa embaixo de cada ponta.
            foreach (float x in new[] { -0.26f, 0.26f })
            {
                foreach (float side in new[] { -1f, 1f })
                    Part(PrimitiveType.Cube, "Pe", t, new Vector3(x, 0.14f, side * 0.15f), new Vector3(0.05f, 0.3f, 0.05f), Quaternion.Euler(side * 28f, 0f, 0f), wood);
                Part(PrimitiveType.Cube, "Travessa", t, new Vector3(x, 0.17f, 0f), new Vector3(0.06f, 0.05f, 0.36f), Quaternion.identity, wood);
            }
            // Pavio de corda saindo do alto.
            PrismPart(6, "Pavio", t, new Vector3(0.04f, y + 0.39f, 0f), new Vector3(0.045f, 0.14f, 0.045f), Quaternion.Euler(0f, 0f, -14f),
                GetMaterial("M_Icone_Pavio", new Color(0.62f, 0.42f, 0.26f), 0.1f));
        }

        /// <summary>Mochila montada: corpo, bolso, aba, alças, fivela e o cobertor enrolado em cima (o kit só tinha o cobertor).</summary>
        static void Backpack(Transform t, string name, Color body, Color blanket, bool skull = false)
        {
            Material Mat(string part, Color c) => GetMaterial($"M_Icone_Mochila{name}_{part}", c, 0.15f);
            var leather = Mat("Corpo", body);
            var dark = Mat("Escuro", body * 0.72f);
            var buckle = GetMaterial("M_Icone_Fivela", new Color(0.86f, 0.66f, 0.28f), 0.5f);
            Part(PrimitiveType.Cube, "Corpo", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.3f), Quaternion.identity, leather);
            Part(PrimitiveType.Cube, "Bolso", t, new Vector3(0f, 0.16f, 0.17f), new Vector3(0.36f, 0.22f, 0.08f), Quaternion.identity, dark);
            Part(PrimitiveType.Cube, "Aba", t, new Vector3(0f, 0.53f, 0.06f), new Vector3(0.54f, 0.2f, 0.34f), Quaternion.Euler(-8f, 0f, 0f), dark);
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                Part(PrimitiveType.Cube, "Correia", t, new Vector3(x, 0.38f, 0.165f), new Vector3(0.05f, 0.42f, 0.02f), Quaternion.identity, Mat("Correia", body * 0.5f));
                Part(PrimitiveType.Cube, "Fivela", t, new Vector3(x, 0.26f, 0.18f), new Vector3(0.07f, 0.06f, 0.02f), Quaternion.identity, buckle);
            }
            PrismPart(8, "Cobertor", t, new Vector3(0f, 0.7f, -0.02f), new Vector3(0.2f, 0.56f, 0.2f), Quaternion.Euler(0f, 0f, 90f), Mat("Cobertor", blanket));
            if (skull)
            {
                var bone = GetMaterial("M_Icone_Osso", new Color(0.93f, 0.9f, 0.82f), 0.2f);
                Part(PrimitiveType.Cube, "Caveira", t, new Vector3(0f, 0.55f, 0.24f), new Vector3(0.12f, 0.1f, 0.03f), Quaternion.Euler(-8f, 0f, 0f), bone);
                Part(PrimitiveType.Cube, "Queixo", t, new Vector3(0f, 0.49f, 0.235f), new Vector3(0.08f, 0.04f, 0.03f), Quaternion.Euler(-8f, 0f, 0f), bone);
            }
        }

        /// <summary>Três toras (casca escura, ponta clara com os anéis) amarradas.</summary>
        static void Logs(Transform t)
        {
            var wood = GetMaterial("M_Icone_Tronco", new Color(0.45f, 0.28f, 0.16f), 0.1f);
            var lying = Quaternion.Euler(0f, 0f, 90f);
            PrismPart(7, "Tora", t, new Vector3(0f, 0.12f, -0.13f), new Vector3(0.26f, 0.8f, 0.26f), lying, wood);
            PrismPart(7, "Tora", t, new Vector3(0.04f, 0.12f, 0.13f), new Vector3(0.26f, 0.8f, 0.26f), lying, wood);
            PrismPart(7, "Tora", t, new Vector3(-0.03f, 0.34f, 0f), new Vector3(0.26f, 0.8f, 0.26f), lying, wood);
            var rope = GetMaterial("M_Icone_Barbante", new Color(0.55f, 0.36f, 0.2f), 0.1f);
            foreach (float x in new[] { -0.2f, 0.2f })
                PrismPart(10, "Corda", t, new Vector3(x, 0.22f, 0f), new Vector3(0.62f, 0.05f, 0.62f), lying, rope);
        }

        /// <summary>Ponta da tora (normal no comprido, em X) clara; o resto é a casca.</summary>
        static Color LogPaint(IconShape s, Vector3 p, Vector3 n, Material m, Color color) =>
            m && m.name == "M_Icone_Tronco" && Mathf.Abs(n.x) > 0.9f ? new Color(0.88f, 0.7f, 0.46f) : color;

        /// <summary>Um marujo em busto (o Henry do kit, roupa de marujo, na pose parada; com a Anne atrás pareciam dois um dentro do outro).</summary>
        static void CrewBust(Transform t)
        {
            void Sailor(string model, int palette, Vector3 at, float yaw)
            {
                var holder = new GameObject(model).transform;
                holder.SetParent(t, false);
                holder.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                var go = KitCharacter(model, holder, CivilianMaterial(model, palette));
                var idle = KitClip(model, "Idle");
                if (idle) idle.SampleAnimation(go, Mathf.Min(0.4f, idle.length));
            }
            Sailor(KitCharacters.Henry, 1, Vector3.zero, 165f);
        }

        /// <summary>Esquema: a planta de pergaminho aberta (dois rolos), a fita e o lacre na cor dele e a miniatura do que ele constrói.</summary>
        static void Blueprint(Transform t, Color ribbon, System.Action<Transform> miniature)
        {
            var paper = GetMaterial("M_Icone_Papel", IconPaper, 0.1f);
            var roll = GetMaterial("M_Icone_PapelRolo", IconPaper * 0.85f, 0.1f);
            var band = new Material(GetMaterial("M_Icone_Fita", IconRibbon, 0.2f));
            band.SetColor("_BaseColor", ribbon);
            var seal = new Material(band);
            seal.SetColor("_BaseColor", ribbon * 0.7f);
            Part(PrimitiveType.Cube, "Papel", t, new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.02f, 0.66f), Quaternion.identity, paper);
            foreach (float x in new[] { -0.47f, 0.47f })
                PrismPart(8, "Rolo", t, new Vector3(x, 0.06f, 0f), new Vector3(0.12f, 0.72f, 0.12f), Quaternion.Euler(90f, 0f, 0f), roll);
            Part(PrimitiveType.Cube, "Fita", t, new Vector3(-0.3f, 0.025f, 0f), new Vector3(0.09f, 0.02f, 0.72f), Quaternion.identity, band);
            PrismPart(10, "Lacre", t, new Vector3(-0.3f, 0.04f, -0.2f), new Vector3(0.15f, 0.03f, 0.15f), Quaternion.identity, seal);
            var mini = new GameObject("Miniatura");
            mini.transform.SetParent(t, false);
            miniature(mini.transform);
            FitIcon(mini, 0.74f, new Vector3(0.1f, 0.02f, 0.02f));
        }

        static void ShipyardMiniature(Transform t)
        {
            Model(Kit.Survival, "resource-planks", t, Vector3.zero, 20f, 1f, "Tabuas");
            var hammer = Model(Kit.Survival, "tool-hammer", t, Vector3.zero, Quaternion.Euler(0f, -30f, 80f), 1f, "Martelo");
            FitIcon(hammer, 0.8f, new Vector3(0.1f, 0.25f, -0.2f));
        }

        /// <summary>Siba (D2): o modelo é todo azul; pinta dorso marrom-avermelhado, barriga clara, braços, nadadeira e olho dourado.</summary>
        static Color SibaPaint(IconShape s, Vector3 p, Vector3 n, Material m, Color color)
        {
            var b = s.bounds;
            var size = b.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int up = axis == 1 ? 2 : 1;
            int side = 3 - axis - up;
            if (m && m.name.Contains("Eye"))
            {
                var outward = Vector3.zero;
                outward[side] = Mathf.Sign(p[side] - b.center[side]);
                return Vector3.Dot(n, outward) > 0.72f ? new Color(0.05f, 0.04f, 0.03f) : new Color(0.95f, 0.72f, 0.2f);
            }
            float t = (p[axis] - b.min[axis]) / Mathf.Max(1e-5f, size[axis]);
            if (s.hasEyes && s.eyes[axis] < b.center[axis]) t = 1f - t;
            float h = (p[up] - b.center[up]) / Mathf.Max(1e-5f, size[up] * 0.5f);
            float sd = Mathf.Abs(p[side] - b.center[side]) / Mathf.Max(1e-5f, size[side] * 0.5f);
            var dorsal = new Color(0.66f, 0.32f, 0.26f);
            var belly = new Color(0.96f, 0.82f, 0.68f);
            var arms = new Color(0.88f, 0.42f, 0.32f);
            var fin = new Color(0.88f, 0.62f, 0.48f);
            if (t > 0.72f) return Color.Lerp(arms, arms * 0.8f, (t - 0.72f) / 0.28f);
            if (sd > 0.8f) return fin;
            return Color.Lerp(belly, dorsal, Mathf.SmoothStep(0f, 1f, (h + 0.35f) / 0.9f));
        }
    }
}
