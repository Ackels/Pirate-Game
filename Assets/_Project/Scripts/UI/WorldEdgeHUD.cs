using Pirates.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>A borda do mundo no HUD (12/11): o aviso perto dela, a tela preta quando o navio volta e a faixa.</summary>
    public class WorldEdgeHUD : MonoBehaviour
    {
        [SerializeField] WorldEdge edge;
        [SerializeField] float bannerSeconds = 5f;

        GameHud hud;
        HudPrompt prompt;
        HudBanner banner;
        VisualElement blackout;
        float bannerUntil, shownFade = -1f;

        public void Bind(WorldEdge worldEdge) => edge = worldEdge;

        void OnEnable()
        {
            if (edge) edge.Lost += OnLost;
        }

        void OnDisable()
        {
            if (edge) edge.Lost -= OnLost;
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
            if (!edge) return;
            if (prompt == null && !Build()) return;
            if (bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
            float fade = edge.Fade;
            if (Mathf.Abs(fade - shownFade) >= 0.004f)
            {
                shownFade = fade;
                blackout.style.display = fade > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
                blackout.style.opacity = fade;
            }
            if (edge.Warning && !edge.IsLost) prompt.Show(null, "Névoa espessa à frente: o fim do Mar de Prata  <size=80%>(quem entra roda em círculos)</size>");
            else prompt.Hide();
        }

        void OnLost()
        {
            if (banner == null) return;
            banner.Show("O fim do Mar de Prata", "a névoa fez o navio rodar e voltar");
            bannerUntil = Time.unscaledTime + Hud.BannerTime(bannerSeconds);
        }
    }
}
