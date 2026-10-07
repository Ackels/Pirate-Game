using Pirates.Core;
using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Fumaça do cenário (chaminés, fogueiras, ruínas) indo com o vento (<see cref="IWind"/>): a velocidade lateral das partículas segue a
    /// direção e a força. Confere duas vezes por segundo e só mexe no sistema quando o vento mudou.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class WindDrift : MonoBehaviour
    {
        [Tooltip("Velocidade (m/s) com calmaria e com vento forte.")]
        [SerializeField] float calm = 0.25f, strong = 1.8f;

        ParticleSystem system;
        Vector3 shown = new(float.NaN, 0f, 0f);
        float next;

        void Awake() => system = GetComponent<ParticleSystem>();

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.5f;
            if (!Services.TryGet(out IWind wind)) return;
            var v = wind.Direction * Mathf.Lerp(calm, strong, wind.Strength);
            if ((v - shown).sqrMagnitude < 0.0025f) return;
            shown = v;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(v.x * 0.6f, v.x * 1.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(v.z * 0.6f, v.z * 1.2f);
        }
    }
}
