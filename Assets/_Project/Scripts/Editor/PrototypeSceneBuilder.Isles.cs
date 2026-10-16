using System.Collections.Generic;
using Pirates.Ambient;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Ships;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ilhas Oeste e Norte 2× (M6.9). Terreno e mata em todas as cenas (CreateIslands); o que se joga só no mundo (cena 07):
    /// <b>Oeste</b> (~80 m): esconderijo de contrabandistas na enseada norte (palafitas, cabanas, cais com botes e uma
    /// chalupa ancorada, contrabando, fogueira, 4 contrabandistas e o baú do contrabando), o tesouro do Zé Caolho numa gruta
    /// de pedras no lado sul do morro e um segundo tesouro na praia noroeste. <b>Norte</b> (~105 m): forte de pedra na costa
    /// sul (muralha em anel com portão a leste, torre, 4 canhões que atiram no seu navio ao chegar perto), que se cala
    /// derrubando as muralhas a tiros de canhão (EnemyFort), guarnição de 5 piratas e o paiol com saque grande.
    /// Layout em coordenadas locais (X leste, Z norte) a partir do centro de cada ilha.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Propriedades (não campos): campos estáticos de outro arquivo da classe parcial não têm ordem garantida.
        // Mapa espaçado (16/10, ~1,6×): eram (−170, 60) e (110, 190).
        static Vector3 WestCenter => new(-340f, 0f, 140f);
        static Vector3 NorthCenter => new(140f, 0f, 400f);
        const float WestRX = 40f, WestRZ = 38f, NorthRX = 52f, NorthRZ = 44f;

        // ---------- Oeste ----------
        static Vector2 SmugglersCove => new(10f, 24f);
        static Vector2 WestGrotto => new(-4f, -15f);
        static Vector2 WestTreasure2 => new(-27f, 22f);
        static readonly Vector2[][] WestTrails =
        {
            new[] { new Vector2(36f, 0f), new Vector2(20f, -6f), new Vector2(2f, -13f) },
            new[] { new Vector2(36f, 2f), new Vector2(22f, 12f), new Vector2(12f, 20f) },
            new[] { new Vector2(4f, 26f), new Vector2(-12f, 27f), new Vector2(-24f, 23f) },
        };

        static IslandSpec WestIslandSpec()
        {
            var paths = new List<(Vector2, Vector2, float)> { (SmugglersCove, SmugglersCove, 9f) };
            foreach (var t in WestTrails) for (int k = 1; k < t.Length; k++) paths.Add((t[k - 1], t[k], 2.6f));
            return new IslandSpec
            {
                name = "Ilha_Oeste", center = WestCenter, radiusX = WestRX, radiusZ = WestRZ, coastNoise = 0.16f, coastOutwardOnly = true,
                seed = 23, plateau = 1.4f, beachWidth = 5f, inlandNoise = 0.5f, cell = 1.6f,
                // Litoral leste fixo: a praia de desembarque.
                anchors = new[] { (0f, WestRX, 25f) },
                hills = new[] { (new Vector2(-8f, 0f), 22f, 9f), (new Vector2(-24f, -18f), 10f, 3f) },
                flats = new[] { (SmugglersCove, 11f, 6f, 1.6f) },
                paths = paths.ToArray(),
            };
        }

        static bool WestWild(Vector2 p)
        {
            if (p.x > 30f && Mathf.Abs(p.y) < 9f) return false;            // praia de desembarque
            if ((p - SmugglersCove).magnitude < 15f) return false;          // esconderijo
            if ((p - WestGrotto).magnitude < 8f) return false;
            if ((p - WestTreasure2).magnitude < 5f) return false;
            if ((p - new Vector2(9f, -8f)).magnitude < 5f) return false;   // pedra grande
            foreach (var t in WestTrails)
                for (int k = 1; k < t.Length; k++)
                    if (DistanceToSegment(p, t[k - 1], t[k]) < 2.6f) return false;
            return true;
        }

        // ---------- Norte ----------
        static Vector2 NorthFort => new(0f, -24f);
        // Pátio baixo (2,2 m): mais alto, as balas do navio batem na praia antes da muralha.
        const float FortRadius = 11f, FortFloor = 2.2f, FortWallScale = 1.2f, FortHealth = 150f;
        static readonly Vector2[] NorthTrail = { new(47f, 0f), new(26f, -12f), new(FortRadius + 2f, -24f) };

        static IslandSpec NorthIslandSpec()
        {
            var paths = new List<(Vector2, Vector2, float)> { (NorthFort, NorthFort, FortRadius + 1f) };
            for (int k = 1; k < NorthTrail.Length; k++) paths.Add((NorthTrail[k - 1], NorthTrail[k], 3f));
            return new IslandSpec
            {
                name = "Ilha_Norte", center = NorthCenter, radiusX = NorthRX, radiusZ = NorthRZ, coastNoise = 0.16f, coastOutwardOnly = true,
                seed = 11, plateau = 1.4f, beachWidth = 6f, inlandNoise = 0.6f, cell = 1.6f,
                // Leste: praia de desembarque. Sul: o forte fica a ~20 m da água, de frente para quem chega do porto.
                anchors = new[] { (0f, NorthRX, 22f), (270f, NorthRZ, 30f) },
                hills = new[] { (new Vector2(-15f, 12f), 22f, 8f), (new Vector2(22f, 18f), 16f, 5f), (new Vector2(-34f, -16f), 12f, 3.5f) },
                flats = new[] { (NorthFort, FortRadius + 4f, 8f, FortFloor) },
                paths = paths.ToArray(),
            };
        }

        static bool NorthWild(Vector2 p)
        {
            if (p.x > 40f && Mathf.Abs(p.y) < 9f) return false;
            if ((p - NorthFort).magnitude < FortRadius + 9f) return false;
            for (int k = 1; k < NorthTrail.Length; k++)
                if (DistanceToSegment(p, NorthTrail[k - 1], NorthTrail[k]) < 3f) return false;
            return true;
        }

        // ---------- Conteúdo do mundo (cena 07) ----------

        static Vector3 Ground(Transform island, Vector2 local)
        {
            var world = island.TransformPoint(new Vector3(local.x, 0f, local.y));
            return IslandGround(island, world, out var hit, out _) ? hit : world + Vector3.up * 1.4f;
        }

        /// <summary>
        /// Tudo que se joga nas duas ilhas: desembarques, esconderijo e forte (visual, antes do NavMesh), tesouros, baús,
        /// caixotes, o NavMesh de cada ilha e só então os piratas (não podem virar obstáculo).
        /// </summary>
        static void CreateIsles(LandMaterials m, Material deck, Material hull, LandLootAssets landLoot, LootItems items,
            FootCharacter player, ShipController playerShip)
        {
            var west = GameObject.Find("Ilha_Oeste").transform;
            var north = GameObject.Find("Ilha_Norte").transform;

            // Oeste: desembarque a leste, esconderijo, gruta e segundo tesouro.
            SkullLanding(west, "Desembarque_IlhaOeste", "Ilha Oeste", new Vector2(WestRX, 0f), Vector2.right, deck, hull, "flag-pennant");
            var hideout = SmugglersHideout(west);
            var contraband = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Contrabando.asset", t =>
            {
                t.drops = new[] { Drop(items.gold, 1f, 80, 140), Drop(items.powder, 0.8f, 3, 6), Drop(items.iron, 0.6f, 2, 4), Drop(items.wood, 0.5f, 3, 6) };
                t.chestChance = 0.35f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 120);
            });
            var hideoutChest = CreateChest(hideout, "Bau_Contrabando", "Baú do contrabando", Ground(west, SmugglersCove + new Vector2(-6f, 3f)), 150f,
                contraband, landLoot, withPadlock: true);
            Grotto(west, WestGrotto);
            var buried = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_TesouroEnterrado.asset", t =>
            {
                t.drops = new[] { Drop(items.gold, 1f, 120, 200), Drop(items.iron, 0.6f, 2, 4) };
                t.chestChance = 0.6f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 150);
            });
            BuriedX(west, WestGrotto + new Vector2(0.5f, 1.5f), TreasureMissionId, "Tesouro (gruta da Ilha Oeste)", buried, landLoot, m);
            var smugglerMap = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_TesouroContrabandista.asset", t =>
            {
                t.drops = new[] { Drop(items.gold, 1f, 100, 180), Drop(items.powder, 0.6f, 2, 5), Drop(items.iron, 0.5f, 2, 5) };
                t.chestChance = 0.5f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 130);
            });
            // Bote virado e destroços marcam o lugar.
            var wreck = Kenney("boat-row-small", west, west.InverseTransformPoint(Ground(west, WestTreasure2 + new Vector2(3f, 1.5f))) + Vector3.up * 0.55f,
                Quaternion.Euler(180f, 35f, 0f), 1.4f, "Bote_Virado");
            wreck.AddComponent<BoxCollider>();
            BuriedX(west, WestTreasure2, SmugglerTreasureId, "Tesouro (praia noroeste da Ilha Oeste)", smugglerMap, landLoot, m);
            IslandBreakablesAt(west, landLoot, "Oeste", new[]
            {
                (SmugglersCove + new Vector2(5f, -3f), false), (SmugglersCove + new Vector2(5.8f, -4.2f), true), (SmugglersCove + new Vector2(-7f, -3f), false),
                (SmugglersCove + new Vector2(-8f, -1.6f), false), (SmugglersCove + new Vector2(2f, 6f), true), (new Vector2(28f, -3f), false),
                (WestGrotto + new Vector2(4f, -2f), true), (new Vector2(-18f, 8f), false),
            });
            PowderKegsAt(hideout, HideoutKegs);

            // Norte: desembarque a leste e o forte.
            SkullLanding(north, "Desembarque_IlhaNorte", "Ilha Norte", new Vector2(NorthRX, 0f), Vector2.right, deck, hull, "flag-pennant");
            var fort = EnemyFortAt(north, playerShip, m);
            var magazineLoot = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_PaiolForte.asset", t =>
            {
                t.drops = new[] { Drop(items.gold, 1f, 150, 250), Drop(items.powder, 1f, 6, 10), Drop(items.iron, 0.8f, 4, 8) };
                t.chestChance = 0.6f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 200);
            });
            var magazineChest = CreateChest(fort.transform, "Bau_Paiol", "Paiol do forte", Ground(north, NorthFort + new Vector2(-2f, 3.3f)), 180f,
                magazineLoot, landLoot, withPadlock: true);
            IslandBreakablesAt(north, landLoot, "Norte", new[]
            {
                (NorthFort + new Vector2(-6f, 6f), true), (NorthFort + new Vector2(-7f, 4.5f), true), (NorthFort + new Vector2(5f, 7f), false),
                (NorthFort + new Vector2(6.5f, 5.5f), false), (new Vector2(38f, -4f), false), (new Vector2(18f, 10f), true),
            });

            BakeNavMeshVolume("NavMesh_IlhaOeste", "_NavMesh_Oeste", WestCenter, new Vector3(WestRX * 2.4f, 24f, WestRZ * 2.4f));
            BakeNavMeshVolume("NavMesh_IlhaNorte", "_NavMesh_Norte", NorthCenter, new Vector3(NorthRX * 2.4f, 24f, NorthRZ * 2.4f));

            // Personagens depois do NavMesh.
            var cutlass = EnemyCutlass();
            var pistol = EnemyPistol();
            var smugglers = Gang(hideout, "Esconderijo dos contrabandistas", HideoutCampId, player, cutlass, pistol, m, 26f, new[]
            {
                ("Contrabandista_Cais", new Vector3(2f, 0f, 5f), 0f, false, KitCharacters.Sharky),
                ("Contrabandista_Fogo", new Vector3(-2f, 0f, 1.5f), 160f, false, KitCharacters.Mako),
                ("Duelista_Contrabando", new Vector3(-5f, 0f, -2f), 120f, false, KitCharacters.Anne),
                ("Contrabandista_Atirador", new Vector3(4f, 0f, -4f), 140f, true, KitCharacters.Skeleton),
                ("Bacamarteiro_Contrabando", new Vector3(0.5f, 0f, -3.5f), 170f, true, KitCharacters.Mako),
            });
            hideoutChest.SetLock(smugglers);
            smugglers.SetKegs(hideout.GetComponentsInChildren<PowderKeg>(true));
            var garrison = Gang(fort.transform, "Guarnição do forte", GarrisonCampId, player, cutlass, pistol, m, 26f, new[]
            {
                ("Guarda_Portao_A", new Vector3(FortRadius + 1.5f, 0f, 2f), 90f, false, KitCharacters.Mako),
                ("Guarda_Portao_B", new Vector3(FortRadius + 1.5f, 0f, -2f), 90f, false, KitCharacters.Sharky),
                ("Guarda_Patio", new Vector3(4f, 0f, -5f), 60f, false, KitCharacters.Mako),
                ("Atirador_Paiol", new Vector3(-4f, 0f, 5f), 120f, true, KitCharacters.HeadlessSkeleton),
                ("Atirador_Patio", new Vector3(-5f, 0f, -4f), 80f, true, KitCharacters.Skeleton),
                ("Bacamarteiro_Patio", new Vector3(1.5f, 0f, -6f), 70f, true, KitCharacters.Sharky),
                ("Brutamontes_Forte", new Vector3(2f, 0f, 2.5f), 90f, false, KitCharacters.Sharky),
            });
            magazineChest.SetLock(garrison);
            garrison.SetKegs(fort.GetComponentsInChildren<PowderKeg>(true));

            hideout.gameObject.AddComponent<WorldMarker>().Configure(HideoutCampId, "Esconderijo (Ilha Oeste)", 8f);
            AddBounty(smugglers, "Duelista", DuelistHideoutId, "Duelista (Ilha Oeste)");
            AddBounty(garrison, "Brutamontes", BruteFortId, "Brutamontes (forte)");
            fort.gameObject.AddComponent<WorldMarker>().Configure(FortMissionId, "Forte da Ilha Norte", 14f);
        }

        /// <summary>Um bando (EnemyCamp) com os piratas nos postos dados (local do <paramref name="site"/>).</summary>
        static EnemyCamp Gang(Transform site, string name, string id, FootCharacter player, WeaponDefinition cutlass, WeaponDefinition pistol,
            LandMaterials m, float leash, (string name, Vector3 local, float yaw, bool gunner, string model)[] posts)
        {
            var camp = site.gameObject.AddComponent<EnemyCamp>();
            var parent = new GameObject("Piratas_" + id).transform;
            var members = new EnemyFootAI[posts.Length];
            for (int k = 0; k < posts.Length; k++)
            {
                var p = posts[k];
                members[k] = CreatePirate(parent, p.name, site.TransformPoint(p.local), p.yaw, PostWeapon(p.name, p.gunner, cutlass, pistol), player, camp,
                    m.iron, m.wood, m.brass, p.model);
            }
            camp.Configure(name, id, members, leash, attackersAtOnce: 2);
            return camp;
        }

        static void IslandBreakablesAt(Transform island, LandLootAssets a, string where, (Vector2 local, bool barrel)[] spots)
        {
            var parent = new GameObject("Caixotes_" + where).transform;
            int n = 0;
            foreach (var (local, barrel) in spots)
            {
                var pos = Ground(island, local);
                if (pos.y - island.position.y < 0.3f) continue;
                Breakable(parent, $"{(barrel ? "Barril" : "Caixote")}_{where}_{n++}", pos, n * 41f, barrel, a);
            }
        }

        /// <summary>X de tesouro enterrado (duas tábuas escuras) com o baú embaixo, revelado quando a missão é aceita.</summary>
        static void BuriedX(Transform island, Vector2 local, string id, string label, LootTable table, LandLootAssets loot, LandMaterials m)
        {
            var ground = Ground(island, local);
            var spot = new GameObject("Tesouro_" + id);
            spot.transform.position = ground;
            var mark = new GameObject("X").transform;
            mark.SetParent(spot.transform, false);
            Part(PrimitiveType.Cube, "Traco_A", mark, new Vector3(0f, 0.03f, 0f), new Vector3(1.8f, 0.05f, 0.28f), Quaternion.Euler(0f, 45f, 0f), m.blackFlag);
            Part(PrimitiveType.Cube, "Traco_B", mark, new Vector3(0f, 0.03f, 0f), new Vector3(1.8f, 0.05f, 0.28f), Quaternion.Euler(0f, -45f, 0f), m.blackFlag);
            var chest = CreateChest(spot.transform, "Bau_Enterrado", "Baú enterrado", ground, 15f, table, loot, withPadlock: false);
            spot.AddComponent<BuriedTreasure>().Configure(id, "Tesouro enterrado", mark.gameObject, chest, showFromStart: false);
            spot.AddComponent<WorldMarker>().Configure(id, label, 4f);
        }

        /// <summary>
        /// Esconderijo na enseada norte: palafitas (plataformas sobre estacas com cabana de palha) meio na água, cabanas na
        /// areia, cais com botes e uma chalupa ancorada, pilhas de contrabando, fogueira e bandeira pirata.
        /// </summary>
        static Transform SmugglersHideout(Transform island)
        {
            var root = new GameObject("Esconderijo_Contrabandistas").transform;
            root.position = Ground(island, SmugglersCove);
            var shore = SkullShore(island, SmugglersCove, Vector2.up);

            // Palafitas: metade em terra, metade sobre a água (estacas de 1,5 m).
            for (int k = 0; k < 3; k++)
            {
                var spot = shore + new Vector2(-9f + k * 9f, -1f + (k % 2) * 1.5f);
                var g = Ground(island, spot);
                var stilt = new GameObject($"Palafita_{k}").transform;
                stilt.SetParent(root, false);
                stilt.SetPositionAndRotation(new Vector3(g.x, Mathf.Min(g.y, 0.4f) - 0.2f, g.z), Quaternion.Euler(0f, k * 25f - 20f, 0f));
                Kenney("structure-platform", stilt, Vector3.zero, 0f, 1.6f, "Plataforma");
                Kenney("structure-roof", stilt, Vector3.up * 1.49f, 90f, 1.15f, "Cabana");
                // Lanterna no deck de cada palafita: só a do meio ilumina (as outras brilham).
                NightLantern(Model(Kit.Town, "lantern", stilt, new Vector3(1.3f, 1.49f, 1.3f), 0f, 1.1f, "Lanterna"), realLight: k == 1, range: 8f);
                var box = stilt.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.6f, 0f);
                box.size = new Vector3(4f, 5.2f, 4f);
                Occluder(stilt.gameObject);
            }
            // Cabanas e um abrigo cercado na areia.
            Tent(root, "Cabana_Contrabando_A", new Vector3(-7f, 0f, -4f), 40f);
            Tent(root, "Cabana_Contrabando_B", new Vector3(7f, 0f, -5f), -30f);
            var shed = Kenney("structure-fence-sides", root, root.InverseTransformPoint(Ground(island, SmugglersCove + new Vector2(-1f, -7f))), 0f, 1.3f, "Abrigo");
            var shedBox = shed.AddComponent<BoxCollider>();
            shedBox.center = new Vector3(0f, 1.1f, 0f);
            shedBox.size = new Vector3(2.5f, 2.2f, 2.5f);
            Occluder(shed);

            // Cais saindo da praia, botes amarrados e a chalupa dos contrabandistas ancorada.
            var pierStart = island.TransformPoint(new Vector3(shore.x + 4f, 0f, shore.y - 2f));
            var pier = new GameObject("Cais_Contrabando").transform;
            pier.SetParent(root, false);
            pier.position = new Vector3(pierStart.x, 0f, pierStart.z);
            for (int k = 0; k < 3; k++)
                Kenney("structure-platform-dock", pier, new Vector3(0f, 1.25f - 1.31f * 1.6f, 2f + k * 4f), 0f, 1.6f, $"Tabuas_{k}");
            NightLantern(Model(Kit.Town, "lantern", pier, new Vector3(-1.75f, 1.25f, 1.5f), 0f, 1.5f, "Lanterna_Cais"), range: 8f);
            var deckBox = pier.gameObject.AddComponent<BoxCollider>();
            deckBox.center = new Vector3(0f, 1.05f, 6f);
            deckBox.size = new Vector3(4f, 0.4f, 12f);
            var life = new GameObject("Vida_Contrabando").transform;
            for (int s = -1; s <= 1; s += 2)
            {
                var boat = new GameObject(s < 0 ? "Bote_O" : "Bote_L").transform;
                boat.SetParent(life, false);
                boat.SetPositionAndRotation(pier.position + new Vector3(s * 3.4f, 0f, 7f + s), Quaternion.Euler(0f, 5f * s, 0f));
                Kenney("boat-row-small", boat, Vector3.zero, 0f, 1.4f, "Modelo");
                boat.gameObject.AddComponent<MooredBob>().Configure(0.25f, 4f, 2f, 4.5f + s);
            }
            var sloop = new GameObject("Chalupa_Contrabando").transform;
            sloop.SetParent(life, false);
            sloop.SetPositionAndRotation(pier.position + new Vector3(-12f, 0f, 22f), Quaternion.Euler(0f, 75f, 0f));
            Kenney("ship-small", sloop, new Vector3(0f, ShipModelKeel, 0f), 0f, 1f, "Casco");
            var sloopBox = sloop.gameObject.AddComponent<BoxCollider>();
            sloopBox.center = new Vector3(0f, 0.5f, 0f);
            sloopBox.size = new Vector3(4.4f, 2.4f, 8.6f);
            sloop.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            sloop.gameObject.AddComponent<MooredBob>().Configure(0.15f, 2.5f, 1f, 6f);
            Occluder(sloop.gameObject, moving: true);

            // Contrabando empilhado (cenário; os quebráveis vêm à parte), fogueira e bandeira.
            var rng = new System.Random(31);
            for (int k = 0; k < 10; k++)
            {
                var spot = SmugglersCove + new Vector2(-3.5f + (k % 4) * 1.2f, -3.5f - (k / 4) * 1.3f);
                string model = k % 3 == 0 ? "barrel" : k % 4 == 1 ? "crate-bottles" : "crate";
                Kenney(model, root, root.InverseTransformPoint(Ground(island, spot)), rng.Next(4) * 90f, model == "barrel" ? 0.75f : 1f, $"Contrabando_{k}")
                    .AddComponent<BoxCollider>();
            }
            Campfire(root, Ground(island, SmugglersCove + new Vector2(-1f, 1f)), "Fogueira");
            Kenney("flag-pirate-high", root, root.InverseTransformPoint(Ground(island, SmugglersCove + new Vector2(6f, 1f))), 90f, 1.4f, "Bandeira");
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Physics.SyncTransforms();
            return root;
        }

        /// <summary>Gruta de pedras no lado sul do morro: ferradura de rochedos aberta para sudeste, o X fica no meio.</summary>
        static void Grotto(Transform island, Vector2 local)
        {
            var root = new GameObject("Gruta").transform;
            root.SetParent(island, false);
            root.position = Ground(island, local);
            var rocks = new (string kit, string model, float angle, float dist, float scale, float yaw)[]
            {
                ("P", "rocks-b", 150f, 4.6f, 1.3f, 20f), ("P", "rocks-b", 230f, 4.8f, 1.2f, 140f), ("P", "rocks-c", 290f, 4.4f, 1.4f, 70f),
                ("P", "rocks-c", 100f, 4.8f, 1.3f, 200f), ("N", "rock_tallA", 190f, 5.2f, 4.5f, 0f), ("N", "rock_tallA", 50f, 5f, 3.8f, 90f),
            };
            foreach (var r in rocks)
            {
                float a = r.angle * Mathf.Deg2Rad;
                var p = local + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r.dist;
                var pos = root.InverseTransformPoint(Ground(island, p)) + Vector3.down * 0.4f;
                var rock = r.kit == "P" ? Kenney(r.model, root, pos, r.yaw, r.scale, "Rochedo")
                    : Model(Kit.Nature, r.model, root, pos, r.yaw, r.scale, "Rochedo");
                foreach (var filter in rock.GetComponentsInChildren<MeshFilter>())
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                Occluder(rock);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Forte inimigo na costa sul da Ilha Norte: muralha em anel (castle-wall/window do kit) com portão a leste, torre
        /// no meio, paiol, 4 canhões no alto da muralha do lado do mar (miram no seu navio), bandeira, a fumaça e o fogo de
        /// quando é calado. Muralhas e torre levam as balas (EnemyFort; vida num filho).
        /// </summary>
        static EnemyFort EnemyFortAt(Transform island, ShipController playerShip, LandMaterials m)
        {
            var stats = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_ForteInimigo.asset", c =>
            {
                c.cannonsPerSide = 1;
                c.reloadTime = 8f;
                c.minElevation = -25f;
                c.maxElevation = 15f;
                c.maxRange = 120f;
                c.damagePerBall = 8f;
                c.spreadDegrees = 2.5f;
            });
            var center = Ground(island, NorthFort);
            var root = new GameObject("Forte_Norte");
            root.transform.position = center;
            var g = root.transform;

            int count = Mathf.RoundToInt(2f * Mathf.PI * FortRadius / (2f * FortWallScale));
            for (int k = 0; k < count; k++)
            {
                float degrees = k * 360f / count;
                if (Mathf.Abs(Mathf.DeltaAngle(degrees, 0f)) < 9f) continue;   // portão a leste
                float a = degrees * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * FortRadius;
                var piece = Kenney(k % 4 == 0 ? "castle-window" : "castle-wall", g, pos + Vector3.down * 0.3f, -(degrees + 90f), FortWallScale, $"Muralha_{k}");
                if (k % 4 == 0) PirateWindowGlow(piece, "castle-window", 0.2f + k * 0.01f);
                var box = piece.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.2f, 0f);
                box.size = new Vector3(2.1f, 4.4f, 1.6f);
                Occluder(piece);
            }
            Kenney("castle-gate", g, new Vector3(FortRadius, -0.3f, 0f), -90f, FortWallScale, "Portao");
            const float tower = 1.4f;
            Occluder(Kenney("tower-base-door", g, Vector3.zero, 90f, tower, "Torre_Base"));
            var towerMiddle = Kenney("tower-middle-windows", g, Vector3.up * 2f * tower, 90f, tower, "Torre_Meio");
            PirateWindowGlow(towerMiddle, "tower-middle-windows", 0.15f);
            Occluder(towerMiddle);
            // Lanternas do lado de fora do portão (leste).
            for (int s = -1; s <= 1; s += 2)
                if (GroundAt(center + new Vector3(FortRadius + 1.8f, 0f, s * 2.8f), out var gateGround))
                    PostLantern(g, gateGround, $"Lanterna_Portao_{(s < 0 ? "S" : "N")}", range: 8f);
            Occluder(Kenney("tower-top", g, Vector3.up * 4f * tower, 90f, tower, "Torre_Topo"));
            var towerCollider = root.AddComponent<CapsuleCollider>();
            towerCollider.center = Vector3.up * 3.2f * tower;
            towerCollider.radius = 2.2f;
            towerCollider.height = 6.4f * tower;
            // Paiol no pátio (norte) com barris de pólvora.
            var magazine = Kenney("structure-fence-sides", g, new Vector3(-2f, 0f, 6f), 180f, 1.3f, "Paiol");
            var magBox = magazine.AddComponent<BoxCollider>();
            magBox.center = new Vector3(0f, 1.1f, 0f);
            magBox.size = new Vector3(2.5f, 2.2f, 2.5f);
            Occluder(magazine);
            // Os barris do paiol explodem (pacote A, 09/10): três juntos (cadeia) e um no pátio, perto do bacamarteiro.
            PowderKegsAt(g, new[] { new Vector3(1.2f, 0f, 7f), new Vector3(2.15f, 0f, 7f), new Vector3(3.1f, 0f, 7f), new Vector3(3f, 0f, -3.6f) });

            // Canhões no alto da muralha do lado do mar (sul), virados para fora.
            float gunHeight = 4.4f * FortWallScale + 0.65f;
            var guns = new GameObject("Canhoes").transform;
            guns.SetParent(g, false);
            foreach (float deg in new[] { 220f, 250f, 290f, 320f })
            {
                float a = deg * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (FortRadius - 0.2f) + Vector3.up * gunHeight;
                FortressGun(guns, $"Canhao_{deg:0}", pos, stats, m.iron);
            }
            foreach (var cannon in guns.GetComponentsInChildren<FortressCannon>())
            {
                cannon.TargetPlayer(playerShip.transform);
                // Visual: o canhão do kit no lugar do cano e da base primitivos (o tiro sai pelo "right" da torreta).
                foreach (var r in cannon.GetComponentsInChildren<Renderer>()) Object.DestroyImmediate(r.gameObject);
                Kenney("cannon", cannon.transform, new Vector3(0.2f, -0.65f, 0f), 90f, 1.3f, "Canhao_Modelo");
            }

            var flag = Kenney("flag-pirate-high", g, Vector3.up * (4f * tower + 2.6f), 90f, 1.5f, "Bandeira");
            var ruin = new GameObject("Ruina_FX").transform;
            ruin.SetParent(g, false);
            Smoke(ruin, center + Vector3.up * (4f * tower + 3f), 2.2f);
            Fire(ruin, center + new Vector3(-6f, 0.3f, -6f), 1.4f);
            Fire(ruin, center + new Vector3(5f, gunHeight - 0.4f, -8f), 1f);
            Smoke(ruin, center + new Vector3(5f, gunHeight + 0.5f, -8f), 1.4f);

            var life = new GameObject("Vida_do_Forte");
            life.transform.SetParent(g, false);
            var health = life.AddComponent<Health>();
            health.Configure(FortHealth);
            var fort = root.AddComponent<EnemyFort>();
            fort.Configure("Forte da Ilha Norte", FortMissionId, health, new[] { flag }, new[] { ruin.gameObject });
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t != ruin && !t.IsChildOf(ruin) && !t.IsChildOf(guns)) t.gameObject.isStatic = true;
            Physics.SyncTransforms();
            return fort;
        }

        /// <summary>NavMesh de um volume (uma ilha), salvo ao lado da cena com o sufixo dado.</summary>
        static void BakeNavMeshVolume(string objectName, string suffix, Vector3 center, Vector3 size)
        {
            string assetPath = System.IO.Path.ChangeExtension(generatingScenePath, null) + suffix + ".asset";
            var go = new GameObject(objectName);
            go.transform.position = center;
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.size = size;
            surface.center = new Vector3(0f, size.y * 0.5f - 0.5f, 0f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (!surface.navMeshData)
            {
                Debug.LogWarning($"[Pirates] {objectName}: NavMesh não gerado (os piratas vão andar em linha reta).");
                return;
            }
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        }
    }
}
