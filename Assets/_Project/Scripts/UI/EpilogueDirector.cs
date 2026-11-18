using Pirates.Controls;
using Pirates.Core;
using Pirates.Progression;
using Pirates.Save;
using Pirates.Story;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// O fim de "A Coroa de Prata" (pacote H, passo 4, 09/11): quando o último capítulo da História acaba (a Coroa de Prata no
    /// porão ou na mochila), espera a faixa do capítulo e a janela do baú fecharem, para o jogo e toca o epílogo (os quadros
    /// UI/Prologo/Fim_*, no mesmo estilo da abertura, com "Fim" no último). Depois volta ao jogo, que continua livre (contratos,
    /// tesouros, chefes), com a faixa "Fim" e o jogo salvo.
    /// </summary>
    public class EpilogueDirector : MonoBehaviour
    {
        [SerializeField] StoryLine story;
        [SerializeField] PrologueHUD prologue;
        [SerializeField] GameSave save;
        [Tooltip("Segundos (tempo real) entre o fim do capítulo e o epílogo: a faixa \"Capítulo concluído\" aparece antes.")]
        [SerializeField] float delay = 5f;
        [SerializeField] float bannerSeconds = 7f;

        float startAt = -1f, bannerUntil;
        float pausedScale = 1f;
        HudBanner banner;

        /// <summary>O epílogo está tocando (o jogo parado atrás).</summary>
        public bool Playing { get; private set; }

        /// <summary>O epílogo vai tocar assim que der (faixa, baú aberto).</summary>
        public bool Pending => startAt > 0f;

        public void Bind(StoryLine storyLine, PrologueHUD prologueHud, GameSave gameSave)
        {
            story = storyLine;
            prologue = prologueHud;
            save = gameSave;
        }

        void OnEnable()
        {
            EventBus.Subscribe<StoryChapterEvent>(OnChapterDone);
            Epilogue.Play = PlayNow;
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<StoryChapterEvent>(OnChapterDone);
            if (Epilogue.Play == PlayNow) Epilogue.Play = null;
        }

        void OnChapterDone(StoryChapterEvent e)
        {
            if (story && e.chapter == story.ChapterCount - 1) startAt = Time.unscaledTime + delay;
        }

        /// <summary>Sandbox e testes: toca o epílogo agora.</summary>
        public void PlayNow()
        {
            if (!Playing) Begin();
        }

        void Update()
        {
            if (banner != null && bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                banner.Hide();
                bannerUntil = 0f;
            }
            if (startAt < 0f || Playing || Time.unscaledTime < startAt) return;
            // Espera a janela do baú (ou outra) fechar e o menu de pausa sair.
            if (ScreenPanel.Open || TitleScreen.Showing || Time.timeScale == 0f || AnyWindowOpen()) return;
            Begin();
        }

        static bool AnyWindowOpen()
        {
            var hud = GameHud.Current;
            if (!hud || hud.Windows == null) return false;
            foreach (var child in hud.Windows.Children())
                if (child.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None && child.ClassListContains("hud-window")) return true;
            return false;
        }

        void Begin()
        {
            startAt = -1f;
            if (!prologue || !prologue.HasEpilogue) return;
            Playing = true;
            HitStop.Cancel();
            AudioListener.pause = true;   // o som do mundo (chuva, ambiente, tiros) não toca por baixo da narração
            pausedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (GameInput.Instance != null) GameInput.Instance.GameplayEnabled = false;
            string name = CaptainFame.Current ? CaptainFame.Current.CaptainName : "O capitão";
            prologue.PlayEpilogue(name, End);
        }

        void End()
        {
            Playing = false;
            TitleScreen.Showing = false;
            Time.timeScale = pausedScale;
            AudioListener.pause = false;
            if (GameInput.Instance != null) GameInput.Instance.GameplayEnabled = true;
            var hud = GameHud.Current;
            if (hud && hud.EnsureBuilt())
            {
                if (banner == null)
                {
                    banner = new HudBanner();
                    hud.Banners.Add(banner);
                }
                banner.Show("Fim de A Coroa de Prata", "o Mar de Prata continua seu: contratos, tesouros e chefes esperam");
                bannerUntil = Time.unscaledTime + Hud.BannerTime(bannerSeconds);
                hud.Feed(GameHud.FeedKind.Notice, "<b>A Coroa de Prata</b>\n<size=75%>equipe no encaixe Relíquia da tela I: mais ouro, fama e tesouro</size>",
                    HudColors.Gold);
            }
            if (save && save.Started) save.SaveNow();
        }
    }
}
