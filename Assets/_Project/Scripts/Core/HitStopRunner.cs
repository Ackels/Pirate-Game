using UnityEngine;

namespace Pirates.Core
{
    /// <summary>Quem conta o tempo real da parada de impacto (<see cref="HitStop"/>) e devolve a velocidade do jogo.</summary>
    public class HitStopRunner : MonoBehaviour
    {
        float until, baseScale = 1f, appliedScale = -1f;

        public void Begin(float seconds, float scale)
        {
            if (appliedScale < 0f)
            {
                if (Time.timeScale <= 0f) return;
                baseScale = Time.timeScale;
                appliedScale = baseScale * Mathf.Clamp01(scale);
                Time.timeScale = appliedScale;
            }
            until = Mathf.Max(until, Time.unscaledTime + seconds);
        }

        void Update()
        {
            if (appliedScale < 0f || Time.unscaledTime < until) return;
            Restore();
        }

        void OnDisable() => Restore();

        void Restore()
        {
            if (appliedScale < 0f) return;
            if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = baseScale;
            appliedScale = -1f;
        }
    }
}
