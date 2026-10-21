using Pirates.Audio;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Progression;
using Pirates.Save;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Sandbox
{
    /// <summary>
    /// Modo sandbox (F1 abre o painel; F2 teleporta para o porto a qualquer momento): painel de debug para testar progressão rápido — dar itens, mexer no casco,
    /// afundar ou trazer inimigos, ir ao porto, zerar upgrades e acelerar o tempo. Em terra (cena 06): vida do personagem,
    /// invencível, levantar/derrotar o acampamento, piratas distraídos, ir direto para o acampamento, esvaziar a mochila
    /// e repor o saque da ilha (baús e caixotes). No mundo (cena 07): cumprir os objetivos das missões ativas, zerar as missões
    /// e o jogo salvo (salvar agora, carregar, começar de novo).
    /// Só existe no editor e em builds de desenvolvimento.
    /// </summary>
    public class SandboxPanel : MonoBehaviour
    {
        [SerializeField] ShipController player;
        [Tooltip("Itens que o painel oferece (o gerador preenche com tudo de Data/Items).")]
        [SerializeField] ItemDefinition[] items = System.Array.Empty<ItemDefinition>();
        [Tooltip("Quantidade de cada recurso no botão \"Encher tudo\".")]
        [SerializeField] int fillAmount = 2000;
        [SerializeField] int fillCrew = 5;

        static readonly float[] TimeScales = { 1f, 2f, 4f };
        static readonly string[] TimeLabels = { "1×", "2×", "4×" };
        static readonly string[] Targets = { "Porão", "Armazém" };

        bool open;
        int target;
        Rect window = new(16f, 16f, 520f, 640f);
        Vector2 scroll;
        Inventory hold;
        Health hull;
        ShipUpgrades upgrades;
        ShipCrew crew;
        PlayerShipClass shipClass;
        float originalCapacity;
        GUIStyle small;
        PlayerModeSwitcher modes;
        Health walkerHealth;
        FootHealthReactions walkerReactions;
        Inventory backpack;
        GameSave save;
        BountyHunters hunters;

        public void Configure(ShipController playerShip, ItemDefinition[] sandboxItems)
        {
            player = playerShip;
            items = sandboxItems;
        }

        void Awake()
        {
            if (!Debug.isDebugBuild) Destroy(this);
        }

        void Start()
        {
            if (!player) return;
            player.TryGetComponent(out hold);
            player.TryGetComponent(out hull);
            player.TryGetComponent(out upgrades);
            player.TryGetComponent(out crew);
            player.TryGetComponent(out shipClass);
            if (hold) originalCapacity = hold.Capacity;
            modes = FindAnyObjectByType<PlayerModeSwitcher>();
            save = FindAnyObjectByType<GameSave>();
            hunters = FindAnyObjectByType<BountyHunters>();
            if (modes && modes.Character)
            {
                walkerHealth = modes.Character.GetComponent<Health>();
                walkerReactions = modes.Character.GetComponent<FootHealthReactions>();
                backpack = modes.Character.GetComponent<Inventory>();
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) open = !open;
            if (keyboard.f2Key.wasPressedThisFrame && Dock) GoToHarbor();
            // Fechado, só há um texto no canto: sem a passada de Layout do IMGUI (que aloca a cada quadro).
            useGUILayout = open;
        }

        void OnDisable() => Time.timeScale = 1f;

        void OnGUI()
        {
            if (!player) return;
            if (!open || !useGUILayout)
            {
                GUI.Label(new Rect(Screen.width - 330f, Screen.height - 26f, 320f, 22f), Dock ? "F1  modo sandbox   ·   F2  ir para o porto" : "F1  modo sandbox");
                return;
            }
            // Texto quebra na largura da janela: uma linha comprida (ex.: "Jogo salvo") esticava o conteúdo e cortava os botões da direita.
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            window.width = Mathf.Min(520f, Screen.width - 32f);
            window.height = Mathf.Min(640f, Screen.height - 32f);
            window = GUILayout.Window(GetHashCode(), window, DrawWindow, "Modo sandbox (F1 fecha)");
        }

        HarborDock Dock => HarborDock.Active.Count > 0 ? HarborDock.Active[0] : null;

        Inventory Destination => target == 1 && Dock ? Dock.Warehouse : hold;

        void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
            DrawItems();
            GUILayout.Space(8f);
            DrawShip();
            GUILayout.Space(8f);
            DrawOnFoot();
            DrawEnemies();
            DrawMissions();
            DrawFame();
            DrawDayNight();
            DrawTutorial();
            DrawStory();
            DrawTreasureMaps();
            DrawCollections();
            DrawRuins();
            DrawWind();
            DrawWeather();
            DrawSave();
            DrawSound();
            DrawTime();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        // ---------- Itens ----------

        void DrawItems()
        {
            GUILayout.Label("<b>Itens</b>", small);
            if (!hold)
            {
                GUILayout.Label("<i>o navio não tem porão nesta cena</i>", small);
                return;
            }

            if (Dock) target = GUILayout.Toolbar(target, Targets);
            else target = 0;

            bool unlimited = GUILayout.Toggle(hold.Unlimited, " Porão sem limite de espaço");
            if (unlimited != hold.Unlimited) hold.Configure(originalCapacity, unlimited);
            GUILayout.Label($"<size=11>Porão: {hold.UsedSpace:0.#} / {(hold.Unlimited ? "∞" : hold.Capacity.ToString("0"))}</size>", small);

            var dest = Destination;
            foreach (var item in items)
            {
                if (!item) continue;
                // Tripulação só existe a bordo (não fica no armazém).
                var to = item.category == ItemCategory.Crew ? hold : dest;
                GUILayout.BeginHorizontal();
                string color = ColorUtility.ToHtmlStringRGB(item.color);
                GUILayout.Label($"<color=#{color}>{item.displayName}</color>  <size=11>({to.Count(item)})</size>", small, GUILayout.Width(200f));
                if (item.IsUnique)
                {
                    if (to.Has(item)) GUILayout.Label("<i>já tem</i>", small);
                    else if (GUILayout.Button("Ganhar", GUILayout.Width(80f))) to.Add(item, 1);
                }
                else
                {
                    if (GUILayout.Button("+1", GUILayout.Width(44f))) to.Add(item, 1);
                    if (GUILayout.Button("+10", GUILayout.Width(48f))) to.Add(item, 10);
                    if (GUILayout.Button("+100", GUILayout.Width(54f))) to.Add(item, 100);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Encher tudo")) FillEverything();
            if (GUILayout.Button("Esvaziar porão")) EmptyHold();
            GUILayout.EndHorizontal();
            GUILayout.Label($"<size=11>Encher tudo: +{fillAmount} de cada recurso no destino, +{fillCrew} sobreviventes a bordo e todos os esquemas.</size>", small);
        }

        void FillEverything()
        {
            var dest = Destination;
            if (dest == hold && !hold.Unlimited) hold.Configure(originalCapacity, true);
            foreach (var item in items)
            {
                if (!item) continue;
                if (item.category == ItemCategory.Crew) hold.Add(item, fillCrew);
                else if (item.IsUnique) dest.Add(item, 1);
                else dest.Add(item, fillAmount);
            }
        }

        void EmptyHold() => EmptyInventory(hold);

        // ---------- Navio ----------

        void DrawShip()
        {
            GUILayout.Label("<b>Navio</b>", small);
            if (hull)
            {
                GUILayout.Label($"Casco: {hull.Current:0} / {hull.Max:0}{(hull.IsDead ? "  <color=#ff7a66>afundando (F5 recomeça)</color>" : "")}", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Levar 50 de dano")) Damage(50f);
                if (GUILayout.Button("Consertar tudo")) hull.Heal(hull.Max);
                GUILayout.EndHorizontal();
                hull.Invulnerable = GUILayout.Toggle(hull.Invulnerable, " Casco invencível");
            }
            if (crew && hold && crew.CrewItem)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Tripulação: {crew.Count} / {crew.Berths}", small, GUILayout.Width(150f));
                if (GUILayout.Button("-1", GUILayout.Width(44f))) hold.Remove(crew.CrewItem, 1);
                if (GUILayout.Button("+1", GUILayout.Width(44f))) hold.Add(crew.CrewItem, 1);
                if (GUILayout.Button("Encher")) hold.Add(crew.CrewItem, crew.FreeBerths);
                if (GUILayout.Button("Zerar")) hold.Remove(crew.CrewItem, crew.Count);
                GUILayout.EndHorizontal();
            }
            if (shipClass && shipClass.Classes.Count > 1)
            {
                // Troca na hora, sem esquema nem obra (como no estaleiro: casco cheio e upgrades no zero).
                GUILayout.BeginHorizontal();
                GUILayout.Label("Navio:", small, GUILayout.Width(60f));
                foreach (var c in shipClass.Classes)
                {
                    if (!c) continue;
                    GUI.enabled = c != shipClass.Current;
                    if (GUILayout.Button(c.displayName)) shipClass.SetClass(c, newShip: true);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            var buildings = Dock ? Dock.Buildings : null;
            bool busy = Dock && (Dock.IsBusy || (buildings && buildings.IsBusy));
            GUILayout.BeginHorizontal();
            GUI.enabled = busy;
            if (GUILayout.Button(busy ? "Concluir obras agora (navio e base)" : "Concluir obras agora (nenhuma)"))
            {
                Dock.FinishNow();
                if (buildings) buildings.FinishNow();
            }
            GUI.enabled = buildings;
            if (GUILayout.Button("Derrubar construções")) buildings.ResetAll();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = Dock;
            if (GUILayout.Button("Teleportar para o porto (F2)")) GoToHarbor();
            GUI.enabled = upgrades;
            if (GUILayout.Button("Zerar upgrades")) upgrades.ResetLevels();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void Damage(float amount)
        {
            bool wasInvulnerable = hull.Invulnerable;
            hull.Invulnerable = false;
            hull.TakeDamage(new Pirates.Core.DamageInfo { amount = amount, point = player.transform.position, source = gameObject });
            hull.Invulnerable = wasInvulnerable;
        }

        /// <summary>Teleporta para a vaga do cais, parado e com as velas recolhidas: é só apertar F.</summary>
        void GoToHarbor()
        {
            var berth = Dock.Berth;
            player.Teleport(new Vector3(berth.position.x, player.transform.position.y, berth.position.z), berth.rotation);
        }

        // ---------- A pé ----------

        void DrawOnFoot()
        {
            if (!walkerHealth) return;
            bool onFoot = modes.IsOnFoot;
            GUILayout.Label("<b>A pé</b>", small);
            GUILayout.Label($"Vida: {walkerHealth.Current:0} / {walkerHealth.Max:0}{(walkerHealth.IsDead ? "  <color=#ff7a66>derrotado</color>" : "")}", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Levar 30 de dano")) HurtWalker(30f);
            if (GUILayout.Button("Vida cheia"))
            {
                if (walkerHealth.IsDead && walkerReactions) walkerReactions.Revive();
                else walkerHealth.Heal(walkerHealth.Max);
            }
            GUILayout.EndHorizontal();
            walkerHealth.Invulnerable = GUILayout.Toggle(walkerHealth.Invulnerable, " Invencível a pé");
            EnemyFootAI.Distracted = GUILayout.Toggle(EnemyFootAI.Distracted, " Piratas distraídos (não veem você)");

            foreach (var camp in EnemyCamp.Active)
            {
                GUILayout.Label($"<size=11>{camp.DisplayName}: {camp.Alive} / {camp.Total} piratas vivos</size>", small);
                GUILayout.BeginHorizontal();
                var graveyard = camp.GetComponent<CursedGraveyard>();
                if (graveyard)
                {
                    // Esqueletos amaldiçoados: só levantam de noite com você perto; aqui, na hora.
                    if (GUILayout.Button(graveyard.IsRisen ? "Voltar às covas" : "Sair das covas"))
                    {
                        if (graveyard.IsRisen) graveyard.BuryNow();
                        else graveyard.RiseNow();
                    }
                }
                else if (GUILayout.Button("Levantar piratas")) camp.ResetAll();
                if (GUILayout.Button("Derrotar todos")) DefeatCamp(camp);
                GUI.enabled = onFoot && !walkerHealth.IsDead;
                if (GUILayout.Button("Ir até lá")) GoToCamp(camp);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                // Chefe a pé (pacote B, passo 4): vida e fase; enfurecer pula para a fase 2 (só lutando: fora da luta ele volta ao normal).
                for (int i = 0; i < camp.Members.Count; i++)
                {
                    var boss = camp.Members[i] ? camp.Members[i].GetComponent<LandBoss>() : null;
                    if (!boss) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<size=11>{boss.DisplayName}: {boss.Health.Current:0} / {boss.Health.Max:0}, fase {boss.Phase}</size>", small);
                    GUI.enabled = boss.IsEngaged && boss.Phase == 1;
                    if (GUILayout.Button("Enfurecer (fase 2)")) boss.EnrageNow();
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            if (backpack)
            {
                GUILayout.Label($"<size=11>Mochila: {backpack.UsedSpace:0.#} / {backpack.Capacity:0} de espaço</size>", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Esvaziar mochila")) EmptyInventory(backpack);
                if (GUILayout.Button("Repor saque da ilha")) RestoreLandLoot();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8f);
        }

        static void EmptyInventory(Inventory inventory)
        {
            foreach (var item in new System.Collections.Generic.List<ItemDefinition>(inventory.Items))
                inventory.Remove(item, inventory.Count(item));
        }

        /// <summary>Baús fechados de novo (sorteiam outra vez), caixotes e barris inteiros, sacos no chão somem.</summary>
        static void RestoreLandLoot()
        {
            foreach (var chest in LandChest.Active) chest.ResetChest();
            foreach (var crate in BreakableLoot.Active) if (crate.IsBroken) crate.Restore();
            foreach (var keg in PowderKeg.Active) if (keg.IsSpent) keg.Restore();
            foreach (var sack in new System.Collections.Generic.List<LootSack>(LootSack.Active)) Destroy(sack.gameObject);
        }

        void HurtWalker(float amount)
        {
            bool wasInvulnerable = walkerHealth.Invulnerable;
            walkerHealth.Invulnerable = false;
            walkerHealth.TakeDamage(new Pirates.Core.DamageInfo { amount = amount, point = walkerHealth.transform.position, source = gameObject });
            walkerHealth.Invulnerable = wasInvulnerable;
        }

        static void DefeatCamp(EnemyCamp camp)
        {
            foreach (var ai in camp.Members)
                if (ai && !ai.IsDead) ai.Health.TakeDamage(new Pirates.Core.DamageInfo { amount = ai.Health.Max * 10f, point = ai.transform.position });
        }

        /// <summary>Põe o personagem a ~20 m ao norte do acampamento ou posto, virado para ele (os guardas já te veem).</summary>
        void GoToCamp(EnemyCamp camp)
        {
            Vector3 entrance = camp.transform.forward;
            Vector3 pos = camp.Center + entrance * 20f;
            modes.Character.Teleport(pos, Quaternion.LookRotation(-entrance));
        }

        // ---------- Missões ----------

        void DrawMissions()
        {
            if (!Pirates.Core.Services.TryGet(out MissionBoard board)) return;
            GUILayout.Label("<b>Missões</b>", small);
            GUILayout.Label($"<size=11>{board.Active.Count} ativa(s). Cumprir: afundar/acampamento/tesouro contam como feitos; entregas ganham a carga no porão.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cumprir objetivos ativos")) board.CompleteAll();
            if (GUILayout.Button("Zerar missões")) board.ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        // ---------- Fama do capitão (M10) ----------

        void DrawFame()
        {
            var fame = CaptainFame.Current;
            if (!fame) return;
            GUILayout.Label("<b>Fama do capitão</b>", small);
            GUILayout.Label($"<size=11>{fame.CaptainName}: {fame.Fame} de fama, {fame.RankTitle(fame.Rank)}; {fame.Perks.Count} bônus, " +
                            $"{fame.PendingPerks} para escolher (tecla C).</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+50 fama")) fame.Add(50, "sandbox");
            if (GUILayout.Button("+200 fama")) fame.Add(200, "sandbox");
            if (GUILayout.Button("Zerar fama")) fame.ResetAll(fame.CaptainName);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Dia e noite (M11): pular para uma hora e acelerar o relógio.</summary>
        void DrawDayNight()
        {
            if (!Services.TryGet(out ITimeOfDay time)) return;
            GUILayout.Label("<b>Dia e noite</b>", small);
            int minutes = Mathf.FloorToInt(time.Hour * 60f);
            GUILayout.Label($"<size=11>Dia {time.Day}, {minutes / 60:00}:{minutes % 60:00} (noite {time.Night:0.00}); relógio ×{time.Speed:0}.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Amanhecer")) time.SetTime(time.Day, 5.6f);
            if (GUILayout.Button("Meio-dia")) time.SetTime(time.Day, 12f);
            if (GUILayout.Button("Pôr do sol")) time.SetTime(time.Day, 18.6f);
            if (GUILayout.Button("Noite")) time.SetTime(time.Day, 23f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (float speed in new[] { 1f, 10f, 60f })
                if (GUILayout.Button($"×{speed:0}")) time.Speed = speed;
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Mapas do tesouro (C1, 16/10): pedaços e o X marcado; dar um pedaço, montar o mapa na hora, desfazer.</summary>
        void DrawTreasureMaps()
        {
            var maps = Pirates.Loot.TreasureMaps.Current;
            if (!maps) return;
            GUILayout.Label("<b>Mapas do tesouro</b>", small);
            string state = maps.HasMap ? $"X marcado: {maps.ActiveSpot.place}." : $"Pedaços: {maps.Pieces} de {maps.PiecesPerMap}.";
            GUILayout.Label($"<size=11>{state}</size>", small);
            GUILayout.BeginHorizontal();
            var hold = modes && modes.Ship ? modes.Ship.GetComponent<Inventory>() : null;
            if (hold && GUILayout.Button("+1 pedaço")) maps.GivePiece(hold);
            if (!maps.HasMap && hold && GUILayout.Button("Montar agora"))
            {
                for (int i = maps.Pieces; i < maps.PiecesPerMap; i++) maps.GivePiece(hold);
                maps.Assemble();
            }
            if (maps.HasMap && GUILayout.Button("Desfazer")) maps.Cancel();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Ruína das alavancas (C4, 19/10): quantas alavancas; abrir o portão na hora ou recomeçar.</summary>
        void DrawRuins()
        {
            var puzzles = FindObjectsByType<Pirates.LandCombat.RuinPuzzle>(FindObjectsInactive.Exclude);
            if (puzzles.Length == 0) return;
            GUILayout.Label("<b>Ruínas</b>", small);
            foreach (var p in puzzles)
            {
                GUILayout.Label($"<size=11>{p.DisplayName}: {(p.IsOpen ? "aberta" : $"{p.Pulled} de {p.LeverCount} alavancas")}.</size>", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ir até lá")) GoToRuin(p);
                if (!p.IsOpen && GUILayout.Button("Abrir")) p.Open();
                if (GUILayout.Button("Recomeçar")) p.ResetAll();
                GUILayout.EndHorizontal();
            }
            // Passagens (C5: alçapão da Ermida e escada das catacumbas): a pé, leva direto para o outro lado.
            if (modes && modes.IsOnFoot)
            {
                GUILayout.BeginHorizontal();
                foreach (var passage in Pirates.LandCombat.RuinPassage.Active)
                {
                    if (!passage.Destination || !GUILayout.Button(passage.ActionText)) continue;
                    modes.Character.Teleport(passage.Destination.position, passage.Destination.rotation);
                    if (Services.TryGet(out ICameraFollow follow)) follow.SnapToTarget();
                }
                GUILayout.EndHorizontal();
                // Monstros das catacumbas (C5, passo 2): todos vivos de novo, no lugar.
                var encounters = FindObjectsByType<Pirates.LandCombat.CryptEncounter>(FindObjectsInactive.Exclude);
                if (encounters.Length > 0 && GUILayout.Button("Monstros das catacumbas de volta"))
                    foreach (var e in encounters) e.ResetAll();
                // Capelão Fantasma (C5, passo 3): fúria na hora (lutando com ele).
                var chaplains = Pirates.LandCombat.ChaplainGhost.Bosses;
                if (chaplains.Count > 0 && chaplains[0].IsEngaged && !chaplains[0].IsEnraged && GUILayout.Button("Enfurecer o Capelão"))
                    chaplains[0].EnrageNow();
            }
            GUILayout.Space(8f);
        }

        /// <summary>
        /// A pé: o capitão na entrada da ruína, virado para dentro (sem entrada marcada: na praça do portão, virado para ele) (como o "Ir até lá" dos acampamentos). No navio: parado a ~25 m da praia de
        /// desembarque mais perto da ruína, de proa para ela (é só apertar F).
        /// </summary>
        void GoToRuin(Pirates.LandCombat.RuinPuzzle ruin)
        {
            if (modes && modes.IsOnFoot)
            {
                if (ruin.Entrance)
                {
                    var e = ruin.Entrance;
                    modes.Character.Teleport(e.position + Vector3.up * 0.2f, Quaternion.LookRotation(e.forward));
                    return;
                }
                var gate = ruin.Gate ? ruin.Gate.position : ruin.transform.position;
                Vector3 outward = ruin.transform.right;
                modes.Character.Teleport(gate + outward * 4.5f + Vector3.up * 0.2f, Quaternion.LookRotation(-outward));
                return;
            }
            Pirates.OnFoot.LandingPoint best = null;
            float bestDistance = float.MaxValue;
            foreach (var l in FindObjectsByType<Pirates.OnFoot.LandingPoint>(FindObjectsInactive.Exclude))
            {
                float d = Vector3.Distance(l.transform.position, ruin.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = l;
                }
            }
            if (!best || !player) return;
            Vector3 seaward = best.transform.position - ruin.transform.position;
            seaward.y = 0f;
            seaward.Normalize();
            var pos = best.transform.position + seaward * 25f;
            player.Teleport(new Vector3(pos.x, player.transform.position.y, pos.z), Quaternion.LookRotation(-seaward));
        }

        /// <summary>Coletáveis (C3, 19/10): quantos de cada coleção; pegar o próximo de uma (conta e paga como no jogo), zerar.</summary>
        void DrawCollections()
        {
            var all = Pirates.Loot.Collections.Current;
            if (!all) return;
            GUILayout.Label("<b>Coleções</b>", small);
            GUILayout.BeginHorizontal();
            foreach (var spec in all.All)
            {
                int have = all.Count(spec.kind), total = all.Total(spec.kind);
                if (have < total && GUILayout.Button($"+1 {spec.name} ({have}/{total})"))
                    foreach (var c in all.Items)
                        if (c && c.Kind == spec.kind && !all.IsFound(c))
                        {
                            all.Collect(c);
                            break;
                        }
            }
            if (GUILayout.Button("Zerar")) all.Restore(null);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>História (16/10): capítulo atual; pular, voltar ou recomeçar (a patente de cada um: botões de fama acima).</summary>
        void DrawStory()
        {
            if (!Services.TryGet(out IStory story)) return;
            GUILayout.Label("<b>História</b>", small);
            string now = story.Chapter < story.ChapterCount ? $"Capítulo {story.Chapter + 1}: {story.ChapterTitle(story.Chapter)}." : "Acabou.";
            GUILayout.Label($"<size=11>{now}</size>", small);
            GUILayout.BeginHorizontal();
            if (story.Chapter > 0 && GUILayout.Button("Voltar")) story.Restore(story.Chapter - 1, null);
            if (story.Chapter < story.ChapterCount && GUILayout.Button("Pular capítulo")) story.Restore(story.Chapter + 1, null);
            if (GUILayout.Button("Recomeçar")) story.Begin();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Tutorial (11/10): etapa atual; pular a etapa, recomeçar ou encerrar.</summary>
        void DrawTutorial()
        {
            if (!Services.TryGet(out ITutorial tutorial)) return;
            GUILayout.Label("<b>Tutorial</b>", small);
            GUILayout.Label(tutorial.Running ? $"<size=11>Etapa {tutorial.Step + 1}.</size>" : "<size=11>Desligado.</size>", small);
            GUILayout.BeginHorizontal();
            if (tutorial.Running && GUILayout.Button("Próxima etapa")) tutorial.Restore(tutorial.Step + 1);
            if (GUILayout.Button("Recomeçar")) tutorial.Begin(true);
            if (tutorial.Running && GUILayout.Button("Encerrar")) tutorial.Restore(-1);
            GUILayout.EndHorizontal();
            // Começar direto numa parte (2 = o mar).
            GUILayout.BeginHorizontal();
            for (int part = 2; part <= tutorial.Parts; part++)
                if (GUILayout.Button($"Ir para a parte {part}")) tutorial.Restore(tutorial.FirstStepOf(part));
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        static readonly string[] CompassNames = { "norte", "nordeste", "leste", "sudeste", "sul", "sudoeste", "oeste", "noroeste" };

        /// <summary>Vento (M11): mostra de onde vem e a força; fixa a direção (de onde vem) e a força, ou volta ao vento que muda sozinho.</summary>
        void DrawWind()
        {
            if (!Services.TryGet(out IWind wind)) return;
            GUILayout.Label("<b>Vento</b>", small);
            // Vento se diz pelo lado de onde vem: soprando para o sul = vento norte.
            int from = Mathf.RoundToInt(Mathf.Repeat(wind.Degrees + 180f, 360f) / 45f) % 8;
            GUILayout.Label($"<size=11>Vem do {CompassNames[from]}, força {wind.Strength:0.00}{(wind.Overridden ? " (fixado aqui)" : " (muda sozinho)")}.</size>", small);
            GUILayout.BeginHorizontal();
            // Botão = de onde vem; o vento sopra para o lado oposto.
            if (GUILayout.Button("Do norte")) wind.SetOverride(180f, FixedStrength(wind));
            if (GUILayout.Button("Do leste")) wind.SetOverride(270f, FixedStrength(wind));
            if (GUILayout.Button("Do sul")) wind.SetOverride(0f, FixedStrength(wind));
            if (GUILayout.Button("Do oeste")) wind.SetOverride(90f, FixedStrength(wind));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fraco")) wind.SetOverride(wind.Degrees, 0.15f);
            if (GUILayout.Button("Forte")) wind.SetOverride(wind.Degrees, 1f);
            if (GUILayout.Button("Muda sozinho")) wind.SetOverride(null, null);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        static float? FixedStrength(IWind wind) => wind.Overridden ? wind.Strength : null;

        /// <summary>Clima (M11): mostra o tempo; fixa céu limpo, nublado, chuva ou tempestade (troca em ~20 s), raio agora, ou volta a mudar sozinho.</summary>
        void DrawWeather()
        {
            if (!Services.TryGet(out IWeather weather)) return;
            GUILayout.Label("<b>Clima</b>", small);
            GUILayout.Label($"<size=11>{weather.Label}: céu fechado {weather.Overcast:0.00}, chuva {weather.Rain:0.00}, tempestade {weather.Storm:0.00}" +
                            $"{(weather.Overridden ? " (fixado aqui)" : " (muda sozinho)")}.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Limpo")) weather.SetOverride(WeatherKind.Clear);
            if (GUILayout.Button("Nublado")) weather.SetOverride(WeatherKind.Cloudy);
            if (GUILayout.Button("Chuva")) weather.SetOverride(WeatherKind.Rain);
            if (GUILayout.Button("Tempestade")) weather.SetOverride(WeatherKind.Storm);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Raio agora")) weather.StrikeNow();
            if (GUILayout.Button("Muda sozinho")) weather.SetOverride(null);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        // ---------- Jogo salvo ----------

        void DrawSave()
        {
            if (!save) return;
            GUILayout.Label("<b>Jogo salvo</b>", small);
            string last = save.LastSaved.HasValue ? $"último às {save.LastSaved.Value:HH:mm:ss}" : save.HasSave ? "há um save (deste jogo ainda não)" : "nenhum save";
            GUILayout.Label($"<size=11>Salva sozinho ao atracar e ao sair do porto ({last}). Carregar recarrega a cena e põe o navio no porto. " +
                            $"Arquivo: {save.FilePath}</size>", small);
            GUILayout.BeginHorizontal();
            GUI.enabled = save.Started;
            if (GUILayout.Button("Salvar agora")) save.SaveNow();
            GUI.enabled = save.HasSave;
            if (GUILayout.Button("Carregar o save")) save.ReloadScene(GameSave.StartMode.Continue);
            GUI.enabled = true;
            if (GUILayout.Button("Novo jogo (apaga o save)")) save.ReloadScene(GameSave.StartMode.NewGame);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        // ---------- Inimigos ----------

        void DrawEnemies()
        {
            int alive = 0;
            bool brig = false;
            var ships = FindObjectsByType<EnemyShipAI>();
            if (ships.Length == 0) return;
            foreach (var ai in ships)
            {
                if (!ai.TryGetComponent(out Health h) || h.IsDead) continue;
                alive++;
                brig |= !string.IsNullOrEmpty(ai.TargetId);
            }
            GUILayout.Label("<b>Inimigos</b>", small);
            GUILayout.Label($"<size=11>{alive} inimigo(s) vivo(s). Afundada, a chalupa volta em ~25 s; o brigue (cena 07), em 150 s " +
                            "ou ao aceitar a missão dele.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Afundar inimigos agora")) SinkEnemies();
            if (GUILayout.Button("Trazer chalupa para perto")) BringEnemy(brig: false);
            GUI.enabled = brig;
            if (GUILayout.Button(brig ? "Trazer brigue" : "Brigue afundado")) BringEnemy(brig: true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            // Fortes inimigos (Ilha Norte, cena 07): calar na hora, restaurar, levar o navio para a frente dele.
            for (int i = 0; i < EnemyFort.Active.Count; i++)
            {
                var fort = EnemyFort.Active[i];
                GUILayout.Label($"<size=11>{fort.DisplayName}: {(fort.IsSilenced ? "<color=#7dffa0>calado</color>" : $"muralhas {fort.Health.Current:0} / {fort.Health.Max:0}{(fort.Engaging ? "  <color=#ff7a66>atirando</color>" : "")}")}</size>", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Calar o forte")) fort.Silence();
                if (GUILayout.Button("Restaurar")) fort.Restore();
                GUI.enabled = modes == null || !modes.IsOnFoot;
                if (GUILayout.Button("Navio na frente dele")) ShipInFrontOf(fort.transform);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            // Lula gigante (cena 07): muda o covil para onde o navio está e ataca agora; vai para o covil; enfurece; mata.
            for (int i = 0; i < GiantSquid.Active.Count; i++)
            {
                var squid = GiantSquid.Active[i];
                GUILayout.Label($"<size=11>{squid.DisplayName}: {squid.Health.Current:0} / {squid.Health.Max:0}, {SquidState(squid)}" +
                                $"{(squid.IsEngaged ? $", {squid.Grabbing} agarrando" : "")}</size>", small);
                GUILayout.BeginHorizontal();
                GUI.enabled = !squid.IsEngaged && !squid.Health.IsDead && (modes == null || !modes.IsOnFoot);
                if (GUILayout.Button("Atacar aqui")) squid.SummonNear();
                GUI.enabled = modes == null || !modes.IsOnFoot;
                if (GUILayout.Button("Ir ao covil")) ShipNear(squid.Lair, 75f);
                GUI.enabled = squid.IsEngaged && !squid.PhaseTwo;
                if (GUILayout.Button("Enfurecer")) squid.EnrageNow();
                GUI.enabled = squid.IsEngaged;
                if (GUILayout.Button("Matar")) squid.Health.TakeDamage(new Pirates.Core.DamageInfo { amount = squid.Health.Max * 10f, point = squid.transform.position });
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            // Megalodonte (cena 07): sobe aqui; vai para a Fossa; enfurece; mata.
            for (int i = 0; i < Megalodon.Active.Count; i++)
            {
                var shark = Megalodon.Active[i];
                GUILayout.Label($"<size=11>{shark.DisplayName}: {shark.Health.Current:0} / {shark.Health.Max:0}, {SharkState(shark)}</size>", small);
                GUILayout.BeginHorizontal();
                GUI.enabled = !shark.IsEngaged && !shark.Health.IsDead && (modes == null || !modes.IsOnFoot);
                if (GUILayout.Button("Atacar aqui")) shark.SummonNear();
                GUI.enabled = modes == null || !modes.IsOnFoot;
                if (GUILayout.Button("Ir à Fossa")) ShipNear(shark.Lair, 90f);
                GUI.enabled = shark.IsEngaged && !shark.PhaseTwo;
                if (GUILayout.Button("Enfurecer")) shark.EnrageNow();
                GUI.enabled = shark.IsEngaged;
                if (GUILayout.Button("Matar"))
                {
                    shark.Health.Invulnerable = false;
                    shark.Health.TakeDamage(new Pirates.Core.DamageInfo { amount = shark.Health.Max * 10f, point = shark.transform.position });
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            // Caçador de recompensas (cena 07): chama um agora, na força da patente atual (abaixo de Corsário, o mais fraco).
            if (hunters)
            {
                GUI.enabled = !hunters.HasHunter;
                if (GUILayout.Button(hunters.HasHunter ? "Caçador de recompensas no mar" : "Chamar caçador de recompensas")) hunters.SpawnNow();
                GUI.enabled = true;
            }
            GUILayout.Space(8f);
        }

        static string SharkState(Megalodon shark) => shark.Current switch
        {
            Megalodon.Phase.Lurking => "no fundo da Fossa",
            Megalodon.Phase.Rising => "subindo",
            Megalodon.Phase.Circle => shark.PhaseTwo ? "circulando (furioso)" : "circulando",
            Megalodon.Phase.WindUp => "preparando a investida",
            Megalodon.Phase.Charge => "investindo",
            Megalodon.Phase.Dive => "mergulhando",
            Megalodon.Phase.Breach => "saltando",
            Megalodon.Phase.Retreat => "voltando à Fossa",
            _ => "morto",
        };

        static string SquidState(GiantSquid squid) => squid.Current switch
        {
            GiantSquid.Phase.Lurking => "esperando no covil",
            GiantSquid.Phase.Ambush => "emboscando",
            GiantSquid.Phase.Fight => squid.PhaseTwo ? "lutando (furiosa)" : "lutando",
            GiantSquid.Phase.Retreat => "voltando ao covil",
            _ => "morta",
        };

        /// <summary>Navio parado a <paramref name="distance"/> m ao sul do ponto, virado para leste.</summary>
        void ShipNear(Vector3 point, float distance)
        {
            var pos = point + new Vector3(0f, 0f, -distance);
            player.Teleport(new Vector3(pos.x, player.transform.position.y, pos.z), Quaternion.Euler(0f, 90f, 0f));
        }

        /// <summary>Navio parado a ~110 m ao sul do forte, de lado para ele (ao alcance dos canhões).</summary>
        void ShipInFrontOf(Transform fort)
        {
            var pos = fort.position + new Vector3(0f, 0f, -110f);
            player.Teleport(new Vector3(pos.x, player.transform.position.y, pos.z), Quaternion.Euler(0f, 90f, 0f));
        }

        static void SinkEnemies()
        {
            foreach (var ai in FindObjectsByType<EnemyShipAI>())
                if (ai.TryGetComponent(out Health h))
                    h.TakeDamage(new Pirates.Core.DamageInfo { amount = h.Max * 10f, point = ai.transform.position });
        }

        /// <summary>Põe o inimigo vivo (chalupa ou o brigue, que tem id de missão) a ~90 m na frente do jogador, virado para ele.</summary>
        void BringEnemy(bool brig)
        {
            foreach (var ai in FindObjectsByType<EnemyShipAI>())
            {
                if (!ai.TryGetComponent(out Health h) || h.IsDead) continue;
                if (string.IsNullOrEmpty(ai.TargetId) == brig) continue;
                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                Vector3 pos = player.transform.position + forward.normalized * 90f;
                pos.y = ai.transform.position.y;
                var rotation = Quaternion.LookRotation(-forward.normalized);
                var body = ai.GetComponent<Rigidbody>();
                body.linearVelocity = Vector3.zero;
                body.position = pos;
                body.rotation = rotation;
                ai.transform.SetPositionAndRotation(pos, rotation);
                return;
            }
        }

        // ---------- Som ----------

        MusicDirector music;

        void DrawSound()
        {
            if (!music) music = FindAnyObjectByType<MusicDirector>();
            GUILayout.Label($"<b>Som</b>{(music ? $"  <size=11>(música: {MusicName(music.Current)})</size>" : "")}", small);
            SoundMix.Master = VolumeSlider("Geral", SoundMix.Master);
            SoundMix.Music = VolumeSlider("Música", SoundMix.Music);
            SoundMix.Effects = VolumeSlider("Efeitos", SoundMix.Effects);
            SoundMix.Ambient = VolumeSlider("Ambiente", SoundMix.Ambient);
            SoundMix.Interface = VolumeSlider("Interface", SoundMix.Interface);
            GUILayout.Space(8f);
        }

        static string MusicName(MusicDirector.Mood mood) => mood switch
        {
            MusicDirector.Mood.Port => "porto", MusicDirector.Mood.Island => "ilha", MusicDirector.Mood.Combat => "combate", _ => "mar",
        };

        float VolumeSlider(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<size=11>{label}</size>", small, GUILayout.Width(70f));
            value = GUILayout.HorizontalSlider(value, 0f, 1f);
            GUILayout.Label($"<size=11>{value * 100f:0}%</size>", small, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return value;
        }

        // ---------- Tempo ----------

        void DrawTime()
        {
            GUILayout.Label("<b>Velocidade do jogo</b>", small);
            int current = Mathf.Max(0, System.Array.IndexOf(TimeScales, Time.timeScale));
            int chosen = GUILayout.Toolbar(current, TimeLabels);
            if (chosen != current) Time.timeScale = TimeScales[chosen];
        }
    }
}
