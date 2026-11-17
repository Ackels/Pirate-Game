using System.Collections.Generic;
using Pirates.Controls;
using Pirates.LandCombat;
using Pirates.OnFoot;
using Pirates.Progression;
using Pirates.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Palco da abordagem (04/11, passo 1; cena 07). Pedido do Jhonata: o convés de verdade é pequeno para andar e lutar, então ao
    /// lançar os ganchos o capitão vai para um cenário à parte, longe do mapa (<see cref="BoardingOrigin"/>), com os dois navios lado a
    /// lado em tamanho grande: o modelo do kit × <see cref="BoardingScale"/> (todos os navios do kit têm o convés a 2,1 m e 4,8 m de
    /// largura; muda só o comprimento). O chão não é a malha do modelo: o gerador mede o convés com raios (<see cref="DeckProbe"/>,
    /// células de 25 cm) e faz uma malha de colisão em colunas (convés, amuradas, castelos, mastros), com rampas inclinadas por cima
    /// das escadas (degraus de 1 m na escala 3 não se sobem). Os dois cascos ficam alinhados pela abertura da amurada (portaló) e uma
    /// prancha reta, na altura do convés, passa por ela. Velas
    /// ficam translúcidas quando tapam o capitão. O NavMesh é feito na hora, pelo <see cref="BoardingStage"/>.
    /// Para refazer na cena aberta, sem regerar: Pirates → Protótipo → Abordagem: refazer o palco.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static Vector3 BoardingOrigin => new(-2400f, 0f, 2400f);
        const float BoardingScale = 3f;
        /// <summary>Altura ampliada menos que o resto (04/11, Jhonata: "tudo muito gigante"): amurada de ~1 m, castelos e mastros mais baixos.</summary>
        const float BoardingHeightScale = 2f;
        const float BoardingCell = 0.25f;
        /// <summary>Folga entre os cascos (m, no mundo).</summary>
        const float BoardingGap = 1.2f;
        const float BoardingPlankWidth = 2.2f;

        sealed class BoardingRamp
        {
            public float xMin, xMax, zStart, zEnd, low, high;
        }

        sealed class BoardingDeckData
        {
            public GameObject root;
            public Transform arrive;
            public float deck, deckEdge, halfWidth;
            public EnemyCamp crew;
        }

        [MenuItem("Pirates/Protótipo/Abordagem: refazer o palco (cena aberta)")]
        public static void RebuildBoardingInOpenScene()
        {
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>(FindObjectsInactive.Include);
            if (!switcher || !switcher.Ship || !switcher.Character)
            {
                Debug.LogWarning("[Pirates] Abordagem: abra a cena 07 (sem o PlayerModeSwitcher).");
                return;
            }
            AddBoarding(switcher);
            var scene = switcher.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AddBoarding(PlayerModeSwitcher switcher)
        {
            var ship = switcher.Ship;
            var character = switcher.Character;
            var old = GameObject.Find("Abordagem");
            if (old) Object.DestroyImmediate(old);
            foreach (var c in ship.GetComponents<ShipBoarding>()) Object.DestroyImmediate(c);
            foreach (var c in character.GetComponents<BoardingReturn>()) Object.DestroyImmediate(c);
            var gameHud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (gameHud) foreach (var c in gameHud.GetComponents<BoardingHUD>()) Object.DestroyImmediate(c);

            var root = new GameObject("Abordagem").transform;
            root.position = BoardingOrigin;

            // Navio do jogador à esquerda (bombordo dele... o estibordo, +x, encosta no inimigo); inimigo à direita. Proas para +z.
            var playerModels = new[]
            {
                ("ShipClass_Chalupa", PlayerShipModel), ("ShipClass_Escuna", BossEscortModel), ("ShipClass_Galeao", BossModel),
            };
            var playerDecks = new List<BoardingStage.Deck>();
            BoardingDeckData first = null;
            foreach (var (id, model) in playerModels)
            {
                var data = BuildBoardingDeck(root, model, -1, "Navio_" + id.Replace("ShipClass_", ""));
                first ??= data;
                playerDecks.Add(new BoardingStage.Deck { id = id, root = data.root, arrive = data.arrive });
            }
            var enemy = BuildBoardingDeck(root, EnemyShipModel, +1, "Inimigo_Chalupa", character, BoardingSloopCrew);
            // Brigue Santa Clara (passo 4): o navio grande do kit, a carga da Coroa no convés e o mestre brutamontes na tripulação.
            var brig = BuildBoardingDeck(root, BrigModel, +1, "Inimigo_Brigue", character, BoardingBrigCrew, ShipBoarding.BrigKind);
            // Galeão Tormenta (passo 5): o mesmo modelo do galeão do jogador, de casco escurecido, a tripulação amaldiçoada (esqueletos do
            // KayKit) e o imediato do Lázaro como chefe.
            var tormenta = BuildBoardingDeck(root, BossModel, +1, "Inimigo_Tormenta", character, BoardingTormentaCrew, ShipBoarding.TormentaKind);
            // Caçador e Esquadra Real (passo 6): o casco médio (ship-medium) do caçador e da corveta; o grande do kit para fragata e nau.
            var hunterDeck = BuildBoardingDeck(root, HunterModel, +1, "Inimigo_Cacador", character, BoardingHunterCrew, ShipBoarding.HunterKind);
            var corvette = BuildBoardingDeck(root, HunterModel, +1, "Inimigo_Corveta", character, BoardingCorvetteCrew, ShipBoarding.CorvetteKind);
            var frigate = BuildBoardingDeck(root, BrigModel, +1, "Inimigo_Fragata", character, BoardingFrigateCrew, ShipBoarding.FrigateKind);
            var enemyDecks = new[]
            {
                new BoardingStage.Deck { id = ShipBoarding.SloopKind, root = enemy.root, arrive = enemy.arrive, crew = enemy.crew },
                new BoardingStage.Deck { id = ShipBoarding.BrigKind, root = brig.root, arrive = brig.arrive, crew = brig.crew },
                new BoardingStage.Deck { id = ShipBoarding.TormentaKind, root = tormenta.root, arrive = tormenta.arrive, crew = tormenta.crew },
                new BoardingStage.Deck { id = ShipBoarding.HunterKind, root = hunterDeck.root, arrive = hunterDeck.arrive, crew = hunterDeck.crew },
                new BoardingStage.Deck { id = ShipBoarding.CorvetteKind, root = corvette.root, arrive = corvette.arrive, crew = corvette.crew },
                new BoardingStage.Deck { id = ShipBoarding.FrigateKind, root = frigate.root, arrive = frigate.arrive, crew = frigate.crew },
            };
            var plank = BuildBoardingPlank(root, first, enemy);

            var landingGo = new GameObject("PontoDeChegada");
            landingGo.transform.SetParent(root, false);
            var landing = landingGo.AddComponent<LandingPoint>();
            landing.Configure("Abordagem", first.arrive, 0f, -1f);

            var stage = root.gameObject.AddComponent<BoardingStage>();
            stage.Configure(playerDecks.ToArray(), enemyDecks, plank, landing);
            foreach (var d in playerDecks) d.root.SetActive(false);
            enemy.root.SetActive(false);
            brig.root.SetActive(false);
            tormenta.root.SetActive(false);
            hunterDeck.root.SetActive(false);
            corvette.root.SetActive(false);
            frigate.root.SetActive(false);
            plank.SetActive(false);

            var boarding = ship.gameObject.AddComponent<ShipBoarding>();
            boarding.Configure(switcher, stage);
            var returner = character.gameObject.AddComponent<BoardingReturn>();
            returner.Configure(boarding);
            if (gameHud) gameHud.gameObject.AddComponent<BoardingHUD>().Bind(boarding, returner);
            // Palco refeito: a pintura do navio volta a valer nos conveses do seu navio (17/11).
            if (ship.GetComponent<Pirates.Progression.ShipLivery>()) AddLivery(ship);
        }

        /// <summary>Um navio do palco: o modelo × 3, a malha de colisão medida, as rampas das escadas e alguns objetos no convés.</summary>
        static BoardingDeckData BuildBoardingDeck(Transform stage, string model, int side, string name, FootCharacter player = null,
            string[] crewNames = null, string kind = ShipBoarding.SloopKind)
        {
            const float S = BoardingScale, SY = BoardingHeightScale, c = BoardingCell;
            string meshKey = crewNames != null && kind == ShipBoarding.TormentaKind ? model + "_Tormenta" : model;
            var deckRoot = new GameObject(name).transform;
            deckRoot.SetParent(stage, false);

            // Medida no modelo na escala 1, longe de tudo (o DeckProbe usa a física da cena).
            var probeModel = Kenney(model, null, new Vector3(0f, -600f, 0f), objectName: "SondaAbordagem");
            var bounds = new Bounds(probeModel.transform.position, Vector3.zero);
            foreach (var r in probeModel.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float halfWidth = bounds.extents.x;
            float zMin = bounds.min.z - probeModel.transform.position.z, zMax = bounds.max.z - probeModel.transform.position.z;
            int nx = Mathf.CeilToInt(halfWidth * 2f / c), nz = Mathf.CeilToInt((zMax - zMin) / c);
            float x0 = -nx * c * 0.5f;
            var h = new float[nx, nz];
            var mastRows = new List<float>();
            var hatch = new Bounds();
            bool hasHatch = false;
            var deckUv = new Vector2(0.5f, 0.5f);
            using (var probe = new DeckProbe(probeModel.transform))
            {
                // Coordenada da textura (paleta) da madeira do convés: o som dos passos e a poeira leem a cor do chão por ela.
                foreach (var hit in Physics.RaycastAll(probeModel.transform.TransformPoint(new Vector3(0.9f, 3.05f, 0.35f)), Vector3.down, 3f))
                    if (hit.collider.name == "SondaConves") deckUv = hit.textureCoord;
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = x0 + (i + 0.5f) * c, z = zMin + (j + 0.5f) * c;
                    float y = probe.DeckHeight(x, z);
                    // Sem acerto de 3,05 m = o raio nasceu dentro do castelo de popa: tenta de cima.
                    if (float.IsNaN(y)) y = probe.DeckHeight(x, z, 6f);
                    h[i, j] = y;
                }
                float deckLevel = BoardingMode(h, 1.8f, 2.4f);
                // Faixa na altura do convés presa entre partes altas (04/11, o Jhonata caiu atrás do timão): o raio de 3,05 m nasceu
                // dentro do castelo de popa e enxergou o piso de dentro dele, uma vala de 2,8 m de um lado a outro. Mede de cima.
                var raised = new bool[nz];
                for (int j = 0; j < nz; j++)
                {
                    int up = 0, n = 0;
                    for (int i = 0; i < nx; i++)
                    {
                        if (float.IsNaN(h[i, j])) continue;
                        n++;
                        if (h[i, j] >= deckLevel + 0.5f) up++;
                    }
                    raised[j] = n > 0 && up * 2 >= n;
                }
                for (int j = 0; j < nz; j++)
                {
                    if (raised[j]) continue;
                    bool before = (j > 0 && raised[j - 1]) || (j > 1 && raised[j - 2]);
                    bool after = (j < nz - 1 && raised[j + 1]) || (j < nz - 2 && raised[j + 2]);
                    if (!before || !after) continue;
                    for (int i = 0; i < nx; i++)
                    {
                        float y = probe.DeckHeight(x0 + (i + 0.5f) * c, zMin + (j + 0.5f) * c, 6f);
                        if (!float.IsNaN(y) && (float.IsNaN(h[i, j]) || y > h[i, j])) h[i, j] = y;
                    }
                }
                // Mastros: um raio de lado na altura do peito acerta perto do meio.
                for (int j = 0; j < nz; j++)
                {
                    int mid = nx / 2;
                    float floor = h[mid, j];
                    if (float.IsNaN(floor)) continue;
                    if (!probe.HitsNearCenter(floor + 0.6f, zMin + (j + 0.5f) * c, 0.35f)) continue;
                    mastRows.Add(zMin + (j + 0.5f) * c);
                    for (int i = 0; i < nx; i++)
                        if (Mathf.Abs(x0 + (i + 0.5f) * c) <= 0.3f && !float.IsNaN(h[i, j])) h[i, j] = Mathf.Max(h[i, j], floor + 2.2f);
                }
                // Amuradas e castelos mais altos que o começo do raio (3,05 m; a amurada do bico da chalupa vai a 3,1): o raio nascia dentro
                // da madeira e media o convés embaixo dela, e o capitão e os marujos atravessavam a parede (04/11, Jhonata). Medido também
                // de cima; até 3,65 (topo dos castelos) vale o mais alto. Fora das linhas dos mastros (a verga mais baixa fica a 3,14).
                for (int j = 0; j < nz; j++)
                {
                    float z = zMin + (j + 0.5f) * c;
                    bool nearMast = false;
                    foreach (float m in mastRows) if (Mathf.Abs(m - z) < 0.7f) nearMast = true;
                    if (nearMast) continue;
                    for (int i = 0; i < nx; i++)
                    {
                        if (float.IsNaN(h[i, j])) continue;
                        float top = probe.DeckHeight(x0 + (i + 0.5f) * c, z, 6f);
                        if (!float.IsNaN(top) && top <= 3.65f && top > h[i, j] + 0.3f) h[i, j] = top;
                    }
                }
                // Alçapões e buracos no convés: tapados (na escala 3 viravam um poço de 1,5 m); a tampa de tábuas vai por cima.
                for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < nz - 1; j++)
                {
                    float v = h[i, j];
                    if (float.IsNaN(v) || v >= deckLevel - 0.02f || v < deckLevel - 1f) continue;
                    if (float.IsNaN(h[i - 1, j]) || float.IsNaN(h[i + 1, j]) || float.IsNaN(h[i, j - 1]) || float.IsNaN(h[i, j + 1])) continue;
                    h[i, j] = deckLevel;
                    var cellCenter = new Vector3(x0 + (i + 0.5f) * c, 0f, zMin + (j + 0.5f) * c);
                    if (hasHatch) hatch.Encapsulate(cellCenter);
                    else hatch = new Bounds(cellCenter, Vector3.zero);
                    hasHatch = true;
                }
            }
            Object.DestroyImmediate(probeModel);
            float deck = BoardingMode(h, 1.8f, 2.4f);
            // Abertura da amurada do lado do outro navio (04/11, pedido do Jhonata: a prancha passa por ela, na altura do convés, como
            // num portaló; antes subia por cima das amuradas): a faixa de linhas mais comprida em que, do lado de lá, só há convés até o
            // fim do casco. Fica no mesmo z do palco nos dois navios (o casco anda em z) e continua aberta; as outras bordas fecham.
            int facing = -side;
            bool OpenRow(int j)
            {
                bool beyond = false;
                for (int i = 0; i < nx; i++)
                {
                    float x = (x0 + (i + 0.5f) * c) * facing, v = h[i, j];
                    if (x < 1.4f || float.IsNaN(v)) continue;
                    if (v > deck + 0.08f) return false;
                    if (x > 2.0f) beyond = true;
                }
                return beyond;
            }
            int gangStart = -1, gangLen = 0;
            for (int j = 0; j < nz;)
            {
                if (!OpenRow(j))
                {
                    j++;
                    continue;
                }
                int k = j;
                while (k < nz && OpenRow(k)) k++;
                if (k - j > gangLen)
                {
                    gangLen = k - j;
                    gangStart = j;
                }
                j = k;
            }
            float gangZ = gangLen > 0 ? zMin + (gangStart + gangLen * 0.5f) * c : 0f, gangHalf = gangLen * c * 0.5f;
            bool InGangway(int i, int j) => gangLen > 0 && (x0 + (i + 0.5f) * c) * facing >= 1.4f && j >= gangStart && j < gangStart + gangLen;
            // Borda do casco sempre fechada (04/11, teste de passeio aleatório): a chalupa tem uma abertura na amurada atrás do mastro,
            // com uma saliência na altura do convés por fora; por ali o capitão andava para fora do navio e caía no mar. Toda célula
            // baixa na borda (vizinha fora do casco) vira amurada de 0,5 (1 m no palco).
            var border = new List<(int, int)>();
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (float.IsNaN(h[i, j]) || h[i, j] >= deck + 0.45f || InGangway(i, j)) continue;
                bool outside = i == 0 || j == 0 || i == nx - 1 || j == nz - 1
                    || float.IsNaN(h[i - 1, j]) || float.IsNaN(h[i + 1, j]) || float.IsNaN(h[i, j - 1]) || float.IsNaN(h[i, j + 1]);
                if (outside) border.Add((i, j));
            }
            foreach (var (i, j) in border) h[i, j] = deck + 0.5f;
            var ramps = BoardingRamps(h, nx, nz, x0, zMin, c, gangZ);

            // Altura de cada célula vira coluna de colisão; as rampas vão por cima das escadas (as células delas ficam no chão de baixo).
            var mesh = BoardingColumns(h, nx, nz, x0, zMin, c, S, SY, deckUv);
            mesh.name = "Abordagem_" + meshKey;
            EnsureFolder(Root, "Meshes");
            string meshPath = $"{MeshesFolder}/Abordagem_{meshKey}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            // Posição: os dois cascos com a folga entre eles; a quilha afundada como nos navios do jogo (ShipModelKeel).
            float centerX = side * (halfWidth * S + BoardingGap * 0.5f);
            deckRoot.localPosition = new Vector3(centerX, ShipModelKeel * SY, -gangZ * S);

            var visual = Kenney(model, deckRoot, Vector3.zero, 0f, S, "Modelo");
            visual.transform.localScale = new Vector3(S, SY, S);
            // Mastros, vergas e cestos numa malha à parte, translúcida quando tapa o capitão (04/11: na escala 3 o mastro é uma coluna
            // de ~1 m e escondia o capitão atrás dele).
            SplitBoardingRig(visual, meshKey, h, nx, nz, x0, zMin, c, deck, mastRows);
            if (meshKey != model) DarkenHull(visual);
            // Velas recolhidas (os navios estão presos pelos ganchos): na escala 3 cobriam metade do convés vistas de cima.
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("sail")) r.gameObject.SetActive(false);
                else if (r.name.StartsWith("flag")) Occluder(r.gameObject);
            }

            if (hasHatch)
            {
                // Tampa do alçapão: tábuas do kit no nível do convés (a malha do modelo mostrava o buraco escuro).
                var lid = Kenney("platform-planks", null, Vector3.zero, objectName: "TabuaMolde");
                hatch.Expand(new Vector3(c, 0f, c));
                var y = deck * SY + 0.03f;
                BoardingSlab(deckRoot, "TampaDoAlcapao", new Vector3(hatch.center.x * S, y, hatch.min.z * S),
                    new Vector3(hatch.center.x * S, y, hatch.max.z * S), hatch.size.x * S, lid);
                Object.DestroyImmediate(lid);
            }

            var collision = new GameObject("Colisao");
            collision.transform.SetParent(deckRoot, false);
            collision.AddComponent<MeshCollider>().sharedMesh = mesh;
            // Rampas com tábuas à mostra (04/11, o Jhonata no Tormenta: "o personagem fica flutuando" perto da escada): nos navios grandes
            // cada degrau tem ~0,8 m no palco e a rampa reta passava até 0,78 m acima dos degraus; com as tábuas o pé encosta em madeira.
            var rampPlanks = ramps.Count > 0 ? Kenney("platform-planks", null, Vector3.zero, objectName: "TabuaMolde") : null;
            foreach (var r in ramps)
            {
                var a = new Vector3((r.xMin + r.xMax) * 0.5f * S, r.low * SY, r.zStart * S);
                var b = new Vector3(a.x, r.high * SY, r.zEnd * S);
                BoardingSlab(deckRoot, "Rampa", a, b, (r.xMax - r.xMin) * S, rampPlanks);
            }
            if (rampPlanks) Object.DestroyImmediate(rampPlanks);

            // Fim do casco na abertura (onde a prancha encosta).
            float outline = 0f;
            for (int j = Mathf.Max(0, gangStart); j < gangStart + gangLen; j++)
            for (int i = 0; i < nx; i++)
                if (!float.IsNaN(h[i, j])) outline = Mathf.Max(outline, (x0 + (i + 0.5f) * c) * facing + c * 0.5f);
            if (outline <= 0f) outline = halfWidth - 0.2f;

            var data = new BoardingDeckData { root = deckRoot.gameObject, deck = deck, deckEdge = outline, halfWidth = halfWidth };
            var taken = BoardingProps(deckRoot, h, nx, nz, x0, zMin, c, deck, side, ramps, gangZ, gangHalf, out var free, out var put);
            if (crewNames != null && player) data.crew = BoardingCrew(deckRoot, h, nx, nz, x0, zMin, c, deck, side, ramps, gangZ, gangHalf, taken,
                player, crewNames, kind);
            // Decoração do navio (depois da tripulação: desvia dos lugares dos piratas).
            if (crewNames != null && kind == ShipBoarding.BrigKind) BrigCargo(free, put, taken, nx, nz, x0, zMin, c, side);
            if (crewNames != null && kind == ShipBoarding.TormentaKind) TormentaCargo(free, put, taken, nx, nz, x0, zMin, c, side);
            if (crewNames != null && (kind == ShipBoarding.HunterKind || kind == ShipBoarding.CorvetteKind || kind == ShipBoarding.FrigateKind))
                NavyCargo(free, put, taken, nx, nz, x0, zMin, c, side, kind == ShipBoarding.HunterKind);
            // Navios da Coroa: bandeiras azuis, como no mar.
            if (crewNames != null && (kind == ShipBoarding.CorvetteKind || kind == ShipBoarding.FrigateKind)) CrownFlags(deckRoot);

            // Chegada: no convés, na frente da abertura, virado para o outro navio.
            var arrive = new GameObject("Chegada").transform;
            arrive.SetParent(deckRoot, false);
            arrive.localPosition = new Vector3(facing * (outline * S - 3.4f), deck * SY + 0.05f, gangZ * S);
            arrive.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
            data.arrive = arrive;
            return data;
        }

        /// <summary>Prancha reta na altura do convés, de abertura a abertura, com corrimão de corda e as cordas dos ganchos.</summary>
        static GameObject BuildBoardingPlank(Transform stage, BoardingDeckData player, BoardingDeckData enemy)
        {
            const float SY = BoardingHeightScale, S = BoardingScale, w = BoardingPlankWidth;
            var plank = new GameObject("Prancha").transform;
            plank.SetParent(stage, false);
            float keel = ShipModelKeel * SY;
            float px = player.root.transform.localPosition.x + player.deckEdge * S;
            float ex = enemy.root.transform.localPosition.x - enemy.deckEdge * S;
            float y = Mathf.Max(player.deck, enemy.deck) * SY + keel + 0.04f;
            var planks = Kenney("platform-planks", null, Vector3.zero, objectName: "TabuaMolde");
            // Entra um pouco em cada convés (as tábuas apoiadas nele).
            BoardingSlab(plank, "Ponte", new Vector3(px - 1f, y, 0f), new Vector3(ex + 1f, y, 0f), w, planks);
            Object.DestroyImmediate(planks);
            float railTop = y + 0.95f;
            // Cordas dos ganchos: das amuradas de um para o outro, na frente e atrás da prancha.
            foreach (float dz in new[] { -4.5f, 4.5f, -9f })
                BoardingRope(plank, new Vector3(px - 0.3f, railTop, dz), new Vector3(ex + 0.3f, railTop, dz * 0.8f), 0.05f, KenneyColors.Cloth);
            // Corrimão de corda nos dois lados, só no vão entre os cascos (nos conveses a amurada faz o papel): colisor invisível na altura
            // da cintura, cordas e postes por cima.
            foreach (float side in new[] { -1f, 1f })
            {
                float rz = side * (w * 0.5f - 0.08f);
                var guard = new GameObject("Corrimao").transform;
                guard.SetParent(plank, false);
                guard.localPosition = new Vector3((px + ex) * 0.5f, y + 0.55f, rz);
                guard.gameObject.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(ex - px) + 0.4f, 1.1f, 0.14f);
                foreach (float x in new[] { px, ex })
                    BoardingRope(plank, new Vector3(x, y, rz), new Vector3(x, y + 1f, rz), 0.09f, KenneyColors.WoodDark);
                BoardingRope(plank, new Vector3(px, y + 0.95f, rz), new Vector3(ex, y + 0.95f, rz), 0.045f, KenneyColors.Cloth);
                BoardingRope(plank, new Vector3(px, y + 0.5f, rz), new Vector3(ex, y + 0.5f, rz), 0.035f, KenneyColors.Cloth);
            }
            return plank.gameObject;
        }

        /// <summary>
        /// Tira da malha do casco (cópia em Meshes/Abordagem_*_Casco) os triângulos do aparelho: acima de 4,45 m (cestos, vergas altas) ou
        /// acima de 2,75 m perto de um mastro e sobre o convés principal (tronco e vergas baixas); eles vão para "Mastros", com a
        /// transparência da câmera. Os castelos (tombadilho, timão) ficam no casco.
        /// </summary>
        static void SplitBoardingRig(GameObject visual, string model, float[,] h, int nx, int nz, float x0, float zMin, float c, float deck,
            List<float> mastRows)
        {
            var filter = visual.GetComponent<MeshFilter>();
            var renderer = visual.GetComponent<MeshRenderer>();
            if (!filter || !renderer || !filter.sharedMesh) return;
            var source = filter.sharedMesh;
            var vertices = source.vertices;
            bool MainDeckUnder(Vector3 p)
            {
                int i = Mathf.FloorToInt((p.x - x0) / c), j = Mathf.FloorToInt((p.z - zMin) / c);
                if (i < 0 || j < 0 || i >= nx || j >= nz || float.IsNaN(h[i, j])) return false;
                float v = h[i, j];
                return v <= deck + 0.55f || (v >= deck + 2.1f && v <= deck + 2.3f);
            }
            bool NearMast(float z)
            {
                foreach (float m in mastRows) if (Mathf.Abs(m - z) < 0.6f) return true;
                return false;
            }
            int subs = source.subMeshCount;
            var hullTris = new List<int>[subs];
            var rigTris = new List<int>[subs];
            for (int s = 0; s < subs; s++)
            {
                hullTris[s] = new List<int>();
                rigTris[s] = new List<int>();
                var tris = source.GetTriangles(s);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    var centroid = (vertices[tris[t]] + vertices[tris[t + 1]] + vertices[tris[t + 2]]) / 3f;
                    bool isRig = centroid.y > 4.45f || (centroid.y > 2.75f && NearMast(centroid.z) && MainDeckUnder(centroid));
                    var list = isRig ? rigTris[s] : hullTris[s];
                    list.Add(tris[t]);
                    list.Add(tris[t + 1]);
                    list.Add(tris[t + 2]);
                }
            }
            Mesh Copy(List<int>[] parts, string suffix)
            {
                // Só os vértices usados (a caixa da malha decide quando ela fica translúcida).
                var normals = source.normals;
                var uvs = new List<Vector2>();
                source.GetUVs(0, uvs);
                var map = new Dictionary<int, int>();
                var v = new List<Vector3>();
                var nrm = new List<Vector3>();
                var uv = new List<Vector2>();
                var tris = new List<int>[subs];
                for (int s = 0; s < subs; s++)
                {
                    tris[s] = new List<int>(parts[s].Count);
                    foreach (int k in parts[s])
                    {
                        if (!map.TryGetValue(k, out int at))
                        {
                            at = v.Count;
                            map[k] = at;
                            v.Add(vertices[k]);
                            if (normals.Length == vertices.Length) nrm.Add(normals[k]);
                            if (uvs.Count == vertices.Length) uv.Add(uvs[k]);
                        }
                        tris[s].Add(at);
                    }
                }
                var mesh = new Mesh { name = $"Abordagem_{model}_{suffix}" };
                mesh.SetVertices(v);
                if (nrm.Count == v.Count) mesh.SetNormals(nrm);
                if (uv.Count == v.Count) mesh.SetUVs(0, uv);
                mesh.subMeshCount = subs;
                for (int s = 0; s < subs; s++) mesh.SetTriangles(tris[s], s);
                if (nrm.Count != v.Count) mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                string path = $"{MeshesFolder}/{mesh.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing)
                {
                    existing.Clear();
                    EditorUtility.CopySerialized(mesh, existing);
                    return existing;
                }
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            filter.sharedMesh = Copy(hullTris, "Casco");
            var rig = new GameObject("Mastros");
            rig.transform.SetParent(visual.transform, false);
            rig.AddComponent<MeshFilter>().sharedMesh = Copy(rigTris, "Mastros");
            rig.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            Occluder(rig);
        }

        /// <summary>Cilindro sem colisor de a até b (corda ou poste).</summary>
        static void BoardingRope(Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.name = "Corda";
            Object.DestroyImmediate(rope.GetComponent<Collider>());
            rope.transform.SetParent(parent, false);
            rope.transform.localPosition = (a + b) * 0.5f;
            rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            rope.transform.localScale = new Vector3(thickness, (b - a).magnitude * 0.5f, thickness);
            rope.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Tábua inclinada de a até b (alto do tampo), com colisor de caixa; com modelo, as tábuas do kit esticadas por cima.</summary>
        static void BoardingSlab(Transform parent, string name, Vector3 a, Vector3 b, float width, GameObject planks)
        {
            const float thick = 0.3f;
            var d = b - a;
            float length = d.magnitude;
            var slab = new GameObject(name).transform;
            slab.SetParent(parent, false);
            var rotation = Quaternion.LookRotation(d / length, Vector3.up);
            slab.localRotation = rotation;
            slab.localPosition = (a + b) * 0.5f - rotation * Vector3.up * (thick * 0.5f);
            var box = slab.gameObject.AddComponent<BoxCollider>();
            // Um pouco mais comprida: sem quina nas pontas.
            box.size = new Vector3(width, thick, length + 0.5f);
            if (!planks) return;
            var visual = Object.Instantiate(planks, slab, false);
            visual.name = "Tabuas";
            var mb = visual.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(width / mb.size.x, thick / Mathf.Max(0.01f, mb.size.y), (length + 0.3f) / mb.size.z);
            visual.transform.localPosition = -Vector3.Scale(mb.center, visual.transform.localScale);
        }

        /// <summary>Altura mais comum entre lo e hi (o convés principal).</summary>
        static float BoardingMode(float[,] h, float lo, float hi)
        {
            var counts = new Dictionary<int, int>();
            foreach (float v in h)
            {
                if (float.IsNaN(v) || v < lo || v > hi) continue;
                int k = Mathf.RoundToInt(v * 20f);
                counts[k] = counts.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            int best = 0, bestN = -1;
            foreach (var kv in counts)
                if (kv.Value > bestN)
                {
                    best = kv.Key;
                    bestN = kv.Value;
                }
            return best / 20f;
        }

        /// <summary>
        /// Escadas: em cada coluna (x), uma subida em degraus pequenos ao longo de z, de um chão plano grande a outro chão plano grande.
        /// As colunas vizinhas com a mesma escada viram uma rampa; as células dos degraus descem ao chão de baixo. Inclinação no
        /// máximo ~38° (entra no chão de baixo se precisar).
        /// </summary>
        static List<BoardingRamp> BoardingRamps(float[,] h, int nx, int nz, float x0, float zMin, float c, float plankZ)
        {
            var levels = new Dictionary<int, int>();
            foreach (float v in h)
                if (!float.IsNaN(v)) levels[Mathf.RoundToInt(v * 20f)] = levels.TryGetValue(Mathf.RoundToInt(v * 20f), out int n) ? n + 1 : 1;
            bool BigFloor(float v) => levels.TryGetValue(Mathf.RoundToInt(v * 20f), out int n) && n >= 30;
            bool Near(float a, float b) => Mathf.Abs(a - b) < 0.03f;

            // (coluna, início, fim, direção, baixo, alto)
            var found = new List<(int i, int j0, int jk, int dir, float low, float high)>();
            for (int i = 0; i < nx; i++)
            foreach (int dir in new[] { 1, -1 })
            {
                for (int j0 = 0; j0 < nz; j0++)
                {
                    int prev = j0 - dir, next = j0 + dir;
                    if (prev < 0 || prev >= nz || next < 0 || next >= nz) continue;
                    float a = h[i, j0];
                    if (float.IsNaN(a) || !Near(h[i, prev], a) || !BigFloor(a)) continue;
                    // Degraus de 25 cm de fundo aparecem em uma ou duas células: subida = sobe até 0,46 ou repete a altura; três
                    // células iguais = chegou em cima.
                    int j = next, rises = 0, top = j0, flat = 0, cells = 0;
                    float last = a;
                    while (j >= 0 && j < nz && !float.IsNaN(h[i, j]) && cells < 16)
                    {
                        float d = h[i, j] - last;
                        if (d > 0.05f && d < 0.46f)
                        {
                            last = h[i, j];
                            top = j;
                            rises++;
                            flat = 0;
                        }
                        else if (Mathf.Abs(d) < 0.03f && rises > 0)
                        {
                            if (++flat >= 2) break;
                        }
                        else break;
                        j += dir;
                        cells++;
                    }
                    // Subida de pelo menos 0,65: o castelo de proa sobe 0,7 e o tombadilho 1,4; a amurada (0,5) não é escada.
                    if (rises < 2 || flat < 1 || last - a < 0.65f || !BigFloor(last)) continue;
                    found.Add((i, j0, top, dir, a, last));
                }
            }

            var ramps = new List<BoardingRamp>();
            var used = new bool[found.Count];
            for (int f = 0; f < found.Count; f++)
            {
                if (used[f]) continue;
                var s = found[f];
                int iMin = s.i, iMax = s.i;
                used[f] = true;
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int g = 0; g < found.Count; g++)
                    {
                        if (used[g]) continue;
                        var o = found[g];
                        if (o.j0 != s.j0 || o.jk != s.jk || o.dir != s.dir || !Near(o.low, s.low) || !Near(o.high, s.high)) continue;
                        if (o.i != iMin - 1 && o.i != iMax + 1) continue;
                        iMin = Mathf.Min(iMin, o.i);
                        iMax = Mathf.Max(iMax, o.i);
                        used[g] = true;
                        grew = true;
                    }
                }
                // Escada estreita (uma coluna) é a amurada subindo: fica.
                if (iMax - iMin < 1) continue;
                float zStart = zMin + (s.j0 + 0.5f) * c + s.dir * c * 0.5f;
                float zEnd = zMin + (s.jk + 0.5f) * c - s.dir * c * 0.5f;
                float rise = s.high - s.low, run = Mathf.Abs(zEnd - zStart);
                float need = rise / Mathf.Tan(38f * Mathf.Deg2Rad);
                // Entra no chão de baixo o que faltar (só por cima de chão plano).
                int back = 0;
                bool nearEdge = Mathf.Max(Mathf.Abs(x0 + iMin * c), Mathf.Abs(x0 + (iMax + 1) * c)) > 0.8f;
                while (run + back * c < need && back < 8)
                {
                    // Não entra no corredor da prancha (a rampa tranca a passagem).
                    if (nearEdge && Mathf.Abs(zStart - s.dir * (back + 1) * c - plankZ) < 0.65f) break;
                    int jb = s.j0 - s.dir * back;
                    bool flat = jb >= 0 && jb < nz;
                    for (int i = iMin; flat && i <= iMax; i++) flat = Near(h[i, jb], s.low);
                    if (!flat) break;
                    back++;
                }
                zStart -= s.dir * back * c;
                for (int i = iMin; i <= iMax; i++)
                for (int j = s.j0 + s.dir; j != s.jk; j += s.dir)
                    h[i, j] = s.low;
                // Degraus pela metade ao lado da rampa viram parede na altura de cima (04/11: o capitão saía de lado da rampa e ficava
                // preso entre ela e a parede do timão).
                foreach (int i in new[] { iMin - 1, iMax + 1 })
                {
                    if (i < 0 || i >= nx) continue;
                    for (int j = s.j0 + s.dir; j != s.jk + s.dir; j += s.dir)
                        if (!float.IsNaN(h[i, j]) && h[i, j] > s.low + 0.05f && h[i, j] < s.high - 0.05f) h[i, j] = s.high;
                }
                ramps.Add(new BoardingRamp
                {
                    xMin = x0 + iMin * c, xMax = x0 + (iMax + 1) * c, zStart = zStart, zEnd = zEnd, low = s.low, high = s.high,
                });
            }
            return ramps;
        }

        /// <summary>Malha de colisão em colunas: o alto de cada célula e as paredes onde a vizinha é mais baixa (ou fora do casco).</summary>
        static Mesh BoardingColumns(float[,] h, int nx, int nz, float x0, float zMin, float c, float S, float SY, Vector2 uv)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Vector3 facing)
            {
                // a, b, d, e em volta; vira se a normal sair para o lado errado.
                if (Vector3.Dot(Vector3.Cross(b - a, d - a), facing) < 0f) (b, e) = (e, b);
                int k = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(d);
                vertices.Add(e);
                triangles.Add(k);
                triangles.Add(k + 1);
                triangles.Add(k + 2);
                triangles.Add(k);
                triangles.Add(k + 2);
                triangles.Add(k + 3);
            }
            float Hgt(int i, int j) => i < 0 || j < 0 || i >= nx || j >= nz || float.IsNaN(h[i, j]) ? 0f : h[i, j];
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float v = h[i, j];
                if (float.IsNaN(v)) continue;
                float xa = (x0 + i * c) * S, xb = (x0 + (i + 1) * c) * S, za = (zMin + j * c) * S, zb = (zMin + (j + 1) * c) * S, y = v * SY;
                Quad(new Vector3(xa, y, za), new Vector3(xa, y, zb), new Vector3(xb, y, zb), new Vector3(xb, y, za), Vector3.up);
                float n;
                if ((n = Hgt(i - 1, j) * SY) < y)
                    Quad(new Vector3(xa, n, za), new Vector3(xa, y, za), new Vector3(xa, y, zb), new Vector3(xa, n, zb), Vector3.left);
                if ((n = Hgt(i + 1, j) * SY) < y)
                    Quad(new Vector3(xb, n, za), new Vector3(xb, y, za), new Vector3(xb, y, zb), new Vector3(xb, n, zb), Vector3.right);
                if ((n = Hgt(i, j - 1) * SY) < y)
                    Quad(new Vector3(xa, n, za), new Vector3(xa, y, za), new Vector3(xb, y, za), new Vector3(xb, n, za), Vector3.back);
                if ((n = Hgt(i, j + 1) * SY) < y)
                    Quad(new Vector3(xa, n, zb), new Vector3(xa, y, zb), new Vector3(xb, y, zb), new Vector3(xb, n, zb), Vector3.forward);
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var uvs = new List<Vector2>(vertices.Count);
            for (int k = 0; k < vertices.Count; k++) uvs.Add(uv);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Objetos no convés principal: canhões encostados nas amuradas (pela largura do convés em cada linha: a proa afina e os canhões de
        /// posição fixa atravessavam o casco, 04/11), barris do lado de fora e caixotes no meio; longe da abertura da prancha e das escadas.
        /// </summary>
        static List<Vector2> BoardingProps(Transform deckRoot, float[,] h, int nx, int nz, float x0, float zMin, float c, float deck, int side,
            List<BoardingRamp> ramps, float gangZ, float gangHalf, out System.Func<float, float, float, bool> free,
            out System.Action<string, float, float, float, float> put)
        {
            const float S = BoardingScale, SY = BoardingHeightScale;
            bool Flat(float x, float z, float radius)
            {
                for (float dx = -radius; dx <= radius + 0.001f; dx += c)
                for (float dz = -radius; dz <= radius + 0.001f; dz += c)
                {
                    int i = Mathf.FloorToInt((x + dx - x0) / c), j = Mathf.FloorToInt((z + dz - zMin) / c);
                    if (i < 0 || j < 0 || i >= nx || j >= nz || float.IsNaN(h[i, j]) || Mathf.Abs(h[i, j] - deck) > 0.03f) return false;
                }
                return true;
            }
            bool InCorridor(float x, float z)
            {
                if (x * -side > -0.2f && Mathf.Abs(z - gangZ) < gangHalf + 0.45f) return true;
                foreach (var r in ramps)
                    if (x > r.xMin - 0.45f && x < r.xMax + 0.45f && z > Mathf.Min(r.zStart, r.zEnd) - 0.6f && z < Mathf.Max(r.zStart, r.zEnd) + 0.6f)
                        return true;
                return false;
            }
            bool Free(float x, float z, float r) => Flat(x, z, r) && !InCorridor(x, z);
            // Borda do convés na linha j, do lado s: do meio para fora até a primeira célula que não é convés.
            float RowEdge(int j, int s)
            {
                float last = 0f;
                for (int k = 0; k < nx; k++)
                {
                    int i = s > 0 ? k : nx - 1 - k;
                    float x = (x0 + (i + 0.5f) * c) * s;
                    if (x < 0.5f) continue;
                    float v = h[i, j];
                    if (float.IsNaN(v) || Mathf.Abs(v - deck) > 0.03f) return x - c * 0.5f;
                    last = x + c * 0.5f;
                }
                return last;
            }
            var taken = new List<Vector2>();
            var props = new GameObject("Objetos").transform;
            props.SetParent(deckRoot, false);
            void Put(string model, float x, float z, float yaw, float size)
            {
                var go = model.StartsWith("gy:")
                    ? Model(Kit.Graveyard, model.Substring(3), props, new Vector3(x * S, deck * SY, z * S), yaw, 1f)
                    : Kenney(model, props, new Vector3(x * S, deck * SY, z * S), yaw, 1f);
                float largest = 0f;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var e = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                    largest = Mathf.Max(largest, Mathf.Max(e.x, Mathf.Max(e.y, e.z)));
                }
                if (largest > 0.01f) go.transform.localScale *= size / largest;
                BoardingBoxCollider(go);
                taken.Add(new Vector2(x, z));
            }
            const float cannonLength = 1.9f, cannonHalf = cannonLength * 0.5f / S;
            foreach (int s in new[] { -1, 1 })
            {
                // Lugares ao longo da amurada, de proa a popa: o canhão de boca encostada nela.
                var spots = new List<(float z, float edge)>();
                for (int j = nz - 1; j >= 0; j--)
                {
                    if (j == 0 || j == nz - 1) continue;
                    // A borda mais estreita entre esta linha e as vizinhas: o canhão (largo, com as rodas) ocupa as três, e onde a amurada
                    // curva ele a atravessaria.
                    float z = zMin + (j + 0.5f) * c, edge = Mathf.Min(RowEdge(j, s), Mathf.Min(RowEdge(j - 1, s), RowEdge(j + 1, s)));
                    if (edge < 1.2f) continue;
                    float x = s * (edge - cannonHalf - 0.03f);
                    if (!Free(x, z, 0.2f) || !Free(s * (edge - 0.2f), z, 0.1f)) continue;
                    spots.Add((z, edge));
                }
                if (spots.Count == 0) continue;
                var cannons = new List<float>();
                foreach (var (z, edge) in spots)
                {
                    if (cannons.Count >= 3) break;
                    if (cannons.Count > 0 && cannons[^1] - z < 1.15f) continue;
                    Put("cannon", s * (edge - cannonHalf - 0.03f), z, s > 0 ? 90f : -90f, cannonLength);
                    cannons.Add(z);
                }
                if (s != side) continue;
                // Barris do lado de fora, entre os canhões.
                foreach (var (z, edge) in spots)
                {
                    bool clear = true;
                    foreach (float cz in cannons) if (Mathf.Abs(cz - z) < 0.55f) clear = false;
                    if (!clear) continue;
                    float x = s * (edge - 0.2f);
                    Put("barrel", x, z, 0f, 1f);
                    if (Free(x, z + 0.25f, 0.12f)) Put("barrel", x, z + 0.25f, 40f, 1f);
                    if (Free(x - s * 0.25f, z + 0.1f, 0.12f)) Put("barrel", x - s * 0.25f, z + 0.1f, 75f, 1f);
                    break;
                }
            }
            // Caixotes perto do meio do convés (longe do mastro e da abertura).
            for (int j = nz - 1; j >= 0; j--)
            {
                float z = zMin + (j + 0.5f) * c - 0.3f, x = side * 0.75f;
                if (!Free(x, z, 0.35f) || !Free(x, z - 0.4f, 0.3f)) continue;
                Put("crate", x, z, 15f, 0.95f);
                Put("crate-bottles", x - side * 0.05f, z - 0.4f, -10f, 0.85f);
                break;
            }
            free = Free;
            put = Put;
            return taken;
        }

        /// <summary>
        /// Convés do Santa Clara (passo 4): era um mercante da Coroa tomado pela Âncora Negra, então leva a carga dela: baús de prata,
        /// pilhas de caixotes, balas de canhão empilhadas e o escaler no meio do convés. Cada peça vai no lugar livre mais longe das que já
        /// estão (espalha pelo convés e deixa caminho para a luta).
        /// </summary>
        static void BrigCargo(System.Func<float, float, float, bool> free, System.Action<string, float, float, float, float> put, List<Vector2> taken,
            int nx, int nz, float x0, float zMin, float c, int side)
        {
            bool Place(string model, float size, float radius, float yaw, float minGap)
            {
                Vector2 best = default;
                float bestD = -1f;
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector2(x0 + (i + 0.5f) * c, zMin + (j + 0.5f) * c);
                    if (!free(p.x, p.y, radius)) continue;
                    float d = float.MaxValue;
                    foreach (var t in taken) d = Mathf.Min(d, (t - p).magnitude);
                    if (d < minGap || d <= bestD) continue;
                    bestD = d;
                    best = p;
                }
                if (bestD < 0f) return false;
                put(model, best.x, best.y, yaw, size);
                return true;
            }
            // O escaler, deitado no meio (o maior: vai primeiro).
            Place("boat-row-small", 3.4f, 0.55f, 0f, 0.9f);
            // Baús da prata da Coroa e caixotes empilhados aos pares.
            for (int k = 0; k < 2; k++) Place("chest", 0.95f, 0.22f, side > 0 ? -90f : 90f, 0.6f);
            for (int k = 0; k < 3; k++)
            {
                int before = taken.Count;
                if (!Place("crate", 1.0f, 0.3f, 10f + 25f * k, 0.6f)) break;
                var at = taken[before];
                if (free(at.x + 0.36f, at.y, 0.14f)) put("crate-bottles", at.x + 0.36f, at.y, -15f, 0.85f);
            }
            // Balas de canhão empilhadas (pirâmide de 4) perto da amurada.
            for (int k = 0; k < 2; k++)
            {
                int before = taken.Count;
                if (!Place("cannon-ball", 0.32f, 0.12f, 0f, 0.5f)) break;
                var at = taken[before];
                const float o = 0.06f;
                put("cannon-ball", at.x + o * 2f, at.y, 0f, 0.32f);
                put("cannon-ball", at.x + o, at.y + o * 1.7f, 0f, 0.32f);
            }
        }

        /// <summary>
        /// Convés do Tormenta (passo 5): o navio do Lázaro, que anda atrás da prata amaldiçoada. Caixão velho, baús, urnas, velas acesas e
        /// lanternas do Graveyard Kit, e balas de canhão empilhadas.
        /// </summary>
        static void TormentaCargo(System.Func<float, float, float, bool> free, System.Action<string, float, float, float, float> put,
            List<Vector2> taken, int nx, int nz, float x0, float zMin, float c, int side)
        {
            bool Place(string model, float size, float radius, float yaw, float minGap)
            {
                Vector2 best = default;
                float bestD = -1f;
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector2(x0 + (i + 0.5f) * c, zMin + (j + 0.5f) * c);
                    if (!free(p.x, p.y, radius)) continue;
                    float d = float.MaxValue;
                    foreach (var t in taken) d = Mathf.Min(d, (t - p).magnitude);
                    if (d < minGap || d <= bestD) continue;
                    bestD = d;
                    best = p;
                }
                if (bestD < 0f) return false;
                put(model, best.x, best.y, yaw, size);
                return true;
            }
            Place("gy:coffin-old", 2.1f, 0.42f, 90f, 0.9f);
            for (int k = 0; k < 2; k++) Place("chest", 0.95f, 0.22f, side > 0 ? -90f : 90f, 0.6f);
            for (int k = 0; k < 2; k++) Place("gy:urn-round", 0.7f, 0.15f, 30f * k, 0.6f);
            for (int k = 0; k < 3; k++) Place("gy:candle-multiple", 0.55f, 0.12f, 50f * k, 0.55f);
            for (int k = 0; k < 2; k++) Place("gy:lantern-candle", 0.6f, 0.12f, 0f, 0.6f);
            Place("barrel", 1f, 0.2f, 0f, 0.6f);
        }

        static void CrownFlags(Transform deckRoot)
        {
            var blue = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Bandeira_Mercante.mat");
            if (!blue) return;
            foreach (var r in deckRoot.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("flag"))
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = blue;
                    r.sharedMaterials = slots;
                }
        }

        /// <summary>Casco do Tormenta mais escuro (quase preto, com um toque azulado): cópia do material do modelo, só nesse convés.</summary>
        static void DarkenHull(GameObject visual)
        {
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i]) continue;
                    string path = $"{MaterialsFolder}/{slots[i].name}_Tormenta.mat";
                    var dark = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!dark)
                    {
                        dark = new Material(slots[i]) { name = slots[i].name + "_Tormenta" };
                        AssetDatabase.CreateAsset(dark, path);
                    }
                    if (dark.HasProperty("_BaseColor")) dark.SetColor("_BaseColor", slots[i].GetColor("_BaseColor") * new Color(0.6f, 0.58f, 0.68f));
                    EditorUtility.SetDirty(dark);
                    slots[i] = dark;
                }
                r.sharedMaterials = slots;
            }
        }

        /// <summary>
        /// Tripulação do Tormenta (passo 5): o imediato primeiro (chefe: espera a tripulação cair, como o Barba-Negra com a Guarda Negra) e
        /// os esqueletos do KayKit: guerreiros de chifre e soldados de cutelo, ladinos encapuzados de pistola e o mago de bacamarte.
        /// </summary>
        static readonly string[] BoardingTormentaCrew =
        {
            "Chefe_ImediatoTormenta", "Pirata_Tormenta_1", "Pirata_Tormenta_2", "Atirador_Tormenta_1", "Pirata_Tormenta_3", "Bacamarteiro_Tormenta",
            "Pirata_Tormenta_4", "Atirador_Tormenta_2", "Pirata_Tormenta_5", "Pirata_Tormenta_6",
        };

        /// <summary>Modelo do KayKit de cada tripulante pelo tipo do navio e o posto (null = os piratas do Quaternius, com as cores da tripulação).</summary>
        static string BoardingLook(string kind, string name, int k) => kind switch
        {
            ShipBoarding.BrigKind => IsBrute(name) ? KayKitCharacters.Barbarian : null,
            ShipBoarding.TormentaKind => TormentaLook(name, k),
            ShipBoarding.HunterKind =>
                IsDuelist(name) ? KayKitCharacters.Bandit :
                name.StartsWith("Atirador") ? (k % 2 == 0 ? KayKitCharacters.Ranger : KayKitCharacters.Bandit) :
                k % 2 == 0 ? KayKitCharacters.Hooded : KayKitCharacters.Bandit,
            ShipBoarding.CorvetteKind or ShipBoarding.FrigateKind =>
                name.StartsWith("Atirador") || name.StartsWith("Bacamarteiro") ? KayKitCharacters.Ranger : KayKitCharacters.Knight,
            _ => null,
        };

        /// <summary>Caçador (passo 6): o caçador-chefe (duelista) primeiro; mercenários encapuzados e de lenço, a atiradora.</summary>
        static readonly string[] BoardingHunterCrew =
        {
            "Duelista_CacadorChefe", "Pirata_Cacador_1", "Atirador_Cacador_1", "Pirata_Cacador_2", "Bacamarteiro_Cacador", "Pirata_Cacador_3",
            "Atirador_Cacador_2", "Pirata_Cacador_4", "Pirata_Cacador_5", "Pirata_Cacador_6",
        };

        /// <summary>Esquadra Real (passo 6): o oficial (sabre, como a Guarda Negra) primeiro; fuzileiros cavaleiros e atiradoras.</summary>
        static readonly string[] BoardingCorvetteCrew =
        {
            "GuardaNegra_OficialCoroa", "Fuzileiro_Coroa_1", "Atirador_Coroa_1", "Fuzileiro_Coroa_2", "Fuzileiro_Coroa_3", "Atirador_Coroa_2",
            "Fuzileiro_Coroa_4", "Fuzileiro_Coroa_5",
        };

        static readonly string[] BoardingFrigateCrew =
        {
            "GuardaNegra_OficialCoroa", "Fuzileiro_Coroa_1", "Atirador_Coroa_1", "Fuzileiro_Coroa_2", "Bacamarteiro_Coroa", "Fuzileiro_Coroa_3",
            "Atirador_Coroa_2", "Fuzileiro_Coroa_4", "Fuzileiro_Coroa_5", "Fuzileiro_Coroa_6",
        };

        /// <summary>
        /// Convés dos navios da Coroa (passo 6): arrumado, de navio de guerra: balas empilhadas em pirâmide, barris de pólvora em fila e
        /// caixotes de mantimentos. Caçador: o baú da recompensa e garrafas.
        /// </summary>
        static void NavyCargo(System.Func<float, float, float, bool> free, System.Action<string, float, float, float, float> put,
            List<Vector2> taken, int nx, int nz, float x0, float zMin, float c, int side, bool hunter)
        {
            bool Place(string model, float size, float radius, float yaw, float minGap, out Vector2 at)
            {
                at = default;
                float bestD = -1f;
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector2(x0 + (i + 0.5f) * c, zMin + (j + 0.5f) * c);
                    if (!free(p.x, p.y, radius)) continue;
                    float d = float.MaxValue;
                    foreach (var t in taken) d = Mathf.Min(d, (t - p).magnitude);
                    if (d < minGap || d <= bestD) continue;
                    bestD = d;
                    at = p;
                }
                if (bestD < 0f) return false;
                put(model, at.x, at.y, yaw, size);
                return true;
            }
            if (hunter)
            {
                Place("chest", 0.95f, 0.22f, side > 0 ? -90f : 90f, 0.6f, out _);
                for (int k = 0; k < 2; k++)
                    if (Place("crate-bottles", 0.85f, 0.2f, 20f * k, 0.6f, out var at) && free(at.x, at.y + 0.3f, 0.12f))
                        put("bottle-large", at.x, at.y + 0.3f, 0f, 0.5f);
                Place("barrel", 1f, 0.2f, 0f, 0.6f, out _);
                return;
            }
            for (int k = 0; k < 3; k++)
            {
                if (!Place("cannon-ball", 0.32f, 0.12f, 0f, 0.5f, out var at)) break;
                const float o = 0.06f;
                put("cannon-ball", at.x + o * 2f, at.y, 0f, 0.32f);
                put("cannon-ball", at.x + o, at.y + o * 1.7f, 0f, 0.32f);
            }
            for (int k = 0; k < 2; k++)
                if (Place("barrel", 1f, 0.2f, 0f, 0.6f, out var at) && free(at.x, at.y + 0.36f, 0.14f))
                    put("barrel", at.x, at.y + 0.36f, 30f, 1f);
            Place("crate", 1f, 0.3f, 0f, 0.6f, out _);
        }

        static string TormentaLook(string name, int k) =>
            name.StartsWith("Chefe") ? KayKitCharacters.Warrior :
            name.StartsWith("Atirador") ? KayKitCharacters.Rogue :
            name.StartsWith("Bacamarteiro") ? KayKitCharacters.Mage :
            k % 2 == 0 ? KayKitCharacters.Warrior : KayKitCharacters.Minion;

        /// <summary>Tripulação do brigue (passo 4): o mestre brutamontes primeiro (a metralha tira os últimos).</summary>
        static readonly string[] BoardingBrigCrew =
        {
            "Brutamontes_MestreDoBrigue", "Pirata_Brigue_1", "Pirata_Brigue_2", "Atirador_Brigue_1", "Pirata_Brigue_3", "Bacamarteiro_Brigue",
            "Pirata_Brigue_4", "Atirador_Brigue_2", "Pirata_Brigue_5", "Pirata_Brigue_6",
        };

        /// <summary>Roupas da tripulação inimiga, na ordem dos nomes (Henry e Anne do kit, cores próprias: CivilianPalette 10–12 / 8–9).</summary>
        static readonly (string model, int palette)[] BoardingCrewLooks =
        {
            (KitCharacters.Henry, 10), (KitCharacters.Henry, 11), (KitCharacters.Anne, 8), (KitCharacters.Henry, 12),
            (KitCharacters.Henry, 11), (KitCharacters.Anne, 9), (KitCharacters.Henry, 10),
        };

        /// <summary>Tripulação da chalupa (passo 2): na ordem em que a metralha tira (os últimos saem primeiro).</summary>
        static readonly string[] BoardingSloopCrew =
        {
            "Pirata_Abordagem_1", "Pirata_Abordagem_2", "Atirador_Abordagem", "Bacamarteiro_Abordagem", "Pirata_Abordagem_3",
            "Pirata_Abordagem_4", "Pirata_Abordagem_5",
        };

        /// <summary>
        /// Piratas da IA de terra no convés inimigo (o mesmo CreatePirate dos acampamentos), espalhados no convés principal: o primeiro
        /// no ponto mais longe da abertura, cada um seguinte no mais longe dos já postos; longe dos objetos, das escadas e da abertura.
        /// Um EnemyCamp próprio (3 atacam por vez, sem fama: a recompensa é o passo 3), desligado até a abordagem.
        /// </summary>
        static EnemyCamp BoardingCrew(Transform deckRoot, float[,] h, int nx, int nz, float x0, float zMin, float c, float deck, int side,
            List<BoardingRamp> ramps, float gangZ, float gangHalf, List<Vector2> taken, FootCharacter player, string[] names, string kind)
        {
            const float S = BoardingScale, SY = BoardingHeightScale;
            bool Flat(float x, float z, float radius)
            {
                for (float dx = -radius; dx <= radius + 0.001f; dx += c)
                for (float dz = -radius; dz <= radius + 0.001f; dz += c)
                {
                    int i = Mathf.FloorToInt((x + dx - x0) / c), j = Mathf.FloorToInt((z + dz - zMin) / c);
                    if (i < 0 || j < 0 || i >= nx || j >= nz || float.IsNaN(h[i, j]) || Mathf.Abs(h[i, j] - deck) > 0.03f) return false;
                }
                return true;
            }
            var spots = new List<Vector2>();
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var p = new Vector2(x0 + (i + 0.5f) * c, zMin + (j + 0.5f) * c);
                if (!Flat(p.x, p.y, 0.25f)) continue;
                if (p.x * -side > -0.3f && Mathf.Abs(p.y - gangZ) < gangHalf + 0.5f) continue;
                bool clear = true;
                foreach (var r in ramps)
                    if (p.x > r.xMin - 0.4f && p.x < r.xMax + 0.4f && p.y > Mathf.Min(r.zStart, r.zEnd) - 0.5f && p.y < Mathf.Max(r.zStart, r.zEnd) + 0.5f)
                        clear = false;
                foreach (var t in taken) if ((t - p).sqrMagnitude < 0.45f * 0.45f) clear = false;
                if (clear) spots.Add(p);
            }
            var chosen = new List<Vector2>();
            var gang = new Vector2(-side * 1.5f, gangZ);
            for (int k = 0; k < names.Length && spots.Count > 0; k++)
            {
                Vector2 best = spots[0];
                float bestD = -1f;
                foreach (var p in spots)
                {
                    float d = chosen.Count == 0 ? (p - gang).sqrMagnitude : float.MaxValue;
                    foreach (var q in chosen) d = Mathf.Min(d, (p - q).sqrMagnitude);
                    if (d > bestD)
                    {
                        bestD = d;
                        best = p;
                    }
                }
                chosen.Add(best);
            }
            taken.AddRange(chosen);

            var site = new GameObject("Tripulacao").transform;
            site.SetParent(deckRoot, false);
            site.localPosition = new Vector3(0f, deck * SY, 0f);
            var camp = site.gameObject.AddComponent<EnemyCamp>();
            var materials = LoadLandMaterials();
            var cutlass = EnemyCutlass();
            var pistol = EnemyPistol();
            var members = new List<EnemyFootAI>();
            Physics.SyncTransforms();
            for (int k = 0; k < chosen.Count; k++)
            {
                string name = names[k];
                bool gunner = name.StartsWith("Atirador");
                var at = deckRoot.TransformPoint(new Vector3(chosen[k].x * S, deck * SY + 0.6f, chosen[k].y * S));
                // Virados para o navio do jogador.
                float yaw = deckRoot.eulerAngles.y + (side > 0 ? -90f : 90f);
                var (kit, palette) = BoardingCrewLooks[k % BoardingCrewLooks.Length];
                string look = BoardingLook(kind, name, k);
                if (look != null)
                {
                    // Modelos do KayKit (pedido do Jhonata, 04/11: variar os inimigos), com a roupa deles e o jeito de lutar pelo nome do posto:
                    // Brutamontes (o bárbaro do brigue), Chefe (o imediato do Tormenta, como o Barba-Negra), Duelista (o caçador-chefe),
                    // GuardaNegra (o oficial da Coroa, de sabre). Sem soltar armas épicas nem rações aqui (a recompensa é a rendição).
                    keepKitLook = true;
                    if (IsChief(name)) pendingChief = new ChiefSpec { name = "Imediato Ossonegro", title = "o braço direito de Lázaro Negro", health = 600f, scale = 1.3f };
                    var ai2 = CreatePirate(site, name, at, yaw, PostWeapon(name, gunner && !IsBrute(name), cutlass, pistol), player, camp,
                        materials.iron, materials.wood, materials.brass, look);
                    keepKitLook = false;
                    pendingChief = null;
                    foreach (var drop in ai2.GetComponents<Pirates.Loot.DropOnDeath>()) Object.DestroyImmediate(drop);
                    // Líder do caçador e da Coroa: mais vida e um pouco maior.
                    if (k == 0 && (kind == ShipBoarding.HunterKind || kind == ShipBoarding.CorvetteKind || kind == ShipBoarding.FrigateKind))
                    {
                        ai2.GetComponent<Pirates.Combat.Health>().Configure(kind == ShipBoarding.FrigateKind ? 260f : 200f);
                        ai2.transform.localScale = Vector3.one * 1.15f;
                    }
                    members.Add(ai2);
                    continue;
                }
                var ai = CreatePirate(site, name, at, yaw, PostWeapon(name, gunner, cutlass, pistol), player, camp,
                    materials.iron, materials.wood, materials.brass, kit);
                // Roupa da tripulação (como os marujos: só o corpo; a arma fica com o material dela).
                var outfit = CivilianMaterial(kit, palette);
                foreach (var r in ai.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int s = 0; s < slots.Length; s++) slots[s] = outfit;
                    r.sharedMaterials = slots;
                }
                members.Add(ai);
            }
            // 7 com 3 atacando por vez (04/11: com 5 e 2 por vez, os dois marujos limpavam o convés sozinhos em 16 s).
            string campName = kind switch
            {
                ShipBoarding.BrigKind => "Tripulação do brigue",
                ShipBoarding.TormentaKind => "Tripulação do Tormenta",
                ShipBoarding.HunterKind => "Bando do caçador",
                ShipBoarding.CorvetteKind or ShipBoarding.FrigateKind => "Fuzileiros da Coroa",
                _ => "Tripulação da chalupa",
            };
            camp.Configure(campName, "abordagem_" + kind, members.ToArray(),
                leash: 45f, attackersAtOnce: 3);
            camp.SetFame(0);
            site.gameObject.SetActive(false);
            return camp;
        }

        /// <summary>Colisor de caixa pela soma das malhas do objeto (no espaço dele).</summary>
        static void BoardingBoxCollider(GameObject go)
        {
            var root = go.transform;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any)
                    {
                        b = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else b.Encapsulate(p);
                }
            }
            if (!any) return;
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }
    }
}
