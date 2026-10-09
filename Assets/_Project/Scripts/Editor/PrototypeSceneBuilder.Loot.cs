using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Parte de loot do gerador (Protótipo 04): itens (Data/Items), tabela de loot do inimigo,
    /// prefab dos destroços (Prefabs/Loot) e o saque no navio do jogador (porão com limite + janela de escolha).
    /// Itens e tabela existentes são preservados (são ajuste de game design); o prefab é
    /// regerado a cada build (é arte provisória, trocada no M6) e religado na tabela.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string ItemsFolder = DataFolder + "/Items";
        const string PrefabsFolder = Root + "/Prefabs";
        const string LootPrefabsFolder = PrefabsFolder + "/Loot";

        /// <summary>Espaço do porão da chalupa. Uma carga média (~29) quase enche; uma carga cheia (~45) obriga a escolher.</summary>
        // 30 → 45 (M9, pedido do Jhonata): agora se anda com munição e madeira do carpinteiro a bordo.
        const float SloopHoldCapacity = 45f;

        sealed class LootAssets
        {
            public LootTable enemyTable;
            public LootItems items;
            /// <summary>Esquemas de construção com os pesos do baú raro (o baú do acampamento usa os mesmos).</summary>
            public LootTable.WeightedItem[] schematics;
        }

        sealed class LootItems
        {
            public ItemDefinition gold, wood, powder, iron, crew;
        }

        static LootAssets LoadLootAssets(Material deck, Material sail)
        {
            EnsureFolder(DataFolder, "Items");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(PrefabsFolder, "Loot");

            var items = new LootItems
            {
                gold = Item("Item_Ouro", "Ouro", ItemCategory.Resource, ItemRarity.Common, new Color(1f, 0.84f, 0.3f), 0.05f,
                    "Moeda de troca: compra upgrades e paga a tripulação. Leve: 20 moedas ocupam 1 de espaço."),
                wood = Item("Item_Madeira", "Madeira", ItemCategory.Resource, ItemRarity.Common, new Color(0.85f, 0.64f, 0.42f), 1f,
                    "Conserta o casco e serve para construir na base."),
                powder = Item("Item_Polvora", "Pólvora", ItemCategory.Resource, ItemRarity.Common, new Color(0.78f, 0.78f, 0.82f), 2f,
                    "Munição dos canhões."),
                iron = Item("Item_Ferro", "Ferro", ItemCategory.Resource, ItemRarity.Uncommon, new Color(0.62f, 0.74f, 0.86f), 3f,
                    "Balas, canhões e reforço do casco. Pesado: ocupa muito espaço."),
                crew = Item("Item_Sobreviventes", "Sobreviventes", ItemCategory.Crew, ItemRarity.Uncommon, new Color(0.55f, 0.92f, 0.6f), 0f,
                    "Marujos do navio afundado que aceitam se juntar à sua tripulação. Vão no alojamento, não no porão."),
            };
            var shipyard = Item("Esquema_Estaleiro", "Estaleiro", ItemCategory.Schematic, ItemRarity.Rare, RareColor, 0f,
                "Construção da base: reforma e troca de navio por classes maiores.");
            var forge = Item("Esquema_Forja", "Forja", ItemCategory.Schematic, ItemRarity.Rare, RareColor, 0f,
                "Construção da base: transforma ferro em canhões e balas melhores.");
            var magazine = Item("Esquema_Paiol", "Paiol de Pólvora", ItemCategory.Schematic, ItemRarity.Rare, RareColor, 0f,
                "Construção da base: guarda mais pólvora com segurança.");
            var fortress = Item("Esquema_Fortaleza", "Fortaleza Costeira", ItemCategory.Schematic, ItemRarity.Epic, EpicColor, 0f,
                "Construção da base: canhões fixos que defendem o porto.");
            // Navio novo (M8.2): a planta da escuna; constrói-se no estaleiro do porto (PrototypeSceneBuilder.Ships.cs).
            var schooner = Item(SchoonerSchematic, "Escuna", ItemCategory.Schematic, ItemRarity.Rare, RareColor, 0f,
                "Planta de navio: a escuna de dois mastros, maior que a chalupa. Construída no estaleiro do porto.");
            // Galeão (3ª classe): só no baú do Tormenta e no mercado da patente 4 (não entra nos outros baús).
            Item(GalleonSchematic, "Galeão", ItemCategory.Schematic, ItemRarity.Epic, EpicColor, 0f,
                "Planta de navio: o galeão de três mastros, o maior navio do mar. Construído no estaleiro do porto (pede o Estaleiro nível 2).");

            // Cores da paleta do Kenney: as primitivas que sobraram combinam com os modelos do kit.
            var m = new LootMaterials
            {
                deck = KenneyColors.WoodLight,
                sail = KenneyColors.Sail,
                crateWood = KenneyColors.WoodDark,
                iron = KenneyColors.Iron,
                gold = KenneyColors.Gold,
                skin = GetMaterial("M_Skin", new Color(0.82f, 0.6f, 0.45f), 0.2f),
                shirt = GetMaterial("M_Shirt", new Color(0.82f, 0.8f, 0.72f), 0.1f),
                shirtStriped = GetMaterial("M_ShirtRed", new Color(0.6f, 0.12f, 0.1f), 0.1f),
                beacon = GetTransparentMaterial("M_LootBeacon", new Color(1f, 0.85f, 0.3f, 0.28f)),
                wreckBeacon = GetTransparentMaterial("M_WreckBeacon", new Color(0.9f, 0.95f, 1f, 0.12f)),
            };
            var wreck = WreckPrefab(m, items);

            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_ChalupaInimiga.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 20, 60),
                    Drop(items.wood, 0.9f, 5, 15),
                    Drop(items.powder, 0.7f, 2, 6),
                    Drop(items.iron, 0.5f, 2, 5),
                    Drop(items.crew, 0.6f, 1, 3),
                };
                t.chestChance = 0.2f;
                t.schematics = new[]
                {
                    Weighted(shipyard, 3f), Weighted(forge, 3f), Weighted(magazine, 3f), Weighted(fortress, 1f),
                };
                t.chestFallback = new ItemStack(items.gold, 150);
            });
            table.wreckPrefab = wreck;
            EditorUtility.SetDirty(table);
            var schematics = new[] { Weighted(shipyard, 3f), Weighted(forge, 3f), Weighted(magazine, 3f), Weighted(fortress, 1f), Weighted(schooner, 3f) };
            // Tabelas que já existiam (preservadas) também passam a sortear a escuna.
            AddSchematicToLootTables(schooner, 3f);
            return new LootAssets { enemyTable = table, items = items, schematics = schematics };
        }

        static void AddLoot(LootAssets loot, ShipController player, ShipController enemy)
        {
            player.gameObject.AddComponent<Inventory>().Configure(SloopHoldCapacity);
            var salvager = player.gameObject.AddComponent<WreckSalvager>();
            player.gameObject.AddComponent<PlayerInteractInput>();
            enemy.gameObject.AddComponent<LootDropper>().Configure(loot.enemyTable);
            new GameObject("LootHUD").AddComponent<LootHUD>().Bind(salvager);
        }

        // ---------- Dados ----------

        static readonly Color RareColor = new(0.45f, 0.72f, 1f);
        static readonly Color EpicColor = new(0.82f, 0.52f, 1f);

        static ItemDefinition Item(string file, string name, ItemCategory category, ItemRarity rarity, Color color,
            float space, string description) =>
            LoadOrCreateAsset<ItemDefinition>($"{ItemsFolder}/{file}.asset", i =>
            {
                i.displayName = name;
                i.category = category;
                i.rarity = rarity;
                i.color = color;
                i.spacePerUnit = space;
                i.description = description;
            });

        static LootTable.Drop Drop(ItemDefinition item, float chance, int min, int max) =>
            new() { item = item, chance = chance, minAmount = min, maxAmount = max };

        static LootTable.WeightedItem Weighted(ItemDefinition item, float weight) => new() { item = item, weight = weight };

        // ---------- Prefab dos destroços (primitivas; modelo de verdade no M6) ----------

        sealed class LootMaterials
        {
            public Material deck, sail, crateWood, iron, gold, skin, shirt, shirtStriped, beacon, wreckBeacon;
        }

        /// <summary>
        /// Raiz com física (Rigidbody + caixa + flutuação em 4 pontos) e visual sem colisores: tábuas, mastro
        /// quebrado e vela rasgada sempre; caixote, tábuas, barril, ferro, marujos e baú só enquanto o item está lá
        /// (caixotes, barril, balas e baú são modelos do Kenney).
        /// Sobrescreve o prefab mantendo o GUID (a LootTable não perde a referência).
        /// </summary>
        static ShipWreck WreckPrefab(LootMaterials m, LootItems items)
        {
            var root = new GameObject("Loot_Destrocos");
            var body = root.AddComponent<Rigidbody>();
            body.mass = 600f;
            body.angularDamping = 1.2f;
            body.centerOfMass = new Vector3(0f, -0.2f, 0f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(6f, 0.5f, 5f);
            root.AddComponent<Buoyancy>().Configure(new[]
            {
                new Vector3(-2.4f, -0.2f, -2f), new Vector3(2.4f, -0.2f, -2f),
                new Vector3(-2.4f, -0.2f, 2f), new Vector3(2.4f, -0.2f, 2f),
            }, depth: 0.5f, horizontalWaterDrag: 0.8f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            // Restos do casco: sempre presentes.
            var planks = new[]
            {
                (new Vector3(-0.8f, 0f, -1.2f), new Vector3(3.2f, 0.16f, 0.4f), 12f),
                (new Vector3(1.1f, 0f, -0.3f), new Vector3(2.6f, 0.16f, 0.35f), -25f),
                (new Vector3(-1.5f, 0f, 0.6f), new Vector3(2.2f, 0.16f, 0.4f), 70f),
                (new Vector3(0.4f, 0f, 1.9f), new Vector3(3f, 0.16f, 0.38f), 5f),
                (new Vector3(-0.2f, 0.02f, 0.2f), new Vector3(2.4f, 0.16f, 0.45f), -60f),
            };
            for (int i = 0; i < planks.Length; i++)
                Part(PrimitiveType.Cube, $"Tabua_{i}", visual, planks[i].Item1, planks[i].Item2, Quaternion.Euler(0f, planks[i].Item3, 0f),
                    i % 2 == 0 ? m.deck : m.crateWood);
            Part(PrimitiveType.Cylinder, "Mastro_Quebrado", visual, new Vector3(0.3f, 0.1f, -2f), new Vector3(0.26f, 2.4f, 0.26f),
                Quaternion.Euler(0f, 18f, 90f), m.crateWood);
            Part(PrimitiveType.Cube, "Vela_Rasgada", visual, new Vector3(-1.4f, 0.04f, 1.6f), new Vector3(2.4f, 0.03f, 1.7f),
                Quaternion.Euler(0f, 20f, 0f), m.sail);

            var visuals = new List<ShipWreck.ContentVisual>
            {
                Content(items.gold, GoldCrate(Group(visual, "Ouro", new Vector3(1.7f, 0.25f, 1f), 20f), m)),
                Content(items.wood, PlankBundle(Group(visual, "Madeira", new Vector3(-1.9f, 0.12f, -0.4f), -15f), m)),
                Content(items.powder, PowderBarrel(Group(visual, "Polvora", new Vector3(0.1f, 0.3f, 1.3f), 35f), m)),
                Content(items.iron, IronCrate(Group(visual, "Ferro", new Vector3(2.1f, 0.3f, -1f), -10f), m)),
                Content(items.crew, Sailors(Group(visual, "Sobreviventes", new Vector3(-0.5f, 0.08f, -0.1f), 0f), m)),
            };

            var chest = Chest(Group(visual, "Bau", new Vector3(0.9f, 0.3f, -0.6f), 25f), m);
            var wreckBeam = Beam(root.transform, "Facho", 0.3f, m.wreckBeacon);
            var chestBeam = Beam(root.transform, "Facho_Bau", 0.5f, m.beacon);

            var wreck = root.AddComponent<ShipWreck>();
            wreck.Configure(visuals.ToArray(), new[] { chest, chestBeam.gameObject }, new[] { wreckBeam, chestBeam });

            var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{LootPrefabsFolder}/Loot_Destrocos.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<ShipWreck>();
        }

        static ShipWreck.ContentVisual Content(ItemDefinition item, GameObject visual) => new() { item = item, visual = visual };

        static Transform Group(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        /// <summary>Facho de luz vertical para achar os destroços de longe (o ShipWreck o mantém em pé).</summary>
        static Transform Beam(Transform parent, string name, float width, Material mat)
        {
            var beam = Part(PrimitiveType.Cylinder, name, parent, new Vector3(0f, 10f, 0f), new Vector3(width, 10f, width), Quaternion.identity, mat);
            beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return beam.transform;
        }

        // Os modelos do kit têm o pivô embaixo; os grupos ficam no centro da peça (0,3 acima das tábuas).
        static GameObject GoldCrate(Transform g, LootMaterials m)
        {
            Kenney("crate", g, new Vector3(0f, -0.3f, 0f), 0f, 0.8f, "Caixote");
            Part(PrimitiveType.Cube, "Moedas", g, new Vector3(0f, 0.05f, 0f), new Vector3(0.74f, 0.12f, 0.9f), Quaternion.identity, m.gold);
            return g.gameObject;
        }

        static GameObject PlankBundle(Transform g, LootMaterials m)
        {
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cube, $"Tabua_{i}", g, new Vector3(0f, i * 0.16f, (i - 1) * 0.3f), new Vector3(2f, 0.16f, 0.32f),
                    Quaternion.Euler(0f, (i - 1) * 4f, 0f), m.crateWood);
            Part(PrimitiveType.Cube, "Amarra_A", g, new Vector3(-0.6f, 0.2f, 0f), new Vector3(0.1f, 0.5f, 1.1f), Quaternion.identity, m.iron);
            Part(PrimitiveType.Cube, "Amarra_B", g, new Vector3(0.6f, 0.2f, 0f), new Vector3(0.1f, 0.5f, 1.1f), Quaternion.identity, m.iron);
            return g.gameObject;
        }

        static GameObject PowderBarrel(Transform g, LootMaterials m)
        {
            // Deitado: girado 90° em Z, o centro do barril (0,61 acima do pivô) vai para -X; o deslocamento o traz de volta.
            const float scale = 0.6f;
            Kenney("barrel", g, new Vector3(0.61f * scale, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), scale, "Barril");
            return g.gameObject;
        }

        static GameObject IronCrate(Transform g, LootMaterials m)
        {
            Kenney("crate", g, new Vector3(0f, -0.3f, 0f), 0f, 0.85f, "Caixote");
            for (int i = 0; i < 3; i++)
                Kenney("cannon-ball", g, new Vector3((i - 1) * 0.26f, 0.1f, (i % 2) * 0.2f - 0.1f), 0f, 0.42f, $"Bala_{i}");
            return g.gameObject;
        }

        static GameObject Sailors(Transform g, LootMaterials m)
        {
            Sailor(g, "Marujo_A", new Vector3(-0.45f, 0f, 0.1f), m.shirt, m.skin);
            Sailor(g, "Marujo_B", new Vector3(0.5f, 0f, -0.15f), m.shirtStriped, m.skin);
            return g.gameObject;
        }

        static void Sailor(Transform parent, string name, Vector3 pos, Material shirt, Material skin)
        {
            Part(PrimitiveType.Capsule, name, parent, pos + new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.38f, 0.42f), Quaternion.identity, shirt);
            Part(PrimitiveType.Sphere, name + "_Cabeca", parent, pos + new Vector3(0f, 0.97f, 0f), Vector3.one * 0.3f, Quaternion.identity, skin);
        }

        static GameObject Chest(Transform g, LootMaterials m)
        {
            Kenney("chest", g, new Vector3(0f, -0.3f, 0f), 0f, 0.75f, "Bau");
            return g.gameObject;
        }
    }
}
