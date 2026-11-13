using Pirates.Combat;
using Pirates.Controls;
using Pirates.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pirates.UI
{
    /// <summary>
    /// Navio do jogador afundado (12/11, no lugar do "F5 para recomeçar"): depois de o navio sumir na água, uma janela com
    /// "Carregar o último save" e "Voltar ao porto" (guarda o progresso, perde a carga e metade da tripulação). Cena sem jogo salvo:
    /// "Recomeçar". No objeto do GameHUD.
    /// </summary>
    public class ShipSunkHUD : MonoBehaviour
    {
        [SerializeField, Tooltip("Segundos entre o navio afundar e a janela aparecer.")] float delay = 3f;

        GameHud hud;
        HudWindow window;
        Health hull;
        GameSave save;
        float deadSince = -1f;
        float nextSearch;
        bool panelOwned;

        public bool IsShowing => window != null && window.IsVisible;

        /// <summary>Alguma janela de navio afundado aberta (a faixa do ShipDebugHUD sai da frente).</summary>
        public static bool Showing { get; private set; }

        void Update()
        {
            if (!hull)
            {
                if (Time.unscaledTime < nextSearch) return;
                nextSearch = Time.unscaledTime + 1f;
                var input = FindAnyObjectByType<PlayerShipInput>(FindObjectsInactive.Include);
                if (input) hull = input.GetComponent<Health>();
                if (!save) save = FindAnyObjectByType<GameSave>();
                return;
            }
            if (!hull.IsDead) { deadSince = -1f; Show(false); return; }
            if (deadSince < 0f) deadSince = Time.unscaledTime;
            if (Time.unscaledTime - deadSince >= delay) Show(true);
        }

        void Show(bool show)
        {
            if (show && window == null)
            {
                hud = GameHud.Current;
                if (!hud) return;
                Build();
            }
            if (window == null) return;
            window.Show(show);
            Showing = show;
            if (show && !panelOwned) { panelOwned = true; Core.ScreenPanel.Open = true; }
            else if (!show && panelOwned) { panelOwned = false; Core.ScreenPanel.Open = false; }
        }

        void OnDisable() => Show(false);

        void Build()
        {
            window = new HudWindow(hud.Windows, "hud-window--surrender");
            window.SetTitle("Seu navio afundou!", "o mar levou o casco e a carga");
            window.AddPane(window.Body, () => (save && save.HasSave ? 1 : 0) + (save && save.CanReturnToPort ? 2 : 0), body =>
            {
                bool hasSave = save && save.HasSave;
                bool port = save && save.CanReturnToPort;
                if (!hasSave && !port)
                {
                    Win.Text(body, "Sem jogo salvo nesta cena.");
                    body.Add(Win.Button("Recomeçar", Restart));
                    return;
                }
                if (port)
                {
                    var back = Win.Section(body, "hud-surrender__option");
                    Win.Text(back, "<b>Voltar ao porto</b>", "hud-surrender__title");
                    Win.Text(back, "Você fica com tudo o que conquistou (fama, missões, armazém), mas a carga do porão vai para o fundo e só metade "
                        + "da tripulação se salva. O navio volta à vaga do porto com metade do casco.");
                    back.Add(Win.Button("Voltar ao porto", ReturnToPort));
                }
                var load = Win.Section(body, "hud-surrender__option");
                Win.Text(load, "<b>Carregar o último save</b>", "hud-surrender__title");
                Win.Text(load, hasSave ? "Volta ao último jogo salvo, do jeito que estava (perde o que fez depois dele)."
                    : "Ainda não há jogo salvo.");
                load.Add(Win.Button("Carregar", LoadLast, hasSave));
            });
        }

        void ReturnToPort()
        {
            if (save) save.ReturnToPortAfterSinking();
        }

        void LoadLast()
        {
            if (save && save.HasSave) save.ReloadScene(GameSave.StartMode.Continue);
        }

        static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
