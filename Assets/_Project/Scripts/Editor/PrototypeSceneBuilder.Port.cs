using System;
using System.Collections.Generic;
using System.Linq;
using Pirates.Ambient;
using Pirates.Base;
using Pirates.OnFoot;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ilha Sul (porto) do M6.4: 2× maior, com o centro mais ao sul (o litoral norte, o píer e o cais ficam onde estavam).
    /// Layout em coordenadas locais (X leste, Z norte, a partir de <see cref="IslandCenter"/>): frente do porto ao norte
    /// (píer, cais lateral com botes, armazém, taverna), praça do mercado, ruas com casas, morro da Fortaleza no meio,
    /// estaleiro na costa leste, forja e paiol a oeste/sudoeste, roça ao sul; mata nos morros e palmeiras nas praias.
    /// A vegetação entra em todas as cenas com a ilha (CreateIslands); a vila, as construções e a vida (moradores,
    /// gaivotas, caranguejos, barcos ancorados, fumaça e fogo) só nas cenas do porto (05 e 07).
    /// Casas de serviço (M6.6, a pé na cena 07): armazém (carga), estaleiro (upgrades do navio e conserto), mestre de obras
    /// (construções da base) e taverna (missões), cada uma com placa, atendente na porta e a entrada (HarborBuilding).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Escala dos props do Nature e do Survival Kit (no kit, 1 unidade ≈ 2,5–3 m).</summary>
        const float NatureScale = 2.8f, SurvivalScale = 3.2f;

        static readonly Vector2 FortressHill = new(0f, -10f);
        static Vector3 FortressTop => IslandCenter + new Vector3(FortressHill.x, HillTop, FortressHill.y);

        static Vector2 PlazaCenter => new(0f, 33f);
        static Vector2 ForgeArea => new(-38f, 2f);
        static Vector2 MagazineArea => new(-24f, -36f);
        static Vector2 ShipyardArea => new(48f, 28f);
        static Vector2 FarmArea => new(23f, -43f);
        /// <summary>Cais lateral (a oeste do píer): botes, desembarque a pé na cena 07.</summary>
        const float SidePierX = -38f;

        /// <summary>Trilha da rua principal até o topo da Fortaleza.</summary>
        static readonly Vector2[] FortressTrail = { new(0f, 22f), new(-8f, 13f), new(-6f, 3f), new(0f, -3f) };

        /// <summary>Ilha do porto atual (para os raios do chão não pegarem casas e árvores).</summary>
        static Transform portIsland;

        static IslandSpec PortIslandSpec()
        {
            var paths = new List<(Vector2, Vector2, float)>
            {
                (new Vector2(0f, 54f), new Vector2(0f, 22f), 5f),               // píer → praça → pé do morro
                (PlazaCenter, PlazaCenter, 19f),                                // praça
                (new Vector2(-50f, 25f), new Vector2(50f, 25f), 4.5f),          // rua de baixo
                (new Vector2(-30f, 50f), new Vector2(34f, 50f), 6f),            // frente do porto (cais)
                (new Vector2(24f, 25f), ShipyardArea + new Vector2(-6f, 0f), 4f),
                (new Vector2(-30f, 25f), ForgeArea + new Vector2(4f, 4f), 4f),
                (ForgeArea, MagazineArea + new Vector2(4f, 6f), 3f),
                (new Vector2(8f, -26f), FarmArea + new Vector2(-8f, 0f), 3f),
                (new Vector2(SidePierX, 50f), new Vector2(SidePierX + 8f, 50f), 5f),
            };
            for (int k = 1; k < FortressTrail.Length; k++) paths.Add((FortressTrail[k - 1], FortressTrail[k], 3f));
            return new IslandSpec
            {
                name = "Ilha_Sul", center = IslandCenter, radiusX = 66f, radiusZ = 56f, coastNoise = 0.1f,
                // Platô a 1,4 m: as cristas das ondas (até ~1,1 m somando as quatro) não aparecem no chão plano da vila.
                coastOutwardOnly = true, seed = 5, plateau = 1.4f, beachWidth = 5f, inlandNoise = 0f, cell = 1.6f,
                // Litoral norte fixo: o píer começa em z ≈ -184 (local 54), como antes.
                anchors = new[] { (90f, PierShore.z - IslandCenter.z + 3.5f, 35f) },
                hills = new[]
                {
                    (FortressHill, 30f, 9.8f), (new Vector2(46f, -20f), 20f, 6f),
                    (new Vector2(-46f, -30f), 16f, 4.5f), (new Vector2(-14f, -48f), 12f, 3f),
                },
                flats = new[] { (FortressHill, 8f, 4f, HillTop) },
                paths = paths.ToArray(),
            };
        }

        /// <summary>Onde não nasce mata (local XZ): vila e porto, áreas das construções, roça, topo e trilha da Fortaleza.</summary>
        static bool PortWild(Vector2 p)
        {
            if (p.x > -58f && p.x < 60f && p.y > 6f) return false;
            if ((p - ForgeArea).magnitude < 13f || (p - MagazineArea).magnitude < 11f) return false;
            if (Mathf.Abs(p.x - FarmArea.x) < 14f && Mathf.Abs(p.y - FarmArea.y) < 10f) return false;
            if ((p - FortressHill).magnitude < 10.5f) return false;
            for (int k = 1; k < FortressTrail.Length; k++)
                if (DistanceToSegment(p, FortressTrail[k - 1], FortressTrail[k]) < 3f) return false;
            return true;
        }

        // ---------- Vegetação (todas as cenas) ----------

        // Árvores, palmeiras, arbustos, capim e pedras: listas em PrototypeSceneBuilder.Vegetation.cs (28/10).
        static readonly string[] Flowers =
        {
            "flower_purpleA", "flower_purpleB", "flower_purpleC", "flower_redA", "flower_redB", "flower_redC",
            "flower_yellowA", "flower_yellowB", "flower_yellowC",
        };
        static readonly string[] GroundProps = { "log", "log_large", "stump_old", "stump_round", "stump_oldTall", "mushroom_redGroup", "mushroom_tanGroup" };

        /// <summary>
        /// Mata cheia: árvores das ruínas do Quaternius (colisor no tronco; secas perto de <see cref="DeadTreeZone"/>), arbustos e capim das
        /// ruínas e do kit pirata, flores e troncos/tocos do Nature Kit, pedras do Quaternius e do Medieval, palmeiras do Quaternius na praia.
        /// Só onde <paramref name="allowed"/> deixa. Tudo estático. Cada objeto gasta os mesmos sorteios de antes da troca (28/10): as posições
        /// não mudaram (garrafas, moedas, gaivotas e tesouros foram postos onde a mata deixava).
        /// </summary>
        static void PlantLush(Transform island, IslandSpec s, Func<Vector2, bool> allowed, int trees, int bushes, int flowers,
            int grass, int ground, int rocks, int palms, int seed)
        {
            var rng = new Random(seed);
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            string Pick(string[] list) => list[rng.Next(list.Length)];
            var root = new GameObject("Mata").transform;
            root.SetParent(island, false);
            float maxR = Mathf.Max(s.radiusX, s.radiusZ) * 1.12f;

            bool Spot(float minHeight, float maxHeight, float maxSlope, out Vector3 local)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    var p = new Vector2(R(-maxR, maxR), R(-maxR, maxR));
                    if (!allowed(p)) continue;
                    if (!IslandGround(island, island.TransformPoint(new Vector3(p.x, 0f, p.y)), out var hit, out float slope)) continue;
                    float h = hit.y - island.position.y;
                    if (h < minHeight || h > maxHeight || slope > maxSlope) continue;
                    local = island.InverseTransformPoint(hit);
                    return true;
                }
                local = default;
                return false;
            }

            float Size(Veg v, float roll) => Mathf.Lerp(v.minScale, v.maxScale, roll);

            float inland = s.plateau + 0.05f;
            for (int k = 0; k < trees; k++)
            {
                if (!Spot(inland, 99f, 28f, out var local)) continue;
                float size = R(0f, 1f);
                double roll = rng.NextDouble();
                var v = DeadTreeZone != null && DeadTreeZone(new Vector2(local.x, local.z))
                    ? roll < 0.75 ? PickVeg(DeadTrees, roll / 0.75) : PickVeg(ForestTrees, (roll - 0.75) / 0.25)
                    : PickVeg(ForestTrees, roll);
                float scale = Size(v, size);
                var tree = PlaceVeg(v, root, local + Vector3.down * 0.1f, R(0f, 360f), scale, $"Arvore_{k}");
                Occluder(tree);
                // D1 (28/10): toda árvore da mata se corta com a machadinha.
                Choppable(tree, v.trunkRadius * scale, palm: false);
            }
            for (int k = 0; k < palms; k++)
            {
                // Palmeiras na areia, perto do mar.
                if (!Spot(0.25f, s.plateau + 0.02f, 22f, out var local)) continue;
                float size = R(0f, 1f);
                var v = PickVeg(PalmModels, rng.NextDouble());
                float palmScale = Size(v, size);
                var palm = PlaceVeg(v, root, local, R(0f, 360f), palmScale, $"Palmeira_{k}");
                Occluder(palm);
                Choppable(palm, v.trunkRadius * palmScale, palm: true);
            }
            for (int k = 0; k < bushes; k++)
                if (Spot(inland, 99f, 32f, out var local))
                {
                    var v = PickVeg(Bushes, rng.NextDouble());
                    PlaceVeg(v, root, local, R(0f, 360f), Size(v, R(0f, 1f)), $"Arbusto_{k}");
                }
            for (int k = 0; k < flowers; k++)
                if (Spot(inland, 99f, 30f, out var local))
                    Model(Kit.Nature, Pick(Flowers), root, local, R(0f, 360f), R(2.6f, 3.6f), $"Flor_{k}");
            for (int k = 0; k < grass; k++)
                if (Spot(s.plateau * 0.6f, 99f, 32f, out var local))
                {
                    var v = PickVeg(Grasses, rng.NextDouble());
                    PlaceVeg(v, root, local, R(0f, 360f), Size(v, R(0f, 1f)), $"Capim_{k}");
                }
            for (int k = 0; k < ground; k++)
                if (Spot(inland, 99f, 25f, out var local))
                    Model(Kit.Nature, Pick(GroundProps), root, local, R(0f, 360f), R(2.4f, 3.2f), $"Chao_{k}");
            for (int k = 0; k < rocks; k++)
            {
                if (!Spot(0.3f, 99f, 35f, out var local)) continue;
                var v = PickVeg(BigRocks, rng.NextDouble());
                var rock = PlaceVeg(v, root, local + Vector3.down * 0.2f, R(0f, 360f), Size(v, R(0f, 1f)), $"Pedra_{k}");
                RockColliders(rock);
                Occluder(rock);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Physics.SyncTransforms();
        }

        // ---------- Vila (cenas do porto) ----------

        /// <summary>Ponto do chão da ilha do porto (local XZ → mundo), sem pegar casas nem árvores.</summary>
        static Vector3 PortGround(Vector2 local)
        {
            var world = IslandCenter + new Vector3(local.x, 0f, local.y);
            if (portIsland && IslandGround(portIsland, world, out var hit, out _)) return hit;
            return world + Vector3.up * 0.8f;
        }

        /// <summary>Último ponto em terra (chão ≥ 0,3 m) andando de <paramref name="from"/> na direção dada (local XZ).</summary>
        static Vector2 PortShore(Vector2 from, Vector2 direction)
        {
            var p = from;
            for (int k = 0; k < 200; k++)
            {
                var next = p + direction.normalized * 0.5f;
                if (PortGround(next).y < 0.3f) return p;
                p = next;
            }
            return p;
        }

        static void AddVillage(Transform root)
        {
            portIsland = GameObject.Find("Ilha_Sul")?.transform;
            var village = new GameObject("Vila").transform;
            village.SetParent(root, false);
            var smoke = new List<Vector3>();

            Transform Place(HouseSpec spec, Vector2 local, float yaw)
            {
                var house = House(village, spec, PortGround(local), yaw);
                var chimney = house.Find("Chamine");
                if (chimney && spec.seed % 2 == 0) smoke.Add(chimney.GetComponentInChildren<Renderer>().bounds.center + Vector3.up * 1.2f);
                return house;
            }

            // Frente do porto: armazém (portão largo) e taverna (dois andares, telhado alto vermelho).
            serviceDoors.Clear();
            var warehouse = Place(new HouseSpec { name = "Armazem", width = 2, depth = 3, cell = 4f, door = "wall-doorway-square-wide", seed = 2 }, new Vector2(17f, 41f), 0f);
            var tavern = Place(new HouseSpec { name = "Taverna", width = 2, depth = 3, floors = 2, cell = 3.5f, highRoof = true, banner = "banner-red", seed = 4 }, new Vector2(-17f, 41f), 0f);
            TavernFront(village, tavern);
            WarehouseYard(village);
            ServiceDoor(village, warehouse, "Armazém", "carga do porão", HarborService.Cargo);
            ServiceDoor(village, tavern, "Taverna", "marinheiros e missões", HarborService.Missions);

            // Estaleiro (upgrades do navio e conserto) e mestre de obras (construções). As quatro casas de serviço têm a porta
            // para o norte (o porto): ao descer no píer a câmera olha para a vila e vê as fachadas.
            var shipyard = Place(new HouseSpec { name = "Estaleiro_Oficina", width = 2, depth = 2, woodGround = true, banner = "banner-green", seed = 6 },
                new Vector2(37.5f, 34.5f), 0f);
            ShipyardFront(village, shipyard);
            ServiceDoor(village, shipyard, "Estaleiro", "upgrades do navio e conserto", HarborService.Shipyard);
            var builder = Place(new HouseSpec { name = "Mestre_de_Obras", width = 2, depth = 2, banner = "banner-red", seed = 8 }, new Vector2(-26f, 33f), 0f);
            BuilderFront(village, builder);
            ServiceDoor(village, builder, "Mestre de obras", "construções da base", HarborService.Construction);

            // Casas nas duas ruas (frente para a rua de baixo, z = 25).
            var houses = new (Vector2 pos, float yaw, int w, int d, int floors, bool wood, bool high)[]
            {
                (new(27f, 33f), 180f, 2, 2, 2, false, true),
                (new(-12f, 17f), 0f, 2, 2, 2, false, true), (new(-23f, 17f), 0f, 1, 2, 1, true, false),
                (new(-33f, 16f), 0f, 2, 2, 1, false, false), (new(12f, 17f), 0f, 2, 2, 1, true, false),
                (new(23f, 17f), 0f, 2, 2, 2, false, false), (new(33f, 16f), 0f, 1, 2, 1, false, true),
                (new(30f, -48f), 0f, 2, 2, 1, true, false),                                     // casa da roça
            };
            for (int k = 0; k < houses.Length; k++)
            {
                var h = houses[k];
                Place(new HouseSpec { name = $"Casa_{k}", width = h.w, depth = h.d, floors = h.floors, woodGround = h.wood, highRoof = h.high, seed = 10 + k },
                    h.pos, h.yaw);
            }

            Plaza(village);
            Farm(village);
            var watchtower = Kenney("tower-complete-small", village, PortGround(new Vector2(36f, 49f)) - village.position, 200f, 1.3f, "TorreDeVigia");
            watchtower.AddComponent<CapsuleCollider>().radius = 1.6f;
            Occluder(watchtower);
            StreetLife(village);

            // Fora do PortoBase (que fica estático): barcos, gaivotas, moradores e fogo se mexem.
            var life = new GameObject("VidaDoPorto").transform;
            foreach (var p in smoke) Smoke(life, p, 1f);
            SidePier(root, life);
            MooredShips(life);
            PortTrainingYard(life);
            // NavMesh depois da vila (casas, barracas, árvores viram obstáculo) e antes dos moradores.
            BakePortNavMesh();
            Villagers(life);
            Seagulls(life);
            Crabs(life);
            Campfire(life, PortGround(new Vector2(SidePierX + 10f, 46f)), "Fogueira_Praia");

            foreach (var t in village.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        /// <summary>Entradas das casas de serviço (chão na frente da porta e o giro da fachada): os atendentes ficam ao lado.</summary>
        static readonly List<(Vector3 ground, float yaw)> serviceDoors = new();

        /// <summary>Entrada de uma casa de serviço: HarborBuilding no chão em frente à porta e uma placa ao lado.</summary>
        static void ServiceDoor(Transform village, Transform house, string displayName, string purpose, HarborService service)
        {
            var door = house.Find("Porta");
            var front = door.position + house.forward * 1.6f;
            var ground = PortGround(new Vector2(front.x - IslandCenter.x, front.z - IslandCenter.z));
            var entrance = new GameObject("Entrada_" + house.name);
            entrance.transform.SetParent(village, false);
            entrance.transform.SetPositionAndRotation(ground, house.rotation);
            entrance.AddComponent<HarborBuilding>().Configure(displayName, purpose, service, 3f, 3.2f);
            var sign = ground - house.right * 2.4f + house.forward * 0.4f;
            Model(Kit.Survival, "signpost-single", village, PortGround(new Vector2(sign.x - IslandCenter.x, sign.z - IslandCenter.z)) - village.position,
                house.eulerAngles.y + 90f, SurvivalScale, "Placa_" + house.name);
            serviceDoors.Add((ground + house.right * 1.9f - house.forward * 0.6f, house.eulerAngles.y));
        }

        /// <summary>Estaleiro: bote em conserto com remos, tábuas e a bancada de carpinteiro ao lado.</summary>
        static void ShipyardFront(Transform village, Transform shop)
        {
            Vector3 At(float right, float forward)
            {
                var p = shop.position + shop.right * right + shop.forward * forward;
                return PortGround(new Vector2(p.x - IslandCenter.x, p.z - IslandCenter.z)) - village.position;
            }
            float yaw = shop.eulerAngles.y;
            Kenney("boat-row-small", village, At(5.6f, 1f), yaw + 80f, 1.4f, "Bote_EmConserto").AddComponent<BoxCollider>();
            Kenney("tool-paddle", village, At(4.4f, 3.2f), yaw + 20f, 1.4f, "Remo");
            Model(Kit.Survival, "resource-planks", village, At(-4.6f, 1.2f), yaw + 10f, SurvivalScale, "Tabuas_A");
            Model(Kit.Survival, "resource-planks", village, At(-4.8f, -0.6f), yaw - 15f, SurvivalScale, "Tabuas_B");
            Model(Kit.Survival, "workbench", village, At(-4.4f, -2.6f), yaw + 90f, SurvivalScale, "Bancada").AddComponent<BoxCollider>();
            Model(Kit.Survival, "tool-hammer", village, At(-3.6f, 2.6f), yaw + 40f, SurvivalScale, "Martelo");
        }

        /// <summary>Mestre de obras: pedra, madeira e ferramentas na frente, e a bancada com bigorna.</summary>
        static void BuilderFront(Transform village, Transform shop)
        {
            Vector3 At(float right, float forward)
            {
                var p = shop.position + shop.right * right + shop.forward * forward;
                return PortGround(new Vector2(p.x - IslandCenter.x, p.z - IslandCenter.z)) - village.position;
            }
            float yaw = shop.eulerAngles.y;
            Model(Kit.Survival, "resource-stone-large", village, At(4.8f, 1.5f), yaw, SurvivalScale, "Pedras").AddComponent<BoxCollider>();
            Model(Kit.Survival, "resource-wood", village, At(4.6f, -0.8f), yaw + 30f, SurvivalScale, "Lenha");
            Model(Kit.Survival, "workbench-anvil", village, At(-4.6f, 0.4f), yaw + 90f, SurvivalScale, "Bigorna").AddComponent<BoxCollider>();
            Model(Kit.Survival, "tool-pickaxe", village, At(-3.8f, 2.4f), yaw + 60f, SurvivalScale, "Picareta");
            Model(Kit.Survival, "tool-shovel", village, At(4f, 3f), yaw - 20f, SurvivalScale, "Pa");
            Model(Kit.Town, "cart", village, At(-5f, -3f), yaw + 100f, 2.6f, "Carrinho_Obras");
        }

        static void TavernFront(Transform village, Transform tavern)
        {
            var front = tavern.TransformPoint(new Vector3(0f, 0f, 3 * 3.5f * 0.5f + 1.6f));
            for (int s = -1; s <= 1; s += 2)
            {
                NightLantern(Model(Kit.Town, "lantern", village, tavern.TransformPoint(new Vector3(s * 2.6f, 0f, 3 * 3.5f * 0.5f + 0.6f)), 0f, 1.6f, "Lanterna"));
                Model(Kit.Town, "stall-bench", village, front + tavern.right * (s * 3.6f), tavern.eulerAngles.y + 90f, 3f, "Banco");
            }
            Model(Kit.Survival, "barrel", village, front + tavern.right * 5.5f, 20f, SurvivalScale, "Barril_A");
            Model(Kit.Survival, "barrel-open", village, front + tavern.right * 6.3f + tavern.forward * 0.8f, 0f, SurvivalScale, "Barril_B");
            Model(Kit.Survival, "bottle-large", village, front + tavern.right * 6.3f + tavern.forward * 0.8f + Vector3.up * 1.05f, 0f, SurvivalScale, "Garrafa");
        }

        static void WarehouseYard(Transform village)
        {
            var rng = new Random(77);
            for (int k = 0; k < 9; k++)
            {
                var local = new Vector2(23.5f + (k % 3) * 1.6f, 44f + (k / 3) * 1.8f);
                var ground = PortGround(local);
                string model = k % 4 == 0 ? "crate-bottles" : k % 3 == 0 ? "barrel" : "crate";
                var go = Kenney(model, village, ground - village.position, rng.Next(4) * 90f + rng.Next(-8, 8), model == "barrel" ? 0.75f : 1f, $"Carga_{k}");
                go.AddComponent<BoxCollider>();
            }
            Model(Kit.Town, "cart-high", village, PortGround(new Vector2(11f, 46f)), 30f, 2.6f, "Carroca");
        }

        /// <summary>Praça do mercado: chafariz, barracas com mercadoria, lanternas, banco e caixotes.</summary>
        static void Plaza(Transform village)
        {
            var c = PortGround(PlazaCenter);
            Model(Kit.Town, "fountain-round-detail", village, c, 0f, 2.6f, "Chafariz").AddComponent<BoxCollider>();
            var stalls = new (Vector2 offset, float yaw, string canopy, string ware)[]
            {
                (new(-7.5f, 1f), 90f, "stall-red", "fish-large"), (new(7.5f, 1f), -90f, "stall-green", "box-open"),
                (new(-5f, -6.5f), 30f, "stall-green", "bucket"), (new(5.5f, -6.5f), -30f, "stall-red", "fish"),
            };
            for (int si = 0; si < stalls.Length; si++)
            {
                var s = stalls[si];
                var pos = PortGround(PlazaCenter + s.offset);
                var stall = new GameObject("Barraca").transform;
                stall.SetParent(village, false);
                stall.SetPositionAndRotation(pos, Quaternion.Euler(0f, s.yaw, 0f));
                Model(Kit.Town, "stall", stall, Vector3.zero, 0f, 3f, "Mesa");
                Model(Kit.Town, s.canopy, stall, Vector3.zero, 0f, 3f, "Toldo");
                for (int k = -1; k <= 1; k++)
                    Model(Kit.Survival, s.ware, stall, new Vector3(0.1f, 1.12f, k * 0.8f), 90f + k * 15f, SurvivalScale, "Mercadoria");
                stall.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f, 3.6f, 3f);
                stall.GetComponent<BoxCollider>().center = new Vector3(0f, 1.8f, 0f);
                // M9: a banca verde do leste é o mercado (comprar e vender).
                if (si == 1) MarketStall(village, stall);
            }
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + k * 90f) * Mathf.Deg2Rad;
                NightLantern(Model(Kit.Town, "lantern", village, PortGround(PlazaCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9.5f), 0f, 1.6f, $"Lanterna_Praca_{k}"));
            }
            Model(Kit.Survival, "box-large", village, PortGround(PlazaCenter + new Vector2(-9f, 6f)), 15f, SurvivalScale, "Caixa_A");
            Model(Kit.Survival, "box", village, PortGround(PlazaCenter + new Vector2(-8.2f, 7f)), 50f, SurvivalScale, "Caixa_B");
            Model(Kit.Town, "cart", village, PortGround(PlazaCenter + new Vector2(9f, 6.5f)), -60f, 2.6f, "Carrinho");
            foreach (var (model, offset, yaw, scale, name) in new[] { ("tree_oak", new Vector2(-10f, -9f), 0f, 4.2f, "Arvore_Praca_A"), ("tree_default", new Vector2(10.5f, -9f), 70f, 4f, "Arvore_Praca_B") })
            {
                var tree = Model(Kit.Nature, model, village, PortGround(PlazaCenter + offset), yaw, scale, name);
                tree.AddComponent<CapsuleCollider>().radius = 0.1f;
                Occluder(tree);
            }
        }

        /// <summary>Roça ao sul: canteiros com milho, abóbora e trigo, cerca em volta e um espantalho de caixotes.</summary>
        static void Farm(Transform village)
        {
            var farm = new GameObject("Roca").transform;
            farm.SetParent(village, false);
            string[] crops = { "crops_cornStageD", "crop_pumpkin", "crops_wheatStageB", "crop_carrot", "crops_cornStageC", "crop_melon" };
            for (int row = 0; row < 6; row++)
            for (int k = 0; k < 9; k++)
            {
                var local = FarmArea + new Vector2(-11f + k * 2.6f, -6f + row * 2.4f);
                var ground = PortGround(local);
                Model(Kit.Nature, "crops_dirtRow", farm, ground + Vector3.down * 0.05f, 0f, NatureScale, "Canteiro");
                Model(Kit.Nature, crops[row], farm, ground + Vector3.up * 0.1f, (row * 37 + k * 53) % 360, NatureScale, "Planta");
            }
            // Cerca (peças de 1 m do kit × escala) nos quatro lados, com porteira ao norte.
            float halfX = 13.5f, halfZ = 8.5f, step = NatureScale;
            for (float x = -halfX; x < halfX; x += step)
            {
                Fence(farm, FarmArea + new Vector2(x + step * 0.5f, -halfZ), 180f, false);
                Fence(farm, FarmArea + new Vector2(x + step * 0.5f, halfZ), 0f, Mathf.Abs(x + step * 0.5f) < step * 0.6f);
            }
            for (float z = -halfZ; z < halfZ; z += step)
            {
                Fence(farm, FarmArea + new Vector2(-halfX, z + step * 0.5f), -90f, false);
                Fence(farm, FarmArea + new Vector2(halfX, z + step * 0.5f), 90f, false);
            }
            Model(Kit.Survival, "signpost", farm, PortGround(FarmArea + new Vector2(2.5f, halfZ + 1.5f)), 0f, SurvivalScale, "Placa");
        }

        static void Fence(Transform parent, Vector2 local, float yaw, bool gate) =>
            // O kit põe a cerca na borda +Z da célula: recua meia célula para ela ficar na linha.
            Model(Kit.Nature, gate ? "fence_gate" : "fence_simple", parent,
                PortGround(local) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -0.47f * NatureScale), yaw, NatureScale, "Cerca");

        /// <summary>Canteiros, barris, lenha e flores espalhados pelas ruas.</summary>
        static void StreetLife(Transform village)
        {
            var rng = new Random(91);
            var spots = new[]
            {
                new Vector2(-20f, 29f), new Vector2(20f, 29f), new Vector2(-30f, 21f), new Vector2(31f, 21f), new Vector2(-6f, 21f),
                new Vector2(6f, 21f), new Vector2(-40f, 29f), new Vector2(44f, 22f), new Vector2(-8f, 46f), new Vector2(8f, 46f),
            };
            foreach (var p in spots)
            {
                for (int k = 0; k < 3; k++)
                {
                    var offset = new Vector2((float)rng.NextDouble() * 3f - 1.5f, (float)rng.NextDouble() * 3f - 1.5f);
                    Model(Kit.Nature, Flowers[rng.Next(Flowers.Length)], village, PortGround(p + offset), rng.Next(360), 3.2f, "Flor");
                }
                var bush = Bushes[rng.Next(Bushes.Length)];
                PlaceVeg(bush, village, PortGround(p + new Vector2(1.8f, 0.5f)), rng.Next(360), (bush.minScale + bush.maxScale) * 0.5f, "Arbusto");
            }
            Model(Kit.Nature, "log_stackLarge", village, PortGround(new Vector2(-37f, 22f)), 90f, NatureScale, "Lenha_A");
            Model(Kit.Nature, "log_stack", village, PortGround(new Vector2(18f, 22f)), 0f, NatureScale, "Lenha_B");
            Model(Kit.Nature, "pot_large", village, PortGround(new Vector2(-16f, 22f)), 0f, NatureScale, "Vaso");
            Model(Kit.Survival, "barrel", village, PortGround(new Vector2(28f, 22f)), 0f, SurvivalScale, "Barril_Rua");
        }

        // ---------- Cais lateral, barcos e vida ----------

        /// <summary>Cais menor a oeste do píer: tábuas do kit sobre estacas, dois botes amarrados e caixotes.</summary>
        static void SidePier(Transform root, Transform life)
        {
            var shore = PortShore(new Vector2(SidePierX, 40f), Vector2.up);
            var start = IslandCenter + new Vector3(SidePierX, 0f, shore.y - 2.5f);
            var pier = new GameObject("Cais_Lateral").transform;
            pier.SetParent(root, false);
            pier.position = start;
            const float length = 16f;
            const float scale = 1.6f;   // peças de 2,5 m → 4 m
            for (int k = 0; k < 4; k++)
                Kenney("structure-platform-dock", pier, new Vector3(0f, 1.25f - 1.31f * scale, 2f + k * 4f), 0f, scale, $"Tabuas_{k}");
            var collider = pier.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.05f, length * 0.5f);
            collider.size = new Vector3(4f, 0.4f, length);
            Kenney("crate", pier, new Vector3(-1.2f, 1.25f, 12f), 15f, 1f, "Caixote_A");
            Kenney("barrel", pier, new Vector3(1.1f, 1.25f, 9f), 0f, 0.7f, "Barril");
            Model(Kit.Survival, "bucket", pier, new Vector3(1.2f, 1.25f, 13.5f), 0f, SurvivalScale, "Balde");
            Model(Kit.Survival, "fish-large", pier, new Vector3(1.1f, 1.25f, 14.2f), 70f, SurvivalScale, "Peixe");

            for (int s = -1; s <= 1; s += 2)
            {
                var boat = new GameObject(s < 0 ? "Bote_Amarrado_O" : "Bote_Amarrado_L").transform;
                boat.SetParent(life, false);
                boat.SetPositionAndRotation(start + new Vector3(s * 3.6f, 0f, 9f + s * 2f), Quaternion.Euler(0f, 4f * s, 0f));
                Kenney(s < 0 ? "boat-row-large" : "boat-row-small", boat, Vector3.zero, 0f, 1.4f, "Modelo");
                boat.gameObject.AddComponent<MooredBob>().Configure(0.25f, 4f, 2f, 4.5f + s);
            }
        }

        /// <summary>
        /// Desembarque no porto (cena 07): ao atracar (HarborShoreLeave), o capitão desce na ponta do píer, virado para a
        /// vila; perto dali volta a bordo. Sem alcance para o navio (shipDistance 0): o "Desembarcar" comum não pega o
        /// píer, só o atracar.
        /// </summary>
        static LandingPoint CreatePierLanding()
        {
            var point = new GameObject("Desembarque_Pier");
            point.transform.position = PierShore + new Vector3(0f, 0f, PierLength);
            var land = new GameObject("PontoEmTerra").transform;
            land.SetParent(point.transform, false);
            land.SetPositionAndRotation(PierShore + new Vector3(0.8f, 1.3f, PierLength - 2.5f), Quaternion.Euler(0f, 180f, 0f));
            var landing = point.AddComponent<LandingPoint>();
            landing.Configure("Porto Alento", land, shipDistance: 0f, boardDistance: 5f);
            return landing;
        }

        /// <summary>Navios ancorados perto do porto (só visual + colisor: o navio do jogador não atravessa).</summary>
        static void MooredShips(Transform life)
        {
            var ships = new (string model, Vector2 local, float yaw)[]
            {
                ("ship-pirate-medium", new(-40f, 90f), 10f), ("ship-medium", new(42f, 94f), -25f),
            };
            foreach (var s in ships)
            {
                var ship = new GameObject("Ancorado_" + s.model).transform;
                ship.SetParent(life, false);
                ship.SetPositionAndRotation(IslandCenter + new Vector3(s.local.x, 0f, s.local.y), Quaternion.Euler(0f, s.yaw, 0f));
                Kenney(s.model, ship, new Vector3(0f, ShipModelKeel, 0f), 0f, 1f, "Casco");
                var box = ship.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0f);
                box.size = new Vector3(4.4f, 2.4f, 10.6f);
                var body = ship.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                ship.gameObject.AddComponent<MooredBob>().Configure(0.13f, 2.5f, 1f, 6f);
                Occluder(ship.gameObject, moving: true);
            }
        }

        /// <summary>Pontos da vila por onde os civis andam (local XZ): praça, taverna, armazém, píer, cais, ruas, estaleiro, forja, roça.</summary>
        static readonly Vector2[] TownStops =
        {
            new(-4f, 30f), new(4f, 36f), new(1f, 27f), new(-17f, 33f), new(21f, 35f), new(0f, 50f), new(SidePierX, 45f),
            new(-30f, 23f), new(30f, 23f), new(-10f, 21f), new(12f, 21f), new(40f, 22f), new(-33f, 9f), new(23f, -33f),
        };

        /// <summary>
        /// Moradores (Quaternius, Anne e Henry com roupas recoloridas, animações do kit): 18 parados (conversando em pares na
        /// praça e na taverna, nas barracas, no píer, no estaleiro, na forja, na roça, no portão da Fortaleza) e 10 andando
        /// pela vila no NavMesh do porto, parando um pouco em cada ponto. Sem o kit no projeto, a vila fica sem moradores.
        /// </summary>
        static void Villagers(Transform life)
        {
            if (!HasKitCharacter(KitCharacters.Anne) || !HasKitCharacter(KitCharacters.Henry)) return;
            var spots = new (Vector2 pos, float yaw, float y)[]
            {
                (PlazaCenter + new Vector2(-2.2f, 4.5f), 60f, -1f), (PlazaCenter + new Vector2(-0.6f, 5.4f), 240f, -1f),
                (PlazaCenter + new Vector2(-9.2f, 1f), 90f, -1f), (PlazaCenter + new Vector2(9.2f, 1f), -90f, -1f),
                (PlazaCenter + new Vector2(-5.5f, -4.5f), 200f, -1f), (PlazaCenter + new Vector2(3.5f, -3.5f), 160f, -1f),
                (new Vector2(-14f, 32f), 200f, -1f), (new Vector2(-12.4f, 31.4f), 290f, -1f),
                (new Vector2(14f, 33f), 160f, -1f), (new Vector2(1.2f, 66f), 270f, 1.25f), (new Vector2(-1.2f, 72f), 90f, 1.25f),
                (new Vector2(SidePierX + 0.8f, 61f), 0f, 1.25f), (ShipyardArea + new Vector2(-2f, -6f), 90f, -1f),
                (ForgeArea + new Vector2(5f, 3f), 230f, -1f), (FarmArea + new Vector2(1f, 9.5f), 0f, -1f),
                (FortressHill + new Vector2(1.6f, 8.8f), 0f, -1f), (new Vector2(-30f, 26.5f), 90f, -1f), (new Vector2(30f, 26.5f), 270f, -1f),
            };
            for (int k = 0; k < spots.Length; k++)
            {
                var s = spots[k];
                var ground = PortGround(s.pos);
                if (s.y > 0f) ground.y = s.y;
                Townsfolk(life, $"Morador_{k}", ground, s.yaw, k, null);
            }

            for (int k = 0; k < serviceDoors.Count; k++)
                Townsfolk(life, $"Atendente_{k}", serviceDoors[k].ground, serviceDoors[k].yaw, k + 5, null);

            var rng = new Random(29);
            var stops = Array.ConvertAll(TownStops, PortGround);
            for (int k = 0; k < 10; k++)
            {
                var route = new Vector3[4];
                for (int i = 0; i < route.Length; i++) route[i] = stops[rng.Next(stops.Length)];
                Townsfolk(life, $"Passante_{k}", route[0], rng.Next(360), k + 3, route);
            }
        }

        static void Townsfolk(Transform parent, string name, Vector3 ground, float yaw, int look, Vector3[] route)
        {
            var (model, palette) = CivilianLooks[look % CivilianLooks.Length];
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 1.7f;

            var visual = KitCharacter(model, go.transform, CivilianMaterial(model, palette));
            if (!visual.TryGetComponent(out Animator animator)) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = CivilAnimator(model);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (route != null)
            {
                var agent = go.AddComponent<NavMeshAgent>();
                agent.speed = 1.1f + (look % 4) * 0.12f;
                agent.radius = 0.3f;
                agent.height = 1.7f;
                agent.angularSpeed = 300f;
                agent.acceleration = 4f;
                agent.stoppingDistance = 0.5f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            go.AddComponent<Townsperson>().Configure(animator, route);
        }

        /// <summary>
        /// NavMesh da ilha do porto (só os civis usam): pelos colisores (chão, casas, barracas, árvores, píer), assado depois
        /// da vila e antes dos moradores, salvo ao lado da cena (&lt;cena&gt;_NavMesh_Porto.asset).
        /// </summary>
        static void BakePortNavMesh()
        {
            string assetPath = System.IO.Path.ChangeExtension(generatingScenePath, null) + "_NavMesh_Porto.asset";
            var go = new GameObject("NavMesh_Porto");
            go.transform.position = IslandCenter;
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.size = new Vector3(160f, 30f, 140f);
            surface.center = new Vector3(0f, 13f, 0f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (!surface.navMeshData)
            {
                Debug.LogWarning("[Pirates] NavMesh do porto não foi gerado: os moradores ficam parados.");
                return;
            }
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        }

        /// <summary>Gaivotas (primitivas brancas, asas que batem): em volta do porto e das praias.</summary>
        static void Seagulls(Transform life)
        {
            var white = GetMaterial("M_Gaivota", new Color(0.95f, 0.95f, 0.97f), 0.1f);
            var gray = GetMaterial("M_Gaivota_Asa", new Color(0.62f, 0.65f, 0.7f), 0.1f);
            var beak = GetMaterial("M_Gaivota_Bico", new Color(1f, 0.7f, 0.2f), 0.2f);
            var rng = new Random(13);
            var dock = PierShore + new Vector3(0f, 0f, PierLength + 2f);
            for (int k = 0; k < 11; k++)
            {
                var bird = new GameObject($"Gaivota_{k}").transform;
                bird.SetParent(life, false);
                Part(PrimitiveType.Cube, "Corpo", bird, Vector3.zero, new Vector3(0.22f, 0.2f, 0.6f), Quaternion.identity, white);
                Part(PrimitiveType.Cube, "Bico", bird, new Vector3(0f, 0f, 0.36f), new Vector3(0.06f, 0.06f, 0.14f), Quaternion.identity, beak);
                var wings = new Transform[2];
                for (int s = 0; s < 2; s++)
                {
                    var pivot = new GameObject(s == 0 ? "Asa_E" : "Asa_D").transform;
                    pivot.SetParent(bird, false);
                    float side = s == 0 ? -1f : 1f;
                    Part(PrimitiveType.Cube, "Pena", pivot, new Vector3(side * 0.5f, 0f, -0.05f), new Vector3(0.9f, 0.04f, 0.32f), Quaternion.identity, gray);
                    wings[s] = pivot;
                }
                // Mais da metade sobre o porto; o resto rodeando a ilha.
                bool harbor = k < 7;
                var center = harbor ? dock + new Vector3((float)rng.NextDouble() * 30f - 15f, 0f, (float)rng.NextDouble() * 20f - 25f) : IslandCenter;
                float radius = harbor ? 14f + (float)rng.NextDouble() * 22f : 60f + (float)rng.NextDouble() * 20f;
                bird.gameObject.AddComponent<CirclingBird>().Configure(center, radius, 11f + (float)rng.NextDouble() * 12f,
                    5f + (float)rng.NextDouble() * 3f, wings[0], wings[1]);
            }
        }

        /// <summary>Caranguejos andando de lado na areia, em vários trechos da praia.</summary>
        static void Crabs(Transform life)
        {
            var shell = GetMaterial("M_Caranguejo", new Color(0.88f, 0.32f, 0.2f), 0.3f);
            float[] angles = { 20f, 55f, 130f, 160f, 200f, 240f, 290f, 330f, 345f };
            for (int k = 0; k < angles.Length; k++)
            {
                float a = angles[k] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var shore = PortShore(dir * 20f, dir) - dir * 2.5f;
                var tangent = new Vector2(-dir.y, dir.x);
                var crab = new GameObject($"Caranguejo_{k}").transform;
                crab.SetParent(life, false);
                crab.position = PortGround(shore);
                Part(PrimitiveType.Sphere, "Casco", crab, new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.14f, 0.28f), Quaternion.identity, shell);
                for (int s = -1; s <= 1; s += 2)
                    Part(PrimitiveType.Sphere, "Garra", crab, new Vector3(s * 0.12f, 0.1f, 0.2f), new Vector3(0.12f, 0.08f, 0.12f), Quaternion.identity, shell);
                crab.gameObject.AddComponent<BeachCrawler>().Configure(PortGround(shore - tangent * 3f), PortGround(shore + tangent * 3f), 0.7f);
            }
        }

        // ---------- Fumaça e fogo ----------

        /// <summary>Fumaça low-poly: bolinhas cinza (malha, sem transparência) que sobem, crescem e encolhem até sumir.</summary>
        static ParticleSystem Smoke(Transform parent, Vector3 position, float strength)
        {
            var go = new GameObject("Fumaca");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * strength, 0.6f * strength);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 2.2f * strength;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.2f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.35f, 1.4f), new Keyframe(1f, 0f)));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // Vento leve para leste (em Play o WindDrift troca pelo vento do momento).
            velocity.x = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0.2f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = SmokePuffMesh;
            renderer.sharedMaterial = GetMaterial("M_Fumaca", new Color(0.78f, 0.78f, 0.8f), 0.05f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<WindDrift>();
            Occluder(go, moving: true);
            return ps;
        }

        /// <summary>Fogo low-poly: cubinhos laranja/amarelos (emissivos) subindo, uma luz que treme e fumaça por cima.</summary>
        static void Fire(Transform parent, Vector3 position, float strength)
        {
            var go = new GameObject("Fogo");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f * strength, 1.8f * strength);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f * strength, 0.32f * strength);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.08f));
            main.maxParticles = 60;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 26f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.3f * strength;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = FireParticleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(go.transform.parent, false);
            lightGo.transform.position = position + Vector3.up * 0.8f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 7f * strength;
            light.shadows = LightShadows.None;
            lightGo.AddComponent<FlickerLight>().Configure(2.2f * strength, 0.35f);
            Smoke(parent, position + Vector3.up * 1.4f * strength, 0.8f * strength);
        }

        /// <summary>Material das chamas: URP Particles Unlit (a cor vem de cada partícula), opaco.</summary>
        static Material FireParticleMaterial()
        {
            string path = $"{MaterialsFolder}/M_FogoParticula.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_FogoParticula" };
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Fogueira de pedras com lenha, fogo e banquinhos de tronco em volta.</summary>
        static void Campfire(Transform parent, Vector3 ground, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = ground;
            Model(Kit.Nature, "campfire_stones", root, Vector3.zero, 0f, 3.2f, "Pedras");
            Model(Kit.Nature, "campfire_logs", root, Vector3.up * 0.05f, 30f, 3.2f, "Lenha");
            Fire(root, ground + Vector3.up * 0.2f, 1f);
            for (int k = 0; k < 3; k++)
            {
                float a = (k * 120f + 20f) * Mathf.Deg2Rad;
                Model(Kit.Nature, "log", root, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.9f, -k * 120f - 20f, 2.6f, "Banco_Tronco");
            }
        }
    }
}
