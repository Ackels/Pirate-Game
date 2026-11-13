using Pirates.Ambient;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.LandCombat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Estaleiro por dentro (13/11, interiores passo 3; pedido do Jhonata depois do armazém): a oficina do mestre carpinteiro, com a mesma
    /// receita da taverna e do armazém (sala longe do mapa, porta que escurece e leva para dentro, zona coberta com a hora e o clima de fora,
    /// a vila em volta, agora com o armazém ao lado como no porto). No meio, <b>um barco sendo construído</b> sobre calços; <b>dois carpinteiros
    /// martelando</b> (um no casco, em pé, e um ajoelhado nas tábuas: animações de trabalho do Kevin Iglesias, martelo na mão, o som no tempo
    /// do golpe, <see cref="Worker"/>); a <b>mesa do mestre carpinteiro</b> (o F abre o conserto e as melhorias do navio); madeira empilhada,
    /// mastro deitado, ferramentas na parede, bancada, canhão em exposição, cordas e breu; um ajudante andando (NavMesh da sala).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static Vector3 ShipyardOrigin => new(2000f, 30f, -5600f);
        const string ShipyardRootName = "Interior_Estaleiro", ShipyardDoorName = "Porta_Estaleiro";
        const string CarpenterAnimatorPath = AnimationFolder + "/Carpinteiro.controller";

        [MenuItem("Pirates/Protótipo/Interiores: refazer o estaleiro")]
        static void MenuShipyardInterior() => Debug.Log(AddShipyardInteriorToOpenScene());

        /// <summary>Monta (ou refaz) o interior do estaleiro na cena aberta e salva. Só na cena com o porto a pé (07).</summary>
        public static string AddShipyardInteriorToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            generatingScenePath = scene.path;
            string r = AddShipyardInterior();
            if (!r.StartsWith("ok")) return r;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }

        static string AddShipyardInterior()
        {
            HarborBuilding door = null;
            foreach (var b in Object.FindObjectsByType<HarborBuilding>(FindObjectsInactive.Include))
                if (b.Service == HarborService.Shipyard && b.name.StartsWith("Entrada_")) door = b;
            if (!door) return "sem a porta do estaleiro (só na cena com o porto a pé)";
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>();
            if (!switcher || !switcher.Character) return "sem o capitão";
            if (!switcher.Character.GetComponent<PassageWalker>()) return "o capitão não tem o PassageWalker (a Ermida põe)";

            foreach (var n in new[] { ShipyardRootName, ShipyardDoorName, "NavMesh_Estaleiro" })
            {
                var old = GameObject.Find(n);
                if (old) Object.DestroyImmediate(old);
            }

            var root = new GameObject(ShipyardRootName).transform;
            root.position = ShipyardOrigin;

            // Paredes: janelas a leste e a oeste (luz para trabalhar); porta no meio do sul.
            TavernShell(root, (side, i) => side switch
            {
                'S' => i == 2 ? "wall-wood-door" : "wall-wood",
                'O' => i is 1 or 3 ? "wall-wood-window-shutters" : "wall-wood",
                'L' => i == 3 ? "wall-wood-window-shutters" : "wall-wood",
                _ => "wall-wood",
            });
            var stuff = new GameObject("Moveis").transform;
            stuff.SetParent(root, false);
            ShipyardFurniture(stuff);
            WarehouseLights(root);
            // O armazém de verdade fica ~20 m a oeste e 6,5 m mais perto do mar que o estaleiro, no porto: aqui, à direita e para a praia.
            TavernOutside(root, (WarehouseHouse, new Vector2(20.5f, -6.5f)));

            Physics.SyncTransforms();
            BakeNavMeshVolume("NavMesh_Estaleiro", "_NavMesh_Estaleiro", root.TransformPoint(new Vector3(0f, -0.5f, 0f)),
                new Vector3(RoomHalfX * 2f, 4f, RoomHalfZ * 2f), 0.04f);
            ShipyardPeople(root);

            door.SetSignOnly(true);
            door.SetAction("Entrar");
            var outsideArrival = new GameObject("Chegada_Porto").transform;
            outsideArrival.SetParent(root, false);
            outsideArrival.SetPositionAndRotation(door.transform.position + door.transform.forward * 2.2f + Vector3.up * 0.05f, door.transform.rotation);
            var insideArrival = new GameObject("Chegada_Estaleiro").transform;
            insideArrival.SetParent(root, false);
            insideArrival.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, -RoomHalfZ + 2.4f), Quaternion.identity);

            var outsidePassage = new GameObject(ShipyardDoorName);
            outsidePassage.transform.SetPositionAndRotation(door.transform.position, door.transform.rotation);
            outsidePassage.AddComponent<RuinPassage>().Configure("Entrar no Estaleiro", insideArrival, "Estaleiro", "conserto e melhorias do navio");
            var exit = new GameObject("Saida").transform;
            exit.SetParent(root, false);
            exit.localPosition = new Vector3(0f, 0f, -RoomHalfZ + 0.4f);
            exit.gameObject.AddComponent<RuinPassage>().Configure("Sair para o porto", outsideArrival, null, null);

            var zone = root.gameObject.AddComponent<CoveredZone>();
            zone.Configure(switcher, new Bounds(root.TransformPoint(new Vector3(0f, 2f, 0f)), new Vector3(RoomHalfX * 2f + 2f, 8f, RoomHalfZ * 2f + 2f)));
            zone.SetLook(new CoveredLook
            {
                light = new Color(1f, 0.9f, 0.74f), intensity = 0.55f, angles = new Vector2(64f, -30f), shadow = 0.6f,
                ambient = new Color(0.32f, 0.27f, 0.21f), music = false, portMusic = true, air = false, rainOnRoof = 0.45f,
                background = new Color(0.012f, 0.03f, 0.06f), outsideWeather = true, indoorMix = 0.3f,
            });
            Physics.SyncTransforms();
            return $"ok: estaleiro em {ShipyardOrigin}";
        }

        // ---------- Móveis ----------

        static void ShipyardFurniture(Transform f)
        {
            // O barco sendo construído, no meio, sobre calços (o bote grande do Pirate Kit, ~4 m).
            var hull = new Vector3(-1.2f, 0.22f, 1.4f);
            for (int k = -1; k <= 1; k++)
                Furnish(f, Kit.Nature, "log_large", new Vector3(hull.x + k * 1.25f, 0f, hull.z), 90f, new Vector3(1.4f, 0.55f, 0.9f), "Calco", solid: true);
            Furnish(f, Kit.Pirate, "boat-row-large", hull, 90f, 1.4f, "Barco_em_Obra", solid: true);
            // Tábuas soltas encostadas e no chão perto do casco.
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(1.2f, 0f, 1.6f), 15f, 2.6f, "Tabuas_do_Casco");
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(1.6f, 0.2f, 1.4f), -10f, 2.4f, "Tabuas_do_Casco");

            // Oeste: madeira empilhada (toras e tábuas) e o mastro deitado.
            Furnish(f, Kit.Nature, "log_stack", new Vector3(-6.4f, 0f, 3.6f), 0f, 1.7f, "Toras", solid: true);
            Furnish(f, Kit.Nature, "log_stack", new Vector3(-6.4f, 0f, 1.9f), 0f, 1.5f, "Toras", solid: true);
            Furnish(f, Kit.Nature, "log_stackLarge", new Vector3(-6.3f, 0f, 0.1f), 0f, 1.2f, "Toras_Grandes", solid: true);
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(-4.9f, 0f, 4.2f), 90f, 2.8f, "Tabuas");
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(-4.9f, 0.25f, 4.2f), 85f, 2.8f, "Tabuas");
            var mast = Model(Kit.Pirate, "mast", f, new Vector3(-1.5f, 0.25f, RoomHalfZ - 0.9f), Quaternion.Euler(0f, 0f, 90f), 0.45f, "Mastro_Deitado");
            foreach (var r in mast.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Norte: ferramentas na parede, bancada com ferramentas, cordas e breu.
            Furnish(f, Kit.Props, "Peg_Rack", new Vector3(1.6f, 1.45f, RoomHalfZ - 0.12f), 180f, 1f, "Ferramentas_Parede");
            Furnish(f, Kit.Props, "Axe_Bronze", new Vector3(2.6f, 1.1f, RoomHalfZ - 0.12f), 180f, 0.9f, "Machado_Parede");
            Furnish(f, Kit.Props, "Workbench", new Vector3(4.6f, 0f, RoomHalfZ - 0.55f), 180f, 0.6f, "Bancada", solid: true);
            Furnish(f, Kit.Survival, "tool-hammer", new Vector3(4.3f, 0.54f, RoomHalfZ - 0.55f), 70f, 2.4f, "Martelo");
            Furnish(f, Kit.Survival, "tool-axe", new Vector3(4.9f, 0.54f, RoomHalfZ - 0.6f), -20f, 2.2f, "Machadinha");
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(5.0f, 0.54f, RoomHalfZ - 0.45f), 0f, 1.6f, "Tabua_na_Bancada");
            Furnish(f, Kit.Props, "Rope_2", new Vector3(6.6f, 0f, RoomHalfZ - 0.7f), 0f, 1.3f, "Corda");
            Furnish(f, Kit.Props, "Rope_3", new Vector3(6.6f, 0.15f, RoomHalfZ - 0.7f), 40f, 1.1f, "Corda");
            Furnish(f, Kit.Survival, "barrel-open", new Vector3(6.6f, 0f, 3.6f), 0f, 1.5f, "Breu", solid: true);
            Furnish(f, Kit.Props, "Bucket_Wooden_1", new Vector3(6.0f, 0f, 3.2f), 0f, 1.3f, "Balde_de_Breu");

            // Leste: canhão em exposição com as balas, remos encostados, uma vela dobrada.
            Furnish(f, Kit.Pirate, "cannon", new Vector3(6.2f, 0f, 1.0f), -90f, 0.5f, "Canhao_Exposicao", solid: true);
            for (int k = 0; k < 3; k++)
                Furnish(f, Kit.Pirate, "cannon-ball", new Vector3(6.6f + (k == 2 ? -0.15f : k * -0.3f), k == 2 ? 0.18f : 0f, -0.2f), 0f, 0.5f, "Bala");
            Furnish(f, Kit.Pirate, "tool-paddle", new Vector3(RoomHalfX - 0.25f, 0f, -1.4f), 90f, 0.45f, "Remo");
            Furnish(f, Kit.Pirate, "tool-paddle", new Vector3(RoomHalfX - 0.25f, 0f, -1.0f), 100f, 0.45f, "Remo");
            Furnish(f, Kit.Survival, "bedroll-packed", new Vector3(3.2f, 0f, -5.3f), 90f, 4.5f, "Vela_Dobrada");

            // Sudoeste: um bote virado de cabeça para baixo, esperando conserto, com uma tábua e o balde em cima.
            var flipped = Model(Kit.Pirate, "boat-row-small", f, new Vector3(-4.3f, 0.85f * 1.05f, -3.3f), Quaternion.Euler(0f, 90f, 180f), 1.05f, "Bote_Virado");
            SolidProp(flipped);
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(-4.4f, 0.9f, -3.2f), 80f, 2.2f, "Tabua_no_Bote");
            Furnish(f, Kit.Props, "Bucket_Wooden_1", new Vector3(-2.6f, 0f, -2.0f), 0f, 1.3f, "Balde");

            // Sul: caixotes e barris nos cantos.
            void Crates(Vector3 at, float yaw, int high)
            {
                for (int h = 0; h < high; h++)
                    Furnish(f, Kit.Props, "Crate_Wooden", at + new Vector3(0f, h * 0.66f, 0f), yaw + h * 17f, 0.75f, "Caixote", solid: h == 0);
            }
            Crates(new Vector3(-6.7f, 0f, -5.2f), 10f, 2);
            Crates(new Vector3(-5.9f, 0f, -5.3f), -8f, 1);
            Crates(new Vector3(6.75f, 0f, -5.2f), -15f, 2);
            Furnish(f, Kit.Pirate, "barrel", new Vector3(-3.6f, 0f, -5.35f), 20f, 0.5f, "Barril", solid: true);

            // Mesa do mestre carpinteiro (sudeste): a planta do barco aberta, livro, vela; o baú das ferramentas atrás.
            Furnish(f, Kit.Props, "Table_Large", new Vector3(5.5f, 0f, -3.2f), 90f, new Vector3(0.45f, TableTop / 0.81f, 0.75f), "Mesa_Mestre", solid: true);
            var plan = Part(PrimitiveType.Cube, "Planta", f, new Vector3(5.5f, TableTop + 0.012f, -3.25f), new Vector3(0.6f, 0.01f, 0.85f),
                Quaternion.Euler(0f, 8f, 0f), TavernMaterial("M_Estaleiro_Planta", new Color(0.62f, 0.72f, 0.86f)));
            Object.DestroyImmediate(plan.GetComponent<Collider>());
            Furnish(f, Kit.Props, "Scroll_2", new Vector3(5.45f, TableTop + 0.02f, -2.65f), 30f, 1.4f, "Rolo");
            Furnish(f, Kit.Props, "Book_5", new Vector3(5.6f, TableTop + 0.01f, -3.95f), 80f, 1.1f, "Livro_de_Obras");
            NightLantern(Furnish(f, Kit.Props, "CandleStick", new Vector3(5.7f, TableTop + 0.01f, -2.4f), 0f, 1f, "Vela_Mestre"), true, 3f, 1.2f);
            Furnish(f, Kit.Props, "Chest_Wood", new Vector3(6.8f, 0f, -3.2f), -90f, 0.42f, "Bau_Ferramentas", solid: true);
            Spot(f, "Mesa_do_Mestre_Carpinteiro", new Vector3(4.25f, 0f, -3.2f), -90f, "Mesa do mestre carpinteiro", "conserto e melhorias do navio",
                HarborService.Shipyard, "Falar com");
        }

        // ---------- Gente ----------

        static void ShipyardPeople(Transform root)
        {
            var people = new GameObject("Gente").transform;
            people.SetParent(root, false);
            Vector3 P(float x, float z) => root.TransformPoint(new Vector3(x, 0f, z));
            Townsfolk(people, "Mestre_Carpinteiro", P(6.4f, -3.2f), -90f, 5, null);
            // Carpinteiros: um em pé martelando o casco (lado sul do barco), um ajoelhado pregando as tábuas soltas.
            Carpenter(people, "Carpinteiro_Casco", root, new Vector3(-1.0f, 0f, -0.75f), 0f, 3, "Martelar_Parede", true);
            Carpenter(people, "Carpinteiro_Tabuas", root, new Vector3(1.4f, 0f, 0.6f), 10f, 6, "Martelar_Chao", false);
            var route = new[] { P(-4.4f, 2.6f), P(-2.4f, -1.4f), P(2.6f, -1.4f), P(3.0f, 3.4f) };
            Townsfolk(people, "Ajudante", route[0], 0f, 7, route);
        }

        /// <summary>Carpinteiro humanoide (kit) com o martelo na mão e o <see cref="Worker"/> repetindo o golpe.</summary>
        static void Carpenter(Transform parent, string name, Transform root, Vector3 local, float yaw, int look, string state, bool wall, WorkMaterial material = WorkMaterial.Wood)
        {
            var (model, palette) = CivilianLooks[look % CivilianLooks.Length];
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(root.TransformPoint(local), root.rotation * Quaternion.Euler(0f, yaw, 0f));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.55f, 0f);
            capsule.radius = 0.28f;
            capsule.height = 1.1f;
            var visual = HumanoidKitCharacter(model, go.transform, CivilianMaterial(model, palette));
            var animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = CarpenterAnimator();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            string clip = wall ? "MiningOneHand01_R - Wall" : "HammeringGround01_R - Loop";
            var hammer = ToolInHand(visual.transform, "Martelo", "tool-hammer", 2.4f, clip, 0.25f);
            hammer.SetActive(true);
            var point = new GameObject("Ponto_do_Golpe").transform;
            point.SetParent(go.transform, false);
            point.localPosition = wall ? new Vector3(0f, 0.45f, 0.55f) : new Vector3(0f, 0.05f, 0.45f);
            var worker = go.AddComponent<Worker>();
            var workClip = KevinClip(clip);
            float length = workClip ? workClip.length : 1f;
            float impact = workClip ? ImpactPhase(workClip, !wall, visual.transform) : 0.45f;
            // O ajoelhado não levanta para descansar (a pose parada é em pé): só pausa entre as marteladas.
            worker.Configure(animator, state, wall ? "Parado" : state, impact, length, wall, point, material);
        }

        /// <summary>Animator dos carpinteiros (refeito a cada geração): martelar na parede/casco, martelar no chão e parado.</summary>
        static RuntimeAnimatorController CarpenterAnimator()
        {
            var wallClip = KevinClip("MiningOneHand01_R - Wall");
            var groundClip = KevinClip("HammeringGround01_R - Loop");
            var idle = BasicMotion(BasicIdle);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CarpenterAnimatorPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(CarpenterAnimatorPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            AnimatorState Add(string n, Motion m)
            {
                var st = sm.AddState(n);
                st.motion = m;
                st.writeDefaultValues = true;
                return st;
            }
            var rest = Add("Parado", idle);
            Add("Martelar_Parede", wallClip);
            Add("Martelar_Chao", groundClip);
            sm.defaultState = rest;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Instante do golpe no clipe (0–1), no corpo de um personagem do kit: no chão, quando a mão direita fica mais baixa; na parede, logo
        /// depois de a mão andar mais rápido (quando ela para no casco).
        /// </summary>
        static float ImpactPhase(AnimationClip clip, bool ground, Transform model)
        {
            var temp = MeasureCopy(model);
            if (!temp) return 0.45f;
            var hand = temp.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            const int steps = 60;
            float best = ground ? float.MaxValue : -1f, bestT = 0.45f;
            Vector3 before = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float t = clip.length * i / steps;
                clip.SampleAnimation(temp, t);
                var p = hand.position;
                if (ground)
                {
                    if (p.y < best) { best = p.y; bestT = (float)i / steps; }
                }
                else if (i > 0)
                {
                    float speed = (p - before).magnitude;
                    if (speed > best) { best = speed; bestT = (float)i / steps; }
                }
                before = p;
            }
            Object.DestroyImmediate(temp);
            return ground ? bestT : Mathf.Min(1f, bestT + 0.03f);
        }
    }
}
