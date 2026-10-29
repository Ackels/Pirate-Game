using Pirates.Controls;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// Cortar árvores e minerar no HUD (D1, 28/10): "[F] Cortar a árvore" / "[F] Minerar o veio de ferro" com a ferramenta (sem ela, "sem machadinha (compre no
    /// mercado)" só parado de frente para a árvore um instante: andando pela mata não fica piscando), "[Esc] Parar" com os golpes que
    /// faltam enquanto corta, e o aviso do que veio ("+3 madeira na mochila") ou do que não coube.
    /// </summary>
    public class GatheringHUD : MonoBehaviour
    {
        [SerializeField] Gatherer gatherer;
        [Tooltip("Parado de frente para a árvore há este tempo, sem a ferramenta: mostra onde comprar.")]
        [SerializeField] float hintAfter = 1.2f;

        HudPrompt prompt;
        float facingSince = -1f;
        CachedText line;

        public void Bind(Gatherer who) => gatherer = who;

        void OnEnable()
        {
            Gatherer.Gathered += OnGathered;
            Gatherer.Stopped += OnStopped;
        }

        void OnDisable()
        {
            Gatherer.Gathered -= OnGathered;
            Gatherer.Stopped -= OnStopped;
            prompt?.Hide();
        }

        bool Build()
        {
            var hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            return true;
        }

        void Update()
        {
            if (!gatherer) return;
            if (prompt == null && !Build()) return;
            if (!gatherer.isActiveAndEnabled || Pirates.Core.ScreenPanel.Open)
            {
                prompt.Hide();
                return;
            }
            var target = gatherer.Target;
            var tool = gatherer.TargetTool;
            if (gatherer.Working && target)
            {
                if (line.Stale(target.HitsLeft)) line.Text = $"Parar  <size=80%>(faltam {target.HitsLeft} de {target.Hits} golpes)</size>";
                prompt.Show("Esc", line.Text);
                return;
            }
            if (!target || tool == null || !target.Standing)
            {
                facingSince = -1f;
                prompt.Hide();
                return;
            }
            if (gatherer.CanOpen)
            {
                prompt.Show("F", target.Type == Pirates.Loot.ResourceNode.Kind.Tree ? $"{tool.verb} a árvore" : $"{tool.verb} o veio de ferro");
                return;
            }
            if (gatherer.BlockReason.Length == 0)
            {
                prompt.Hide();
                return;
            }
            // Sem ferramenta: só avisa parado de frente para a árvore um instante (andando pela mata não fica piscando).
            if (gatherer.BlockReason.StartsWith("sem "))
            {
                bool still = gatherer.TryGetComponent(out Pirates.OnFoot.FootCharacter c) && c.PlanarVelocity.sqrMagnitude < 0.04f;
                if (!still || facingSince < 0f) facingSince = Time.time;
                if (Time.time - facingSince < hintAfter)
                {
                    prompt.Hide();
                    return;
                }
            }
            prompt.Show(null, $"{tool.verb}  <size=80%>({gatherer.BlockReason})</size>");
        }

        void OnGathered(Gatherer who, ItemDefinition item, int added, int lost)
        {
            if (who != gatherer || !item) return;
            var h = GameHud.Current;
            if (!h) return;
            string name = item.displayName.ToLowerInvariant();
            if (added > 0) h.Feed(GameHud.FeedKind.Pickup, $"+{added} {name}  <size=80%>(na mochila)</size>", item.color);
            if (lost > 0) h.Feed(GameHud.FeedKind.Pickup, $"Mochila cheia: {lost} de {name} ficou para trás", new Color(0.95f, 0.55f, 0.4f));
        }

        void OnStopped(Gatherer who, string reason)
        {
            if (who != gatherer || string.IsNullOrEmpty(reason)) return;
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, $"Parou: {reason}", new Color(0.85f, 0.8f, 0.7f));
        }
    }
}
