using Pirates.Combat;
using Pirates.LandCombat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Barra do chefe no topo da tela (M7.1): nome do navio e do capitão e a vida do casco, só enquanto um
    /// <see cref="ShipBoss"/> está lutando; a pé, o nome e o título do <see cref="LandBoss"/> (pacote B, passo 4), com o aviso da
    /// fúria (fase 2) e da vitória. Avisa quando a escolta dele chega. Texto e largura só mudam com a vida
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
        LandBoss shownLand;
        GiantSquid shownSquid;

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
            LandBoss.Enraged += OnEnraged;
            LandBoss.Defeated += OnDefeated;
            GiantSquid.Ambushed += OnSquidAmbush;
            GiantSquid.Enraged += OnSquidEnraged;
            GiantSquid.Defeated += OnSquidDefeated;
        }

        void OnDisable()
        {
            ShipBoss.EscortArrived -= OnEscortArrived;
            BountyHunters.Arrived -= OnHunterArrived;
            LandBoss.Enraged -= OnEnraged;
            LandBoss.Defeated -= OnDefeated;
            GiantSquid.Ambushed -= OnSquidAmbush;
            GiantSquid.Enraged -= OnSquidEnraged;
            GiantSquid.Defeated -= OnSquidDefeated;
            shown = null;
            shownLand = null;
            shownSquid = null;
            if (card != null) Hud.Show(card, false);
        }

        void Update()
        {
            if (card == null && !Build()) return;
            ShipBoss engaged = null;
            var bosses = ShipBoss.Active;
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i].IsEngaged) { engaged = bosses[i]; break; }

            LandBoss land = null;
            if (!engaged)
            {
                var walkers = LandBoss.Active;
                for (int i = 0; i < walkers.Count; i++)
                    if (walkers[i].IsEngaged) { land = walkers[i]; break; }
            }

            GiantSquid squid = null;
            if (!engaged && !land)
            {
                var monsters = GiantSquid.Active;
                for (int i = 0; i < monsters.Count; i++)
                    if (monsters[i].IsEngaged) { squid = monsters[i]; break; }
            }

            if (engaged != shown || land != shownLand || squid != shownSquid)
            {
                Show(engaged);
                ShowLand(land);
                ShowSquid(squid);
            }
            Hud.Show(card, engaged || land || squid);
            if (engaged) bar.Set(engaged.Health.Normalized);
            else if (land) bar.Set(land.Health.Normalized);
            else if (squid) bar.Set(squid.Health.Normalized);
        }

        void ShowLand(LandBoss boss)
        {
            shownLand = boss;
            if (!boss) return;
            Hud.Set(title, boss.DisplayName);
            Hud.Set(subtitle, boss.Title);
            Hud.Show(subtitle, !string.IsNullOrEmpty(boss.Title));
        }

        void ShowSquid(GiantSquid squid)
        {
            shownSquid = squid;
            if (!squid) return;
            Hud.Set(title, squid.DisplayName);
            Hud.Set(subtitle, squid.Title);
            Hud.Show(subtitle, !string.IsNullOrEmpty(squid.Title));
        }

        void OnSquidAmbush(GiantSquid squid)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"A {squid.DisplayName} agarrou o navio! Dispare o bordo onde estão os tentáculos!", escortNoticeColor);
        }

        void OnSquidEnraged(GiantSquid squid)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"A {squid.DisplayName} ficou furiosa!", escortNoticeColor);
        }

        void OnSquidDefeated(GiantSquid squid)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"Você derrotou a {squid.DisplayName}!", HudColors.Good);
        }

        void OnEnraged(LandBoss boss)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"{boss.DisplayName} ficou furioso!", escortNoticeColor);
        }

        void OnDefeated(LandBoss boss)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"Você derrotou o {boss.DisplayName}!", HudColors.Good);
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
