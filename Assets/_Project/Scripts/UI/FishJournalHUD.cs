using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Diário do pescador na tela do capitão (aba Pesca, tecla P; D2, 30/10): em cima quantas espécies e lendários já vieram e os três
    /// prêmios (vara de ouro, rede reforçada, os lendários); embaixo uma ficha por espécie, com o ícone, o nome, quantas pescou e o maior
    /// tamanho. A que ainda não veio aparece em sombra, "???", só com a dica de onde e quando ela morde. Prêmio recebido = faixa.
    /// </summary>
    public class FishJournalHUD : MonoBehaviour
    {
        static readonly Color PrizeColor = new(1f, 0.83f, 0.42f);

        [SerializeField] FishJournal journal;
        [SerializeField] float bannerSeconds = 4.5f;

        GameHud hud;
        HudWindow window;
        HudBanner banner;
        float bannerUntil;
        bool open;

        public void Bind(FishJournal fishJournal) => journal = fishJournal;

        /// <summary>Aba aberta (testes pela CLI: Toggle sem teclado).</summary>
        public bool IsOpen => open;

        /// <summary>P: a aba Pesca da tela do capitão (abre, fecha ou troca de aba).</summary>
        public void Toggle() => CaptainBook.Toggle(CaptainBook.Tab.Fishing);

        void OnEnable() => FishJournal.PrizeWon += OnPrize;

        void OnDisable()
        {
            FishJournal.PrizeWon -= OnPrize;
            SetOpen(false);
        }

        void OnDestroy() => CaptainBook.Unregister(CaptainBook.Tab.Fishing);

        void Update()
        {
            if (!journal) return;
            if (window == null && !Build()) return;
            var input = GameInput.Instance;
            if (input.GameplayEnabled)
            {
                if (input.FishLog.WasPressedThisFrame()) Toggle();
                else if (open && input.Cancel.WasPressedThisFrame()) SetOpen(false);
            }
            else if (open) SetOpen(false);   // menu inicial por cima
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

        void SetOpen(bool value)
        {
            if (value && window == null) return;
            if (open == value) return;
            open = value;
            ScreenPanel.Open = value;
            if (value) CaptainBook.RefreshTabs(window);
            window?.Show(value);
            if (hud != null && hud.Prompts != null) Hud.Show(hud.Prompts, !value);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            banner = new HudBanner();
            hud.Banners.Add(banner);
            window = new HudWindow(hud.Windows, "hud-window--fishlog");
            window.SetTitle("Diário do pescador", "P ou Esc fecha");
            CaptainBook.AddTabs(window, CaptainBook.Tab.Fishing);
            CaptainBook.Register(CaptainBook.Tab.Fishing, () => open, SetOpen);
            var scroll = Win.Scroll(window.Body);
            window.AddPane(scroll.contentContainer, () => journal.Version, BuildBody);
            window.Footer.Add(Hud.Text("Cada peixe sai com um tamanho: o diário guarda o maior. Uns só mordem à noite, na chuva ou perto de certas ilhas.",
                "hud-window__hint"));
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[P]</b>", () => SetOpen(false)));
            return true;
        }

        void BuildBody(VisualElement body)
        {
            var top = Win.Section(body, "hud-fishlog__top");
            var row = Win.Row(top);
            row.Add(Hud.Text($"<b>{journal.FoundCommon}</b> de {journal.CommonTotal} espécies", "hud-fishlog__count", "hud-grow"));
            row.Add(Hud.Text($"lendários: <b>{journal.FoundLegends}</b> de {journal.LegendTotal}", "hud-fishlog__count"));
            Win.Bar(top, journal.CommonTotal > 0 ? journal.FoundCommon / (float)journal.CommonTotal : 0f);

            var prizes = Hud.Row("hud-fishlog__prizes");
            top.Add(prizes);
            Prize(prizes, journal.GoldRod, $"{journal.RodPrizeAt} espécies", Mathf.Min(journal.Found, journal.RodPrizeAt), journal.RodPrizeAt,
                (journal.Prizes & FishJournal.PrizeRod) != 0, "fisga com mais tempo e a barra da briga fica maior");
            Prize(prizes, journal.StrongNet, "todas as espécies", journal.FoundCommon, journal.CommonTotal,
                (journal.Prizes & FishJournal.PrizeNet) != 0, "a rede do navio segura mais peixes");
            Prize(prizes, null, "os lendários", journal.FoundLegends, journal.LegendTotal, (journal.Prizes & FishJournal.PrizeLegends) != 0,
                $"+{journal.LegendGold} de ouro e {journal.LegendFame} de fama");

            var grid = Hud.Box("hud-fishlog__grid");
            body.Add(grid);
            foreach (var s in journal.AllSpecies)
                if (s != null && s.item) Card(grid, s);
        }

        static void Prize(VisualElement parent, ItemDefinition item, string goal, int have, int need, bool done, string effect)
        {
            var box = Hud.Box("hud-section", "hud-fishlog__prize");
            if (done) box.AddToClassList("hud-fishlog__prize--done");
            parent.Add(box);
            var head = Hud.Row("hud-fishlog__prize-head");
            box.Add(head);
            if (item) head.Add(Win.Icon(item));
            var text = Hud.Box("hud-grow");
            head.Add(text);
            text.Add(Hud.Text($"<b>{(item ? item.displayName : "Rei dos pescadores")}</b>", "hud-window__label", "hud-window__text"));
            text.Add(Hud.Text(done ? "recebido!" : $"{goal}: <b>{have}</b> de {need}", "hud-window__small"));
            box.Add(Hud.Text(effect, "hud-window__small", "hud-window__muted", "hud-window__text"));
        }

        void Card(VisualElement grid, FishJournal.Species s)
        {
            var item = s.item;
            int count = journal.Count(item);
            bool known = count > 0;
            var card = Hud.Box("hud-section", "hud-fishlog__card");
            if (!known) card.AddToClassList("hud-fishlog__card--unknown");
            if (s.legendary) card.AddToClassList("hud-fishlog__card--legend");
            grid.Add(card);

            var head = Hud.Row("hud-fishlog__head");
            card.Add(head);
            var icon = Win.Icon(item, "hud-item__slot--big");
            if (!known)
            {
                // Sombra: o ícone em preto, a moldura sem a cor da raridade.
                foreach (var child in icon.Children()) child.style.unityBackgroundImageTintColor = new Color(0.08f, 0.06f, 0.05f, 0.75f);
                var dim = new Color(0.43f, 0.29f, 0.18f);
                icon.style.borderTopColor = dim;
                icon.style.borderBottomColor = dim;
                icon.style.borderLeftColor = dim;
                icon.style.borderRightColor = dim;
            }
            head.Add(icon);
            var names = Hud.Box("hud-grow");
            head.Add(names);
            names.Add(Hud.Text(known ? $"<color={Win.Ink(item.rarity)}><b>{item.displayName}</b></color>" : "<b>???</b>",
                "hud-window__label", "hud-window__text"));
            names.Add(Hud.Text(Rarity.NameOf(item.rarity), "hud-window__small", "hud-window__muted"));

            card.Add(Hud.Text(s.where, "hud-window__small", "hud-window__text", "hud-fishlog__where"));
            card.Add(Hud.Text(known
                    ? (s.legendary
                        ? $"Pescado! · <b>{FishJournal.FormatSize(journal.Best(item))}</b>"
                        : $"Pescados: <b>{count}</b> · recorde <b>{FishJournal.FormatSize(journal.Best(item))}</b>")
                    : "ainda não pescado",
                "hud-window__small", "hud-fishlog__stat"));
        }

        void OnPrize(string title, string subtitle)
        {
            if (banner != null)
            {
                banner.Show(title, subtitle);
                bannerUntil = Time.time + bannerSeconds;
            }
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, $"<b>{title}</b>\n<size=75%>{subtitle} · chegou ao porão</size>", PrizeColor);
        }
    }
}
