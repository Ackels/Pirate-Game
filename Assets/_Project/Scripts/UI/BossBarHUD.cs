using Pirates.Combat;
using Pirates.LandCombat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Barra do chefe no topo da tela (M7.1): nome do navio e do capitão e a vida do casco, só enquanto um
    /// <see cref="ShipBoss"/> está lutando; a pé, o nome e o título do <see cref="LandBoss"/> (pacote B, passo 4), com o aviso da
    /// fúria (fase 2) e da vitória; nas catacumbas, o <see cref="ChaplainGhost"/>. Avisa quando a escolta dele chega. Texto e largura só mudam com a vida
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
        Megalodon shownShark;
        ChaplainGhost shownChaplain;

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
            Megalodon.Spotted += OnSharkSpotted;
            Megalodon.Enraged += OnSharkEnraged;
            Megalodon.Defeated += OnSharkDefeated;
            ChaplainGhost.Enraged += OnChaplainEnraged;
            ChaplainGhost.Defeated += OnChaplainDefeated;
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
            Megalodon.Spotted -= OnSharkSpotted;
            Megalodon.Enraged -= OnSharkEnraged;
            Megalodon.Defeated -= OnSharkDefeated;
            ChaplainGhost.Enraged -= OnChaplainEnraged;
            ChaplainGhost.Defeated -= OnChaplainDefeated;
            shown = null;
            shownLand = null;
            shownSquid = null;
            shownShark = null;
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

            Megalodon shark = null;
            if (!engaged && !land && !squid)
            {
                var sharks = Megalodon.Active;
                for (int i = 0; i < sharks.Count; i++)
                    if (sharks[i].IsEngaged) { shark = sharks[i]; break; }
            }

            ChaplainGhost chaplain = null;
            if (!engaged && !land && !squid && !shark)
            {
                var ghosts = ChaplainGhost.Bosses;
                for (int i = 0; i < ghosts.Count; i++)
                    if (ghosts[i].IsEngaged) { chaplain = ghosts[i]; break; }
            }

            if (engaged != shown || land != shownLand || squid != shownSquid || shark != shownShark || chaplain != shownChaplain)
            {
                Show(engaged);
                ShowLand(land);
                ShowSquid(squid);
                ShowShark(shark);
                ShowChaplain(chaplain);
            }
            Hud.Show(card, engaged || land || squid || shark || chaplain);
            if (engaged) bar.Set(engaged.Health.Normalized);
            else if (land) bar.Set(land.Health.Normalized);
            else if (squid) bar.Set(squid.Health.Normalized);
            else if (shark) bar.Set(shark.Health.Normalized);
            else if (chaplain) bar.Set(chaplain.Health.Normalized);
        }

        void ShowChaplain(ChaplainGhost chaplain)
        {
            shownChaplain = chaplain;
            if (!chaplain) return;
            Hud.Set(title, chaplain.DisplayName);
            Hud.Set(subtitle, chaplain.Title);
            Hud.Show(subtitle, !string.IsNullOrEmpty(chaplain.Title));
        }

        void OnChaplainEnraged(ChaplainGhost chaplain)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"O {chaplain.DisplayName} chama as almas do salão!", escortNoticeColor);
        }

        void OnChaplainDefeated(ChaplainGhost chaplain)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"Você derrotou o {chaplain.DisplayName}! O tesouro das catacumbas está livre.", HudColors.Good);
        }

        void ShowShark(Megalodon shark)
        {
            shownShark = shark;
            if (!shark) return;
            Hud.Set(title, shark.DisplayName);
            Hud.Set(subtitle, shark.Title);
            Hud.Show(subtitle, !string.IsNullOrEmpty(shark.Title));
        }

        void OnSharkSpotted(Megalodon shark)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"Uma barbatana enorme corta a água: o {shark.DisplayName}!", escortNoticeColor);
        }

        void OnSharkEnraged(Megalodon shark)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"O {shark.DisplayName} ficou furioso! Cuidado com a água estourando!", escortNoticeColor);
        }

        void OnSharkDefeated(Megalodon shark)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Notice, $"Você derrotou o {shark.DisplayName}!", HudColors.Good);
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
