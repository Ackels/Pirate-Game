using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Mapas do tesouro em pedaços (C1, 16/10; pacote C, exploração; cena 07): o item "Pedaço de mapa do tesouro" entra nas tabelas de
    /// saque (baús de terra, navios grandes, chefes); com 3 o mapa se monta (<see cref="TreasureMaps"/>) e um X aparece num dos 7 lugares
    /// das ilhas (achados pela cena: chão firme, NavMesh ligado à praia, longe dos acampamentos e dos outros tesouros). Os baús enterrados
    /// usam a tabela própria (ouro, pólvora, ferro, chance de esquema). Assets e listas que existem são preservados.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string MapPieceFile = "Item_PedacoMapa";

        static ItemDefinition MapPieceItem() =>
            Item(MapPieceFile, "Pedaço de mapa do tesouro", ItemCategory.Resource, ItemRarity.Rare, new Color(0.86f, 0.74f, 0.5f), 0.1f,
                "Um canto rasgado de mapa, com traços de costa e um pedaço de X. Junte 3 e o mapa se monta sozinho: o X aparece numa ilha.");

        /// <summary>Lugares do X: ilha (nome do objeto), posição local a partir do centro dela e o nome para o jogador.</summary>
        static readonly (string island, Vector2 local, string place)[] MapTreasureSpots =
        {
            ("Ilha_Oeste", new Vector2(-32.4f, -0.1f), "Ilha Oeste, lado oeste"),
            ("Ilha_Oeste", new Vector2(-8.4f, 29.9f), "Ilha Oeste, ponta norte"),
            ("Ilha_Norte", new Vector2(-41.5f, 16f), "Ilha Norte, lado oeste"),
            ("Ilha_Norte", new Vector2(18.5f, 34f), "Ilha Norte, costa norte"),
            ("IlhaGrande", new Vector2(-101.9f, -56.6f), "Ilha da Caveira, morro sudoeste"),
            ("IlhaGrande", new Vector2(108.1f, -32.6f), "Ilha da Caveira, ponta leste"),
            ("IlhaGrande", new Vector2(-113.9f, 27.4f), "Ilha da Caveira, lado oeste"),
        };

        /// <summary>Chance de um pedaço por tabela de saque (um pedaço por vez).</summary>
        static readonly (string table, float chance)[] MapPieceDrops =
        {
            ("LootTable_BauAcampamento", 0.35f), ("LootTable_Contrabando", 0.4f), ("LootTable_PaiolForte", 0.35f),
            ("LootTable_TesouroEnterrado", 0.5f), ("LootTable_TesouroContrabandista", 0.5f),
            ("LootTable_Brigue", 0.5f), ("LootTable_Galeao", 1f), ("LootTable_Cacador", 0.3f), ("LootTable_ChalupaInimiga", 0.1f),
            ("LootTable_Lula", 1f), ("LootTable_Megalodonte", 0.6f), ("LootTable_Barril", 0.02f), ("LootTable_Caixote", 0.02f),
        };

        static void AddTreasureMaps(LandMaterials m, LandLootAssets landLoot, LootAssets loot, PlayerModeSwitcher switcher, GameObject hud)
        {
            var piece = MapPieceItem();
            foreach (var (file, chance) in MapPieceDrops)
                AddLoot(AssetDatabase.LoadAssetAtPath<LootTable>($"{DataFolder}/{file}.asset"), piece, chance, 1, 1);

            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_MapaDoTesouro.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 250, 450), Drop(items.powder, 0.7f, 6, 12), Drop(items.iron, 0.7f, 5, 10), Drop(items.wood, 0.5f, 10, 20),
                    Drop(BombItem(), 0.4f, 1, 3), Drop(CartridgeItem(), 0.5f, 10, 20),
                };
                t.chestChance = 0.35f;
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 300);
            });

            var spots = new System.Collections.Generic.List<TreasureMaps.Spot>();
            for (int k = 0; k < MapTreasureSpots.Length; k++)
            {
                var (islandName, local, place) = MapTreasureSpots[k];
                var island = GameObject.Find(islandName);
                if (!island)
                {
                    Debug.LogWarning($"[Pirates] Mapa do tesouro: ilha {islandName} não achada.");
                    continue;
                }
                string id = $"mapa_tesouro_{k + 1}";
                BuriedX(island.transform, local, id, $"Mapa do tesouro ({place})", table, landLoot, m);
                var treasure = GameObject.Find("Tesouro_" + id)?.GetComponent<BuriedTreasure>();
                if (treasure) spots.Add(new TreasureMaps.Spot { treasure = treasure, place = place });
            }

            var maps = new GameObject("MapasDoTesouro").AddComponent<TreasureMaps>();
            maps.Configure(piece, spots.ToArray());
            hud.AddComponent<TreasureMapHUD>().Bind(maps, switcher);
        }
    }
}
