using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.OnFoot;
using Pirates.Ocean;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Protótipo 06 (M4.1): só terra. Uma ilha grande (praia em domo, morros, árvores, pedras), uma praia de
    /// desembarque marcada por um bote e o navio do jogador logo ao norte. F desembarca; a pé, F perto do bote volta.
    /// M4.3: armas e bonecos de treino. M4.4: acampamento pirata com quatro piratas de cutelo e o NavMesh da ilha;
    /// morrendo, o jogador acorda na praia. M4.2: mochila, baú do tesouro do acampamento e caixotes/barris quebráveis;
    /// embarcando, a mochila descarrega no porão.
    /// O personagem usa o modelo e as animações do Starter Assets (Unity) se o pacote estiver no projeto;
    /// senão, uma cápsula provisória.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string LandingScenePath = ScenesFolder + "/Proto06_Desembarque.unity";
        const string AnimationFolder = Root + "/Animation";
        const string PlayerAnimatorPath = AnimationFolder + "/PlayerCharacter.controller";

        // Starter Assets – Character Controllers | URP (Unity Technologies, Asset Store). Importado pelo Jhonata.
        const string StarterThirdPerson = "Assets/Starter Assets/Runtime/ThirdPersonController";
        const string StarterModel = StarterThirdPerson + "/Character/Models/Armature.fbx";
        const string StarterController = StarterThirdPerson + "/Character/Animations/StarterAssetsThirdPerson.controller";
        const string StarterPrefab = StarterThirdPerson + "/Prefabs/PlayerArmature.prefab";
        // Centro da ilha grande. Tudo dela (praia, acampamento, caixotes, bonecos) é relativo a ele: na cena 06 fica ao sul
        // do navio; no mundo (cena 07), a leste. Quem gera a cena escolhe antes de criar a ilha.
        static readonly Vector3 Scene06IslandCenter = new(0f, 0f, -150f);
        static Vector3 islandCenter = Scene06IslandCenter;
        static Vector3 BigIslandCenter => islandCenter;
        // A camada do tronco do Animator é refeita uma vez por geração (o controller é o mesmo para todos os personagens).
        static bool animatorLayersBuilt;

        [MenuItem("Pirates/Protótipo/06 - Criar cena de Desembarque (a pé)")]
        public static void BuildLanding()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Root, "Data");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Scenes");
            generatingScenePath = LandingScenePath;

            var waves = LoadOrCreateAsset<WaveSettings>(DataFolder + "/WaveSettings_Default.asset");
            var sloop = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Chalupa.asset", s => PlayerShipHandling(s, SloopTurnRate));
            var water = GetMaterial("M_Water", new Color(0.04f, 0.2f, 0.28f), 0.93f);
            var hull = GetMaterial("M_HullWood", new Color(0.33f, 0.2f, 0.11f), 0.25f);
            var deck = GetMaterial("M_DeckWood", new Color(0.52f, 0.37f, 0.22f), 0.2f);
            var sail = GetMaterial("M_Sail", new Color(0.9f, 0.86f, 0.76f), 0.05f);
            var materials = LoadLandMaterials();
            animatorLayersBuilt = false;
            islandCenter = Scene06IslandCenter;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment();
            var ocean = CreateOcean(water, waves);

            var ship = CreateShip("PlayerShip", new Vector3(0f, 0.3f, -40f), Quaternion.Euler(0f, 180f, 0f), sloop, SloopHull(PlayerShipModel));
            ship.gameObject.AddComponent<PlayerShipInput>();
            ship.gameObject.AddComponent<PlayerInteractInput>();
            var hold = ship.gameObject.AddComponent<Inventory>();
            hold.Configure(SloopHoldCapacity);
            var shipLanding = ship.gameObject.AddComponent<ShipLandingInteraction>();

            var island = CreateCampIsland(materials, LoadLootAssets(deck, sail), deck, hull, "Praia do Norte", "Praia do Sul", trainingDummies: true,
                LandingScenePath);
            var foot = CreateOnFoot(ship, hold, shipLanding, island, materials, "Acampamento pirata", "acampamento");

            var camera = CreateCamera(ship.transform);
            ocean.Configure(waves, camera.transform);

            CreateGameHud();
            var hud = new GameObject("HUD");
            var shipHud = hud.AddComponent<ShipDebugHUD>();
            shipHud.Bind(ship, "Desembarque — Protótipo 06");
            hud.AddComponent<FootHUD>().Bind(foot.switcher, shipLanding, shipHud);
            hud.AddComponent<LandLootHUD>().Bind(foot.switcher, foot.looter, foot.unloader, hold);
            // Barra do chefe a pé (o Barba-Negra nas ruínas; na 07 ela vem com o galeão Tormenta).
            hud.AddComponent<BossBarHUD>();
            var debug = new GameObject("DebugTools");
            debug.AddComponent<DebugSceneReset>();
            debug.AddComponent<HealthBarsHUD>().Ignore(foot.health);
            AddSandbox(ship);
            AddAudio(camera, ship);
            AddVfx();
            BakeShoreMap(ocean, LandingScenePath);
            OptimizeSceneRendering();

            EditorSceneManager.SaveScene(scene, LandingScenePath);
            AddSceneToBuildSettings(LandingScenePath);
            AssetDatabase.SaveAssets();
            // Mesma razão do Build(): a cena salva é a correta para entrar em Play.
            EditorSceneManager.OpenScene(LandingScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("PlayerShip");
            Debug.Log($"[Pirates] Cena criada em {LandingScenePath}. Aperte Play.");
        }

        sealed class LandMaterials
        {
            public Material sand, grass, rock, trunk, leaves, flag, shirt, skin, canvas, blackFlag, fire, iron, wood, brass, sack;
        }

        static LandMaterials LoadLandMaterials() => new()
        {
            sand = GetMaterial("M_Sand", new Color(0.83f, 0.75f, 0.55f), 0.1f),
            grass = GetMaterial("M_Vegetation", new Color(0.2f, 0.36f, 0.16f), 0.15f),
            rock = GetMaterial("M_Rock", new Color(0.38f, 0.37f, 0.35f), 0.2f),
            trunk = GetMaterial("M_Trunk", new Color(0.3f, 0.2f, 0.12f), 0.1f),
            leaves = GetMaterial("M_Leaves", new Color(0.16f, 0.42f, 0.18f), 0.1f),
            flag = GetMaterial("M_Flag", new Color(0.78f, 0.1f, 0.1f), 0.1f),
            shirt = GetMaterial("M_ShirtRed", new Color(0.6f, 0.12f, 0.1f), 0.1f),
            skin = GetMaterial("M_Skin", new Color(0.82f, 0.6f, 0.45f), 0.2f),
            canvas = GetMaterial("M_Canvas", new Color(0.72f, 0.66f, 0.52f), 0.05f),
            blackFlag = GetMaterial("M_FlagBlack", new Color(0.05f, 0.05f, 0.06f), 0.1f),
            fire = GetEmissiveMaterial("M_Fire", new Color(1f, 0.45f, 0.1f), 3f),
            iron = GetMaterial("M_Iron", new Color(0.11f, 0.11f, 0.12f), 0.55f),
            wood = GetMaterial("M_TargetWood", new Color(0.46f, 0.31f, 0.16f), 0.2f),
            brass = GetMaterial("M_Gold", new Color(1f, 0.76f, 0.2f), 0.85f),
            sack = GetMaterial("M_Sack", new Color(0.72f, 0.62f, 0.42f), 0.05f),
        };

        /// <summary>A ilha grande com acampamento, baú, caixotes e o NavMesh (antes dos personagens).</summary>
        sealed class CampIsland
        {
            public LandingPoint landing, southLanding;
            public Transform campSite;
            public LandChest treasure;
            public LandLootAssets loot;
            public List<OutpostSite> outposts;
            public Transform graveyard;
        }

        /// <summary>
        /// Ilha da Caveira (PrototypeSceneBuilder.Skull.cs): terreno, praias, naufrágio, ruínas e postos; o acampamento com o
        /// baú; caixotes e barris; a mata em volta de tudo; e o NavMesh assado (antes dos personagens).
        /// </summary>
        static CampIsland CreateCampIsland(LandMaterials m, LootAssets lootAssets, Material deck, Material hull, string landingName,
            string southLandingName, bool trainingDummies, string scenePath)
        {
            var skull = CreateSkullIsland(deck, hull, landingName, southLandingName);
            var campSite = CreateCampSite(m.fire);
            var loot = LoadLandLootAssets(lootAssets);
            var treasure = CreateTreasureChest(campSite, loot);
            CreateBreakables(campSite, skull, loot);
            PowderKegsAt(campSite, SkullCampKegs);
            if (trainingDummies) CreateTrainingDummies(m.wood, m.sack);
            PlantSkullJungle(skull);
            // NavMesh antes dos personagens: eles não podem entrar como obstáculo.
            BakeIslandNavMesh(scenePath);
            return new CampIsland
            {
                landing = skull.north, southLanding = skull.south, campSite = campSite, treasure = treasure, loot = loot,
                outposts = skull.outposts, graveyard = skull.graveyard,
            };
        }

        /// <summary>O jogador a pé e o que vem com ele (troca navio ↔ terra, mochila, piratas que o perseguem).</summary>
        sealed class OnFootPlayer
        {
            public PlayerModeSwitcher switcher;
            public FootCharacter character;
            public LandLooter looter;
            public BackpackUnloader unloader;
            public Health health;
            public EnemyCamp camp;
            public List<EnemyCamp> outposts;
        }

        /// <summary>
        /// Personagem a pé (armas, vida, mochila, saque), os piratas do acampamento (que perseguem este personagem), a troca
        /// navio ↔ terra com o reaparecer na praia e a mochila que descarrega no porão, e o F a pé (baú, voltar ao navio).
        /// </summary>
        static OnFootPlayer CreateOnFoot(ShipController ship, Inventory hold, ShipLandingInteraction shipLanding, CampIsland island,
            LandMaterials m, string campName, string campId)
        {
            var character = CreateCharacter(m.shirt, m.skin);
            var model = character.transform.Find("Model");
            AddFootCombat(character, model, m.iron, m.wood, m.brass);
            var health = character.gameObject.AddComponent<Health>();
            health.Configure(100f);
            // O jogador não é interrompido ao levar golpe (os piratas são).
            var reactions = character.gameObject.AddComponent<FootHealthReactions>();
            reactions.Configure(model, 0f);
            // Derrubado (revisão de 09/10): levanta rápido da explosão, invencível um instante, e pode levantar rolando (Ctrl).
            reactions.SetQuickRecovery(true);
            character.AllowRollOutOfKnockdown(true);
            // Fôlego (pacote A, passo 6): limita rolar e segurar a guarda. Só o capitão (a IA não tem limite).
            character.gameObject.AddComponent<Stamina>();
            character.gameObject.AddComponent<Inventory>().Configure(BackpackCapacity);
            var looter = character.gameObject.AddComponent<LandLooter>();
            looter.Configure(hold);
            var camp = CreatePirates(island.campSite, character, m.iron, m.wood, m.brass, campName, campId);
            island.treasure.SetLock(camp);
            var outposts = CreateOutpostGuards(island.outposts, character, m.iron, m.wood, m.brass);
            // Esqueletos amaldiçoados (pacote B, passo 3): saem das covas à noite.
            if (island.graveyard) CreateGraveyardSkeletons(island.graveyard, character, m.iron, m.wood, m.brass);

            var switcher = new GameObject("PlayerModes").AddComponent<PlayerModeSwitcher>();
            switcher.Configure(ship, character);
            switcher.gameObject.AddComponent<PlayerFootRespawn>().Configure(switcher);
            var unloader = switcher.gameObject.AddComponent<BackpackUnloader>();
            unloader.Configure(switcher);
            AddFoodEater(character.gameObject, switcher);
            // A pé, F abre o baú, cava o tesouro ou volta ao navio (perto do bote).
            character.gameObject.AddComponent<BoatBoarding>().Configure(switcher);
            character.gameObject.AddComponent<PlayerInteractInput>();
            shipLanding.Configure(switcher);
            return new OnFootPlayer
            {
                switcher = switcher, character = character, looter = looter, unloader = unloader, health = health, camp = camp, outposts = outposts,
            };
        }

        static bool GroundAt(Vector3 xz, out Vector3 ground)
        {
            if (Physics.Raycast(new Vector3(xz.x, 60f, xz.z), Vector3.down, out RaycastHit hit, 120f))
            {
                ground = hit.point;
                return true;
            }
            ground = new Vector3(xz.x, 0f, xz.z);
            return false;
        }

        static void Rowboat(Transform parent, Vector3 position, Material deck, Material hull)
        {
            var b = new GameObject("Bote").transform;
            b.SetParent(parent, false);
            b.SetPositionAndRotation(position, Quaternion.Euler(0f, 20f, 0f));
            GroundAt(position, out Vector3 ground);
            // Bote do Kenney (2 × 3,3 m em 1,4): na areia fica apoiado; na água, um pouco afundado.
            b.position = new Vector3(position.x, Mathf.Max(ground.y, -0.2f), position.z);
            Kenney("boat-row-small", b, Vector3.zero, 0f, 1.4f, "Bote_Modelo");
        }

        /// <summary>
        /// Personagem a pé: CharacterController (pivô nos pés) + FootCharacter + input. O visual "Model" é uma
        /// cápsula provisória; o modelo do Starter Assets entra no lugar dela, com o Animator e o CharacterAnimatorDriver.
        /// </summary>
        static FootCharacter CreateCharacter(Material shirt, Material skin)
        {
            var go = new GameObject("PlayerCharacter");
            var controller = go.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            go.AddComponent<PlayerFootInput>();
            var character = go.AddComponent<FootCharacter>();

            if (!AddStarterAssetsModel(go.transform, character, kitModel: PlayerKitModel))
            {
                var model = new GameObject("Model").transform;
                model.SetParent(go.transform, false);
                Part(PrimitiveType.Capsule, "Corpo", model, new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.6f, 0.55f), Quaternion.identity, shirt);
                Part(PrimitiveType.Sphere, "Cabeca", model, new Vector3(0f, 1.55f, 0f), Vector3.one * 0.4f, Quaternion.identity, skin);
                Part(PrimitiveType.Cube, "Nariz", model, new Vector3(0f, 1.55f, 0.22f), new Vector3(0.1f, 0.1f, 0.15f), Quaternion.identity, skin);
                Debug.LogWarning("[Pirates] Starter Assets não encontrado: personagem com cápsula provisória.");
            }
            return character;
        }

        /// <summary>
        /// Visual humanoide do personagem a pé: um personagem do Quaternius Pirate Kit com o esqueleto adaptado
        /// (<paramref name="kitModel"/>, PrototypeSceneBuilder.Characters.cs) ou, sem ele, o boneco do Starter Assets
        /// (tingido com <paramref name="tint"/>). Animator com o nosso controller (cópia do do pacote, blend tree nas nossas
        /// velocidades), sem root motion (quem move é o FootCharacter) e o CharacterAnimatorDriver passando
        /// Speed/Grounded/Jump/FreeFall.
        /// </summary>
        static bool AddStarterAssetsModel(Transform parent, FootCharacter character, Color? tint = null, string tintSuffix = "_Pirata",
            string kitModel = null)
        {
            var controller = PlayerAnimator(character);
            if (!controller) return false;
            GameObject model;
            if (kitModel != null && HasKitCharacter(kitModel))
            {
                model = HumanoidKitCharacter(kitModel, parent);
                if (kitModel == KitCharacters.Captain) PinParrotToShoulder(model, kitModel);
            }
            else
            {
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StarterModel);
                if (!modelAsset) return false;
                model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                model.transform.SetParent(parent, false);
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                // O FBX vem sem materiais: usa os do prefab PlayerArmature (URP), casando pelo nome do renderer.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefab);
                if (prefab)
                {
                    var source = new System.Collections.Generic.Dictionary<string, Material[]>();
                    foreach (var r in prefab.GetComponentsInChildren<Renderer>(true)) source.TryAdd(r.name, r.sharedMaterials);
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!source.TryGetValue(r.name, out var mats)) continue;
                        // Piratas inimigos: cópias tingidas dos mesmos materiais.
                        if (tint is Color color) mats = System.Array.ConvertAll(mats, m => Tinted(m, color, tintSuffix));
                        r.sharedMaterials = mats;
                    }
                }
            }

            // TryGetComponent: no editor, GetComponent de algo ausente devolve um "null falso" que o ?? não pega.
            if (!model.TryGetComponent(out Animator animator)) animator = model.AddComponent<Animator>();
            if (!animatorLayersBuilt)
            {
                EnsureAttackLayer(controller);
                animatorLayersBuilt = true;
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            model.AddComponent<CharacterAnimatorDriver>().Configure(character);
            return true;
        }

        /// <summary>
        /// Nosso Animator Controller: cópia do StarterAssetsThirdPerson (estados de pulo/queda/aterrissagem já afinados)
        /// com os blend trees de locomoção nas velocidades do FootCharacter (parado 0, andar, correr).
        /// Criado uma vez; depois é nosso para ajustar (o do pacote nunca é alterado).
        /// </summary>
        static AnimatorController PlayerAnimator(FootCharacter character)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorPath);
            if (existing) return existing;
            if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(StarterController)) return null;

            EnsureFolder(Root, "Animation");
            if (!AssetDatabase.CopyAsset(StarterController, PlayerAnimatorPath)) return null;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorPath);

            float[] thresholds = { 0f, character.WalkSpeed, character.RunSpeed };
            foreach (var layer in controller.layers)
            foreach (var child in layer.stateMachine.states)
            {
                if (child.state.motion is not BlendTree tree || tree.children.Length != thresholds.Length) continue;
                var motions = tree.children;
                for (int i = 0; i < motions.Length; i++) motions[i].threshold = thresholds[i];
                tree.useAutomaticThresholds = false;
                tree.children = motions;
                EditorUtility.SetDirty(tree);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
