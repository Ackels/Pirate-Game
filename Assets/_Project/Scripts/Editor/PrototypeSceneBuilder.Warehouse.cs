using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Armazém por dentro (13/11, interiores passo 2; pedido do Jhonata depois da taverna). Mesma receita da taverna (sala longe do mapa,
    /// porta que escurece e leva para dentro, zona coberta com a luz do lugar, a vila de noite em volta, agora com a taverna ao lado como no
    /// porto): prateleiras, caixotes, sacos e cordas; o <b>balcão do almoxarife</b> (o F abre a carga: porão, armazém e mochila); dois
    /// estivadores andando pelos corredores (NavMesh da sala) e um parado; e <b>as pilhas de mercadoria aparecem conforme o que está guardado</b>
    /// (<see cref="WarehouseDisplay"/>: madeira, pedra, ferro, pólvora, ouro, especiarias, rum, cordame, lona e breu). Toca a música do porto.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static Vector3 WarehouseOrigin => new(2000f, 30f, -4000f);
        const string WarehouseRootName = "Interior_Armazem", WarehouseDoorName = "Porta_Armazem";

        [MenuItem("Pirates/Protótipo/Interiores: refazer o armazém")]
        static void MenuWarehouseInterior() => Debug.Log(AddWarehouseInteriorToOpenScene());

        /// <summary>Monta (ou refaz) o interior do armazém na cena aberta e salva. Só na cena com o porto a pé (07).</summary>
        public static string AddWarehouseInteriorToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            generatingScenePath = scene.path;
            string r = AddWarehouseInterior();
            if (!r.StartsWith("ok")) return r;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }

        static string AddWarehouseInterior()
        {
            HarborBuilding door = null;
            foreach (var b in Object.FindObjectsByType<HarborBuilding>(FindObjectsInactive.Include))
                if (b.Service == HarborService.Cargo && b.name.StartsWith("Entrada_")) door = b;
            if (!door) return "sem a porta do armazém (só na cena com o porto a pé)";
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>();
            if (!switcher || !switcher.Character) return "sem o capitão";
            if (!switcher.Character.GetComponent<PassageWalker>()) return "o capitão não tem o PassageWalker (a Ermida põe)";
            var dock = Object.FindAnyObjectByType<HarborDock>(FindObjectsInactive.Include);

            foreach (var n in new[] { WarehouseRootName, WarehouseDoorName, "NavMesh_Armazem" })
            {
                var old = GameObject.Find(n);
                if (old) Object.DestroyImmediate(old);
            }

            var root = new GameObject(WarehouseRootName).transform;
            root.position = WarehouseOrigin;

            // Paredes: sem janela ao norte (prateleiras), uma janela a oeste e uma a leste; porta no meio do sul.
            TavernShell(root, (side, i) => side switch
            {
                'S' => i == 2 ? "wall-wood-door" : "wall-wood",
                'O' => i == 1 ? "wall-wood-window-shutters" : "wall-wood",
                'L' => i == 2 ? "wall-wood-window-shutters" : "wall-wood",
                _ => "wall-wood",
            });
            var stuff = new GameObject("Moveis").transform;
            stuff.SetParent(root, false);
            WarehouseFurniture(stuff);
            var piles = WarehousePiles(stuff);
            if (dock) stuff.gameObject.AddComponent<WarehouseDisplay>().Configure(dock, piles);
            WarehouseLights(root);
            // A taverna de verdade fica 34 m a oeste do armazém no porto: aqui, de costas para o porto, 34 m à direita.
            TavernOutside(root, (TavernHouse, new Vector2(34f, 0f)));

            // NavMesh da sala (os estivadores andam), antes de pôr gente.
            Physics.SyncTransforms();
            BakeNavMeshVolume("NavMesh_Armazem", "_NavMesh_Armazem", root.TransformPoint(new Vector3(0f, -0.5f, 0f)),
                new Vector3(RoomHalfX * 2f, 4f, RoomHalfZ * 2f), 0.04f);
            WarehousePeople(root);

            door.SetSignOnly(true);
            door.SetAction("Entrar");
            var outsideArrival = new GameObject("Chegada_Porto").transform;
            outsideArrival.SetParent(root, false);
            outsideArrival.SetPositionAndRotation(door.transform.position + door.transform.forward * 2.2f + Vector3.up * 0.05f, door.transform.rotation);
            var insideArrival = new GameObject("Chegada_Armazem").transform;
            insideArrival.SetParent(root, false);
            insideArrival.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, -RoomHalfZ + 2.4f), Quaternion.identity);

            var outsidePassage = new GameObject(WarehouseDoorName);
            outsidePassage.transform.SetPositionAndRotation(door.transform.position, door.transform.rotation);
            outsidePassage.AddComponent<RuinPassage>().Configure("Entrar no Armazém", insideArrival, "Armazém", "carga do porão e mercadoria guardada");
            var exit = new GameObject("Saida").transform;
            exit.SetParent(root, false);
            exit.localPosition = new Vector3(0f, 0f, -RoomHalfZ + 0.4f);
            exit.gameObject.AddComponent<RuinPassage>().Configure("Sair para o porto", outsideArrival, null, null);

            var zone = root.gameObject.AddComponent<CoveredZone>();
            zone.Configure(switcher, new Bounds(root.TransformPoint(new Vector3(0f, 2f, 0f)), new Vector3(RoomHalfX * 2f + 2f, 8f, RoomHalfZ * 2f + 2f)));
            zone.SetLook(new CoveredLook
            {
                light = new Color(1f, 0.88f, 0.7f), intensity = 0.5f, angles = new Vector2(64f, -30f), shadow = 0.6f,
                ambient = new Color(0.3f, 0.25f, 0.2f), music = false, portMusic = true, air = false, rainOnRoof = 0.45f, outsideWeather = true, indoorMix = 0.35f,
                background = new Color(0.012f, 0.03f, 0.06f),
            });
            Physics.SyncTransforms();
            return $"ok: armazém em {WarehouseOrigin}, {piles.Length} mercadorias com pilhas";
        }

        // ---------- Móveis fixos ----------

        static void WarehouseFurniture(Transform f)
        {
            // Prateleiras na parede norte: 4 vãos com 3 tábuas cada (a Shelf_Simple do kit é uma tábua de parede), mercadoria miúda em cima.
            var rng = new System.Random(41);
            string[] small = { "Crate_Wooden", "Bag", "Pot_1", "Vase_4", "Crate_Wooden", "Bucket_Wooden_1", "Pouch_Large" };
            float[] smallScale = { 0.3f, 0.35f, 0.6f, 0.55f, 0.3f, 0.8f, 0.6f };
            for (int i = 0; i < 4; i++)
            {
                float x = -5.4f + i * 3.6f;
                for (int k = 0; k < 3; k++)
                {
                    float y = 0.45f + k * 0.5f;
                    Furnish(f, Kit.Props, "Shelf_Simple", new Vector3(x, y, RoomHalfZ - 0.22f), 180f, new Vector3(1.3f, 1f, 1.3f), $"Prateleira_{i}_{k}");
                    float top = y + 0.31f;
                    for (int m = 0; m < 3; m++)
                    {
                        if (rng.NextDouble() < 0.2) continue;
                        int pick = rng.Next(small.Length);
                        Furnish(f, Kit.Props, small[pick], new Vector3(x - 0.5f + m * 0.5f, top, RoomHalfZ - 0.24f), rng.Next(360), smallScale[pick], "Na_Prateleira");
                    }
                }
            }

            // Carga fixa: caixotes empilhados nos cantos e ao longo das paredes, sacos, barris de água e a carroça.
            void Crates(Vector3 at, float yaw, int high)
            {
                for (int h = 0; h < high; h++)
                    Furnish(f, Kit.Props, "Crate_Wooden", at + new Vector3(0f, h * 0.66f, 0f), yaw + h * 17f, 0.75f, "Caixote", solid: h == 0);
            }
            Crates(new Vector3(-6.7f, 0f, -5.2f), 10f, 3);
            Crates(new Vector3(-5.9f, 0f, -5.3f), -8f, 2);
            Crates(new Vector3(-6.75f, 0f, -4.4f), 30f, 1);
            Crates(new Vector3(6.75f, 0f, -5.2f), -15f, 2);
            Crates(new Vector3(-6.75f, 0f, 5.2f), 5f, 2);
            Crates(new Vector3(6.75f, 0f, 5.2f), -20f, 3);
            foreach (var (at, yaw) in new[] { (new Vector3(-3.4f, 0f, -5.4f), 60f), (new Vector3(-2.9f, 0f, -5.5f), 150f), (new Vector3(-3.15f, 0.5f, -5.45f), 100f),
                         (new Vector3(-6.8f, 0f, -1.4f), 20f), (new Vector3(-6.75f, 0f, -0.8f), 80f) })
                Furnish(f, Kit.Props, "Bag", at, yaw, 0.7f, "Saco", solid: at.y == 0f);
            foreach (var at in new[] { new Vector3(2.3f, 0f, -5.3f), new Vector3(3.05f, 0f, -5.35f) })
                Furnish(f, Kit.Pirate, "barrel", at, at.x * 40f, 0.5f, "Barril_Agua", solid: true);
            Furnish(f, Kit.Props, "Stall_Cart_Empty", new Vector3(-4.9f, 0f, -4.1f), 25f, 0.45f, "Carroca", solid: true);
            Furnish(f, Kit.Props, "Bag", new Vector3(-4.75f, 0.41f, -4.05f), 10f, 0.5f, "Saco_na_Carroca");
            Furnish(f, Kit.Props, "Peg_Rack", new Vector3(-RoomHalfX + 0.12f, 1.35f, -3.0f), 90f, 0.9f, "Cabideiro");
            Furnish(f, Kit.Props, "Rope_3", new Vector3(-RoomHalfX + 0.35f, 0f, -2.2f), 0f, 1.4f, "Corda");
            Furnish(f, Kit.Props, "Chain_Coil", new Vector3(RoomHalfX - 0.45f, 0f, 2.6f), 0f, 1f, "Corrente");

            // Mais carga (13/11, pedido do Jhonata: "mais caixas, pilhas"): uma ilha no meio e pilhas ao longo das paredes, deixando os corredores
            // dos estivadores e o caminho da porta até o balcão livres.
            var cargo = new System.Random(97);
            void Pile(Vector3 at, int high)
            {
                // Caixotes de dois kits misturados, sacos ou barril por cima de vez em quando.
                for (int h = 0; h < high; h++)
                {
                    bool kenney = cargo.NextDouble() < 0.4;
                    float yaw = cargo.Next(-12, 12) + (cargo.NextDouble() < 0.5 ? 0f : 90f);
                    if (kenney) Furnish(f, Kit.Pirate, "crate", at + new Vector3(0f, h * 0.62f, 0f), yaw, 0.6f, "Caixote", solid: h == 0);
                    else Furnish(f, Kit.Props, "Crate_Wooden", at + new Vector3(0f, h * 0.62f, 0f), yaw, 0.7f, "Caixote", solid: h == 0);
                }
                double extra = cargo.NextDouble();
                if (extra < 0.3) Furnish(f, Kit.Props, "Bag", at + new Vector3(0.05f, high * 0.62f, 0.05f), cargo.Next(360), 0.55f, "Saco_em_Cima");
                else if (extra < 0.42) Furnish(f, Kit.Props, "Rope_1", at + new Vector3(0f, high * 0.62f, 0f), cargo.Next(360), 1.3f, "Corda_em_Cima");
            }
            // Ilha do meio (x −1,4…1,2, z −0,3…1,4): 3 × 2 pilhas de alturas diferentes.
            int[,] heights = { { 2, 3, 1 }, { 1, 2, 3 } };
            for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
                Pile(new Vector3(-0.95f + c * 0.95f, 0f, 0.15f + r * 0.95f), heights[r, c]);
            Furnish(f, Kit.Props, "Bag", new Vector3(1.25f, 0f, -0.35f), 30f, 0.7f, "Saco", solid: true);
            Furnish(f, Kit.Props, "Bag", new Vector3(-1.4f, 0f, -0.3f), 200f, 0.65f, "Saco", solid: true);
            // Ao longo das paredes.
            foreach (var (at, high) in new[]
            {
                (new Vector3(-6.75f, 0f, -3.55f), 2), (new Vector3(-6.75f, 0f, 1.45f), 1),             // oeste
                (new Vector3(4.0f, 0f, -5.35f), 2), (new Vector3(4.85f, 0f, -5.35f), 1),               // sul, perto do balcão
                (new Vector3(1.4f, 0f, -5.35f), 1),                                                    // sul, perto da porta
                (new Vector3(-4.6f, 0f, 5.25f), 1), (new Vector3(4.6f, 0f, 5.3f), 2),                  // norte, entre as prateleiras
                (new Vector3(6.8f, 0f, -1.7f), 1),                                                     // leste
                (new Vector3(-4.45f, 0f, -2.65f), 2), (new Vector3(-3.55f, 0f, -2.6f), 1), (new Vector3(-4.45f, 0f, -1.75f), 3), // oeste da entrada
                (new Vector3(2.5f, 0f, -4.5f), 2), (new Vector3(3.35f, 0f, -4.55f), 1),               // perto do balcão
                (new Vector3(-3.6f, 0f, 5.3f), 3), (new Vector3(0f, 0f, 5.3f), 2),                     // entre as prateleiras
            })
                Pile(at, high);
            foreach (var (at, yaw) in new[] { (new Vector3(6.8f, 0f, -0.95f), 10f), (new Vector3(-1.9f, 0f, -5.45f), 70f), (new Vector3(-6.8f, 0f, 0.7f), 120f) })
                Furnish(f, Kit.Pirate, "barrel", at, yaw, 0.5f, "Barril", solid: true);

            // Balcão do almoxarife (sudeste): mesa com o livro, as moedas, a chave e a vela; o baú atrás.
            Furnish(f, Kit.Props, "Table_Large", new Vector3(5.5f, 0f, -3.2f), 90f, new Vector3(0.45f, TableTop / 0.81f, 0.75f), "Mesa_Almoxarife", solid: true);
            Furnish(f, Kit.Props, "Book_7", new Vector3(5.5f, TableTop + 0.01f, -3.4f), 80f, 1.1f, "Livro_do_Armazem");
            Furnish(f, Kit.Props, "Coin_Pile", new Vector3(5.4f, TableTop + 0.01f, -2.85f), 0f, 1.5f);
            Furnish(f, Kit.Props, "Key_Gold", new Vector3(5.6f, TableTop + 0.01f, -3.8f), 30f, 2f);
            NightLantern(Furnish(f, Kit.Props, "CandleStick", new Vector3(5.65f, TableTop + 0.01f, -2.6f), 0f, 1f, "Vela_Almoxarife"), true, 3f, 1.2f);
            Furnish(f, Kit.Props, "Chest_Wood", new Vector3(6.8f, 0f, -3.2f), -90f, 0.42f, "Bau_Almoxarife", solid: true);
            Spot(f, "Balcao_do_Almoxarife", new Vector3(4.25f, 0f, -3.2f), -90f, "Balcão do almoxarife", "carga do porão, armazém e mochila",
                HarborService.Cargo, "Falar com");
        }

        // ---------- Pilhas que mostram o estoque ----------

        static ItemDefinition StockItem(string name) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{name}.asset");

        /// <summary>
        /// Cada mercadoria com até 3 pilhas no chão, ligadas pela quantidade no armazém (<see cref="WarehouseDisplay"/>). Começam desligadas.
        /// </summary>
        static WarehouseDisplay.Pile[] WarehousePiles(Transform f)
        {
            var root = new GameObject("Estoque").transform;
            root.SetParent(f, false);
            var list = new List<WarehouseDisplay.Pile>();

            GameObject Group(string name)
            {
                var g = new GameObject(name);
                g.transform.SetParent(root, false);
                return g;
            }

            void Add(string item, int[] at, params System.Action<Transform>[] tiers)
            {
                var def = StockItem(item);
                if (!def) return;
                var objs = new GameObject[tiers.Length];
                for (int k = 0; k < tiers.Length; k++)
                {
                    var g = Group($"{item}_{k + 1}");
                    tiers[k](g.transform);
                    // Tudo 35% maior que o tamanho de partida (no salão de 15 × 12 m as pilhas sumiam).
                    foreach (Transform c in g.transform) c.localScale *= 1.35f;
                    g.SetActive(false);
                    objs[k] = g;
                }
                list.Add(new WarehouseDisplay.Pile { item = def, at = at, tiers = objs });
            }

            // Oeste: madeira e pedra.
            Add("Item_Madeira", new[] { 1, 30, 100 },
                t => Furnish(t, Kit.Survival, "resource-wood", new Vector3(-6.2f, 0f, 3.9f), 90f, 4.5f, "Madeira", solid: true),
                t => Furnish(t, Kit.Nature, "log_stack", new Vector3(-6.2f, 0f, 2.3f), 90f, 1.6f, "Toras", solid: true),
                t => Furnish(t, Kit.Survival, "resource-planks", new Vector3(-4.7f, 0f, 3.9f), 0f, 2.6f, "Tabuas", solid: true));
            Add("Item_Pedra", new[] { 1, 30, 80 },
                t => Furnish(t, Kit.Survival, "resource-stone", new Vector3(-6.3f, 0f, 0.5f), 20f, 4.5f, "Pedra", solid: true),
                t => Furnish(t, Kit.Survival, "resource-stone-large", new Vector3(-5.0f, 0f, 0.6f), -30f, 2.8f, "Pedra_Grande", solid: true));
            // Meio, junto das prateleiras: ouro e especiarias.
            Add("Item_Ouro", new[] { 1, 300, 1500 },
                t => Furnish(t, Kit.Props, "Coin_Pile", new Vector3(0.5f, 0f, 4.2f), 0f, 3.5f, "Moedas"),
                t => Furnish(t, Kit.Props, "Coin_Pile_2", new Vector3(-0.4f, 0f, 4.3f), 40f, 3.5f, "Moedas_2"),
                t =>
                {
                    Furnish(t, Kit.Props, "Chest_Wood", new Vector3(0.1f, 0f, 3.3f), 180f, 0.45f, "Bau_de_Ouro", solid: true);
                    Furnish(t, Kit.Props, "Coin_Pile_2", new Vector3(0.95f, 0f, 3.2f), 10f, 3f, "Moedas_3");
                });
            Add("Item_Especiarias", new[] { 1, 10, 30 },
                t => Furnish(t, Kit.Props, "Bag", new Vector3(-1.9f, 0f, 3.0f), 20f, 0.5f, "Especiarias", solid: true),
                t => Furnish(t, Kit.Props, "FarmCrate_Carrot", new Vector3(-2.3f, 0f, 3.9f), -10f, 1.1f, "Especiarias_2", solid: true));
            // Leste: ferro e pólvora no fundo; rum, cordame, lona e breu na frente.
            Add("Item_Ferro", new[] { 1, 15, 40 },
                t => Furnish(t, Kit.Props, "Crate_Metal", new Vector3(3.4f, 0f, 4.2f), 0f, 0.55f, "Ferro", solid: true),
                t => Furnish(t, Kit.Props, "Crate_Metal", new Vector3(3.4f, 0f, 3.2f), 15f, 0.55f, "Ferro_2", solid: true),
                t => Furnish(t, Kit.Props, "Crate_Metal", new Vector3(3.4f, 0.48f, 4.2f), -10f, 0.45f, "Ferro_3"));
            Add("Item_Polvora", new[] { 1, 15, 40 },
                t => Furnish(t, Kit.Pirate, "barrel", new Vector3(6.4f, 0f, 4.3f), 0f, 0.4f, "Polvora", solid: true),
                t => Furnish(t, Kit.Pirate, "barrel", new Vector3(5.6f, 0f, 4.4f), 30f, 0.4f, "Polvora_2", solid: true),
                t =>
                {
                    Furnish(t, Kit.Pirate, "barrel", new Vector3(6.4f, 0f, 3.5f), 60f, 0.4f, "Polvora_3", solid: true);
                    Furnish(t, Kit.Pirate, "barrel", new Vector3(5.6f, 0f, 3.6f), 10f, 0.4f, "Polvora_4", solid: true);
                });
            Add("Item_Rum", new[] { 1, 10, 30 },
                t => Furnish(t, Kit.Pirate, "crate-bottles", new Vector3(6.4f, 0f, 1.6f), 0f, 0.4f, "Rum", solid: true),
                t => Furnish(t, Kit.Pirate, "crate-bottles", new Vector3(5.6f, 0f, 1.6f), 20f, 0.4f, "Rum_2", solid: true));
            Add("Item_Cordame", new[] { 1, 15, 40 },
                t => Furnish(t, Kit.Props, "Rope_1", new Vector3(3.6f, 0f, 1.5f), 0f, 1.6f, "Cordame"),
                t => Furnish(t, Kit.Props, "Rope_2", new Vector3(3.8f, 0f, 0.5f), 50f, 1.3f, "Cordame_2"));
            Add("Item_Lona", new[] { 1, 15, 40 },
                t => Furnish(t, Kit.Survival, "bedroll-packed", new Vector3(6.4f, 0f, 0.2f), 90f, 3.5f, "Lona"),
                t => Furnish(t, Kit.Survival, "bedroll-packed", new Vector3(6.4f, 0.3f, 0.25f), 80f, 3.5f, "Lona_2"));
            Add("Item_Breu", new[] { 1, 10, 30 },
                t => Furnish(t, Kit.Props, "Bucket_Wooden_1", new Vector3(5.3f, 0f, 0.2f), 0f, 1.3f, "Breu"),
                t => Furnish(t, Kit.Survival, "barrel-open", new Vector3(5.2f, 0f, -0.7f), 0f, 1.5f, "Breu_2", solid: true));
            return list.ToArray();
        }

        // ---------- Gente e luz ----------

        static void WarehousePeople(Transform root)
        {
            var people = new GameObject("Gente").transform;
            people.SetParent(root, false);
            Vector3 P(float x, float z) => root.TransformPoint(new Vector3(x, 0f, z));
            Townsfolk(people, "Almoxarife", P(6.4f, -3.2f), -90f, 2, null);
            Townsfolk(people, "Estivador_Parado", P(-3.6f, 2.0f), 230f, 6, null);
            var aisleA = new[] { P(-2.6f, -1.6f), P(-2.4f, 2.0f), P(1.8f, 2.2f), P(1.6f, -1.0f) };
            var aisleB = new[] { P(2.2f, 0.6f), P(2.0f, -1.8f), P(-2.8f, -2.2f), P(-2.6f, 1.0f) };
            Townsfolk(people, "Estivador_1", aisleA[0], 0f, 5, aisleA);
            Townsfolk(people, "Estivador_2", aisleB[0], 180f, 7, aisleB);
        }

        static void WarehouseLights(Transform root)
        {
            var lights = new GameObject("Luzes").transform;
            lights.SetParent(root, false);
            foreach (var (at, n) in new[] { (new Vector3(-3.2f, RoomWall - 0.1f, 1.2f), "Lustre_Oeste"), (new Vector3(3.2f, RoomWall - 0.1f, 1.2f), "Lustre_Leste") })
            {
                var c = Furnish(lights, Kit.Props, "Chandelier", at, 0f, 0.6f, n);
                c.transform.localPosition -= Vector3.up * 0.85f;
                var l = new GameObject("LuzDoLustre").AddComponent<Light>();
                l.transform.SetParent(c.transform, false);
                l.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.76f, 0.46f);
                l.range = 7.5f;
                l.intensity = 2.2f;
                l.shadows = LightShadows.None;
            }
            foreach (var (at, yaw) in new[]
            {
                (new Vector3(-RoomHalfX + 0.43f, 1.3f, 2.0f), 90f), (new Vector3(RoomHalfX - 0.43f, 1.3f, -0.9f), -90f),
                (new Vector3(0f, 1.6f, RoomHalfZ - 0.43f), 180f), (new Vector3(-2.2f, 1.3f, -RoomHalfZ + 0.43f), 0f),
            })
                NightLantern(Furnish(lights, Kit.Props, "Lantern_Wall", at, yaw, 0.5f, "Lanterna_Parede"), true, 5f, 1.6f);
        }
    }
}
