using UnityEngine;

namespace Pirates.OnFoot
{
    public enum WeaponKind { Melee, Firearm }

    /// <summary>Uma arma a pé (espada, pistola...). Ajuste de game design em Data/Weapons.</summary>
    [CreateAssetMenu(menuName = "Pirates/Weapon", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Arma";
        public WeaponKind kind = WeaponKind.Melee;
        [Tooltip("Índice da arma no Animator (parâmetro WeaponType): 0 = espada, 1 = pistola, 2 = bacamarte.")]
        public int animatorWeaponType;

        [Header("Dano")]
        public float damage = 25f;
        [Tooltip("Alcance (m): golpe à frente (espada) ou tiro (pistola).")]
        public float range = 2f;
        [Tooltip("Espada: abertura do golpe (graus à frente). Pistola: dispersão (graus).")]
        public float angle = 100f;

        [Header("Tempo")]
        [Tooltip("Segundos do clique até o golpe acertar / a bala sair (casa com a animação).")]
        public float hitDelay = 0.25f;
        [Tooltip("Velocidade da animação de ataque (parâmetro AttackSpeed). Menor = golpe mais lento e visível: piratas " +
                 "inimigos usam ~0,7 para dar tempo de reagir. Mudou aqui, divida o hitDelay pelo mesmo tanto.")]
        public float attackAnimSpeed = 1f;
        [Tooltip("Segundos até poder atacar de novo.")]
        public float cooldown = 0.7f;
        [Tooltip("Pistola: tiros antes de recarregar (pederneira = 1). 0 = não recarrega (espada).")]
        [Min(0)] public int shotsPerReload;
        [Tooltip("Pistola: segundos de recarga.")]
        public float reloadTime = 2.5f;

        [Header("Combo")]
        [Tooltip("Golpes diferentes em sequência (parâmetro AttackVariant). 1 = sempre o mesmo.")]
        [Min(1)] public int attackVariants = 1;
        [Tooltip("Segundos depois de um ataque em que o próximo continua o combo (senão volta ao 1º golpe).")]
        public float comboWindow = 1f;

        [Header("Sacar / guardar")]
        [Tooltip("Tem animação de sacar e guardar (espada). Sem ela a troca é instantânea (pistola no cinto).")]
        public bool animatedDraw;
        [Tooltip("Segundos do início do saque até a mão pegar a arma na bainha.")]
        public float drawGrabTime = 0.45f;
        [Tooltip("Duração do saque até poder atacar.")]
        public float drawDuration = 0.95f;
        [Tooltip("Segundos do início do guardar até a arma entrar na bainha.")]
        public float holsterReleaseTime = 0.85f;
        [Tooltip("Duração do guardar.")]
        public float holsterDuration = 1f;

        [Header("Pistola")]
        [Tooltip("Segundos de pose de mira depois do tiro (a recarga também mantém a pose).")]
        public float aimPoseSeconds = 1.5f;
        [Tooltip("Mirando (botão direito): segundos do clique até a bala sair. O braço já está erguido.")]
        public float aimedShotDelay = 0.04f;
        [Tooltip("Mirando: fração da dispersão (1 = igual ao tiro sem mirar).")]
        [Range(0f, 1f)] public float aimedSpread = 0.3f;
        [Tooltip("Mirando: fração da velocidade de andar (não corre).")]
        [Range(0f, 1f)] public float aimMoveSpeed = 0.75f;
        [Tooltip("Coice: graus que o braço sobe no tiro.")]
        public float recoilDegrees = 14f;
        [Tooltip("Projéteis por tiro (bacamarte: chumbo em leque). 1 = bala única. O dano é por projétil.")]
        [Min(1)] public int pellets = 1;
        [Tooltip("Abertura do leque de chumbo (graus, de ponta a ponta), além da dispersão normal.")]
        public float pelletSpread;
        [Tooltip("Arma de duas mãos (bacamarte): a mira gira o tronco inteiro com a arma, não só o braço.")]
        public bool twoHanded;

        [Header("Movimento")]
        [Tooltip("Fração da velocidade enquanto ataca (1 = normal).")]
        [Range(0f, 1f)] public float moveSpeedWhileAttacking = 0.4f;
    }
}
