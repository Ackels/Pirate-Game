using System.Linq;
using Pirates.CameraSystem;
using Pirates.Controls;
using Pirates.Ocean;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Gera as cenas de protótipo com primitivas: oceano, navio do jogador,
    /// ilhas para colisão/referência, boias flutuantes, luz, fog, câmera e HUD.
    /// A cena 02 acrescenta canhões, sistemas de combate e alvos (PrototypeSceneBuilder.Combat.cs).
    /// Rodar de novo recria a cena (os assets de dados existentes são preservados).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string DataFolder = Root + "/Data";
        const string MaterialsFolder = Root + "/Materials";
        const string ScenesFolder = Root + "/Scenes";
        const string NavigationScenePath = ScenesFolder + "/Proto01_Navegacao.unity";
        const string CombatScenePath = ScenesFolder + "/Proto02_CombateNaval.unity";
        const string DuelScenePath = ScenesFolder + "/Proto03_Duelo.unity";
        const string LootScenePath = ScenesFolder + "/Proto04_Loot.unity";
        const string HarborScenePath = ScenesFolder + "/Proto05_Base.unity";

        [MenuItem("Pirates/Protótipo/01 - Criar cena de Navegação")]
        public static void BuildNavigation() => Build(NavigationScenePath, null);

        [MenuItem("Pirates/Protótipo/02 - Criar cena de Combate Naval")]
        public static void BuildNavalCombat() => Build(CombatScenePath, CombatScenario.TargetPractice);

        [MenuItem("Pirates/Protótipo/03 - Criar cena de Duelo (IA)")]
        public static void BuildDuel() => Build(DuelScenePath, CombatScenario.Duel);

        [MenuItem("Pirates/Protótipo/04 - Criar cena de Loot (duelo + carga)")]
        public static void BuildLoot() => Build(LootScenePath, CombatScenario.LootHunt);

        [MenuItem("Pirates/Protótipo/05 - Criar cena do Porto-base")]
        public static void BuildHarbor() => Build(HarborScenePath, CombatScenario.Harbor);

        static void Build(string scenePath, CombatScenario? scenario)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root, "Data");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Scenes");
            generatingScenePath = scenePath;

            var waves = LoadOrCreateAsset<WaveSettings>(DataFolder + "/WaveSettings_Default.asset");
            var sloop = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Chalupa.asset", s => PlayerShipHandling(s, SloopTurnRate));

            var water = GetMaterial("M_Water", new Color(0.04f, 0.2f, 0.28f), 0.93f);
            var hull = GetMaterial("M_HullWood", new Color(0.33f, 0.2f, 0.11f), 0.25f);
            var deck = GetMaterial("M_DeckWood", new Color(0.52f, 0.37f, 0.22f), 0.2f);
            var sail = GetMaterial("M_Sail", new Color(0.9f, 0.86f, 0.76f), 0.05f);
            var sand = GetMaterial("M_Sand", new Color(0.83f, 0.75f, 0.55f), 0.1f);
            var grass = GetMaterial("M_Vegetation", new Color(0.2f, 0.36f, 0.16f), 0.15f);
            var rock = GetMaterial("M_Rock", new Color(0.38f, 0.37f, 0.35f), 0.2f);
            var buoyMat = GetMaterial("M_Buoy", new Color(0.75f, 0.12f, 0.08f), 0.4f);
            var combat = scenario.HasValue ? LoadCombatAssets() : null;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEnvironment();
            var ocean = CreateOcean(water, waves);
            var ship = CreatePlayerShip(sloop);
            CreateIslands(sand, grass, rock);
            CreateBuoyChannel(buoyMat);
            var camera = CreateCamera(ship.transform);
            ocean.Configure(waves, camera.transform);
            // Prefabs são gerados dentro da cena nova (objetos temporários não sujam a cena anterior).
            var loot = scenario is CombatScenario.LootHunt or CombatScenario.Harbor or CombatScenario.World ? LoadLootAssets(deck, sail) : null;
            if (combat != null) AddNavalCombat(combat, scenario.Value, ship, sloop, deck, loot);
            if (scenario is CombatScenario.Harbor or CombatScenario.World) AddHarbor(ship, loot.items, combat, deck, hull);
            if (loot != null) AddSandbox(ship);
            CreateGameHud();
            var hudObject = new GameObject("HUD");
            hudObject.AddComponent<ShipDebugHUD>().Bind(ship, HudTitle(scenario));
            // Fama do capitão (M10): painel da tecla C nas cenas com porto.
            if (scenario is CombatScenario.Harbor or CombatScenario.World) AddFameHud(hudObject);
            // Mundo (M5): a terra, o tesouro e as missões em cima da cena do porto.
            if (scenario == CombatScenario.World) AddWorld(ship, combat, loot, deck, hull);
            // Áudio por último: acha navios, personagens, fogueiras e gaivotas já na cena.
            AddAudio(camera, ship);
            AddVfx();
            BakeShoreMap(ocean, scenePath);
            OptimizeSceneRendering();

            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            // Antes de reabrir: a cena nova carrega as classes do disco junto (descarregar depois quebra a referência da cena).
            ReloadShipClasses();

            // A cena recém-gerada em memória perde referências a ScriptableObjects ao entrar em Play
            // (a versão salva está correta). Reabrir do disco garante que o editor use a versão salva.
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("PlayerShip");
            Debug.Log($"[Pirates] Cena criada em {scenePath}. Aperte Play.");
        }

        static string HudTitle(CombatScenario? scenario) => scenario switch
        {
            CombatScenario.TargetPractice => "Combate Naval — Protótipo 02",
            CombatScenario.Duel => "Duelo — Protótipo 03",
            CombatScenario.LootHunt => "Loot — Protótipo 04",
            CombatScenario.Harbor => "Porto-base — Protótipo 05",
            CombatScenario.World => "Mundo — Protótipo 07",
            _ => "Navegação — Protótipo 01",
        };

        // ---------- Ambiente ----------

        static void CreateEnvironment()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.64f, 0.75f, 0.83f);
            // Dia e noite (M11): o ciclo move este sol (de noite, a lua) e acompanha o resto da luz.
            CreateDayNight(sun);
            CreateWind();
            CreateWeather();
        }

        static OceanSurface CreateOcean(Material water, WaveSettings waves)
        {
            var go = new GameObject("Ocean");
            var ocean = go.AddComponent<OceanSurface>();
            var renderer = go.GetComponent<MeshRenderer>();
            // Ondas na GPU (Ocean.shader: o URP Lit com o vértice na altura da onda); o material guarda as cores e o brilho.
            var oceanShader = Shader.Find("Pirates/Ocean");
            if (oceanShader && water.shader != oceanShader)
            {
                water.shader = oceanShader;
                EditorUtility.SetDirty(water);
            }
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ocean.Configure(waves, null);
            // Cena nova: as ilhas criadas daqui em diante entram no mapa da costa (BakeShoreMap, no fim do build).
            sceneIslands.Clear();
            return ocean;
        }

        // ---------- Navio ----------

        static ShipController CreatePlayerShip(ShipStats stats)
        {
            var controller = CreateShip("PlayerShip", new Vector3(0f, 0.3f, 0f), Quaternion.identity, stats, SloopHull(PlayerShipModel));
            controller.gameObject.AddComponent<PlayerShipInput>();
            return controller;
        }

        /// <summary>Casco: o modelo do Kenney e a física em volta dele (colisor, massa, inércia, pontos de flutuação, canhões).</summary>
        sealed class ShipHull
        {
            public string model;
            public float mass;
            public Vector3 colliderCenter, colliderSize, inertia;
            public Vector3[] floatPoints;
            /// <summary>Canhões de cada bordo espalhados entre estes Z (espaço do navio).</summary>
            public float cannonZMin, cannonZMax;
        }

        /// <summary>
        /// Chalupa (8,8 m com o gurupés, 4,8 m com as bordas). A inércia é a do casco antigo (caixa 3,2 × 1,6 × 10 m em volta
        /// do centro de massa da ShipStats, medida em Play): o colisor do modelo não muda o balanço "pesado" já aprovado.
        /// </summary>
        static ShipHull SloopHull(string model) => new()
        {
            model = model,
            mass = 6000f,
            colliderCenter = new Vector3(0f, 0.5f, 0.2f),
            colliderSize = new Vector3(4.2f, 2.2f, 8.4f),
            inertia = new Vector3(56140f, 55120f, 11260f),
            floatPoints = new[]
            {
                new Vector3(-1.4f, -0.5f, 4f), new Vector3(1.4f, -0.5f, 4f),
                new Vector3(-1.5f, -0.5f, 0f), new Vector3(1.5f, -0.5f, 0f),
                new Vector3(-1.4f, -0.5f, -4f), new Vector3(1.4f, -0.5f, -4f),
                new Vector3(0f, -0.5f, 5.2f), new Vector3(0f, -0.5f, -4.8f),
            },
            cannonZMin = -2.4f,     // popa elevada
            cannonZMax = 1.6f,      // castelo de proa
        };

        /// <summary>
        /// Brigue (ship-large, 13,1 m): 1,5× a massa e a mesma receita da chalupa esticada para o comprimento dele (inércia
        /// de uma caixa 3,2 × 1,6 × 13 m em volta do mesmo centro de massa): balança e gira mais devagar.
        /// </summary>
        static ShipHull BrigHull(string model) => new()
        {
            model = model,
            mass = 9000f,
            colliderCenter = new Vector3(0f, 0.5f, 0f),
            colliderSize = new Vector3(4.2f, 2.2f, 12.4f),
            inertia = new Vector3(135960f, 134430f, 16890f),
            floatPoints = new[]
            {
                new Vector3(-1.4f, -0.5f, 5.2f), new Vector3(1.4f, -0.5f, 5.2f),
                new Vector3(-1.5f, -0.5f, 0f), new Vector3(1.5f, -0.5f, 0f),
                new Vector3(-1.4f, -0.5f, -5.2f), new Vector3(1.4f, -0.5f, -5.2f),
                new Vector3(0f, -0.5f, 6.6f), new Vector3(0f, -0.5f, -6.2f),
            },
            cannonZMin = -3.5f,     // popa elevada (mais para trás o castelo de popa fecha o convés)
            cannonZMax = 3.0f,      // castelo de proa, onde o casco já afina
        };

        /// <summary>
        /// Casco com física, flutuação e controle, sem fonte de comando (o chamador adiciona jogador ou IA). O visual é um
        /// navio do Kenney Pirate Kit (<paramref name="hull"/>), com as velas que sobem e descem com o nível de vela.
        /// </summary>
        static ShipController CreateShip(string name, Vector3 position, Quaternion rotation, ShipStats stats, ShipHull hull)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);

            var body = root.AddComponent<Rigidbody>();
            body.mass = hull.mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 1.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = hull.colliderCenter;
            collider.size = hull.colliderSize;
            body.automaticInertiaTensor = false;
            body.inertiaTensor = hull.inertia;
            body.inertiaTensorRotation = Quaternion.identity;

            root.AddComponent<Buoyancy>().Configure(hull.floatPoints, depth: 1.2f, horizontalWaterDrag: 0f);

            var controller = root.AddComponent<ShipController>();
            controller.Configure(stats);

            // Visual (sem colisores): o modelo com a quilha abaixo da linha d'água.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var model = Kenney(hull.model, visual, new Vector3(0f, ShipModelKeel, 0f), objectName: "Casco");
            root.AddComponent<SailVisual>().Configure(controller, FurlableSails(model.transform));
            // Lanterna da popa (M11): o navio do jogador ilumina o convés à noite; os outros só brilham (acha-se o inimigo no escuro).
            ShipLantern(root.transform, hull, realLight: name == "PlayerShip");

            return controller;
        }

        // ---------- Mundo ----------

        /// <summary>
        /// Ilhas do mar (cenas 01–05 e 07), geradas com litoral irregular (PrototypeSceneBuilder.Terrain.cs). Os pontos de que
        /// o jogo depende ficam fixos: na Oeste, a praia de desembarque (leste) e o tesouro (sul, depois da pedra grande); na
        /// Sul (porto, PrototypeSceneBuilder.Port.cs), o litoral do píer (norte) e o topo plano do morro (Fortaleza).
        /// </summary>
        static void CreateIslands(Material sand, Material grass, Material rock)
        {
            var parent = new GameObject("Islands").transform;

            // Norte e Oeste 2× (M6.9, PrototypeSceneBuilder.Isles.cs): o forte e o esconderijo entram só no mundo (cena 07).
            var norte = NorthIslandSpec();
            var t = CreateIslandTerrain(parent, norte);
            PlantLush(t, norte, NorthWild, trees: 110, bushes: 100, flowers: 50, grass: 130, ground: 15, rocks: 10, palms: 26, seed: 11);

            var oeste = WestIslandSpec();
            t = CreateIslandTerrain(parent, oeste);
            // A pedra grande da missão do tesouro: entre a praia de desembarque (leste) e a gruta (sul).
            IslandRock(t, "Pedra_Grande", new Vector2(9f, -8f), "rocks-b", 1.5f, 30f);
            PlantLush(t, oeste, WestWild, trees: 70, bushes: 70, flowers: 40, grass: 90, ground: 12, rocks: 8, palms: 24, seed: 23);

            // Porto (M6.4): ilha 2× maior com vila, morro da Fortaleza e mata (PrototypeSceneBuilder.Port.cs).
            var sul = PortIslandSpec();
            t = CreateIslandTerrain(parent, sul);
            portIsland = t;
            PlantLush(t, sul, PortWild, trees: 140, bushes: 160, flowers: 140, grass: 220, ground: 25, rocks: 16, palms: 40, seed: 5);

            // Rochedos soltos para testar manobra fina: pedras do kit, meio afundadas.
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(angle) * 70f, -1.2f, Mathf.Sin(angle) * 70f + 20f);
                var r = Kenney(GrayRocks[i % 3], parent, pos, i * 37f, 2.2f, $"Rochedo_{i}");
                foreach (var filter in r.GetComponentsInChildren<MeshFilter>())
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                foreach (var c in r.GetComponentsInChildren<Transform>()) c.gameObject.isStatic = true;
            }
        }

        static void CreateBuoyChannel(Material buoyMat)
        {
            var parent = new GameObject("Buoys").transform;
            for (int i = 0; i < 8; i++)
            {
                float z = 35f + i * 22f;
                float sway = Mathf.Sin(i * 0.8f) * 10f;
                Buoy(parent, $"Boia_E{i}", new Vector3(sway - 14f, 0f, z), buoyMat);
                Buoy(parent, $"Boia_D{i}", new Vector3(sway + 14f, 0f, z), buoyMat);
            }
        }

        static GameObject Buoy(Transform parent, string name, Vector3 position, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.9f, 0.7f, 0.9f);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var body = go.AddComponent<Rigidbody>();
            body.mass = 80f;
            body.centerOfMass = new Vector3(0f, -0.4f, 0f);
            var buoyancy = go.AddComponent<Buoyancy>();
            buoyancy.Configure(new[]
            {
                new Vector3(-0.35f, -0.6f, -0.35f), new Vector3(0.35f, -0.6f, -0.35f),
                new Vector3(-0.35f, -0.6f, 0.35f), new Vector3(0.35f, -0.6f, 0.35f),
            }, depth: 0.8f, horizontalWaterDrag: 0.8f);
            return go;
        }

        static Camera CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;
            go.AddComponent<AudioListener>();
            go.AddComponent<ElevatedCameraRig>().SetTarget(target);
            // O que fica entre a câmera e o alvo (casas, árvores, fumaça) fica translúcido.
            go.AddComponent<CameraOcclusionFader>();
            go.transform.position = target.position + new Vector3(0f, 30f, -28f);
            go.transform.LookAt(target);
            return camera;
        }

        // ---------- Helpers ----------

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
            Quaternion rotation, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPos, rotation);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Material GetMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Carrega o asset; se não existir, cria com os valores padrão e aplica <paramref name="initialize"/>.</summary>
        static T LoadOrCreateAsset<T>(string path, System.Action<T> initialize = null) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
