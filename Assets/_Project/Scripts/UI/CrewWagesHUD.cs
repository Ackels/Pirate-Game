using Pirates.Base;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>Avisos do salário da tripulação (12/11): dia de pagamento, atraso (uma vez) e deserção.</summary>
    public class CrewWagesHUD : MonoBehaviour
    {
        static readonly Color Warn = new(1f, 0.7f, 0.35f);
        CrewWages wages;
        bool lateShown;

        void Update()
        {
            if (!wages)
            {
                wages = CrewWages.Current;
                if (!wages) return;
                wages.PaydayCame += OnPayday;
                wages.Deserted += OnDeserted;
            }
            if (wages.Late && !lateShown)
            {
                lateShown = true;
                Feed("Salário atrasado: a moral da tripulação está caindo (pague na taverna)", Warn);
            }
            else if (!wages.Late) lateShown = false;
        }

        void OnDestroy()
        {
            if (!wages) return;
            wages.PaydayCame -= OnPayday;
            wages.Deserted -= OnDeserted;
        }

        void OnPayday(int due) => Feed($"Dia de pagamento: {due} de ouro para a tripulação (pague na taverna)", HudColors.Gold);
        void OnDeserted() => Feed("Um marujo desertou: o salário está atrasado", new Color(0.95f, 0.4f, 0.3f));

        static void Feed(string text, Color color)
        {
            var hud = GameHud.Current;
            if (hud) hud.Feed(GameHud.FeedKind.Notice, text, color);
        }
    }
}
