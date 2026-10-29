using UnityEngine;

namespace Pirates.OnFoot
{
    public enum WeaponKind { Melee, Firearm }

    /// <summary>Uma arma a pé (espada, pistola...). Ajuste de game design em Data/Weapons.</summary>
    [CreateAssetMenu(menuName = "Pirates/Weapon", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Arma";
        [Tooltip("Ícone na tela de equipamento (I), fotografado do modelo pelo gerador.")]
        public Texture2D icon;
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
        [Tooltip("Somado ao AttackVariant no Animator: golpes próprios com a mesma pose (adaga do duelista = cutelo com os golpes 2 e 3).")]
        [Min(0)] public int animatorVariantOffset;
        [Tooltip("AttackVariant do Animator de cada golpe (mesma ordem; vazio = golpe + offset). O 3º golpe do cutelo (finalização) é o 6.")]
        public int[] animatorVariants = System.Array.Empty<int>();

        /// <summary>AttackVariant do Animator para o golpe <paramref name="variant"/>.</summary>
        public int AnimatorVariantFor(int variant) =>
            animatorVariants != null && variant >= 0 && variant < animatorVariants.Length ? animatorVariants[variant] : variant + animatorVariantOffset;
        [Tooltip("Golpe sorteado (diferente do anterior) em vez da sequência do combo: a IA mistura golpes rápidos e lentos.")]
        public bool randomVariants;
        [Tooltip("Segundos até o impacto de cada golpe (mesma ordem dos golpes; vazio = hitDelay em todos). Golpe lento pega quem rola cedo.")]
        public float[] variantHitDelays = System.Array.Empty<float>();

        /// <summary>Segundos do começo do golpe <paramref name="variant"/> até o impacto.</summary>
        public float HitDelayFor(int variant) =>
            variantHitDelays != null && variant >= 0 && variant < variantHitDelays.Length && variantHitDelays[variant] > 0f ? variantHitDelays[variant] : hitDelay;
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
        [Tooltip("Mão de apoio (SupportHandIK): onde a mão esquerda segura, a partir da mão direita, em fração do comprimento da arma para a " +
                 "ponta mais longe (boca do cano, lâmina). 0 = sem IK.")]
        [Range(-0.5f, 0.8f)] public float supportOffset;

        [Tooltip("Mão de apoio que escorrega no cabo (machado de duas mãos): vai ao ponto do cabo mais perto de onde a animação põe a mão " +
                 "esquerda, entre o supportOffset e supportSlideGap da mão direita (o braço fica dobrado como na animação).")]
        public bool supportSlides;

        [Tooltip("Com supportSlides: distância mínima da mão direita, em fração do comprimento da arma.")]
        [Range(0f, 0.5f)] public float supportSlideGap = 0.12f;

        [Header("Movimento")]
        [Tooltip("Fração da velocidade enquanto ataca (1 = normal).")]
        [Range(0f, 1f)] public float moveSpeedWhileAttacking = 0.4f;

        [Header("Golpe pesado (brutamontes)")]
        [Tooltip("Golpe que acerta derruba (Stumble = cambaleia e cai sentado). Na guarda não derruba.")]
        public Pirates.Core.Knockdown knockdown;
        [Tooltip("Fôlego que o golpe tira de quem segura na guarda, × o custo normal (machado pesado cansa mais).")]
        public float guardCost = 1f;
        [Tooltip("Machado (E2): golpe que pega na guarda erguida quebra a guarda (passa parte do dano, quem defendia fica sem guarda um tempo). Aparar continua valendo.")]
        public bool breaksGuard;
        [Tooltip("Empurrão (m/s) em quem leva o golpe sem guarda (as lâminas do capitão; 0 = nenhum).")]
        public float hitPush;
        [Tooltip("Adaga (E2, passo 2): golpe em quem está de costas para quem bate (mais de 110° da frente dele) × isto (1 = nada a mais).")]
        public float backstabDamage = 1f;

        [Header("Lâmina sagrada (Lâmina de São Telmo)")]
        [Tooltip("Fere as almas das catacumbas mesmo transparentes, e o morto amaldiçoado que ela derruba não levanta de novo.")]
        public bool holy;

        [Header("Golpes especiais (chefe)")]
        [Tooltip("Por golpe (mesma ordem dos golpes; vazio = os valores da arma): alcance, abertura, dano, queda, imparável e acertos a mais.")]
        public StrikeOverride[] variantStrikes = System.Array.Empty<StrikeOverride>();

        /// <summary>Ajustes do golpe <paramref name="variant"/> (null = os da arma).</summary>
        public StrikeOverride StrikeFor(int variant) =>
            variantStrikes != null && variant >= 0 && variant < variantStrikes.Length ? variantStrikes[variant] : null;

        /// <summary>Segundos do começo do golpe até o último acerto (o primeiro + os acertos a mais).</summary>
        public float LastHitFor(int variant)
        {
            var strike = StrikeFor(variant);
            float last = HitDelayFor(variant);
            if (strike?.followUps != null)
                foreach (var t in strike.followUps) last = Mathf.Max(last, HitDelayFor(variant) + t);
            return last;
        }
    }

    /// <summary>
    /// Golpe com regras próprias dentro de uma arma (espadão do chefe): 0 = o valor da arma. Imparável = passa pela guarda (não dá para
    /// segurar nem aparar: só rolar ou sair do alcance). Acertos a mais = o mesmo golpe acerta de novo nesses segundos depois do primeiro.
    /// </summary>
    [System.Serializable]
    public class StrikeOverride
    {
        [Tooltip("Alcance (m); 0 = o da arma.")]
        public float range;
        [Tooltip("Abertura (graus); 360 = em volta. 0 = a da arma.")]
        public float angle;
        [Tooltip("Dano; 0 = o da arma.")]
        public float damage;
        [Tooltip("Troca a queda da arma pela deste golpe.")]
        public bool customKnockdown;
        public Pirates.Core.Knockdown knockdown;
        [Tooltip("Passa pela guarda (o chefe brilha em vermelho no preparo).")]
        public bool unblockable;
        [Tooltip("Segundos depois do primeiro acerto em que o golpe acerta de novo (sequências).")]
        public float[] followUps = System.Array.Empty<float>();
        [Tooltip("Investida deste golpe (m/s, 0 = a da IA): o salto vai muito mais longe que um passo.")]
        public float lungeSpeed;
        [Tooltip("Segundos a mais antes do próximo golpe (a finalização do combo deixa uma brecha).")]
        public float extraRecovery;
        [Tooltip("Empurrão (m/s) deste golpe; 0 = o da arma.")]
        public float push;
    }
}
