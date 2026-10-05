using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Chefe no mar (M7.1, cena 07): o galeão pirata "Tormenta" do Capitão Lázaro Negro, de velas pretas (ship-pirate-large,
    /// mesmo casco do brigue), patrulhando o mar do sudeste, além da Ilha da Caveira. Mais forte e mais rápido que o brigue,
    /// nunca foge e, com o casco pela metade, chama 2 corsários (chalupas pirata, ship-pirate-medium) que chegam por trás
    /// dele. Barra de vida no topo da tela durante a luta (BossBarHUD). Saque grande com esquema garantido; volta depois de
    /// 300 s ou ao aceitar a missão. Stats e saque em Data (ShipStats_Galeao, CannonStats_Galeao, LootTable_Galeao):
    /// criados uma vez a partir dos do brigue/chalupa e preservados depois (ajuste de game design).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string BossMissionId = "galeao_tormenta";
        const string BossModel = "ship-pirate-large", BossEscortModel = "ship-pirate-medium";
        const float BossHullHealth = 700f;
        /// <summary>Mar do sudeste: ~150 m ao sul da Caveira e ~200 m a leste do porto, longe das rotas das chalupas.</summary>
        static readonly Vector3[] BossSpawnPoints = { new(330f, 0.3f, -380f), new(420f, 0.3f, -330f), new(250f, 0.3f, -440f) };

        /// <summary>
        /// Chalupa pirata (ship-pirate-medium, 10,6 m): entre a chalupa e o brigue. Inércia de uma caixa ~3,5 × 1,6 × 11,5 m
        /// na massa dela, pela mesma conta dos outros cascos.
        /// </summary>
        static ShipHull PirateMediumHull(string model) => new()
        {
            model = model,
            mass = 7000f,
            colliderCenter = new Vector3(0f, 0.5f, 0f),
            colliderSize = new Vector3(4.2f, 2.2f, 10.2f),
            inertia = new Vector3(84180f, 83000f, 13130f),
            floatPoints = new[]
            {
                new Vector3(-1.4f, -0.5f, 4.6f), new Vector3(1.4f, -0.5f, 4.6f),
                new Vector3(-1.5f, -0.5f, 0f), new Vector3(1.5f, -0.5f, 0f),
                new Vector3(-1.4f, -0.5f, -4.6f), new Vector3(1.4f, -0.5f, -4.6f),
                new Vector3(0f, -0.5f, 5.8f), new Vector3(0f, -0.5f, -5.4f),
            },
            cannonZMin = -3.0f,
            cannonZMax = 2.3f,
        };

        static void AddBoss(ShipController player, CombatAssets combat, LootAssets loot)
        {
            var brigStats = AssetDatabase.LoadAssetAtPath<ShipStats>(DataFolder + "/ShipStats_Brigue.asset");
            var stats = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Galeao.asset", s =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(brigStats ? brigStats : combat.enemyShip), s);
                s.maxSpeed = 9.8f;      // brigue 9, chalupas 9,5, a sua 11: ainda dá para fugir, por pouco
                s.acceleration = 1.3f;
                s.deceleration = 1.7f;
                s.turnRate = 24f;       // brigue 22, chalupas 28
            });
            var brigCannons = AssetDatabase.LoadAssetAtPath<CannonStats>(DataFolder + "/CannonStats_Brigue.asset");
            var cannons = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_Galeao.asset", c =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(brigCannons ? brigCannons : combat.enemyCannons), c);
                c.cannonsPerSide = 6;
                c.reloadTime = 4f;      // brigue 4,5
                c.spreadDegrees = 0.8f; // brigue 0,9
                c.damagePerBall = 16f;  // brigue 14: bordada de 96 se todas acertarem
            });

            // Molde da escolta: chalupa pirata comum (stats e saque das chalupas inimigas), desligada.
            var holder = new GameObject("ChefeEscolta");
            var escort = CreateEnemy("Corsario", new Vector3(0f, 0.3f, -2000f), Quaternion.identity, combat.enemyShip, combat.enemyCannons,
                PirateMediumHull(BossEscortModel), EnemyHull, combat, player.transform);
            escort.transform.SetParent(holder.transform, true);
            escort.GetComponent<EnemyShipAI>().SetFame(8);
            escort.gameObject.AddComponent<LootDropper>().Configure(loot.enemyTable);
            // Bandeiras vermelhas: a escuna do jogador (M8.2) é o mesmo modelo; assim dá para saber quem é quem na luta.
            var redFlag = GetMaterial("M_Bandeira_Corsario", new Color(0.75f, 0.09f, 0.07f), 0.2f);
            foreach (var r in escort.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("flag")) r.sharedMaterial = redFlag;
            escort.gameObject.SetActive(false);

            var boss = CreateEnemy("Galeao_Tormenta", BossSpawnPoints[0], Quaternion.Euler(0f, 250f, 0f), stats, cannons,
                BrigHull(BossModel), BossHullHealth, combat, player.transform);
            var ai = boss.GetComponent<EnemyShipAI>();
            ai.SetTargetId(BossMissionId);
            ai.SetFame(80);
            // Tripulação de assassinos: erra pouco, dispara rápido num través aberto e nunca foge.
            ai.ConfigureGunnery(rangeErrorFraction: 0.03f, reaction: new Vector2(0.1f, 0.35f), fireArc: 20f, fleeAt: 0f, leadErrorFraction: 0.12f);
            boss.gameObject.AddComponent<ShipBoss>().Configure("Galeão Tormenta", "Capitão Lázaro Negro", escort.gameObject, 2, 0.5f);

            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Galeao.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 200, 350), Drop(items.wood, 1f, 20, 40), Drop(items.powder, 1f, 10, 20),
                    Drop(items.iron, 1f, 8, 15), Drop(items.crew, 0.9f, 3, 6),
                };
                t.chestChance = 1f;     // baú garantido: esquema que você ainda não tem (ou ouro, se já tiver todos)
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 400);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            EditorUtility.SetDirty(table);
            boss.gameObject.AddComponent<LootDropper>().Configure(table);
            boss.gameObject.AddComponent<WorldMarker>().Configure(BossMissionId, "Galeão Tormenta", 13f);

            // Por último: o molde do respawner é uma cópia do galeão já completo (aponta para o mesmo molde da escolta).
            AddRespawner("GaleaoRespawner", boss.gameObject, player, BossSpawnPoints, 300f, BossMissionId);

            var hud = GameObject.Find("HUD");
            if (hud) hud.AddComponent<BossBarHUD>();
        }

        static MissionDefinition BossMission(LootItems i) => Mission("Missao_GaleaoTormenta", m =>
        {
            m.missionId = BossMissionId;
            m.title = "O galeão Tormenta";
            m.giver = "Velho Tobias";
            m.description = "O Capitão Lázaro Negro voltou: o galeão Tormenta, de velas pretas, caça no mar do sudeste, além da " +
                            "Ilha da Caveira. Seis canhões por bordo, tripulação que nunca se rende, e dizem que ele chama corsários " +
                            "quando aperta. Afunde o Tormenta e o tesouro do Lázaro é seu.";
            m.type = MissionType.SinkShips;
            m.count = 1;
            m.targetId = BossMissionId;
            m.targetName = "galeão Tormenta";
            m.rewards = new[] { new ItemStack(i.gold, 450), new ItemStack(i.powder, 12), new ItemStack(i.iron, 10) };
        });
    }
}
