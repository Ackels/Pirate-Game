using Pirates.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Barra do chefe no topo da tela (M7.1): nome do navio e do capitão e a vida do casco, só enquanto um
    /// <see cref="ShipBoss"/> está lutando. Avisa quando a escolta dele chega. Texto e largura só mudam com a vida
    /// (o resto do quadro não aloca nada).
    /// </summary>
    public class BossBarHUD : MonoBehaviour
    {
        [SerializeField] Color escortNoticeColor = new(1f, 0.55f, 0.35f);

        GameHud hud;
        VisualElement card;
        Label title, subtitle;
        HudBar bar;
        ShipBoss shown;

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Box("hud-boss");
            title = Hud.Text("", "hud-boss__title");
            subtitle = Hud.Text("", "hud-boss__subtitle");
            bar = new HudBar("hud-boss__bar");
            bar.SetColor(HudColors.Bad);
            card.Add(title);
            card.Add(subtitle);
            card.Add(bar);
            Hud.Show(card, false);
            // Primeiro da faixa do topo: fica acima da faixa do porto.
            hud.TopCenter.Insert(0, card);
            return true;
        }

        void OnEnable()
        {
            ShipBoss.EscortArrived += OnEscortArrived;
            BountyHunters.Arrived += OnHunterArrived;
        }

        void OnDisable()
        {
            ShipBoss.EscortArrived -= OnEscortArrived;
            BountyHunters.Arrived -= OnHunterArrived;
            shown = null;
            if (card != null) Hud.Show(card, false);
        }

        void Update()
        {
            if (card == null && !Build()) return;
            ShipBoss engaged = null;
            var bosses = ShipBoss.Active;
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i].IsEngaged) { engaged = bosses[i]; break; }

            if (engaged != shown) Show(engaged);
            Hud.Show(card, engaged);
            if (!engaged) return;
            bar.Set(engaged.Health.Normalized);
        }

        void Show(ShipBoss boss)
        {
            shown = boss;
            if (!boss) return;
            Hud.Set(title, boss.ShipName);
            Hud.Set(subtitle, boss.CaptainName);
            Hud.Show(subtitle, !string.IsNullOrEmpty(boss.CaptainName));
        }

        void OnHunterArrived(ShipBoss boss)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"Sua cabeça tem preço: {boss.CaptainName} vem atrás de você!", escortNoticeColor);
        }

        void OnEscortArrived(ShipBoss boss)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"O {boss.ShipName} chamou a escolta!", escortNoticeColor);
        }
    }
}
