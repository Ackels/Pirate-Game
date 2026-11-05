using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// A Esquadra Real (G, passo 3, 03/11; cena 07): o <see cref="RoyalNavy"/> com três moldes de navio de guerra da Coroa, desligados
    /// fora do mapa, de bandeira azul como os mercantes, mas armados: corveta (ship-medium, 4 canhões por bordo; com 5 × 12 tirava 108 da chalupa parada em ~8 s: forte demais para 1 estrela), fragata e nau de guerra
    /// (ship-large, 6 e 8). Levas pelas estrelas de procurado (04/11): 1 = nenhum; 2 = corveta; 3 = fragata; 4 = fragata + corveta;
    /// 5 = a nau com uma fragata. Stats, canhões e saque em Data (criados uma vez).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static void AddRoyalNavy(ShipController player, CombatAssets combat, LootAssets loot)
        {
            var hunterStats = AssetDatabase.LoadAssetAtPath<ShipStats>(DataFolder + "/ShipStats_Cacador.asset");
            var brigStats = AssetDatabase.LoadAssetAtPath<ShipStats>(DataFolder + "/ShipStats_Brigue.asset");
            var brigCannons = AssetDatabase.LoadAssetAtPath<CannonStats>(DataFolder + "/CannonStats_Brigue.asset");
            ShipStats Stats(string file, ShipStats from, float speed, float turn) =>
                LoadOrCreateAsset<ShipStats>($"{DataFolder}/{file}.asset", s =>
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from ? from : combat.enemyShip), s);
                    s.maxSpeed = speed;
                    s.turnRate = turn;
                });
            CannonStats Cannons(string file, int perSide, float damage, float reload, float spread) =>
                LoadOrCreateAsset<CannonStats>($"{DataFolder}/{file}.asset", c =>
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(brigCannons ? brigCannons : combat.enemyCannons), c);
                    c.cannonsPerSide = perSide;
                    c.damagePerBall = damage;
                    c.reloadTime = reload;
                    c.spreadDegrees = spread;
                });
            // A corveta alcança a chalupa sem melhorias (11) só com o vento a favor; a nau é lenta (o galeão do jogador foge dela).
            var corvetteStats = Stats("ShipStats_Coroa_Corveta", hunterStats, 10.5f, 24f);
            var frigateStats = Stats("ShipStats_Coroa_Fragata", brigStats, 9.8f, 20f);
            var shipOfLineStats = Stats("ShipStats_Coroa_Nau", brigStats, 8.8f, 16f);
            var corvetteCannons = Cannons("CannonStats_Coroa_Corveta", 4, 10f, 4.6f, 1.2f);
            var frigateCannons = Cannons("CannonStats_Coroa_Fragata", 6, 14f, 4.3f, 1f);
            var shipOfLineCannons = Cannons("CannonStats_Coroa_Nau", 8, 16f, 4f, 0.9f);

            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_EsquadraReal.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 80, 160), Drop(items.powder, 0.9f, 8, 16), Drop(items.iron, 0.8f, 6, 12),
                    Drop(items.wood, 0.7f, 8, 18),
                };
                t.chestChance = 0.3f;
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 250);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            AddLoot(table, CanvasItem(), 0.6f, 3, 6);
            AddLoot(table, RopeItem(), 0.6f, 3, 6);
            AddLoot(table, TarItem(), 0.4f, 2, 4);
            AddLoot(table, RumItem(), 0.3f, 1, 3);
            AddLoot(table, SpiceItem(), 0.25f, 1, 2);
            EditorUtility.SetDirty(table);
            // 04/11 (revisão do chat de otimização): a mesma tabela pagava ~410 numa corveta de 180 de casco e numa nau de 850. A fragata
            // fica com a tabela de cima; a corveta paga cerca de metade e a nau mais de 2× (perto do galeão chefe).
            var (corvetteLoot, lineLoot) = CrownLootTiers(table, items.gold);

            var flag = GetMaterial("M_Bandeira_Mercante", new Color(0.12f, 0.27f, 0.62f), 0.2f);
            var group = new GameObject("EsquadraReal").transform;
            GameObject Mold(string name, int slot, ShipStats stats, CannonStats cannons, ShipHull hull, float hullHealth, LootTable drops = null)
            {
                var mold = CreateEnemy(name, new Vector3(2600f + slot * 60f, 0.3f, 2600f), Quaternion.identity, stats, cannons, hull,
                    hullHealth, combat, player.transform);
                var ai = mold.GetComponent<EnemyShipAI>();
                ai.SetCrown(true);
                ai.SetTargetId("esquadra_real");
                // A Marinha não foge.
                ai.ConfigureGunnery(rangeErrorFraction: 0.05f, reaction: new Vector2(0.3f, 0.7f), fireArc: 16f, fleeAt: 0f, leadErrorFraction: 0.25f);
                mold.gameObject.AddComponent<LootDropper>().Configure(drops ? drops : table);
                foreach (var r in mold.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name.StartsWith("flag")) r.sharedMaterial = flag;
                mold.transform.SetParent(group, true);
                mold.gameObject.SetActive(false);
                return mold.gameObject;
            }

            var types = new[]
            {
                new RoyalNavy.ShipType { label = "Corveta da Coroa", hull = 180f, fame = 8, cannons = corvetteCannons,
                    leadError = 0.35f, rangeError = 0.06f, reaction = new Vector2(0.35f, 0.8f),
                    template = Mold("Corveta_Coroa_Molde", 0, corvetteStats, corvetteCannons, PirateMediumHull(HunterModel), 180f, corvetteLoot) },
                new RoyalNavy.ShipType { label = "Fragata da Coroa", hull = 380f, fame = 15, cannons = frigateCannons,
                    leadError = 0.22f, rangeError = 0.05f, reaction = new Vector2(0.3f, 0.65f),
                    template = Mold("Fragata_Coroa_Molde", 1, frigateStats, frigateCannons, BrigHull(BrigModel), 380f) },
                new RoyalNavy.ShipType { label = "Nau de guerra da Coroa", hull = 850f, fame = 30, cannons = shipOfLineCannons,
                    leadError = 0.15f, rangeError = 0.04f, reaction = new Vector2(0.2f, 0.5f),
                    template = Mold("Nau_Coroa_Molde", 2, shipOfLineStats, shipOfLineCannons, BrigHull(BrigModel), 850f, lineLoot) },
            };
            // 04/11 (pedido do Jhonata: "pode ficar chato sempre ser perseguido"): 1 estrela não manda navio, e a escada desceu uma.
            var waves = new[]
            {
                new RoyalNavy.Wave { ships = new int[0] },
                new RoyalNavy.Wave { ships = new[] { 0 } },
                new RoyalNavy.Wave { ships = new[] { 1 } },
                new RoyalNavy.Wave { ships = new[] { 1, 0 } },
                new RoyalNavy.Wave { ships = new[] { 2, 1 } },
            };
            group.gameObject.AddComponent<RoyalNavy>().Configure(player.transform, types, waves);

            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<RoyalNavyHUD>()) hud.gameObject.AddComponent<RoyalNavyHUD>();
        }

        /// <summary>Corveta (ouro 30–70, carga × 0,7, baú 20%/150) e nau (ouro 160–320, carga × 1,8, baú 60%/400), a partir da tabela da fragata.</summary>
        static (LootTable corvette, LootTable shipOfLine) CrownLootTiers(LootTable fleet, ItemDefinition gold)
        {
            LootTable Tier(string file, int goldMin, int goldMax, float cargo, float chest, int fallback) =>
                LoadOrCreateAsset<LootTable>($"{DataFolder}/{file}.asset", t =>
                {
                    var list = new List<LootTable.Drop>();
                    foreach (var d in fleet.drops)
                    {
                        bool isGold = d.item == gold;
                        list.Add(new LootTable.Drop
                        {
                            item = d.item, chance = d.chance,
                            minAmount = isGold ? goldMin : Mathf.Max(1, Mathf.RoundToInt(d.minAmount * cargo)),
                            maxAmount = isGold ? goldMax : Mathf.Max(1, Mathf.RoundToInt(d.maxAmount * cargo)),
                        });
                    }
                    t.drops = list.ToArray();
                    t.chestChance = chest;
                    t.schematics = fleet.schematics;
                    t.chestFallback = new ItemStack(gold, fallback);
                    t.wreckPrefab = fleet.wreckPrefab;
                });
            return (Tier("LootTable_Coroa_Corveta", 30, 70, 0.7f, 0.2f, 150), Tier("LootTable_Coroa_Nau", 160, 320, 1.8f, 0.6f, 400));
        }

        /// <summary>Na cena aberta (sem regerar): cria as duas tabelas e põe nos moldes da corveta e da nau.</summary>
        public static string ApplyCrownLootTiers()
        {
            var fleet = AssetDatabase.LoadAssetAtPath<LootTable>(DataFolder + "/LootTable_EsquadraReal.asset");
            var gold = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Item_Ouro.asset");
            if (!fleet || !gold) return "sem tabela ou ouro";
            var (corvette, line) = CrownLootTiers(fleet, gold);
            AssetDatabase.SaveAssets();
            string r = "";
            foreach (var d in Object.FindObjectsByType<LootDropper>(FindObjectsInactive.Include))
            {
                LootTable pick = d.name == "Corveta_Coroa_Molde" ? corvette : d.name == "Nau_Coroa_Molde" ? line : null;
                if (!pick) continue;
                d.Configure(pick);
                EditorUtility.SetDirty(d);
                r += d.name + "->" + pick.name + "; ";
            }
            return r;
        }
    }
}
