using System.Collections.Generic;
using Pirates.Ambient;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Ships;
using Pirates.UI;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ilha da Ermida e a ruína das alavancas (C4, 19/10; pacote C, exploração; cena 07): ilha nova no lugar vazio "A" do mapa espaçado
    /// (sudoeste, a caminho das Águas Negras). No alto, a Ermida de São Telmo, que os marujos da Frota de Prata ergueram cem anos atrás.
    /// Refeita em 19/10 (pedido do Jhonata: "uma construção grande onde você entra e vai passando pelos desafios dentro dela"): um prédio
    /// só, sem teto (a câmera vê de cima), numa plataforma de 0,9 m, com as peças das Ultimate Modular Ruins do Quaternius repintadas em
    /// pedra quente. Caminho único: escadaria → vestíbulo (os 3 braseiros) → corredor 1, a cripta dos espinhos → sala 1 (alavanca) →
    /// corredor 2, o claustro do fogo → sala 2 (alavanca) → corredor 3, a galeria dos dardos (saem das janelas gradeadas) → sala 3
    /// (alavanca e a grade da capela). Cada alavanca acende um braseiro e desarma o corredor dela; as três afundam a grade da capela
    /// (o altar e o baú) e a do atalho da sala 3 de volta ao vestíbulo (<see cref="RuinPuzzle"/>). Missão com o Tobias. Layout local
    /// (X leste, Z norte) a partir do centro da ruína; a ilha só existe no mundo.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string ErmidaId = "ermida_sao_telmo", ErmidaName = "Ermida de São Telmo";
        static Vector3 ErmidaCenter => new(-270f, 0f, -470f);
        const float ErmidaRX = 44f, ErmidaRZ = 40f, ErmidaFloor = 2.2f;
        static Vector2 ErmidaRuin => new(-8f, 0f);
        static readonly Vector2[] ErmidaTrail = { new(40f, 0f), new(26f, -3f), new(12f, 0f) };

        /// <summary>Altura da plataforma (uma escada do kit) e o meio tamanho dela (o prédio vai de x −14 a 14 e z −12 a 12).</summary>
        const float ErmidaPlinth = 0.9f, PlinthHalfX = 15f, PlinthHalfZ = 13f;

        /// <summary>As partes do prédio (x, z, largura, fundo): vestíbulo + capela + corredor 2, corredor 1, sala 1, sala 2, corredor 3, sala 3.</summary>
        static readonly Rect[] ErmidaParts =
        {
            new(-14f, -4f, 28f, 8f), new(-8f, 4f, 22f, 3f), new(-14f, 4f, 6f, 8f),
            new(-14f, -12f, 6f, 8f), new(-8f, -7f, 10f, 3f), new(2f, -10f, 10f, 6f),
        };

        static bool InsideErmida(float x, float z, float margin = 0f)
        {
            foreach (var r in ErmidaParts)
                if (x >= r.xMin - margin && x <= r.xMax + margin && z >= r.yMin - margin && z <= r.yMax + margin) return true;
            return false;
        }

        static IslandSpec ErmidaIslandSpec()
        {
            var paths = new List<(Vector2, Vector2, float)> { (ErmidaRuin + new Vector2(21f, 0f), ErmidaRuin + new Vector2(21f, 0f), 5f) };
            for (int k = 1; k < ErmidaTrail.Length; k++) paths.Add((ErmidaTrail[k - 1], ErmidaTrail[k], 3f));
            return new IslandSpec
            {
                name = "Ilha_Ermida", center = ErmidaCenter, radiusX = ErmidaRX, radiusZ = ErmidaRZ, coastNoise = 0.14f, coastOutwardOnly = true,
                seed = 31, plateau = 1.4f, beachWidth = 5f, inlandNoise = 0.4f, cell = 1.6f,
                anchors = new[] { (0f, ErmidaRX, 22f) },
                hills = new[] { (new Vector2(-30f, 27f), 10f, 4f), (new Vector2(20f, -27f), 10f, 3f), (new Vector2(18f, 26f), 8f, 2.5f) },
                flats = new[] { (ErmidaRuin, 26f, 6f, ErmidaFloor) },
                paths = paths.ToArray(),
            };
        }

        static bool ErmidaWild(Vector2 p)
        {
            if (p.x > 32f && Mathf.Abs(p.y) < 9f) return false;
            if ((p - ErmidaRuin).magnitude < 28f) return false;
            for (int k = 1; k < ErmidaTrail.Length; k++)
                if (DistanceToSegment(p, ErmidaTrail[k - 1], ErmidaTrail[k]) < 3f) return false;
            return true;
        }

        static void AddErmida(FootCharacter captain, LandMaterials m, LandLootAssets landLoot, LootItems items, Material deck, Material hull, GameObject hud)
        {
            var spec = ErmidaIslandSpec();
            var islands = GameObject.Find("Islands");
            var island = CreateIslandTerrain(islands ? islands.transform : null, spec);
            PlantLush(island, spec, ErmidaWild, trees: 45, bushes: 50, flowers: 30, grass: 70, ground: 10, rocks: 8, palms: 22, seed: 31);
            SkullLanding(island, "Desembarque_IlhaErmida", "Ilha da Ermida", new Vector2(ErmidaRX, 0f), Vector2.right, deck, hull, "flag-pennant");

            var root = new GameObject("Ermida").transform;
            root.position = Ground(island, ErmidaRuin);
            var mat = RuinMaterials(m);
            ErmidaBuilding(root, mat, out var gate, out var shortcut, out var entrance);
            var fires = ErmidaBraziers(root);
            var levers = new List<RuinLever>
            {
                SpikeWing(root, mat, fires[0]),
                FireWing(root, mat, fires[2]),
                DartWing(root, mat, fires[1]),
            };
            ErmidaRooms(root);
            ErmidaDetails(root);
            ErmidaDecor(root);

            var puzzle = root.gameObject.AddComponent<RuinPuzzle>();
            // As grades afundam dentro da plataforma (para cima passariam do alto das paredes).
            puzzle.Configure(ErmidaName, ErmidaId, 25, levers.ToArray(), gate, -3.1f);
            puzzle.SetExtras(new[] { shortcut }, entrance);
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Ermida.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 280, 450), Drop(items.powder, 0.7f, 4, 8), Drop(items.iron, 0.7f, 4, 8), Drop(BombItem(), 0.5f, 1, 3),
                    Drop(MapPieceItem(), 0.5f, 1, 1),
                };
                t.chestChance = 0.5f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 300);
            });
            var chest = CreateChest(root, "Bau_Ermida", "Tesouro da Ermida", root.TransformPoint(new Vector3(-5f, ErmidaPlinth, 0f)), 90f, table,
                landLoot, withPadlock: true, ChestStyle.RuinsGold);
            chest.SetLock(puzzle);
            root.gameObject.AddComponent<WorldMarker>().Configure(ErmidaId, ErmidaName, 9f);
            // C5: o alçapão da capela (abre com a grade) e as catacumbas lá embaixo.
            AddCatacombs(root, puzzle, landLoot, items, mat, captain);

            BakeNavMeshVolume("NavMesh_IlhaErmida", "_NavMesh_Ermida", ErmidaCenter, new Vector3(ErmidaRX * 2.4f, 26f, ErmidaRZ * 2.4f));

            var puller = captain.gameObject.AddComponent<LeverPuller>();
            var walker = captain.gameObject.AddComponent<PassageWalker>();
            hud.AddComponent<RuinHUD>().Bind(puller, walker, new[] { puzzle });
        }

        sealed class RuinMats
        {
            public Material slab, stone, hole, warn, ember, iron, wood, brass;
        }

        static RuinMats RuinMaterials(LandMaterials m)
        {
            var warn = GetMaterial("M_Ruina_Aviso", new Color(0.55f, 0.06f, 0.04f), 0.3f);
            warn.SetColor("_EmissionColor", new Color(2.4f, 0.25f, 0.1f));
            Emissive(warn);
            var ember = GetMaterial("M_Ruina_Brasa", new Color(0.45f, 0.18f, 0.06f), 0.2f);
            ember.SetColor("_EmissionColor", new Color(2.2f, 0.75f, 0.15f));
            Emissive(ember);
            return new RuinMats
            {
                // Laje dos espinhos e pedestais na pedra das ruínas (eram o cinza do kit pirata e o azul do Nature Kit).
                slab = RuinsMaterial("Main2"), stone = RuinsMaterial("Highlights"),
                hole = GetMaterial("M_Ruina_Buraco", new Color(0.06f, 0.05f, 0.05f), 0.1f),
                warn = warn, ember = ember, iron = m.iron, wood = m.wood, brass = m.brass,
            };
        }

        // ---------- Peças das ruínas (Quaternius) ----------

        /// <summary>
        /// Peça das ruínas num suporte nosso (o FBX tem a raiz girada do Blender): giro, escala e colisores ficam no suporte, em metros
        /// (X ao longo da peça, Y para cima; as paredes têm 0,28 m de espessura, de z −0,25 a +0,03).
        /// </summary>
        static Transform RuinPiece(Transform parent, string model, Vector3 local, float yaw, string name = null, float scale = 1f)
        {
            var holder = new GameObject(name ?? model).transform;
            holder.SetParent(parent, false);
            holder.SetLocalPositionAndRotation(local, Quaternion.Euler(0f, yaw, 0f));
            holder.localScale = Vector3.one * scale;
            Model(Kit.Ruins, model, holder, Vector3.zero, 0f, 1f, model);
            return holder;
        }

        static void RuinBox(Transform t, Vector3 center, Vector3 size)
        {
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        /// <summary>Giro que põe o X da peça ao longo da direção (no plano XZ local).</summary>
        static float YawAlong(Vector2 dir) => Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

        /// <summary>
        /// Fileira de peças de parede de 2 m de <paramref name="a"/> a <paramref name="b"/> (local, chão em <paramref name="y"/>); comprimento
        /// que não fecha em 2 m estica as peças (sem sobrepor: faces no mesmo plano piscariam). A parede fica à <b>direita</b> de quem anda
        /// de a para b (o kit põe a peça de z −0,25 a +0,03): contorne as salas no sentido anti-horário para ela ficar do lado de fora.
        /// <paramref name="pick"/> escolhe o modelo de cada vão (null = vão vazio, ruína caída). <paramref name="collide"/> &gt; 0 põe um
        /// colisor dessa altura em cada peça; a câmera deixa translúcido o que tapa o capitão.
        /// </summary>
        static void RuinWallRun(Transform parent, Vector2 a, Vector2 b, float y, System.Func<int, string> pick, float collide, string name,
            bool occlude = true, string recolor = null)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 2f - 0.05f));
            Vector2 dir = d / len;
            float step = len / n, yaw = YawAlong(dir);
            for (int i = 0; i < n; i++)
            {
                string model = pick(i);
                if (model == null) continue;
                Vector2 at = a + dir * (step * (i + 0.5f));
                var piece = RuinPiece(parent, model, new Vector3(at.x, y, at.y), yaw, name);
                piece.localScale = new Vector3(step / 2f, 1f, 1f);
                if (collide > 0f) RuinBox(piece, new Vector3(0f, collide * 0.5f, -0.11f), new Vector3(2f, collide, 0.32f));
                // Repinta antes da transparência da câmera (ela guarda os materiais que a peça tem agora).
                if (recolor != null) RuinsRecolor(piece, recolor);
                if (occlude) Occluder(piece.gameObject);
            }
        }

        /// <summary>
        /// Parede alta (dois andares): a de baixo inteira e com colisor; a de cima com janelas, partes com mato e falhas. Em metade das
        /// falhas nasce um arbusto em cima do muro (o mato tomando a ruína).
        /// </summary>
        static void RuinHighWall(Transform parent, Vector2 a, Vector2 b, string name, int seed, float y = ErmidaPlinth)
        {
            string[] top = { "Window_Open", "Wall", "Wall_Broken", null, "Wall_Overgrown", "Window_Open", "Wall_Broken", "Wall" };
            RuinWallRun(parent, a, b, y, i => (i * 5 + seed) % 7 == 3 ? "Wall_Overgrown" : "Wall", 2f, name);
            RuinWallRun(parent, a, b, y + 2f, i => top[(i * 3 + seed) % top.Length], 0f, name + "_Alta");
            Vector2 d = b - a;
            int n = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 2f - 0.05f));
            for (int i = 0; i < n; i++)
            {
                if (top[(i * 3 + seed) % top.Length] != null || (i + seed) % 2 == 1) continue;
                Vector2 at = a + d * ((i + 0.5f) / n) + new Vector2(d.y, -d.x).normalized * 0.11f;
                var bush = RuinPiece(parent, (i + seed) % 3 == 0 ? "Bush_2x1" : "Bush_Round", new Vector3(at.x, y + 1.95f, at.y), YawAlong(d.normalized), "Mato_Muro");
                bush.localScale = Vector3.one * 0.75f;
            }
        }

        static void RuinLowWall(Transform parent, Vector2 a, Vector2 b, string name, int seed, float y = ErmidaPlinth) =>
            RuinWallRun(parent, a, b, y, i => (i * 5 + seed) % 6 == 2 ? "Wall_Overgrown" : "Wall", 2f, name);

        /// <summary>Uma peça de 4 m com arco (porta): pernas e o alto com colisor, o vão de 3,2 × 3 m livre.</summary>
        static Transform RuinDoorway(Transform parent, string model, Vector2 at, Vector2 along, string name, float y = ErmidaPlinth,
            string recolor = null)
        {
            var arch = RuinPiece(parent, model, new Vector3(at.x, y, at.y), YawAlong(along), name);
            if (recolor != null) RuinsRecolor(arch, recolor);
            foreach (float side in new[] { -1.8f, 1.8f })
                RuinBox(arch, new Vector3(side, 2f, -0.11f), new Vector3(0.4f, 4f, 0.32f));
            RuinBox(arch, new Vector3(0f, 3.5f, -0.11f), new Vector3(3.2f, 1f, 0.32f));
            Occluder(arch.gameObject);
            return arch;
        }

        /// <summary>Arco gótico solto atravessando um corredor (as pernas param o capitão; o vão de 2,4 m passa).</summary>
        static void CorridorArch(Transform parent, Vector3 local, float yaw, string recolor = null)
        {
            var arch = RuinPiece(parent, "Arch_Gothic", local, yaw, "Arco");
            if (recolor != null) RuinsRecolor(arch, recolor);
            foreach (float side in new[] { -1.3f, 1.3f })
                RuinBox(arch, new Vector3(side, 1.4f, -0.12f), new Vector3(0.45f, 2.8f, 0.5f));
            Occluder(arch.gameObject);
        }

        /// <summary>Grade de ferro de 3,2 × 3 m (barras ao longo do X local; gire para o vão), com colisor; afunda ao abrir.</summary>
        static Transform RuinGrate(Transform root, Vector3 local, float yaw, string name, RuinMats mat)
        {
            var gate = new GameObject(name).transform;
            gate.SetParent(root, false);
            gate.SetLocalPositionAndRotation(local, Quaternion.Euler(0f, yaw, 0f));
            for (int k = 0; k < 11; k++)
                Part(PrimitiveType.Cylinder, "Barra", gate, new Vector3(-1.5f + k * 0.3f, 1.5f, 0f), new Vector3(0.07f, 1.5f, 0.07f), Quaternion.identity, mat.iron);
            foreach (float y in new[] { 0.5f, 1.5f, 2.5f })
                Part(PrimitiveType.Cube, "Travessa", gate, new Vector3(0f, y, 0f), new Vector3(3.2f, 0.1f, 0.1f), Quaternion.identity, mat.iron);
            RuinBox(gate, new Vector3(0f, 1.5f, 0f), new Vector3(3.2f, 3f, 0.3f));
            return gate;
        }

        /// <summary>
        /// O prédio: plataforma (um colisor; a borda é a parede do kit afundada) com piso de lajes, a escadaria e as estátuas da entrada, e
        /// as paredes de todas as salas e corredores (local, chão em 0,9; os três corredores têm 2,94 m por dentro):
        /// vestíbulo V (x 8…14, z −4…4; entrada a leste), capela C (x −8…8, z −4…4; a grade ao sul em x = 4), corredor 1 (z 4…7, x −8…14),
        /// sala 1 (x −14…−8, z 4…12), corredor 2 (x −11…−8, z −4…4), sala 2 (x −14…−8, z −12…−4), corredor 3 (z −7…−4, x −8…2) e
        /// sala 3 (x 2…12, z −10…−4; o atalho para o vestíbulo em x = 10).
        /// </summary>
        static void ErmidaBuilding(Transform root, RuinMats mat, out Transform gate, out Transform shortcut, out Transform entrance)
        {
            const float p = ErmidaPlinth, ex = PlinthHalfX;
            var b = new GameObject("Ermida_Predio").transform;
            b.SetParent(root, false);
            // A plataforma segue o contorno do prédio (um retângulo por parte, 1 m de folga para fora): sem terraço vazio em volta.
            var parts = ErmidaParts;
            var rim = new GameObject("Borda").transform;
            rim.SetParent(b, false);
            System.Func<int, string> wall = i => "Wall";
            foreach (var r in parts)
            {
                var o = new Rect(r.xMin - 1f, r.yMin - 1f, r.width + 2f, r.height + 2f);
                RuinBox(b, new Vector3(o.center.x, p * 0.5f, o.center.y), new Vector3(o.width, p, o.height));
                // Fundo escuro debaixo das lajes (pelos furos do piso aparecia o verde do chão lá embaixo).
                Part(PrimitiveType.Cube, "Fundo_Piso", b, new Vector3(o.center.x, p - 0.12f, o.center.y), new Vector3(o.width - 0.1f, 0.04f, o.height - 0.1f),
                    Quaternion.identity, RuinsMaterial("Piso_Fundo"));
                RuinWallRun(rim, new Vector2(o.xMin, o.yMin), new Vector2(o.xMax, o.yMin), p - 2f, wall, 0f, "Borda", occlude: false);
                RuinWallRun(rim, new Vector2(o.xMax, o.yMin), new Vector2(o.xMax, o.yMax), p - 2f, wall, 0f, "Borda", occlude: false);
                RuinWallRun(rim, new Vector2(o.xMax, o.yMax), new Vector2(o.xMin, o.yMax), p - 2f, wall, 0f, "Borda", occlude: false);
                RuinWallRun(rim, new Vector2(o.xMin, o.yMax), new Vector2(o.xMin, o.yMin), p - 2f, wall, 0f, "Borda", occlude: false);
            }

            // Piso: lajes de 2 m (mais escuras que as paredes); losangos no vestíbulo e na nave da capela; a laje com terra da árvore.
            var floor = new GameObject("Piso").transform;
            floor.SetParent(b, false);
            var rng = new System.Random(19);
            for (int ix = -7; ix <= 7; ix++)
            for (int iz = -6; iz <= 6; iz++)
            {
                float x = ix * 2f, z = iz * 2f;
                if (!InsideErmida(x, z, 0.01f)) continue;
                bool aisle = iz == 0 && x > -7f;
                string tile = aisle ? "Floor_Diamond" : ix == 1 && iz == 1 ? "Floor_Tree"
                    : rng.NextDouble() switch { < 0.5 => "Floor_Standard", < 0.78 => "Floor_Squares", _ => "Floor_SquareLarge" };
                RuinsRecolor(RuinPiece(floor, tile, new Vector3(x, p, z), (float)rng.Next(4) * 90f, "Laje"), "Piso_");
            }

            var w = new GameObject("Paredes").transform;
            w.SetParent(b, false);
            // Contorno de fora (paredes altas; anti-horário, a parede para fora). Leste: a entrada no meio.
            RuinHighWall(w, new Vector2(14f, -4f), new Vector2(14f, -2f), "Leste", 1);
            RuinDoorway(w, "Wall_ArchGothic", new Vector2(14f, 0f), Vector2.up, "Entrada");
            Occluder(RuinPiece(w, "Wall_Hole", new Vector3(14f, p + 4f, 0f), YawAlong(Vector2.up), "Rosacea").gameObject);
            RuinHighWall(w, new Vector2(14f, 2f), new Vector2(14f, 7f), "Leste", 2);
            RuinHighWall(w, new Vector2(14f, 7f), new Vector2(-8f, 7f), "Norte_Corredor1", 3);
            RuinHighWall(w, new Vector2(-8f, 7f), new Vector2(-8f, 12f), "Sala1_Leste", 4);
            RuinHighWall(w, new Vector2(-8f, 12f), new Vector2(-14f, 12f), "Sala1_Norte", 5);
            RuinHighWall(w, new Vector2(-14f, 12f), new Vector2(-14f, 4f), "Sala1_Oeste", 6);
            RuinHighWall(w, new Vector2(-14f, 4f), new Vector2(-11f, 4f), "Sala1_Sul", 7);
            RuinHighWall(w, new Vector2(-11f, 4f), new Vector2(-11f, -4f), "Corredor2_Oeste", 8);
            RuinHighWall(w, new Vector2(-11f, -4f), new Vector2(-14f, -4f), "Sala2_Norte", 9);
            RuinHighWall(w, new Vector2(-14f, -4f), new Vector2(-14f, -12f), "Sala2_Oeste", 10);
            RuinHighWall(w, new Vector2(-14f, -12f), new Vector2(-8f, -12f), "Sala2_Sul", 11);
            RuinHighWall(w, new Vector2(-8f, -12f), new Vector2(-8f, -7f), "Sala2_Leste", 12);
            // Corredor 3: a parede sul tem as janelas gradeadas dos dardos (vãos em x = −7, −5, −3, −1, 1).
            RuinWallRun(w, new Vector2(-8f, -7f), new Vector2(2f, -7f), p, i => "Window_Bars", 2f, "Corredor3_Sul");
            RuinWallRun(w, new Vector2(-8f, -7f), new Vector2(2f, -7f), p + 2f, i => i % 2 == 0 ? "Wall_Broken" : null, 0f, "Corredor3_Sul_Alta");
            RuinHighWall(w, new Vector2(2f, -7f), new Vector2(2f, -10f), "Sala3_Oeste", 13);
            RuinHighWall(w, new Vector2(2f, -10f), new Vector2(12f, -10f), "Sala3_Sul", 14);
            RuinHighWall(w, new Vector2(12f, -10f), new Vector2(12f, -4f), "Sala3_Leste", 15);
            RuinHighWall(w, new Vector2(12f, -4f), new Vector2(14f, -4f), "Vestibulo_Sul", 16);

            // Paredes de dentro. A capela (alta, o coração da ermida): norte com o corredor 1, oeste com o corredor 2, sul com o corredor 3 e a
            // sala 3 (a grade em x = 4), leste com o vestíbulo. As paredes da capela ficam do lado de dentro dela (os corredores limpos).
            RuinHighWall(w, new Vector2(-8f, 4f), new Vector2(8f, 4f), "Capela_Norte", 17);
            RuinHighWall(w, new Vector2(-8f, -4f), new Vector2(-8f, 4f), "Capela_Oeste", 18);
            RuinHighWall(w, new Vector2(8f, 4f), new Vector2(8f, -4f), "Capela_Leste", 19);
            RuinHighWall(w, new Vector2(8f, -4f), new Vector2(6f, -4f), "Capela_Sul", 20);
            RuinDoorway(w, "Wall_ArchGothic", new Vector2(4f, -4f), Vector2.left, "Portao_Capela");
            RuinHighWall(w, new Vector2(2f, -4f), new Vector2(-8f, -4f), "Capela_Sul", 21);
            // Vestíbulo: ao norte a porta para o corredor 1 (x 10…14), ao sul o atalho da sala 3 (x 8…12, com grade).
            RuinLowWall(w, new Vector2(8f, 4f), new Vector2(10f, 4f), "Vestibulo_Norte", 22);
            RuinDoorway(w, "Wall_ArchRound", new Vector2(12f, 4f), Vector2.right, "Porta_Corredor1");
            RuinDoorway(w, "Wall_ArchRound_Overgrown", new Vector2(10f, -4f), Vector2.left, "Atalho");
            // Colunas nos cantos de fora.
            foreach (var c in new[] { new Vector2(14f, 7f), new Vector2(14f, -4f), new Vector2(-14f, 12f), new Vector2(-14f, -12f), new Vector2(12f, -10f),
                         new Vector2(-8f, 12f), new Vector2(-8f, -12f), new Vector2(2f, -10f) })
                Occluder(RuinPiece(w, "Column_Square", new Vector3(c.x, p, c.y), 0f, "Coluna_Canto").gameObject);

            // Grades: a da capela e a do atalho (barras ao longo de X; a passagem é em Z).
            gate = RuinGrate(root, new Vector3(4f, p, -4f), 0f, "Grade", mat);
            shortcut = RuinGrate(root, new Vector3(10f, p, -4f), 0f, "Grade_Atalho", mat);

            // Escadaria (3 lances do kit, 6 m, sobe para oeste) com rampa invisível de 24° e as estátuas dos lados.
            var s = new GameObject("Escadaria").transform;
            s.SetParent(root, false);
            foreach (float z in new[] { -2f, 0f, 2f }) RuinPiece(s, "Stairs", new Vector3(ex + 1f, 0f, z), 0f, "Degraus");
            float angle = Mathf.Atan2(p, 2f) * Mathf.Rad2Deg;
            var ramp = new GameObject("Rampa").transform;
            ramp.SetParent(s, false);
            var rot = Quaternion.Euler(0f, 0f, -angle);
            ramp.SetLocalPositionAndRotation(new Vector3(ex + 1f, p * 0.5f, 0f) - rot * Vector3.up * 0.15f, rot);
            RuinBox(ramp, Vector3.zero, new Vector3(2.4f, 0.3f, 6f));
            var stag = RuinPiece(s, "Statue_Stag", new Vector3(ex + 1.6f, 0f, 4.6f), 90f, "Cervo", 0.8f);
            RuinBox(stag, new Vector3(0.4f, 1.6f, 0f), new Vector3(1.6f, 3.2f, 1.1f));
            var fox = RuinPiece(s, "Statue_Fox", new Vector3(ex + 1.6f, 0f, -4.6f), 90f, "Raposa", 0.8f);
            RuinBox(fox, new Vector3(0f, 1.5f, 0f), new Vector3(1.2f, 3f, 1.3f));
            Occluder(stag.gameObject);
            Occluder(fox.gameObject);
            entrance = new GameObject("Entrada_Ruina").transform;
            entrance.SetParent(root, false);
            entrance.SetLocalPositionAndRotation(new Vector3(ex + 3.5f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));
        }

        /// <summary>
        /// Os três braseiros no vestíbulo, encostados na parede da capela (o de cima é o dos espinhos, o do meio o do fogo, o de baixo o
        /// dos dardos); o fogo de cada um começa apagado. Ordem devolvida: espinhos, dardos, fogo.
        /// </summary>
        static GameObject[] ErmidaBraziers(Transform root)
        {
            var spots = new[] { new Vector3(9.1f, ErmidaPlinth, 2.2f), new Vector3(9.1f, ErmidaPlinth, -2.2f), new Vector3(9.1f, ErmidaPlinth, 0f) };
            var fires = new GameObject[spots.Length];
            for (int k = 0; k < spots.Length; k++)
            {
                var b = new GameObject($"Braseiro_{k + 1}").transform;
                b.SetParent(root, false);
                b.localPosition = spots[k];
                RuinPiece(b, "Column_Round_Short", Vector3.zero, k * 40f, "Pedestal", 0.75f);
                Model(Kit.Survival, "campfire-pit", b, Vector3.up * 1.37f, 0f, 4f, "Bacia");
                var col = b.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.75f, 0f);
                col.size = new Vector3(1.1f, 1.5f, 1.1f);
                var fire = new GameObject("Fogo_Aceso").transform;
                fire.SetParent(b, false);
                Fire(fire, b.TransformPoint(Vector3.up * 1.55f), 0.75f);
                fire.gameObject.SetActive(false);
                fires[k] = fire.gameObject;
            }
            return fires;
        }

        /// <summary>
        /// Alavanca presa na face da frente (+Z) de um pedestal, baixa (pivô a 0,25 m), na altura da mão do capitão no "Pulling Lever"
        /// do Mixamo (pega a ~0,7 m, 0,3 m à frente dele, e puxa até o quadril): o cabo de 0,57 m vira no X do pivô, de 30° para 75°
        /// na direção de quem puxa. <paramref name="bothSides"/>: pivô em cima do pedestal, puxa-se pela frente ou por trás.
        /// </summary>
        static RuinLever Lever(Transform root, Vector3 local, float yaw, string name, GameObject fire, RuinMats mat, bool bothSides = false)
        {
            var go = new GameObject("Alavanca_" + name).transform;
            go.SetParent(root, false);
            go.SetLocalPositionAndRotation(local, Quaternion.Euler(0f, yaw, 0f));
            var pedestal = Part(PrimitiveType.Cube, "Pedestal", go, new Vector3(0f, 0.5f, 0f), Vector3.one, Quaternion.identity, mat.stone);
            var col = go.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1f, 1f, 1f);
            var pivot = new GameObject("Cabo").transform;
            pivot.SetParent(go, false);
            if (bothSides)
            {
                // Base baixa e estreita (0,5 m), o cabo em pé no meio: o capitão para a 0,6 m dele, de qualquer lado.
                pedestal.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.15f, 0f), Quaternion.identity);
                pedestal.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
                col.center = new Vector3(0f, 0.15f, 0f);
                col.size = new Vector3(0.5f, 0.3f, 0.5f);
                pivot.localPosition = new Vector3(0f, 0.3f, 0f);
                Part(PrimitiveType.Cube, "Suporte", go, new Vector3(0f, 0.32f, 0f), new Vector3(0.3f, 0.06f, 0.4f), Quaternion.identity, mat.iron);
                Part(PrimitiveType.Cylinder, "Eixo", pivot, Vector3.zero, new Vector3(0.12f, 0.1f, 0.12f), Quaternion.Euler(0f, 0f, 90f), mat.iron);
            }
            else
            {
                pivot.localPosition = new Vector3(0f, 0.25f, 0.56f);
                Part(PrimitiveType.Cube, "Suporte", go, new Vector3(0f, 0.25f, 0.53f), new Vector3(0.3f, 0.3f, 0.08f), Quaternion.identity, mat.iron);
                Part(PrimitiveType.Cylinder, "Eixo", pivot, Vector3.zero, new Vector3(0.12f, 0.1f, 0.12f), Quaternion.Euler(0f, 0f, 90f), mat.iron);
            }
            Part(PrimitiveType.Cylinder, "Haste", pivot, new Vector3(0f, 0.28f, 0f), new Vector3(0.06f, 0.28f, 0.06f), Quaternion.identity, mat.wood);
            Part(PrimitiveType.Sphere, "Ponta", pivot, new Vector3(0f, 0.57f, 0f), new Vector3(0.13f, 0.13f, 0.13f), Quaternion.identity, mat.brass);
            var lever = go.gameObject.AddComponent<RuinLever>();
            lever.Configure("Alavanca", pivot, fire, bothSides);
            return lever;
        }

        /// <summary>Corredor 1, a cripta (z 4…7, de leste para oeste): 5 lajes de espinhos que sobem em onda e arcos; a alavanca na sala 1.</summary>
        static RuinLever SpikeWing(Transform root, RuinMats mat, GameObject fire)
        {
            const float p = ErmidaPlinth, z = 5.5f;
            var g = new GameObject("Corredor1_Espinhos").transform;
            g.SetParent(root, false);
            foreach (float x in new[] { -6.6f, -2.3f, 2.1f, 7.6f }) CorridorArch(g, new Vector3(x, p, z), YawAlong(Vector2.up));
            var traps = new List<RuinTrap>();
            for (int k = 0; k < 5; k++)
            {
                var plate = new GameObject($"Espinhos_{k + 1}").transform;
                plate.SetParent(root, false);
                plate.localPosition = new Vector3(-3.4f + k * 2.2f, p, z);
                Part(PrimitiveType.Cube, "Laje", plate, new Vector3(0f, 0.03f, 0f), new Vector3(2.1f, 0.06f, 2.6f), Quaternion.identity, mat.slab);
                var spikes = new GameObject("Pontas").transform;
                spikes.SetParent(plate, false);
                for (int i = 0; i < 4; i++)
                for (int j = 0; j < 5; j++)
                    Part(PrimitiveType.Cylinder, "Ponta", spikes, new Vector3(-0.75f + i * 0.5f, 0.3f, -1.0f + j * 0.5f), new Vector3(0.06f, 0.3f, 0.06f),
                        Quaternion.identity, mat.iron);
                spikes.localPosition = new Vector3(0f, -0.55f, 0f);
                var trap = plate.gameObject.AddComponent<RuinTrap>();
                // A onda anda no sentido de quem chega (de leste para oeste): a laje mais a leste (k = 4) sobe primeiro e dá para ir
                // atrás dela. Deslocamento maior = sobe antes.
                trap.Configure(RuinTrap.Kind.Spikes, 2.6f, k * 0.52f, 0.5f, 0.9f, 18f);
                trap.SetBox(new Vector3(0f, 0.6f, 0f), new Vector3(1.05f, 0.6f, 1.3f));
                trap.SetSpikes(spikes);
                traps.Add(trap);
            }
            // Sala 1: a alavanca na parede oeste, de frente para quem chega.
            return Disarming(Lever(root, new Vector3(-13.1f, p, 8f), 90f, "Sala1", fire, mat), traps);
        }

        /// <summary>Corredor 2, o claustro (x −11…−8, de norte para sul): 3 grades de fogo em onda e arcos; a alavanca na sala 2.</summary>
        static RuinLever FireWing(Transform root, RuinMats mat, GameObject fire)
        {
            const float p = ErmidaPlinth, x = -9.5f;
            var g = new GameObject("Corredor2_Fogo").transform;
            g.SetParent(root, false);
            foreach (float z in new[] { 3.75f, -3.75f }) CorridorArch(g, new Vector3(x, p, z), 0f);
            var smokeMat = FxMaterial("M_FX_Fumaca", "smoke_04", additive: false);
            var traps = new List<RuinTrap>();
            float[] spots = { 2.4f, 0f, -2.4f };
            for (int n = 0; n < spots.Length; n++)
            {
                float z = spots[n];
                var grate = new GameObject($"Fogo_{n + 1}").transform;
                grate.SetParent(root, false);
                grate.localPosition = new Vector3(x, p, z);
                var plate = Part(PrimitiveType.Cube, "Grade", grate, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.06f, 1.9f), Quaternion.identity, mat.iron);
                for (int i = -2; i <= 2; i++)
                    Part(PrimitiveType.Cube, "Fresta", grate, new Vector3(i * 0.5f, 0.065f, 0f), new Vector3(0.12f, 0.02f, 1.6f), Quaternion.identity, mat.hole);
                var flames = FireJet(grate);
                var smoke = WarnSmoke(grate, smokeMat);
                Light light = null;
                if (n != 1)
                {
                    var lg = new GameObject("Luz").transform;
                    lg.SetParent(grate, false);
                    lg.localPosition = new Vector3(0f, 1.2f, 0f);
                    light = lg.gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.55f, 0.2f);
                    light.range = 7f;
                    light.intensity = 2.4f;
                    light.shadows = LightShadows.None;
                    light.enabled = false;
                }
                var trap = grate.gameObject.AddComponent<RuinTrap>();
                // Uma grade de cada vez, de norte para sul (há sempre duas apagadas para atravessar no ritmo).
                trap.Configure(RuinTrap.Kind.Fire, 3f, (2 - n) * 1f, 0.7f, 1.2f, 12f);
                trap.SetBox(new Vector3(0f, 0.9f, 0f), new Vector3(1.35f, 0.9f, 0.95f));
                trap.SetFire(flames, smoke, light);
                trap.SetWarning(plate.GetComponent<Renderer>(), mat.iron, mat.ember);
                traps.Add(trap);
            }
            return Disarming(Lever(root, new Vector3(-13.1f, p, -8f), 90f, "Sala2", fire, mat), traps);
        }

        /// <summary>Corredor 3, a galeria (z −7…−4, de oeste para leste): 5 janelas gradeadas na parede sul que atiram dardos; a alavanca na sala 3.</summary>
        static RuinLever DartWing(Transform root, RuinMats mat, GameObject fire)
        {
            const float p = ErmidaPlinth;
            var g = new GameObject("Corredor3_Dardos").transform;
            g.SetParent(root, false);
            foreach (float x in new[] { -4f, 0f }) CorridorArch(g, new Vector3(x, p, -5.5f), YawAlong(Vector2.up));
            var traps = new List<RuinTrap>();
            for (int k = 0; k < 5; k++)
            {
                float x = -7f + k * 2f, y = k % 2 == 0 ? 0.95f : 1.25f;
                var shooter = new GameObject($"Dardo_{k + 1}").transform;
                shooter.SetParent(root, false);
                // Na janela da parede sul (a face de dentro fica em z = −6,97).
                shooter.localPosition = new Vector3(x, p, -6.9f);
                var hole = Part(PrimitiveType.Cube, "Buraco", shooter, new Vector3(0f, y, -0.12f), new Vector3(0.3f, 0.3f, 0.06f), Quaternion.identity, mat.hole);
                var muzzle = new GameObject("Boca").transform;
                muzzle.SetParent(shooter, false);
                muzzle.localPosition = new Vector3(0f, y, 0.12f);
                var dart = new GameObject("Dardo").transform;
                dart.SetParent(shooter, false);
                Part(PrimitiveType.Cylinder, "Haste", dart, Vector3.zero, new Vector3(0.03f, 0.22f, 0.03f), Quaternion.Euler(90f, 0f, 0f), mat.wood);
                Part(PrimitiveType.Cube, "Ponta", dart, new Vector3(0f, 0f, 0.25f), new Vector3(0.05f, 0.05f, 0.08f), Quaternion.identity, mat.iron);
                dart.gameObject.SetActive(false);
                var trap = shooter.gameObject.AddComponent<RuinTrap>();
                // A fila de dardos anda no sentido de quem chega (de oeste para leste): o do oeste (k = 0) atira primeiro.
                trap.Configure(RuinTrap.Kind.Dart, 2.4f, (4 - k) * 0.48f, 0.6f, 0.3f, 15f);
                trap.SetDart(muzzle, dart, 3.2f);
                trap.SetWarning(hole.GetComponent<Renderer>(), mat.hole, mat.warn);
                traps.Add(trap);
            }
            // Sala 3: a alavanca na parede sul, de frente para a grade da capela.
            return Disarming(Lever(root, new Vector3(7f, p, -9.1f), 0f, "Sala3", fire, mat), traps);
        }

        /// <summary>
        /// O que mora dentro: a capela (altar com a raposa de pedra, velas, lanternas, colunas, a árvore do piso, cacos), a sala 1 (cripta:
        /// caixões e túmulos), a sala 2 (claustro: mato, vasos quebrados, árvore seca), a sala 3 (o cervo de pedra e estandartes) e o
        /// vestíbulo (estandartes e mato).
        /// </summary>
        static void ErmidaRooms(Transform root)
        {
            const float p = ErmidaPlinth;
            var g = new GameObject("Ermida_Dentro").transform;
            g.SetParent(root, false);
            var rng = new System.Random(23);

            // Capela.
            var block = Part(PrimitiveType.Cube, "Pedra_Altar", g, new Vector3(-6.3f, p + 0.5f, 0f), new Vector3(1.3f, 1f, 2.4f), Quaternion.identity,
                RuinsMaterial("Main2"), keepCollider: true);
            Occluder(block);
            RuinPiece(g, "Floor_SquareLarge", new Vector3(-6.3f, p + 1.03f, 0f), 0f, "Laje_Altar").localScale = new Vector3(0.75f, 1f, 1.3f);
            var fox = RuinPiece(g, "Statue_Fox", new Vector3(-7.25f, p, 0f), 90f, "Raposa_Santo", 0.8f);
            RuinBox(fox, new Vector3(0f, 1.5f, 0f), new Vector3(1.2f, 3f, 1.3f));
            Occluder(fox.gameObject);
            foreach (float z in new[] { -1.7f, 1.7f })
            {
                RuinPiece(g, "Candles_2", new Vector3(-6.4f, p, z), z * 40f, "Velas");
                PostLantern(g, root.TransformPoint(new Vector3(-3.4f, p, z * 1.5f)), "Lanterna_Altar", range: 7f);
            }
            foreach (float x in new[] { -1.5f, 5.5f })
            foreach (float z in new[] { -2.6f, 2.6f })
            {
                if (x > 5f) continue; // ao sul tapava o vão da grade; ao norte, o alçapão das catacumbas (C5)
                var col = RuinPiece(g, "Column_Round", new Vector3(x, p, z), 0f, "Coluna", 0.9f);
                var cap = col.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.3f;
                cap.height = 3.6f;
                cap.center = Vector3.up * 1.8f;
                Occluder(col.gameObject);
            }
            RuinPiece(g, "Tree_2", new Vector3(2f, p, 2f), 35f, "Arvore_Capela");
            RuinPiece(g, "Pot1_Broken", new Vector3(-6.9f, p, 3.3f), 20f, "Vaso");
            RuinPiece(g, "Pot3_Broken", new Vector3(-6.6f, p, -3.3f), 70f, "Vaso");
            RuinPiece(g, "Bricks", new Vector3(6.6f, p, -2.9f), 15f, "Tijolos");

            // Sala 1 (cripta): caixões e túmulos encostados nas paredes, a caveira e velas perto da alavanca. A paleta do Graveyard Kit
            // puxa para o azul: tingida para o tom da pedra (só a Ermida usa o kit).
            var graveMat = KitMaterial(Kit.Graveyard);
            graveMat.SetColor("_BaseColor", new Color(1f, 0.84f, 0.64f));
            UnityEditor.EditorUtility.SetDirty(graveMat);
            Model(Kit.Graveyard, "coffin-old", g, new Vector3(-11f, p, 11.1f), 90f, 2f, "Caixao");
            Model(Kit.Graveyard, "coffin", g, new Vector3(-9.2f, p, 10.9f), 95f, 2f, "Caixao");
            Model(Kit.Graveyard, "gravestone-cross", g, new Vector3(-12.9f, p, 11f), 135f, 2f, "Tumulo");
            Model(Kit.Graveyard, "gravestone-broken", g, new Vector3(-12.9f, p, 5f), 60f, 2f, "Tumulo");
            RuinPiece(g, "Skull", new Vector3(-12.7f, p, 9.4f), 120f, "Caveira");
            RuinPiece(g, "Candles_1", new Vector3(-12.8f, p, 6.6f), 0f, "Velas");
            RuinPiece(g, "Candles_2", new Vector3(-9.1f, p, 8.6f), 0f, "Velas");

            // Sala 2 (claustro): mato, vasos quebrados e uma árvore seca no canto.
            RuinPiece(g, "DeadTree_2", new Vector3(-9.3f, p, -10.8f), 30f, "Arvore_Seca");
            RuinPiece(g, "Pot2_Broken", new Vector3(-12.9f, p, -11f), 10f, "Vaso");
            RuinPiece(g, "Pot1", new Vector3(-12.8f, p, -5.1f), 40f, "Vaso");
            RuinPiece(g, "Bricks", new Vector3(-10f, p, -6.2f), 80f, "Tijolos");

            // Sala 3: o cervo de pedra no canto e estandartes nas paredes.
            var stag = RuinPiece(g, "Statue_Stag", new Vector3(10.6f, p, -8.8f), -45f, "Cervo_Santo", 0.7f);
            RuinBox(stag, new Vector3(0.4f, 1.4f, 0f), new Vector3(1.4f, 2.8f, 1f));
            Occluder(stag.gameObject);
            RuinPiece(g, "Flag_Wall", new Vector3(4.2f, p + 0.1f, -9.7f), 0f, "Estandarte");
            RuinPiece(g, "Flag_Wall", new Vector3(11.7f, p + 0.1f, -6.6f), -90f, "Estandarte");
            // Vestíbulo: estandartes dos lados da entrada.
            RuinPiece(g, "Flag_Wall", new Vector3(13.7f, p + 0.1f, 3f), -90f, "Estandarte");
            RuinPiece(g, "Flag_Wall", new Vector3(13.7f, p + 0.1f, -3f), -90f, "Estandarte");

            // Mato entre as lajes (fora dos corredores das armadilhas).
            for (int k = 0; k < 26; k++)
            {
                float x = -13f + (float)rng.NextDouble() * 26f, z = -11f + (float)rng.NextDouble() * 22f;
                if (z > 4f && z < 7f && x > -8f) continue;
                if (x > -11f && x < -8f && z > -4f && z < 4f) continue;
                if (z > -7f && z < -4f && x > -8f && x < 2f) continue;
                if (!InsideErmida(x, z, -0.5f)) continue;
                // Nem no altar e no baú, nem na frente das alavancas.
                if (x < -3f && Mathf.Abs(z) < 2.2f) continue;
                if (Mathf.Abs(x + 12f) < 2f && Mathf.Abs(Mathf.Abs(z) - 8f) < 1.5f) continue;
                if (Mathf.Abs(x - 7f) < 1.5f && z < -7f) continue;
                if (x > 5f && z > 0.8f && z < 4f) continue; // o alçapão das catacumbas (C5)
                RuinPiece(g, "Grass", new Vector3(x, p, z), (float)rng.NextDouble() * 360f, "Mato", 0.7f + (float)rng.NextDouble() * 0.5f);
            }
        }

        /// <summary>As armadilhas da ala param quando a alavanca dela é puxada (só a ida é perigosa).</summary>
        static RuinLever Disarming(RuinLever lever, List<RuinTrap> traps)
        {
            foreach (var t in traps) t.SetLever(lever);
            return lever;
        }

        /// <summary>Jato de fogo da grade: cubinhos de fogo subindo rápido (como o fogo do porto), parado até a armadilha ligar.</summary>
        static ParticleSystem FireJet(Transform parent)
        {
            var go = new GameObject("Chamas");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.05f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 110f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.4f, 1.5f, 0.05f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = FireParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Fumaça fina saindo das frestas: o aviso do fogo.</summary>
        static ParticleSystem WarnSmoke(Transform parent, Material smoke)
        {
            var go = new GameObject("Fumaca_Aviso");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.33f, 0.32f, 0.7f), new Color(0.5f, 0.48f, 0.45f, 0.5f));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.4f, 1.5f, 0.05f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smoke;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>
        /// Os detalhes de dentro (19/10): tochas de ferro nas paredes de todas as salas e corredores (a chama sempre acesa; uma em cada duas
        /// com luz de verdade à noite e na tempestade, como as lanternas do porto), estandartes nos arcos das portas, entulho (tijolos,
        /// uma coluna caída), caveiras na cripta e velas perto das alavancas.
        /// </summary>
        static void ErmidaDetails(Transform root)
        {
            const float p = ErmidaPlinth, h = p + 2f;
            var g = new GameObject("Ermida_Detalhes").transform;
            g.SetParent(root, false);
            // Tochas: (x, z) na face de dentro da parede, a direção para dentro e se tem luz.
            var torches = new (float x, float z, Vector2 inward, bool light)[]
            {
                (13.97f, 3f, Vector2.left, true), (13.97f, -3f, Vector2.left, false),             // vestíbulo, dos lados da entrada
                (11f, 6.97f, Vector2.down, true), (4.3f, 6.97f, Vector2.down, false), (-4.4f, 6.97f, Vector2.down, true), // corredor 1
                (-11f, 11.97f, Vector2.down, true), (-13.97f, 5.6f, Vector2.right, false),       // sala 1
                (-10.97f, 3.75f, Vector2.right, false), (-10.97f, -3.75f, Vector2.right, false), // corredor 2 (o fogo já ilumina)
                (-11f, -11.97f, Vector2.up, true), (-13.97f, -5.6f, Vector2.right, false),       // sala 2
                (-6f, -4.03f, Vector2.down, true), (-2f, -4.03f, Vector2.down, false), (1.2f, -4.03f, Vector2.down, false), // corredor 3
                (4.5f, -9.97f, Vector2.up, true), (9.5f, -9.97f, Vector2.up, false), (11.97f, -6.5f, Vector2.left, true), // sala 3
                (-4f, 3.75f, Vector2.down, false), (3f, 3.75f, Vector2.down, false), (-1f, -3.75f, Vector2.up, false), // capela
                (7.75f, 1.2f, Vector2.left, true), // ao lado do alçapão (C5), com luz
            };
            foreach (var t in torches)
            {
                var at = new Vector3(t.x + t.inward.x * 0.29f, h + 0.2f, t.z + t.inward.y * 0.29f);
                var torch = RuinPiece(g, "Torch", at, Mathf.Atan2(t.inward.x, t.inward.y) * Mathf.Rad2Deg, "Tocha");
                if (!t.light) continue;
                var lightGo = new GameObject("LuzDaTocha");
                lightGo.transform.SetParent(torch, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.75f, 0.25f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.58f, 0.25f);
                light.range = 6.5f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                torch.gameObject.AddComponent<NightLight>().Configure(light, 2.4f, null);
            }

            // Estandartes pendurados nos arcos das portas (o vão de 3 m continua livre embaixo deles).
            RuinPiece(g, "Flag_GothicArch", new Vector3(14f, p, 0f), YawAlong(Vector2.up), "Estandarte_Entrada");
            RuinPiece(g, "Flag_RoundArch", new Vector3(12f, p, 4f), YawAlong(Vector2.right), "Estandarte_Corredor1");

            // Entulho e ossos.
            RuinPiece(g, "Bricks", new Vector3(12.8f, p, -3.1f), 70f, "Tijolos");
            RuinPiece(g, "Bricks", new Vector3(11.2f, p, -5f), 160f, "Tijolos");
            RuinPiece(g, "Brick", new Vector3(10.2f, p, -8.9f), 30f, "Tijolo");
            RuinPiece(g, "Bricks", new Vector3(-13f, p, 6.2f), 200f, "Tijolos");
            var fallen = RuinPiece(g, "Column_Round", new Vector3(6.2f, p + 0.27f, -9.1f), 0f, "Coluna_Caida", 0.8f);
            fallen.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var fallenBox = new GameObject("Colisor").transform;
            fallenBox.SetParent(g, false);
            fallenBox.localPosition = new Vector3(4.6f, p + 0.27f, -9.1f);
            RuinBox(fallenBox, Vector3.zero, new Vector3(3.2f, 0.5f, 0.5f));
            foreach (var (x, z, yaw) in new[] { (8.6f, 6.5f, 30f), (-6.2f, 4.5f, 200f), (-9.2f, 11.4f, 80f), (-12.8f, 10.8f, 300f) })
                RuinPiece(g, "Skull", new Vector3(x, p, z), yaw, "Caveira");
            // Velas perto das alavancas das salas 2 e 3.
            RuinPiece(g, "Candles_1", new Vector3(-13.2f, p, -9.6f), 40f, "Velas");
            RuinPiece(g, "Candles_2", new Vector3(8.6f, p, -9.3f), 10f, "Velas");
        }

        /// <summary>
        /// Em volta do prédio: anel de colunas (umas inteiras, outras quebradas), colunas e uma caída na chegada, arbustos e mato encostados
        /// na plataforma, árvores secas e tijolos soltos.
        /// </summary>
        static void ErmidaDecor(Transform root)
        {
            var g = new GameObject("Ruinas_Ermida").transform;
            g.SetParent(root, false);
            var rng = new System.Random(31);
            for (int k = 0; k < 12; k++)
            {
                float a = (k * 30f + 8f) * Mathf.Deg2Rad;
                var local = new Vector3(Mathf.Cos(a) * 22.5f, 0f, Mathf.Sin(a) * 19.5f);
                // A chegada pela trilha (leste) fica livre.
                if (local.x > 15f && Mathf.Abs(local.z) < 9f) continue;
                bool broken = rng.NextDouble() < 0.55;
                var piece = RuinPiece(g, broken ? "Column_Round_Short" : "Column_Round", local, (float)rng.NextDouble() * 360f, "Coluna",
                    broken ? 1f : 0.8f + (float)rng.NextDouble() * 0.2f);
                var c = piece.gameObject.AddComponent<CapsuleCollider>();
                c.radius = 0.3f;
                c.height = broken ? 1.8f : 3.6f;
                c.center = Vector3.up * c.height * 0.5f;
                Occluder(piece.gameObject);
            }
            // Chegada: duas fileiras de colunas até a escadaria, uma caída.
            foreach (float x in new[] { 20f, 23.5f })
            foreach (float z in new[] { -3.6f, 3.6f })
            {
                bool broken = x > 22f && z > 0f;
                var piece = RuinPiece(g, broken ? "Column_Round_Short" : "Column_Round", new Vector3(x, 0f, z), x * 13f, "Coluna_Chegada", broken ? 1f : 0.85f);
                var c = piece.gameObject.AddComponent<CapsuleCollider>();
                c.radius = 0.3f;
                c.height = broken ? 1.8f : 3.4f;
                c.center = Vector3.up * c.height * 0.5f;
                Occluder(piece.gameObject);
            }
            var fallen = RuinPiece(g, "Column_Round", new Vector3(24.5f, 0.3f, 6.5f), 0f, "Coluna_Caida", 0.85f);
            fallen.localRotation = Quaternion.Euler(0f, 25f, 90f);
            RuinPiece(g, "Bricks", new Vector3(22.5f, 0f, 5.2f), 40f, "Tijolos");
            RuinPiece(g, "Bricks", new Vector3(-17f, 0f, -9f), 200f, "Tijolos");
            RuinPiece(g, "DeadTree_3", new Vector3(-19f, 0f, 13f), 20f, "Arvore_Seca");
            RuinPiece(g, "DeadTree_1", new Vector3(17.5f, 0f, -12f), 140f, "Arvore_Seca");
            RuinPiece(g, "Tree_1", new Vector3(-20f, 0f, -13.5f), 80f, "Arvore");
            // Arbustos e mato na base da plataforma (menos na escadaria).
            string[] bushes = { "Bush_2x1", "Bush_Round", "Bush_1x1", "Bush_2x2", "Bush_Large" };
            for (int k = 0; k < 26; k++)
            {
                float t = (float)rng.NextDouble() * 4f;
                int side = Mathf.Min(3, (int)t);
                float u = t - side;
                Vector3 at = side switch
                {
                    0 => new Vector3(Mathf.Lerp(-PlinthHalfX, PlinthHalfX, u), 0f, PlinthHalfZ + 0.8f),
                    1 => new Vector3(Mathf.Lerp(-PlinthHalfX, PlinthHalfX, u), 0f, -PlinthHalfZ - 0.8f),
                    2 => new Vector3(-PlinthHalfX - 0.8f, 0f, Mathf.Lerp(-PlinthHalfZ, PlinthHalfZ, u)),
                    _ => new Vector3(PlinthHalfX + 0.8f, 0f, (u < 0.5f ? -1f : 1f) * Mathf.Lerp(6f, PlinthHalfZ, Mathf.Abs(u - 0.5f) * 2f)),
                };
                RuinPiece(g, bushes[k % bushes.Length], at, (float)rng.NextDouble() * 360f, "Arbusto", 0.8f + (float)rng.NextDouble() * 0.5f);
            }
            for (int k = 0; k < 28; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 17f + (float)rng.NextDouble() * 6f;
                var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.85f);
                if (at.x > 15f && Mathf.Abs(at.z) < 5f) continue;
                RuinPiece(g, "Grass", at, (float)rng.NextDouble() * 360f, "Mato", 0.8f + (float)rng.NextDouble() * 0.6f);
            }
        }
    }
}
