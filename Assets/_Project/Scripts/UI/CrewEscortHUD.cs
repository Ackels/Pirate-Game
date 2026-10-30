using Pirates.Controls;
using Pirates.LandCombat;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>Marujos em terra no HUD (D3, 30/10): avisos quando descem com o capitão, quando um cai e quando levanta.</summary>
    public class CrewEscortHUD : MonoBehaviour
    {
        static readonly Color CrewColor = new(0.55f, 0.8f, 1f);

        CrewEscort escort;

        void Start()
        {
            escort = FindAnyObjectByType<CrewEscort>(FindObjectsInactive.Include);
            if (escort) escort.Landed += OnLanded;
        }

        void OnDestroy()
        {
            if (escort) escort.Landed -= OnLanded;
        }

        void OnEnable()
        {
            CrewmateAI.Downed += OnDowned;
            CrewmateAI.Revived += OnRevived;
        }

        void OnDisable()
        {
            CrewmateAI.Downed -= OnDowned;
            CrewmateAI.Revived -= OnRevived;
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        static void OnLanded(int n) =>
            Feed(n == 1 ? "<b>Um marujo desceu com você</b>\n<size=75%>ele segue o capitão e briga com os piratas</size>"
                        : $"<b>{n} marujos desceram com você</b>\n<size=75%>eles seguem o capitão e brigam com os piratas</size>", CrewColor);

        static void OnDowned(CrewmateAI mate) => Feed("<b>Marujo caído!</b>\n<size=75%>levanta ferido quando a briga acabar</size>", HudColors.Bad);

        static void OnRevived(CrewmateAI mate) => Feed("Marujo de pé de novo", CrewColor);
    }
}
