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

        [Header("Céu fechado (clima: nublado, chuva, tempestade)")]
        [Tooltip("Sol × isto com o céu todo fechado (a lua também).")]
        [Range(0f, 1f)] public float overcastSun = 0.2f;
        [Tooltip("Luz ambiente × isto com o céu todo fechado.")]
        [Range(0f, 1f)] public float overcastAmbient = 0.5f;
        [Tooltip("Tom da luz (sol e ambiente) com o céu todo fechado: cinza-azulado de chuva.")]
        public Color overcastTint = new(0.78f, 0.84f, 0.95f);
        [Tooltip("Sombras com o céu todo fechado (força).")]
        [Range(0f, 1f)] public float overcastShadow = 0.25f;
        [Tooltip("Cor da névoa com o céu fechado de dia (de noite continua a da noite).")]
        public Color overcastFog = new(0.42f, 0.47f, 0.52f);
        [Tooltip("Névoa × isto com o céu todo fechado (mais densa: enxerga menos longe).")]
        [Min(1f)] public float overcastFogDensity = 2.4f;
        [Tooltip("Exposição do céu × isto com o céu todo fechado.")]
        [Range(0f, 1f)] public float overcastSky = 0.45f;
        [Tooltip("Reflexo do céu na água × isto com o céu todo fechado.")]
        [Range(0f, 1f)] public float overcastReflection = 0.5f;
        [Tooltip("Lanternas acesas (0–1) com a tempestade cheia, mesmo de dia.")]
        [Range(0f, 1f)] public float stormLanterns = 0.7f;

        [Header("Zona coberta (catacumbas, cavernas)")]
        [Tooltip("Debaixo da terra a hora não aparece: a luz do sol vira uma luz fraca e fria de cima (as frestas), com sombra, e o fundo fica preto.")]
        public float coveredLightIntensity = 0.42f;
        public Color coveredLightColor = new(0.62f, 0.72f, 0.9f);
        [Tooltip("Giro da luz de cima (graus: inclinação, direção).")]
        public Vector2 coveredLightAngles = new(72f, 35f);
        [Range(0f, 1f)] public float coveredShadow = 0.8f;
        [Tooltip("Luz ambiente debaixo da terra (bem escura: quem ilumina são as tochas).")]
        public Color coveredAmbient = new(0.14f, 0.14f, 0.17f);

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
