using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Quadros da abertura de "A Coroa de Prata" (HISTORIA.md): nove cenas montadas com os nossos modelos (capitão, Lázaro Negro,
    /// Tormenta, chalupa, tentáculo, esqueletos, vila) numa cena de prévia, fotografadas e tratadas como pintura (filtro de Kuwahara,
    /// cor por cena, vinheta, grão de papel e borda queimada). O céu e o fundo do mar são pintados por baixo (recorte com fundo preto e
    /// branco, como o retrato da fama). Saem em UI/Prologo/Quadro_1..9.png (1600 × 900). Menu Pirates → Prólogo.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string PrologueFolder = Root + "/UI/Prologo";
        const int PrologueWidth = 1600, PrologueHeight = 900;

        /// <summary>Uma cena do prólogo: objetos, câmera, céu pintado e o tratamento de cor.</summary>
        sealed class Stage
        {
            public Scene scene;
            public Transform root;
            public Camera cam;
            public Func<float, float, Color> sky = (u, v) => Color.black;
            public Color tint = Color.white;
            public float saturation = 1f, contrast = 1f, vignette = 0.45f, haze;
            public Color hazeColor = Color.black;
            public int paintRadius = 4;
            /// <summary>Contorno de tinta nas bordas (0 = sem).</summary>
            public float ink = 0.55f;
        }

        [MenuItem("Pirates/Prólogo/Gerar quadros da abertura")]
        public static void BuildPrologueFrames()
        {
            for (int i = 1; i <= PrologueFrames.Length; i++) BuildPrologueFrame(i);
            Debug.Log($"[Pirates] Quadros do prólogo em {PrologueFolder}.");
        }

        static Action<Stage>[] PrologueFrames => new Action<Stage>[]
        {
            FrameCrown, FrameBones, FrameCabin, FrameBattle, FrameBurning, FramePlank, FrameBeach, FramePier, FrameTitle,
        };

        /// <summary>Gera um quadro só (1 a 9): para ajustar uma cena sem refazer todas.</summary>
        public static void BuildPrologueFrame(int number)
        {
            EnsureFolder(Root + "/UI", "Prologo");
            var stage = new Stage { scene = EditorSceneManager.NewPreviewScene() };
            var rt = new RenderTexture(PrologueWidth, PrologueHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            string path = $"{PrologueFolder}/Quadro_{number}.png";
            try
            {
                var holder = new GameObject("Quadro");
                SceneManager.MoveGameObjectToScene(holder, stage.scene);
                stage.root = holder.transform;
                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(stage.root, false);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = stage.scene;
                cam.fieldOfView = 40f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 1500f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.targetTexture = rt;
                stage.cam = cam;

                PrologueFrames[number - 1](stage);

                var black = Render(cam, rt, Color.black);
                var white = Render(cam, rt, Color.white);
                cam.targetTexture = null;
                var pixels = Composite(stage, black, white);
                Paint(stage, pixels);
                var tex = new Texture2D(PrologueWidth, PrologueHeight, TextureFormat.RGB24, false);
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(stage.scene);
            }
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None || importer.maxTextureSize < 2048))
            {
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        // ---------- As nove cenas ----------

        /// <summary>1. O fundo do mar: a nau capitânia afundada, o baú brilhando prata, um tentáculo na sombra.</summary>
        static void FrameCrown(Stage s)
        {
            Ground(s, new Color(0.42f, 0.44f, 0.36f), 0f, 120f, 0.6f, 3, 0.8f);
            Kenney("ship-pirate-large", s.root, new Vector3(7f, -2.2f, 30f), Quaternion.Euler(8f, 215f, 26f), 2.2f, "Naufragio");
            Kenney("chest", s.root, new Vector3(-2.2f, 0.2f, 9.5f), Quaternion.Euler(0f, 150f, -8f), 1f, "Bau");
            // A Coroa de Prata na areia, brilhando, com moedas em volta.
            var crownAt = new Vector3(0.2f, 0.75f, 5.5f);
            var silver = GetEmissiveMaterial("M_Prologo_Prata", new Color(0.86f, 0.9f, 0.96f), 1.6f);
            Crown(s, crownAt, 0.8f, silver, Quaternion.Euler(-14f, 20f, 8f));
            Glow(s, crownAt + Vector3.up * 1.2f, new Color(0.8f, 0.9f, 1f), 0f, 8f, 10f);
            var rng = new System.Random(4);
            for (int i = 0; i < 16; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.8f + (float)rng.NextDouble() * 1.6f;
                Blob(s, crownAt + new Vector3(Mathf.Cos(a) * r, -0.3f, Mathf.Sin(a) * r * 0.6f), new Vector3(0.22f, 0.06f, 0.22f), silver);
            }
            Tentacle(s, new Vector3(-7.5f, -1f, 10f), 90f, "Tentacle_Idle", 1.4f, 1f);
            Tentacle(s, new Vector3(9f, -1f, 22f), 230f, "Tentacle_Attack2", 0.3f, 1.3f);
            Sun(s, Quaternion.Euler(70f, 20f, 0f), new Color(0.55f, 0.85f, 0.95f), 0.9f);
            Sun(s, Quaternion.Euler(-20f, 160f, 0f), new Color(0.2f, 0.35f, 0.5f), 0.35f);
            s.cam.fieldOfView = 46f;
            Aim(s, new Vector3(-0.5f, 2.2f, -1.5f), new Vector3(1.5f, 1.8f, 14f));
            s.sky = Underwater;
            s.haze = 0.28f;
            s.hazeColor = new Color(0.06f, 0.24f, 0.32f);
            s.tint = new Color(0.82f, 1f, 1.02f);
            s.vignette = 0.65f;
        }

        /// <summary>2. Praia de ossos ao entardecer: esqueletos caídos, espadas na areia e cascos quebrados.</summary>
        static void FrameBones(Stage s)
        {
            Sea(s, new Color(0.16f, 0.32f, 0.42f), -0.2f, 400f, 0.18f, 2);
            Ground(s, new Color(0.78f, 0.66f, 0.46f), 0.05f, 60f, 0.35f, 5, 0.6f, new Vector3(0f, 0f, -14f));
            Kenney("ship-wreck", s.root, new Vector3(-7f, -2.6f, 18f), Quaternion.Euler(4f, 130f, 14f), 1.4f, "Casco1");
            Kenney("ship-wreck", s.root, new Vector3(12f, -3f, 32f), Quaternion.Euler(-6f, 40f, -18f), 1.5f, "Casco2");
            Character(s, KitCharacters.Skeleton, null, "Death", 1f, new Vector3(-0.4f, 0.55f, 1.9f), 120f);
            Character(s, KitCharacters.HeadlessSkeleton, null, "Death", 1f, new Vector3(2.6f, 0.55f, 3.4f), 230f);
            Character(s, KitCharacters.Skeleton, null, "Death", 1f, new Vector3(4.6f, 0.55f, 1.4f), 20f);
            var sword = QuaterniusProp(s, "Weapon_Cutlass", new Vector3(0.6f, 0.3f, 1.2f), Quaternion.Euler(0f, 40f, 90f), 1f);
            QuaterniusProp(s, "Weapon_Sword_1", new Vector3(3.9f, 0.6f, 3.2f), Quaternion.Euler(20f, 0f, 160f), 1f);
            Kenney("barrel", s.root, new Vector3(-4.2f, 0.1f, 5f), Quaternion.Euler(80f, 30f, 0f), 1f, "Barril");
            Sun(s, Quaternion.Euler(14f, 200f, 0f), new Color(1f, 0.66f, 0.4f), 1.6f);
            Sun(s, Quaternion.Euler(40f, 20f, 0f), new Color(0.45f, 0.4f, 0.65f), 0.45f);
            Aim(s, new Vector3(0.5f, 1.7f, -2.6f), new Vector3(2f, 0.4f, 4f));
            s.sky = (u, v) => Sky(u, v, new Color(0.98f, 0.62f, 0.32f), new Color(0.36f, 0.22f, 0.4f), 0.45f, 0f);
            s.tint = new Color(1.04f, 0.94f, 0.88f);
        }

        /// <summary>3. Cabine à luz de vela: o capitão e Lázaro Negro debruçados sobre o mapa.</summary>
        static void FrameCabin(Stage s)
        {
            var wood = GetMaterial("M_Prologo_Madeira", new Color(0.36f, 0.22f, 0.13f), 0.2f);
            var dark = GetMaterial("M_Prologo_MadeiraEscura", new Color(0.2f, 0.12f, 0.08f), 0.1f);
            Box(s, new Vector3(0f, -0.05f, 0f), new Vector3(14f, 0.1f, 14f), dark);
            Box(s, new Vector3(0f, 2.5f, 4f), new Vector3(14f, 5f, 0.2f), dark);
            Box(s, new Vector3(0f, 0.75f, 0.6f), new Vector3(2.6f, 0.12f, 1.5f), wood);
            Box(s, new Vector3(0f, 0.36f, 0.6f), new Vector3(0.3f, 0.7f, 0.3f), wood);
            var paper = GetMaterial("M_Prologo_Mapa", new Color(0.86f, 0.77f, 0.58f), 0.05f);
            Box(s, new Vector3(0.1f, 0.82f, 0.6f), new Vector3(1.4f, 0.01f, 0.95f), paper, Quaternion.Euler(0f, 8f, 0f));
            var ink = GetMaterial("M_Prologo_Tinta", new Color(0.42f, 0.18f, 0.1f), 0.05f);
            Box(s, new Vector3(0.35f, 0.83f, 0.5f), new Vector3(0.22f, 0.012f, 0.04f), ink, Quaternion.Euler(0f, 45f, 0f));
            Box(s, new Vector3(0.35f, 0.83f, 0.5f), new Vector3(0.22f, 0.012f, 0.04f), ink, Quaternion.Euler(0f, -45f, 0f));
            Kenney("bottle", s.root, new Vector3(0.75f, 0.81f, 1.05f), Quaternion.identity, 0.45f, "Garrafa");
            // Vela: cilindro claro e a chama acesa (luz quente forte, só ela ilumina a cena).
            Box(s, new Vector3(-0.55f, 0.92f, 0.35f), new Vector3(0.07f, 0.18f, 0.07f), GetMaterial("M_Prologo_Cera", new Color(0.92f, 0.88f, 0.78f), 0.1f));
            Glow(s, new Vector3(-0.55f, 1.06f, 0.35f), new Color(1f, 0.72f, 0.35f), 0.06f, 3.2f, 7f);
            Character(s, PlayerKitModel, null, "Idle", 0.3f, new Vector3(-0.15f, 0f, -0.55f), 15f);
            var lazaro = Character(s, KitCharacters.Captain, CivilianMaterial(KitCharacters.Captain, 2), "Idle", 0.7f, new Vector3(0.35f, 0f, 1.7f), 195f);
            // O papagaio é só do capitão.
            foreach (var r in lazaro.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Ernest") r.enabled = false;
            Sun(s, Quaternion.Euler(30f, 120f, 0f), new Color(0.35f, 0.45f, 0.75f), 0.25f);
            Aim(s, new Vector3(-3.3f, 1.65f, 0.4f), new Vector3(0.1f, 1.15f, 0.6f));
            s.cam.fieldOfView = 38f;
            s.sky = (u, v) => new Color(0.05f, 0.03f, 0.02f);
            s.tint = new Color(1.08f, 0.92f, 0.78f);
            s.vignette = 0.75f;
        }

        /// <summary>4. A traição: o Tormenta de velas pretas despeja uma bordada no navio do capitão, de noite.</summary>
        static void FrameBattle(Stage s)
        {
            Sea(s, new Color(0.05f, 0.1f, 0.17f), 0f, 600f, 0.3f, 7);
            var tormenta = Kenney("ship-pirate-large", s.root, new Vector3(-7f, -0.9f, 12f), Quaternion.Euler(0f, 160f, 4f), 1.15f, "Tormenta");
            Kenney("ship-pirate-medium", s.root, new Vector3(13f, -1.1f, 26f), Quaternion.Euler(0f, 200f, 9f), 1f, "Andorinha");
            var flash = GetEmissiveMaterial("M_Prologo_Clarao", new Color(1f, 0.75f, 0.35f), 4f);
            var smoke = GetMaterial("M_Prologo_Fumaca", new Color(0.62f, 0.62f, 0.66f), 0f);
            var foam = GetMaterial("M_Prologo_Espuma", new Color(0.82f, 0.88f, 0.92f), 0.2f);
            for (int i = 0; i < 5; i++)
            {
                Vector3 muzzle = tormenta.transform.TransformPoint(new Vector3(-2.4f, 1.5f, -3.2f + i * 1.6f));
                Vector3 outward = -tormenta.transform.right;
                Blob(s, muzzle + outward * 0.5f, new Vector3(0.6f, 0.6f, 0.6f), flash).transform.localScale = new Vector3(1.2f, 0.5f, 0.5f);
                Blob(s, muzzle + outward * 1.6f + Vector3.up * 0.3f, Vector3.one * 0.7f, smoke);
            }
            Glow(s, tormenta.transform.position - tormenta.transform.right * 4f + Vector3.up * 2f, new Color(1f, 0.6f, 0.3f), 0f, 10f, 24f);
            // Acertos no navio do capitão: clarão no costado e colunas de água em volta.
            Glow(s, new Vector3(11.5f, 1.4f, 25f), new Color(1f, 0.55f, 0.25f), 0.6f, 6f, 10f);
            foreach (var at in new[] { new Vector3(9.5f, 0.6f, 22f), new Vector3(10.5f, 0.8f, 29f), new Vector3(8f, 0.5f, 26.5f) })
                Blob(s, at, new Vector3(0.7f, 2.6f, 0.7f), foam);
            Sun(s, Quaternion.Euler(35f, -30f, 0f), new Color(0.45f, 0.55f, 0.85f), 0.55f);
            Aim(s, new Vector3(-1f, 2.6f, -9f), new Vector3(3f, 3f, 20f));
            s.sky = NightSky;
            s.tint = new Color(0.9f, 0.95f, 1.08f);
        }

        /// <summary>5. O navio do capitão em chamas, afundando de proa; o Tormenta vai embora ao fundo.</summary>
        static void FrameBurning(Stage s)
        {
            Sea(s, new Color(0.05f, 0.09f, 0.15f), 0f, 600f, 0.3f, 11);
            var ship = Kenney("ship-pirate-medium", s.root, new Vector3(0f, -1.3f, 14f), Quaternion.Euler(12f, 60f, 20f), 1f, "Andorinha");
            Kenney("ship-pirate-large", s.root, new Vector3(-24f, -0.9f, 62f), Quaternion.Euler(0f, 250f, 0f), 1f, "Tormenta");
            var fire = GetEmissiveMaterial("M_Prologo_Fogo", new Color(1f, 0.42f, 0.08f), 3.2f);
            var core = GetEmissiveMaterial("M_Prologo_FogoClaro", new Color(1f, 0.85f, 0.45f), 3.5f);
            var smoke = GetMaterial("M_Prologo_FumacaEscura", new Color(0.32f, 0.28f, 0.27f), 0f);
            var rng = new System.Random(9);
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = ship.transform.TransformPoint(new Vector3(((float)rng.NextDouble() - 0.5f) * 2.4f, 1.4f + (float)rng.NextDouble() * 1.2f, -3.4f + i * 0.75f));
                float tall = 2f + (float)rng.NextDouble() * 1.8f;
                Blob(s, p + Vector3.up * (0.8f + tall * 0.4f), new Vector3(0.6f, tall, 0.6f), fire);
                Blob(s, p + Vector3.up * (0.8f + tall * 0.2f), new Vector3(0.3f, tall * 0.5f, 0.3f), core);
            }
            Glow(s, ship.transform.position + Vector3.up * 3f, new Color(1f, 0.5f, 0.2f), 0f, 9f, 30f);
            Sun(s, Quaternion.Euler(30f, -20f, 0f), new Color(0.4f, 0.48f, 0.8f), 0.4f);
            Aim(s, new Vector3(-4f, 2f, -3f), new Vector3(-2f, 3f, 16f));
            s.sky = (u, v) => Plume(Color.Lerp(NightSky(u, v), new Color(0.6f, 0.25f, 0.1f), Mathf.Clamp01(0.5f - Mathf.Abs(u - 0.55f)) * Mathf.Clamp01(1.2f - v) * 0.7f), u, v, 0.6f, 0.62f);
            s.tint = new Color(1.06f, 0.94f, 0.9f);
        }

        /// <summary>6. Sozinho no mar: o capitão agarrado a uma tábua, com Ernest no ombro, na névoa da madrugada.</summary>
        static void FramePlank(Stage s)
        {
            Sea(s, new Color(0.16f, 0.3f, 0.38f), 0f, 600f, 0.35f, 13);
            var plank = GetMaterial("M_Prologo_Tabua", new Color(0.45f, 0.3f, 0.18f), 0.1f);
            Box(s, new Vector3(0f, 0.12f, 0.55f), new Vector3(2.4f, 0.18f, 0.6f), plank, Quaternion.Euler(0f, 12f, 6f));
            Box(s, new Vector3(-2.6f, 0.02f, 4f), new Vector3(1.6f, 0.14f, 0.4f), plank, Quaternion.Euler(0f, -30f, 0f));
            Kenney("barrel", s.root, new Vector3(3.2f, -0.35f, 6f), Quaternion.Euler(70f, 0f, 20f), 1f, "Barril");
            // Na água até o peito (o mar esconde as pernas), braços sobre a tábua.
            Character(s, PlayerKitModel, null, "Idle", 0.6f, new Vector3(0.1f, -0.5f, 1f), 165f);
            Sun(s, Quaternion.Euler(12f, 110f, 0f), new Color(0.95f, 0.78f, 0.62f), 0.8f);
            Sun(s, Quaternion.Euler(45f, -60f, 0f), new Color(0.55f, 0.65f, 0.8f), 0.5f);
            Aim(s, new Vector3(1.2f, 1.25f, -2.4f), new Vector3(0f, 0.75f, 1f));
            s.cam.fieldOfView = 36f;
            s.sky = (u, v) => Sky(u, v, new Color(0.86f, 0.74f, 0.68f), new Color(0.42f, 0.5f, 0.6f), 0.42f, 0f);
            s.haze = 0.18f;
            s.hazeColor = new Color(0.62f, 0.66f, 0.7f);
            s.saturation = 0.8f;
        }

        /// <summary>7. Praia de Porto Alento ao amanhecer: o Velho Bento acha o capitão desacordado na areia.</summary>
        static void FrameBeach(Stage s)
        {
            Sea(s, new Color(0.2f, 0.55f, 0.6f), -0.25f, 500f, 0.2f, 17);
            Ground(s, new Color(0.86f, 0.75f, 0.52f), 0.05f, 70f, 0.4f, 6, 0.7f, new Vector3(0f, 0f, -26f));
            Character(s, PlayerKitModel, null, "Death", 1f, new Vector3(0.4f, 0.3f, 2.2f), 200f);
            Character(s, KitCharacters.Henry, CivilianMaterial(KitCharacters.Henry, 5), "Duck", 0.7f, new Vector3(-0.9f, 0.3f, 2.8f), 110f);
            Kenney("palm-detailed-bend", s.root, new Vector3(-7f, 0.1f, 11f), Quaternion.Euler(0f, 40f, 0f), 1.3f, "Palmeira");
            Kenney("palm-bend", s.root, new Vector3(6f, 0.1f, 12f), Quaternion.Euler(0f, 200f, 0f), 1.2f, "Palmeira2");
            Kenney("boat-row-small", s.root, new Vector3(4.2f, -0.2f, 4f), Quaternion.Euler(0f, 70f, 8f), 1f, "Bote");
            Kenney("rocks-sand-a", s.root, new Vector3(-4.5f, 0f, 6f), Quaternion.Euler(0f, 30f, 0f), 1f, "Pedra");
            Sun(s, Quaternion.Euler(14f, 60f, 0f), new Color(1f, 0.82f, 0.6f), 1.2f);
            Sun(s, Quaternion.Euler(40f, -120f, 0f), new Color(0.6f, 0.7f, 0.9f), 0.45f);
            Aim(s, new Vector3(-1.5f, 2.6f, -3.8f), new Vector3(0f, 0.5f, 2.6f));
            s.cam.fieldOfView = 42f;
            s.sky = (u, v) => Sky(u, v, new Color(1f, 0.82f, 0.6f), new Color(0.5f, 0.68f, 0.86f), 0.5f, 0f);
            s.tint = new Color(1.04f, 0.98f, 0.92f);
        }

        /// <summary>8. O cais em ruínas e a chalupa remendada; o Almirante Teodoro aponta o barco ao capitão.</summary>
        static void FramePier(Stage s)
        {
            Sea(s, new Color(0.18f, 0.5f, 0.56f), -0.1f, 500f, 0.2f, 21);
            for (int i = 0; i < 4; i++)
            {
                var tilt = i == 2 ? Quaternion.Euler(6f, 0f, -9f) : Quaternion.identity;
                Kenney("structure-platform-dock", s.root, new Vector3(0f, 1.25f - 1.31f * 1.6f, i * 4f), tilt, 1.6f, $"Cais_{i}");
            }
            Kenney("ship-pirate-small", s.root, new Vector3(5.4f, -0.9f, 8f), Quaternion.Euler(0f, 0f, 3f), 1f, "Chalupa");
            Kenney("barrel", s.root, new Vector3(-1.2f, 1.25f, 11.5f), Quaternion.identity, 0.8f, "Barril");
            Kenney("crate", s.root, new Vector3(-1.1f, 1.25f, 1.5f), Quaternion.Euler(0f, 20f, 0f), 0.8f, "Caixote");
            Character(s, KitCharacters.Henry, CivilianMaterial(KitCharacters.Henry, 6), "Wave", 0.5f, new Vector3(0.7f, 1.25f, 7.6f), 200f);
            Character(s, PlayerKitModel, null, "Idle", 0.2f, new Vector3(0f, 1.25f, 5.6f), 20f);
            Sun(s, Quaternion.Euler(35f, 140f, 0f), new Color(1f, 0.92f, 0.78f), 1.2f);
            Sun(s, Quaternion.Euler(30f, -60f, 0f), new Color(0.6f, 0.72f, 0.9f), 0.5f);
            Aim(s, new Vector3(-10f, 4.6f, 1.5f), new Vector3(2.2f, 2f, 7.5f));
            s.sky = (u, v) => Sky(u, v, new Color(0.96f, 0.88f, 0.7f), new Color(0.42f, 0.64f, 0.86f), 0.4f, 0f);
        }

        /// <summary>9. O título: a chalupa saindo para o mar ao pôr do sol (o nome do jogo entra por cima, na tela da abertura).</summary>
        static void FrameTitle(Stage s)
        {
            Sea(s, new Color(0.2f, 0.24f, 0.4f), 0f, 900f, 0.35f, 23);
            Kenney("ship-pirate-small", s.root, new Vector3(-3f, -0.9f, 30f), Quaternion.Euler(0f, 300f, -4f), 1f, "Chalupa");
            Kenney("ship-pirate-large", s.root, new Vector3(60f, -0.9f, 220f), Quaternion.Euler(0f, 80f, 0f), 1f, "Tormenta");
            Sun(s, Quaternion.Euler(6f, 180f, 0f), new Color(1f, 0.6f, 0.32f), 1.4f);
            Sun(s, Quaternion.Euler(40f, 0f, 0f), new Color(0.5f, 0.45f, 0.7f), 0.4f);
            Aim(s, new Vector3(0f, 4f, -6f), new Vector3(0f, 5.5f, 60f));
            s.cam.fieldOfView = 44f;
            s.sky = (u, v) => Sky(u, v, new Color(1f, 0.55f, 0.25f), new Color(0.22f, 0.2f, 0.42f), 0.38f, 0.5f);
            s.tint = new Color(1.06f, 0.94f, 0.88f);
        }

        // ---------- Peças ----------

        static void Aim(Stage s, Vector3 from, Vector3 at)
        {
            s.cam.transform.position = from;
            s.cam.transform.LookAt(at);
        }

        static void Sun(Stage s, Quaternion rotation, Color color, float intensity)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(s.root, false);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>Bolinha brilhante (emissiva) com uma luz de ponto: vela, clarão, prata.</summary>
        static void Glow(Stage s, Vector3 at, Color color, float size, float intensity, float range)
        {
            if (size > 0f) Blob(s, at, Vector3.one * size, GetEmissiveMaterial("M_Prologo_Brilho_" + ColorUtility.ToHtmlStringRGB(color), color, 4f));
            var go = new GameObject("LuzPonto");
            go.transform.SetParent(s.root, false);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>Coroa low-poly: aro de 12 placas e 6 pontas com uma bolinha em cada (a Coroa de Prata).</summary>
        static void Crown(Stage s, Vector3 at, float size, Material material, Quaternion tilt)
        {
            var holder = new GameObject("Coroa").transform;
            holder.SetParent(s.root, false);
            holder.SetPositionAndRotation(at, tilt);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * 360f;
                var plate = Box(s, Vector3.zero, new Vector3(size * 0.56f, size * 0.42f, size * 0.08f), material);
                plate.transform.SetParent(holder, false);
                plate.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                plate.transform.localPosition = plate.transform.localRotation * Vector3.forward * size;
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                var spike = Box(s, Vector3.zero, Vector3.one * size * 0.34f, material);
                spike.transform.SetParent(holder, false);
                spike.transform.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(0f, 0f, 45f);
                spike.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, size * 0.3f, size);
                var ball = Blob(s, Vector3.zero, Vector3.one * size * 0.16f, material);
                ball.transform.SetParent(holder, false);
                ball.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, size * 0.58f, size);
            }
        }

        static GameObject Blob(Stage s, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(s.root, false);
            go.transform.position = at;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Box(Stage s, Vector3 center, Vector3 size, Material material, Quaternion rotation = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(s.root, false);
            go.transform.SetPositionAndRotation(center, rotation == default ? Quaternion.identity : rotation);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Personagem do kit (rig genérico) parado numa pose do próprio kit: o clipe no tempo pedido (fração da duração).</summary>
        static GameObject Character(Stage s, string model, Material material, string clip, float at, Vector3 position, float yaw)
        {
            var holder = new GameObject(model);
            holder.transform.SetParent(s.root, false);
            holder.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var go = KitCharacter(model, holder.transform, material);
            var animation = KitClip(model, clip);
            if (animation) animation.SampleAnimation(go, animation.length * Mathf.Clamp01(at));
            return go;
        }

        static GameObject QuaterniusProp(Stage s, string model, Vector3 position, Quaternion rotation, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{model}.fbx");
            if (!asset) return null;
            var go = (GameObject)Object.Instantiate(asset, s.root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale *= scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = QuaterniusMaterial;
            return go;
        }

        /// <summary>Tentáculo da lula (o mesmo do jogo) parado numa pose das animações do kit, mais escuro (na sombra).</summary>
        static void Tentacle(Stage s, Vector3 position, float yaw, string clip, float at, float scale)
        {
            var holder = new GameObject("Tentaculo");
            holder.transform.SetParent(s.root, false);
            holder.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            holder.transform.localScale = Vector3.one * scale;
            var go = KitCharacter("Characters_Tentacle", holder.transform, SquidMaterial());
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{QuaterniusFolder}/Characters_Tentacle.fbx"))
                if (o is AnimationClip c && c.name.EndsWith(clip)) c.SampleAnimation(go, Mathf.Min(at, c.length));
        }

        /// <summary>Mar low-poly: grade facetada com ondas de seno (cada triângulo com a sua normal, como o resto do jogo).</summary>
        static void Sea(Stage s, Color color, float y, float size, float amplitude, int seed) =>
            Facets(s, "Mar", GetMaterial("M_Prologo_Mar_" + ColorUtility.ToHtmlStringRGB(color), color, 0.3f), y, size, amplitude, seed, 2.2f, Vector3.zero);

        static void Ground(Stage s, Color color, float y, float size, float amplitude, int seed, float cell, Vector3 offset = default) =>
            Facets(s, "Chao", GetMaterial("M_Prologo_Chao_" + ColorUtility.ToHtmlStringRGB(color), color, 0.05f), y, size, amplitude, seed, cell, offset);

        static void Facets(Stage s, string name, Material material, float y, float size, float amplitude, int seed, float cell, Vector3 offset)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(size / cell), 8, 220);
            // Perto da câmera, células menores (o detalhe fica na frente): a grade cresce com a distância.
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float H(float x, float z) => amplitude * (Mathf.Sin(x * 0.42f + seed) * 0.5f + Mathf.Sin(z * 0.31f + x * 0.17f + seed * 1.7f) * 0.35f +
                                                      Mathf.Sin(z * 0.9f - x * 0.6f + seed * 0.3f) * 0.25f);
            float half = size * 0.5f;
            Vector3 P(int i, int j)
            {
                float u = (i / (float)n) * 2f - 1f, v = j / (float)n;
                float x = Mathf.Sign(u) * u * u * half;
                float z = -30f + v * v * size;
                return new Vector3(x, H(x, z), z) + offset;
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    Vector3 a = P(i, j), b = P(i + 1, j), c = P(i, j + 1), d = P(i + 1, j + 1);
                    int k = verts.Count;
                    verts.Add(a); verts.Add(c); verts.Add(b);
                    verts.Add(b); verts.Add(c); verts.Add(d);
                    for (int t = 0; t < 6; t++) tris.Add(k + t);
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(s.root, false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // ---------- Céus pintados (u, v de 0 a 1; v de baixo para cima) ----------

        /// <summary>Do horizonte (cor quente) ao alto; sol baixo opcional (brilho em volta de u = 0,5 na linha do horizonte).</summary>
        static Color Sky(float u, float v, Color horizon, Color top, float line, float sun)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - line) / (1f - line)));
            var c = Color.Lerp(horizon, top, Mathf.Pow(t, 0.7f));
            if (sun > 0f)
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) * 3.2f + (v - line - 0.04f) * (v - line - 0.04f) * 10f);
                c = Color.Lerp(c, new Color(1f, 0.9f, 0.6f), sun * Mathf.Clamp01(1f - d * 2.2f));
            }
            // Nuvens: faixas suaves de ruído.
            float cloud = Mathf.PerlinNoise(u * 4f, v * 12f) * Mathf.PerlinNoise(u * 9f + 3f, v * 5f);
            return Color.Lerp(c, Color.Lerp(c, Color.white, 0.35f), Mathf.Clamp01(cloud * 2.2f - 0.7f) * t);
        }

        /// <summary>Coluna de fumaça pintada no céu, saindo de (x0, base) e indo para a direita com o vento; embaixo, pega a luz do fogo.</summary>
        static Color Plume(Color sky, float u, float v, float x0, float baseV)
        {
            if (v < baseV - 0.05f) return sky;
            float h = v - baseV;
            float center = x0 + h * 0.35f + Mathf.Sin(h * 9f) * 0.02f;
            float width = 0.05f + h * 0.22f;
            float body = Mathf.Clamp01(1f - Mathf.Abs(u - center) / width);
            float puffs = Mathf.PerlinNoise(u * 18f, v * 9f - h * 4f) * 0.7f + Mathf.PerlinNoise(u * 6f, v * 4f) * 0.5f;
            float density = Mathf.Clamp01(body * 1.6f * puffs - 0.15f) * Mathf.Clamp01(1.2f - h * 1.6f);
            var smoke = Color.Lerp(new Color(0.75f, 0.36f, 0.14f), new Color(0.36f, 0.33f, 0.36f), Mathf.Clamp01(h * 4f));
            return Color.Lerp(sky, smoke, density);
        }

        static Color NightSky(float u, float v)
        {
            var c = Color.Lerp(new Color(0.1f, 0.14f, 0.24f), new Color(0.02f, 0.03f, 0.08f), Mathf.Clamp01((v - 0.4f) / 0.6f));
            // Lua cheia com halo, à direita.
            float d = Mathf.Sqrt((u - 0.78f) * (u - 0.78f) * 3.2f + (v - 0.8f) * (v - 0.8f));
            c = Color.Lerp(c, new Color(0.35f, 0.42f, 0.6f), Mathf.Clamp01(0.28f - d) * 2f);
            if (d < 0.05f) c = new Color(0.92f, 0.94f, 0.86f);
            // Estrelas: pontos de hash.
            int px = Mathf.FloorToInt(u * 800f), py = Mathf.FloorToInt(v * 450f);
            uint h = (uint)(px * 73856093 ^ py * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            if ((h & 1023) < 3 && v > 0.45f) c = Color.Lerp(c, Color.white, 0.6f + (h >> 20 & 3) * 0.1f);
            return c;
        }

        static Color Underwater(float u, float v)
        {
            var c = Color.Lerp(new Color(0.02f, 0.07f, 0.11f), new Color(0.1f, 0.38f, 0.46f), Mathf.Pow(v, 1.4f));
            // Raios de luz vindo de cima, inclinados.
            float ray = Mathf.PerlinNoise((u + v * 0.35f) * 9f, 0.5f);
            return Color.Lerp(c, new Color(0.45f, 0.75f, 0.78f), Mathf.Clamp01(ray * 1.6f - 0.85f) * v * 0.9f);
        }

        // ---------- Recorte, pintura e acabamento ----------

        /// <summary>Objetos por cima do céu pintado: alfa = 1 − (branco − preto), como no retrato.</summary>
        static Color[] Composite(Stage s, Color[] black, Color[] white)
        {
            var result = new Color[black.Length];
            for (int y = 0; y < PrologueHeight; y++)
                for (int x = 0; x < PrologueWidth; x++)
                {
                    int k = y * PrologueWidth + x;
                    Color b = black[k], w = white[k];
                    float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                    Color fg = alpha > 0.01f ? b / alpha : Color.black;
                    Color sky = s.sky(x / (float)PrologueWidth, y / (float)PrologueHeight);
                    if (s.haze > 0f) fg = Color.Lerp(fg, s.hazeColor, s.haze);
                    var c = Color.Lerp(sky, fg, alpha);
                    c.a = 1f;
                    result[k] = c;
                }
            return result;
        }

        /// <summary>
        /// Cara de pintura: filtro de Kuwahara (em cada pixel, a média do quadrante mais liso em volta: borra as texturas e mantém as
        /// bordas, como pinceladas), cor da cena (tom, contraste, saturação), vinheta, grão de papel e borda queimada.
        /// </summary>
        static void Paint(Stage s, Color[] px)
        {
            int w = PrologueWidth, h = PrologueHeight, r = s.paintRadius;
            int sw = w + 1;
            var sr = new double[sw * (h + 1)];
            var sg = new double[sr.Length];
            var sb = new double[sr.Length];
            var sl = new double[sr.Length];
            var sl2 = new double[sr.Length];
            for (int y = 0; y < h; y++)
            {
                double rr = 0, gg = 0, bb = 0, ll = 0, l2 = 0;
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
                    rr += c.r; gg += c.g; bb += c.b; ll += l; l2 += l * l;
                    int k = (y + 1) * sw + x + 1, up = y * sw + x + 1;
                    sr[k] = sr[up] + rr; sg[k] = sg[up] + gg; sb[k] = sb[up] + bb; sl[k] = sl[up] + ll; sl2[k] = sl2[up] + l2;
                }
            }
            double Sum(double[] a, int x0, int y0, int x1, int y1) => a[(y1 + 1) * sw + x1 + 1] - a[y0 * sw + x1 + 1] - a[(y1 + 1) * sw + x0] + a[y0 * sw + x0];

            var paper = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    paper[y * w + x] = Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 0.6f + Mathf.PerlinNoise(x * 0.2f + 50f, y * 0.2f) * 0.4f;

            var output = new Color[px.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double best = double.MaxValue;
                    Color mean = px[y * w + x];
                    for (int q = 0; q < 4; q++)
                    {
                        int x0 = q % 2 == 0 ? x - r : x, x1 = q % 2 == 0 ? x : x + r;
                        int y0 = q < 2 ? y - r : y, y1 = q < 2 ? y : y + r;
                        x0 = Mathf.Clamp(x0, 0, w - 1); x1 = Mathf.Clamp(x1, 0, w - 1);
                        y0 = Mathf.Clamp(y0, 0, h - 1); y1 = Mathf.Clamp(y1, 0, h - 1);
                        double n = (x1 - x0 + 1) * (y1 - y0 + 1);
                        double ml = Sum(sl, x0, y0, x1, y1) / n;
                        double variance = Sum(sl2, x0, y0, x1, y1) / n - ml * ml;
                        if (variance >= best) continue;
                        best = variance;
                        mean = new Color((float)(Sum(sr, x0, y0, x1, y1) / n), (float)(Sum(sg, x0, y0, x1, y1) / n), (float)(Sum(sb, x0, y0, x1, y1) / n));
                    }

                    var c = mean * s.tint;
                    // Contorno de tinta: borda forte da imagem original (Sobel na luminância) escurece.
                    if (s.ink > 0f && x > 0 && y > 0 && x < w - 1 && y < h - 1)
                    {
                        float L(int xx, int yy) { var p = px[yy * w + xx]; return 0.299f * p.r + 0.587f * p.g + 0.114f * p.b; }
                        float gx = L(x + 1, y - 1) + 2f * L(x + 1, y) + L(x + 1, y + 1) - L(x - 1, y - 1) - 2f * L(x - 1, y) - L(x - 1, y + 1);
                        float gy = L(x - 1, y + 1) + 2f * L(x, y + 1) + L(x + 1, y + 1) - L(x - 1, y - 1) - 2f * L(x, y - 1) - L(x + 1, y - 1);
                        float edgeInk = Mathf.SmoothStep(0.12f, 0.45f, Mathf.Sqrt(gx * gx + gy * gy));
                        c = Color.Lerp(c, c * 0.35f, edgeInk * s.ink);
                    }
                    float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                    c = Color.Lerp(new Color(lum, lum, lum), c, s.saturation);
                    c = (c - new Color(0.5f, 0.5f, 0.5f)) * s.contrast + new Color(0.5f, 0.5f, 0.5f);

                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float dist = Mathf.Sqrt(u * u + v * v * 0.8f) / 0.62f;
                    c *= 1f - s.vignette * Mathf.SmoothStep(0.45f, 1f, dist);

                    float grain = paper[y * w + x];
                    // Papel: manchas grandes e fibras; pinceladas: listras diagonais leves.
                    float stroke = Mathf.PerlinNoise((x + y) * 0.09f, (x - y) * 0.012f);
                    c *= 0.88f + 0.16f * grain + 0.06f * (stroke - 0.5f);
                    // Papel quente nas partes claras (como tinta sobre pergaminho).
                    c = Color.Lerp(c, c * new Color(1.04f, 0.98f, 0.88f), 0.6f);

                    // Borda queimada: irregular, escurece para marrom.
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    float rag = 14f + 22f * Mathf.PerlinNoise(x * 0.02f + y * 0.02f, (x + y) * 0.013f);
                    float burn = 1f - Mathf.Clamp01(edge / rag);
                    c = Color.Lerp(c, new Color(0.16f, 0.09f, 0.05f), burn * burn);

                    c.a = 1f;
                    output[y * w + x] = c;
                }
            Array.Copy(output, px, px.Length);
        }
    }
}
