using Pirates.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// A névoa da Ilha Amaldiçoada no HUD (pacote H, passo 1, 06/11): o aviso com a névoa fechada, a tela preta quando o navio se perde e
    /// as faixas (perdido na névoa; a névoa abre caminho; chegada na ilha).
    /// </summary>
    public class CursedFogHUD : MonoBehaviour
    {
        [SerializeField] CursedFog fog;
        [SerializeField] float bannerSeconds = 5f;

        GameHud hud;
        HudPrompt prompt;
        HudBanner banner;
        VisualElement blackout;
        float bannerUntil, shownFade = -1f;
        bool openedShown, reachedShown;

        public void Bind(CursedFog cursedFog) => fog = cursedFog;

        void OnEnable()
        {
            if (!fog) return;
            fog.Lost += OnLost;
            fog.Reached += OnReached;
        }

        void OnDisable()
        {
            if (fog)
            {
                fog.Lost -= OnLost;
                fog.Reached -= OnReached;
            }
            if (prompt == null) return;
            blackout.style.display = DisplayStyle.None;
            shownFade = 0f;
            prompt.Hide();
            banner.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            banner = new HudBanner();
            hud.Banners.Add(banner);
            blackout = new VisualElement { pickingMode = PickingMode.Ignore };
            blackout.AddToClassList("hud-blackout");
            blackout.style.display = DisplayStyle.None;
            hud.Menu.Add(blackout);
            return true;
        }

        void Update()
        {
            if (!fog) return;
            if (prompt == null && !Build()) return;
            if (bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }

            float fade = fog.Fade;
            if (Mathf.Abs(fade - shownFade) >= 0.004f)
            {
                shownFade = fade;
                blackout.style.display = fade > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
                blackout.style.opacity = fade;
            }

            if (fog.Warning && !fog.IsLost) prompt.Show(null, "Uma névoa fria fecha o mar à frente  <size=80%>(sem a rota das Águas Negras, ninguém acha a ilha)</size>");
            else prompt.Hide();

            // Rota aberta: a primeira vez que o navio chega perto na sessão, a faixa.
            if (!openedShown && fog.IsOpen && fog.DistanceToShip < 300f)
            {
                openedShown = true;
                Banner("A névoa abre caminho", "a Ilha Amaldiçoada surge à frente");
            }
        }

        void OnLost() => Banner("Perdido na névoa", "o navio rodou em círculos e saiu de onde entrou");

        void OnReached()
        {
            if (reachedShown) return;
            reachedShown = true;
            Banner("Ilha Amaldiçoada", "Lázaro espera em algum lugar no alto da ilha");
        }

        void Banner(string title, string subtitle)
        {
            if (banner == null) return;
            banner.Show(title, subtitle);
            bannerUntil = Time.unscaledTime + Hud.BannerTime(bannerSeconds);
        }
    }
}
