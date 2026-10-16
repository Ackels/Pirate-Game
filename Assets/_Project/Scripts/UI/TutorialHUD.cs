using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Missions;
using Pirates.Tutorial;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Tutorial na tela (11/10): cartão de pergaminho à esquerda, embaixo do painel do navio/capitão ("Tutorial 3/15", o título, a
    /// fala do Velho Bento e o que fazer; no alto e no meio ele cobria o caminho à frente do capitão), e um marcador sobre o lugar
    /// da etapa (porta da casa, o Bento, o campo de treino, o píer), com seta na borda quando fora da tela. Etapa cumprida = o
    /// cartão troca (e a vinheta toca, no Audio); a última = faixa no meio. Some com o menu, numa casa do porto aberta e na
    /// conversa com um NPC (cobriria a janela). Só remonta o texto quando o tutorial muda.
    /// </summary>
    public class TutorialHUD : MonoBehaviour
    {
        [SerializeField] TutorialGuide tutorial;
        [SerializeField] PlayerModeSwitcher switcher;

        GameHud hud;
        VisualElement card;
        Label header, title, quote, objective;
        WorldPointer pointer;
        HudBanner banner;
        int shownVersion = -1, shownWaiting;
        float bannerUntil;
        HarborBuildingVisitor buildings;
        NpcTalker talker;

        public void Bind(TutorialGuide guide, PlayerModeSwitcher modeSwitcher)
        {
            tutorial = guide;
            switcher = modeSwitcher;
        }

        void OnEnable() => EventBus.Subscribe<TutorialStepEvent>(OnStep);

        void OnDisable()
        {
            EventBus.Unsubscribe<TutorialStepEvent>(OnStep);
            if (card != null) Hud.Show(card, false);
            pointer?.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Card(paper: true);
            card.AddToClassList("hud-tutorial");
            header = Hud.Text("", "hud-tutorial__header");
            title = Hud.Text("", "hud-tutorial__title");
            quote = Hud.Text("", "hud-tutorial__quote");
            objective = Hud.Text("", "hud-tutorial__objective");
            card.Add(header);
            card.Add(title);
            card.Add(quote);
            card.Add(objective);
            card.style.display = DisplayStyle.None;
            hud.TopLeft.Add(card);
            pointer = new WorldPointer(hud, "hud-marker--tutorial");
            pointer.Hide();
            banner = new HudBanner();
            hud.Banners.Add(banner);
            if (switcher && switcher.Character)
            {
                buildings = switcher.Character.GetComponent<HarborBuildingVisitor>();
                talker = switcher.Character.GetComponent<NpcTalker>();
            }
            return true;
        }

        // Fim de cada parte: faixa no meio ("O porto: aprendido!" e o que vem agora, ou o fim do tutorial).
        void OnStep(TutorialStepEvent e)
        {
            if (!e.partFinished || banner == null) return;
            string next = e.finished || !tutorial ? null : tutorial.PartName(tutorial.PartOf(e.step + 1));
            banner.Show($"{e.part}: aprendido!", next == null
                ? "Fim do tutorial. Bons ventos, capitão!"
                : $"Agora: {next.ToLowerInvariant()}.");
            bannerUntil = Time.unscaledTime + 5f;
        }

        void Update()
        {
            if (!tutorial) return;
            if (card == null && !Build()) return;
            if (banner != null && bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                banner.Hide();
                bannerUntil = 0f;
            }

            bool busy = TitleScreen.Showing || (buildings && buildings.IsOpen) || (talker && talker.IsOpen);
            bool show = tutorial.Running && !busy;
            Hud.Show(card, show);
            if (!show)
            {
                pointer.Hide();
                return;
            }
            int waiting = tutorial.WaitingOnFoot ? 1 : tutorial.WaitingOnShip ? 2 : 0;
            if (tutorial.Version != shownVersion || waiting != shownWaiting) Refresh(waiting);
            UpdatePointer();
        }

        /// <param name="waiting">1 = a etapa é a pé e o capitão está no navio; 2 = é no mar e ele está a pé.</param>
        void Refresh(int waiting)
        {
            shownVersion = tutorial.Version;
            shownWaiting = waiting;
            var stage = tutorial.CurrentStage;
            var (index, count) = tutorial.PartProgress;
            Hud.Set(header, $"TUTORIAL  ·  {tutorial.PartName(stage.part).ToUpperInvariant()}  ·  {index} de {count}");
            Hud.Set(title, stage.title);
            bool hasQuote = !string.IsNullOrWhiteSpace(stage.guide);
            Hud.Show(quote, hasQuote);
            if (hasQuote) Hud.Set(quote, $"<b>{tutorial.GuideName}:</b> “{stage.guide}”");
            string progress = stage.count > 1 ? $"  <b>({tutorial.Progress}/{stage.count})</b>" : "";
            Hud.Set(objective, waiting switch
            {
                1 => tutorial.DisembarkHint,
                2 => "Primeiro volte ao navio: no píer, aperte <b>F</b> para embarcar.",
                _ => stage.objective + progress,
            });
        }

        void UpdatePointer()
        {
            var cam = Camera.main;
            if (!cam || hud.World.panel == null || !tutorial.TryGetTarget(out Vector3 target, out string label))
            {
                pointer.Hide();
                return;
            }
            Vector3 me = switcher && switcher.IsOnFoot ? switcher.Character.transform.position
                : switcher && switcher.Ship ? switcher.Ship.transform.position : target;
            Vector3 to = target - me;
            to.y = 0f;
            pointer.Place(hud, cam, target, label, Mathf.RoundToInt(to.magnitude));
        }
    }
}
