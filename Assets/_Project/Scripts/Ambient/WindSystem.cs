using Pirates.Core;
using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Vento (M11, clima): direção e força mudam devagar com a hora do jogo (ruído pela hora do <see cref="ITimeOfDay"/>: o save do relógio
    /// já traz o vento junto). Registra <see cref="IWind"/> no <see cref="Services"/>; os navios andam mais com o vento de través ou por
    /// trás e menos contra ele, a fumaça e a espuma do mar acompanham.
    /// </summary>
    public class WindSystem : MonoBehaviour, IWind
    {
        [SerializeField] WindSettings settings;

        float? fixedDegrees, fixedStrength;

        public Vector3 Direction { get; private set; } = Vector3.right;
        public float Degrees { get; private set; } = 90f;
        public float Strength { get; private set; } = 0.5f;
        public bool Overridden => fixedDegrees.HasValue || fixedStrength.HasValue;
        public WindSettings Settings => settings;

        public void Configure(WindSettings windSettings) => settings = windSettings;

        void OnEnable()
        {
            Services.Register<IWind>(this);
            Evaluate();
        }

        void OnDisable() => Services.Unregister<IWind>(this);

        void Update() => Evaluate();

        public void SetOverride(float? degrees, float? strength)
        {
            fixedDegrees = degrees;
            fixedStrength = strength.HasValue ? Mathf.Clamp01(strength.Value) : null;
            Evaluate();
        }

        /// <summary>Horas de jogo corridas (dia × 24 + hora); sem ciclo na cena, 1 hora por minuto real.</summary>
        static float GameHours() => Services.TryGet(out ITimeOfDay time) ? (time.Day - 1) * 24f + time.Hour : Time.time / 60f;

        void Evaluate()
        {
            if (!settings) return;
            float t = GameHours() / settings.changeHours;
            float degrees = fixedDegrees ?? settings.prevailingDegrees + (Mathf.PerlinNoise(t, 7.31f) - 0.5f) * 2f * settings.directionSwing;
            // O ruído fica quase todo entre 0,25 e 0,75: esticado para ir de calmaria a vento forte.
            float gust = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(t * 1.37f + 11.3f, 91.7f)));
            Strength = fixedStrength ?? Mathf.Lerp(settings.minStrength, settings.maxStrength, gust);
            Degrees = Mathf.Repeat(degrees, 360f);
            Direction = Quaternion.Euler(0f, Degrees, 0f) * Vector3.forward;
        }

        public float SailFactor(Vector3 forward)
        {
            if (!settings) return 1f;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return 1f;
            float angle = Vector3.Angle(forward, Direction);
            float factor = settings.pointOfSail.Evaluate(angle);
            return 1f + (factor - 1f) * Mathf.Lerp(settings.calmInfluence, 1f, Strength);
        }
    }
}
