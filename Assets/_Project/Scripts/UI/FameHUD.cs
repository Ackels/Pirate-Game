using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Missions;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Fama do capitão no HUD (M10, UI Toolkit): plaquinha no canto de cima à direita (patente, barra até a próxima, tecla C;
    /// avisa quando há bônus para escolher), "+N fama" nos ganhos, faixa de patente nova e o painel da tecla C (pergaminho com
    /// o retrato do capitão, nome, patente, estrelas, feitos, barra de fama, o que cada patente libera e os bônus: escolher um
    /// quando há patente nova). O painel não pausa; com ele aberto, os cliques são dele (ScreenPanel).
    /// </summary>
    public class FameHUD : MonoBehaviour
    {
        static readonly Color FameColor = new(1f, 0.83f, 0.42f);

        [SerializeField] CaptainFame fame;
        [Tooltip("Retrato do capitão (feito pelo gerador a partir do modelo do jogador).")]
        [SerializeField] Texture2D portrait;
        [SerializeField] float bannerSeconds = 4.5f;

        GameHud hud;
        HudWindow window;
        VisualElement chip;
        Label chipRank, chipNote;
        HudBar chipBar;
        HudBanner banner;
        float bannerUntil;
        int shownVersion = -1;
        bool open;

        public void Bind(CaptainFame captainFame, Texture2D captainPortrait)
        {
            fame = captainFame;
            portrait = captainPortrait;
        }

        /// <summary>Painel aberto (teste pela CLI: Toggle sem teclado).</summary>
        public bool IsOpen => open;

        void OnEnable()
        {
            if (!fame) return;
            fame.FameGained += OnFameGained;
            fame.RankUp += OnRankUp;
        }

        void OnDisable()
        {
            if (fame)
            {
                fame.FameGained -= OnFameGained;
                fame.RankUp -= OnRankUp;
            }
            SetOpen(false);
        }

        void Update()
        {
            if (!fame) return;
            if (chip == null && !Build()) return;

            var input = GameInput.Instance;
            if (input.GameplayEnabled)
            {
                if (input.Fame.WasPressedThisFrame()) Toggle();
                else if (open && input.Cancel.WasPressedThisFrame()) SetOpen(false);
            }
            else if (open) SetOpen(false);   // menu inicial por cima

            if (fame.Version != shownVersion)
            {
                shownVersion = fame.Version;
                RefreshChip();
            }
            if (open)
            {
                window.RefreshAll();
                hud.LowerPickups();
            }
            if (bannerUntil > 0f && Time.time > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
        }

        public void Toggle() => SetOpen(!open);

        void SetOpen(bool value)
        {
            if (value && window == null) return;
            open = value;
            ScreenPanel.Open = value;
            window?.Show(value);
            // Os avisos de ação ("[F] atracar") ficam por cima das janelas: somem com o painel aberto.
            if (hud != null && hud.Prompts != null) Hud.Show(hud.Prompts, !value);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;

            chip = Hud.Box("hud-card", "hud-card--wood", "hud-fame-chip");
            var top = Hud.Row("hud-fame-chip__row");
            top.Add(Hud.Box("hud-fame-chip__star"));
            chipRank = Hud.Text("", "hud-fame-chip__rank");
            top.Add(chipRank);
            top.Add(Hud.Key("C"));
            chip.Add(top);
            chipBar = new HudBar("hud-fame-chip__bar");
            chipBar.SetColor(FameColor);
            chip.Add(chipBar);
            chipNote = Hud.Text("", "hud-fame-chip__note");
            chip.Add(chipNote);
            hud.TopRight.Insert(0, chip);

            banner = new HudBanner();
            hud.Banners.Add(banner);

            window = new HudWindow(hud.Windows, "hud-window--fame");
            window.SetTitle("Fama do capitão", "C ou Esc fecha");
            window.AddPane(window.Body, () => Win.Mix(fame.Version, MissionBoardKey()), BuildBody);
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[C]</b>", () => SetOpen(false)));
            return true;
        }

        static long MissionBoardKey() => Services.TryGet(out MissionBoard board) ? board.Finished.Count : 0;

        // ---------- Plaquinha ----------

        void RefreshChip()
        {
            Hud.Set(chipRank, fame.RankTitle(fame.Rank));
            chipBar.Set(fame.RankProgress);
            bool pending = fame.PendingPerks > 0;
            chip.EnableInClassList("hud-fame-chip--pending", pending);
            string note = pending ? "bônus para escolher!"
                : fame.IsMaxRank ? $"{fame.Fame} de fama · patente máxima"
                : $"{fame.Fame} / {fame.RankFame(fame.Rank + 1)} de fama";
            Hud.Set(chipNote, note);
        }

        // ---------- Avisos ----------

        void OnFameGained(int amount, string reason)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, $"+{amount} fama  <size=80%>{reason}</size>", FameColor);
        }

        void OnRankUp(int rank)
        {
            if (banner == null) return;
            banner.Show($"Nova patente: {fame.RankTitle(rank)}", fame.PendingPerks > 0 ? "aperte C para escolher um bônus" : null);
            bannerUntil = Time.time + bannerSeconds;
        }

        // ---------- Painel ----------

        void BuildBody(VisualElement body)
        {
            var layout = Hud.Row("hud-fame");
            body.Add(layout);
            BuildCaptain(layout);

            var right = Win.Scroll(layout);
            right.AddToClassList("hud-fame__right");
            BuildProgress(right);
            if (fame.PendingPerks > 0) BuildPerkChoice(right);
            BuildRanks(right);
            BuildPerks(right);
            Win.Text(right, "<i>Fama vem de afundar navios, derrotar acampamentos, calar fortes, desenterrar tesouros e entregar missões " +
                            "(as difíceis valem mais).</i>", "hud-window__small", "hud-window__muted");
        }

        /// <summary>Coluna da esquerda: retrato na moldura, nome, patente com estrelas e os feitos.</summary>
        void BuildCaptain(VisualElement layout)
        {
            var card = Hud.Box("hud-fame__card");
            layout.Add(card);

            var frame = Hud.Box("hud-fame__frame");
            var face = Hud.Box("hud-fame__portrait");
            if (portrait) face.style.backgroundImage = new StyleBackground(portrait);
            frame.Add(face);
            frame.Add(Hud.Box("hud-fame__ring"));
            card.Add(frame);

            card.Add(Hud.Text(fame.CaptainName, "hud-fame__name"));
            card.Add(Hud.Text(fame.RankTitle(fame.Rank), "hud-fame__rank"));
            var stars = Hud.Row("hud-fame__stars");
            for (int i = 1; i < fame.RankCount; i++)
                stars.Add(Hud.Box("hud-fame__star", i <= fame.Rank ? "hud-fame__star--on" : "hud-fame__star--off"));
            card.Add(stars);
            string quote = fame.RankDescription(fame.Rank);
            if (!string.IsNullOrEmpty(quote)) card.Add(Hud.Text($"\"{quote}\"", "hud-fame__quote"));

            var deeds = Hud.Box("hud-section", "hud-fame__deeds");
            card.Add(deeds);
            Deed(deeds, "Navios afundados", fame.ShipsSunk);
            Deed(deeds, "Acampamentos e fortes", fame.CampsCleared);
            Deed(deeds, "Tesouros desenterrados", fame.TreasuresFound);
            Deed(deeds, "Missões entregues", fame.MissionsDone);
        }

        static void Deed(VisualElement parent, string label, int value)
        {
            var row = Hud.Row("hud-fame__deed");
            row.Add(Hud.Text(label, "hud-window__small", "hud-grow"));
            row.Add(Hud.Text($"<b>{value}</b>", "hud-fame__deed-value"));
            parent.Add(row);
        }

        void BuildProgress(VisualElement parent)
        {
            var box = Win.Section(parent, "hud-fame__progress");
            var top = Win.Row(box);
            top.Add(Hud.Text($"{fame.Fame}", "hud-fame__points"));
            top.Add(Hud.Text("de fama", "hud-fame__points-label", "hud-grow"));
            if (!fame.IsMaxRank)
                top.Add(Hud.Text($"próxima: <b>{fame.RankTitle(fame.Rank + 1)}</b> com {fame.RankFame(fame.Rank + 1)}", "hud-window__small", "hud-align-right"));
            var bar = new HudBar("hud-fame__bar");
            bar.Set(fame.RankProgress);
            bar.SetColor(new Color(0.85f, 0.6f, 0.15f));
            box.Add(bar);
            Win.Text(box, fame.IsMaxRank
                ? "<b>Patente máxima.</b> Os mares inteiros conhecem o seu nome."
                : $"Faltam <b>{fame.RankFame(fame.Rank + 1) - fame.Fame}</b> de fama para <b>{fame.RankTitle(fame.Rank + 1)}</b>.",
                "hud-window__small");
        }

        void BuildPerkChoice(VisualElement parent)
        {
            var box = Win.Section(parent, "hud-section--ship");
            int pending = fame.PendingPerks;
            Win.Text(box, pending > 1 ? $"Escolha um bônus  <size=75%>(mais {pending - 1} depois deste)</size>" : "Escolha um bônus",
                "hud-window__subtitle-strong");
            Win.Text(box, "<i>Vale para sempre, neste navio e nos próximos.</i>", "hud-window__small", "hud-window__muted");
            var row = Hud.Row("hud-fame__offers");
            box.Add(row);
            foreach (var perk in fame.Offer)
            {
                var p = perk;
                var card = Hud.Box("hud-section", "hud-fame__offer");
                card.pickingMode = PickingMode.Position;
                card.Add(Hud.Text($"<b>{p.title}</b>", "hud-window__label", "hud-window__text"));
                card.Add(Hud.Text(p.description, "hud-window__small", "hud-window__text", "hud-grow"));
                card.Add(Win.Button("Escolher", () => Choose(p), true, "hud-button--small"));
                row.Add(card);
            }
        }

        void Choose(FameSettings.Perk perk)
        {
            if (!fame.ChoosePerk(perk)) return;
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, $"Bônus de patente: {perk.title}", FameColor);
        }

        void BuildRanks(VisualElement parent)
        {
            Win.Text(parent, "Patentes", "hud-window__subtitle-strong");
            for (int r = 0; r < fame.RankCount; r++)
            {
                bool reached = r <= fame.Rank, current = r == fame.Rank;
                var row = Hud.Row("hud-fame__rank-row");
                if (current) row.AddToClassList("hud-fame__rank-row--current");
                if (!reached) row.AddToClassList("hud-fame__rank-row--locked");
                row.Add(Hud.Box("hud-fame__star", reached ? "hud-fame__star--on" : "hud-fame__star--off"));
                var text = Hud.Box("hud-grow");
                text.Add(Hud.Text($"<b>{fame.RankTitle(r)}</b>   <size=85%>{(r == 0 ? "início" : $"{fame.RankFame(r)} de fama")}</size>",
                    "hud-window__label", "hud-window__text"));
                text.Add(Hud.Text(Unlocks(r), "hud-window__small", "hud-window__text", "hud-window__muted"));
                row.Add(text);
                if (current) row.Add(Hud.Text("você", "hud-fame__you"));
                parent.Add(row);
            }
        }

        /// <summary>O que a patente libera: bônus, ouro das missões, contratos e itens do mercado que pedem ela.</summary>
        string Unlocks(int rank)
        {
            if (rank == 0) return "começo de carreira: os contratos simples da taverna";
            var parts = new List<string> { "1 bônus para escolher" };
            float gold = fame.Settings ? fame.Settings.missionGoldPerRank * rank : 0f;
            if (gold > 0f) parts.Add($"ouro das missões +{gold * 100f:0}% (menos entregas)");
            if (Services.TryGet(out MissionBoard board))
                foreach (var m in board.Offered)
                    if (m && m.requiredRank == rank) parts.Add($"contrato \"{m.title}\"");
            if (HarborDock.Active.Count > 0 && HarborDock.Active[0].Settings)
                foreach (var e in HarborDock.Active[0].Settings.market)
                    if (e?.item && e.requiredRank == rank) parts.Add($"{MarketName(e)} no mercado");
            return string.Join(" · ", parts);
        }

        static string MarketName(HarborSettings.MarketEntry e) =>
            e.item.category == ItemCategory.Schematic ? $"esquema {e.item.displayName}" : e.item.displayName.ToLowerInvariant();

        void BuildPerks(VisualElement parent)
        {
            Win.Text(parent, "Seus bônus", "hud-window__subtitle-strong");
            if (fame.Perks.Count == 0)
            {
                Win.Text(parent, "nenhum ainda: cada patente nova dá um", "hud-window__empty");
                return;
            }
            foreach (var p in fame.Perks)
            {
                var row = Hud.Row("hud-fame__perk");
                row.Add(Hud.Box("hud-fame__check"));
                row.Add(Hud.Text($"<b>{p.title}</b>  <size=90%>{p.description}</size>", "hud-window__small", "hud-window__text", "hud-grow"));
                parent.Add(row);
            }
        }
    }
}
