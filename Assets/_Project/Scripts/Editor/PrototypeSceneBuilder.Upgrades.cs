using Pirates.Combat;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Parte de upgrades do gerador (Protótipo 05, M3.2): assets dos upgrades (Data/Upgrades, com tempo de obra), ShipUpgrades no
    /// navio do jogador, a tripulação (Data/CrewSettings_Navio + ShipCrew, M8.1) e o respawner de inimigos (sem ele não há
    /// recursos para comprar mais de um upgrade).
    /// Assets existentes são preservados (são ajuste de game design).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string UpgradesFolder = DataFolder + "/Upgrades";

        static readonly Vector3[] HarborEnemySpawnPoints =
        {
            new(-50f, 0.3f, 230f), new(230f, 0.3f, 40f), new(-220f, 0.3f, -60f),
        };
        /// <summary>
        /// Mundo (cena 07, mapa espaçado em 16/10): as chalupas patrulham os caminhos entre as ilhas (Oeste–Norte, Norte–Caveira e a
        /// sudoeste da Oeste), a 370–510 m do cais, longe do brigue. As outras cenas ficam com os pontos de antes.
        /// </summary>
        static readonly Vector3[] WorldEnemySpawnPoints =
        {
            new(-100f, 0.3f, 330f), new(360f, 0.3f, 190f), new(-330f, 0.3f, -120f),
        };
        static Vector3[] EnemySpawnPoints => generatingScenePath == WorldScenePath ? WorldEnemySpawnPoints : HarborEnemySpawnPoints;

        /// <summary>Upgrades do navio; o último nível de cada um exige <paramref name="topLevelBuilding"/> (Estaleiro) na base.</summary>
        static void AddUpgrades(ShipController player, LootItems items, BuildingDefinition topLevelBuilding)
        {
            requiredForTopLevel = topLevelBuilding;
            EnsureFolder(DataFolder, "Upgrades");
            var g = items.gold;
            var w = items.wood;
            var i = items.iron;
            var p = items.powder;

            var hull = Upgrade("Upgrade_Casco", "Casco reforçado", UpgradeStat.HullMax,
                "Tábuas duplas e cintas de ferro: mais vida no casco.",
                Lvl(40f, 0, 20f, (w, 10), (i, 3), (g, 60)),
                Lvl(90f, 0, 40f, (w, 20), (i, 6), (g, 120)),
                Lvl(150f, 0, 75f, (w, 30), (i, 10), (g, 200)));
            var cannons = Upgrade("Upgrade_Canhoes", "Canhões reforçados", UpgradeStat.CannonDamage,
                "Bocas de ferro mais grossas aguentam carga maior: mais dano por bala.",
                Lvl(3f, 0, 20f, (i, 4), (p, 2), (g, 50)),
                Lvl(6f, 0, 40f, (i, 8), (p, 4), (g, 100)),
                Lvl(10f, 0, 75f, (i, 12), (p, 6), (g, 180)));
            var sails = Upgrade("Upgrade_Velame", "Velame novo", UpgradeStat.MaxSpeedPercent,
                "Velas e cordame novos: mais velocidade máxima.",
                Lvl(8f, 0, 20f, (w, 10), (g, 40)),
                Lvl(16f, 0, 40f, (w, 18), (g, 80)),
                Lvl(25f, 0, 75f, (w, 28), (g, 150)));
            var gunners = Upgrade("Upgrade_Artilheiros", "Artilheiros treinados", UpgradeStat.ReloadPercent,
                "Sobreviventes treinados nos canhões: recarga mais rápida. Exige tripulação extra a bordo.",
                Lvl(12f, 1, 20f, (p, 3), (g, 60)),
                Lvl(24f, 3, 40f, (p, 6), (g, 120)),
                Lvl(35f, 5, 75f, (p, 9), (g, 200)));

            var aim = Upgrade("Upgrade_Mira", "Luneta e mira assistida", UpgradeStat.AimAssistPercent,
                "A mira gruda na distância do inimigo, desacelera perto dele e os canhões giram até 12° para ele. Menos dispersão. Nível 3 antecipa o movimento do alvo.",
                Lvl(40f, 0, 15f, (i, 3), (p, 2), (g, 40)),
                Lvl(70f, 0, 35f, (i, 6), (p, 4), (g, 90)),
                Lvl(100f, 0, 60f, (i, 10), (p, 6), (g, 160)));

            // Nível 4 (M9): pede o Estaleiro nível 2; custa ~1,7× o nível 3 e rende menos (rendimento decrescente). A mira já
            // chega a 100% no nível 3: não ganha nível 4. Assets preservados: só cresce quem ainda tem 3 níveis.
            AddUpgradeLevel(hull, Lvl(200f, 0, 120f, (w, 51), (i, 17), (g, 340)), topLevelBuilding);
            AddUpgradeLevel(cannons, Lvl(13f, 0, 120f, (i, 20), (p, 10), (g, 305)), topLevelBuilding);
            AddUpgradeLevel(sails, Lvl(31f, 0, 120f, (w, 48), (g, 255)), topLevelBuilding);
            AddUpgradeLevel(gunners, Lvl(42f, 7, 120f, (p, 15), (g, 340)), topLevelBuilding);

            // Tripulação antes dos upgrades: o ShipUpgrades aplica o bônus dela nos stats.
            var crew = LoadOrCreateAsset<CrewSettings>(DataFolder + "/CrewSettings_Navio.asset", c =>
            {
                c.berths = 16;
                c.reloadBonusAtFull = 0.2f;
                c.handlingBonusAtFull = 0.15f;
                c.damagePerCasualty = 40f;      // casco cheio (150) perdido = ~4 tripulantes
                c.recruitCost = new[] { new ItemStack(g, 30) };
            });
            crew.crewItem = items.crew;
            UnityEditor.EditorUtility.SetDirty(crew);
            player.gameObject.AddComponent<ShipCrew>().Configure(crew);

            player.gameObject.AddComponent<ShipUpgrades>().Configure(new[] { aim, hull, cannons, sails, gunners });
        }

        /// <summary>
        /// Guarda uma cópia desativada do inimigo como molde e liga o respawner: afundou, outro aparece
        /// no ponto mais longe do jogador.
        /// </summary>
        static void AddEnemyRespawner(ShipController player)
        {
            var enemy = GameObject.Find("NavioInimigo");
            if (enemy) AddRespawner("EnemyRespawner", enemy, player, EnemySpawnPoints, 25f, null);
        }

        /// <summary>Molde desativado (cópia do navio já completo) e o respawner que o clona depois de afundar.</summary>
        static void AddRespawner(string name, GameObject enemy, ShipController player, Vector3[] points, float delay, string targetId)
        {
            var spawner = new GameObject(name);
            var template = Object.Instantiate(enemy, spawner.transform);
            template.name = enemy.name;
            template.SetActive(false);
            spawner.AddComponent<EnemyRespawner>().Configure(enemy.GetComponent<SinkOnDeath>(), template,
                player.transform, points, delay, targetId);
        }

        static BuildingDefinition requiredForTopLevel;

        static UpgradeDefinition Upgrade(string file, string name, UpgradeStat stat, string description,
            params UpgradeDefinition.Level[] levels) =>
            LoadOrCreateAsset<UpgradeDefinition>($"{UpgradesFolder}/{file}.asset", u =>
            {
                u.displayName = name;
                u.stat = stat;
                u.description = description;
                u.levels = levels;
                if (levels.Length > 0) levels[^1].requiredBuilding = requiredForTopLevel;
            });

        static void AddUpgradeLevel(UpgradeDefinition u, UpgradeDefinition.Level level, BuildingDefinition shipyard)
        {
            if (!u || u.levels.Length != 3) return;
            level.requiredBuilding = shipyard;
            level.requiredBuildingLevel = 2;
            var list = new System.Collections.Generic.List<UpgradeDefinition.Level>(u.levels) { level };
            u.levels = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(u);
        }

        static UpgradeDefinition.Level Lvl(float value, int crew, float buildSeconds, params (ItemDefinition item, int amount)[] cost)
        {
            var stacks = new ItemStack[cost.Length];
            for (int k = 0; k < cost.Length; k++) stacks[k] = new ItemStack(cost[k].item, cost[k].amount);
            return new UpgradeDefinition.Level { value = value, crewRequired = crew, buildSeconds = buildSeconds, cost = stacks };
        }
    }
}
