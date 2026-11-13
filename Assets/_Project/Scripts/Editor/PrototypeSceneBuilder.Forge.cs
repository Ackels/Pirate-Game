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
    /// Forja por dentro (13/11, interiores passo 5; pedido do Jhonata depois do mestre de obras): a oficina do ferreiro, com a mesma receita dos
    /// outros interiores (sala longe do mapa, porta que escurece e leva para dentro, zona coberta com a hora e o clima de fora, a vila em volta).
    /// A porta de fora fica dentro do grupo "Forja_N1": só existe com a Forja construída, como a porta do ferreiro de antes. Na parede norte,
    /// <b>a forja acesa</b> (fornalha de pedra com o carvão em brasa, o fogo, a coifa e a chaminé, o fole); na frente dela <b>o ferreiro
    /// martelando na bigorna</b> e um aprendiz ajoelhado numa bigorna pequena (o tinido do metal, <see cref="Ambient.Worker"/>); o barril de
    /// têmpera, o monte de carvão, lingotes de ferro e caixas de metal, o suporte de armas, escudo e espadas na parede, o rebolo, as balas de
    /// canhão e a corrente; o <b>balcão do ferreiro</b> (o F abre as encomendas da Forja).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static Vector3 ForgeOrigin => new(2000f, 30f, -8800f);
        const string ForgeRootName = "Interior_Forja", ForgeDoorName = "Porta_Forja";

        [MenuItem("Pirates/Protótipo/Interiores: refazer a forja")]
        static void MenuForgeInterior() => Debug.Log(AddForgeInteriorToOpenScene());

        /// <summary>Monta (ou refaz) o interior da forja na cena aberta e salva. Só na cena com o porto a pé (07).</summary>
        public static string AddForgeInteriorToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            generatingScenePath = scene.path;
            string r = AddForgeInterior();
            if (!r.StartsWith("ok")) return r;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }

        static string AddForgeInterior()
        {
            HarborBuilding door = null;
            foreach (var b in Object.FindObjectsByType<HarborBuilding>(FindObjectsInactive.Include))
                if (b.Service == HarborService.Smithy && b.name.StartsWith("Entrada_")) door = b;
            if (!door) return "sem a porta da forja (só na cena com o porto a pé)";
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>();
            if (!switcher || !switcher.Character) return "sem o capitão";
            if (!switcher.Character.GetComponent<PassageWalker>()) return "o capitão não tem o PassageWalker (a Ermida põe)";

            foreach (var n in new[] { ForgeRootName, "NavMesh_Forja" })
            {
                var old = GameObject.Find(n);
                if (old) Object.DestroyImmediate(old);
            }
            // A porta de fora fica no grupo da Forja (desligado sem a Forja): o GameObject.Find não acha objeto desligado.
            var oldDoor = door.transform.parent ? door.transform.parent.Find(ForgeDoorName) : null;
            if (oldDoor) Object.DestroyImmediate(oldDoor.gameObject);

            var root = new GameObject(ForgeRootName).transform;
            root.position = ForgeOrigin;

            // Paredes de pedra: uma janela de cada lado; porta no meio do sul.
            TavernShell(root, (side, i) => side switch
            {
                'S' => i == 2 ? "wall-wood-door" : "wall-wood",
                'O' => i == 1 ? "wall-wood-window-shutters" : "wall-wood",
                'L' => i == 1 ? "wall-wood-window-shutters" : "wall-wood",
                _ => "wall-wood",
            });
            var stuff = new GameObject("Moveis").transform;
            stuff.SetParent(root, false);
            ForgeFurniture(stuff);
            WarehouseLights(root);
            // A Forja de verdade fica de lado (45°) no bairro oeste: lá fora vai a vila de sempre, sem vizinho de serviço.
            TavernOutside(root);

            Physics.SyncTransforms();
            BakeNavMeshVolume("NavMesh_Forja", "_NavMesh_Forja", root.TransformPoint(new Vector3(0f, -0.5f, 0f)),
                new Vector3(RoomHalfX * 2f, 4f, RoomHalfZ * 2f), 0.04f);
            ForgePeople(root);

            door.SetSignOnly(true);
            door.SetAction("Entrar");
            var outsideArrival = new GameObject("Chegada_Porto").transform;
            outsideArrival.SetParent(root, false);
            outsideArrival.SetPositionAndRotation(door.transform.position + door.transform.forward * 1.2f + Vector3.up * 0.05f, door.transform.rotation);
            var insideArrival = new GameObject("Chegada_Forja").transform;
            insideArrival.SetParent(root, false);
            insideArrival.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, -RoomHalfZ + 2.4f), Quaternion.identity);

            var outsidePassage = new GameObject(ForgeDoorName);
            outsidePassage.transform.SetParent(door.transform.parent, false);
            outsidePassage.transform.SetPositionAndRotation(door.transform.position, door.transform.rotation);
            outsidePassage.AddComponent<RuinPassage>().Configure("Entrar na Forja", insideArrival, "Forja", "munições e encomendas do ferreiro");
            var exit = new GameObject("Saida").transform;
            exit.SetParent(root, false);
            exit.localPosition = new Vector3(0f, 0f, -RoomHalfZ + 0.4f);
            exit.gameObject.AddComponent<RuinPassage>().Configure("Sair para o porto", outsideArrival, null, null);

            var zone = root.gameObject.AddComponent<CoveredZone>();
            zone.Configure(switcher, new Bounds(root.TransformPoint(new Vector3(0f, 2f, 0f)), new Vector3(RoomHalfX * 2f + 2f, 8f, RoomHalfZ * 2f + 2f)));
            zone.SetLook(new CoveredLook
            {
                light = new Color(1f, 0.78f, 0.55f), intensity = 0.45f, angles = new Vector2(62f, 160f), shadow = 0.6f,
                ambient = new Color(0.3f, 0.22f, 0.16f), music = false, portMusic = true, air = false, rainOnRoof = 0.45f,
                background = new Color(0.012f, 0.03f, 0.06f), outsideWeather = true, indoorMix = 0.35f,
            });
            Physics.SyncTransforms();
            return $"ok: forja em {ForgeOrigin}";
        }

        // ---------- Móveis ----------

        static void ForgeFurniture(Transform f)
        {
            var stone = TavernMaterial("M_Taverna_Pedra", new Color(0.34f, 0.3f, 0.27f));
            var soot = TavernMaterial("M_Taverna_Fuligem", new Color(0.08f, 0.07f, 0.06f));
            var coal = TavernMaterial("M_Forja_Carvao", new Color(0.09f, 0.08f, 0.08f), 0.25f);
            var ember = TavernMaterial("M_Forja_Brasa", new Color(1f, 0.42f, 0.12f));
            ember.SetColor("_EmissionColor", new Color(1f, 0.38f, 0.08f) * 2.2f);
            Emissive(ember);
            var iron = TavernMaterial("M_Forja_Ferro", new Color(0.3f, 0.31f, 0.33f), 0.45f);
            var leather = TavernMaterial("M_Forja_Couro", new Color(0.42f, 0.26f, 0.15f));
            var water = TavernMaterial("M_Forja_Agua", new Color(0.2f, 0.32f, 0.38f), 0.85f);

            // A forja: fornalha de pedra encostada na parede norte, carvão em brasa em cima, fogo, coifa e chaminé.
            var hearth = new GameObject("Fornalha").transform;
            hearth.SetParent(f, false);
            hearth.localPosition = new Vector3(-1.6f, 0f, RoomHalfZ - 0.75f);
            // Virada para a sala: no espaço dela, +Z é a frente (a borda e os pilares) e −Z o fundo encostado na parede.
            hearth.localRotation = Quaternion.Euler(0f, 180f, 0f);
            void Box(Transform p, string n, Vector3 c, Vector3 s, Material m, float yaw = 0f)
            {
                var g = Part(PrimitiveType.Cube, n, p, c, s, Quaternion.Euler(0f, yaw, 0f), m);
                Object.DestroyImmediate(g.GetComponent<Collider>());
            }
            Box(hearth, "Base", new Vector3(0f, 0.25f, 0f), new Vector3(2.0f, 0.5f, 1.2f), stone);
            Box(hearth, "Borda_Frente", new Vector3(0f, 0.55f, 0.52f), new Vector3(2.0f, 0.1f, 0.16f), stone);
            Box(hearth, "Carvao", new Vector3(0f, 0.53f, -0.05f), new Vector3(1.7f, 0.06f, 0.9f), coal);
            Box(hearth, "Brasa", new Vector3(0.05f, 0.565f, -0.1f), new Vector3(0.9f, 0.02f, 0.55f), ember);
            for (int k = 0; k < 9; k++)
                Box(hearth, "Brasa_Pedaco", new Vector3(-0.6f + (k % 5) * 0.3f, 0.585f, -0.35f + (k / 5) * 0.4f), new Vector3(0.12f, 0.06f, 0.1f),
                    k % 3 == 0 ? coal : ember, k * 37f);
            Box(hearth, "Fundo", new Vector3(0f, 1.1f, -0.52f), new Vector3(2.0f, 1.2f, 0.16f), soot);
            Box(hearth, "Coifa", new Vector3(0f, 1.95f, -0.2f), new Vector3(1.9f, 0.5f, 0.8f), stone);
            Box(hearth, "Chamine", new Vector3(0f, 2.6f, -0.35f), new Vector3(1.1f, 0.9f, 0.5f), stone);
            for (int s = -1; s <= 1; s += 2)
                Box(hearth, "Pilar", new Vector3(s * 0.9f, 1.2f, 0.35f), new Vector3(0.2f, 1.4f, 0.2f), stone);
            var hearthBox = hearth.gameObject.AddComponent<BoxCollider>();
            hearthBox.center = new Vector3(0f, 1.2f, -0.05f);
            hearthBox.size = new Vector3(2.0f, 2.4f, 1.25f);
            Occluder(hearth.gameObject);
            Fire(hearth, hearth.TransformPoint(new Vector3(0.05f, 0.58f, -0.1f)), 0.7f);
            for (int i = hearth.childCount - 1; i >= 0; i--)
                if (hearth.GetChild(i).GetComponent<ParticleSystem>() && hearth.GetChild(i).name != "Fogo") Object.DestroyImmediate(hearth.GetChild(i).gameObject);

            // Fole de couro ao lado da fornalha (duas tábuas e o couro no meio) e o bico entrando nela.
            var bellows = new GameObject("Fole").transform;
            bellows.SetParent(f, false);
            bellows.localPosition = new Vector3(-3.15f, 0.42f, RoomHalfZ - 0.75f);
            bellows.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var wood = TavernMaterial("M_Taverna_Madeira", new Color(0.45f, 0.3f, 0.18f));
            Box(bellows, "Tabua_Cima", new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.04f, 0.8f), wood);
            Box(bellows, "Tabua_Baixo", new Vector3(0f, -0.12f, 0f), new Vector3(0.5f, 0.04f, 0.8f), wood);
            Box(bellows, "Couro", new Vector3(0f, 0f, -0.05f), new Vector3(0.46f, 0.22f, 0.66f), leather);
            Box(bellows, "Bico", new Vector3(0f, 0f, 0.5f), new Vector3(0.08f, 0.08f, 0.3f), iron);
            Furnish(f, Kit.Props, "Crate_Wooden", new Vector3(-3.15f, 0f, RoomHalfZ - 0.75f), 0f, 0.4f, "Apoio_Fole", solid: true);

            // Bigorna grande na frente da forja (o ferreiro martela nela) e o barril de têmpera.
            Furnish(f, Kit.Props, "Anvil_Log", new Vector3(-1.6f, 0f, 2.6f), 90f, 0.45f, "Bigorna", solid: true);
            Furnish(f, Kit.Pirate, "barrel", new Vector3(0.4f, 0f, 4.2f), 0f, 0.5f, "Barril_Tempera", solid: true);
            var w = Part(PrimitiveType.Cylinder, "Agua", f, new Vector3(0.4f, 0.57f, 4.2f), new Vector3(0.42f, 0.01f, 0.42f), Quaternion.identity, water);
            Object.DestroyImmediate(w.GetComponent<Collider>());
            Furnish(f, Kit.Props, "Sword_Bronze", new Vector3(0.55f, 0.35f, 4.15f), 25f, 0.55f, "Lamina_Esfriando");

            // A bigorna pequena do aprendiz (ajoelhado, ver ForgePeople), meio metro à frente dele.
            Furnish(f, Kit.Props, "Anvil", new Vector3(2.4f, 0f, 2.0f) + Quaternion.Euler(0f, -20f, 0f) * new Vector3(0f, 0f, 0.5f), 70f, 0.3f,
                "Bigorna_Pequena", solid: true);

            // Monte de carvão no canto noroeste (pedras do kit pintadas de carvão).
            foreach (var (at, yaw, sc) in new[] { (new Vector3(-5.6f, 0f, 4.9f), 10f, 1.4f), (new Vector3(-6.5f, 0f, 4.2f), 80f, 1.1f), (new Vector3(-5.0f, 0f, 4.2f), -30f, 0.8f) })
            {
                var pile = Furnish(f, Kit.Castle, "rocks-small", at, yaw, sc, "Carvao", solid: true);
                foreach (var r in pile.GetComponentsInChildren<Renderer>()) r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, _ => coal);
            }
            Furnish(f, Kit.Survival, "tool-shovel", new Vector3(-4.3f, 0f, 4.9f), 40f, 2.4f, "Pa_Carvao");

            // Lingotes de ferro empilhados e caixas de metal (oeste).
            var ingots = new GameObject("Lingotes").transform;
            ingots.SetParent(f, false);
            ingots.localPosition = new Vector3(-6.5f, 0f, 2.4f);
            var ingotBox = ingots.gameObject.AddComponent<BoxCollider>();
            ingotBox.center = new Vector3(0f, 0.18f, 0f);
            ingotBox.size = new Vector3(0.75f, 0.36f, 0.75f);
            for (int layer = 0; layer < 4; layer++)
                for (int k = 0; k < 4; k++)
                {
                    bool across = layer % 2 == 1;
                    var p = across ? new Vector3(-0.27f + k * 0.18f, 0.045f + layer * 0.085f, 0f) : new Vector3(0f, 0.045f + layer * 0.085f, -0.27f + k * 0.18f);
                    Box(ingots, "Lingote", p, across ? new Vector3(0.14f, 0.08f, 0.65f) : new Vector3(0.65f, 0.08f, 0.14f), iron);
                }
            Furnish(f, Kit.Props, "Crate_Metal", new Vector3(-6.6f, 0f, 1.2f), 10f, 0.6f, "Caixa_Metal", solid: true);
            Furnish(f, Kit.Props, "Crate_Metal", new Vector3(-6.6f, 0.52f, 1.2f), -12f, 0.5f, "Caixa_Metal");

            // Parede oeste: suporte de armas, escudo e espadas penduradas; a bancada com ferramentas.
            Furnish(f, Kit.Props, "WeaponStand", new Vector3(-6.75f, 0f, -0.6f), 90f, 0.6f, "Suporte_Armas", solid: true);
            Furnish(f, Kit.Props, "Shield_Wooden", new Vector3(-RoomHalfX + 0.14f, 1.45f, -2.2f), 90f, 0.8f, "Escudo_Parede");
            for (int k = 0; k < 2; k++)
                Furnish(f, Kit.Props, "Sword_Bronze", new Vector3(-RoomHalfX + 0.12f, 0.95f, -1.4f + k * 0.35f), 90f, 0.6f, "Espada_Parede");
            Furnish(f, Kit.Props, "Workbench", new Vector3(-6.8f, 0f, -3.6f), 90f, new Vector3(0.55f, 0.5f, 0.6f), "Bancada", solid: true);
            Furnish(f, Kit.Survival, "tool-hammer", new Vector3(-6.75f, 0.45f, -3.9f), 20f, 2.4f, "Martelo");
            Furnish(f, Kit.Props, "Axe_Bronze", new Vector3(-6.85f, 0.45f, -3.3f), 80f, 0.6f, "Machado_Bancada");
            Furnish(f, Kit.Props, "Barrel_Holder", new Vector3(-6.5f, 0f, -5.2f), 0f, 0.55f, "Barris_Sucata", solid: true);

            // Leste: o rebolo, as balas de canhão em pirâmide, a corrente, ferramentas na parede.
            Furnish(f, Kit.Props, "Whetstone", new Vector3(4.6f, 0f, 4.7f), 180f, 0.5f, "Rebolo", solid: true);
            int ball = 0;
            for (int layer = 0; layer < 3; layer++)
                for (int a = 0; a < 3 - layer; a++)
                    for (int b = 0; b < 3 - layer; b++)
                        Furnish(f, Kit.Pirate, "cannon-ball", new Vector3(6.2f + (a - (2 - layer) * 0.5f) * 0.3f, layer * 0.25f, 2.2f + (b - (2 - layer) * 0.5f) * 0.3f),
                            0f, 0.45f, $"Bala_{ball++}");
            Furnish(f, Kit.Props, "Chain_Coil", new Vector3(5.0f, 0f, 2.4f), 30f, 0.6f, "Corrente");
            Furnish(f, Kit.Props, "Peg_Rack", new Vector3(RoomHalfX - 0.12f, 1.45f, 0.4f), -90f, 1f, "Ferramentas_Parede");
            Furnish(f, Kit.Props, "Bucket_Metal", new Vector3(4.0f, 0f, 1.3f), 0f, 0.8f, "Balde");
            Furnish(f, Kit.Props, "Cauldron", new Vector3(6.6f, 0f, -0.4f), 0f, 0.5f, "Caldeirao", solid: true);

            // Meio da sala: a mesa de trabalho com as encomendas em andamento (lâminas, uma carga de corrente, cartuchos) e um canhão
            // sobre o cavalete, esperando conserto.
            Furnish(f, Kit.Props, "Workbench", new Vector3(-3.2f, 0f, -0.6f), 0f, new Vector3(0.6f, 0.5f, 0.6f), "Mesa_Trabalho", solid: true);
            Furnish(f, Kit.Props, "Sword_Bronze", new Vector3(-3.5f, 0.45f, -0.6f), 0f, new Vector3(0.6f, 0.6f, 0.6f), "Lamina_em_Obra");
            var laid = f.Find("Lamina_em_Obra");
            laid.localRotation = Quaternion.Euler(90f, 70f, 0f);
            laid.localPosition = new Vector3(-3.5f, 0.47f, -0.45f);
            for (int s = -1; s <= 1; s += 2)
                Furnish(f, Kit.Pirate, "cannon-ball", new Vector3(-2.7f + s * 0.14f, 0.45f, -0.7f), 0f, 0.28f, "Carga_Corrente");
            Box(f, "Corrente_da_Carga", new Vector3(-2.7f, 0.53f, -0.7f), new Vector3(0.22f, 0.025f, 0.025f), iron);
            Furnish(f, Kit.Props, "Pouch_Large", new Vector3(-2.9f, 0.45f, -0.4f), 40f, 0.4f, "Cartuchos_Mesa");
            Furnish(f, Kit.Pirate, "cannon", new Vector3(2.6f, 0f, -1.4f), 200f, 0.5f, "Canhao_Conserto", solid: true);
            Furnish(f, Kit.Survival, "tool-hammer", new Vector3(3.3f, 0f, -1.0f), 120f, 2.4f, "Martelo_Chao");
            Furnish(f, Kit.Pirate, "barrel", new Vector3(3.6f, 0f, -2.2f), -10f, 0.45f, "Barril_Polvora", solid: true);

            // Balcão do ferreiro (sudeste): o livro das encomendas, cartuchos, uma bomba, moedas e a vela.
            Furnish(f, Kit.Props, "Table_Large", new Vector3(5.2f, 0f, -3.2f), 0f, new Vector3(0.45f, TableTop / 0.81f, 0.6f), "Balcao_Ferreiro", solid: true);
            Furnish(f, Kit.Props, "Book_5", new Vector3(4.75f, TableTop + 0.01f, -3.25f), 80f, 1.1f, "Livro_Encomendas");
            Furnish(f, Kit.Props, "Pouch_Large", new Vector3(5.35f, TableTop, -3.15f), 20f, 0.5f, "Cartuchos");
            Furnish(f, Kit.Pirate, "cannon-ball", new Vector3(5.75f, TableTop, -3.3f), 0f, 0.3f, "Bomba");
            Furnish(f, Kit.Props, "Coin_Pile", new Vector3(5.1f, TableTop, -3.45f), 0f, 0.5f, "Moedas");
            NightLantern(Furnish(f, Kit.Props, "CandleStick", new Vector3(5.85f, TableTop + 0.01f, -3.0f), 0f, 1f, "Vela_Ferreiro"), true, 3f, 1.2f);
            Spot(f, "Balcao_do_Ferreiro", new Vector3(5.2f, 0f, -4.2f), 180f, "Balcão do ferreiro", "munições e encomendas",
                HarborService.Smithy, "Falar com");

            // Sul: caixotes e um barril nos cantos.
            Furnish(f, Kit.Props, "Crate_Wooden", new Vector3(6.75f, 0f, -5.2f), -15f, 0.75f, "Caixote", solid: true);
            Furnish(f, Kit.Pirate, "barrel", new Vector3(-3.6f, 0f, -5.35f), 20f, 0.5f, "Barril", solid: true);
        }

        // ---------- Gente ----------

        static void ForgePeople(Transform root)
        {
            var people = new GameObject("Gente").transform;
            people.SetParent(root, false);
            Vector3 P(float x, float z) => root.TransformPoint(new Vector3(x, 0f, z));
            Townsfolk(people, "Ferreiro", P(5.2f, -2.2f), 180f, 3, null);
            // O mestre ferreiro martela na bigorna grande (de costas para a sala, de frente para a forja); o aprendiz ajoelhado numa bigorna
            // pequena no chão.
            Carpenter(people, "Ferreiro_Bigorna", root, new Vector3(-1.6f, 0f, 2.05f), 0f, 5, "Martelar_Parede", true, WorkMaterial.Metal);
            Carpenter(people, "Aprendiz", root, new Vector3(2.4f, 0f, 2.0f), -20f, 6, "Martelar_Chao", false, WorkMaterial.Metal);
            // Mais devagar que os carpinteiros (13/11, pedido do Jhonata: o metal batendo toda hora irritava): pausas maiores, descansos
            // mais longos.
            foreach (var n in new[] { "Ferreiro_Bigorna", "Aprendiz" })
                people.Find(n).GetComponent<Ambient.Worker>().SetPacing(new Vector2(0.8f, 1.8f), new Vector2Int(2, 4), new Vector2(6f, 12f), 26f);
        }
    }
}
