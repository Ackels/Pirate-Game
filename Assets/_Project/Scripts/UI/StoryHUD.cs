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
    /// A História na tela (16/10): marcador com a joia dourada no próximo alvo (some se uma missão ativa já aponta para o mesmo lugar)
    /// e faixa no meio quando um capítulo acaba e quando o próximo abre. O capítulo, a fala e os objetivos ficam na aba Missões da tela
    /// do capitão (J, <see cref="MissionJournal"/>; 21/10, pedido do Jhonata: o cartão à direita era grande e cobria o resto). O marcador
    /// some com o menu, o tutorial rodando, uma casa do porto, a conversa e os painéis.
    /// </summary>
    public class StoryHUD : MonoBehaviour
    {
        [SerializeField] StoryLine story;
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] MissionBoard board;
        [SerializeField] float bannerSeconds = 5f;

        GameHud hud;
        WorldPointer pointer;
        HudBanner banner;
        readonly Queue<(string title, string subtitle, string feed)> pending = new();
        string feedAfterBanner;
        float bannerUntil;
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
            pointer?.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
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
            // A fala de abertura fica no diário (J): o aviso depois da faixa lembra onde ler.
            pending.Enqueue(($"Capítulo {chapter + 1}: {spec.title}", sub, $"<b>História: {spec.title}</b>\n<size=75%>J abre o diário com o capítulo inteiro</size>"));
        }

        static void Feed(string text)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, HudColors.Gold);
        }

        void Update()
        {
            if (!story) return;
            if (pointer == null && !Build()) return;
            UpdateBanner();

            bool busy = TitleScreen.Showing || ScreenPanel.Open || (buildings && buildings.IsOpen) || (talker && talker.IsOpen)
                        || (visitor && visitor.IsOpen) || (Services.TryGet(out ITutorial tutorial) && tutorial.Running);
            if (busy || story.Finished)
            {
                pointer.Hide();
                return;
            }
            UpdatePointer();
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
