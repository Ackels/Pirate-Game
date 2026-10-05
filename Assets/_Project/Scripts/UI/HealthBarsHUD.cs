using System.Collections.Generic;
using Pirates.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Barras de vida (UI Toolkit) sobre os alvos que levaram dano há pouco (navios, piratas, caixotes, o forte).
    /// </summary>
    public class HealthBarsHUD : MonoBehaviour
    {
        [SerializeField] Health ignore;
        [Tooltip("Mais vidas sem barra (ex.: o personagem a pé, além do navio).")]
        [SerializeField] Health[] alsoIgnore = System.Array.Empty<Health>();
        [Tooltip("Altura mínima da barra acima do alvo; alvos mais altos (navio com mastro) ganham a barra acima do topo.")]
        [SerializeField] float heightOffset = 3f;
        [SerializeField] float visibleSecondsAfterHit = 6f;

        readonly List<Health> targets = new();
        readonly Dictionary<Health, float> heights = new();
        readonly List<HudBar> bars = new();
        float nextRefresh;
        GameHud hud;
        int used;

        public void Ignore(Health health)
        {
            if (!ignore) ignore = health;
            else alsoIgnore = new List<Health>(alsoIgnore) { health }.ToArray();
        }

        void OnDisable() => Hide(0);

        void LateUpdate()
        {
            if (!hud)
            {
                hud = GameHud.Current;
                if (!hud || !hud.EnsureBuilt()) return;
            }
            var cam = Camera.main;
            var panel = hud.World.panel;
            used = 0;
            if (!cam || panel == null)
            {
                Hide(0);
                return;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 1f;
                targets.Clear();
                targets.AddRange(FindObjectsByType<Health>());
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var health = targets[i];
                if (!health || health == ignore || System.Array.IndexOf(alsoIgnore, health) >= 0 || !health.isActiveAndEnabled || health.IsDead) continue;
                if (Time.time - health.LastDamageTime > visibleSecondsAfterHit) continue;

                Vector3 screen = cam.WorldToScreenPoint(health.transform.position + Vector3.up * BarHeight(health));
                if (screen.z <= 0f) continue;
                Vector2 p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                if (used == bars.Count)
                {
                    var created = new HudBar("hud-world-bar");
                    hud.World.Add(created);
                    bars.Add(created);
                }
                var bar = bars[used++];
                bar.style.left = p.x;
                bar.style.top = p.y;
                bar.SetDanger(health.Normalized, greenWhenFull: true);
                Hud.Show(bar, true);
            }
            Hide(used);
        }

        void Hide(int from)
        {
            for (int i = from; i < bars.Count; i++) Hud.Show(bars[i], false);
        }

        /// <summary>Acima do topo do visual (medido uma vez por alvo), nunca abaixo de heightOffset.</summary>
        float BarHeight(Health health)
        {
            if (heights.TryGetValue(health, out float height)) return height;
            height = heightOffset;
            foreach (var r in health.GetComponentsInChildren<MeshRenderer>())
                height = Mathf.Max(height, r.bounds.max.y - health.transform.position.y + 0.8f);
            heights[health] = height;
            return height;
        }
    }
}
