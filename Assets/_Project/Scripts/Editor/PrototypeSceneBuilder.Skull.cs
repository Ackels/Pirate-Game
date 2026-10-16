using System.Collections.Generic;
using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ilha da Caveira 2× (M6.7; também a ilha da cena 06): ~260 × 210 m, a praia norte de desembarque no mesmo lugar e a
    /// ilha crescendo para os lados e para o sul. Acampamento grande no meio (paliçada de estacas do Survival Kit, dois
    /// portões, cabanas, barracas, torre de vigia, fogueira), trilhas de terra até três postos de guarda (o do naufrágio na
    /// praia leste, o das ruínas no alto do morro oeste e o da trilha sul), segunda praia de desembarque ao sul, casco
    /// naufragado na areia, ruínas de pedra e mata fechada do Nature Kit no resto. Layout em coordenadas locais (X leste,
    /// Z norte) a partir de <see cref="SkullCenter"/>.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Centro do terreno e do acampamento: a praia norte (BigIslandCenter + 52 m) fica 104 m ao norte.</summary>
        static Vector3 SkullCenter => BigIslandCenter + new Vector3(0f, 0f, -52f);
        const float SkullRadiusX = 128f, SkullRadiusZ = 104f, SkullPlateau = 1.4f;

        // Trilhas (pontos locais): praia norte → portão norte; portão leste → posto do naufrágio → praia leste;
        // portão oeste → morro das ruínas; portão sul → posto da trilha → praia sul.
        static readonly Vector2[] TrailNorth = { new(0f, 97f), new(-6f, 72f), new(4f, 44f), new(0f, 17f) };
        static readonly Vector2[] TrailEast = { new(15f, 4f), new(45f, 10f), new(80f, 14f), new(112f, 10f) };
        static readonly Vector2[] TrailWest = { new(-15f, -4f), new(-40f, -14f), new(-64f, -32f) };
        static readonly Vector2[] TrailSouth = { new(0f, -17f), new(18f, -40f), new(28f, -58f), new(22f, -84f) };

        static Vector2 RuinsSpot => new(-72f, -38f);
        const string ChiefLairId = "covil_barbanegra";
        const int ChiefFame = 60;
        // Topo das ruínas na altura que o morro já tem ali (r ≈ 9 m): mais alto, vira um degrau que o NavMesh não sobe.
        const float RuinsHeight = 11.2f;

        /// <summary>Posto de guarda: um EnemyCamp pequeno (não conta para a missão do acampamento).</summary>
        sealed class OutpostSpec
        {
            public string name, id;
            public Vector2 local;
            public bool camp;
            public (string name, Vector3 local, float yaw, bool gunner, string model)[] guards;
        }

        static readonly OutpostSpec[] Outposts =
        {
            new()
            {
                name = "Posto do naufrágio", id = "posto_naufragio", local = new(82f, 14f), camp = true,
                guards = new[]
                {
                    ("Guarda_Naufragio_A", new Vector3(-2.2f, 0f, 2.4f), -100f, false, KitCharacters.Sharky),
                    ("Guarda_Naufragio_B", new Vector3(2.4f, 0f, 2.8f), -80f, false, KitCharacters.Mako),
                    ("Atirador_Naufragio", new Vector3(0.5f, 0f, -3.2f), -90f, true, KitCharacters.Skeleton),
                },
            },
            new()
            {
                // Covil do chefe (pacote B, passo 4): o Capitão Barba-Negra no meio das ruínas, de frente para a trilha (que chega pelo
                // arco, a nordeste), e a Guarda Negra (passo 2): dois espadachins entre ele e o arco e a mosqueteira atrás, perto da cabeça
                // de pedra. Ele espera enquanto eles lutam.
                name = "Ruínas do Barba-Negra", id = ChiefLairId, local = new(-72f, -38f), camp = false,
                guards = new[]
                {
                    ("Chefe_BarbaNegra", new Vector3(0.5f, 0f, 1.5f), 55f, false, KitCharacters.Captain),
                    ("GuardaNegra_Sabre_A", new Vector3(3.6f, 0f, 3.8f), 50f, false, KitCharacters.Henry),
                    ("GuardaNegra_Sabre_B", new Vector3(4.6f, 0f, 0.2f), 70f, false, KitCharacters.Henry),
                    // A ~9,6 m do chefe (era 7,6: quem ia atrás dela passava a ~5 m dele e o acordava com a guarda de pé; revisão de 16/10).
                    ("GuardaNegra_Mosquete", new Vector3(-4f, 0f, -7f), 45f, true, KitCharacters.Anne),
                },
            },
            new()
            {
                name = "Posto da trilha sul", id = "posto_sul", local = new(28f, -58f), camp = true,
                guards = new[]
                {
                    ("Guarda_Sul_A", new Vector3(-2f, 0f, -2.4f), 190f, false, KitCharacters.Mako),
                    ("Guarda_Sul_B", new Vector3(2.4f, 0f, -2f), 170f, false, KitCharacters.Sharky),
                },
            },
        };

        /// <summary>Posto já montado na ilha (os guardas entram depois do NavMesh, junto com o jogador).</summary>
        sealed class OutpostSite
        {
            public OutpostSpec spec;
            public Transform site;
        }

        /// <summary>Tudo da ilha que vem antes do acampamento e da mata (pontos calculados no litoral).</summary>
        sealed class SkullIsland
        {
            public Transform island;
            public IslandSpec spec;
            public LandingPoint north, south;
            public Vector2 wreck, southBeach;
            public List<OutpostSite> outposts = new();
            /// <summary>Cemitério amaldiçoado (os esqueletos entram depois do NavMesh).</summary>
            public Transform graveyard;
            /// <summary>Pontos locais onde a mata não nasce (caixotes soltos, peças).</summary>
            public List<Vector2> clearings = new();
        }

        static IslandSpec SkullIslandSpec()
        {
            var paths = new List<(Vector2, Vector2, float)>();
            foreach (var trail in new[] { TrailNorth, TrailEast, TrailWest, TrailSouth })
                for (int k = 1; k < trail.Length; k++) paths.Add((trail[k - 1], trail[k], 3.5f));
            paths.Add((Vector2.zero, Vector2.zero, 15f));   // chão batido do acampamento
            // Atalho até a borda do cemitério (pacote B, passo 3): para antes das covas.
            paths.Add((TrailWest[1], GraveyardSpot + (TrailWest[1] - GraveyardSpot).normalized * 9.5f, 2.5f));
            var flats = new List<(Vector2, float, float, float)>
            {
                (Vector2.zero, 24f, 10f, 1.8f), (RuinsSpot, 9f, 6f, RuinsHeight), (GraveyardSpot, 11f, 5f, GraveyardHeight),
            };
            foreach (var o in Outposts) if (o.camp) flats.Add((o.local, 9f, 6f, 1.6f));
            return new IslandSpec
            {
                name = "IlhaGrande", center = SkullCenter, radiusX = SkullRadiusX, radiusZ = SkullRadiusZ, coastNoise = 0.14f,
                coastOutwardOnly = true, seed = 42, plateau = SkullPlateau, beachWidth = 10f, inlandNoise = 0.9f, cell = 2f,
                // Litoral norte fixo: a praia de desembarque continua onde sempre esteve.
                anchors = new[] { (90f, SkullRadiusZ, 22f) },
                hills = new[]
                {
                    (RuinsSpot, 34f, 12.4f), (new Vector2(60f, 55f), 26f, 9f), (new Vector2(-62f, 50f), 24f, 7f),
                    (new Vector2(74f, -60f), 26f, 10f), (new Vector2(-24f, -80f), 16f, 5f), (new Vector2(-102f, 4f), 18f, 4f),
                },
                flats = flats.ToArray(),
                paths = paths.ToArray(),
            };
        }

        /// <summary>
        /// Terreno, praias de desembarque (norte e sul), casco naufragado na praia leste, ruínas e os postos de guarda
        /// (só o visual; os guardas vêm depois). A mata vem por último (PlantSkullJungle), em volta de tudo.
        /// </summary>
        static SkullIsland CreateSkullIsland(Material deck, Material hull, string northName, string southName)
        {
            var spec = SkullIslandSpec();
            var island = CreateIslandTerrain(null, spec);
            var s = new SkullIsland { island = island, spec = spec };

            // Praia norte (a de sempre): bote e bandeira.
            s.north = SkullLanding(island, "Praia_Desembarque", northName, new Vector2(0f, SkullRadiusZ), Vector2.up, deck, hull,
                "flag-pirate-high");
            // Praia sul: no fim da trilha sul.
            s.southBeach = SkullShore(island, TrailSouth[TrailSouth.Length - 1], Vector2.down);
            s.south = SkullLanding(island, "Praia_Sul", southName, s.southBeach, Vector2.down, deck, hull, "flag-pennant");

            s.wreck = SkullShore(island, new Vector2(100f, 10f), Vector2.right) + new Vector2(-3f, 0f);
            Shipwreck(island, s.wreck);
            Ruins(island, RuinsSpot);
            RuinsArena(island, island.Find("Ruinas"), RuinsSpot);
            s.graveyard = Graveyard(island, GraveyardSpot);
            foreach (var o in Outposts)
            {
                var site = OutpostVisual(island, o);
                s.outposts.Add(new OutpostSite { spec = o, site = site });
            }
            return s;
        }

        /// <summary>Último ponto em terra (chão ≥ 0,3 m) andando de <paramref name="from"/> (local) na direção dada.</summary>
        static Vector2 SkullShore(Transform island, Vector2 from, Vector2 direction)
        {
            var p = from;
            for (int k = 0; k < 400; k++)
            {
                var next = p + direction.normalized * 0.5f;
                if (!IslandGround(island, island.TransformPoint(new Vector3(next.x, 0f, next.y)), out var hit, out _) ||
                    hit.y - island.position.y < 0.3f) return p;
                p = next;
            }
            return p;
        }

        static Vector3 SkullGround(Transform island, Vector2 local)
        {
            var world = island.TransformPoint(new Vector3(local.x, 0f, local.y));
            return IslandGround(island, world, out var hit, out _) ? hit : world + Vector3.up * SkullPlateau;
        }

        /// <summary>Praia de desembarque: o ponto no litoral, o lugar em terra 5 m para dentro (virado para a ilha), bote e bandeira.</summary>
        static LandingPoint SkullLanding(Transform island, string objectName, string displayName, Vector2 shore, Vector2 seaward,
            Material deck, Material hull, string flagModel)
        {
            var inland = -seaward.normalized;
            var point = new GameObject(objectName);
            point.transform.SetParent(island, false);
            point.transform.position = island.TransformPoint(new Vector3(shore.x, 0f, shore.y));
            var land = new GameObject("PontoEmTerra").transform;
            land.SetParent(point.transform, false);
            var landLocal = shore + inland * 5f;
            float yaw = Mathf.Atan2(inland.x, inland.y) * Mathf.Rad2Deg;
            land.SetPositionAndRotation(island.TransformPoint(new Vector3(landLocal.x, 0f, landLocal.y)), Quaternion.Euler(0f, yaw, 0f));
            var side = new Vector2(inland.y, -inland.x);
            var boat = shore + inland * 3.5f + side * 2.2f;
            Rowboat(point.transform, island.TransformPoint(new Vector3(boat.x, 0f, boat.y)), deck, hull);
            var flag = SkullGround(island, shore + inland * 6f - side * 2.5f);
            Kenney(flagModel, point.transform, flag - point.transform.position, 0f, 1.5f, "Bandeira");

            var landing = point.AddComponent<LandingPoint>();
            landing.Configure(displayName, land, shipDistance: 40f, boardDistance: 4f);
            return landing;
        }

        /// <summary>Casco naufragado do kit encalhado na areia, adernado, com colisor da malha.</summary>
        static void Shipwreck(Transform island, Vector2 local)
        {
            var ground = SkullGround(island, local);
            var wreck = Kenney("ship-wreck", island, island.InverseTransformPoint(ground) + Vector3.down * 0.8f,
                Quaternion.Euler(0f, 12f, 11f), 1.2f, "Naufragio");
            foreach (var filter in wreck.GetComponentsInChildren<MeshFilter>())
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            foreach (var t in wreck.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Occluder(wreck);
        }

        /// <summary>
        /// Ruínas no alto do morro oeste (Nature e Pirate Kit): anel de colunas (algumas quebradas, uma caída), a cabeça de
        /// pedra virada para a trilha, obelisco, arco, dois pedaços de muralha e a base de uma torre, blocos soltos.
        /// </summary>
        static void Ruins(Transform island, Vector2 local)
        {
            var root = new GameObject("Ruinas").transform;
            root.SetParent(island, false);
            root.position = SkullGround(island, local);
            // De frente para quem chega pela trilha (vem de nordeste).
            var toTrail = (TrailWest[TrailWest.Length - 2] - local).normalized;
            float face = Mathf.Atan2(toTrail.x, toTrail.y) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, face, 0f);

            GameObject Piece(Kit kit, string model, Vector2 offset, float yaw, float scale, string name, bool occlude = true)
            {
                var p3 = root.position + rot * new Vector3(offset.x, 0f, offset.y);
                var ground = SkullGround(island, new Vector2(p3.x - island.position.x, p3.z - island.position.z));
                var go = Model(kit, model, root, ground - root.position + Vector3.down * 0.15f, face + yaw, scale, name);
                if (occlude) Occluder(go);
                return go;
            }

            void Column(GameObject c, float height)
            {
                var col = c.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, height * 0.5f, 0f);
                col.radius = 0.15f;
                col.height = height;
            }

            for (int k = 0; k < 9; k++)
            {
                if (k == 4) continue;   // a caída fica no chão, logo abaixo
                float a = (k * 40f + 20f) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 7.5f;
                bool broken = k % 3 == 1;
                var column = Piece(Kit.Nature, broken ? "statue_columnDamaged" : "statue_column", offset, k * 23f, 3.4f, $"Coluna_{k}");
                Column(column, broken ? 0.7f : 1f);
            }
            var fallen = Piece(Kit.Nature, "statue_column", new Vector2(Mathf.Sin(3.1f), Mathf.Cos(3.1f)) * 9f, 0f, 3.4f, "Coluna_Caida");
            fallen.transform.localRotation = rot * Quaternion.Euler(0f, 75f, 0f) * Quaternion.Euler(90f, 0f, 0f);
            fallen.transform.position += Vector3.up * 0.45f;
            Column(fallen, 1f);

            var head = Piece(Kit.Nature, "statue_head", new Vector2(0f, -5.5f), 0f, 5f, "Cabeca_de_Pedra");
            var headBox = head.AddComponent<BoxCollider>();
            headBox.center = new Vector3(0f, 0.45f, 0f);
            headBox.size = new Vector3(0.73f, 1f, 0.55f);
            Piece(Kit.Nature, "statue_obelisk", new Vector2(-4.5f, -4f), 10f, 4f, "Obelisco").AddComponent<CapsuleCollider>().radius = 0.15f;
            Piece(Kit.Nature, "statue_ring", new Vector2(0f, 9f), 0f, 4f, "Arco");
            Piece(Kit.Pirate, "castle-wall", new Vector2(-9f, -2f), 90f, 1f, "Muralha_A").AddComponent<BoxCollider>();
            Piece(Kit.Pirate, "castle-window", new Vector2(9.5f, -3f), 75f, 1f, "Muralha_B").AddComponent<BoxCollider>();
            Piece(Kit.Pirate, "tower-base", new Vector2(8f, -8f), 20f, 1.2f, "Torre_Caida").AddComponent<BoxCollider>();
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 61f + 15f) * Mathf.Deg2Rad;
                var block = Piece(Kit.Nature, "statue_block", new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (4f + k % 3 * 2.5f), k * 37f, 3f,
                    $"Bloco_{k}", occlude: false);
                block.AddComponent<BoxCollider>();
            }
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        /// <summary>Visual de um posto de guarda: fogueira, barraca, lenha, colchonete e uma bandeirinha pirata.</summary>
        static Transform OutpostVisual(Transform island, OutpostSpec o)
        {
            var root = new GameObject("Posto_" + o.id).transform;
            root.position = SkullGround(island, o.local);
            if (o.camp)
            {
                Campfire(root, root.position, "Fogueira");
                var tent = Model(Kit.Nature, "tent_detailedOpen", root, new Vector3(-4.5f, -0.05f, -2f), 60f, 4f, "Barraca");
                var box = tent.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(0.87f, 0.5f, 0.67f);
                Occluder(tent);
                Model(Kit.Nature, "log_stack", root, new Vector3(3.8f, 0f, -2.5f), 20f, 3.2f, "Lenha").AddComponent<BoxCollider>();
                Model(Kit.Survival, "bedroll", root, new Vector3(-1.5f, 0.05f, -4.2f), 80f, SurvivalScale, "Colchonete");
            }
            Kenney("flag-pirate", root, new Vector3(4.5f, 0f, 2f), 90f, 1.3f, "Bandeira");
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return root;
        }

        /// <summary>Onde a mata não nasce (local XZ): praias de desembarque, acampamento, postos, ruínas, naufrágio, trilhas.</summary>
        static bool SkullWild(SkullIsland s, Vector2 p)
        {
            if (p.y > 82f && Mathf.Abs(p.x) < 20f) return false;
            if ((p - s.southBeach).magnitude < 14f) return false;
            if (p.magnitude < 27f) return false;
            foreach (var o in Outposts) if ((p - o.local).magnitude < (o.camp ? 11f : 15f)) return false;
            if ((p - s.wreck).magnitude < 13f) return false;
            if ((p - GraveyardSpot).magnitude < 14f) return false;
            foreach (var c in s.clearings) if ((p - c).magnitude < 2.5f) return false;
            foreach (var trail in new[] { TrailNorth, TrailEast, TrailWest, TrailSouth })
                for (int k = 1; k < trail.Length; k++)
                    if (DistanceToSegment(p, trail[k - 1], trail[k]) < 3.5f) return false;
            return true;
        }

        /// <summary>Mata fechada (Nature Kit) e palmeiras nas praias, em volta de tudo que já está na ilha.</summary>
        static void PlantSkullJungle(SkullIsland s) =>
            PlantLush(s.island, s.spec, p => SkullWild(s, p), trees: 300, bushes: 280, flowers: 110, grass: 320, ground: 50, rocks: 26,
                palms: 90, seed: 42);

        /// <summary>Guardas dos postos (perseguem o jogador), um EnemyCamp por posto.</summary>
        static List<EnemyCamp> CreateOutpostGuards(List<OutpostSite> outposts, FootCharacter player, Material iron, Material wood, Material brass)
        {
            var camps = new List<EnemyCamp>();
            var cutlass = EnemyCutlass();
            var pistol = EnemyPistol();
            foreach (var o in outposts)
            {
                var camp = o.site.gameObject.AddComponent<EnemyCamp>();
                var parent = new GameObject("Guardas_" + o.spec.id).transform;
                var members = new EnemyFootAI[o.spec.guards.Length];
                for (int k = 0; k < members.Length; k++)
                {
                    var g = o.spec.guards[k];
                    members[k] = CreatePirate(parent, g.name, o.site.TransformPoint(g.local), g.yaw, PostWeapon(g.name, g.gunner, cutlass, pistol),
                        player, camp, iron, wood, brass, g.model);
                }
                // Covil do Barba-Negra: um espadachim da Guarda Negra ataca por vez (com os dois juntos e a mosqueteira, o capitão parado
                // perdia 100 de vida em ~2 s; a Caveira inteira tira ~28/s). A mosqueteira atira independente da vez.
                camp.Configure(o.spec.name, o.spec.id, members, leash: 24f, attackersAtOnce: o.spec.id == ChiefLairId ? 1 : 2);
                if (o.spec.id == ChiefLairId) camp.SetFame(ChiefFame);
                camps.Add(camp);
            }
            return camps;
        }
    }
}
