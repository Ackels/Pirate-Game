using Pirates.Economy;
using Pirates.Loot;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// HUD do loot no mar (UI Toolkit): porão (cartão à direita), nome sobre os destroços, aviso para saquear, ganhos
    /// ("+3 Madeira") e a janela de saque (destroços ↔ porão, com o mouse).
    /// </summary>
    public class LootHUD : MonoBehaviour
    {
        [SerializeField] WreckSalvager salvager;
        [SerializeField] float labelRange = 250f;

        CachedText promptText;
        GameHud hud;
        CargoCard hold;
        HudPrompt prompt;
        WorldLabelPool wreckLabels;
        LootWindow window;

        public void Bind(WreckSalvager target) => salvager = target;

        void OnEnable()
        {
            if (salvager) salvager.Transferred += OnTransferred;
        }

        void OnDisable()
        {
            if (salvager) salvager.Transferred -= OnTransferred;
            if (hold == null) return;
            hold.Hide();
            prompt.Hide();
            wreckLabels.HideAll();
            window.Update(false, null);
        }

        void OnTransferred(ItemStack stack, bool toHold)
        {
            string text = !toHold ? $"-{stack.amount} {stack.item.displayName} (de volta à água)"
                : stack.item.category == ItemCategory.Schematic ? $"Esquema obtido: {stack.item.displayName}!"
                : stack.item.category == ItemCategory.Crew ? $"+{stack.amount} {stack.item.displayName} a bordo"
                : $"+{stack.amount} {stack.item.displayName}";
            var color = toHold ? stack.item.color : new Color(0.75f, 0.75f, 0.75f);
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, text, color);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            hold = new CargoCard(hud.TopRight);
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            wreckLabels = new WorldLabelPool(hud.World, "hud-world-label--small");
            window = new LootWindow(hud.Windows, new LootWindow.Setup
            {
                sourceTitle = "Nos destroços",
                targetTitle = "No porão",
                emptySource = "nada sobrou",
                emptyTarget = "vazio",
                giveBack = "Jogar",
                giveBackHint = "devolve o item aos destroços",
                contents = () => salvager.Current ? salvager.Current.Contents : null,
                target = () => salvager.Inventory,
                take = salvager.Take,
                discard = salvager.Discard,
                takeAll = salvager.TakeAll,
                close = salvager.Close,
            });
            return true;
        }

        void Update()
        {
            if (!salvager) return;
            if (hold == null && !Build()) return;

            hold.Show("Porão", salvager.Inventory, "vazio: afunde um navio e saqueie os destroços");
            UpdateWreckLabels();
            bool open = salvager.IsOpen;
            window.Update(open, "Saque dos destroços");
            if (open)
            {
                prompt.Hide();
                hud.LowerPickups();
            }
            else UpdatePrompt();
        }

        void UpdatePrompt()
        {
            if (!salvager.Nearby)
            {
                prompt.Hide();
                return;
            }
            bool tooFast = salvager.TooFast;
            if (promptText.Stale(HudDraw.Flag(tooFast), tooFast ? HudDraw.Tenths(salvager.SpeedKnots) : 0))
                promptText.Text = tooFast
                    ? $"Recolha as velas para saquear  <size=80%>({salvager.SpeedKnots:0.0} / {salvager.MaxSpeedKnots:0} nós)</size>"
                    : "Saquear destroços";
            prompt.Show(tooFast ? null : "F", promptText.Text);
        }

        void UpdateWreckLabels()
        {
            if (!wreckLabels.Begin()) return;
            var cam = Camera.main;
            float range2 = labelRange * labelRange;
            for (int i = 0; i < ShipWreck.Active.Count; i++)
            {
                var wreck = ShipWreck.Active[i];
                Vector3 pos = wreck.transform.position + Vector3.up * 3f;
                if ((pos - cam.transform.position).sqrMagnitude > range2) continue;
                Color color = wreck.HasChestLoot ? HudColors.Gold : HudColors.Cream;
                if (wreck.SecondsLeft < 15f && !wreck.IsBeingSalvaged) color.a = 0.55f;
                wreckLabels.Place(pos, wreck.Label, color);
            }
            wreckLabels.End();
        }
    }
}
