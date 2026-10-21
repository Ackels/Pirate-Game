using System.Collections.Generic;
using Pirates.Core;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.Progression;
using Pirates.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Aba Missões da tela do capitão (tecla J; 21/10, pedido do Jhonata: as missões e a História saem da tela e vão para cá, "e lá
    /// você vai ler toda a história da missão"). À esquerda a lista: a História (capítulo atual), os contratos ativos e o mapa do
    /// tesouro; à direita o escolhido por inteiro: a conversa de quem deu o contrato, o objetivo com o progresso, onde fica, o
    /// pagamento e "Desistir" (pede confirmação). Os marcadores no mundo continuam. Remonta só quando algo muda.
    /// </summary>
    class MissionJournal
    {
        enum Entry { None, Story, Mission, Map }

        readonly MissionBoard board;
        readonly HudWindow window;
        GameHud hud;
        bool open;
        Entry selectedKind = Entry.None;
        MissionDefinition selectedMission;
        MissionDefinition confirmAbandon;
        float confirmUntil;

        public bool IsOpen => open;

        public MissionJournal(GameHud gameHud, MissionBoard missionBoard)
        {
            hud = gameHud;
            board = missionBoard;
            window = new HudWindow(hud.Windows, "hud-window--journal");
            window.SetTitle("Diário do capitão", "J ou Esc fecha · clique num contrato para ler");
            CaptainBook.AddTabs(window, CaptainBook.Tab.Missions);
            var layout = Hud.Row("hud-journal");
            window.Body.Add(layout);
            var left = Hud.Box("hud-journal__list");
            layout.Add(left);
            var right = Hud.Box("hud-journal__page");
            layout.Add(right);
            window.AddPane(left, Signature, BuildList);
            window.AddPane(right, Signature, BuildPage);
            window.Footer.Add(Hud.Text("Contratos: no quadro da taverna e com os moradores que têm \"!\" na cabeça. A estrela no mundo aponta o caminho.",
                "hud-window__hint"));
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[J]</b>", () => SetOpen(false)));
            CaptainBook.Register(CaptainBook.Tab.Missions, () => open, SetOpen);
        }

        public void Unregister() => CaptainBook.Unregister(CaptainBook.Tab.Missions);

        public void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            ScreenPanel.Open = value;
            if (value)
            {
                CaptainBook.RefreshTabs(window);
                confirmAbandon = null;
                if (!SelectionValid()) SelectFirst();
            }
            window.Show(value);
            // Os avisos de ação ("[F] atracar") ficam por cima das janelas: somem com a tela aberta.
            if (hud.Prompts != null) Hud.Show(hud.Prompts, !value);
        }

        /// <summary>A cada quadro (de quem é dono): tecla J, Esc e remontar o que mudou.</summary>
        public void Tick()
        {
            var input = Pirates.Controls.GameInput.Instance;
            if (input.GameplayEnabled)
            {
                if (input.Journal.WasPressedThisFrame()) CaptainBook.Toggle(CaptainBook.Tab.Missions);
                else if (open && input.Cancel.WasPressedThisFrame()) SetOpen(false);
            }
            else if (open) SetOpen(false);   // menu inicial por cima
            if (!open) return;
            if (confirmAbandon && Time.unscaledTime > confirmUntil) confirmAbandon = null;
            if (!SelectionValid()) SelectFirst();
            window.RefreshAll();
            hud.LowerPickups();
        }

        // ---------- Seleção ----------

        static StoryLine Story => StoryLine.Current;
        static TreasureMaps Maps => TreasureMaps.Current;
        static bool HasStory => Story && !Story.Finished;
        static bool HasMap => Maps && (Maps.HasMap || Maps.Pieces > 0);

        bool SelectionValid() => selectedKind switch
        {
            Entry.Story => HasStory,
            Entry.Map => HasMap,
            Entry.Mission => selectedMission && board.IsActive(selectedMission),
            _ => false,
        };

        void SelectFirst()
        {
            selectedMission = null;
            if (board.Active.Count > 0)
            {
                selectedKind = Entry.Mission;
                selectedMission = board.Active[0].definition;
            }
            else selectedKind = HasStory ? Entry.Story : HasMap ? Entry.Map : Entry.None;
        }

        void Select(Entry kind, MissionDefinition mission = null)
        {
            selectedKind = kind;
            selectedMission = mission;
            confirmAbandon = null;
        }

        long Signature()
        {
            long h = Win.Mix((int)selectedKind, selectedMission);
            h = Win.Mix(h, confirmAbandon);
            for (int i = 0; i < board.Active.Count; i++)
            {
                var a = board.Active[i];
                h = Win.Mix(h, a.definition);
                h = Win.Mix(h, a.progress);
                h = Win.Mix(h, board.IsComplete(a) ? 1 : 0);
                // Entregas contam a carga (sem evento): o texto do progresso entra na assinatura.
                if (a.definition.type == MissionType.Deliver) h = Win.Mix(h, board.ProgressText(a).GetHashCode());
            }
            if (Story) h = Win.Mix(Win.Mix(h, Story.Version), StoryLine.Rank);
            if (CaptainFame.Current) h = Win.Mix(h, CaptainFame.Current.Fame);
            if (Maps) h = Win.Mix(h, Maps.Version);
            return h;
        }

        // ---------- Lista ----------

        void BuildList(VisualElement root)
        {
            var scroll = Win.Scroll(root);
            if (HasStory)
            {
                Win.Text(scroll, "História", "hud-window__subtitle-strong");
                var c = Story.CurrentChapter;
                ListEntry(scroll, Entry.Story, null, $"Capítulo {Story.Chapter + 1}: {c.title}",
                    c.comingSoon ? "em breve" : !Story.IsOpen ? "trancado: falta patente" : Progress(), false, "hud-journal__entry--story");
            }

            Win.Text(scroll, $"Contratos  <size=80%>({board.Active.Count} de {board.MaxActive})</size>", "hud-window__subtitle-strong");
            if (board.Active.Count == 0)
                Win.Text(scroll, "nenhum contrato aceito", "hud-window__empty");
            for (int i = 0; i < board.Active.Count; i++)
            {
                var a = board.Active[i];
                bool done = board.IsComplete(a);
                string status = !done ? board.ProgressText(a)
                    : a.definition.givenByNpc ? $"pronto: fale com {a.definition.giver}" : "pronto: entregue na taverna";
                ListEntry(scroll, Entry.Mission, a.definition, a.definition.title, status, done);
            }

            if (HasMap)
            {
                Win.Text(scroll, "Tesouro", "hud-window__subtitle-strong");
                var m = Maps;
                ListEntry(scroll, Entry.Map, null, "Mapa do tesouro",
                    m.HasMap ? $"X em {m.ActiveSpot.place}" : $"pedaços: {m.Pieces} de {m.PiecesPerMap}", false, "hud-journal__entry--map");
            }
        }

        string Progress()
        {
            var c = Story.CurrentChapter;
            int done = 0;
            for (int i = 0; i < c.objectives.Length; i++)
                if (Story.IsDone(i)) done++;
            return $"{done} de {c.objectives.Length} objetivos";
        }

        void ListEntry(VisualElement parent, Entry kind, MissionDefinition mission, string title, string status, bool done, string extraClass = null)
        {
            var row = Hud.Row("hud-journal__entry");
            if (extraClass != null) row.AddToClassList(extraClass);
            row.pickingMode = PickingMode.Position;
            bool selected = kind == selectedKind && (kind != Entry.Mission || mission == selectedMission);
            if (selected) row.AddToClassList("hud-journal__entry--on");
            var icon = Hud.Box("hud-mission__icon", "hud-journal__icon");
            icon.EnableInClassList("hud-mission__icon--done", done);
            row.Add(icon);
            var text = Hud.Box("hud-grow");
            text.Add(Hud.Text($"<b>{title}</b>", "hud-window__text", "hud-journal__entry-title"));
            text.Add(Hud.Text(done ? $"<color=#2f7a2a>{status}</color>" : status, "hud-window__small", "hud-window__text", "hud-window__muted"));
            row.Add(text);
            row.RegisterCallback<ClickEvent>(_ => Select(kind, mission));
            parent.Add(row);
        }

        // ---------- Página ----------

        void BuildPage(VisualElement root)
        {
            var scroll = Win.Scroll(root);
            switch (selectedKind)
            {
                case Entry.Story: StoryPage(scroll); break;
                case Entry.Mission: MissionPage(scroll); break;
                case Entry.Map: MapPage(scroll); break;
                default:
                    Win.Text(scroll, "<b>Nenhum contrato por enquanto.</b>", "hud-window__label");
                    Win.Text(scroll, "Passe na taverna do porto ou fale com os moradores que têm \"!\" na cabeça: cada um tem uma história e um serviço.",
                        "hud-window__small", "hud-window__muted");
                    break;
            }
        }

        void MissionPage(VisualElement page)
        {
            var a = board.ActiveFor(selectedMission);
            if (a == null) return;
            var m = a.definition;
            bool done = board.IsComplete(a);
            Win.Text(page, m.title, "hud-journal__title");
            Win.Text(page, m.givenByNpc ? $"Contrato de <b>{m.giver}</b>" : $"Contrato do quadro da taverna · {m.giver}", "hud-window__small", "hud-window__muted");

            // A conversa de quem deu (missões de NPC) ou o recado do quadro.
            var story = Win.Section(page, "hud-journal__story");
            if (m.givenByNpc && m.offerLines.Length > 0)
            {
                foreach (var line in m.offerLines)
                    if (!string.IsNullOrEmpty(line)) Win.Text(story, $"<i>“{line}”</i>", "hud-window__small", "hud-journal__line");
            }
            else if (!string.IsNullOrEmpty(m.description)) Win.Text(story, $"<i>“{m.description}”</i>", "hud-window__small", "hud-journal__line");
            else Hud.Show(story, false);

            Win.Text(page, "Objetivo", "hud-window__subtitle-strong");
            var goal = Win.Row(page, "hud-journal__goal");
            var icon = Hud.Box("hud-mission__icon", "hud-journal__icon");
            icon.EnableInClassList("hud-mission__icon--done", a.objectiveDone || done);
            goal.Add(icon);
            goal.Add(Hud.Text(board.ProgressText(a), "hud-window__text", "hud-grow"));

            var marker = !done && !string.IsNullOrEmpty(m.targetId) ? WorldMarker.Find(m.targetId) : null;
            string where = done ? (m.givenByNpc ? $"volte a <b>{m.giver}</b>, na vila do porto" : "volte à <b>taverna do porto</b> para receber")
                : marker ? $"<b>{marker.Label}</b> (a estrela no mapa aponta o caminho)"
                : !string.IsNullOrEmpty(m.targetName) ? $"<b>{m.targetName}</b>" : null;
            if (where != null) Win.Text(page, $"Onde: {where}", "hud-window__small", "hud-window__text");

            Win.Text(page, "Pagamento", "hud-window__subtitle-strong");
            Win.Text(page, $"{Win.Stacks(board.Rewards(m))}  ·  <b>+{m.fameReward}</b> de fama" + (done ? "  <color=#2f7a2a><b>(pronto para receber)</b></color>" : ""),
                "hud-window__small", "hud-window__text");
            if (m.repeatable) Win.Text(page, "<i>Repetível: depois de entregue, volta mais tarde.</i>", "hud-window__small", "hud-window__muted");

            var actions = Win.Row(page);
            Win.Spacer(actions);
            bool confirming = confirmAbandon == m;
            actions.Add(Win.Button(confirming ? "Confirmar: desistir?" : "Desistir do contrato", () =>
            {
                if (confirmAbandon == m)
                {
                    board.Abandon(a);
                    confirmAbandon = null;
                    var h = GameHud.Current;
                    if (h) h.Feed(GameHud.FeedKind.Notice, $"Contrato abandonado: <b>{m.title}</b>", HudColors.Muted);
                }
                else
                {
                    confirmAbandon = m;
                    confirmUntil = Time.unscaledTime + 3f;
                }
            }, true, "hud-button--small"));
        }

        void StoryPage(VisualElement page)
        {
            var s = Story;
            var c = s.CurrentChapter;
            Win.Text(page, $"HISTÓRIA  ·  CAPÍTULO {s.Chapter + 1} DE {s.ChapterCount}", "hud-journal__kicker");
            Win.Text(page, c.title, "hud-journal__title");
            if (!string.IsNullOrEmpty(c.intro))
            {
                var box = Win.Section(page, "hud-journal__story");
                Win.Text(box, $"<i>{c.intro}</i>", "hud-window__small", "hud-journal__line");
            }

            if (c.comingSoon) Win.Text(page, "<b>Em breve.</b> Este capítulo ainda está sendo escrito.", "hud-window__small", "hud-window__text");
            else if (!s.IsOpen && Services.TryGet(out ICaptainRank ranks))
            {
                var f = CaptainFame.Current;
                string fameText = f ? $" (fama {f.Fame} de {f.RankFame(c.requiredRank)})" : "";
                Win.Text(page, $"Para começar: chegue à patente <b>{ranks.RankTitle(c.requiredRank)}</b>{fameText}. Contratos, tesouros e saques dão fama.",
                    "hud-window__small", "hud-window__text");
            }

            // "Em breve" também mostra as conversas dele (o coveiro conta o que vem).
            if (s.IsOpen || c.comingSoon)
            {
                Win.Text(page, "Objetivos", "hud-window__subtitle-strong");
                for (int i = 0; i < c.objectives.Length; i++)
                {
                    var o = c.objectives[i];
                    bool done = s.IsDone(i);
                    var row = Win.Row(page, "hud-journal__goal");
                    var icon = Hud.Box("hud-mission__icon", "hud-journal__icon", "hud-story__icon");
                    icon.EnableInClassList("hud-mission__icon--done", done);
                    row.Add(icon);
                    string amount = o.count > 1 ? $"  <b>({Mathf.Min(s.Amount(i), o.count)}/{o.count})</b>" : "";
                    row.Add(Hud.Text(done ? $"<color=#2f7a2a>{o.text}</color>" : o.text + amount, "hud-window__text", "hud-grow"));
                }
            }

            // A história até aqui: os capítulos já vividos, com a fala do fim de cada um.
            if (s.Chapter > 0)
            {
                Win.Text(page, "Até aqui", "hud-window__subtitle-strong");
                for (int i = 0; i < s.Chapter; i++)
                {
                    var past = s.Spec(i);
                    if (past == null) continue;
                    Win.Text(page, $"<b>{i + 1}. {past.title}</b>", "hud-window__small", "hud-window__text");
                    if (!string.IsNullOrEmpty(past.outro)) Win.Text(page, $"<i>{past.outro}</i>", "hud-window__small", "hud-window__muted", "hud-journal__past");
                }
            }
        }

        void MapPage(VisualElement page)
        {
            var m = Maps;
            Win.Text(page, "Mapa do tesouro", "hud-journal__title");
            var box = Win.Section(page, "hud-journal__story");
            if (m.HasMap)
                Win.Text(box, $"O mapa está inteiro. Um X marca o lugar: <b>{m.ActiveSpot.place}</b>. Desembarque, ache o X (a estrela vermelha aponta) e aperte <b>F</b> para cavar.",
                    "hud-window__small", "hud-window__text");
            else
                Win.Text(box, $"Pedaços juntados: <b>{m.Pieces} de {m.PiecesPerMap}</b>. Com todos, o mapa se monta sozinho e marca um X numa das ilhas. " +
                              "Pedaços caem de baús, navios grandes e chefes.", "hud-window__small", "hud-window__text");
        }
    }
}
