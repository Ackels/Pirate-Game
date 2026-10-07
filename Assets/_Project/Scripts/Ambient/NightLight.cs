using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Lanterna que acende à noite (M11): a luz (opcional: nos navios inimigos só o brilho, para não somar luzes) e o brilho do vidro
    /// seguem o <see cref="Level"/> que o <see cref="DayNightCycle"/> põe. Só trabalha quando o nível muda. Nas casas, o "vidro" são as
    /// janelas acesas e a luz é a que sai pela frente da casa.
    /// </summary>
    public class NightLight : MonoBehaviour
    {
        static float level;
        static int version;

        /// <summary>0 = apagadas (dia), 1 = acesas (noite fechada). Sem ciclo na cena, ficam apagadas.</summary>
        public static float Level
        {
            get => level;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - level) < 0.004f && (value > 0f || level == 0f)) return;
                level = value;
                version++;
            }
        }

        [SerializeField] Light lamp;
        [SerializeField] float intensity = 3f;
        [Tooltip("Brilho do vidro (objeto ligado à noite).")]
        [SerializeField] GameObject glow;
        [Tooltip("Nível a partir do qual o vidro brilha.")]
        [SerializeField, Range(0f, 1f)] float glowFrom = 0.15f;

        int seen = -1;

        /// <param name="from">Nível em que acende (casas: sorteado, para as janelas não acenderem todas juntas).</param>
        public void Configure(Light light, float lightIntensity, GameObject glass, float from = 0.15f)
        {
            lamp = light;
            intensity = lightIntensity;
            glow = glass;
            glowFrom = from;
        }

        void OnEnable()
        {
            seen = -1;
            Update();
        }

        void Update()
        {
            if (seen == version) return;
            seen = version;
            if (lamp)
            {
                // Apagada = desligada: o Forward+ conta luz ligada mesmo com intensidade 0.
                lamp.enabled = level > glowFrom;
                lamp.intensity = intensity * level;
            }
            if (glow && glow.activeSelf != level > glowFrom) glow.SetActive(level > glowFrom);
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            level = 0f;
            version++;
        }
    }
}
