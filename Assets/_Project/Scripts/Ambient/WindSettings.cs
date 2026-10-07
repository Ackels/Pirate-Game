using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>Ajustes do vento (M11, clima). Não mude em Play (fica salvo no asset).</summary>
    [CreateAssetMenu(menuName = "Pirates/Ambient/Wind Settings", fileName = "Vento")]
    public class WindSettings : ScriptableObject
    {
        [Header("Direção e força")]
        [Tooltip("Vento que mais sopra: para onde ele vai, em graus (0 = norte/+Z, 90 = leste/+X).")]
        public float prevailingDegrees = 70f;
        [Tooltip("Quanto a direção pode virar para cada lado do vento dominante (graus).")]
        [Range(0f, 180f)] public float directionSwing = 110f;
        [Tooltip("Horas de jogo para o vento mudar de cara (o dia todo tem 24 h, ~20 min reais).")]
        [Min(0.5f)] public float changeHours = 6f;
        [Range(0f, 1f)] public float minStrength = 0.25f;
        [Range(0f, 1f)] public float maxStrength = 1f;

        [Header("Navios")]
        [Tooltip("Velocidade × isto pelo ângulo entre a proa e o vento (0° = vento por trás, 90° = de través, 180° = contra). " +
                 "Vento bem por trás é o melhor (pedido do Jhonata, 07/10: alinhado com a seta do HUD tem de dar o bônus).")]
        public AnimationCurve pointOfSail = DefaultPointOfSail();

        public static AnimationCurve DefaultPointOfSail() => new(
            new Keyframe(0f, 1.12f), new Keyframe(45f, 1.1f), new Keyframe(90f, 1.04f), new Keyframe(135f, 0.84f), new Keyframe(180f, 0.7f));
        [Tooltip("Com o vento mais fraco, quanto da diferença acima vale (1 = toda).")]
        [Range(0f, 1f)] public float calmInfluence = 0.45f;
    }
}
