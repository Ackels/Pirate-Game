using Pirates.Controls;
using Pirates.Loot;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Coletáveis no HUD (C3, 19/10): aviso "[F] Abrir a garrafa", a carta aberta (o mesmo pergaminho da conversa com NPC, com o
    /// número da carta e quantas já foram achadas; F ou Esc fecha), o ganho de cada moeda ("Moeda da Frota de Prata 5 de 12") e a
    /// faixa da coleção completa com o prêmio. O contador fica no painel da fama (C).
    /// </summary>
    public class CollectiblesHUD : MonoBehaviour
    {
        public static readonly Color Silver = new(0.8f, 0.87f, 0.95f);

        [SerializeField] CollectiblePicker picker;
        [SerializeField] Collections collections;
        [SerializeField] float bannerSeconds = 6f;

        GameHud hud;
        VisualElement root;
        Label nameLabel, roleLabel, text;
        HudPrompt prompt;
        HudBanner banner;
        float bannerUntil;
        Collectible shown;

        public void Bind(CollectiblePicker target, Collections all)
        {
            picker = target;
            collections = all;
        }

        void OnEnable()
        {
            if (!picker) return;
            picker.Picked += OnPicked;
            picker.CollectionDone += OnCollectionDone;
        }

        void OnDisable()
        {
            if (picker)
            {
                picker.Picked -= OnPicked;
                picker.CollectionDone -= OnCollectionDone;
            }
            if (root == null) return;
            Hud.Show(root, false);
            prompt.Hide();
            banner.Hide();
            shown = null;
        }

        void Update()
        {
            if (!picker || !collections) return;
            if (root == null && !Build()) return;

            bool reading = picker.isActiveAndEnabled && picker.Reading;
            Hud.Show(root, reading);
            if (reading)
            {
                if (shown != picker.Reading) Refresh(picker.Reading);
                prompt.Hide();
                hud.LowerPickups();
            }
            else
            {
                shown = null;
                if (picker.isActiveAndEnabled && picker.Nearby) prompt.Show("F", "Abrir a garrafa com mensagem");
                else prompt.Hide();
            }
            if (bannerUntil > 0f && Time.time > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            root = Hud.Box("hud-dialogue");
            root.pickingMode = PickingMode.Ignore;
            var box = Hud.Box("hud-dialogue__box", "hud-letter");
            box.pickingMode = PickingMode.Position;
            var plate = Hud.Box("hud-dialogue__plate");
            nameLabel = Hud.Text("", "hud-dialogue__name");
            roleLabel = Hud.Text("", "hud-dialogue__role");
            plate.Add(nameLabel);
            plate.Add(roleLabel);
            text = Hud.Text("", "hud-dialogue__text", "hud-letter__text");
            var footer = Hud.Box("hud-dialogue__footer");
            var hint = Hud.Box("hud-dialogue__hint");
            hint.Add(Hud.Key("F"));
            hint.Add(Hud.Text("Fechar", "hud-dialogue__hint-text"));
            footer.Add(hint);
            box.Add(text);
            box.Add(footer);
            box.Add(plate);
            root.Add(box);
            root.style.display = DisplayStyle.None;
            hud.Windows.Add(root);
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            banner = new HudBanner();
            hud.Banners.Add(banner);
            return true;
        }

        void Refresh(Collectible c)
        {
            shown = c;
            int total = collections.Total(c.Kind);
            Hud.Set(nameLabel, string.IsNullOrEmpty(c.Title) ? "Garrafa com mensagem" : c.Title);
            Hud.Set(roleLabel, $"carta {c.Number} de {total}  ·  achadas {collections.Count(c.Kind)} de {total}");
            Hud.Set(text, $"<i>{c.Message}</i>");
        }

        void OnPicked(Collectible c, int have, int total)
        {
            var h = GameHud.Current;
            if (!h) return;
            string name = c.Kind == CollectibleKind.Coin ? "Moeda da Frota de Prata" : "Garrafa com mensagem";
            h.Feed(GameHud.FeedKind.Pickup, $"{name}  <size=80%>{have} de {total}</size>", c.Kind == CollectibleKind.Coin ? Silver : HudColors.Cream);
        }

        void OnCollectionDone(Collections.Collection spec, string got)
        {
            if (banner == null) return;
            // Título curto: "Coleção completa: Moedas da Frota de Prata" passava da faixa.
            banner.Show("Coleção completa!", string.IsNullOrEmpty(got) ? spec.doneLine : $"{spec.doneLine}  ({got})");
            bannerUntil = Time.time + bannerSeconds;
            var h = GameHud.Current;
            if (h && !string.IsNullOrEmpty(got)) h.Feed(GameHud.FeedKind.Notice, $"<b>{spec.name}</b>: {got}", HudColors.Gold);
        }
    }
}
