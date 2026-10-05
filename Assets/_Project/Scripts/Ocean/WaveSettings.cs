using System;
using UnityEngine;

namespace Pirates.Ocean
{
    /// <summary>
    /// Definição das ondas (soma de senoides direcionais). A mesma função alimenta
    /// a malha visual e a física, então o barco sempre "senta" na água que se vê.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Ocean/Wave Settings", fileName = "WaveSettings")]
    public class WaveSettings : ScriptableObject
    {
        [Serializable]
        public struct Wave
        {
            [Tooltip("Direção de propagação em graus (0 = +Z).")]
            public float directionDegrees;
            [Min(0.5f)] public float wavelength;
            [Min(0f)] public float amplitude;
            [Tooltip("Multiplicador sobre a velocidade física de onda em águas profundas.")]
            public float speedMultiplier;
        }

        public float seaLevel = 0f;

        public Wave[] waves =
        {
            new() { directionDegrees = 0f,   wavelength = 60f, amplitude = 0.55f, speedMultiplier = 1f },
            new() { directionDegrees = 38f,  wavelength = 31f, amplitude = 0.32f, speedMultiplier = 1f },
            new() { directionDegrees = -57f, wavelength = 17f, amplitude = 0.16f, speedMultiplier = 1f },
            new() { directionDegrees = 105f, wavelength = 9f,  amplitude = 0.07f, speedMultiplier = 1f },
        };

        /// <summary>Constantes de uma onda, calculadas uma vez: altura = amplitude · sen(kx·x + kz·z − ω·t).</summary>
        public struct Term
        {
            public float kx, kz, omega, amplitude;
        }

        [NonSerialized] Term[] terms;

        /// <summary>Ondas já convertidas em constantes (refeitas quando o asset muda no Inspector).</summary>
        public Term[] Terms
        {
            get
            {
                if (terms == null || terms.Length != waves.Length) BuildTerms();
                return terms;
            }
        }

        void OnEnable() => terms = null;
        void OnValidate() => terms = null;

        void BuildTerms()
        {
            terms = new Term[waves.Length];
            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
                float k = 2f * Mathf.PI / w.wavelength;
                float speed = Mathf.Sqrt(9.81f / k) * w.speedMultiplier;
                float rad = w.directionDegrees * Mathf.Deg2Rad;
                terms[i] = new Term
                {
                    kx = k * Mathf.Sin(rad),
                    kz = k * Mathf.Cos(rad),
                    omega = k * speed,
                    amplitude = w.amplitude,
                };
            }
        }

        public float Evaluate(float x, float z, float time)
        {
            var t = Terms;
            float height = seaLevel;
            for (int i = 0; i < t.Length; i++)
                height += t[i].amplitude * Mathf.Sin(t[i].kx * x + t[i].kz * z - t[i].omega * time);
            return height;
        }
    }
}
