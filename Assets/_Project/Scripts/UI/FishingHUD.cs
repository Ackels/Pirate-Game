using Pirates.Controls;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// A pesca no HUD (D2, 27/10): "[F] Pescar" de frente para a água (ou por que não dá: sem vara, mochila cheia, piratas por perto),
    /// "[Esc] Recolher" esperando, "[F] FISGAR!" e um "!" grande sobre a boia quando o peixe morde, o painel da briga com o peixe ao lado
    /// do capitão (<see cref="FishingMeter"/>, "[Espaço] Segure para subir"), o que veio (aviso; faixa com a cor da raridade a partir de
    /// raro) e o que deu errado (puxou cedo, o peixe levou a isca, escapou).
    /// </summary>
    public class FishingHUD : MonoBehaviour
    {
        [SerializeField] FishingRod rod;
        [SerializeField] float bannerSeconds = 3.5f;
        [Tooltip("Parado de frente para a água há este tempo, sem vara: mostra onde comprar.")]
        [SerializeField] float hintAfter = 1.2f;

        GameHud hud;
        HudPrompt prompt;
        HudBanner banner;
        WorldLabelPool bite;
        FishingMeter meter;
        float bannerUntil, facingWaterSince = -1f;
        CachedText line;

        public void Bind(FishingRod fishingRod) => rod = fishingRod;

        void OnEnable()
        {
            FishingRod.Caught += OnCaught;
            FishingRod.Missed += OnMissed;
        }

        void OnDisable()
        {
            FishingRod.Caught -= OnCaught;
            FishingRod.Missed -= OnMissed;
            if (prompt == null) return;
            prompt.Hide();
            banner.Hide();
            bite.HideAll();
            meter.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            banner = new HudBanner();
            hud.Banners.Add(banner);
            bite = new WorldLabelPool(hud.World, "hud-world-label--alert-now");
            meter = new FishingMeter();
            hud.World.Add(meter);
            meter.Hide();
            return true;
        }

        void Update()
        {
            if (!rod) return;
            if (prompt == null && !Build()) return;
            if (bannerUntil > 0f && Time.time > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
            if (!rod.isActiveAndEnabled || Pirates.Core.ScreenPanel.Open)
            {
                prompt.Hide();
                bite.HideAll();
                meter.Hide();
                return;
            }
            UpdatePrompt();
            UpdateMeter();
            bite.Begin();
            if (rod.State == FishingRod.Phase.Bite) bite.Place(rod.BobberPosition + Vector3.up * 0.9f, "!", HudColors.Gold);
            bite.End();
        }

        void UpdatePrompt()
        {
            switch (rod.State)
            {
                case FishingRod.Phase.Waiting:
                    prompt.Show("Esc", "Recolher  <size=80%>(espere a boia afundar e aperte F)</size>");
                    return;
                case FishingRod.Phase.Bite:
                    prompt.Show("F", "<b>FISGAR!</b>");
                    return;
                case FishingRod.Phase.Fight:
                    // A tecla redonda só cabe 3 letras: Espaço vai no texto.
                    prompt.Show(null, "Segure <b>Espaço</b> para subir a barra  <size=80%>(peixe dentro dela enche o medidor · Esc solta)</size>");
                    return;
                case FishingRod.Phase.Reeling:
                    prompt.Show(null, rod.Hooked ? "Puxando..." : "Recolhendo a linha...");
                    return;
                case FishingRod.Phase.Casting:
                case FishingRod.Phase.Showing:
                    prompt.Hide();
                    return;
            }
            if (!rod.SpotAhead)
            {
                facingWaterSince = -1f;
                prompt.Hide();
                return;
            }
            if (rod.CanOpen)
            {
                prompt.Show("F", "Pescar");
                return;
            }
            // Sem vara: só avisa parado de frente para a água um instante (andando pela praia não fica piscando).
            if (!rod.HasRod)
            {
                if (facingWaterSince < 0f) facingWaterSince = Time.time;
                bool still = rod.TryGetComponent(out Pirates.OnFoot.FootCharacter c) && c.PlanarVelocity.sqrMagnitude < 0.04f;
                if (!still) facingWaterSince = Time.time;
                if (Time.time - facingWaterSince < hintAfter)
                {
                    prompt.Hide();
                    return;
                }
            }
            if (rod.BlockReason.Length == 0)
            {
                prompt.Hide();
                return;
            }
            if (line.Stale(rod.BlockReason)) line.Text = $"Pescar  <size=80%>({rod.BlockReason})</size>";
            prompt.Show(null, line.Text);
        }

        /// <summary>O painel da briga ao lado do capitão, só na briga.</summary>
        void UpdateMeter()
        {
            var cam = Camera.main;
            if (rod.State != FishingRod.Phase.Fight || !cam || hud.World.panel == null)
            {
                meter.Hide();
                return;
            }
            Vector3 screen = cam.WorldToScreenPoint(rod.transform.position + Vector3.up * 1.1f);
            if (screen.z <= 0f)
            {
                meter.Hide();
                return;
            }
            Vector2 p = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(hud.World.panel, new Vector2(screen.x, Screen.height - screen.y));
            meter.Show(rod, p, rod.Hooked ? Rarity.ColorOf(rod.Hooked.rarity) : HudColors.Gold);
        }

        void OnCaught(FishingRod who, ItemDefinition item)
        {
            if (who != rod || !item) return;
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, $"Pescou: <b>{item.displayName}</b>  <size=80%>(+1 na mochila)</size>", item.color);
            if (item.rarity < ItemRarity.Rare || banner == null) return;
            banner.Show($"<color={Rarity.Hex(item.rarity)}>{Rarity.NameOf(item.rarity)}</color>: {item.displayName}!", "pescado");
            bannerUntil = Time.time + bannerSeconds;
        }

        void OnMissed(FishingRod who, string reason)
        {
            if (who != rod || string.IsNullOrEmpty(reason)) return;
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, $"Nada: {reason}", new Color(0.85f, 0.8f, 0.7f));
        }
    }
}
