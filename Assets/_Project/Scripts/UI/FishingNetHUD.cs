using System.Collections.Generic;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// HUD da pesca com rede (D2, passo 2): "Cardume" sobre as bolhas por perto, o aviso do F ("Lançar a rede", o porquê de não dar,
    /// "Puxar a rede (3 / 6)") e, ao puxar, o que foi para o porão nos ganhos. Só com o navio ligado (a pé o FishingHUD cuida da vara).
    /// </summary>
    public class FishingNetHUD : MonoBehaviour
    {
        [SerializeField] FishingNet net;
        [SerializeField] float labelRange = 300f;

        GameHud hud;
        HudPrompt prompt;
        WorldLabelPool labels;
        CachedText promptText;
        string shownReason, reasonText;

        public void Bind(FishingNet fishingNet) => net = fishingNet;

        void OnEnable()
        {
            FishingNet.Hauled += OnHauled;
        }

        void OnDisable()
        {
            FishingNet.Hauled -= OnHauled;
            if (prompt == null) return;
            prompt.Hide();
            labels.HideAll();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            labels = new WorldLabelPool(hud.World, "hud-world-label--small");
            return true;
        }

        void Update()
        {
            if (prompt == null && !Build()) return;
            bool aboard = net && net.isActiveAndEnabled;
            if (!aboard)
            {
                prompt.Hide();
                labels.HideAll();
                return;
            }
            UpdateLabels();
            UpdatePrompt();
        }

        void UpdatePrompt()
        {
            switch (net.State)
            {
                case FishingNet.Phase.Lowering:
                case FishingNet.Phase.Soaking:
                {
                    bool full = net.Caught >= net.Capacity;
                    if (promptText.Stale(net.Caught, net.Capacity))
                        promptText.Text = $"Puxar a rede  <size=80%>({net.Caught} / {net.Capacity}{(full ? ", cheia" : "")})</size>";
                    prompt.Show("F", promptText.Text);
                    return;
                }
                case FishingNet.Phase.Hauling:
                    prompt.Hide();
                    return;
            }
            var spot = net.NearestSpot;
            if (!spot || net.OtherAvailable || net.SpeedKnots > 6f)
            {
                prompt.Hide();
                return;
            }
            if (!net.InsideSpot)
            {
                // Parando perto do cardume: dica para entrar nas bolhas.
                if (net.SpeedKnots > 3f || !net.HasNet) prompt.Hide();
                else prompt.Show(null, "Entre nas bolhas do cardume para lançar a rede");
                return;
            }
            if (net.BlockReason.Length == 0)
            {
                prompt.Show("F", "Lançar a rede");
                return;
            }
            // Texto refeito só quando o motivo muda (o FishingNet troca a string a cada 0,25 s no máximo).
            if (!ReferenceEquals(shownReason, net.BlockReason))
            {
                shownReason = net.BlockReason;
                reasonText = "Rede de pesca: " + shownReason;
            }
            prompt.Show(null, reasonText);
        }

        void UpdateLabels()
        {
            if (!labels.Begin()) return;
            var cam = Camera.main;
            float range2 = labelRange * labelRange;
            for (int i = 0; i < FishingSpot.Active.Count; i++)
            {
                var spot = FishingSpot.Active[i];
                if (spot.Empty) continue;
                Vector3 pos = spot.transform.position + Vector3.up * 2.5f;
                if (!cam || (pos - cam.transform.position).sqrMagnitude > range2) continue;
                labels.Place(pos, "Cardume", HudColors.Cream);
            }
            labels.End();
        }

        void OnHauled(FishingNet from, IReadOnlyList<ItemStack> landed, int lost)
        {
            var h = GameHud.Current;
            if (!h) return;
            if (landed.Count == 0 && lost == 0) h.Feed(GameHud.FeedKind.Pickup, "A rede voltou vazia", new Color(0.75f, 0.75f, 0.75f));
            for (int i = 0; i < landed.Count; i++)
                h.Feed(GameHud.FeedKind.Pickup, $"+{landed[i].amount} {landed[i].item.displayName}", landed[i].item.color);
            if (lost > 0) h.Feed(GameHud.FeedKind.Pickup, $"Porão cheio: {lost} voltaram para o mar", new Color(1f, 0.55f, 0.4f));
        }
    }
}
