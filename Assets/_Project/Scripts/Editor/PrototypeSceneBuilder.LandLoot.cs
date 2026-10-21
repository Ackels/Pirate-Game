using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Saque em terra do gerador (Protótipo 06, M4.2): mochila do personagem, baú do tesouro no acampamento (trancado
    /// até derrotar os piratas), caixotes e barris quebráveis no acampamento e pela ilha, tabelas de saque (Data) e o
    /// prefab do saco que cai deles (Prefabs/Loot, regerado a cada build). Tabelas existentes são preservadas.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Espaço da mochila: metade do porão da chalupa. Um baú cheio (~22) não cabe inteiro: escolha.</summary>
        const float BackpackCapacity = 15f;

        // Caixotes e barris do acampamento (no espaço do acampamento).
        static readonly Vector3[] CampCrates =
        {
            new(-12.2f, 0f, 5.5f), new(-11.4f, 0f, 7f), new(12.6f, 0f, 3.6f), new(3.5f, 0f, -13f), new(-13f, 0f, -4.5f),
        };
        static readonly Vector3[] CampBarrels = { new(11.8f, 0f, 5.2f), new(-3.4f, 0f, -12.8f), new(-4.6f, 0f, -12.2f), new(13.4f, 0f, -4f) };
        // Espalhados pela ilha (local da Caveira, SkullCenter): praia norte, trilhas, postos, ruínas e a mata.
        static readonly (Vector2 pos, bool barrel)[] IslandBreakables =
        {
            (new(-12f, 90f), false), (new(-10.8f, 88.6f), false), (new(14f, 86f), true),
            (new(-4f, 62f), true), (new(8f, 48f), false),
            (new(85.5f, 18f), false), (new(86.5f, 16.5f), true), (new(78.5f, 10f), false), (new(77.5f, 11f), false),
            (new(-66f, -32f), false), (new(-78f, -43f), true), (new(-76f, -31f), false),
            (new(31.5f, -55f), false), (new(24.5f, -61f), true),
            (new(-40f, 40f), false), (new(45f, -28f), true), (new(-30f, -58f), false), (new(58f, 32f), false), (new(-88f, 18f), true),
        };
        // Em volta do naufrágio (relativo a ele).
        static readonly (Vector2 pos, bool barrel)[] WreckBreakables =
        {
            (new(-5f, 7f), false), (new(-5.8f, 5.6f), true), (new(-4f, -7f), false), (new(-5.2f, -8f), false), (new(-6.5f, 0f), true),
        };

        sealed class LandLootAssets
        {
            public LootTable chest, crate, barrel;
            public LootSack sack;
            public Material crateWood, barrelWood, iron, beacon;
        }

        static LandLootAssets LoadLandLootAssets(LootAssets loot)
        {
            var i = loot.items;
            var a = new LandLootAssets
            {
                chest = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_BauAcampamento.asset", t =>
                {
                    t.drops = new[] { Drop(i.gold, 1f, 60, 120), Drop(i.iron, 0.8f, 2, 4), Drop(i.powder, 0.8f, 2, 5), Drop(i.wood, 0.5f, 3, 6) };
                    t.chestChance = 0.5f;   // metade das vezes vem um esquema de construção
                    t.schematics = loot.schematics;
                    t.chestFallback = new ItemStack(i.gold, 100);
                }),
                crate = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Caixote.asset", t =>
                {
                    t.drops = new[] { Drop(i.wood, 0.9f, 2, 5), Drop(i.gold, 0.5f, 5, 20), Drop(i.iron, 0.25f, 1, 2) };
                    t.chestChance = 0f;
                }),
                barrel = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Barril.asset", t =>
                {
                    t.drops = new[] { Drop(i.powder, 0.9f, 1, 3), Drop(i.wood, 0.3f, 1, 2), Drop(i.gold, 0.25f, 5, 10) };
                    t.chestChance = 0f;
                }),
                // Lascas que voam ao quebrar e o que ainda é primitiva: cores da paleta do Kenney.
                crateWood = KenneyColors.WoodLight,
                barrelWood = KenneyColors.WoodDark,
                iron = KenneyColors.Iron,
                beacon = GetTransparentMaterial("M_LootBeacon", new Color(1f, 0.85f, 0.3f, 0.28f)),
            };
            a.sack = SackPrefab(KenneyColors.Cloth, KenneyColors.WoodDark);
            // Comida do capitão (05/10): ração nos caixotes, barris e no baú do acampamento.
            AddRationLoot(a.crate, 0.35f, 1, 2);
            AddRationLoot(a.barrel, 0.2f, 1, 1);
            AddRationLoot(a.chest, 0.6f, 2, 4);
            AddLoot(a.barrel, CartridgeItem(), 0.3f, 3, 6);
            AddLoot(a.chest, CartridgeItem(), 0.5f, 6, 12);
            AddShotLoot(a.barrel, a.chest);
            AddBombLoot(a.barrel, a.chest);
            return a;
        }

        /// <summary>Saco de saque (prefab): o BreakableLoot solta um destes com o que sorteou.</summary>
        static LootSack SackPrefab(Material cloth, Material rope)
        {
            var root = new GameObject("Loot_Saco");
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Part(PrimitiveType.Sphere, "Saco", visual, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.42f, 0.5f), Quaternion.identity, cloth);
            Part(PrimitiveType.Cylinder, "Boca", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 0.07f, 0.16f), Quaternion.identity, rope);
            root.AddComponent<LootSack>().Configure(visual);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{LootPrefabsFolder}/Loot_Saco.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<LootSack>();
        }

        /// <summary>Baú do tesouro nos fundos do acampamento, virado para o portão. A tranca (o acampamento) vem depois.</summary>
        static LandChest CreateTreasureChest(Transform camp, LandLootAssets a)
        {
            GroundAt(camp.TransformPoint(new Vector3(1.5f, 0f, -12.2f)), out Vector3 ground);
            return CreateChest(camp, "Bau_Tesouro", "Baú do tesouro", ground, 8f, a.chest, a, withPadlock: true);
        }

        /// <summary>
        /// Modelo do baú (19/10, pedido do Jhonata: os do Kenney não agradavam mais). <b>Pirate</b>: o do Quaternius Pirate Kit, de uma peça
        /// só; ao abrir troca pelo aberto e cheio de ouro (acampamentos, contrabandistas, tesouros enterrados). <b>Ruins</b>/<b>RuinsGold</b>:
        /// os das ruínas do Quaternius, com a tampa separada que gira (o de ouro tem moedas dentro): paiol do forte e a Ermida.
        /// </summary>
        enum ChestStyle { Pirate, Ruins, RuinsGold }

        /// <summary>Peça do Quaternius Pirate Kit com o atlas do kit (a raiz do FBX vem girada do Blender: compõe por baixo).</summary>
        static GameObject QuaterniusKitProp(Transform parent, string model, Vector3 local, float yaw, float scale, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{model}.fbx");
            if (!asset) throw new System.IO.FileNotFoundException($"Modelo do Quaternius não encontrado: {model} (copie do _Downloads).");
            var go = (GameObject)Object.Instantiate(asset, parent, false);
            go.name = name;
            go.transform.SetLocalPositionAndRotation(local, Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation);
            go.transform.localScale = asset.transform.localScale * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = QuaterniusMaterial;
                r.sharedMaterials = slots;
            }
            return go;
        }

        /// <summary>Caixa (no espaço de <paramref name="space"/>) das malhas de <paramref name="model"/>.</summary>
        static Bounds LocalBounds(Transform space, GameObject model)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>
        /// Baú com cadeado, facho e o LandChest, no modelo do <paramref name="style"/> (frente em +Z, ~1,2 m de largura). Nos das ruínas a
        /// tampa ganha um eixo na borda de trás para girar; o pirata troca de modelo ao abrir.
        /// </summary>
        static LandChest CreateChest(Transform parent, string objectName, string displayName, Vector3 ground, float yaw, LootTable table,
            LandLootAssets a, bool withPadlock, ChestStyle style = ChestStyle.Pirate)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            Transform hinge = null;
            GameObject closed, open = null;
            Bounds size;
            if (style == ChestStyle.Pirate)
            {
                closed = QuaterniusKitProp(t, "Prop_Chest_Closed", Vector3.zero, 0f, 0.75f, "Bau");
                open = QuaterniusKitProp(t, "Prop_Chest_Gold", Vector3.zero, 0f, 0.75f, "Bau_Aberto");
                open.SetActive(false);
                size = LocalBounds(t, closed);
            }
            else
            {
                closed = Model(Kit.Ruins, style == ChestStyle.RuinsGold ? "Chest_Gold" : "Chest", t, Vector3.zero, 0f, 0.95f, "Bau");
                size = LocalBounds(t, closed);
                // A tampa (a peça "_Top") ganha um eixo na borda de trás, embaixo dela.
                foreach (var r in closed.GetComponentsInChildren<Renderer>())
                {
                    if (!r.name.EndsWith("_Top")) continue;
                    var lb = LocalBounds(t, r.gameObject);
                    hinge = new GameObject("Tampa").transform;
                    hinge.SetParent(t, false);
                    hinge.localPosition = new Vector3(0f, lb.min.y, lb.min.z);
                    r.transform.SetParent(hinge, true);
                }
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = size.center;
            box.size = size.size;
            var padlock = withPadlock
                ? Part(PrimitiveType.Cube, "Cadeado", t, new Vector3(0f, size.min.y + size.size.y * 0.55f, size.max.z + 0.03f), new Vector3(0.16f, 0.2f, 0.07f),
                    Quaternion.identity, a.iron)
                : null;
            var beacon = Part(PrimitiveType.Cylinder, "Facho", t, new Vector3(0f, 3.5f, 0f), new Vector3(0.45f, 3.5f, 0.45f), Quaternion.identity, a.beacon);
            beacon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var chest = root.AddComponent<LandChest>();
            chest.Configure(displayName, table, hinge, padlock, beacon);
            if (open) chest.SetModels(closed, open);
            chest.SetRarityBeacons(RarityBeacons());
            return chest;
        }

        /// <summary>
        /// Caixotes e barris quebráveis: os do acampamento, os espalhados pela ilha e os do naufrágio (antes da mata e do
        /// NavMesh: a mata não nasce em cima deles e eles viram obstáculo).
        /// </summary>
        static void CreateBreakables(Transform camp, SkullIsland skull, LandLootAssets a)
        {
            var parent = new GameObject("Caixotes_e_Barris").transform;
            int n = 0;
            foreach (var local in CampCrates) Breakable(parent, $"Caixote_{n++}", camp.TransformPoint(local), n * 23f, false, a);
            foreach (var local in CampBarrels) Breakable(parent, $"Barril_{n++}", camp.TransformPoint(local), n * 23f, true, a);
            var loose = new List<(Vector2, bool)>(IslandBreakables);
            foreach (var (offset, barrel) in WreckBreakables) loose.Add((skull.wreck + offset, barrel));
            foreach (var (local, barrel) in loose)
            {
                Vector3 pos = SkullCenter + new Vector3(local.x, 0f, local.y);
                // Fora d'água (a praia desce para dentro do mar).
                if (!GroundAt(pos, out Vector3 ground) || ground.y < 0.3f) continue;
                Breakable(parent, $"{(barrel ? "Barril" : "Caixote")}_{n++}", pos, n * 37f, barrel, a);
                skull.clearings.Add(local);
            }
        }

        static void Breakable(Transform parent, string name, Vector3 position, float yaw, bool barrel, LandLootAssets a)
        {
            GroundAt(position, out Vector3 ground);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // Modelos do Kenney (pivô embaixo): barril 0,94 × 0,86 m, caixote 0,81 × 0,58 × 0,98 m (às vezes com garrafas).
            if (barrel)
            {
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.45f, 0f);
                col.height = 0.9f;
                col.radius = 0.45f;
                Kenney("barrel", visual, Vector3.zero, 0f, 0.7f, "Barril");
            }
            else
            {
                var col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.29f, 0f);
                col.size = new Vector3(0.81f, 0.58f, 0.98f);
                Kenney(Mathf.RoundToInt(yaw) % 3 == 0 ? "crate-bottles" : "crate", visual, Vector3.zero, 0f, 0.75f, "Caixote");
            }
            // Um golpe de cutelo (25) ou um tiro quebra.
            root.AddComponent<Health>().Configure(barrel ? 15f : 20f);
            root.AddComponent<BreakableLoot>().Configure(barrel ? a.barrel : a.crate, a.sack, visual.gameObject, barrel ? a.barrelWood : a.crateWood);
        }
    }
}
