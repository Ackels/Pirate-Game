using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Progression;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Navios mercantes (G, passo 1, 03/11; cena 07): três <see cref="MerchantShip"/> neutros (ship-medium, bandeira azul e dourada da Coroa,
    /// sem canhões) indo de porto em porto entre um ponto de rota no mar perto de cada ilha. Cada um tem o seu respawner (volta noutro ponto
    /// da rota, longe do jogador, 3 min depois de afundar). Saque bom (ouro, madeira, ferro, pólvora, rações, baú 25%). Atacar mercante ainda
    /// não tem consequência (o nível de procurado é o passo 2; a Esquadra Real, o 3).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string MerchantModel = "ship-medium";
        const float MerchantHull = 240f;

        static readonly string[] MerchantNames = { "Mercante Boa Esperança", "Mercante Santa Rita", "Mercante Flor do Mar" };

        static void AddMerchants(ShipController player, LootAssets loot)
        {
            var route = MerchantRoute();
            if (route.Length < 2)
            {
                Debug.LogWarning("[Pirates] Mercantes: a rota ficou com menos de 2 portos.");
                return;
            }
            var hunterStats = AssetDatabase.LoadAssetAtPath<ShipStats>(DataFolder + "/ShipStats_Cacador.asset");
            var stats = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Mercante.asset", s =>
            {
                if (hunterStats) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(hunterStats), s);
                s.maxSpeed = 9f;          // a chalupa (11) alcança; fugindo a vela cheia ainda dá trabalho
                s.acceleration = 1.2f;
                s.deceleration = 1.6f;
                s.turnRate = 20f;
            });

            var items = loot.items;
            var ration = LoadItem("Item_Racao");
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Mercante.asset", t =>
            {
                var drops = new List<LootTable.Drop>
                {
                    Drop(items.gold, 1f, 50, 100), Drop(items.wood, 0.9f, 10, 21), Drop(items.iron, 0.8f, 4, 8),
                    Drop(items.powder, 0.8f, 4, 10),
                };
                if (ration) drops.Add(Drop(ration, 0.6f, 2, 4));
                t.drops = drops.ToArray();
                t.chestChance = 0.25f;
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 200);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            EditorUtility.SetDirty(table);
            // A carga boa dos recursos novos (R1): lona, cordame, rum, especiarias.
            AddMaterialLoot();

            // Bandeira da Coroa: azul com dourado (as dos piratas são pretas, a dos corsários vermelha).
            var flag = GetMaterial("M_Bandeira_Mercante", new Color(0.12f, 0.27f, 0.62f), 0.2f);
            var group = new GameObject("Mercantes").transform;
            for (int i = 0; i < MerchantNames.Length; i++)
            {
                Vector3 at = route[(i * 2) % route.Length];
                Vector3 next = route[(i * 2 + 1) % route.Length];
                var rotation = Quaternion.LookRotation(new Vector3(next.x - at.x, 0f, next.z - at.z));
                var merchant = CreateMerchant($"Mercante_{i}", at + Vector3.up * 0.3f, rotation, stats, table, flag, route,
                    MerchantNames[i], player.transform);
                merchant.transform.SetParent(group, true);

                // Molde desligado (cópia antes de qualquer ajuste de cena) e o respawner dele.
                var spawner = new GameObject($"Respawner_Mercante_{i}");
                spawner.transform.SetParent(group, false);
                var template = Object.Instantiate(merchant.gameObject, spawner.transform);
                template.name = merchant.name;
                template.SetActive(false);
                var points = new Vector3[route.Length];
                for (int k = 0; k < route.Length; k++) points[k] = route[k] + Vector3.up * 0.3f;
                spawner.AddComponent<EnemyRespawner>().Configure(merchant.GetComponent<SinkOnDeath>(), template, player.transform,
                    points, 480f, null);
            }

            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<MerchantHUD>()) hud.gameObject.AddComponent<MerchantHUD>();

            // Procurado pela Coroa (G, passo 2): atacar e afundar mercantes sobe as estrelas; o Almirante Teodoro perdoa (Npcs.cs).
            var wanted = new GameObject("Procurado").AddComponent<WantedLevel>();
            wanted.Configure(AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/Item_Ouro.asset"));
            if (hud && !hud.GetComponent<WantedHUD>()) hud.gameObject.AddComponent<WantedHUD>();
        }

        static ShipController CreateMerchant(string name, Vector3 position, Quaternion rotation, ShipStats stats, LootTable table,
            Material flag, Vector3[] route, string label, Transform player)
        {
            var hull = PirateMediumHull(MerchantModel);
            var ship = CreateShip(name, position, rotation, stats, hull);
            MakeSinkable(ship.gameObject, MerchantHull);
            ship.gameObject.AddComponent<ShipDebuffs>();
            ship.gameObject.AddComponent<AimTarget>();
            ship.gameObject.AddComponent<MerchantShip>().Configure(label, route, player);
            ship.gameObject.AddComponent<LootDropper>().Configure(table);
            foreach (var r in ship.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("flag")) r.sharedMaterial = flag;
            return ship;
        }

        /// <summary>
        /// Um ponto no mar perto de cada ilha (Porto Alento, Caveira, Norte, Oeste, Ermida), do lado que dá para o meio do mapa: o primeiro
        /// sem nada sólido a 40 m, saindo do centro da ilha. Fica fora dos covis dos monstros e longe do Tormenta.
        /// </summary>
        static Vector3[] MerchantRoute()
        {
            var points = new List<Vector3>();
            var middle = new Vector3(100f, 0f, 0f);
            foreach (var name in new[] { "Ilha_Sul", "IlhaGrande", "Ilha_Norte", "Ilha_Oeste", "Ilha_Ermida" })
            {
                var island = GameObject.Find(name);
                if (!island) continue;
                Vector3 c = island.transform.position;
                c.y = 0f;
                Vector3 dir = middle - c;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1f ? dir.normalized : Vector3.forward;
                bool found = false;
                for (int turn = 0; turn < 12 && !found; turn++)
                {
                    // Primeiro de frente para o meio do mapa, depois abrindo para os lados.
                    float yaw = (turn % 2 == 0 ? 1 : -1) * 30f * ((turn + 1) / 2);
                    Vector3 d = Quaternion.AngleAxis(yaw, Vector3.up) * dir;
                    for (float dist = 90f; dist <= 260f; dist += 10f)
                    {
                        Vector3 p = c + d * dist;
                        if (Physics.CheckSphere(p, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Vector3.Distance(p, SquidLair) < 300f || Vector3.Distance(p, MegalodonLair) < 300f) continue;
                        points.Add(p);
                        found = true;
                        break;
                    }
                }
            }
            return points.ToArray();
        }
    }
}
