using Pirates.Base;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Ships;
using Pirates.UI;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Parte do porto-base do gerador (Protótipo 05): cais com píer na costa norte da Ilha Sul, a vila (M6.4,
    /// PrototypeSceneBuilder.Port.cs), o armazém sem limite e o HarborVisitor no navio do jogador.
    /// Upgrades e reaparição de inimigos (M3.2) ficam em PrototypeSceneBuilder.Upgrades.cs.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Ilha Sul: costa norte em z ≈ -181 (fixa desde o M3.1), chão da praia em y ≈ 0,8.
        static readonly Vector3 PierShore = new(40f, 0f, -184f);
        const float PierLength = 24f;
        const float DockRadius = 20f;
        /// <summary>Raio da zona segura em volta do cais: inimigos não atacam e as obras do estaleiro andam.</summary>
        const float SafeZoneRadius = 60f;

        static void AddHarbor(ShipController player, LootItems items, CombatAssets combat, Material deck, Material hullWood)
        {
            var settings = LoadOrCreateAsset<HarborSettings>(DataFolder + "/HarborSettings_Base.asset", s =>
            {
                s.repairWood = items.wood;
                s.woodPer10Hull = 1;
                s.repairGold = items.gold;
                s.goldPer10Hull = 5;
            });

            var root = new GameObject("PortoBase").transform;
            BuildPier(root);
            AddVillage(root);

            // Ponto de atracação: na ponta do píer, do lado de fora.
            var dockGo = new GameObject("Cais");
            dockGo.transform.SetParent(root, false);
            dockGo.transform.position = PierShore + new Vector3(0f, 0f, PierLength + 2f);
            dockGo.AddComponent<Inventory>().Configure(0f, unlimitedSpace: true);
            dockGo.AddComponent<SafeZone>().Configure(SafeZoneRadius);
            SafeZoneBuoys(root, dockGo.transform.position);
            dockGo.AddComponent<HarborDock>().Configure("Porto Alento", settings, DockRadius);

            var buildings = LoadBuildingAssets(items);
            AddLevelFour(buildings, items);
            AddBuildings(dockGo, buildings, combat, deck, hullWood);
            AddUpgrades(player, items, buildings.shipyard);
            AddShipClasses(player, items, combat, buildings.shipyard);
            AddDeckCrew(player);
            var supplies = LoadSupplies();
            AddPlayerSupplies(player, supplies, items);
            ConfigureHarborTrade(settings, items, supplies, buildings.forge);
            AddCaptainFame(settings);
            AddEnemyRespawner(player);
            player.gameObject.AddComponent<HarborVisitor>();
            new GameObject("HarborHUD").AddComponent<HarborHUD>().Bind(player.GetComponent<HarborVisitor>());

            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (!t.GetComponentInParent<Rigidbody>() && !t.GetComponent<HarborDock>()) t.gameObject.isStatic = true;
        }

        /// <summary>Boias verdes marcando a borda da zona segura (pula as que cairiam em cima da ilha).</summary>
        static void SafeZoneBuoys(Transform root, Vector3 center)
        {
            var mat = GetMaterial("M_SafeBuoy", new Color(0.15f, 0.7f, 0.3f), 0.4f);
            var parent = new GameObject("ZonaSegura_Boias").transform;
            parent.SetParent(root, false);
            const int count = 28;
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * SafeZoneRadius;
                if (OverLand(pos)) continue;
                Buoy(parent, $"Boia_Zona_{k}", pos, mat);
            }
        }

        /// <summary>Boia que cairia em terra (chão da ilha acima de -1 m) ou em cima do cais.</summary>
        static bool OverLand(Vector3 pos) =>
            Physics.Raycast(new Vector3(pos.x, 60f, pos.z), Vector3.down, out RaycastHit hit, 120f) && hit.point.y > -1f;

        /// <summary>
        /// Píer: tábuas do kit sobre estacas (structure-platform-dock, 2,5 m → 4 m), um colisor de caixa (o navio encosta e
        /// não atravessa; o personagem anda em cima), mastro com bandeira pirata na ponta, caixotes e lanternas.
        /// </summary>
        static void BuildPier(Transform root)
        {
            var pier = new GameObject("Pier").transform;
            pier.SetParent(root, false);
            pier.position = PierShore;

            const float scale = 1.6f;
            for (int i = 0; i < Mathf.RoundToInt(PierLength / 4f); i++)
                Kenney("structure-platform-dock", pier, new Vector3(0f, 1.25f - 1.31f * scale, 2f + i * 4f), 0f, scale, $"Tabuas_{i}");
            var deck = pier.gameObject.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, 1.05f, PierLength * 0.5f);
            deck.size = new Vector3(4f, 0.4f, PierLength);
            // Mastro com a bandeira pirata na ponta: dá para achar o porto de longe.
            Kenney("flag-pirate-high", pier, new Vector3(1.5f, 1.25f, PierLength - 0.6f), 90f, 1.8f, "Bandeira");
            for (int i = 0; i < 3; i++)
                NightLantern(Model(Kit.Town, "lantern", pier, new Vector3(-1.75f, 1.25f, 4f + i * 8f), 0f, 1.5f, $"Lanterna_{i}"));
            Kenney("crate", pier, new Vector3(1.1f, 1.25f, 5f), 10f, 1f, "Caixote_A");
            Kenney("crate-bottles", pier, new Vector3(1.2f, 1.25f, 6.4f), -20f, 1f, "Caixote_B");
            Kenney("barrel", pier, new Vector3(-1.1f, 1.25f, 14f), 0f, 0.7f, "Barril_A");
            Kenney("barrel", pier, new Vector3(-1.2f, 1.25f, 15.1f), 0f, 0.7f, "Barril_B");
            Model(Kit.Survival, "box-large", pier, new Vector3(1.2f, 1.25f, 19f), 0f, SurvivalScale, "Caixa");
        }
    }
}
