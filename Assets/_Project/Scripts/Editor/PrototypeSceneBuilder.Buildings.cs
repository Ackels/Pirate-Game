using System.Collections.Generic;
using Pirates.Ambient;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Parte de construções da base do gerador (Protótipo 05, M3.3): assets (Data/Buildings), BaseBuildings no cais e
    /// os visuais na Ilha Sul (M6.4: casas e peças dos kits do Kenney, nas áreas do layout de PrototypeSceneBuilder.Port.cs),
    /// desligados até cada nível ficar pronto. A Fortaleza fica no alto do morro com dois canhões que giram
    /// (FortressCannon) e um anel de boias marcando a zona segura maior.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string BuildingsFolder = DataFolder + "/Buildings";
        /// <summary>Centro da Ilha Sul (M6.4: 2× maior, centro 28 m mais ao sul; o litoral norte do píer não mudou).</summary>
        static readonly Vector3 IslandCenter = new(40f, 0f, -238f);
        /// <summary>Topo plano do morro da Fortaleza (platô 0,8 + morro 9,8).</summary>
        const float HillTop = 10.6f;
        const float FortressZoneBonus = 25f;

        sealed class BuildingAssets
        {
            public BuildingDefinition lodging, shipyard, forge, magazine, fortress;
        }

        static ItemDefinition LoadItem(string file) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{file}.asset");

        static BuildingAssets LoadBuildingAssets(LootItems items)
        {
            EnsureFolder(DataFolder, "Buildings");
            var g = items.gold;
            var w = items.wood;
            var i = items.iron;
            var p = items.powder;
            return new BuildingAssets
            {
                lodging = BuildingAsset("Building_Alojamento", "Alojamento dos trabalhadores", BuildingEffect.WorkerSpeed, null, null,
                    "Mais trabalhadores morando na base: todas as obras (navio e base) andam mais rápido.",
                    BLvl(1.25f, 25f, (w, 20), (g, 60)),
                    BLvl(1.5f, 50f, (w, 40), (i, 5), (g, 140)),
                    BLvl(2f, 90f, (w, 70), (i, 12), (g, 260))),
                shipyard = BuildingAsset("Building_Estaleiro", "Estaleiro", BuildingEffect.Unlock, LoadItem("Esquema_Estaleiro"), null,
                    "Carreira e guindastes para obras grandes: libera o nível 3 dos upgrades do navio (e, no futuro, navios maiores).",
                    BLvl(0f, 60f, (w, 40), (i, 10), (g, 150))),
                forge = BuildingAsset("Building_Forja", "Forja", BuildingEffect.ProduceItem, LoadItem("Esquema_Forja"), items.iron,
                    "Ferreiros fundem sucata e minério: produz ferro sozinho no armazém.",
                    BLvl(30f, 40f, (w, 25), (i, 5), (g, 80)),
                    BLvl(20f, 60f, (w, 40), (i, 10), (g, 160)),
                    BLvl(12f, 90f, (w, 60), (i, 18), (g, 280))),
                magazine = BuildingAsset("Building_Paiol", "Paiol de Pólvora", BuildingEffect.ProduceItem, LoadItem("Esquema_Paiol"), items.powder,
                    "Moinho de pólvora protegido: produz pólvora sozinho no armazém.",
                    BLvl(30f, 40f, (w, 30), (i, 4), (g, 80)),
                    BLvl(20f, 60f, (w, 45), (i, 8), (g, 160)),
                    BLvl(12f, 90f, (w, 65), (i, 14), (g, 280))),
                fortress = BuildingAsset("Building_Fortaleza", "Fortaleza Costeira", BuildingEffect.CoastalDefense, LoadItem("Esquema_Fortaleza"), null,
                    "Muralha e canhões no alto do morro: atiram nos inimigos perto do porto e aumentam a zona segura.",
                    BLvl(FortressZoneBonus, 90f, (w, 60), (i, 30), (p, 20), (g, 300))),
            };
        }

        static void AddBuildings(GameObject dock, BuildingAssets b, CombatAssets combat, Material deck, Material hullWood)
        {
            // Fora do "isStatic" do porto: a Fortaleza gira os canhões e o moinho gira as pás.
            var root = new GameObject("Construcoes").transform;
            HouseSpec Spec(string name, int w, int d, int floors, bool wood, int seed, bool high = false) =>
                new() { name = name, width = w, depth = d, floors = floors, woodGround = wood, seed = seed, highRoof = high };
            Transform HouseAt(Transform g, HouseSpec spec, Vector2 local, float yaw) => House(g, spec, PortGround(local), yaw);

            var visuals = new List<BaseBuildings.BuildingVisual>
            {
                // Alojamento: casas dos trabalhadores no bairro oeste da vila.
                Visual(b.lodging,
                    Group(root, "Alojamento_N1", g => HouseAt(g, Spec("Casa", 2, 2, 1, true, 41), new Vector2(-47f, 28f), 90f)),
                    Group(root, "Alojamento_N2", g => HouseAt(g, Spec("Casa", 1, 2, 2, true, 42, high: true), new Vector2(-49f, 17f), 90f)),
                    Group(root, "Alojamento_N3", g => HouseAt(g, Spec("Casa", 2, 2, 2, false, 43), new Vector2(-37f, 36f), 180f)),
                    // N4 (M9): acampamento de barracas a oeste das casas, perto da praia (chão livre nas fotos aéreas).
                    Group(root, "Alojamento_N4", g =>
                    {
                        Model(Kit.Survival, "tent-canvas", g, PortGround(new Vector2(-56f, 19.5f)), 80f, SurvivalScale, "Barraca_A");
                        Model(Kit.Survival, "tent-canvas", g, PortGround(new Vector2(-56f, 27f)), 100f, SurvivalScale, "Barraca_B");
                        Model(Kit.Survival, "campfire-pit", g, PortGround(new Vector2(-59f, 23.2f)), 0f, SurvivalScale, "Fogueira");
                        Fire(g, PortGround(new Vector2(-59f, 23.2f)) + Vector3.up * 0.3f, 0.6f);
                    })),
                Visual(b.shipyard, Group(root, "Estaleiro_N1", g => Shipyard(g)),
                    // N2 (M9): mais tábuas e toras ao lado do galpão (obras maiores).
                    Group(root, "Estaleiro_N2", g =>
                    {
                        var p0 = PortGround(ShipyardArea + new Vector2(-2f, -9f));
                        Model(Kit.Survival, "resource-planks", g, p0, 10f, SurvivalScale, "Tabuas_N2_A");
                        Model(Kit.Survival, "resource-planks", g, p0 + new Vector3(1.6f, 0f, 0.4f), -20f, SurvivalScale, "Tabuas_N2_B");
                        Model(Kit.Nature, "log_stackLarge", g, PortGround(ShipyardArea + new Vector2(2.5f, -9.5f)), 90f, NatureScale, "Toras_N2");
                    })),
                Visual(b.forge,
                    Group(root, "Forja_N1", g =>
                    {
                        var forge = HouseAt(g, Spec("Oficina", 2, 2, 1, false, 51), ForgeArea, 45f);
                        var front = forge.TransformPoint(new Vector3(0f, 0f, 4.6f));
                        Model(Kit.Survival, "workbench-anvil", g, front + forge.right * 1.6f, 45f, SurvivalScale, "Bigorna");
                        Model(Kit.Survival, "campfire-pit", g, front - forge.right * 1.4f, 0f, SurvivalScale, "Brasa");
                        Fire(g, front - forge.right * 1.4f + Vector3.up * 0.3f, 0.8f);
                        // M9: o ferreiro faz munições especiais (a porta só existe com a Forja construída).
                        SmithyDoor(g, forge);
                    }),
                    Group(root, "Forja_N2", g =>
                    {
                        var pos = PortGround(ForgeArea + new Vector2(-5.5f, 1f));
                        Model(Kit.Town, "chimney-base", g, pos, 0f, 3f, "Chamine_Base");
                        Model(Kit.Town, "chimney", g, pos + Vector3.up * 3f, 0f, 3f, "Chamine");
                        Model(Kit.Town, "chimney-top", g, pos + Vector3.up * 6f, 0f, 3f, "Chamine_Topo");
                        Smoke(g, pos + new Vector3(-0.8f, 8.2f, 0f), 1.4f);
                    }),
                    Group(root, "Forja_N3", g => HouseAt(g, Spec("Fundicao", 1, 2, 1, false, 52, high: true), ForgeArea + new Vector2(-6f, -7f), 20f)),
                    // N4 (M9): segunda fornalha ao ar livre com bigorna e barris de sucata.
                    Group(root, "Forja_N4", g =>
                    {
                        var c = PortGround(new Vector2(-47f, 8f));
                        Model(Kit.Survival, "campfire-pit", g, c, 0f, SurvivalScale, "Fornalha");
                        Fire(g, c + Vector3.up * 0.3f, 0.9f);
                        Model(Kit.Survival, "workbench-anvil", g, PortGround(new Vector2(-45f, 9.5f)), 30f, SurvivalScale, "Bigorna_N4");
                        Kenney("barrel", g, PortGround(new Vector2(-49f, 9.5f)), 0f, 0.75f, "Sucata_A");
                        Kenney("barrel", g, PortGround(new Vector2(-49.6f, 8.2f)), 40f, 0.75f, "Sucata_B");
                    })),
                Visual(b.magazine,
                    Group(root, "Paiol_N1", g =>
                    {
                        var paiol = Spec("Paiol", 1, 2, 1, false, 61);
                        paiol.lit = false;   // pólvora: nada de vela acesa
                        var magazine = HouseAt(g, paiol, MagazineArea, 30f);
                        for (int k = 0; k < 3; k++)
                            Kenney("barrel", g, magazine.TransformPoint(new Vector3(-2.6f, 0f, -2f + k * 1.5f)), k * 40f, 0.75f, $"Polvora_{k}");
                    }),
                    Group(root, "Paiol_N2", g =>
                    {
                        var depot = Spec("Deposito", 2, 1, 1, false, 62);
                        depot.lit = false;
                        HouseAt(g, depot, MagazineArea + new Vector2(7f, -4f), 30f);
                    }),
                    Group(root, "Paiol_N3", g => Windmill(g, PortGround(MagazineArea + new Vector2(-6f, 6f)))),
                    // N4 (M9): pilha de barris de pólvora e caixotes ao lado do paiol.
                    Group(root, "Paiol_N4", g =>
                    {
                        for (int k = 0; k < 5; k++)
                            Kenney("barrel", g, PortGround(MagazineArea + new Vector2(5.5f + (k % 3) * 1.1f, 4.5f + (k / 3) * 1.2f)), k * 37f, 0.75f, $"Polvora_N4_{k}");
                        Kenney("crate", g, PortGround(MagazineArea + new Vector2(9f, 4.2f)), 15f, 1f, "Caixote_N4");
                    })),
                FortressVisual(root, dock.transform.position, b.fortress, combat),
            };

            var all = new[] { b.lodging, b.shipyard, b.forge, b.magazine, b.fortress };
            dock.AddComponent<BaseBuildings>().Configure(all, visuals.ToArray());
            foreach (var v in visuals)
            {
                foreach (var go in v.perLevel) go.SetActive(false);
                foreach (var go in v.exactLevel) go.SetActive(false);
            }
        }

        /// <summary>
        /// Estaleiro na costa leste: galpão de madeira com portão largo para o mar, carreira que desce na água com um bote
        /// grande em obra, guindaste (mastro do kit), pilhas de tábua e tronco, bancada com martelo.
        /// </summary>
        static void Shipyard(Transform g)
        {
            House(g, new HouseSpec { name = "Galpao", width = 2, depth = 3, cell = 4f, woodGround = true, chimney = false,
                door = "wall-wood-doorway-square-wide", seed = 31 }, PortGround(ShipyardArea), 90f);
            var shore = PortShore(ShipyardArea, Vector2.right);
            var top = PortGround(shore - new Vector2(3f, 0f));
            // Carreira: rampa de tábuas que entra no mar.
            var ramp = new GameObject("Carreira").transform;
            ramp.SetParent(g, false);
            ramp.SetPositionAndRotation(top, Quaternion.Euler(0f, 90f, 0f));
            const float length = 14f, slope = 7f;
            float drop = Mathf.Tan(slope * Mathf.Deg2Rad) * length * 0.5f;
            Part(PrimitiveType.Cube, "Rampa", ramp, new Vector3(0f, -drop, length * 0.5f), new Vector3(5f, 0.3f, length),
                Quaternion.Euler(slope, 0f, 0f), KenneyColors.WoodLight);
            for (int k = 0; k < 2; k++)
                Part(PrimitiveType.Cube, $"Trilho_{k}", ramp, new Vector3(-1.6f + k * 3.2f, 0.18f - drop, length * 0.5f),
                    new Vector3(0.25f, 0.2f, length), Quaternion.Euler(slope, 0f, 0f), KenneyColors.WoodDark);
            Kenney("boat-row-large", ramp, new Vector3(0f, 0.1f - Mathf.Tan(slope * Mathf.Deg2Rad) * 4f, 4f), Quaternion.Euler(slope, 0f, 0f), 2f, "Bote_EmObra");
            Kenney("mast", g, PortGround(shore - new Vector2(4f, 6f)) - g.position, 0f, 1f, "Guindaste");
            Model(Kit.Nature, "log_stackLarge", g, PortGround(ShipyardArea + new Vector2(-2f, -8f)), 0f, NatureScale, "Toras");
            Model(Kit.Survival, "resource-planks", g, PortGround(ShipyardArea + new Vector2(2f, -8.5f)), 10f, SurvivalScale, "Tabuas_A");
            Model(Kit.Survival, "resource-planks", g, PortGround(ShipyardArea + new Vector2(2.2f, -7.6f)) + Vector3.up * 0.3f, -15f, SurvivalScale, "Tabuas_B");
            Model(Kit.Survival, "workbench", g, PortGround(ShipyardArea + new Vector2(6f, 7f)), 90f, SurvivalScale, "Bancada");
            Model(Kit.Survival, "tool-hammer", g, PortGround(ShipyardArea + new Vector2(6f, 7f)) + Vector3.up * 0.95f, 30f, SurvivalScale, "Martelo");
        }

        /// <summary>Moinho de pólvora: torre de pedra do kit pirata com as pás do Fantasy Town Kit girando no alto.</summary>
        static void Windmill(Transform g, Vector3 ground)
        {
            var local = ground - g.position;
            Kenney("tower-base", g, local, 0f, 1.3f, "Torre_Base");
            PirateWindowGlow(Kenney("tower-middle-windows", g, local + Vector3.up * 2.6f, 0f, 1.3f, "Torre_Meio"), "tower-middle-windows", 0.35f);
            Kenney("tower-roof", g, local + Vector3.up * 5.2f, 0f, 1.3f, "Torre_Telhado");
            var hub = new GameObject("Eixo_Pas").transform;
            hub.SetParent(g, false);
            hub.SetPositionAndRotation(ground + new Vector3(0f, 6.2f, 2.4f), Quaternion.identity);
            Model(Kit.Town, "windmill", hub, Vector3.zero, 90f, 3f, "Pas");
            hub.gameObject.AddComponent<Spinner>().Configure(Vector3.forward, 25f);
            var tower = g.gameObject.AddComponent<CapsuleCollider>();
            tower.center = local + Vector3.up * 3.5f;
            tower.radius = 2f;
            tower.height = 7f;
        }

        static GameObject Group(Transform root, string name, System.Action<Transform> build)
        {
            var g = new GameObject(name).transform;
            g.SetParent(root, false);
            build(g);
            return g.gameObject;
        }

        static BaseBuildings.BuildingVisual Visual(BuildingDefinition building, params GameObject[] perLevel) =>
            new() { building = building, perLevel = perLevel };

        /// <summary>
        /// Torre de pedra do kit no alto do morro, muralha em anel com portão ao norte (a trilha da vila chega nele), dois
        /// canhões giratórios no alto da torre, bandeira e o anel de boias da zona segura maior.
        /// </summary>
        /// <summary>
        /// Fortaleza: nível 1 (torre, muralha, 2 canhões), nível 2 (M9: mais 2 canhões em cima da muralha, leste e oeste) e um
        /// anel de boias por nível (só o do nível atual aparece: a zona segura cresce).
        /// </summary>
        static BaseBuildings.BuildingVisual FortressVisual(Transform root, Vector3 dockPosition, BuildingDefinition fortress, CombatAssets combat)
        {
            var levels = new List<GameObject> { Fortress(root, dockPosition, combat) };
            // Depois do nível 1: ele cria o asset na primeira vez.
            var stats = AssetDatabase.LoadAssetAtPath<CannonStats>(DataFolder + "/CannonStats_Fortaleza.asset");
            if (fortress.levels.Length > 1)
                levels.Add(Group(root, "Fortaleza_N2", g =>
                {
                    // Em cima da muralha (4,84 m acima da base dela, que fica 0,3 abaixo do topo do morro): atira por cima dela.
                    float wallTop = HillTop - 0.3f + 4.4f * 1.1f + 0.65f - HillTop;
                    FortressGun(g, "Canhao_Leste", FortressTop + new Vector3(7f, wallTop, 0f), stats, combat.iron);
                    FortressGun(g, "Canhao_Oeste", FortressTop + new Vector3(-7f, wallTop, 0f), stats, combat.iron);
                }));
            var rings = new List<GameObject>();
            for (int i = 0; i < fortress.levels.Length; i++)
                rings.Add(FortressRing(root, $"ZonaSegura_Boias_Fortaleza_N{i + 1}", dockPosition, SafeZoneRadius + fortress.levels[i].value));
            return new BaseBuildings.BuildingVisual { building = fortress, perLevel = levels.ToArray(), exactLevel = rings.ToArray() };
        }

        /// <summary>Anel de boias verdes na borda da zona segura com a Fortaleza (pula as que cairiam em terra).</summary>
        static GameObject FortressRing(Transform root, string name, Vector3 dockPosition, float radius)
        {
            var buoyMat = GetMaterial("M_SafeBuoy", new Color(0.15f, 0.7f, 0.3f), 0.4f);
            var ring = new GameObject(name).transform;
            ring.SetParent(root, false);
            const int count = 36;
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                var pos = dockPosition + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (OverLand(pos)) continue;
                Buoy(ring, $"Boia_Fortaleza_{k}", pos, buoyMat);
            }
            return ring.gameObject;
        }

        static GameObject Fortress(Transform root, Vector3 dockPosition, CombatAssets combat)
        {
            var stats = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_Fortaleza.asset", c =>
            {
                c.cannonsPerSide = 2;
                c.reloadTime = 6f;
                c.minElevation = -25f;
                c.maxElevation = 15f;
                c.maxRange = 140f;
                c.damagePerBall = 15f;
                c.spreadDegrees = 1.5f;
            });

            var top = FortressTop;
            return Group(root, "Fortaleza_N1", g =>
            {
                var local = top - g.position;
                const float tower = 1.6f;
                Occluder(Kenney("tower-base-door", g, local, 0f, tower, "Torre_Base"));
                var middle = Kenney("tower-middle-windows", g, local + Vector3.up * 2f * tower, 0f, tower, "Torre_Meio");
                PirateWindowGlow(middle, "tower-middle-windows", 0.2f);
                Occluder(middle);
                Occluder(Kenney("tower-top", g, local + Vector3.up * 4f * tower, 0f, tower, "Torre_Topo"));
                var towerCollider = g.gameObject.AddComponent<CapsuleCollider>();
                towerCollider.center = local + Vector3.up * 3.2f;
                towerCollider.radius = 2.5f;
                towerCollider.height = 6.4f;
                Kenney("flag-pirate-high", g, local + new Vector3(1.8f, 4f * tower + 0.4f, -1.8f), 90f, 1.4f, "Bandeira");
                FortressWall(g, local);
                // Piso do topo da torre: base 2 + meio 2 + 2,02 do tower-top (× 1,6); a base do canhão fica 0,65 abaixo do eixo.
                float gunHeight = (4f + 2.02f) * tower + 0.65f;
                FortressGun(g, "Canhao_Norte", top + new Vector3(0f, gunHeight, 1.4f), stats, combat.iron);
                FortressGun(g, "Canhao_Sul", top + new Vector3(0f, gunHeight, -1.4f), stats, combat.iron);
            });
        }

        /// <summary>Muralha em anel (r 7 m) de peças castle-wall/castle-window do kit, portão (castle-gate) virado para o norte.</summary>
        static void FortressWall(Transform g, Vector3 center)
        {
            const float radius = 7f, scale = 1.1f;
            const int count = 20;
            for (int k = 0; k < count; k++)
            {
                float degrees = k * 360f / count;
                if (Mathf.Abs(Mathf.DeltaAngle(degrees, 90f)) < 10f) continue;
                float a = degrees * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                // Giro que põe o comprimento da peça (X) na tangente do anel.
                var piece = Kenney(k % 3 == 0 ? "castle-window" : "castle-wall", g, pos + Vector3.down * 0.3f, -(degrees + 90f), scale, $"Muralha_{k}");
                if (k % 3 == 0) PirateWindowGlow(piece, "castle-window", 0.2f + k * 0.015f);
                var box = piece.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.2f, 0f);
                box.size = new Vector3(2.1f, 4.4f, 1.6f);
            }
            Kenney("castle-gate", g, center + new Vector3(0f, -0.3f, radius), 180f, scale, "Portao");
            // Lanternas dos dois lados do portão, do lado de fora (a trilha da vila chega aqui).
            for (int s = -1; s <= 1; s += 2)
                if (GroundAt(g.TransformPoint(center + new Vector3(s * 2.6f, 0f, radius + 1.6f)), out var ground))
                    PostLantern(g, ground, $"Lanterna_Portao_{(s < 0 ? "O" : "L")}", range: 8f);
        }

        static void FortressGun(Transform parent, string name, Vector3 position, CannonStats stats, Material iron)
        {
            var turret = new GameObject(name).transform;
            turret.SetParent(parent, false);
            turret.position = position;

            var batteryGo = new GameObject("Bateria");
            batteryGo.transform.SetParent(turret, false);
            int count = Mathf.Max(1, stats.cannonsPerSide);
            var muzzles = new Transform[count];
            for (int k = 0; k < count; k++)
            {
                float z = count == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, k / (float)(count - 1));
                var barrel = Part(PrimitiveType.Cylinder, $"Cano_{k}", turret, new Vector3(0.8f, 0f, z), new Vector3(0.35f, 0.9f, 0.35f), Quaternion.Euler(0f, 0f, 90f), iron);
                barrel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                var muzzle = new GameObject($"Muzzle_{k}").transform;
                muzzle.SetParent(batteryGo.transform, false);
                muzzle.localPosition = new Vector3(1.8f, 0f, z);
                muzzles[k] = muzzle;
            }
            Part(PrimitiveType.Cube, "Base", turret, Vector3.down * 0.4f, new Vector3(1.6f, 0.5f, 1.8f), Quaternion.identity, iron);

            var battery = batteryGo.AddComponent<CannonBattery>();
            battery.Configure(stats, BroadsideSide.Starboard, muzzles, null, null, null);
            // Atira a até 10 m do alcance do canhão (a bala chega lá com folga).
            turret.gameObject.AddComponent<FortressCannon>().Configure(battery, stats.maxRange - 10f);
        }

        static BuildingDefinition BuildingAsset(string file, string name, BuildingEffect effect, ItemDefinition schematic,
            ItemDefinition produced, string description, params BuildingDefinition.Level[] levels) =>
            LoadOrCreateAsset<BuildingDefinition>($"{BuildingsFolder}/{file}.asset", b =>
            {
                b.displayName = name;
                b.effect = effect;
                b.schematic = schematic;
                b.producedItem = produced;
                b.description = description;
                b.levels = levels;
            });

        static BuildingDefinition.Level BLvl(float value, float buildSeconds, params (ItemDefinition item, int amount)[] cost)
        {
            var stacks = new ItemStack[cost.Length];
            for (int k = 0; k < cost.Length; k++) stacks[k] = new ItemStack(cost[k].item, cost[k].amount);
            return new BuildingDefinition.Level { value = value, buildSeconds = buildSeconds, cost = stacks };
        }
    }
}
