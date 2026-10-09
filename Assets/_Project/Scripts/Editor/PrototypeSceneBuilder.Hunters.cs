using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Caçadores de recompensas (inimigo novo de navio, passo 3 de 09/10; cena 07): o <see cref="BountyHunters"/> com um molde de caçador
    /// (ship-medium, velas brancas, o único modelo de navio do kit que ainda não navegava; casco do tamanho da escuna) desligado fora do
    /// mapa. Força por patente do capitão (Corsário, Capitão Temido, Lenda): casco, canhões e fama; o último passa do galeão Tormenta
    /// (revisão do galeão do jogador: o próximo inimigo precisa estar acima dele). Stats, canhões e saque em Data (criados uma vez).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string HunterModel = "ship-medium";

        static readonly string[] HunterCaptains =
        {
            "Capitão Duarte Ferraz", "Capitã Inês do Rochedo", "Capitão Baltasar Sombra", "Capitão Simão Garra", "Capitã Leonor Malva",
        };

        static void AddBountyHunters(ShipController player, CombatAssets combat, LootAssets loot)
        {
            var brigStats = AssetDatabase.LoadAssetAtPath<ShipStats>(DataFolder + "/ShipStats_Brigue.asset");
            var brigCannons = AssetDatabase.LoadAssetAtPath<CannonStats>(DataFolder + "/CannonStats_Brigue.asset");
            var stats = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Cacador.asset", s =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(brigStats ? brigStats : combat.enemyShip), s);
                s.maxSpeed = 11f;       // a chalupa sem upgrade não foge; com o velame melhorado (~14) dá para fugir
                s.acceleration = 1.5f;
                s.deceleration = 1.9f;
                s.turnRate = 25f;
            });
            // Todos com 6 canhões por bordo (os canhões são montados no casco): muda o dano, a recarga e o espalhamento.
            CannonStats Cannons(string file, float damage, float reload, float spread) =>
                LoadOrCreateAsset<CannonStats>($"{DataFolder}/{file}.asset", c =>
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(brigCannons ? brigCannons : combat.enemyCannons), c);
                    c.cannonsPerSide = 6;
                    c.damagePerBall = damage;
                    c.reloadTime = reload;
                    c.spreadDegrees = spread;
                });
            var tiers = new[]
            {
                // Corsário chega cedo (60 de fama), com a chalupa: casco 280 e pontaria de veterano (a do brigue). Com 360 e pontaria de
                // elite a chalupa nível 1 perdia por pouco mesmo com mira perfeita (revisão do chat de otimização, 09/10).
                new BountyHunters.Tier { minRank = 2, shipLabel = "Caçador de recompensas", hull = 280f, fame = 20,
                    cannons = Cannons("CannonStats_Cacador_1", 13f, 4.5f, 1f), leadError = 0.2f, rangeError = 0.04f, reaction = new Vector2(0.25f, 0.6f) },
                new BountyHunters.Tier { minRank = 3, shipLabel = "Caçador de recompensas veterano", hull = 560f, fame = 35,
                    cannons = Cannons("CannonStats_Cacador_2", 15f, 4.2f, 0.9f) },
                // Lenda dos Sete Mares: acima do Tormenta (700, 6 × 16, recarga 4) para o galeão do jogador ter com quem brigar.
                new BountyHunters.Tier { minRank = 4, shipLabel = "Caçador de lendas", hull = 820f, fame = 60,
                    cannons = Cannons("CannonStats_Cacador_3", 18f, 3.8f, 0.8f) },
            };

            var mold = CreateEnemy("Cacador_Molde", new Vector3(2500f, 0.3f, 2500f), Quaternion.identity, stats, tiers[2].cannons,
                PirateMediumHull(HunterModel), tiers[0].hull, combat, player.transform);
            var ai = mold.GetComponent<EnemyShipAI>();
            // Caçador profissional: pontaria de elite, reage rápido, não foge.
            ai.ConfigureGunnery(rangeErrorFraction: 0.04f, reaction: new Vector2(0.15f, 0.4f), fireArc: 18f, fleeAt: 0f, leadErrorFraction: 0.15f);

            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Cacador.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 180, 320), Drop(items.wood, 0.9f, 10, 25), Drop(items.powder, 0.9f, 8, 16),
                    Drop(items.iron, 0.8f, 6, 12), Drop(items.crew, 0.7f, 2, 4),
                };
                t.chestChance = 0.35f;
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 300);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            EditorUtility.SetDirty(table);
            mold.gameObject.AddComponent<LootDropper>().Configure(table);
            mold.gameObject.AddComponent<ShipBoss>().Configure(tiers[0].shipLabel, "", null, 0, 0f);
            mold.gameObject.SetActive(false);

            var director = new GameObject("CacadoresDeRecompensa");
            director.AddComponent<BountyHunters>().Configure(mold.gameObject, player.transform, tiers, HunterCaptains);
        }
    }
}
