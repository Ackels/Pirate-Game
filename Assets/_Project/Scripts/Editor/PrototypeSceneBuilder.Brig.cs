using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Brigue inimigo "Santa Clara" (cena 07): o navio grande de velas brancas do kit, sozinho, patrulhando as águas ao
    /// norte da Ilha Norte. Bem mais forte que a chalupa (casco 250, 6 canhões por bordo), mais lento para acelerar e
    /// virar, com saque maior. Afundado, volta depois de 150 s (ou na hora, ao aceitar a missão dele). Stats e saque em
    /// Data (ShipStats_Brigue, CannonStats_Brigue, LootTable_Brigue): criados uma vez a partir dos da chalupa inimiga e
    /// preservados depois (são ajuste de game design).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string BrigMissionId = "brigue_santa_clara";
        const string BrigModel = "ship-large";
        const float BrigHullHealth = 400f;
        /// <summary>
        /// Ao norte da Ilha Norte, ~320 m do centro dela e ~880 m do cais (16/10, pedido do Jhonata: mais longe; antes ~130 m da chalupa
        /// pirata, que o tutorial manda afundar), longe da rota das chalupas. Mapa espaçado (16/10): andou junto com a Ilha Norte
        /// (+30, +210); eram (60, 510), (210, 480), (−90, 540).
        /// </summary>
        static readonly Vector3[] BrigSpawnPoints = { new(90f, 0.3f, 720f), new(240f, 0.3f, 690f), new(-60f, 0.3f, 750f) };

        static void AddBrig(ShipController player, CombatAssets combat, LootAssets loot)
        {
            var stats = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Brigue.asset", s =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(combat.enemyShip), s);
                s.maxSpeed = 9f;        // chalupa inimiga 9,5; a sua 11: ainda dá para fugir dele
                s.acceleration = 1.2f;
                s.deceleration = 1.6f;
                s.turnRate = 22f;       // chalupas 28
            });
            var cannons = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_Brigue.asset", c =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(combat.enemyCannons), c);
                c.cannonsPerSide = 6;   // chalupa 4: bordada de 84 de dano se todas acertarem
                c.reloadTime = 4.5f;    // chalupa inimiga 5
                c.spreadDegrees = 0.9f; // chalupa 1,2
                c.damagePerBall = 14f;  // chalupa 12
            });

            var brig = CreateEnemy("Brigue_SantaClara", BrigSpawnPoints[0], Quaternion.Euler(0f, 200f, 0f), stats, cannons,
                BrigHull(BrigModel), BrigHullHealth, combat, player.transform);
            var ai = brig.GetComponent<EnemyShipAI>();
            ai.SetTargetId(BrigMissionId);
            ai.SetFame(25);
            // Tripulação veterana: erra menos o alcance, dispara mais rápido e num través mais aberto, luta até o fim.
            ai.ConfigureGunnery(rangeErrorFraction: 0.04f, reaction: new Vector2(0.15f, 0.45f), fireArc: 18f, fleeAt: 0.1f, leadErrorFraction: 0.2f);

            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Brigue.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 80, 160), Drop(items.wood, 1f, 15, 30), Drop(items.powder, 0.9f, 6, 14),
                    Drop(items.iron, 0.8f, 5, 10), Drop(items.crew, 0.8f, 2, 5),
                };
                t.chestChance = 0.5f;   // chalupa 20%
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 250);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            EditorUtility.SetDirty(table);
            brig.gameObject.AddComponent<LootDropper>().Configure(table);
            // O ★ do HUD segue o navio (a missão aponta para ele enquanto estiver no mar).
            brig.gameObject.AddComponent<WorldMarker>().Configure(BrigMissionId, "Brigue Santa Clara", 12f);

            // Por último: o molde do respawner é uma cópia do brigue já completo.
            AddRespawner("BrigueRespawner", brig.gameObject, player, BrigSpawnPoints, 150f, BrigMissionId);
        }

        static MissionDefinition BrigMission(LootItems i) => Mission("Missao_BrigueSantaClara", m =>
        {
            m.missionId = "brigue_santa_clara";
            m.title = "O brigue Santa Clara";
            m.giver = "Contramestre Ferraz";
            m.description = "Um brigue mercante bem armado, o Santa Clara, ronda as águas ao norte, depois da Ilha Norte. " +
                            "Seis canhões por bordo: venha preparado. Afunde-o e o porão dele é seu.";
            m.type = MissionType.SinkShips;
            m.count = 1;
            m.targetId = BrigMissionId;
            m.targetName = "brigue Santa Clara";
            m.rewards = new[] { new ItemStack(i.gold, 300), new ItemStack(i.iron, 8) };
        });
    }
}
