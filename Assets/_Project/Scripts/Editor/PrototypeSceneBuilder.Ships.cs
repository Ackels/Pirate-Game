using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Progression;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Navio novo (M8.2, cenas com porto): classes do navio do jogador (Data/Ships/ShipClass_*) e os cascos de cada uma no
    /// mesmo objeto do navio (PlayerShipClass). A chalupa é o casco que a cena já tem; a escuna (ship-pirate-medium, dois
    /// mastros) fica montada e desligada ao lado, com os canhões dela, e entra quando o estaleiro termina a obra (esquema
    /// "Escuna" achado no baú raro + a construção Estaleiro na base). No navio novo os upgrades começam do zero e rendem e
    /// custam mais (multiplicadores da classe). Assets existentes são preservados (ajuste de game design).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string ShipsFolder = DataFolder + "/Ships";
        const string SchoonerSchematic = "Esquema_Escuna", SchoonerModel = "ship-pirate-medium";
        const string GalleonSchematic = "Esquema_Galeao";

        static void AddShipClasses(ShipController player, LootItems items, CombatAssets combat, BuildingDefinition shipyard)
        {
            EnsureFolder(DataFolder, "Ships");
            var root = player.transform;
            var body = player.GetComponent<Rigidbody>();
            var sloopCannons = player.GetComponentInChildren<CannonBattery>().Stats;

            var sloop = LoadOrCreateAsset<ShipClassDefinition>(ShipsFolder + "/ShipClass_Chalupa.asset", c =>
            {
                c.displayName = "Chalupa";
                c.description = "Um mastro, rápida e ágil, mas de casco fino e porão pequeno.";
                c.tier = 0;
                c.hullMax = PlayerHull;
                c.holdCapacity = SloopHoldCapacity;
                c.crewBerths = 16;
            });
            // Porão maior (M9): as classes criadas antes ficam com o tamanho novo (só se ainda estão no antigo).
            if (Mathf.Approximately(sloop.holdCapacity, 30f)) sloop.holdCapacity = SloopHoldCapacity;
            sloop.stats = player.Stats;
            sloop.cannons = sloopCannons;
            EditorUtility.SetDirty(sloop);

            var schoonerStats = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_Escuna.asset", s =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(player.Stats), s);
                s.maxSpeed = 10.5f;     // chalupa 11: um pouco mais lenta
                s.acceleration = 1.4f;  // chalupa 1,6
                s.deceleration = 1.9f;  // chalupa 2,2
                s.turnRate = 24f;       // chalupa 28: mais pesada para virar
            });
            var schoonerCannons = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_Escuna.asset", c =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sloopCannons), c);
                c.cannonsPerSide = 6;   // chalupa 4: bordada 50% maior
            });
            var schematic = LoadItem(SchoonerSchematic);
            var schooner = LoadOrCreateAsset<ShipClassDefinition>(ShipsFolder + "/ShipClass_Escuna.asset", c =>
            {
                c.displayName = "Escuna";
                c.description = "Dois mastros e seis canhões por bordo: casco mais grosso, porão e alojamento maiores. Um pouco mais " +
                                "lenta e pesada para virar que a chalupa.";
                c.tier = 1;
                c.hullMax = 260f;
                c.holdCapacity = 75f;
                c.crewBerths = 24;
                c.cost = new[] { new ItemStack(items.gold, 500), new ItemStack(items.wood, 80), new ItemStack(items.iron, 25) };
                c.buildSeconds = 180f;
                // Pedido do Jhonata (04/10): upgrades iguais aos da chalupa, menos casco, velocidade (2 velas) e recarga, ×1,2.
                // No nível 3: casco +180 (chalupa +150), velocidade +30% (+25%), recarga -42% (-35%). Dano e mira iguais (os 2
                // canhões a mais já somam dano; mira passa de 100% com qualquer bônus). Custo igual ao da chalupa.
                c.hullUpgradeMultiplier = 1.2f;
                c.speedUpgradeMultiplier = 1.2f;
                c.reloadUpgradeMultiplier = 1.2f;
            });
            if (Mathf.Approximately(schooner.holdCapacity, 50f)) schooner.holdCapacity = 75f;
            schooner.stats = schoonerStats;
            schooner.cannons = schoonerCannons;
            schooner.schematic = schematic;
            schooner.requiredBuilding = shipyard;
            EditorUtility.SetDirty(schooner);

            // Casco da chalupa: o que a cena já montou (CreateShip + AddNavalCombat).
            var sloopHull = SloopHull(PlayerShipModel);
            var sloopRig = Rig(sloop, sloopHull, SailsOf(player.GetComponent<SailVisual>()),
                root.Find("Visual").gameObject, root.Find("Cannons").gameObject);

            // Casco da escuna: modelo e canhões montados ao lado, desligados até o navio virar escuna.
            var schoonerHull = PirateMediumHull(SchoonerModel);
            var visual = new GameObject("Visual_Escuna").transform;
            visual.SetParent(root, false);
            var model = Kenney(schoonerHull.model, visual, new Vector3(0f, ShipModelKeel, 0f), objectName: "Casco");
            var sails = FurlableSails(model.transform);
            var cannons = new GameObject("Cannons_Escuna").transform;
            cannons.SetParent(root, false);
            CreateBattery(BroadsideSide.Port, schoonerCannons, combat, cannons, body, schoonerHull, withAimPreview: true, hullModel: model.transform);
            CreateBattery(BroadsideSide.Starboard, schoonerCannons, combat, cannons, body, schoonerHull, withAimPreview: true, hullModel: model.transform);
            visual.gameObject.SetActive(false);
            cannons.gameObject.SetActive(false);
            var schoonerRig = Rig(schooner, schoonerHull, sails, visual.gameObject, cannons.gameObject);

            var galleonRig = AddGalleon(player, items, combat, shipyard, schoonerStats, schoonerCannons);

            // Lanterna da popa (uma só, criada no CreateShip pelo casco da chalupa): cada casco a põe na popa dele.
            var lantern = root.Find("Lanterna_Popa");
            foreach (var (rig, hull) in new[] { (sloopRig, sloopHull), (schoonerRig, schoonerHull), (galleonRig, BrigHull(BossModel)) })
            {
                rig.lantern = lantern;
                rig.lanternPosition = Stern(hull);
            }

            player.gameObject.AddComponent<PlayerShipClass>().Configure(new[] { sloopRig, schoonerRig, galleonRig });
        }

        /// <summary>Lugar da lanterna da popa num casco (o mesmo cálculo do ShipLantern).</summary>
        static Vector3 Stern(ShipHull hull) =>
            hull.colliderCenter + new Vector3(0f, hull.colliderSize.y * 0.5f + 0.2f, -hull.colliderSize.z * 0.5f + 1.2f);

        /// <summary>
        /// Galeão (3ª classe, pedido do Jhonata em 09/10): o casco grande pirata do Tormenta (ship-pirate-large, medidas do brigue), 8 canhões
        /// por bordo, o mais forte e o mais lento dos três. Planta só no baú do Tormenta ou no mercado da patente 4, e pede o Estaleiro 2.
        /// Assets preservados depois de criados (ajuste de game design).
        /// </summary>
        static PlayerShipClass.Rig AddGalleon(ShipController player, LootItems items, CombatAssets combat, BuildingDefinition shipyard,
            ShipStats schoonerStats, CannonStats schoonerCannons)
        {
            var root = player.transform;
            var body = player.GetComponent<Rigidbody>();
            var stats = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_GaleaoJogador.asset", s =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(schoonerStats), s);
                s.maxSpeed = 10f;       // escuna 10,5, chalupa 11
                s.acceleration = 1.2f;  // escuna 1,4
                s.deceleration = 1.6f;  // escuna 1,9
                s.turnRate = 20f;       // escuna 24, chalupa 28
            });
            var cannons = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_GaleaoJogador.asset", c =>
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(schoonerCannons), c);
                c.cannonsPerSide = 8;   // escuna 6, chalupa 4
            });
            var galleon = LoadOrCreateAsset<ShipClassDefinition>(ShipsFolder + "/ShipClass_Galeao.asset", c =>
            {
                c.displayName = "Galeão";
                c.description = "Três mastros e oito canhões por bordo: o casco mais grosso, o maior porão e o maior alojamento. " +
                                "O mais lento e o mais pesado para virar dos três.";
                c.tier = 2;
                c.hullMax = 420f;
                c.holdCapacity = 110f;
                c.crewBerths = 32;
                c.cost = new[]
                {
                    new ItemStack(items.gold, 1200), new ItemStack(items.wood, 160), new ItemStack(items.iron, 60), new ItemStack(items.powder, 40),
                };
                c.buildSeconds = 300f;
                c.hullUpgradeMultiplier = 1.4f;
                // Velocidade ×1,3 (não 1,4): com 1,4 o galeão no nível 4 empatava com a escuna (14,3 × 14,4) e deixava de ser o mais lento
                // (revisão do chat de otimização, 09/10).
                c.speedUpgradeMultiplier = 1.3f;
                c.reloadUpgradeMultiplier = 1.4f;
                c.requiredBuildingLevel = 2;
            });
            galleon.stats = stats;
            galleon.cannons = cannons;
            galleon.schematic = LoadItem(GalleonSchematic);
            galleon.requiredBuilding = shipyard;
            EditorUtility.SetDirty(galleon);

            var hull = BrigHull(BossModel);
            var visual = new GameObject("Visual_Galeao").transform;
            visual.SetParent(root, false);
            var model = Kenney(hull.model, visual, new Vector3(0f, ShipModelKeel, 0f), objectName: "Casco");
            var sails = FurlableSails(model.transform);
            var batteries = new GameObject("Cannons_Galeao").transform;
            batteries.SetParent(root, false);
            CreateBattery(BroadsideSide.Port, cannons, combat, batteries, body, hull, withAimPreview: true, hullModel: model.transform);
            CreateBattery(BroadsideSide.Starboard, cannons, combat, batteries, body, hull, withAimPreview: true, hullModel: model.transform);
            visual.gameObject.SetActive(false);
            batteries.gameObject.SetActive(false);
            return Rig(galleon, hull, sails, visual.gameObject, batteries.gameObject);
        }

        static PlayerShipClass.Rig Rig(ShipClassDefinition shipClass, ShipHull hull, Transform[] sails, params GameObject[] parts) => new()
        {
            shipClass = shipClass,
            parts = parts,
            sails = sails,
            mass = hull.mass,
            inertia = hull.inertia,
            colliderCenter = hull.colliderCenter,
            colliderSize = hull.colliderSize,
            floatPoints = hull.floatPoints,
        };

        /// <summary>Eixos de vela que o CreateShip já deu ao SailVisual (FurlableSails não pode rodar duas vezes no mesmo modelo).</summary>
        static Transform[] SailsOf(SailVisual visual)
        {
            var list = new SerializedObject(visual).FindProperty("sails");
            var sails = new Transform[list.arraySize];
            for (int i = 0; i < sails.Length; i++) sails[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            return sails;
        }

        /// <summary>
        /// O mesmo problema da cena gerada em memória vale para as classes: depois de gerar, a ShipClass_Chalupa ficava apontando
        /// para um ShipStats/CannonStats que não existe mais (o arquivo estava certo). Descarregar faz o editor reler do disco.
        /// </summary>
        static void ReloadShipClasses()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ShipClassDefinition", new[] { ShipsFolder }))
            {
                var shipClass = AssetDatabase.LoadAssetAtPath<ShipClassDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (shipClass && !EditorUtility.IsDirty(shipClass)) Resources.UnloadAsset(shipClass);
            }
        }

        /// <summary>Põe o esquema no sorteio do baú raro de todas as tabelas de Data que já sorteiam esquemas.</summary>
        static void AddSchematicToLootTables(ItemDefinition schematic, float weight)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:LootTable", new[] { DataFolder }))
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (!table || table.schematics == null || table.schematics.Length == 0) continue;
                if (System.Array.Exists(table.schematics, s => s.item == schematic)) continue;
                var list = new System.Collections.Generic.List<LootTable.WeightedItem>(table.schematics) { Weighted(schematic, weight) };
                table.schematics = list.ToArray();
                EditorUtility.SetDirty(table);
            }
        }
    }
}
