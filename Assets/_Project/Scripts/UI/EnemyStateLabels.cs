using Pirates.Combat;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>Debug: mostra o estado da IA sobre cada navio inimigo, para avaliar o comportamento (UI Toolkit).</summary>
    public class EnemyStateLabels : MonoBehaviour
    {
        [SerializeField] float heightOffset = 9f;

        static readonly Color LabelColor = new(1f, 0.62f, 0.52f);
        EnemyShipAI[] enemies = System.Array.Empty<EnemyShipAI>();
        float nextRefresh;
        WorldLabelPool labels;

        void OnDisable() => labels?.HideAll();

        void LateUpdate()
        {
            if (labels == null)
            {
                var hud = GameHud.Current;
                if (!hud || !hud.EnsureBuilt()) return;
                labels = new WorldLabelPool(hud.World, "hud-world-label--debug");
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 1f;
                enemies = FindObjectsByType<EnemyShipAI>();
            }
            if (!labels.Begin()) return;
            foreach (var ai in enemies)
            {
                if (!ai || !ai.isActiveAndEnabled) continue;
                bool sinking = ai.TryGetComponent(out Health health) && health.IsDead;
                string text = sinking ? "Afundando" : ai.State switch
                {
                    EnemyShipAI.AIState.Chase => "Perseguindo",
                    EnemyShipAI.AIState.Broadside => "Costado",
                    EnemyShipAI.AIState.Flee => "Fugindo",
                    _ => "Patrulhando",
                };
                // Combinações fixas: sem montar texto novo a cada quadro.
                if (!sinking && ai.TargetIsSafe) text = ai.IsAvoiding ? Avoiding(Safe(text)) : Safe(text);
                else if (!sinking && ai.IsAvoiding) text = Avoiding(text);
                labels.Place(ai.transform.position + Vector3.up * heightOffset, text, LabelColor);
            }
            labels.End();
        }

        static readonly System.Collections.Generic.Dictionary<string, string> safe = new(), avoiding = new();

        static string Safe(string state)
        {
            if (!safe.TryGetValue(state, out var t)) safe[state] = t = state + " (você está na zona segura)";
            return t;
        }

        static string Avoiding(string state)
        {
            if (!avoiding.TryGetValue(state, out var t)) avoiding[state] = t = state + " (desviando)";
            return t;
        }
    }
}
