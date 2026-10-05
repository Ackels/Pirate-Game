using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>Stats de uma bateria de canhões. Upgrades de canhão (M3) modificam estes valores.</summary>
    [CreateAssetMenu(menuName = "Pirates/Combat/Cannon Stats", fileName = "CannonStats")]
    public class CannonStats : ScriptableObject
    {
        [Header("Bateria")]
        [Min(1)] public int cannonsPerSide = 4;
        [Tooltip("Segundos entre salvas.")]
        public float reloadTime = 4f;
        [Tooltip("Atraso máximo aleatório entre os canhões de uma salva.")]
        public float maxFireDelay = 0.18f;

        [Header("Mira")]
        [Tooltip("Segundos segurando o botão para ir do alcance mínimo ao máximo.")]
        public float aimChargeTime = 2f;
        [Tooltip("Alcance (m) ao começar a mirar.")]
        public float minRange = 20f;
        [Tooltip("Alcance (m) com a mira carregada. Limitado pela elevação máxima.")]
        public float maxRange = 120f;
        [Tooltip("Carga (0-1) → fração do alcance. Começa devagar para dar controle fino em alvos próximos.")]
        public AnimationCurve aimRangeCurve = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        [Tooltip("Limites físicos do canhão (graus). A elevação é calculada a partir do alcance desejado.")]
        public float minElevation = -3f;
        public float maxElevation = 12f;
        [Tooltip("Dispersão aleatória em graus (horizontal e vertical).")]
        public float spreadDegrees = 1.2f;
        [Tooltip("Mira assistida (0-1, upgrade de luneta): a distância gruda no inimigo, a carga desacelera perto dele " +
                 "e os canhões giram um pouco na direção dele. 0 = sem ajuda (inimigos e navio sem upgrade).")]
        [Range(0f, 1f)] public float aimAssist;
        [Tooltip("Quanto os canhões giram (graus) para o inimigo com mira assistida total.")]
        public float maxAssistTraverse = 12f;

        [Header("Projétil")]
        public float muzzleVelocity = 70f;
        public float damagePerBall = 12f;
        [Tooltip("Massa da bala (kg). Define o empurrão no impacto e o coice no navio.")]
        public float ballMass = 6f;
        public float ballLifetime = 8f;
    }
}
