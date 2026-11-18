using Pirates.LandCombat;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// O cerco do altar no HUD (pacote H, passo 2, 06/11): as faixas (o altar desperta, onda N de M, cerco quebrado, o altar adormece) e,
    /// durante o cerco, a linha de quantos mortos estão de pé ou quanto falta para a próxima onda.
    /// </summary>
    public class AltarSiegeHUD : MonoBehaviour
    {
        [SerializeField] AltarSiege siege;
        [SerializeField] float bannerSeconds = 4f;

        GameHud hud;
        HudPrompt prompt;
        HudBanner banner;
        float bannerUntil;
        CachedText line;

        public void Bind(AltarSiege altar) => siege = altar;

        void OnEnable()
        {
            if (!siege) return;
            siege.Started += OnStarted;
            siege.WaveRose += OnWave;
            siege.Failed += OnFailed;
            siege.Won += OnWon;
            LazarusCurse.Appeared += OnLazarus;
            LazarusCurse.Called += OnCalled;
        }

        void OnDisable()
        {
            if (siege)
            {
                siege.Started -= OnStarted;
                siege.WaveRose -= OnWave;
                siege.Failed -= OnFailed;
                siege.Won -= OnWon;
            }
            LazarusCurse.Appeared -= OnLazarus;
            LazarusCurse.Called -= OnCalled;
            if (prompt == null) return;
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
            return true;
        }

        void Update()
        {
            if (!siege) return;
            if (prompt == null && !Build()) return;
            if (bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
            if (!siege.IsActive)
            {
                prompt.Hide();
                return;
            }
            int wave = siege.WaveIndex + 1, total = siege.WaveCount;
            float next = siege.NextWaveIn;
            int left = Mathf.CeilToInt(next), standing = siege.Standing;
            if (line.Stale(wave, left, standing))
                line.Text = next > 0f
                    ? $"Cerco do altar  ·  a onda {wave} de {total} sobe em {left} s"
                    : $"Cerco do altar  ·  onda {wave} de {total}  ·  mortos de pé: {standing}";
            prompt.Show(null, line.Text);
        }

        void OnStarted() => Banner("O altar desperta", "os mortos da ilha sobem da terra (não se afaste do altar)");

        void OnWave(int index, int total) =>
            Banner($"Onda {index + 1} de {total}", index == total - 1 ? "as feras da maldição" : "pólvora e a Lâmina de São Telmo os mantêm no chão");

        void OnFailed() => Banner("O altar adormece", "os mortos voltaram para a terra");

        void OnWon() => Banner("Cerco quebrado!", "o altar se abre...");

        void OnLazarus(LazarusCurse l) => Banner("Lázaro Negro", "o capitão amaldiçoado da prata ergue-se do altar");

        void OnCalled(LazarusCurse l) => Banner("A névoa chama os mortos", "Lázaro chama a tripulação que a maldição lhe deu");

        void Banner(string title, string subtitle)
        {
            if (banner == null) return;
            banner.Show(title, subtitle);
            bannerUntil = Time.unscaledTime + Hud.BannerTime(bannerSeconds);
        }
    }
}
