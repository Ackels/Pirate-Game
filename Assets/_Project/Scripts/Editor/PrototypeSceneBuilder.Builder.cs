using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.LandCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Mestre de obras por dentro (13/11, interiores passo 4; pedido do Jhonata depois do estaleiro): o escritório e o canteiro do mestre de
    /// obras, com a mesma receita dos outros interiores (sala longe do mapa, porta que escurece e leva para dentro, zona coberta com a hora e
    /// o clima de fora, a vila em volta, com a taverna ao lado como no porto). No meio, <b>a maquete de Porto Alento</b> numa mesa (casinhas,
    /// a torre de vigia, o moinho, a Fortaleza, o píer e um navio na água); a <b>mesa do mestre de obras</b> com as plantas (o F abre as
    /// construções da base); plantas pregadas na parede; a oeste o <b>canteiro de pedra</b>: um muro pela metade com um pedreiro talhando e
    /// outro ajoelhado batendo numa laje (som de pedra, <see cref="Ambient.Worker"/>), pilhas de pedra e blocos cortados; a leste o carrinho,
    /// sacos de cal, balde de argamassa e a bancada com as ferramentas; um ajudante andando (NavMesh da sala).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static Vector3 BuilderOrigin => new(2000f, 30f, -7200f);
        const string BuilderRootName = "Interior_MestreDeObras", BuilderDoorName = "Porta_MestreDeObras";

        [MenuItem("Pirates/Protótipo/Interiores: refazer o mestre de obras")]
        static void MenuBuilderInterior() => Debug.Log(AddBuilderInteriorToOpenScene());

        /// <summary>Monta (ou refaz) o interior do mestre de obras na cena aberta e salva. Só na cena com o porto a pé (07).</summary>
        public static string AddBuilderInteriorToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            generatingScenePath = scene.path;
            string r = AddBuilderInterior();
            if (!r.StartsWith("ok")) return r;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }

        static string AddBuilderInterior()
        {
            HarborBuilding door = null;
            foreach (var b in Object.FindObjectsByType<HarborBuilding>(FindObjectsInactive.Include))
                if (b.Service == HarborService.Construction && b.name.StartsWith("Entrada_")) door = b;
            if (!door) return "sem a porta do mestre de obras (só na cena com o porto a pé)";
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>();
            if (!switcher || !switcher.Character) return "sem o capitão";
            if (!switcher.Character.GetComponent<PassageWalker>()) return "o capitão não tem o PassageWalker (a Ermida põe)";

            foreach (var n in new[] { BuilderRootName, BuilderDoorName, "NavMesh_MestreDeObras" })
            {
                var old = GameObject.Find(n);
                if (old) Object.DestroyImmediate(old);
            }

            var root = new GameObject(BuilderRootName).transform;
            root.position = BuilderOrigin;

            // Paredes: a norte fica sem janela (as plantas pregadas); janelas a leste e uma a oeste; porta no meio do sul.
            TavernShell(root, (side, i) => side switch
            {
                'S' => i == 2 ? "wall-wood-door" : "wall-wood",
                'O' => i == 3 ? "wall-wood-window-shutters" : "wall-wood",
                'L' => i is 1 or 3 ? "wall-wood-window-shutters" : "wall-wood",
                _ => "wall-wood",
            });
            var stuff = new GameObject("Moveis").transform;
            stuff.SetParent(root, false);
            BuilderFurniture(stuff);
            WarehouseLights(root);
            // A taverna de verdade fica 9 m a leste e 8 m mais perto do mar que o mestre de obras, no porto: aqui, à esquerda e para a
            // praia. Um pouco mais longe que no porto (13,5 m): a sala é maior que a casa e a taverna cairia em cima dela.
            TavernOutside(root, (TavernHouse, new Vector2(-13.5f, -9f)));

            Physics.SyncTransforms();
            BakeNavMeshVolume("NavMesh_MestreDeObras", "_NavMesh_MestreDeObras", root.TransformPoint(new Vector3(0f, -0.5f, 0f)),
                new Vector3(RoomHalfX * 2f, 4f, RoomHalfZ * 2f), 0.04f);
            BuilderPeople(root);

            door.SetSignOnly(true);
            door.SetAction("Entrar");
            var outsideArrival = new GameObject("Chegada_Porto").transform;
            outsideArrival.SetParent(root, false);
            outsideArrival.SetPositionAndRotation(door.transform.position + door.transform.forward * 2.2f + Vector3.up * 0.05f, door.transform.rotation);
            var insideArrival = new GameObject("Chegada_MestreDeObras").transform;
            insideArrival.SetParent(root, false);
            insideArrival.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, -RoomHalfZ + 2.4f), Quaternion.identity);

            var outsidePassage = new GameObject(BuilderDoorName);
            outsidePassage.transform.SetPositionAndRotation(door.transform.position, door.transform.rotation);
            outsidePassage.AddComponent<RuinPassage>().Configure("Entrar no Mestre de obras", insideArrival, "Mestre de obras", "construções da base");
            var exit = new GameObject("Saida").transform;
            exit.SetParent(root, false);
            exit.localPosition = new Vector3(0f, 0f, -RoomHalfZ + 0.4f);
            exit.gameObject.AddComponent<RuinPassage>().Configure("Sair para o porto", outsideArrival, null, null);

            var zone = root.gameObject.AddComponent<CoveredZone>();
            zone.Configure(switcher, new Bounds(root.TransformPoint(new Vector3(0f, 2f, 0f)), new Vector3(RoomHalfX * 2f + 2f, 8f, RoomHalfZ * 2f + 2f)));
            zone.SetLook(new CoveredLook
            {
                light = new Color(1f, 0.9f, 0.76f), intensity = 0.55f, angles = new Vector2(64f, 30f), shadow = 0.6f,
                ambient = new Color(0.31f, 0.27f, 0.22f), music = false, portMusic = true, air = false, rainOnRoof = 0.45f,
                background = new Color(0.012f, 0.03f, 0.06f), outsideWeather = true, indoorMix = 0.3f,
            });
            Physics.SyncTransforms();
            return $"ok: mestre de obras em {BuilderOrigin}";
        }

        // ---------- Móveis ----------

        static void BuilderFurniture(Transform f)
        {
            BuilderModel(f);

            // Mesa do mestre de obras (nordeste): plantas abertas, livro de obras, régua, vela; o baú das plantas atrás.
            Furnish(f, Kit.Props, "Table_Large", new Vector3(5.2f, 0f, 3.3f), 0f, new Vector3(0.45f, TableTop / 0.81f, 0.6f), "Mesa_Mestre", solid: true);
            var paper = TavernMaterial("M_Obras_Papel", new Color(0.93f, 0.86f, 0.68f));
            var blue = TavernMaterial("M_Estaleiro_Planta", new Color(0.62f, 0.72f, 0.86f));
            Sheet(f, "Planta_Mesa", new Vector3(5.0f, TableTop + 0.012f, 3.25f), new Vector2(0.62f, 0.45f), -6f, blue);
            Sheet(f, "Planta_Mesa_2", new Vector3(5.55f, TableTop + 0.02f, 3.4f), new Vector2(0.4f, 0.34f), 14f, paper);
            Furnish(f, Kit.Props, "Scroll_2", new Vector3(4.55f, TableTop + 0.02f, 3.5f), 70f, 1.4f, "Rolo");
            Furnish(f, Kit.Props, "Book_5", new Vector3(5.75f, TableTop + 0.01f, 3.15f), 100f, 1.1f, "Livro_de_Obras");
            NightLantern(Furnish(f, Kit.Props, "CandleStick", new Vector3(4.4f, TableTop + 0.01f, 3.1f), 0f, 1f, "Vela_Mestre"), true, 3f, 1.2f);
            Furnish(f, Kit.Props, "Chest_Wood", new Vector3(6.85f, 0f, 3.4f), -90f, 0.42f, "Bau_Plantas", solid: true);
            Spot(f, "Mesa_do_Mestre_de_Obras", new Vector3(5.2f, 0f, 2.3f), 180f, "Mesa do mestre de obras", "construções da base",
                HarborService.Construction, "Falar com");

            // Norte: plantas pregadas na parede (azuis e de papel) e a estante com rolos.
            float wall = RoomHalfZ - 0.12f;
            var sheets = new (float x, float y, float w, float h, float tilt, bool isBlue)[]
            {
                (-3.2f, 1.25f, 0.75f, 0.55f, 3f, true), (-2.2f, 1.45f, 0.55f, 0.7f, -4f, false), (-1.1f, 1.2f, 0.7f, 0.5f, 2f, true),
                (1.1f, 1.35f, 0.6f, 0.6f, -2f, false), (2.1f, 1.2f, 0.7f, 0.5f, 5f, true),
            };
            foreach (var s in sheets)
            {
                var g = Part(PrimitiveType.Cube, "Planta_Parede", f, new Vector3(s.x, s.y, wall), new Vector3(s.w, s.h, 0.01f),
                    Quaternion.Euler(0f, 0f, s.tilt), s.isBlue ? blue : paper);
                Object.DestroyImmediate(g.GetComponent<Collider>());
            }
            Furnish(f, Kit.Props, "Bookcase_2", new Vector3(3.6f, 0f, RoomHalfZ - 0.3f), 180f, new Vector3(0.6f, 0.55f, 0.7f), "Estante", solid: true);
            for (int k = 0; k < 4; k++)
                Furnish(f, Kit.Props, "Scroll_1", new Vector3(3.35f + k * 0.16f, 0.7f, RoomHalfZ - 0.3f), 90f + k * 9f, 1.6f, "Rolo_Estante");

            // Oeste: o canteiro de pedra. Um muro pela metade (blocos em fiadas desencontradas) com o pedreiro talhando.
            var stone = TavernMaterial("M_Obras_Pedra", new Color(0.62f, 0.6f, 0.56f));
            var stoneDark = TavernMaterial("M_Obras_Pedra_Escura", new Color(0.5f, 0.48f, 0.45f));
            var wallRoot = new GameObject("Muro_em_Obra").transform;
            wallRoot.SetParent(f, false);
            wallRoot.localPosition = new Vector3(-6.1f, 0f, 0.5f);
            var wallBox = wallRoot.gameObject.AddComponent<BoxCollider>();
            wallBox.center = new Vector3(0f, 0.5f, 0.02f);
            wallBox.size = new Vector3(0.3f, 1f, 2.1f);
            int[] blocksPerRow = { 5, 5, 4, 2 };
            for (int row = 0; row < blocksPerRow.Length; row++)
                for (int k = 0; k < blocksPerRow[row]; k++)
                {
                    float z = -1.0f + k * 0.5f + (row % 2 == 1 ? 0.25f : 0f);
                    if (z > 1.05f) continue;
                    Part(PrimitiveType.Cube, "Bloco", wallRoot, new Vector3(0f, 0.125f + row * 0.25f, z), new Vector3(0.3f, 0.24f, 0.48f),
                        Quaternion.identity, (row + k) % 3 == 0 ? stoneDark : stone);
                }
            Occluder(wallRoot.gameObject);
            // Argamassa no alto da última fiada e um bloco esperando ao pé do muro.
            Furnish(f, Kit.Props, "Bucket_Metal", new Vector3(-5.5f, 0f, 2.0f), 0f, 1.1f, "Balde_Argamassa");
            Part(PrimitiveType.Cube, "Bloco_Solto", f, new Vector3(-5.6f, 0.12f, -0.9f), new Vector3(0.48f, 0.24f, 0.3f), Quaternion.Euler(0f, 20f, 0f), stone);

            // Pedras brutas e blocos cortados empilhados.
            Furnish(f, Kit.Castle, "rocks-large", new Vector3(-6.2f, 0f, 4.5f), 20f, 1.5f, "Pedras_Brutas", solid: true);
            Furnish(f, Kit.Castle, "rocks-large", new Vector3(-4.5f, 0f, 5.0f), -40f, 1.2f, "Pedras_Brutas", solid: true);
            Furnish(f, Kit.Survival, "resource-stone-large", new Vector3(-3.6f, 0f, 4.4f), 30f, 4f, "Pedra");
            var pallet = new GameObject("Blocos_Cortados").transform;
            pallet.SetParent(f, false);
            pallet.localPosition = new Vector3(-6.2f, 0f, -2.8f);
            var palletBox = pallet.gameObject.AddComponent<BoxCollider>();
            palletBox.center = new Vector3(0f, 0.3f, 0f);
            palletBox.size = new Vector3(1.0f, 0.6f, 1.05f);
            for (int layer = 0; layer < 2; layer++)
                for (int a = 0; a < 2; a++)
                    for (int b = 0; b < 2; b++)
                    {
                        if (layer == 1 && a == 1 && b == 1) continue;
                        Part(PrimitiveType.Cube, "Bloco", pallet, new Vector3(-0.25f + a * 0.5f, 0.12f + layer * 0.25f, -0.26f + b * 0.52f),
                            new Vector3(0.48f, 0.24f, 0.5f), Quaternion.identity, (a + b + layer) % 2 == 0 ? stone : stoneDark);
                    }
            foreach (var c in pallet.GetComponentsInChildren<Collider>()) if (c != palletBox) Object.DestroyImmediate(c);
            // A laje do pedreiro ajoelhado (ele bate nela; ver BuilderPeople).
            var slab = Part(PrimitiveType.Cube, "Laje_em_Obra", f, new Vector3(-3.0f, 0.06f, -0.55f), new Vector3(0.55f, 0.12f, 0.45f),
                Quaternion.Euler(0f, 8f, 0f), stone);
            Object.DestroyImmediate(slab.GetComponent<Collider>());

            // Andaime simples encostado no muro: dois postes e uma tábua.
            Furnish(f, Kit.Town, "poles", new Vector3(-6.5f, 0f, 2.2f), 0f, new Vector3(1f, 1.3f, 0.9f), "Andaime");
            Furnish(f, Kit.Town, "planks-half", new Vector3(-6.45f, 0.9f, 2.2f), 90f, new Vector3(0.5f, 1f, 0.9f), "Tabua_Andaime");

            // Leste: carrinho de mão, sacos de cal, balde, bancada com ferramentas e ferramentas na parede.
            Furnish(f, Kit.Town, "cart", new Vector3(6.1f, 0f, 0.3f), 100f, 1.7f, "Carrinho", solid: true);
            Furnish(f, Kit.Survival, "resource-stone", new Vector3(6.05f, 0.58f, 0.25f), 0f, 3.5f, "Pedra_no_Carrinho");
            Furnish(f, Kit.Props, "Bag", new Vector3(6.7f, 0f, -1.4f), 15f, 0.75f, "Saco_de_Cal", solid: true);
            Furnish(f, Kit.Props, "Bag", new Vector3(6.05f, 0f, -1.85f), -30f, 0.7f, "Saco_de_Cal", solid: true);
            Furnish(f, Kit.Props, "Bag", new Vector3(6.5f, 0.58f, -1.6f), 70f, 0.6f, "Saco_de_Cal");
            Furnish(f, Kit.Props, "Workbench", new Vector3(6.85f, 0f, -3.6f), -90f, new Vector3(0.55f, 0.5f, 0.6f), "Bancada", solid: true);
            Furnish(f, Kit.Survival, "tool-hammer", new Vector3(6.75f, 0.45f, -3.9f), 20f, 2.4f, "Martelo");
            Furnish(f, Kit.Survival, "tool-shovel", new Vector3(6.85f, 0.45f, -3.3f), -80f, 2.2f, "Pa");
            Furnish(f, Kit.Props, "Peg_Rack", new Vector3(RoomHalfX - 0.12f, 1.45f, -2.4f), -90f, 1f, "Ferramentas_Parede");
            Furnish(f, Kit.Props, "Pickaxe_Bronze", new Vector3(RoomHalfX - 0.15f, 0f, -4.6f), -90f, 0.75f, "Picareta");

            // Sul: madeira, caixotes e barris nos cantos.
            Furnish(f, Kit.Survival, "resource-planks", new Vector3(-6.3f, 0f, -4.9f), 90f, 3.6f, "Tabuas");
            Furnish(f, Kit.Survival, "resource-wood", new Vector3(-4.8f, 0f, -5.2f), 0f, 3.6f, "Lenha");
            Furnish(f, Kit.Props, "Crate_Wooden", new Vector3(6.75f, 0f, -5.2f), -15f, 0.75f, "Caixote", solid: true);
            Furnish(f, Kit.Props, "Crate_Wooden", new Vector3(6.75f, 0.66f, -5.2f), 5f, 0.75f, "Caixote");
            Furnish(f, Kit.Pirate, "barrel", new Vector3(3.6f, 0f, -5.35f), 20f, 0.5f, "Barril", solid: true);

            // Sudeste: pilha de tijolos vermelhos (para as obras da vila) e vigas deitadas.
            var brick = TavernMaterial("M_Obras_Tijolo", new Color(0.7f, 0.36f, 0.26f));
            var bricks = new GameObject("Tijolos").transform;
            bricks.SetParent(f, false);
            bricks.localPosition = new Vector3(4.9f, 0f, -4.8f);
            var bricksBox = bricks.gameObject.AddComponent<BoxCollider>();
            bricksBox.center = new Vector3(0f, 0.22f, 0f);
            bricksBox.size = new Vector3(1.0f, 0.44f, 0.65f);
            for (int layer = 0; layer < 4; layer++)
                for (int k = 0; k < 4 - (layer == 3 ? 2 : 0); k++)
                    for (int r = 0; r < 2; r++)
                        Part(PrimitiveType.Cube, "Tijolo", bricks, new Vector3(-0.36f + k * 0.24f + (layer % 2) * 0.06f, 0.055f + layer * 0.11f, -0.15f + r * 0.3f),
                            new Vector3(0.22f, 0.1f, 0.28f), Quaternion.identity, brick);
            for (int k = 0; k < 3; k++)
                Furnish(f, Kit.Nature, "log", new Vector3(-2.2f + k * 0.05f, k == 2 ? 0.2f : 0f, -5.1f + (k == 2 ? 0.17f : k * 0.34f)), 90f,
                    new Vector3(1.4f, 0.8f, 0.8f), "Viga", solid: k == 0);
        }

        /// <summary>Folha (planta) deitada sem colisor.</summary>
        static void Sheet(Transform f, string name, Vector3 at, Vector2 size, float yaw, Material mat)
        {
            var g = Part(PrimitiveType.Cube, name, f, at, new Vector3(size.x, 0.01f, size.y), Quaternion.Euler(0f, yaw, 0f), mat);
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        /// <summary>
        /// A maquete de Porto Alento no meio da sala: tábua verde com a praia e o mar ao norte, o píer e um navio, casinhas de telhado vermelho,
        /// a torre de vigia, o moinho e a Fortaleza (peças dos kits em miniatura, sem sombra nem colisor).
        /// </summary>
        static void BuilderModel(Transform f)
        {
            var at = new Vector3(0f, 0f, 1.3f);
            Furnish(f, Kit.Props, "Table_Large", at, 0f, new Vector3(0.62f, TableTop / 0.81f, 1.0f), "Mesa_Maquete", solid: true);
            var m = new GameObject("Maquete_Porto_Alento").transform;
            m.SetParent(f, false);
            m.localPosition = at + Vector3.up * TableTop;
            var grass = TavernMaterial("M_Maquete_Grama", new Color(0.46f, 0.6f, 0.33f));
            var sand = TavernMaterial("M_Maquete_Areia", new Color(0.88f, 0.8f, 0.58f));
            var sea = TavernMaterial("M_Maquete_Mar", new Color(0.3f, 0.55f, 0.68f), 0.5f);
            var wallMat = TavernMaterial("M_Maquete_Parede", new Color(0.92f, 0.88f, 0.78f));
            var roof = TavernMaterial("M_Maquete_Telhado", new Color(0.66f, 0.25f, 0.18f));
            var dirt = TavernMaterial("M_Maquete_Terra", new Color(0.62f, 0.5f, 0.36f));
            void Box(string n, Vector3 p, Vector3 s, float yaw, Material mat)
            {
                var g = Part(PrimitiveType.Cube, n, m, p, s, Quaternion.Euler(0f, yaw, 0f), mat);
                Object.DestroyImmediate(g.GetComponent<Collider>());
            }
            Box("Base_Grama", new Vector3(0f, 0.02f, -0.15f), new Vector3(1.66f, 0.04f, 0.72f), 0f, grass);
            Box("Base_Areia", new Vector3(0f, 0.015f, 0.29f), new Vector3(1.66f, 0.03f, 0.16f), 0f, sand);
            Box("Base_Mar", new Vector3(0f, 0.01f, 0.47f), new Vector3(1.66f, 0.02f, 0.2f), 0f, sea);
            Box("Rua", new Vector3(0f, 0.042f, -0.05f), new Vector3(1.3f, 0.005f, 0.08f), 0f, dirt);
            Box("Rua_Praca", new Vector3(0.05f, 0.042f, -0.25f), new Vector3(0.08f, 0.005f, 0.38f), 0f, dirt);
            // Casinhas: parede creme e telhado de duas águas (dois planos inclinados).
            var houses = new (float x, float z, float yaw, float w)[]
            {
                (-0.62f, 0.06f, 0f, 0.16f), (-0.36f, 0.08f, 0f, 0.13f), (0.34f, 0.07f, 0f, 0.15f), (0.6f, 0.05f, 0f, 0.13f),
                (-0.5f, -0.2f, 0f, 0.13f), (0.4f, -0.24f, 10f, 0.14f), (0.05f, -0.42f, -8f, 0.12f), (0.24f, -0.4f, 5f, 0.12f),
            };
            for (int k = 0; k < houses.Length; k++)
            {
                var h = houses[k];
                float hh = 0.09f;
                var house = new GameObject($"Casinha_{k}").transform;
                house.SetParent(m, false);
                house.localPosition = new Vector3(h.x, 0.04f, h.z);
                house.localRotation = Quaternion.Euler(0f, h.yaw, 0f);
                var body = Part(PrimitiveType.Cube, "Parede", house, new Vector3(0f, hh * 0.5f, 0f), new Vector3(h.w, hh, h.w * 0.8f), Quaternion.identity, wallMat);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                for (int s = -1; s <= 1; s += 2)
                {
                    var r = Part(PrimitiveType.Cube, "Telhado", house, new Vector3(0f, hh + 0.03f, s * h.w * 0.2f),
                        new Vector3(h.w * 1.08f, 0.016f, h.w * 0.52f), Quaternion.Euler(s * 38f, 0f, 0f), roof);
                    Object.DestroyImmediate(r.GetComponent<Collider>());
                }
            }
            // Peças dos kits em miniatura: torre de vigia, moinho, a Fortaleza (torre do castelo), a fonte da praça, o píer e o navio.
            void Mini(Kit kit, string model, Vector3 p, float yaw, float scale, string n)
            {
                var g = Furnish(m, kit, model, p, yaw, scale, n);
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Mini(Kit.Pirate, "tower-complete-small", new Vector3(0.72f, 0.04f, -0.36f), 200f, 0.045f, "Mini_TorreDeVigia");
            // Moinho: torrezinha sextavada com o telhado e as pás viradas para a frente (o "windmill" do kit é só a roda de pás).
            Mini(Kit.Castle, "tower-hexagon-base", new Vector3(-0.7f, 0.04f, -0.36f), 0f, 0.12f, "Mini_Moinho");
            Mini(Kit.Castle, "tower-hexagon-roof", new Vector3(-0.7f, 0.04f + 1.31f * 0.12f, -0.36f), 0f, 0.12f, "Mini_Moinho_Telhado");
            Mini(Kit.Town, "windmill", new Vector3(-0.7f, 0.06f, -0.43f), 90f, 0.075f, "Mini_Moinho_Pas");
            // Fortaleza: torre quadrada de três andares no fundo, com dois trechos de muralha.
            var fort = new Vector3(-0.3f, 0.04f, -0.42f);
            Mini(Kit.Castle, "tower-square-base", fort, 0f, 0.1f, "Mini_Fortaleza");
            Mini(Kit.Castle, "tower-square-mid-windows", fort + Vector3.up * 0.131f, 0f, 0.1f, "Mini_Fortaleza_Meio");
            Mini(Kit.Castle, "tower-square-top-roof", fort + Vector3.up * 0.262f, 0f, 0.1f, "Mini_Fortaleza_Topo");
            for (int s = -1; s <= 1; s += 2)
                Mini(Kit.Castle, "wall-half", fort + new Vector3(s * 0.1f, 0f, 0f), 0f, 0.1f, "Mini_Muralha");
            Mini(Kit.Town, "fountain-round", new Vector3(0.05f, 0.043f, -0.18f), 0f, 0.06f, "Mini_Fonte");
            Mini(Kit.Pirate, "structure-platform-dock-small", new Vector3(0f, 0.01f, 0.42f), 0f, 0.09f, "Mini_Pier");
            Mini(Kit.Pirate, "ship-pirate-small", new Vector3(0.45f, 0.02f, 0.48f), 90f, 0.035f, "Mini_Navio");
        }

        // ---------- Gente ----------

        static void BuilderPeople(Transform root)
        {
            var people = new GameObject("Gente").transform;
            people.SetParent(root, false);
            Vector3 P(float x, float z) => root.TransformPoint(new Vector3(x, 0f, z));
            Townsfolk(people, "Mestre_de_Obras", P(5.2f, 4.4f), 180f, 2, null);
            // Pedreiros: um em pé talhando o muro (de frente para oeste), um ajoelhado batendo na laje.
            Carpenter(people, "Pedreiro_Muro", root, new Vector3(-5.4f, 0f, 0.4f), -90f, 4, "Martelar_Parede", true, WorkMaterial.Stone);
            Carpenter(people, "Pedreiro_Laje", root, new Vector3(-3.0f, 0f, -1.0f), 0f, 1, "Martelar_Chao", false, WorkMaterial.Stone);
            var route = new[] { P(-3.6f, -3.2f), P(3.6f, -3.2f), P(3.4f, 0.1f), P(-1.6f, -1.9f) };
            Townsfolk(people, "Ajudante", route[0], 90f, 7, route);
        }
    }
}
