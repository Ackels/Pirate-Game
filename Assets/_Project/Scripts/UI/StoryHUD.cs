using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Missions;
using Pirates.Progression;
using Pirates.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// A História na tela (16/10): cartão de pergaminho à direita, embaixo da fama e do relógio e acima das missões ("HISTÓRIA ·
    /// CAPÍTULO 2 DE 6", o título, uma linha do que está acontecendo e os objetivos com ✓; capítulo trancado = a patente que falta e a
    /// fama), marcador com a joia dourada no próximo alvo (some se uma missão ativa já aponta para o mesmo lugar) e faixa no meio quando
    /// um capítulo acaba e quando o próximo abre. Some com o menu, o tutorial rodando, uma casa do porto, a conversa e os painéis.
    /// Só remonta o texto quando a História muda.
    /// </summary>
    public class StoryHUD : MonoBehaviour
    {
        [SerializeField] StoryLine story;
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] MissionBoard board;
        [SerializeField] float bannerSeconds = 5f;

        GameHud hud;
        VisualElement card;
        Label header, title, intro, locked;
        readonly List<(VisualElement row, VisualElement icon, Label text)> rows = new();
        WorldPointer pointer;
        HudBanner banner;
        readonly Queue<(string title, string subtitle, string feed)> pending = new();
        string feedAfterBanner;
        float bannerUntil;
        int shownVersion = -1, shownRank = -1, shownFame = -1;
        float nextPlace;
        HarborBuildingVisitor buildings;
        HarborVisitor visitor;
        NpcTalker talker;

        public void Bind(StoryLine storyLine, PlayerModeSwitcher modeSwitcher, MissionBoard missionBoard)
        {
            story = storyLine;
            switcher = modeSwitcher;
            board = missionBoard;
        }

        void OnEnable()
        {
            EventBus.Subscribe<StoryChapterEvent>(OnChapterDone);
            if (story) story.Opened += OnChapterOpened;
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<StoryChapterEvent>(OnChapterDone);
            if (story) story.Opened -= OnChapterOpened;
            if (card != null) Hud.Show(card, false);
            pointer?.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Card(paper: true);
            card.AddToClassList("hud-story");
            header = Hud.Text("", "hud-story__header");
            title = Hud.Text("", "hud-story__title");
            intro = Hud.Text("", "hud-story__intro");
            locked = Hud.Text("", "hud-story__locked");
            card.Add(header);
            card.Add(title);
            card.Add(intro);
            card.Add(locked);
            card.style.display = DisplayStyle.None;
            hud.TopRight.Add(card);
            pointer = new WorldPointer(hud, "hud-marker--story");
            pointer.Hide();
            banner = new HudBanner();
            hud.Banners.Add(banner);
            if (switcher && switcher.Character)
            {
                buildings = switcher.Character.GetComponent<HarborBuildingVisitor>();
                talker = switcher.Character.GetComponent<NpcTalker>();
            }
            if (switcher && switcher.Ship) visitor = switcher.Ship.GetComponent<HarborVisitor>();
            return true;
        }

        // A faixa só tem uma linha curta embaixo do título (a fala inteira vazava): a fala do fim do capítulo vai para os avisos.
        void OnChapterDone(StoryChapterEvent e)
        {
            var spec = story ? story.Spec(e.chapter) : null;
            var next = story ? story.Spec(e.chapter + 1) : null;
            string sub = next == null ? null : next.comingSoon ? $"Próximo: {next.title} (em breve)" : $"Próximo: {next.title}";
            // A fala sai nos avisos depois da faixa (na mesma altura, ficava escondida atrás dela).
            string feed = spec != null && !string.IsNullOrEmpty(spec.outro) ? $"<b>{e.title}</b>\n<size=75%><i>{spec.outro}</i></size>" : null;
            pending.Enqueue(($"Capítulo {e.chapter + 1} concluído: {e.title}", sub, feed));
        }

        void OnChapterOpened(int chapter)
        {
            var spec = story.Spec(chapter);
            if (spec == null) return;
            string sub = spec.comingSoon ? "Em breve" : spec.objectives.Length > 0 ? spec.objectives[0].text : null;
            // A fala de abertura já está no cartão.
            pending.Enqueue(($"Capítulo {chapter + 1}: {spec.title}", sub, null));
        }

        static void Feed(string text)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, HudColors.Gold);
        }

        void Update()
        {
            if (!story) return;
            if (card == null && !Build()) return;
            UpdateBanner();

            bool busy = TitleScreen.Showing || ScreenPanel.Open || (buildings && buildings.IsOpen) || (talker && talker.IsOpen)
                        || (visitor && visitor.IsOpen) || (Services.TryGet(out ITutorial tutorial) && tutorial.Running);
            bool show = !busy && !story.Finished;
            Hud.Show(card, show);
            if (!show)
            {
                pointer.Hide();
                return;
            }
            if (Time.unscaledTime >= nextPlace) Place();
            int rank = StoryLine.Rank, fame = CaptainFame.Current ? CaptainFame.Current.Fame : 0;
            bool open = story.IsOpen;
            if (story.Version != shownVersion || rank != shownRank || (!open && fame != shownFame)) Refresh(rank, fame);
            UpdatePointer();
        }

        /// <summary>Embaixo da plaquinha da fama e do relógio, acima do porão e das missões (eles montam em qualquer ordem).</summary>
        void Place()
        {
            nextPlace = Time.unscaledTime + 1f;
            var parent = hud.TopRight;
            int want = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent[i].ClassListContains("hud-fame-chip") || parent[i].ClassListContains("hud-clock")) want = i + 1;
            int at = parent.IndexOf(card);
            if (at < want) want--;
            if (at != want) parent.Insert(want, card);
        }

        void Refresh(int rank, int fame)
        {
            shownVersion = story.Version;
            shownRank = rank;
            shownFame = fame;
            var c = story.CurrentChapter;
            Hud.Set(header, $"HISTÓRIA  ·  CAPÍTULO {story.Chapter + 1} DE {story.ChapterCount}");
            Hud.Set(title, c.title);
            Hud.Set(intro, c.intro);
            bool open = story.IsOpen;

            string lockText = null;
            if (c.comingSoon) lockText = "<b>Em breve.</b> Este capítulo ainda está sendo escrito.";
            else if (!open && Services.TryGet(out ICaptainRank ranks))
            {
                var f = CaptainFame.Current;
                string fameText = f ? $" (fama {f.Fame} de {f.RankFame(c.requiredRank)})" : "";
                lockText = $"Para começar: chegue à patente <b>{ranks.RankTitle(c.requiredRank)}</b>{fameText}. " +
                           "Contratos, tesouros e saques dão fama.";
            }
            Hud.Show(locked, lockText != null);
            if (lockText != null) Hud.Set(locked, lockText);

            // "Em breve" também mostra as conversas dele (o coveiro conta o que vem).
            int count = open ? c.objectives.Length : 0;
            for (int i = 0; i < count; i++)
            {
                if (i == rows.Count)
                {
                    var row = Hud.Row("hud-mission");
                    var icon = Hud.Box("hud-mission__icon", "hud-story__icon");
                    var text = Hud.Text("", "hud-mission__text");
                    row.Add(icon);
                    row.Add(text);
                    card.Add(row);
                    rows.Add((row, icon, text));
                }
                var o = c.objectives[i];
                bool done = story.IsDone(i);
                var r = rows[i];
                Hud.Show(r.row, true);
                r.icon.EnableInClassList("hud-mission__icon--done", done);
                string amount = o.count > 1 ? $"  <b>({Mathf.Min(story.Amount(i), o.count)}/{o.count})</b>" : "";
                Hud.Set(r.text, done ? $"<color=#2f7a2a>{o.text}</color>" : o.text + amount);
            }
            for (int i = count; i < rows.Count; i++) Hud.Show(rows[i].row, false);
        }

        void UpdateBanner()
        {
            if (banner == null) return;
            if (bannerUntil > 0f && Time.unscaledTime < bannerUntil) return;
            if (feedAfterBanner != null)
            {
                Feed(feedAfterBanner);
                feedAfterBanner = null;
            }
            if (pending.Count > 0)
            {
                var (t, s, f) = pending.Dequeue();
                banner.Show(t, s);
                feedAfterBanner = f;
                bannerUntil = Time.unscaledTime + bannerSeconds;
            }
            else if (bannerUntil > 0f)
            {
                banner.Hide();
                bannerUntil = 0f;
            }
        }

        void UpdatePointer()
        {
            var cam = Camera.main;
            string id = story.NextTargetId;
            var marker = string.IsNullOrEmpty(id) ? null : WorldMarker.Find(id);
            if (!cam || !marker || hud.World.panel == null || MissionPointsAt(id))
            {
                pointer.Hide();
                return;
            }
            Vector3 me = switcher && switcher.IsOnFoot ? switcher.Character.transform.position
                : switcher && switcher.Ship ? switcher.Ship.transform.position : marker.transform.position;
            Vector3 to = marker.transform.position - me;
            to.y = 0f;
            pointer.Place(hud, cam, marker.LabelPosition, marker.Label, Mathf.RoundToInt(to.magnitude));
        }

        /// <summary>Uma missão ativa (ainda por cumprir) já põe o marcador dela no mesmo alvo.</summary>
        bool MissionPointsAt(string id)
        {
            if (!board) return false;
            var active = board.Active;
            for (int i = 0; i < active.Count; i++)
                if (active[i].definition.targetId == id && !board.IsComplete(active[i])) return true;
            return false;
        }
    }
}
