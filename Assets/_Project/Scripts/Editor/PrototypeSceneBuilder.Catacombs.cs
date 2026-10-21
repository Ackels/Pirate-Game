using System.Collections.Generic;
using Pirates.Ambient;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Catacumbas da Ermida (C5, passo 1, 20/10; pedido do Jhonata: o porão da Ermida com os mortos da Frota de Prata): quando a grade da
    /// capela abre, o alçapão da nave abre junto; F desce (a tela escurece) e o capitão aparece embaixo. O lugar fica longe do mapa
    /// (<see cref="CatacombOrigin"/>, acima do nível do mar: sem água, sem ilhas em volta) e é uma <see cref="CoveredZone"/>: sem sol, céu,
    /// mar, chuva e vento, com tochas e o ar das catacumbas. Caminho: escada → corredor → ossuário (prateleiras de caveiras) → corredor →
    /// cripta (sarcófagos) → corredor → salão do Capelão (altar e o baú). As almas penadas, os morcegos e o Capelão Fantasma vêm nos passos 2
    /// e 3. Layout local (X leste, Z norte), chão em 0.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string CatacombsName = "Catacumbas da Ermida";
        static Vector3 CatacombOrigin => new(-270f, 12f, -3000f);

        /// <summary>As partes (x, z, largura, fundo): escada, alcova da escada, corredor 1, ossuário, corredor 2, cripta, corredor 3, salão.</summary>
        static readonly Rect[] CryptParts =
        {
            new(0f, -4f, 8f, 8f), new(8f, -1.5f, 5f, 3f), new(-10f, -1.5f, 10f, 3f), new(-20f, -5f, 10f, 10f),
            new(-16.5f, -15f, 3f, 10f), new(-22f, -23f, 14f, 8f), new(-8f, -20.5f, 12f, 3f), new(4f, -28f, 16f, 18f),
        };

        const string CryptStone = "Cripta_", CryptFloor = "CriptaPiso_";

        /// <summary>
        /// O alçapão na nave da capela (abre com a grade; a tampa gira na borda de cima) e as catacumbas com a escada de volta.
        /// <paramref name="ermida"/> = raiz da ruína (chão da capela em <see cref="ErmidaPlinth"/>).
        /// </summary>
        static void AddCatacombs(Transform ermida, RuinPuzzle puzzle, LandLootAssets landLoot, LootItems items, RuinMats mat, FootCharacter captain)
        {
            const float p = ErmidaPlinth;
            var root = new GameObject("Catacumbas").transform;
            root.position = CatacombOrigin;

            // Alçapão: a tampa do kit numa dobradiça na borda norte; embaixo, o escuro (aparece com a tampa aberta). No canto nordeste da
            // capela, longe da grade (20/10, Jhonata: no meio da nave ficava escondido e colado na entrada); aberta, a tampa encosta na parede.
            var hatch = new GameObject("Alcapao").transform;
            hatch.SetParent(ermida, false);
            hatch.localPosition = new Vector3(6.7f, p, 2.3f);
            RuinPiece(ermida, "Candles_1", new Vector3(7.5f, p, 0.9f), 30f, "Velas_Alcapao");
            var hinge = new GameObject("Tampa_Eixo").transform;
            hinge.SetParent(hatch, false);
            hinge.localPosition = new Vector3(0f, 0.01f, 0.79f);
            RuinPiece(hinge, "Trapdoor", new Vector3(0f, 0f, -0.79f), 0f, "Tampa");
            // O escuro fica acima das pedras da laje (o topo delas está a 3,3 cm) e abaixo da tampa fechada.
            var hole = Part(PrimitiveType.Cube, "Buraco", hatch, new Vector3(0f, 0.045f, 0f), new Vector3(1.5f, 0.02f, 1.4f), Quaternion.identity, mat.hole);
            var upArrival = new GameObject("Chegada_Capela").transform;
            upArrival.SetParent(ermida, false);
            upArrival.SetLocalPositionAndRotation(new Vector3(4.6f, p + 0.05f, 0.6f), Quaternion.Euler(0f, 180f, 0f));

            CryptFloors(root);
            CryptWalls(root);
            var stairs = CryptStairs(root, mat);
            var downArrival = new GameObject("Chegada_Catacumbas").transform;
            downArrival.SetParent(root, false);
            downArrival.SetLocalPositionAndRotation(new Vector3(6f, 0.05f, 0f), Quaternion.Euler(0f, -90f, 0f));

            hatch.gameObject.AddComponent<RuinPassage>().Configure("Descer às catacumbas", downArrival, CatacombsName,
                "onde dormem os marujos da Frota de Prata", puzzle, hinge, hole);
            stairs.gameObject.AddComponent<RuinPassage>().Configure("Subir para a capela", upArrival, null, null);

            CryptLights(root);
            CryptOssuary(root);
            CryptTombs(root);
            var table = CryptHall(root, landLoot, items);
            var chest = CreateChest(root, "Bau_Catacumbas", "Tesouro das Catacumbas", root.TransformPoint(new Vector3(12f, 0f, -22.6f)), 0f, table,
                landLoot, withPadlock: true, ChestStyle.RuinsGold);

            // Passo 2: as almas penadas e os morcegos (antes do NavMesh: eles não têm colisor no chão que atrapalhe). Passo 3: o Capelão
            // tranca o baú do salão até ser derrotado.
            var chaplain = AddCryptMonsters(root, captain);
            chest.SetLock(chaplain);

            var zone = root.gameObject.AddComponent<CoveredZone>();
            zone.Configure(Object.FindAnyObjectByType<PlayerModeSwitcher>(),
                new Bounds(root.TransformPoint(new Vector3(-1f, 3f, -11.5f)), new Vector3(48f, 16f, 42f)));
            BakeNavMeshVolume("NavMesh_Catacumbas", "_NavMesh_Catacumbas", root.TransformPoint(new Vector3(-1f, -1f, -11.5f)), new Vector3(46f, 12f, 40f));
        }

        // ---------- Construção ----------

        /// <summary>Um colisor de chão por parte (topo em 0) e lajes de ~2 m esticadas para fechar cada parte.</summary>
        static void CryptFloors(Transform root)
        {
            var floor = new GameObject("Piso").transform;
            floor.SetParent(root, false);
            var rng = new System.Random(41);
            var under = RuinsMaterial("CriptaPiso_Fundo");
            foreach (var r in CryptParts)
            {
                RuinBox(floor, new Vector3(r.center.x, -0.5f, r.center.y), new Vector3(r.width, 1f, r.height));
                // Fundo escuro debaixo das lajes (pelas frestas e furos do piso aparecia o preto do vazio).
                Part(PrimitiveType.Cube, "Fundo_Piso", floor, new Vector3(r.center.x, -0.1f, r.center.y), new Vector3(r.width - 0.05f, 0.04f, r.height - 0.05f),
                    Quaternion.identity, under);
                int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / 2f)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / 2f));
                float sx = r.width / nx, sz = r.height / nz;
                for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    string tile = rng.NextDouble() switch { < 0.45 => "Floor_Standard", < 0.8 => "Floor_Squares", _ => "Floor_SquareLarge" };
                    var at = new Vector3(r.xMin + sx * (ix + 0.5f), 0f, r.yMin + sz * (iz + 0.5f));
                    var piece = RuinPiece(floor, tile, at, rng.Next(2) * 180f, "Laje");
                    piece.localScale = new Vector3(sx / 2f, 1f, sz / 2f);
                }
            }
            RuinsRecolor(floor, CryptFloor);
        }

        /// <summary>
        /// Paredes de 4 m (dois andares do kit, só o de baixo com colisor), contornando cada sala no sentido anti-horário (a parede para
        /// fora) com os vãos das portas (arco de 4 m); os corredores só têm os lados compridos.
        /// </summary>
        static void CryptWalls(Transform root)
        {
            var w = new GameObject("Paredes").transform;
            w.SetParent(root, false);
            // Salas: lados 0 sul, 1 leste, 2 norte, 3 oeste; porta = (lado, centro no eixo da parede).
            CryptRoom(w, CryptParts[0], "Escada", 1, (1, 0f), (3, 0f));
            CryptRoom(w, CryptParts[3], "Ossuario", 2, (1, 0f), (0, -15f));
            CryptRoom(w, CryptParts[5], "Cripta", 3, (2, -15f), (1, -19f));
            CryptRoom(w, CryptParts[7], "Salao", 4, (3, -19f));
            // Corredores e a alcova da escada (a ponta leste dela é o escuro da subida).
            CryptCorridor(w, CryptParts[1], "Alcova", true, 5);
            CryptCorridor(w, CryptParts[2], "Corredor1", true, 6);
            CryptCorridor(w, CryptParts[4], "Corredor2", false, 7);
            CryptCorridor(w, CryptParts[6], "Corredor3", true, 8);
            // Arcos soltos atravessando os corredores.
            foreach (float x in new[] { -3f, -7f }) CorridorArch(w, new Vector3(x, 0f, 0f), YawAlong(Vector2.up), CryptStone);
            foreach (float z in new[] { -8.5f, -11.5f }) CorridorArch(w, new Vector3(-15f, 0f, z), 0f, CryptStone);
            foreach (float x in new[] { -4f, 0f }) CorridorArch(w, new Vector3(x, 0f, -19f), YawAlong(Vector2.up), CryptStone);
            // Colunas nos cantos das salas.
            foreach (int k in new[] { 0, 3, 5, 7 })
            {
                var r = CryptParts[k];
                foreach (var c in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
                {
                    var col = RuinPiece(w, "Column_Square", new Vector3(c.x, 0f, c.y), 0f, "Coluna_Canto");
                    RuinsRecolor(col, CryptStone);
                    Occluder(col.gameObject);
                }
            }
        }

        static void CryptRoom(Transform parent, Rect r, string name, int seed, params (int side, float center)[] doors)
        {
            var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[(side + 1) % 4];
                Vector2 dir = (b - a).normalized;
                float len = (b - a).magnitude;
                // Posições dos vãos ao longo do lado (de a para b).
                var gaps = new List<float>();
                foreach (var (s, center) in doors)
                {
                    if (s != side) continue;
                    float along = Vector2.Dot(new Vector2(side % 2 == 0 ? center : a.x, side % 2 == 0 ? a.y : center) - a, dir);
                    gaps.Add(along);
                    RuinDoorway(parent, "Wall_ArchRound", a + dir * along, dir, name + "_Porta", 0f, CryptStone);
                }
                gaps.Sort();
                float from = 0f;
                foreach (float g in gaps)
                {
                    CryptHighWall(parent, a + dir * from, a + dir * (g - 2f), name, seed + side);
                    from = g + 2f;
                }
                CryptHighWall(parent, a + dir * from, a + dir * len, name, seed + side);
            }
        }

        /// <summary>Corredor: as duas paredes compridas (ao longo de X ou de Z).</summary>
        static void CryptCorridor(Transform parent, Rect r, string name, bool alongX, int seed)
        {
            if (alongX)
            {
                CryptHighWall(parent, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), name, seed);
                CryptHighWall(parent, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), name, seed + 1);
            }
            else
            {
                CryptHighWall(parent, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), name, seed);
                CryptHighWall(parent, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), name, seed + 1);
            }
        }

        static void CryptHighWall(Transform parent, Vector2 a, Vector2 b, string name, int seed)
        {
            if ((b - a).magnitude < 0.3f) return;
            string[] top = { "Wall", "Wall", "Wall_Broken", "Wall", "Wall_Hole", "Wall" };
            RuinWallRun(parent, a, b, 0f, i => "Wall", 2f, name, recolor: CryptStone);
            RuinWallRun(parent, a, b, 2f, i => top[(i * 5 + seed) % top.Length], 0f, name + "_Alta", recolor: CryptStone);
        }

        /// <summary>
        /// A escada de volta na alcova a leste da primeira sala: dois lances do kit subindo para o escuro (um teto baixo e uma parede preta
        /// na ponta), bloqueados por um colisor; o F fica no pé da escada.
        /// </summary>
        static Transform CryptStairs(Transform root, RuinMats mat)
        {
            var g = new GameObject("Escada_Subida").transform;
            g.SetParent(root, false);
            var lower = RuinPiece(g, "Stairs", new Vector3(9.6f, 0f, 0f), 180f, "Degraus");
            var upper = RuinPiece(g, "Stairs", new Vector3(11.6f, 0.9f, 0f), 180f, "Degraus");
            lower.localScale = upper.localScale = new Vector3(1f, 1f, 1.36f);
            RuinsRecolor(lower, CryptStone);
            RuinsRecolor(upper, CryptStone);
            Part(PrimitiveType.Cube, "Base", g, new Vector3(11.6f, 0.45f, 0f), new Vector3(2f, 0.9f, 2.9f), Quaternion.identity, RuinsMaterial("Cripta_Main2"));
            Part(PrimitiveType.Cube, "Escuro", g, new Vector3(12.9f, 3.4f, 0f), new Vector3(0.1f, 3.4f, 3f), Quaternion.identity, mat.hole);
            Part(PrimitiveType.Cube, "Teto", g, new Vector3(12f, 3.2f, 0f), new Vector3(2f, 0.4f, 3.2f), Quaternion.identity, RuinsMaterial("Cripta_Main2"));
            RuinBox(g, new Vector3(10.9f, 2f, 0f), new Vector3(4.2f, 4f, 3f));
            var foot = new GameObject("Pe_Da_Escada").transform;
            foot.SetParent(root, false);
            foot.localPosition = new Vector3(8.2f, 0f, 0f);
            return foot;
        }

        // ---------- Luz e ambiente ----------

        /// <summary>
        /// Tochas nas paredes de todas as salas (a chama sempre; uma em cada duas com luz de verdade, acesas debaixo da terra pelo
        /// <see cref="NightLight"/>) e o brilho verde dos mortos (luzes fixas, sem sombra) no ossuário, na cripta e no altar.
        /// </summary>
        static void CryptLights(Transform root)
        {
            var g = new GameObject("Luzes").transform;
            g.SetParent(root, false);
            // (x, z) na linha da parede, direção para dentro, com luz.
            var torches = new (float x, float z, Vector2 inward, bool light)[]
            {
                (4f, 4f, Vector2.down, true), (4f, -4f, Vector2.up, false), (-5f, 1.5f, Vector2.down, true),
                (-15f, 5f, Vector2.down, true), (-20f, 0f, Vector2.right, false), (-11.5f, -5f, Vector2.up, false),
                (-10f, -15f, Vector2.down, true), (-22f, -19f, Vector2.right, true), (-15f, -23f, Vector2.up, false),
                (-2f, -20.5f, Vector2.up, true), (-13.5f, -10f, Vector2.left, false),
                (4f, -13f, Vector2.right, false), (4f, -25f, Vector2.right, true), (20f, -14f, Vector2.left, true), (20f, -24f, Vector2.left, false),
                (12f, -10f, Vector2.down, false), (8f, -28f, Vector2.up, true), (16f, -28f, Vector2.up, true),
            };
            foreach (var t in torches)
            {
                // A face de dentro da parede fica ~0,25 m para dentro da linha; a tocha encosta nela.
                var at = new Vector3(t.x + t.inward.x * 0.29f, 2.2f, t.z + t.inward.y * 0.29f);
                var torch = RuinPiece(g, "Torch", at, Mathf.Atan2(t.inward.x, t.inward.y) * Mathf.Rad2Deg, "Tocha");
                if (!t.light) continue;
                var lightGo = new GameObject("LuzDaTocha");
                lightGo.transform.SetParent(torch, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.75f, 0.25f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.58f, 0.25f);
                light.range = 7.5f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                torch.gameObject.AddComponent<NightLight>().Configure(light, 2.6f, null);
            }
            foreach (var (x, z, y, range) in new[] { (-15f, 0f, 1.4f, 8f), (-15f, -19f, 1.6f, 8f), (12f, -25f, 1.8f, 9f) })
            {
                var go = new GameObject("BrilhoDosMortos");
                go.transform.SetParent(g, false);
                go.transform.localPosition = new Vector3(x, y, z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.4f, 1f, 0.6f);
                light.range = range;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Caixa do modelo como colisor no objeto dele (pelas malhas, no espaço local: vale com giro).</summary>
        static void SolidProp(GameObject go)
        {
            var b = new Bounds();
            bool any = false;
            var toLocal = go.transform.worldToLocalMatrix;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var m = toLocal * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var pt = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(pt, Vector3.zero); any = true; }
                    else b.Encapsulate(pt);
                }
            }
            if (!any) return;
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }

        /// <summary>Monte de caveiras (e uns tijolos) no chão.</summary>
        static void SkullPile(Transform parent, Vector2 at, int count, System.Random rng)
        {
            for (int k = 0; k < count; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble() * 0.55f;
                float y = k >= count - 2 ? 0.25f : 0f;
                RuinPiece(parent, "Skull", new Vector3(at.x + Mathf.Cos(a) * r, y, at.y + Mathf.Sin(a) * r), (float)rng.NextDouble() * 360f, "Caveira",
                    0.7f + (float)rng.NextDouble() * 0.3f);
            }
        }

        /// <summary>
        /// Ossuário: prateleiras de pedra nas paredes oeste e norte, cheias de caveiras viradas para a sala; montes de ossos nos cantos;
        /// quatro colunas e velas.
        /// </summary>
        static void CryptOssuary(Transform root)
        {
            var g = new GameObject("Ossuario").transform;
            g.SetParent(root, false);
            var rng = new System.Random(53);
            var shelf = RuinsMaterial("Cripta_Main2");
            foreach (float h in new[] { 0.55f, 1.25f })
            {
                // Oeste (parede em x = −20, face de dentro em ~−19,75).
                Part(PrimitiveType.Cube, "Prateleira", g, new Vector3(-19.55f, h - 0.04f, 0f), new Vector3(0.4f, 0.08f, 8.4f), Quaternion.identity, shelf);
                for (float z = -3.9f; z <= 3.95f; z += 0.6f)
                    RuinPiece(g, "Skull", new Vector3(-19.55f, h, z + (float)rng.NextDouble() * 0.1f), 90f + (float)rng.NextDouble() * 40f - 20f, "Caveira", 0.75f);
                // Norte (parede em z = 5).
                Part(PrimitiveType.Cube, "Prateleira", g, new Vector3(-15f, h - 0.04f, 4.55f), new Vector3(8.4f, 0.08f, 0.4f), Quaternion.identity, shelf);
                for (float x = -18.9f; x <= -11.05f; x += 0.6f)
                    RuinPiece(g, "Skull", new Vector3(x + (float)rng.NextDouble() * 0.1f, h, 4.55f), 180f + (float)rng.NextDouble() * 40f - 20f, "Caveira", 0.75f);
            }
            SkullPile(g, new Vector2(-18.8f, -3.8f), 8, rng);
            SkullPile(g, new Vector2(-11.2f, 3.6f), 6, rng);
            SkullPile(g, new Vector2(-11.3f, -3.9f), 5, rng);
            foreach (var c in new[] { new Vector2(-17.5f, 2.5f), new Vector2(-12.5f, 2.5f), new Vector2(-17.5f, -2.5f), new Vector2(-12.5f, -2.5f) })
            {
                var col = RuinPiece(g, "Column_Round", new Vector3(c.x, 0f, c.y), (float)rng.NextDouble() * 360f, "Coluna");
                RuinsRecolor(col, CryptStone);
                var cap = col.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.32f;
                cap.height = 4f;
                cap.center = Vector3.up * 2f;
                Occluder(col.gameObject);
            }
            RuinPiece(g, "Candles_2", new Vector3(-17.5f, 0f, 0f), 30f, "Velas");
            RuinPiece(g, "Candles_1", new Vector3(-13f, 0f, 4f), 0f, "Velas");
        }

        /// <summary>Cripta: sarcófagos com velas em cima, caixões no chão, urnas e pilares.</summary>
        static void CryptTombs(Transform root)
        {
            var g = new GameObject("Cripta").transform;
            g.SetParent(root, false);
            var rng = new System.Random(61);
            foreach (var (x, z, yaw) in new[] { (-20.4f, -18.2f, 0f), (-18f, -18.2f, 0f), (-12f, -21.6f, 90f) })
            {
                var tomb = Model(Kit.Graveyard, "crypt", g, new Vector3(x, 0f, z), yaw, 2f, "Sarcofago");
                SolidProp(tomb);
                Occluder(tomb);
                Model(Kit.Graveyard, "candle-multiple", g, new Vector3(x + 0.2f, 1.46f, z + 0.3f), (float)rng.NextDouble() * 360f, 1.5f, "Velas");
            }
            foreach (var (x, z, yaw, model) in new[] { (-16.6f, -22.2f, 90f, "coffin-old"), (-14.2f, -22.2f, 95f, "coffin"), (-21.1f, -15.9f, 0f, "coffin-old") })
            {
                var coffin = Model(Kit.Graveyard, model, g, new Vector3(x, 0f, z), yaw, 2f, "Caixao");
                SolidProp(coffin);
            }
            foreach (var (x, z) in new[] { (-21.3f, -22.3f), (-9f, -15.8f), (-9.2f, -22.2f) })
                SolidProp(Model(Kit.Graveyard, "urn-round", g, new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, 2f, "Urna"));
            foreach (var (x, z) in new[] { (-12f, -16.2f), (-17f, -16f) })
            {
                var pillar = Model(Kit.Graveyard, "pillar-large", g, new Vector3(x, 0f, z), 0f, 2f, "Pilar");
                SolidProp(pillar);
                Occluder(pillar);
            }
            SkullPile(g, new Vector2(-21f, -21f), 5, rng);
            RuinPiece(g, "Candles_1", new Vector3(-9f, 0f, -19.9f), 0f, "Velas");
        }

        /// <summary>
        /// Salão do Capelão: duas fileiras de colunas, o altar num degrau ao sul com a cruz e velas, estandartes nas paredes, caixões e
        /// ossos; o baú fica na frente do altar (trancado pelo Capelão Fantasma, com a Lâmina de São Telmo garantida). Devolve a tabela do baú.
        /// </summary>
        static LootTable CryptHall(Transform root, LandLootAssets landLoot, LootItems items)
        {
            var g = new GameObject("Salao_Capelao").transform;
            g.SetParent(root, false);
            var rng = new System.Random(71);
            foreach (float x in new[] { 7f, 17f })
            foreach (float z in new[] { -13f, -17f, -21f, -25f })
            {
                var col = RuinPiece(g, "Column_Round", new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, "Coluna");
                RuinsRecolor(col, CryptStone);
                var cap = col.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.32f;
                cap.height = 4f;
                cap.center = Vector3.up * 2f;
                Occluder(col.gameObject);
            }
            // Degrau do altar (x 9…15, z −28…−25,5; 0,3 m) com rampa invisível na frente.
            var dais = Part(PrimitiveType.Cube, "Degrau_Altar", g, new Vector3(12f, 0.15f, -26.75f), new Vector3(6f, 0.3f, 2.5f), Quaternion.identity,
                RuinsMaterial("CriptaPiso_Highlights"), keepCollider: true);
            Occluder(dais);
            var ramp = new GameObject("Rampa").transform;
            ramp.SetParent(g, false);
            ramp.SetLocalPositionAndRotation(new Vector3(12f, 0.05f, -25.35f), Quaternion.Euler(-14f, 0f, 0f));
            RuinBox(ramp, Vector3.zero, new Vector3(6f, 0.2f, 1.2f));
            var altar = Model(Kit.Graveyard, "altar-stone", g, new Vector3(12f, 0.3f, -27f), 0f, 2.2f, "Altar");
            SolidProp(altar);
            Occluder(altar);
            Model(Kit.Graveyard, "cross", g, new Vector3(12f, 0.3f, -27.7f), 0f, 2.6f, "Cruz");
            Model(Kit.Graveyard, "candle-multiple", g, new Vector3(11.2f, 1.38f, -26.9f), 20f, 1.5f, "Velas");
            Model(Kit.Graveyard, "candle-multiple", g, new Vector3(12.8f, 1.38f, -26.9f), 80f, 1.5f, "Velas");
            RuinPiece(g, "Candles_2", new Vector3(9.6f, 0.3f, -26.2f), 0f, "Velas");
            RuinPiece(g, "Candles_2", new Vector3(14.4f, 0.3f, -26.2f), 120f, "Velas");
            // Estandartes rasgados nas paredes do leste e do oeste; caixões encostados no leste.
            foreach (float z in new[] { -17.5f, -21.5f }) RuinPiece(g, "Flag_Wall", new Vector3(19.7f, 0.1f, z), -90f, "Estandarte");
            foreach (float z in new[] { -15.5f, -22.5f }) RuinPiece(g, "Flag_Wall", new Vector3(4.3f, 0.1f, z), 90f, "Estandarte");
            foreach (var (x, z, yaw) in new[] { (19f, -19.5f, 0f), (19f, -26.5f, 5f) })
                SolidProp(Model(Kit.Graveyard, "coffin-old", g, new Vector3(x, 0f, z), yaw, 2f, "Caixao"));
            SkullPile(g, new Vector2(5.2f, -27f), 6, rng);
            SkullPile(g, new Vector2(18.8f, -11f), 5, rng);
            SkullPile(g, new Vector2(5f, -11f), 4, rng);
            SkullPile(g, new Vector2(18.6f, -23f), 4, rng);
            // Sarcófagos dos oficiais da frota encostados na parede norte (entre as colunas) e um em cada lado do altar.
            foreach (var (x, z, yaw) in new[] { (10f, -11.3f, 90f), (14f, -11.3f, 90f), (6.6f, -27f, 0f), (17.4f, -27f, 0f) })
            {
                var tomb = Model(Kit.Graveyard, "crypt", g, new Vector3(x, 0f, z), yaw, 2f, "Sarcofago");
                SolidProp(tomb);
                Occluder(tomb);
                Model(Kit.Graveyard, "candle-multiple", g, new Vector3(x, 1.46f, z), (float)rng.NextDouble() * 360f, 1.5f, "Velas");
            }
            // Urnas e lápides quebradas nos cantos; entulho entre as colunas.
            foreach (var (x, z) in new[] { (5f, -13.5f), (19f, -13.6f), (5f, -24.5f) })
                SolidProp(Model(Kit.Graveyard, "urn-round", g, new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, 2f, "Urna"));
            foreach (var (x, z, yaw) in new[] { (8.5f, -15f, 20f), (15.5f, -23f, 200f), (8.3f, -23.4f, 130f) })
                Model(Kit.Graveyard, "gravestone-broken", g, new Vector3(x, 0f, z), yaw, 2f, "Lapide");
            foreach (var (x, z) in new[] { (17f, -19f), (7f, -19.6f) })
                Model(Kit.Graveyard, "debris", g, new Vector3(x + 0.8f, 0f, z), (float)rng.NextDouble() * 360f, 2f, "Entulho");
            RuinPiece(g, "Bricks", new Vector3(9f, 0f, -11.2f), 40f, "Tijolos");
            RuinPiece(g, "Bricks", new Vector3(15.5f, 0f, -12f), 200f, "Tijolos");

            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Catacumbas.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 450, 700), Drop(items.powder, 0.8f, 6, 10), Drop(items.iron, 0.8f, 6, 10), Drop(BombItem(), 0.6f, 2, 4),
                    Drop(MapPieceItem(), 0.6f, 1, 1),
                };
                t.chestChance = 0.7f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 400);
            });
            // As lâminas entram pela tabela do E1 (criada aqui na primeira geração, depois do AddBladeDrops).
            AddBladeDrops();
            // A Lâmina de São Telmo vem sempre, até o jogador ter a dele (passo 3).
            if (!table.guaranteedGear)
            {
                table.guaranteedGear = LegendaryBladeItem();
                UnityEditor.EditorUtility.SetDirty(table);
            }
            return table;
        }

        // ---------- Som ----------

        const string CryptLoopPath = GeneratedAudioFolder + "/Catacumba_Gerado.wav";
        const string CryptLoopVersion = "catacumba-v1";

        /// <summary>
        /// Ar das catacumbas feito aqui (sem gravação de terceiros), 16 s em loop: ronco grave (ruído com passa-baixa de 2 etapas em 140 Hz),
        /// o ar passando nos corredores (passa-faixa de 240–400 Hz que sobe e desce: é o que a caixinha do notebook toca) e pingos d'água
        /// com eco (seno caindo de ~2 kHz, 3 repetições cada vez mais baixas). Ondulações com período que divide os 16 s; fim misturado
        /// com o começo.
        /// </summary>
        static AudioClip CryptLoopClip() => GeneratedClip(CryptLoopPath, CryptLoopVersion, 0.09f, () =>
        {
            const int rate = AudioRate, seconds = 16;
            int n = rate * seconds, fade = rate;
            var raw = new float[n + fade];
            var rng = new System.Random(2020);
            float lp1 = 0f, lp2 = 0f, low = 0f, band = 0f;
            float lpA = Mathf.Exp(-2f * Mathf.PI * 140f / rate);
            const float q = 0.5f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 = lpA * lp1 + (1f - lpA) * white;
                lp2 = lpA * lp2 + (1f - lpA) * lp1;
                float center = 320f + 80f * Mathf.Sin(2f * Mathf.PI * t / 8f) + 40f * Mathf.Sin(2f * Mathf.PI * t / 4f + 1f);
                float f = 2f * Mathf.Sin(Mathf.PI * center / rate);
                low += f * band;
                float high = white - low - q * band;
                band += f * high;
                float swell = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * t / 16f + 0.5f);
                raw[i] = (lp2 * 6f + band * 1.5f) * swell;
            }
            var echoes = new[] { (0f, 1f), (0.13f, 0.4f), (0.29f, 0.18f), (0.47f, 0.08f) };
            for (int k = 0; k < 8; k++)
            {
                float t0 = 0.7f + k * 1.9f + (float)rng.NextDouble() * 0.8f;
                float amp = 0.25f + 0.35f * (float)rng.NextDouble();
                float f0 = 1700f + 900f * (float)rng.NextDouble();
                foreach (var (delay, gain) in echoes)
                {
                    int start = (int)((t0 + delay) * rate);
                    double phase = 0.0;
                    for (int j = 0; j < (int)(0.09f * rate) && start + j < raw.Length; j++)
                    {
                        float tt = (float)j / rate;
                        float freq = f0 * (1f - 0.35f * Mathf.Min(1f, tt / 0.03f));
                        phase += 2.0 * System.Math.PI * freq / rate;
                        float env = Mathf.Exp(-tt / 0.022f) * (1f - Mathf.Exp(-tt / 0.0015f));
                        raw[start + j] += (float)System.Math.Sin(phase) * env * amp * gain;
                    }
                }
            }
            return SeamlessLoop(raw, n, fade);
        });
    }
}
