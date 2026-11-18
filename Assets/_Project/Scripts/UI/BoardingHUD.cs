using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Abordagem no HUD (04/11, passo 1): no navio, "[F] Lançar ganchos" com uma chalupa pirata avariada colada (ou o que falta);
    /// no palco, "[F] Voltar ao seu navio" perto do ponto de chegada; a tela preta da ida e da volta e a faixa ao chegar.
    /// </summary>
    public class BoardingHUD : MonoBehaviour
    {
        [SerializeField] ShipBoarding boarding;
        [SerializeField] BoardingReturn returner;

        GameHud hud;
        HudPrompt prompt;
        HudBanner banner;
        VisualElement blackout;
        BoardingIcon icon;
        HudWindow surrender;
        bool panelOwned;
        CachedText iconLine, crewLine;
        static readonly Color ReadyColor = new(1f, 0.78f, 0.3f);
        static readonly Color WaitColor = new(0.72f, 0.66f, 0.58f);
        float bannerUntil, shownFade = -1f;
        CachedText line;
        // Textos pelo tipo do navio (chalupa, brigue...), refeitos só quando o tipo muda.
        ShipBoarding.Kind textKind;
        string leaveText, boardText, blockedPrefix;

        void TextsFor(ShipBoarding.Kind kind)
        {
            if (kind == null || kind == textKind) return;
            textKind = kind;
            int minutes = Mathf.Max(1, Mathf.RoundToInt(boarding.AdriftSeconds / 60f));
            leaveText = $"Voltar ao seu navio  <size=80%>({kind.noun} fica à deriva e afunda em ~{minutes} min)</size>";
            boardText = $"Lançar ganchos  <size=80%>(abordar {kind.noun})</size>";
            blockedPrefix = $"Abordar {kind.noun}: ";
        }

        public void Bind(ShipBoarding shipBoarding, BoardingReturn boardingReturn)
        {
            boarding = shipBoarding;
            returner = boardingReturn;
        }

        void OnEnable()
        {
            if (!boarding) return;
            boarding.Boarded += OnBoarded;
            boarding.Returned += OnReturned;
            boarding.Repelled += OnRepelled;
            boarding.Rewarded += OnRewarded;
            if (boarding.Stage) boarding.Stage.Defeated += OnCrewDefeated;
        }

        void OnDisable()
        {
            if (boarding)
            {
                boarding.Boarded -= OnBoarded;
                boarding.Returned -= OnReturned;
                boarding.Repelled -= OnRepelled;
                boarding.Rewarded -= OnRewarded;
                if (boarding.Stage) boarding.Stage.Defeated -= OnCrewDefeated;
            }
            WorldPointer.Suppressed = false;
            if (prompt == null) return;
            prompt.Hide();
            banner.Hide();
            icon.Hide();
            ShowSurrender(false);
            blackout.style.display = DisplayStyle.None;
            shownFade = 0f;
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
            icon = new BoardingIcon();
            hud.World.Add(icon);
            BuildSurrender();
            return true;
        }

        void Update()
        {
            if (!boarding) return;
            if (prompt == null && !Build()) return;
            if (bannerUntil > 0f && Time.time > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
            WorldPointer.Suppressed = boarding.Active;
            UpdateBlackout();
            ShowSurrender(boarding.AwaitingChoice);
            UpdatePrompt();
            UpdateIcon();
        }

        void UpdateBlackout()
        {
            float fade = boarding.Fade;
            if (Mathf.Abs(fade - shownFade) < 0.004f) return;
            shownFade = fade;
            blackout.style.display = fade > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            blackout.style.opacity = fade;
        }

        void UpdatePrompt()
        {
            if (boarding.IsTraveling || Core.ScreenPanel.Open || boarding.AwaitingChoice)
            {
                prompt.Hide();
                return;
            }
            if (boarding.Active)
            {
                var stage = boarding.Stage;
                TextsFor(boarding.TargetKind);
                if (returner && returner.CanOpen)
                    prompt.Show("F", stage.CrewDefeated ? leaveText : "Voltar ao seu navio");
                else if (stage.CrewTotal > 0 && !stage.CrewDefeated)
                {
                    if (crewLine.Stale(stage.CrewAlive, stage.CrewTotal))
                        crewLine.Text = $"Piratas a bordo: <b>{stage.CrewAlive}</b> de {stage.CrewTotal}";
                    prompt.Show(null, crewLine.Text);
                }
                else prompt.Hide();
                return;
            }
            var enemy = boarding.Candidate;
            if (!enemy)
            {
                prompt.Hide();
                return;
            }
            string reason = boarding.BlockReason;
            TextsFor(boarding.CandidateKind);
            if (reason == null)
            {
                prompt.Show("F", boardText);
                return;
            }
            // Casco ainda forte: só avisa colado nele (no meio da troca de tiros a placa ficaria sempre na tela).
            if (boarding.CandidateDistance > boarding.Range && !boarding.CandidateDamaged)
            {
                prompt.Hide();
                return;
            }
            if (line.Stale(reason, Mathf.RoundToInt(boarding.CandidateDistance)))
                line.Text = $"{blockedPrefix}<size=85%>{reason}</size>";
            prompt.Show(null, line.Text);
        }

        /// <summary>Gancho sobre a chalupa: dourado pulsando = [F] abordar; apagado = o que falta (casco forte: só colado nele).</summary>
        void UpdateIcon()
        {
            var enemy = boarding.Candidate;
            var cam = Camera.main;
            if (!enemy || !cam || boarding.Active || boarding.IsTraveling || Core.ScreenPanel.Open
                || (boarding.CandidateDistance > boarding.Range && !boarding.CandidateDamaged))
            {
                icon.Hide();
                return;
            }
            bool ready = boarding.BlockReason == null;
            if (iconLine.Stale(boarding.BlockReason, ready ? 1 : 0, Mathf.RoundToInt(boarding.CandidateDistance)))
                iconLine.Text = ready ? "[F] Abordar" : boarding.BlockReason;
            icon.Place(hud.World, cam, enemy.transform.position + Vector3.up * 5f, ready ? ReadyColor : WaitColor, iconLine.Text, ready);
        }

        void OnBoarded(string shipName)
        {
            if (banner == null) return;
            banner.Show("Abordagem!", $"{shipName} · F no seu convés volta ao navio");
            bannerUntil = Time.time + Hud.BannerTime(3.5f);
        }

        void OnCrewDefeated() { }

        /// <summary>Janela da rendição (passo 3): as duas escolhas, num pergaminho no meio da tela; trava o ataque enquanto aberta.</summary>
        void BuildSurrender()
        {
            surrender = new HudWindow(hud.Windows, "hud-window--surrender");
            surrender.SetTitle("Rendição!", "a tripulação larga as armas");
            surrender.AddPane(surrender.Body, () => boarding.Surrendered * 1000L + boarding.RecruitsThatFit * 10L + (boarding.HasPrisoner ? 1 : 0)
                + boarding.TargetKind.id.GetHashCode() * 100000L,
                body =>
                {
                    var kind = boarding.TargetKind;
                    Win.Text(body, $"<b>{kind.label}</b> se rende. O que fazer com {kind.noun}?");
                    var plunder = Win.Section(body, "hud-surrender__option");
                    Win.Text(plunder, "<b>Saquear o porão</b>", "hud-surrender__title");
                    Win.Text(plunder, "Toda a carga do porão, mais do que sobra nos destroços, e mais chance de baú. Vai direto para o seu porão "
                        + "(o que não couber fica boiando ao lado do seu navio).");
                    plunder.Add(Win.Button("Saquear", () => boarding.Choose(ShipBoarding.Reward.Plunder)));
                    if (kind.canRecruit)
                    {
                        var recruit = Win.Section(body, "hud-surrender__option");
                        Win.Text(recruit, "<b>Recrutar os rendidos</b>", "hud-surrender__title");
                        int fits = boarding.RecruitsThatFit;
                        Win.Text(recruit, fits > 0
                            ? $"{fits} de {boarding.Surrendered} piratas entram para a sua tripulação e metade da carga vem junto."
                            : $"{boarding.Surrendered} piratas querem entrar, mas os seus beliches estão cheios. Metade da carga vem junto.");
                        recruit.Add(Win.Button("Recrutar", () => boarding.Choose(ShipBoarding.Reward.Recruit)));
                    }
                    if (!string.IsNullOrEmpty(kind.note)) Win.Text(body, $"<i>{kind.note}</i>", "hud-surrender__note");
                    if (boarding.HasPrisoner)
                        Win.Text(body, kind.canRecruit
                            ? "<i>Há um prisioneiro trancado no porão: ele vem junto nas duas escolhas.</i>"
                            : "<i>Há um prisioneiro trancado no porão: ele vem junto.</i>", "hud-surrender__note");
                    int minutes = Mathf.Max(1, Mathf.RoundToInt(boarding.AdriftSeconds / 60f));
                    Win.Text(body, $"{(kind.canRecruit ? "Nas duas, " + kind.noun : char.ToUpper(kind.noun[0]) + kind.noun.Substring(1))} fica à deriva e afunda em ~{minutes} min (afundado, só solta madeira).",
                        "hud-surrender__note");
                });
        }

        void ShowSurrender(bool show)
        {
            if (surrender == null) return;
            surrender.Show(show);
            if (show) surrender.RefreshAll();
            // O painel trava o ataque a pé enquanto a janela está aberta (os cliques são dela).
            if (show && !panelOwned)
            {
                panelOwned = true;
                Core.ScreenPanel.Open = true;
            }
            else if (!show && panelOwned)
            {
                panelOwned = false;
                Core.ScreenPanel.Open = false;
            }
        }

        void OnRewarded(string title, List<ItemStack> cargo, int recruited, bool spilled)
        {
            if (banner == null) return;
            var parts = new List<string>();
            foreach (var s in cargo) if (s.item) parts.Add($"{s.amount} {s.item.displayName}");
            if (recruited > 0) parts.Add($"{recruited} {(recruited == 1 ? "tripulante" : "tripulantes")}");
            banner.Show(title + "!", parts.Count > 0 ? string.Join(" · ", parts) : "nada de valor a bordo");
            bannerUntil = Time.time + Hud.BannerTime(5f);
            var h = GameHud.Current;
            if (h && spilled)
                h.Feed(GameHud.FeedKind.Notice, "<b>O porão encheu</b>\n<size=75%>o resto fica boiando ao lado do seu navio (F para pegar)</size>",
                    new Color(1f, 0.85f, 0.45f));
        }

        void OnRepelled()
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, $"<b>Abordagem repelida!</b>\n<size=75%>você volta ao seu navio · {boarding.TargetKind.noun} se soltou dos ganchos</size>",
                new Color(1f, 0.45f, 0.35f));
        }

        void OnReturned()
        {
            if (banner == null) return;
            banner.Hide();
            bannerUntil = 0f;
        }
    }
}
