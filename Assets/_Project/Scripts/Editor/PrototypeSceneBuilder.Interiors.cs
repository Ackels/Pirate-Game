using System.Collections.Generic;
using Pirates.Ambient;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Interiores das casas do porto (13/11, pedido do Jhonata: "o capitão conseguir entrar dentro da taverna, andar por dentro"; depois
    /// armazém, estaleiro etc.). Cada interior é uma sala montada longe do mapa, como as catacumbas: a porta da casa (F) escurece a tela e
    /// leva o capitão para dentro (<see cref="RuinPassage"/> + <see cref="PassageWalker"/>), a zona coberta troca a luz do dia pela do lugar
    /// (<see cref="CoveredZone"/> com <see cref="CoveredLook"/>) e a música. A placa da porta continua lá fora (só a placa).
    /// <para>
    /// <b>Taverna:</b> o quadro de contratos na parede (missões), o balcão do taverneiro (cozinha), a mesa do contramestre (marinheiros e
    /// salário), mesas com fregueses sentados (animações de sentar do Mixamo, várias, sorteadas), gente em pé no balcão, lareira, lustres,
    /// lanternas e quadros da história nas paredes. Móveis: Fantasy Props MegaKit do Quaternius (CC0); paredes e chão: Fantasy Town Kit.
    /// </para>
    /// <para>
    /// <b>Escala:</b> os personagens do kit são pequenos (cabeça a ~1 m, quadril a 0,38 m em pé, 0,25–0,29 sentados): assento a ~0,17 m,
    /// mesa a ~0,43 m e balcão a ~0,62 m (menos da metade do tamanho de gente de verdade). Os modelos ficam dentro de um suporte com a
    /// escala de cada eixo (<see cref="Furnish"/>).
    /// </para>
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static Vector3 TavernOrigin => new(2000f, 30f, -2400f);
        const string TavernRootName = "Interior_Taverna", TavernDoorName = "Porta_Taverna";
        const string PatronAnimatorPath = AnimationFolder + "/Fregues.controller";

        // Sala: 15 × 12 m (paredes de 3 m do kit da vila, peças de 1 m × 3), porta no meio da parede sul.
        const float RoomHalfX = 7.5f, RoomHalfZ = 6f, RoomWall = 3f;
        // Altura dos assentos, mesas e balcão na escala dos personagens do kit (medida na pose de sentado).
        const float SeatTop = 0.17f, TableTop = 0.43f, CounterTop = 0.62f;
        // Banquetas do balcão mais altas (13/11, o Jhonata achou os bancos pequenos demais): quem senta nelas sobe junto.
        const float BarSeatTop = 0.3f;
        // O corpo sentado fica 7 cm abaixo do chão (pés no chão) e 6 cm atrás do meio do assento.
        const float SeatDrop = 0.07f, SeatBack = 0.06f;

        static readonly (string state, string clip)[] SeatedClips =
        {
            ("Sentado_Parado1", "Sitting Idle 1"), ("Sentado_Parado2", "Sitting Idle 3"), ("Sentado_Parado3", "Sitting"),
            ("Sentado_Conversa1", "Sitting Talking 1"), ("Sentado_Conversa2", "Sitting Talking 2"),
            ("Sentado_Bebendo", "Sitting Drinking"), ("Sentado_Comemora", "Cheering While Sitting"), ("Sentado_Chao", "Sitting Idle 2"),
        };

        static readonly string[] ChattyPatron = { "Sentado_Conversa1", "Sentado_Conversa2", "Sentado_Parado1" };
        // O "Sitting" senta 15 cm mais para trás que os outros: fica fora das trocas.
        static readonly string[] QuietPatron = { "Sentado_Parado1", "Sentado_Parado2", "Sentado_Conversa2" };
        static readonly string[] MerryPatron = { "Sentado_Comemora", "Sentado_Conversa1", "Sentado_Parado2" };
        static readonly string[] DrinkingPatron = { "Sentado_Bebendo" };
        // O "Sitting Drinking" do Mixamo senta 1,4 m à frente (e 16 cm de lado) de onde começa: o corpo volta para o banco (só nele; trocar de
        // animação com ele faria o corpo escorregar).
        static readonly Vector3 DrinkingBodyShift = new(0.16f, 0f, 1.39f);

        [MenuItem("Pirates/Protótipo/Interiores: refazer a taverna")]
        static void MenuTavernInterior() => Debug.Log(AddTavernInteriorToOpenScene());

        /// <summary>Monta (ou refaz) o interior da taverna na cena aberta e salva. Só na cena com o porto a pé (07).</summary>
        public static string AddTavernInteriorToOpenScene()
        {
            string r = AddTavernInterior();
            if (!r.StartsWith("ok")) return r;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }

        static string AddTavernInterior()
        {
            HarborBuilding door = null;
            foreach (var b in Object.FindObjectsByType<HarborBuilding>(FindObjectsInactive.Include))
                if (b.Service == HarborService.Missions && b.name.StartsWith("Entrada_")) door = b;
            if (!door) return "sem a porta da taverna (só na cena com o porto a pé)";
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>();
            if (!switcher || !switcher.Character) return "sem o capitão";
            var captain = switcher.Character.GetComponent<FootCharacter>();
            if (!captain.GetComponent<PassageWalker>()) return "o capitão não tem o PassageWalker (a Ermida põe)";

            var old = GameObject.Find(TavernRootName);
            if (old) Object.DestroyImmediate(old);
            var oldDoor = GameObject.Find(TavernDoorName);
            if (oldDoor) Object.DestroyImmediate(oldDoor);

            var root = new GameObject(TavernRootName).transform;
            root.position = TavernOrigin;

            TavernShell(root);
            var spots = TavernFurniture(root);
            TavernPeople(root);
            TavernLights(root);
            TavernOutside(root);

            // Porta: lá fora a placa fica, mas o F entra (a janela abre nos cantos lá dentro).
            door.SetSignOnly(true);
            door.SetAction("Entrar");
            var outsideArrival = new GameObject("Chegada_Porto").transform;
            outsideArrival.SetParent(root, false);
            outsideArrival.SetPositionAndRotation(door.transform.position + door.transform.forward * 2.2f + Vector3.up * 0.05f, door.transform.rotation);
            var insideArrival = new GameObject("Chegada_Taverna").transform;
            insideArrival.SetParent(root, false);
            insideArrival.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, -RoomHalfZ + 2.4f), Quaternion.identity);

            var outsidePassage = new GameObject(TavernDoorName);
            outsidePassage.transform.SetPositionAndRotation(door.transform.position, door.transform.rotation);
            outsidePassage.AddComponent<RuinPassage>().Configure("Entrar na Taverna", insideArrival, "Taverna", "contratos, comida e marinheiros");
            var exit = new GameObject("Saida").transform;
            exit.SetParent(root, false);
            exit.localPosition = new Vector3(0f, 0f, -RoomHalfZ + 0.4f);
            exit.gameObject.AddComponent<RuinPassage>().Configure("Sair para o porto", outsideArrival, null, null);

            var zone = root.gameObject.AddComponent<CoveredZone>();
            zone.Configure(switcher, new Bounds(root.TransformPoint(new Vector3(0f, 2f, 0f)), new Vector3(RoomHalfX * 2f + 2f, 8f, RoomHalfZ * 2f + 2f)));
            zone.SetLook(new CoveredLook
            {
                light = new Color(1f, 0.8f, 0.58f), intensity = 0.55f, angles = new Vector2(62f, 25f), shadow = 0.55f,
                ambient = new Color(0.34f, 0.25f, 0.18f), music = true, air = false, rainOnRoof = 0.35f, outsideWeather = true, indoorMix = 0.35f,
                background = new Color(0.012f, 0.03f, 0.06f),
            });
            return $"ok: taverna em {TavernOrigin}, {spots} cantos de serviço";
        }

        // ---------- Peças ----------

        /// <summary>
        /// Um modelo dentro de um suporte com giro e escala por eixo (largura, altura, fundo): o pé do modelo vai para y = 0 do suporte e o
        /// meio dele para x = z = 0. <paramref name="solid"/> = caixa de colisão pelas malhas.
        /// </summary>
        static GameObject Furnish(Transform parent, Kit kit, string model, Vector3 at, float yaw, Vector3 size, string name = null, bool solid = false)
        {
            var holder = new GameObject(name ?? model);
            holder.transform.SetParent(parent, false);
            holder.transform.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            holder.transform.localScale = size;
            var m = Model(kit, model, holder.transform, Vector3.zero, 0f, 1f, "Modelo");
            var rs = m.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var toLocal = holder.transform.worldToLocalMatrix;
                var c = toLocal.MultiplyPoint3x4(b.center);
                // Altura do pé: o canto mais baixo da caixa no espaço do suporte.
                float minY = float.MaxValue;
                for (int k = 0; k < 8; k++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    minY = Mathf.Min(minY, toLocal.MultiplyPoint3x4(corner).y);
                }
                m.transform.localPosition -= new Vector3(c.x, minY, c.z);
            }
            if (solid) SolidProp(holder);
            return holder;
        }

        static GameObject Furnish(Transform parent, Kit kit, string model, Vector3 at, float yaw, float scale, string name = null, bool solid = false) =>
            Furnish(parent, kit, model, at, yaw, Vector3.one * scale, name, solid);

        /// <summary>Peça de parede do kit da vila (1 m × 3) com o meio em <paramref name="center"/>, deitada ao longo de <paramref name="along"/>.</summary>
        static GameObject TavernWallPiece(Transform parent, string model, Vector3 center, Vector3 along, string name)
        {
            float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
            var holder = Furnish(parent, Kit.Town, model, center, yaw, new Vector3(3f, RoomWall, 3f), name);
            var box = holder.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.12f, 1f, 1f);
            return holder;
        }

        // ---------- Sala ----------

        /// <summary>
        /// Sala de 15 × 12 m (chão de tábuas, paredes de madeira do kit da vila, pilares). <paramref name="piece"/> escolhe a peça de cada
        /// parede (lado N/S/O/L e índice; vazio = a da taverna); <paramref name="under"/> = cor do escuro debaixo das tábuas.
        /// </summary>
        static void TavernShell(Transform root, System.Func<char, int, string> piece = null)
        {
            piece ??= (side, i) => side switch
            {
                'S' => i == 2 ? "wall-wood-door" : "wall-wood",
                'O' => i == 3 ? "wall-wood-window-shutters" : "wall-wood",
                'L' => i is 1 or 3 ? "wall-wood-window-shutters" : "wall-wood",
                _ => "wall-wood",
            };
            var shell = new GameObject("Sala").transform;
            shell.SetParent(root, false);

            // Chão: tábuas do kit da vila (1 m × 3) e um colisor só com o topo em 0.
            var floor = new GameObject("Chao").transform;
            floor.SetParent(shell, false);
            for (float x = -RoomHalfX + 1.5f; x < RoomHalfX; x += 3f)
            for (float z = -RoomHalfZ + 1.5f; z < RoomHalfZ; z += 3f)
                Furnish(floor, Kit.Town, "planks", new Vector3(x, 0f, z), 0f, new Vector3(3f, 1f, 3f), "Tabuas");
            var ground = floor.gameObject.AddComponent<BoxCollider>();
            ground.center = new Vector3(0f, -0.5f, 0f);
            ground.size = new Vector3(RoomHalfX * 2f + 1f, 1f, RoomHalfZ * 2f + 1f);
            // Escuro debaixo das tábuas (pelas frestas aparecia o fundo preto).
            Part(PrimitiveType.Cube, "Fundo", floor, new Vector3(0f, -0.03f, 0f), new Vector3(RoomHalfX * 2f, 0.04f, RoomHalfZ * 2f),
                Quaternion.identity, TavernMaterial("M_Taverna_Fundo", new Color(0.3f, 0.19f, 0.11f)));

            // Paredes: norte e sul com 5 peças, leste e oeste com 4; porta no meio do sul, janelas a leste e oeste.
            var walls = new GameObject("Paredes").transform;
            walls.SetParent(shell, false);
            var night = GetEmissiveMaterial("M_Taverna_Noite", new Color(0.12f, 0.16f, 0.28f), 0.6f);
            for (int i = 0; i < 5; i++)
            {
                float x = -RoomHalfX + 1.5f + i * 3f;
                string north = piece('N', i);
                var n = TavernWallPiece(walls, north, new Vector3(x, 0f, RoomHalfZ), Vector3.right, $"Parede_N_{i}");
                Occluder(n);
                if (north.Contains("window")) WindowNight(n.transform, night, 1f);
                // Parede sul = a do lado da câmera (vista por fora): lisa, só a porta.
                string south = piece('S', i);
                var s = TavernWallPiece(walls, south, new Vector3(x, 0f, -RoomHalfZ), Vector3.right, $"Parede_S_{i}");
                Occluder(s);
                if (south.Contains("window")) WindowNight(s.transform, night, -1f);
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -RoomHalfZ + 1.5f + i * 3f;
                string west = piece('O', i);
                string east = piece('L', i);
                var w = TavernWallPiece(walls, west, new Vector3(-RoomHalfX, 0f, z), Vector3.forward, $"Parede_O_{i}");
                var e = TavernWallPiece(walls, east, new Vector3(RoomHalfX, 0f, z), Vector3.forward, $"Parede_L_{i}");
                Occluder(w);
                Occluder(e);
                if (west.Contains("window")) WindowNight(w.transform, night, 1f);
                if (east.Contains("window")) WindowNight(e.transform, night, -1f);
            }
            // Pilares nos cantos e nas emendas das paredes (o kit tem a viga de 1 m).
            for (int k = 0; k < 4; k++)
            {
                var corner = new Vector3(k % 2 == 0 ? -RoomHalfX : RoomHalfX, 0f, k < 2 ? -RoomHalfZ : RoomHalfZ);
                Occluder(Furnish(walls, Kit.Town, "pillar-wood", corner, 0f, new Vector3(2f, RoomWall, 2f), $"Pilar_{k}"));
            }
        }

        /// <summary>Material de cor da taverna (a cor é reaplicada a cada geração).</summary>
        static Material TavernMaterial(string name, Color color, float smoothness = 0.08f)
        {
            var mat = GetMaterial(name, color, smoothness);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Noite azulada atrás da janela (lá fora é o fundo preto da zona coberta).</summary>
        static void WindowNight(Transform wall, Material night, float outward)
        {
            // 13/11: lá fora agora é a vila com a hora e o clima de verdade; pela janela aparece ela (a placa da noite saiu).
            return;
            // A peça fica num suporte de escala 3 com a parede no plano x = 0 dele: a placa vai um pouco para fora, do lado de fora da sala.
            var room = wall.parent.parent.parent;
            var away = wall.position - room.position;
            away.y = 0f;
            away = away.normalized;
            var plate = Part(PrimitiveType.Cube, "Noite", wall, Vector3.zero, Vector3.one, Quaternion.identity, night);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.position = wall.position + away * 0.2f + Vector3.up * 1.5f;
            plate.transform.localScale = new Vector3(0.01f, 1.9f / RoomWall, 2.4f / 3f);
            plate.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- Móveis ----------

        /// <summary>Bancos, mesas, balcão, quadro, lareira e decoração. Devolve quantos cantos de serviço pôs.</summary>
        static int TavernFurniture(Transform root)
        {
            var f = new GameObject("Moveis").transform;
            f.SetParent(root, false);
            var seat = new Vector3(0.85f, SeatTop / 0.53f, 0.9f);

            // Balcão do taverneiro (norte-leste): armários lado a lado de frente para a sala, tampo de madeira escura.
            float counterZ = 3.2f;
            for (int i = 0; i < 4; i++)
                Furnish(f, Kit.Props, "Cabinet", new Vector3(1.25f + i * 1.3f, 0f, counterZ), 180f, new Vector3(0.95f, CounterTop / 1f, 1.4f), $"Balcao_{i}", solid: true);
            var top = Part(PrimitiveType.Cube, "Balcao_Tampo", f, new Vector3(3.2f, CounterTop + 0.03f, counterZ), new Vector3(5.5f, 0.06f, 0.62f),
                Quaternion.identity, GetMaterial("M_F_woodDark", FurniturePalette["woodDark"], 0.12f));
            Object.DestroyImmediate(top.GetComponent<Collider>());
            Furnish(f, Kit.Props, "Mug", new Vector3(1.4f, CounterTop + 0.06f, counterZ), 30f, 0.8f);
            Furnish(f, Kit.Props, "Mug", new Vector3(4.6f, CounterTop + 0.06f, counterZ - 0.1f), -50f, 0.8f);
            Furnish(f, Kit.Props, "Bottle_1", new Vector3(2.4f, CounterTop + 0.06f, counterZ + 0.12f), 0f, 0.9f);
            Furnish(f, Kit.Props, "Chalice", new Vector3(5.3f, CounterTop + 0.06f, counterZ + 0.05f), 0f, 0.9f);
            Furnish(f, Kit.Props, "Coin_Pile", new Vector3(3.6f, CounterTop + 0.06f, counterZ - 0.12f), 20f, 1f);
            // Atrás do balcão: barris deitados, prateleiras de garrafas na parede.
            Furnish(f, Kit.Props, "Barrel_Holder", new Vector3(6.4f, 0f, RoomHalfZ - 0.55f), 180f, 0.7f, "Barris_Deitados", solid: true);
            Furnish(f, Kit.Props, "Shelf_Small_Bottles", new Vector3(1.6f, 0.95f, RoomHalfZ - 0.2f), 180f, 0.9f, "Prateleira_Garrafas");
            Furnish(f, Kit.Props, "Shelf_Small_Bottles", new Vector3(3.6f, 1.25f, RoomHalfZ - 0.2f), 180f, 0.9f, "Prateleira_Garrafas_2");
            Furnish(f, Kit.Props, "Barrel", new Vector3(0.6f, 0f, RoomHalfZ - 0.6f), 0f, 0.62f, "Barril_Balcao", solid: true);
            for (int i = 0; i < 3; i++)
                Furnish(f, Kit.Props, "Stool", new Vector3(1.6f + i * 1.6f, 0f, counterZ - 0.75f), i * 40f, new Vector3(0.85f, BarSeatTop / 0.53f, 0.9f), $"Banqueta_Balcao_{i}", solid: true);
            // A banqueta do meio é do capitão: "Uma caneca de rum" no balcão (13/11) senta ele ali, com uma caneca na frente.
            var stool = new Vector3(3.2f, 0f, counterZ - 0.75f);
            var seatBody = new GameObject("Assento_Capitao").transform;
            seatBody.SetParent(f, false);
            seatBody.SetLocalPositionAndRotation(stool - Vector3.forward * SeatBack + Vector3.up * (BarSeatTop - SeatTop - SeatDrop), Quaternion.identity);
            var seatStand = new GameObject("Assento_Capitao_DePe").transform;
            seatStand.SetParent(f, false);
            seatStand.SetLocalPositionAndRotation(stool + new Vector3(0f, 0.05f, -0.85f), Quaternion.identity);
            var seatMug = Furnish(f, Kit.Props, "Mug", new Vector3(3.15f, CounterTop + 0.06f, counterZ - 0.2f), -20f, 0.8f, "Caneca_Capitao");
            seatBody.gameObject.AddComponent<BarSeat>().Configure(seatBody, seatStand, seatMug, PatronAnimator());
            var bar = Spot(f, "Balcao_do_Taverneiro", new Vector3(3.2f, 0f, counterZ - 1.05f), 180f, "Balcão do taverneiro", "cozinha e bebidas",
                HarborService.TavernBar, "Pedir");

            // Quadro de contratos (parede oeste): tábua com moldura e papéis pregados.
            NoticeBoard(f, new Vector3(-RoomHalfX + 0.12f, 0f, 1.5f), 90f);
            Spot(f, "Quadro_de_Contratos", new Vector3(-RoomHalfX + 1.3f, 0f, 1.5f), 90f, "Quadro de contratos", "missões para pegar",
                HarborService.Missions, "Ver");

            // Mesa do contramestre (sudeste): livro de contas, moedas, vela e o baú da tripulação.
            Furnish(f, Kit.Props, "Table_Large", new Vector3(5.5f, 0f, -3.4f), 90f, new Vector3(0.45f, TableTop / 0.81f, 0.75f), "Mesa_Contramestre", solid: true);
            Furnish(f, Kit.Props, "Book_5", new Vector3(5.5f, TableTop + 0.01f, -3.6f), 80f, 1.1f, "Livro_de_Contas");
            Furnish(f, Kit.Props, "Coin_Pile_2", new Vector3(5.4f, TableTop + 0.01f, -3.05f), 0f, 1f);
            Furnish(f, Kit.Props, "Scroll_1", new Vector3(5.6f, TableTop + 0.01f, -3.95f), 30f, 1f);
            NightLantern(Furnish(f, Kit.Props, "CandleStick", new Vector3(5.65f, TableTop + 0.01f, -2.9f), 0f, 1f, "Vela_Contramestre"), true, 3f, 1.2f);
            Furnish(f, Kit.Props, "Stool", new Vector3(6.25f, 0f, -3.4f), 0f, seat, "Banqueta_Contramestre", solid: true);
            Furnish(f, Kit.Props, "Chest_Wood", new Vector3(6.6f, 0f, -5.1f), -90f, 0.55f, "Bau_Tripulacao", solid: true);
            Spot(f, "Mesa_do_Contramestre", new Vector3(4.25f, 0f, -3.4f), -90f, "Mesa do contramestre", "contratar marinheiros e pagar o salário",
                HarborService.TavernCrew, "Falar com");

            // Mesas dos fregueses.
            TableWithBenches(f, "Mesa_Longa", new Vector3(-2.6f, 0f, -0.2f));
            TableWithStools(f, "Mesa_Canto", new Vector3(2.0f, 0f, -3.4f)); // fora da chegada (0; −3,6): o capitão nascia em cima de um freguês
            TableWithStools(f, "Mesa_Lareira", new Vector3(-4.3f, 0f, -3.9f));
            Furnish(f, Kit.Furniture, "rugRectangle", new Vector3(-2.6f, 0.005f, -0.2f), 0f, new Vector3(0.24f, 1f, 0.26f), "Tapete");

            // Lareira na parede oeste, perto do canto sul.
            Hearth(f, new Vector3(-RoomHalfX + 0.45f, 0f, -3.2f));

            // Decoração das paredes e cantos.
            Furnish(f, Kit.Props, "Banner_1_Cloth", new Vector3(-1.2f, 0.9f, RoomHalfZ - 0.2f), 0f, 0.8f, "Estandarte");
            Furnish(f, Kit.Props, "Shield_Wooden", new Vector3(-4.6f, 1.5f, RoomHalfZ - 0.18f), 0f, 0.9f, "Escudo");
            Furnish(f, Kit.Props, "Sword_Bronze", new Vector3(-3.9f, 1.15f, RoomHalfZ - 0.18f), 0f, 1f, "Espada").transform.localRotation *= Quaternion.Euler(0f, 0f, 20f);
            Furnish(f, Kit.Props, "Peg_Rack", new Vector3(RoomHalfX - 0.12f, 1.35f, 1.6f), -90f, 0.9f, "Cabideiro");
            Furnish(f, Kit.Props, "Rope_2", new Vector3(RoomHalfX - 0.5f, 0f, 2.6f), 30f, 0.8f, "Corda");
            Furnish(f, Kit.Props, "Crate_Wooden", new Vector3(-RoomHalfX + 0.55f, 0f, RoomHalfZ - 0.55f), 15f, 0.65f, "Caixote", solid: true);
            Furnish(f, Kit.Props, "Barrel", new Vector3(-RoomHalfX + 1.35f, 0f, RoomHalfZ - 0.5f), 0f, 0.62f, "Barril_Canto", solid: true);
            Furnish(f, Kit.Props, "Barrel_Apples", new Vector3(RoomHalfX - 0.55f, 0f, -0.6f), 0f, 0.62f, "Barril_Macas", solid: true);
            Furnish(f, Kit.Props, "Bookcase_2", new Vector3(RoomHalfX - 0.3f, 0f, 4.4f), -90f, new Vector3(0.6f, 0.62f, 0.75f), "Estante", solid: true);
            Furnish(f, Kit.Props, "Vase_2", new Vector3(-RoomHalfX + 0.5f, 0f, -RoomHalfZ + 0.6f), 0f, 0.7f, "Jarro", solid: true);
            Furnish(f, Kit.Props, "Bag", new Vector3(RoomHalfX - 0.6f, 0f, -1.4f), 40f, 0.6f, "Saco");
            Painting(f, 1, new Vector3(-3.4f, 1.55f, RoomHalfZ - 0.12f), 180f, 1.3f);
            Painting(f, 5, new Vector3(-6.2f, 1.55f, RoomHalfZ - 0.12f), 180f, 1.1f);
            Painting(f, 9, new Vector3(RoomHalfX - 0.12f, 1.55f, -0.9f), -90f, 1.3f);
            Painting(f, 3, new Vector3(-4.5f, 1.5f, -RoomHalfZ + 0.12f), 0f, 1.1f);
            Painting(f, 7, new Vector3(3.7f, 1.5f, -RoomHalfZ + 0.12f), 0f, 1.1f);
            return bar ? 3 : 0;
        }

        /// <summary>Mesa comprida com um banco de cada lado (2 fregueses em cada).</summary>
        static void TableWithBenches(Transform parent, string name, Vector3 at)
        {
            Furnish(parent, Kit.Props, "Table_Large", at, 0f, new Vector3(0.62f, TableTop / 0.81f, 0.62f), name, solid: true);
            var bench = new Vector3(0.9f, SeatTop / 0.53f, 0.9f);
            Furnish(parent, Kit.Props, "Bench", at + new Vector3(0f, 0f, 0.62f), 0f, bench, name + "_Banco_N", solid: true);
            Furnish(parent, Kit.Props, "Bench", at + new Vector3(0f, 0f, -0.62f), 0f, bench, name + "_Banco_S", solid: true);
            TableClutter(parent, at, 1.6f, 0.5f);
        }

        /// <summary>Mesa pequena (a grande encurtada) com duas banquetas.</summary>
        static void TableWithStools(Transform parent, string name, Vector3 at)
        {
            Furnish(parent, Kit.Props, "Table_Large", at, 0f, new Vector3(0.32f, TableTop / 0.81f, 0.62f), name, solid: true);
            var seat = new Vector3(0.85f, SeatTop / 0.53f, 0.9f);
            Furnish(parent, Kit.Props, "Stool", at + new Vector3(-0.75f, 0f, 0f), 10f, seat, name + "_Banqueta_O", solid: true);
            Furnish(parent, Kit.Props, "Stool", at + new Vector3(0.75f, 0f, 0f), -25f, seat, name + "_Banqueta_L", solid: true);
            TableClutter(parent, at, 0.7f, 0.5f);
        }

        /// <summary>Canecas, pratos e uma vela em cima da mesa.</summary>
        static void TableClutter(Transform parent, Vector3 at, float length, float depth)
        {
            var rng = new System.Random(Mathf.RoundToInt(at.x * 31f + at.z * 7f));
            float y = TableTop + 0.005f;
            string[] things = { "Mug", "Table_Plate", "Mug", "Bottle_1", "Chalice" };
            int n = length > 1f ? 5 : 3;
            for (int i = 0; i < n; i++)
            {
                float x = -length * 0.5f + length * (i + 0.5f) / n;
                float z = (i % 2 == 0 ? 1f : -1f) * depth * 0.22f;
                Furnish(parent, Kit.Props, things[(i + rng.Next(3)) % things.Length], at + new Vector3(x, y, z), rng.Next(360), 0.75f, "Na_Mesa");
            }
            NightLantern(Furnish(parent, Kit.Props, "CandleStick", at + new Vector3(0f, y, 0f), 0f, 0.9f, "Vela_Mesa"), true, 3.5f, 1.4f);
        }

        /// <summary>Quadro de contratos: tábua com moldura escura, papéis tortos pregados com alfinete vermelho.</summary>
        static void NoticeBoard(Transform parent, Vector3 wallPoint, float yaw)
        {
            var board = new GameObject("Quadro_Contratos").transform;
            board.SetParent(parent, false);
            board.SetLocalPositionAndRotation(wallPoint, Quaternion.Euler(0f, yaw, 0f));
            var wood = GetMaterial("M_F_wood", FurniturePalette["wood"], 0.12f);
            var dark = GetMaterial("M_F_woodDark", FurniturePalette["woodDark"], 0.12f);
            var paper = GetMaterial("M_Props_Pagina", new Color(0.86f, 0.8f, 0.66f), 0.05f);
            var pin = GetMaterial("M_Taverna_Alfinete", new Color(0.7f, 0.1f, 0.08f), 0.4f);
            void Box(string n, Vector3 c, Vector3 s, Material m, float roll = 0f)
            {
                var p = Part(PrimitiveType.Cube, n, board, c, s, Quaternion.Euler(0f, 0f, roll), m);
                Object.DestroyImmediate(p.GetComponent<Collider>());
            }
            Box("Tabua", new Vector3(0f, 1.3f, 0.04f), new Vector3(2f, 1.2f, 0.06f), wood);
            Box("Moldura_Cima", new Vector3(0f, 1.92f, 0.07f), new Vector3(2.14f, 0.1f, 0.1f), dark);
            Box("Moldura_Baixo", new Vector3(0f, 0.68f, 0.07f), new Vector3(2.14f, 0.1f, 0.1f), dark);
            Box("Moldura_E", new Vector3(-1.04f, 1.3f, 0.07f), new Vector3(0.1f, 1.3f, 0.1f), dark);
            Box("Moldura_D", new Vector3(1.04f, 1.3f, 0.07f), new Vector3(0.1f, 1.3f, 0.1f), dark);
            Box("Telhadinho", new Vector3(0f, 2.02f, 0.12f), new Vector3(2.3f, 0.06f, 0.24f), dark);
            var rng = new System.Random(5);
            for (int i = 0; i < 6; i++)
            {
                float x = -0.7f + (i % 3) * 0.7f + (float)(rng.NextDouble() - 0.5) * 0.15f;
                float y = 1.6f - (i / 3) * 0.55f + (float)(rng.NextDouble() - 0.5) * 0.08f;
                float roll = (float)(rng.NextDouble() - 0.5) * 12f;
                Box($"Papel_{i}", new Vector3(x, y, 0.08f), new Vector3(0.42f, 0.46f, 0.01f), paper, roll);
                Box($"Alfinete_{i}", new Vector3(x, y + 0.18f, 0.095f), new Vector3(0.05f, 0.05f, 0.03f), pin);
            }
            // Colisor na parede (não deixa entrar no quadro).
            var col = board.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.2f, 0.1f);
            col.size = new Vector3(2.2f, 1.6f, 0.2f);
        }

        /// <summary>Quadro pintado na parede com uma cena da história (os quadros do prólogo).</summary>
        static void Painting(Transform parent, int frame, Vector3 wallPoint, float yaw, float width)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Project/UI/Prologo/Quadro_{frame}.png");
            if (!tex) return;
            var holder = new GameObject($"Quadro_Parede_{frame}").transform;
            holder.SetParent(parent, false);
            holder.SetLocalPositionAndRotation(wallPoint, Quaternion.Euler(0f, yaw, 0f));
            float h = width * 9f / 16f;
            var canvas = GetTexturedMaterial($"M_Taverna_Quadro_{frame}", tex, 0.05f);
            var dark = GetMaterial("M_F_woodDark", FurniturePalette["woodDark"], 0.12f);
            var c = Part(PrimitiveType.Cube, "Tela", holder, new Vector3(0f, 0f, 0.03f), new Vector3(width, h, 0.02f), Quaternion.identity, canvas);
            Object.DestroyImmediate(c.GetComponent<Collider>());
            // O cubo pinta a textura de lado em cada face: na da frente (−Z do cubo virado para a sala) ela sai espelhada; vira o cubo.
            c.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var (n, p, s) in new[]
            {
                ("Moldura_Cima", new Vector3(0f, h * 0.5f + 0.04f, 0.04f), new Vector3(width + 0.16f, 0.08f, 0.06f)),
                ("Moldura_Baixo", new Vector3(0f, -h * 0.5f - 0.04f, 0.04f), new Vector3(width + 0.16f, 0.08f, 0.06f)),
                ("Moldura_E", new Vector3(-width * 0.5f - 0.04f, 0f, 0.04f), new Vector3(0.08f, h, 0.06f)),
                ("Moldura_D", new Vector3(width * 0.5f + 0.04f, 0f, 0.04f), new Vector3(0.08f, h, 0.06f)),
            })
            {
                var m = Part(PrimitiveType.Cube, n, holder, p, s, Quaternion.identity, dark);
                Object.DestroyImmediate(m.GetComponent<Collider>());
            }
        }

        /// <summary>Lareira de pedra encostada na parede, com fogo, lenha e a luz quente.</summary>
        static void Hearth(Transform parent, Vector3 at)
        {
            var root = new GameObject("Lareira").transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var stone = TavernMaterial("M_Taverna_Pedra", new Color(0.34f, 0.3f, 0.27f));
            var soot = TavernMaterial("M_Taverna_Fuligem", new Color(0.08f, 0.07f, 0.06f));
            void Box(string n, Vector3 c, Vector3 s, Material m)
            {
                var p = Part(PrimitiveType.Cube, n, root, c, s, Quaternion.identity, m);
                Object.DestroyImmediate(p.GetComponent<Collider>());
            }
            Box("Fundo", new Vector3(0f, 0.75f, -0.25f), new Vector3(1.7f, 1.5f, 0.2f), soot);
            Box("Lado_E", new Vector3(-0.75f, 0.55f, 0f), new Vector3(0.3f, 1.1f, 0.6f), stone);
            Box("Lado_D", new Vector3(0.75f, 0.55f, 0f), new Vector3(0.3f, 1.1f, 0.6f), stone);
            Box("Cornija", new Vector3(0f, 1.15f, 0.02f), new Vector3(1.9f, 0.14f, 0.7f), stone);
            Box("Chamine", new Vector3(0f, 2.1f, -0.1f), new Vector3(1.2f, 1.8f, 0.45f), stone);
            Box("Base", new Vector3(0f, 0.04f, 0.1f), new Vector3(1.9f, 0.08f, 0.8f), stone);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.2f, -0.05f);
            col.size = new Vector3(1.9f, 2.4f, 0.7f);
            Model(Kit.Nature, "campfire_logs", root, new Vector3(0f, 0.08f, 0f), 30f, 1.6f, "Lenha");
            // O fogo do porto (chamas + luz que tremula); a fumaça dele sobe pela chaminé (dentro da sala ficava boiando).
            Fire(root, root.TransformPoint(new Vector3(0f, 0.2f, 0f)), 0.75f);
            for (int i = root.childCount - 1; i >= 0; i--)
                if (root.GetChild(i).GetComponent<ParticleSystem>() && root.GetChild(i).name != "Fogo") Object.DestroyImmediate(root.GetChild(i).gameObject);
            Furnish(parent, Kit.Props, "Cauldron", at + new Vector3(0.75f, 0f, 1.1f), 0f, 0.45f, "Caldeirao", solid: true);
        }

        /// <summary>Canto de serviço: o F abre a parte da taverna (janela) a <c>range</c> dele; a câmera olha na direção oposta à frente.</summary>
        static HarborBuilding Spot(Transform parent, string name, Vector3 at, float yaw, string title, string purpose, HarborService service, string action)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var b = go.AddComponent<HarborBuilding>();
            b.Configure(title, purpose, service, 1.8f, 2.3f);
            b.SetAction(action);
            return b;
        }

        // ---------- Gente ----------

        static void TavernPeople(Transform root)
        {
            var people = new GameObject("Gente").transform;
            people.SetParent(root, false);
            // Taverneiro atrás do balcão e o contramestre na mesa dele.
            Townsfolk(people, "Taverneiro", root.TransformPoint(new Vector3(3.2f, 0f, 4.2f)), 180f, 1, null);
            SeatedPatron(people, "Contramestre", new Vector3(6.25f, 0f, -3.4f), -90f, 3, QuietPatron);
            // Em pé no balcão.
            Townsfolk(people, "Freguês_Balcao", root.TransformPoint(new Vector3(0.7f, 0f, 2.2f)), 20f, 4, null);
            // Mesa longa: dois de cada lado.
            var t1 = new Vector3(-2.6f, 0f, -0.2f);
            SeatedPatron(people, "Freguês_1", t1 + new Vector3(-0.55f, 0f, 0.62f), 180f, 0, DrinkingPatron);
            SeatedPatron(people, "Freguês_2", t1 + new Vector3(0.6f, 0f, 0.62f), 180f, 5, MerryPatron);
            SeatedPatron(people, "Freguês_3", t1 + new Vector3(-0.2f, 0f, -0.62f), 0f, 2, ChattyPatron);
            // Mesa do canto e da lareira.
            SeatedPatron(people, "Freguês_4", new Vector3(2.0f - 0.75f, 0f, -3.4f), 90f, 6, QuietPatron);
            SeatedPatron(people, "Freguês_5", new Vector3(2.0f + 0.75f, 0f, -3.4f), -90f, 7, ChattyPatron);
            SeatedPatron(people, "Freguês_6", new Vector3(-4.3f + 0.75f, 0f, -3.9f), -90f, 1, MerryPatron);
            // Banqueta do balcão.
            SeatedPatron(people, "Freguês_7", new Vector3(4.8f, BarSeatTop - SeatTop, 3.2f - 0.75f), 0f, 3, DrinkingPatron);
            // No chão, encostado nos barris perto da lareira.
            SeatedPatron(people, "Freguês_Chao", new Vector3(-5.9f, 0f, -1.7f), -120f, 4, new[] { "Sentado_Chao" }, onFloor: true);
        }

        /// <summary>
        /// Freguês sentado: morador do kit com o avatar humanoide e o <see cref="Patron"/>. <paramref name="seat"/> = meio do assento no chão
        /// (local da sala); o corpo fica um pouco atrás e abaixo (pés no chão).
        /// </summary>
        static void SeatedPatron(Transform parent, string name, Vector3 seat, float yaw, int look, string[] states, bool onFloor = false)
        {
            var (model, palette) = CivilianLooks[look % CivilianLooks.Length];
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(seat - rot * Vector3.forward * SeatBack - Vector3.up * (onFloor ? 0f : SeatDrop), rot);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.55f, 0.05f);
            capsule.radius = 0.28f;
            capsule.height = 1.1f;
            var visual = HumanoidKitCharacter(model, go.transform, CivilianMaterial(model, palette));
            if (states == DrinkingPatron) visual.transform.localPosition = -DrinkingBodyShift;
            var animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = PatronAnimator();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            go.AddComponent<Patron>().Configure(animator, states);
        }

        /// <summary>Animator dos fregueses: um estado por animação de sentar (refeito a cada geração).</summary>
        static RuntimeAnimatorController PatronAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PatronAnimatorPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(PatronAnimatorPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            foreach (var (state, clipName) in SeatedClips)
            {
                var clip = SeatedClip(clipName);
                if (!clip) continue;
                var st = sm.AddState(state);
                st.motion = clip;
                st.writeDefaultValues = true;
            }
            if (sm.states.Length > 0) sm.defaultState = sm.states[0].state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip SeatedClip(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{MixamoImportSettings.Folder}/Sentado/Sentado@{name}.fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__")) return c;
            return null;
        }

        // ---------- Lado de fora: a vila de noite (13/11, pedido do Jhonata: "o lado de fora da taverna está na ilha") ----------

        /// <summary>
        /// Em volta da sala (que fica longe do mapa) a câmera via o fundo preto; depois foi um cais, mas a taverna de verdade fica na vila da
        /// Ilha Sul. Agora é um pedaço da vila à noite, no jeito do entorno de verdade dela: do lado da porta, a rua de terra, a praia e o mar com
        /// o píer e botes; de um lado, a praça com a fonte e as bancas; em volta, casas com janelas acesas, grama facetada como a das ilhas,
        /// árvores, a fogueira e lanternas nos postes. Só visual: o capitão não sai das paredes.
        /// </summary>
        /// <summary>A taverna e o armazém de verdade, para aparecerem do lado de fora de outro interior (13/11).</summary>
        static HouseSpec TavernHouse => new() { name = "Taverna_Vizinha", width = 2, depth = 3, floors = 2, cell = 3.5f, highRoof = true, banner = "banner-red", seed = 4 };
        static HouseSpec WarehouseHouse => new() { name = "Armazem_Vizinho", width = 2, depth = 3, cell = 4f, door = "wall-doorway-square-wide", seed = 2 };

        /// <summary>
        /// <paramref name="neighbors"/> = casas de serviço de verdade em volta (posição local da sala; a porta delas para o lado da praia, como a
        /// da sala), com duas lanternas na porta; as casas e árvores comuns que cairiam em cima delas saem.
        /// </summary>
        static void TavernOutside(Transform root, params (HouseSpec spec, Vector2 at)[] neighbors)
        {
            var w = new GameObject("Vila_de_Fora").transform;
            w.SetParent(root, false);
            foreach (var (spec, at) in neighbors)
            {
                var house = House(w, spec, root.TransformPoint(new Vector3(at.x, 0f, at.y)), 180f);
                float front = spec.depth * spec.cell * 0.5f + 0.6f;
                for (int s = -1; s <= 1; s += 2)
                    NightLantern(Model(Kit.Town, "lantern", w, house.TransformPoint(new Vector3(s * 2.6f, 0f, front)) - w.position, 0f, 1.6f, "Lanterna_Vizinha"));
            }
            bool Blocked(Vector2 p)
            {
                foreach (var (_, at) in neighbors)
                    if ((p - at).sqrMagnitude < 9.5f * 9.5f) return true;
                return false;
            }
            const float sea = -1.1f;

            // Mar ao sul (lado da porta), escuro com um pouco de brilho azul.
            var water = TavernMaterial("M_Taverna_Mar", new Color(0.1f, 0.38f, 0.48f), 0.85f);
            water.SetColor("_EmissionColor", Color.black);
            water.DisableKeyword("_EMISSION");
            water.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            var plane = Part(PrimitiveType.Cube, "Mar", w, new Vector3(0f, sea - 0.05f, -120f), new Vector3(400f, 0.1f, 220f), Quaternion.identity, water);
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Chão: malha facetada com as cores da paleta das ilhas (grama, terra, areia), descendo para o mar ao sul.
            var ground = new GameObject("Chao_da_Vila");
            ground.transform.SetParent(w, false);
            ground.AddComponent<MeshFilter>().sharedMesh = TavernGroundMesh();
            ground.AddComponent<MeshRenderer>().sharedMaterial = KenneyMaterial;

            // Píer a sudeste com os botes; navio ancorado ao sudoeste.
            const float scale = 1.6f;
            for (int i = 0; i < 5; i++)
                Kenney("structure-platform-dock", w, new Vector3(13f, -DockDeckTop * scale + 0.05f, -22f - i * 4f), 0f, scale, "Pier");
            Kenney("boat-row-small", w, new Vector3(16.2f, sea, -29f), 5f, 1.4f, "Bote");
            Kenney("boat-row-large", w, new Vector3(9.8f, sea, -34f), -8f, 1.4f, "Bote");
            Kenney("crate", w, new Vector3(12.3f, 0.05f, -23f), 10f, 1f, "Caixote_Pier");
            Kenney("barrel", w, new Vector3(13.8f, 0.05f, -26f), 0f, 0.7f, "Barril_Pier");
            var ship = new GameObject("Navio_Ancorado").transform;
            ship.SetParent(w, false);
            ship.SetLocalPositionAndRotation(new Vector3(-14f, sea, -46f), Quaternion.Euler(0f, 70f, 0f));
            Kenney("ship-pirate-medium", ship, new Vector3(0f, ShipModelKeel, 0f), 0f, 1f, "Casco");
            NightLantern(Model(Kit.Town, "lantern", ship, new Vector3(0f, 2.3f, -4.6f), 0f, 1.2f, "Lanterna_Popa"), true, 7f, 2f);

            // Casas da vila (as mesmas da Ilha Sul), com janelas que acendem à noite.
            var homes = new (Vector2 pos, float yaw, int floors, bool high, string banner)[]
            {
                (new(-9f, 15.5f), 180f, 1, false, null), (new(10f, 16.5f), 180f, 2, true, "banner-green"),
                (new(20f, 2f), -90f, 1, false, null), (new(-30f, 12f), 90f, 2, true, "banner-red"),
                (new(-31f, -7f), 90f, 1, false, null), (new(27f, 18f), 200f, 1, true, null),
            };
            for (int k = 0; k < homes.Length; k++)
            {
                var h = homes[k];
                if (Blocked(h.pos)) continue;
                House(w, new HouseSpec { name = $"Casa_{k}", width = 2, depth = 2, floors = h.floors, highRoof = h.high, banner = h.banner, seed = 70 + k },
                    root.TransformPoint(new Vector3(h.pos.x, 0f, h.pos.y)), h.yaw);
            }

            // A praça com a fonte e as bancas (oeste), a fogueira (leste).
            Model(Kit.Town, "fountain-round-detail", w, new Vector3(-19f, 0f, -1f), 0f, 2.6f, "Chafariz");
            Model(Kit.Town, "stall-red", w, new Vector3(-15f, 0f, 6f), 160f, 3f, "Banca");
            Model(Kit.Town, "stall-green", w, new Vector3(-24.5f, 0f, -8.5f), 40f, 3f, "Banca");
            Model(Kit.Town, "stall-bench", w, new Vector3(-13.5f, 0f, -4f), 90f, 3f, "Banco");
            if (!Blocked(new Vector2(15f, -6f))) Campfire(w, root.TransformPoint(new Vector3(15f, 0f, -6f)), "Fogueira");

            // Árvores e palmeiras (a praia é de palmeiras), cerca, carga e lanternas da rua.
            var trees = new (string model, Vector2 pos, float yaw)[]
            {
                ("tree", new(-12f, 22f), 0f), ("tree-high", new(2f, 24f), 40f), ("tree-crooked", new(22f, 10f), 80f),
                ("tree", new(-22f, 20f), 120f), ("tree-high-round", new(25f, -4f), 10f), ("tree", new(-36f, 2f), 60f),
            };
            foreach (var t in trees)
                if (!Blocked(t.pos)) Model(Kit.Town, t.model, w, new Vector3(t.pos.x, 0f, t.pos.y), t.yaw, 3f, "Arvore");
            var palms = new (string model, Vector2 pos, float yaw)[]
            {
                ("palm-detailed-bend", new(-8f, -17f), 30f), ("palm-detailed-straight", new(-20f, -15f), 0f),
                ("palm-bend", new(3f, -18f), 200f), ("palm-detailed-bend", new(24f, -15f), 140f), ("palm-straight", new(-30f, -18f), 80f),
            };
            foreach (var p in palms) Kenney(p.model, w, new Vector3(p.pos.x, GroundHeight(p.pos.x, p.pos.y), p.pos.y), p.yaw, 1.3f, "Palmeira");
            for (int i = 0; i < 4; i++)
                if (!Blocked(new Vector2(18f + i * 3f, 9f))) Model(Kit.Town, i == 2 ? "fence-broken" : "fence", w, new Vector3(18f + i * 3f, 0f, 9f), 0f, 3f, "Cerca");
            Kenney("barrel", w, new Vector3(-8.6f, 0f, -7.4f), 0f, 0.7f, "Barril");
            Kenney("barrel", w, new Vector3(-9.6f, 0f, -6.9f), 20f, 0.7f, "Barril");
            Kenney("crate", w, new Vector3(8.8f, 0f, -7.2f), 15f, 1f, "Caixote");
            Kenney("crate-bottles", w, new Vector3(9.4f, 0f, 7.5f), -10f, 1f, "Caixote");
            Furnish(w, Kit.Props, "Bench", new Vector3(4.5f, 0f, -7.3f), 0f, new Vector3(0.9f, SeatTop / 0.53f, 0.9f), "Banco_de_Fora");
            Kenney("flag-pirate-high", w, new Vector3(-12f, 0f, -9.5f), 20f, 1.3f, "Bandeira");
            var posts = new[] { new Vector2(-10f, -10f), new Vector2(10.5f, -10f), new Vector2(-14f, 3f), new Vector2(-25f, 3f), new Vector2(13f, -18.5f), new Vector2(9f, 9f) };
            foreach (var p in posts) PostLantern(w, root.TransformPoint(new Vector3(p.x, 0f, p.y)), "Poste_Rua", true, 9f);

            // O lado de fora segue a hora e o clima de verdade (13/11, pedido do Jhonata): as cores do dia, a luz do ciclo escurece de noite.
        }

        /// <summary>Altura do chão em volta da taverna: plano na vila, descendo para a praia e o mar ao sul.</summary>
        static float GroundHeight(float x, float z)
        {
            if (z > -12f) return 0f;
            return Mathf.Lerp(0f, -2.2f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-12f, -26f, z)));
        }

        /// <summary>
        /// Chão facetado em volta da sala (triângulos de 2,5 m com a altura sorteada de leve, como as ilhas), pintado pela paleta do kit:
        /// areia na praia, terra na rua da porta, no caminho do píer e na praça, grama no resto. Não cobre a sala (o chão dela é de tábuas).
        /// </summary>
        static Mesh TavernGroundMesh()
        {
            string path = MeshesFolder + "/Taverna_Arredores.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = !mesh;
            if (create) mesh = new Mesh { name = "Taverna_Arredores" };
            mesh.Clear();
            const float step = 2.5f, x0 = -45f, x1 = 45f, z0 = -32f, z1 = 40f;
            int nx = Mathf.RoundToInt((x1 - x0) / step), nz = Mathf.RoundToInt((z1 - z0) / step);
            var rng = new System.Random(13);
            var h = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                float x = x0 + i * step, z = z0 + j * step;
                // Perto da sala o chão fica liso e um pouco abaixo do chão dela (passa por baixo das paredes, sem fresta e sem furar as tábuas).
                bool near = Mathf.Abs(x) < RoomHalfX + 3f && Mathf.Abs(z) < RoomHalfZ + 3f;
                h[i, j] = near ? -0.07f : GroundHeight(x, z) + (float)(rng.NextDouble() - 0.5) * 0.12f - 0.02f;
            }
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var m = (a + b + c) / 3f;
                if (Mathf.Abs(m.x) < RoomHalfX - 1.3f && Mathf.Abs(m.z) < RoomHalfZ - 1.3f) return;
                float t = (float)rng.NextDouble();
                bool plaza = new Vector2(m.x + 19f, m.z + 1f).magnitude < 9f;
                bool road = m.z > -12f && m.z < -7.5f || Mathf.Abs(m.x - 13f) < 2.2f && m.z < -7.5f;
                var uv = m.z < -12f ? Palette.Sand(t) : plaza || road ? Palette.Dirt(t) : Palette.Grass(t);
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            Vector3 P(int a, int b) => new(x0 + a * step, h[a, b], z0 + b * step);
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if ((i + j) % 2 == 0)
                {
                    Tri(P(i, j), P(i, j + 1), P(i + 1, j + 1));
                    Tri(P(i, j), P(i + 1, j + 1), P(i + 1, j));
                }
                else
                {
                    Tri(P(i, j), P(i, j + 1), P(i + 1, j));
                    Tri(P(i + 1, j), P(i, j + 1), P(i + 1, j + 1));
                }
            }
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// Lá fora é noite, mas a luz da zona coberta é a quente de dentro da taverna: o que fica fora ganha cópias dos materiais puxadas para
        /// o azul-escuro (as lanternas acesas, o brilho delas e o mar ficam como estão).
        /// </summary>
        static void NightTint(Transform root)
        {
            var tint = new Color(0.36f, 0.42f, 0.62f);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!m || m.IsKeywordEnabled("_EMISSION") || m.name.StartsWith("M_Noite_") || m.name == "M_Taverna_Mar") continue;
                    string path = $"{MaterialsFolder}/M_Noite_{m.name}.mat";
                    var night = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!night)
                    {
                        night = new Material(m) { name = "M_Noite_" + m.name };
                        AssetDatabase.CreateAsset(night, path);
                    }
                    var baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    if (night.HasProperty("_BaseColor")) night.SetColor("_BaseColor", baseColor * tint);
                    EditorUtility.SetDirty(night);
                    mats[i] = night;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // ---------- Luz ----------

        static void TavernLights(Transform root)
        {
            var lights = new GameObject("Luzes").transform;
            lights.SetParent(root, false);
            foreach (var (at, n) in new[] { (new Vector3(-2.6f, RoomWall - 0.1f, -0.2f), "Lustre_Mesa"), (new Vector3(2.4f, RoomWall - 0.1f, -2.4f), "Lustre_Canto") })
            {
                var c = Furnish(lights, Kit.Props, "Chandelier", at, 0f, 0.6f, n);
                // O lustre pende do teto: o pé do suporte fica embaixo, então sobe o tamanho dele.
                c.transform.localPosition -= Vector3.up * 0.85f;
                var l = new GameObject("LuzDoLustre").AddComponent<Light>();
                l.transform.SetParent(c.transform, false);
                l.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.72f, 0.4f);
                l.range = 7.5f;
                l.intensity = 2.4f;
                l.shadows = LightShadows.None;
            }
            foreach (var (at, yaw) in new[]
            {
                // O braço da lanterna sai da parede (+Z do modelo): o meio dela fica ~0,33 m para dentro.
                (new Vector3(-RoomHalfX + 0.43f, 1.3f, -0.6f), 90f), (new Vector3(RoomHalfX - 0.43f, 1.3f, -2.4f), -90f),
                (new Vector3(-0.2f, 1.3f, RoomHalfZ - 0.43f), 180f), (new Vector3(-1.6f, 1.3f, -RoomHalfZ + 0.43f), 0f),
            })
                NightLantern(Furnish(lights, Kit.Props, "Lantern_Wall", at, yaw, 0.5f, "Lanterna_Parede"), true, 5f, 1.6f);
        }
    }
}
