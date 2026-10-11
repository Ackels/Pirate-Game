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
        int shownVersion = -1;
        bool shownWaiting;
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

        void OnStep(TutorialStepEvent e)
        {
            if (!e.finished || banner == null) return;
            banner.Show("Tutorial do porto concluído!", "Bons ventos, capitão. Tecla H mostra os controles a qualquer hora.");
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
            bool waiting = tutorial.WaitingOnFoot;
            if (tutorial.Version != shownVersion || waiting != shownWaiting) Refresh(waiting);
            UpdatePointer();
        }

        void Refresh(bool waiting)
        {
            shownVersion = tutorial.Version;
            shownWaiting = waiting;
            var stage = tutorial.CurrentStage;
            Hud.Set(header, $"TUTORIAL  ·  {tutorial.Step + 1} de {tutorial.StageCount}");
            Hud.Set(title, stage.title);
            bool hasQuote = !string.IsNullOrWhiteSpace(stage.guide);
            Hud.Show(quote, hasQuote);
            if (hasQuote) Hud.Set(quote, $"<b>{tutorial.GuideName}:</b> “{stage.guide}”");
            string count = stage.count > 1 ? $"  <b>({tutorial.Progress}/{stage.count})</b>" : "";
            Hud.Set(objective, waiting
                ? "Primeiro volte à terra: no cais do porto, aperte <b>F</b> para descer do navio."
                : stage.objective + count);
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
