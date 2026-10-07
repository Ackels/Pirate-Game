using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Ajustes do ciclo de dia e noite (M11, Data/DayNight_Ciclo). O dia (do nascer ao pôr do sol) e a noite têm durações próprias em
    /// minutos reais. As cores do dia claro são as que a cena já tinha (céu, névoa, luz ambiente): o ciclo só mexe no amanhecer, no
    /// anoitecer e na noite. Ajuste fora do Play (é um asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Day Night Settings", fileName = "DayNight_")]
    public class DayNightSettings : ScriptableObject
    {
        [Header("Tempo")]
        [Tooltip("Minutos reais do nascer ao pôr do sol.")]
        [Min(0.5f)] public float dayMinutes = 14f;
        [Tooltip("Minutos reais do pôr ao nascer do sol.")]
        [Min(0.5f)] public float nightMinutes = 6f;
        [Range(0f, 24f)] public float sunrise = 6f;
        [Range(0f, 24f)] public float sunset = 19f;
        [Tooltip("Hora em que o jogo novo começa.")]
        [Range(0f, 24f)] public float startHour = 8f;
        [Tooltip("Horas de transição em volta do nascer e do pôr do sol (meia antes, meia depois).")]
        [Min(0.1f)] public float twilightHours = 1.6f;

        [Header("Sol")]
        [Tooltip("Altura do sol ao meio-dia (graus).")]
        [Range(10f, 89f)] public float sunMaxElevation = 55f;
        [Tooltip("Direção do sol no meio-dia (graus; a cena antiga tinha −35) e quanto ele anda para cada lado no dia.")]
        public float sunNoonAzimuth = -35f, sunAzimuthSwing = 70f;
        public float sunIntensity = 1.3f;
        [Tooltip("Cor do sol pela altura: esquerda = no horizonte, direita = no alto.")]
        public Gradient sunColor = DefaultSunColor();

        [Header("Lua (a mesma luz, de noite)")]
        [Range(10f, 89f)] public float moonMaxElevation = 50f;
        public float moonAzimuth = 145f;
        public float moonIntensity = 0.35f;
        public Color moonColor = new(0.55f, 0.65f, 0.95f);
        [Range(0f, 1f)] public float moonShadowStrength = 0.6f;

        [Header("Ambiente")]
        [Tooltip("Luz ambiente da noite fechada (o dia usa a do céu da cena).")]
        public Color nightAmbient = new(0.08f, 0.1f, 0.18f);
        [Tooltip("Tom da luz ambiente e da névoa no amanhecer e no anoitecer.")]
        public Color twilightTint = new(1f, 0.72f, 0.55f);
        public Color nightFog = new(0.05f, 0.07f, 0.12f);
        public Color twilightFog = new(0.86f, 0.6f, 0.46f);
        [Tooltip("Reflexo do céu na água e nas coisas lisas à noite (1 = o do dia). O céu do dia refletido no mar escuro brilhava.")]
        [Range(0f, 1f)] public float nightReflection = 0.08f;
        [Tooltip("Exposição do céu à noite (o céu da cena tem a dele de dia).")]
        [Min(0f)] public float nightSkyExposure = 0.08f;

        [Header("Lanternas")]
        [Tooltip("Noite (0–1) em que as lanternas começam a acender; cheias com a noite fechada.")]
        [Range(0f, 1f)] public float lanternsFrom = 0.25f;

        static Gradient DefaultSunColor()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.5f, 0.28f), 0f),
                new GradientColorKey(new Color(1f, 0.8f, 0.6f), 0.25f),
                new GradientColorKey(new Color(1f, 0.95f, 0.86f), 0.6f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
