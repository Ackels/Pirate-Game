using System.Collections.Generic;
using System.IO;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Inventário da tela I (E1, 20/10; pedido do Jhonata: "mostrando o personagem e nele você coloca a espada, arma, roupa, acessório; e
    /// outra parte com o resto dos itens"). (1) <b>Ícones</b> dos itens e armas, fotografados dos modelos 3D numa cena de prévia (fundo
    /// preto e branco para o recorte, como o retrato da fama) em <c>UI/Icones/*.png</c>; só fotografa o que ainda não tem arquivo (apague o
    /// PNG para refazer) e só põe o ícone em asset que ainda não tem um. (2) A <b>vitrine</b>: cópia do modelo do capitão (só malhas e
    /// Animator) num pedestal longe do mapa, com câmera e luzes próprias, desligada até a tela abrir (Controls/LoadoutMannequin).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string IconsFolder = HudFolder + "/Icones";
        const int IconSize = 128;
        /// <summary>Longe de tudo (nada no caminho da luz nem da câmera), no alto (o mar segue a câmera do jogo).</summary>
        static readonly Vector3 MannequinSpot = new(-4000f, 300f, -4000f);

        // ---------- Ícones ----------

        static readonly Color IconGold = new(1f, 0.78f, 0.25f), IconIron = new(0.56f, 0.6f, 0.66f), IconPaper = new(0.92f, 0.84f, 0.64f),
            IconRibbon = new(0.7f, 0.16f, 0.12f), IconLead = new(0.36f, 0.37f, 0.4f);

        /// <summary>Fotografa os ícones que faltam e liga cada um à arma ou ao item (só onde ainda não há ícone).</summary>
        static void BakeItemIcons()
        {
            EnsureFolder(HudFolder, "Icones");
            // Lâminas e armas de fogo (vista do lado chato da lâmina, inclinada).
            var blade = Quaternion.Euler(0f, 0f, -45f);
            SetWeaponIcon("Weapon_Cutelo", Icon("Arma_Cutelo", t => KitWeapon(t, "Weapon_Cutlass", blade, 1f, Vector3.zero)));
            SetWeaponIcon("Weapon_Pistola", Icon("Arma_Pistola", t => KitWeapon(t, "Weapon_Pistol", Quaternion.Euler(0f, 0f, 20f), 1f, Vector3.zero)));
            var rifle = Icon("Arma_Bacamarte", t => KitWeapon(t, "Weapon_Rifle", Quaternion.Euler(0f, 0f, 35f), 1f, Vector3.zero));
            SetWeaponIcon("Weapon_Bacamarte", rifle);
            SetItemIcon("Equip_Bacamarte", rifle);
            foreach (var spec in BladeSpecs)
            {
                var s = spec;
                var icon = Icon("Arma_" + s.file, t =>
                {
                    if (!KitWeapon(t, s.model, blade, 1f, Vector3.zero)) return;
                    if (s.tint == Color.white && s.glow == Color.black) return;
                    var mat = BladeMaterial(s);
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                });
                SetWeaponIcon("Weapon_" + s.file, icon);
                SetItemIcon("Equip_" + s.file, icon);
            }

            // Mochilas, recursos, munição, papéis.
            var pack = Icon("Item_Mochila", t => Model(Kit.Survival, "bedroll-packed", t, Vector3.zero, 30f, 1f, "Mochila"), new Vector3(-0.6f, 0.7f, 1f));
            SetItemIcon("Equip_MochilaCouro", pack);
            SetItemIcon("Equip_MochilaContrabandista", pack);
            SetItemIcon("Item_Ouro", Icon("Item_Ouro", CoinStack, new Vector3(-0.5f, 0.9f, 1f)));
            SetItemIcon("Item_Madeira", Icon("Item_Madeira", t => Model(Kit.Survival, "resource-wood", t, Vector3.zero, 20f, 1f, "Madeira"), new Vector3(-0.6f, 0.8f, 1f)));
            SetItemIcon("Item_Polvora", Icon("Item_Polvora", t => Kenney("barrel", t, Vector3.zero, 30f, 1f, "Polvora"), new Vector3(-0.5f, 0.6f, 1f)));
            SetItemIcon("Item_Ferro", Icon("Item_Ferro", Ingots, new Vector3(-0.6f, 0.8f, 1f)));
            SetItemIcon("Item_Racao", Icon("Item_Racao", t => Kenney("bottle", t, Vector3.zero, Quaternion.Euler(0f, 0f, -20f), 1f, "Racao")));
            SetItemIcon("Item_Cartuchos", Icon("Item_Cartuchos", t => Model(Kit.Survival, "box", t, Vector3.zero, 25f, 1f, "Cartuchos"), new Vector3(-0.6f, 0.7f, 1f)));
            SetItemIcon("Item_Chumbo", Icon("Item_Chumbo", LeadShot, new Vector3(-0.4f, 0.9f, 1f)));
            var ball = Icon("Item_Bomba", t =>
            {
                Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Bomba");
                Part(PrimitiveType.Cylinder, "Pavio", t, new Vector3(0.05f, 0.27f, 0f), new Vector3(0.04f, 0.08f, 0.04f), Quaternion.Euler(0f, 0f, -25f),
                    GetMaterial("M_Icone_Pavio", new Color(0.82f, 0.7f, 0.45f), 0.1f));
            }, new Vector3(-0.5f, 0.5f, 1f));
            SetItemIcon("Item_Bomba", ball);
            SetItemIcon("Item_CargaCorrente", Icon("Item_CargaCorrente", t =>
            {
                Kenney("cannon-ball", t, new Vector3(-0.18f, 0f, 0f), 0f, 1f, "Bala1");
                Kenney("cannon-ball", t, new Vector3(0.18f, 0f, 0f), 0f, 1f, "Bala2");
                Part(PrimitiveType.Cylinder, "Corrente", t, new Vector3(0f, 0.1f, 0f), new Vector3(0.03f, 0.18f, 0.03f), Quaternion.Euler(0f, 0f, 90f),
                    GetMaterial("M_Icone_Ferro", IconIron, 0.5f));
            }, new Vector3(-0.4f, 0.6f, 1f)));
            SetItemIcon("Item_CargaMetralha", Icon("Item_CargaMetralha", t => Model(Kit.Survival, "bucket", t, Vector3.zero, 0f, 1f, "Metralha"), new Vector3(-0.4f, 0.9f, 1f)));
            SetItemIcon("Item_PedacoMapa", Icon("Item_PedacoMapa", MapPiece, new Vector3(-0.2f, 1.4f, 1f)));
            var scroll = Icon("Item_Esquema", Scroll, new Vector3(-0.4f, 0.8f, 1f));
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item && item.category == ItemCategory.Schematic) SetItemIcon(item.name, scroll);
            }
        }

        static void SetWeaponIcon(string asset, Texture2D icon)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponsFolder}/{asset}.asset");
            if (!w || !icon || w.icon) return;
            w.icon = icon;
            EditorUtility.SetDirty(w);
        }

        static void SetItemIcon(string asset, Texture2D icon)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{asset}.asset");
            if (!item || !icon || item.icon) return;
            item.icon = icon;
            EditorUtility.SetDirty(item);
        }

        static void CoinStack(Transform t)
        {
            var gold = GetMaterial("M_Icone_Ouro", IconGold, 0.7f);
            for (int i = 0; i < 5; i++)
                Part(PrimitiveType.Cylinder, "Moeda", t, new Vector3(0.015f * (i % 2), 0.025f + i * 0.045f, 0.01f * i), new Vector3(0.3f, 0.02f, 0.3f), Quaternion.identity, gold);
            Part(PrimitiveType.Cylinder, "Moeda", t, new Vector3(0.3f, 0.12f, -0.05f), new Vector3(0.3f, 0.02f, 0.3f), Quaternion.Euler(0f, 0f, 70f), gold);
            Part(PrimitiveType.Cylinder, "Moeda", t, new Vector3(-0.28f, 0.03f, 0.05f), new Vector3(0.3f, 0.02f, 0.3f), Quaternion.identity, gold);
        }

        static void Ingots(Transform t)
        {
            var iron = GetMaterial("M_Icone_Ferro", IconIron, 0.5f);
            Part(PrimitiveType.Cube, "Barra", t, new Vector3(-0.11f, 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.45f), Quaternion.identity, iron);
            Part(PrimitiveType.Cube, "Barra", t, new Vector3(0.11f, 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.45f), Quaternion.identity, iron);
            Part(PrimitiveType.Cube, "Barra", t, new Vector3(0f, 0.18f, 0f), new Vector3(0.2f, 0.12f, 0.45f), Quaternion.identity, iron);
        }

        static void LeadShot(Transform t)
        {
            var lead = GetMaterial("M_Icone_Chumbo", IconLead, 0.6f);
            var at = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0f, 0.03f), new Vector3(0.05f, 0f, -0.1f), new Vector3(-0.08f, 0f, -0.06f), new Vector3(0.05f, 0.09f, -0.03f) };
            foreach (var p in at) Part(PrimitiveType.Sphere, "Chumbo", t, p, Vector3.one * 0.1f, Quaternion.identity, lead);
        }

        static void MapPiece(Transform t)
        {
            Part(PrimitiveType.Cube, "Papel", t, Vector3.zero, new Vector3(0.5f, 0.01f, 0.4f), Quaternion.Euler(0f, 15f, 0f), GetMaterial("M_Icone_Papel", IconPaper, 0.1f));
            var red = GetMaterial("M_Icone_Fita", IconRibbon, 0.2f);
            Part(PrimitiveType.Cube, "X1", t, new Vector3(0.06f, 0.01f, 0.02f), new Vector3(0.16f, 0.01f, 0.03f), Quaternion.Euler(0f, 60f, 0f), red);
            Part(PrimitiveType.Cube, "X2", t, new Vector3(0.06f, 0.01f, 0.02f), new Vector3(0.16f, 0.01f, 0.03f), Quaternion.Euler(0f, -30f, 0f), red);
        }

        static void Scroll(Transform t)
        {
            Part(PrimitiveType.Cylinder, "Rolo", t, Vector3.zero, new Vector3(0.18f, 0.3f, 0.18f), Quaternion.Euler(0f, 0f, 90f), GetMaterial("M_Icone_Papel", IconPaper, 0.1f));
            Part(PrimitiveType.Cylinder, "Fita", t, Vector3.zero, new Vector3(0.19f, 0.03f, 0.19f), Quaternion.Euler(0f, 0f, 90f), GetMaterial("M_Icone_Fita", IconRibbon, 0.2f));
        }

        /// <summary>
        /// Foto de um objeto para ícone (recorte pelo fundo preto e branco), enquadrado pela caixa dele e visto de <paramref name="view"/>
        /// (direção do objeto para a câmera; padrão: de frente, um pouco de cima). Já existe = só carrega.
        /// </summary>
        static Texture2D Icon(string file, System.Action<Transform> build, Vector3? view = null)
        {
            string path = $"{IconsFolder}/{file}.png";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(IconSize * 2, IconSize * 2, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var holder = new GameObject("Icone");
                SceneManager.MoveGameObjectToScene(holder, scene);
                build(holder.transform);
                var renderers = holder.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                PortraitLight(scene, Quaternion.Euler(40f, 150f, 0f), 1.3f, new Color(1f, 0.96f, 0.88f));
                PortraitLight(scene, Quaternion.Euler(20f, -100f, 0f), 0.6f, new Color(0.75f, 0.82f, 1f));
                PortraitLight(scene, Quaternion.Euler(-30f, 0f, 0f), 0.35f, Color.white);

                var camGo = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.fieldOfView = 22f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.targetTexture = rt;
                var dir = (view ?? new Vector3(0f, 0.15f, -1f)).normalized;
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                // A câmera fica do lado de "view" (direção do objeto para a câmera).
                camGo.transform.position = bounds.center + dir * distance;
                camGo.transform.LookAt(bounds.center);

                int size = rt.width;
                var black = Render(cam, rt, Color.black);
                var white = Render(cam, rt, Color.white);
                cam.targetTexture = null;
                // Recorte e redução 2:1 (bordas mais lisas que o MSAA sozinho).
                var pixels = new Color[IconSize * IconSize];
                for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    Color sum = Color.clear;
                    for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int k = (y * 2 + dy) * size + x * 2 + dx;
                        Color b = black[k], w = white[k];
                        float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                        Color fg = alpha > 0.01f ? b / alpha : Color.black;
                        sum += new Color(fg.r * alpha, fg.g * alpha, fg.b * alpha, alpha);
                    }
                    sum /= 4f;
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
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Vitrine ----------

        /// <summary>
        /// Cópia do modelo do capitão (com as lâminas, a pistola na faixa, o bacamarte nas costas e o papagaio) num pedestal longe do mapa,
        /// só com malhas e o Animator (sem combate, mira, colisores), mais câmera e três luzes próprias. Tudo desligado até a tela I abrir.
        /// </summary>
        static void AddLoadoutMannequin(GameObject character)
        {
            var model = character.transform.Find("Model");
            if (!model) return;
            var root = new GameObject("VitrineDoCapitao");
            root.transform.position = MannequinSpot;
            var pedestal = new GameObject("Pedestal").transform;
            pedestal.SetParent(root.transform, false);
            var copy = Object.Instantiate(model.gameObject, pedestal, false);
            copy.name = "Capitao";
            copy.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            StripToVisuals(copy);
            if (copy.TryGetComponent(out Animator animator))
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            Transform Find(string name)
            {
                foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
                return null;
            }
            var bottle = Find("Garrafa");
            if (bottle) bottle.gameObject.SetActive(false);
            var looks = new List<LoadoutMannequin.BladeLook>();
            foreach (var spec in BladeSpecs)
            {
                var v = Find("Lamina_" + spec.file);
                if (v) looks.Add(new LoadoutMannequin.BladeLook { weapon = BladeWeapon(spec), visual = v.gameObject });
            }
            var starter = Find("Cutelo");
            var gun = Find("Bacamarte");

            // Medidas da cópia (o capitão é baixinho e cabeçudo: ~1,2 m).
            var bounds = new Bounds(copy.transform.position, Vector3.zero);
            foreach (var r in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true)) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(0.8f, bounds.size.y);

            var camGo = new GameObject("CameraDaVitrine");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            cam.depth = -10f;
            float frame = height * 1.18f;
            float distance = frame * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var center = new Vector3(0f, height * 0.5f, 0f);
            camGo.transform.localPosition = center + new Vector3(0f, height * 0.08f, distance);
            camGo.transform.LookAt(root.transform.TransformPoint(center));
            cam.enabled = false;

            Light Lamp(string name, Vector3 local, Color color, float intensity)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = local;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.range = 9f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                return l;
            }
            var lights = new[]
            {
                Lamp("Luz_Chave", new Vector3(1.4f, height * 1.4f, distance * 0.8f), new Color(1f, 0.92f, 0.8f), 3.2f),
                Lamp("Luz_Preenche", new Vector3(-1.6f, height * 0.8f, distance * 0.6f), new Color(0.7f, 0.8f, 1f), 1.4f),
                Lamp("Luz_Recorte", new Vector3(0.3f, height * 1.3f, -1.4f), new Color(1f, 0.85f, 0.6f), 2.2f),
            };

            var mannequin = root.AddComponent<LoadoutMannequin>();
            mannequin.Configure(copy, pedestal, cam, lights, starter ? starter.gameObject : null, looks.ToArray(), gun ? gun.gameObject : null,
                BlunderbussItem());
            copy.SetActive(false);
        }

        /// <summary>Deixa só o que desenha (malhas) e o Animator: tira combate, mira, colisores, linhas, partículas e o resto.</summary>
        static void StripToVisuals(GameObject copy)
        {
            // Duas voltas: componentes que dependem de outros (RequireComponent) saem antes.
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in copy.GetComponentsInChildren<Component>(true))
                {
                    if (!c || c is Transform || c is Animator || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                    Object.DestroyImmediate(c);
                }
        }
    }
}
