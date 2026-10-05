using System.Collections.Generic;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Passa o estado do FootCharacter para o Animator do modelo (fica no mesmo objeto do Animator).
    /// Nomes dos parâmetros iguais aos do Starter Assets da Unity: Speed (m/s, blend tree parado/andar/correr),
    /// MotionSpeed, Grounded, Jump, FreeFall; e do combate (M4.3), na camada do tronco: WeaponType (0 espada, 1 pistola),
    /// Attack (trigger) + AttackVariant (golpe do combo) + AttackSpeed, Equip/Holster (triggers de sacar/guardar),
    /// Reloading e Aiming; e da vida (M4.4): Hit + HitVariant, Die + DeathVariant (variação sorteada, sem repetir a
    /// anterior), Revive. Parâmetros que o controller não tiver são ignorados.
    /// Controla o peso da camada "CorpoInteiro" (cópia sincronizada do tronco, sem máscara): parado, golpes, saque e
    /// pistola usam o corpo todo do clipe (quadril e pernas na postura de luta); andando, só o tronco; morto, sempre o
    /// corpo todo. MotionSpeed = -1 anda de ré (mirando de costas para onde anda).
    /// Também recebe os eventos de animação dos clipes (OnFootstep, OnLand) para não gerar erro sem receptor.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int MotionSpeed = Animator.StringToHash("MotionSpeed");
        static readonly int Grounded = Animator.StringToHash("Grounded");
        static readonly int Jump = Animator.StringToHash("Jump");
        static readonly int FreeFall = Animator.StringToHash("FreeFall");
        static readonly int Attack = Animator.StringToHash("Attack");
        static readonly int WeaponType = Animator.StringToHash("WeaponType");
        static readonly int AttackVariant = Animator.StringToHash("AttackVariant");
        static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
        static readonly int Equip = Animator.StringToHash("Equip");
        static readonly int Holster = Animator.StringToHash("Holster");
        static readonly int Reloading = Animator.StringToHash("Reloading");
        static readonly int Aiming = Animator.StringToHash("Aiming");
        static readonly int Hit = Animator.StringToHash("Hit");
        static readonly int Die = Animator.StringToHash("Die");
        static readonly int Revive = Animator.StringToHash("Revive");
        static readonly int HitVariant = Animator.StringToHash("HitVariant");
        static readonly int DeathVariant = Animator.StringToHash("DeathVariant");
        static readonly int Eat = Animator.StringToHash("Eat");
        static readonly int FullBodyTag = Animator.StringToHash("CorpoInteiro");
        static readonly int AimTag = Animator.StringToHash("Mira");

        [SerializeField] FootCharacter character;
        [Tooltip("Suavização do Speed (s) para o blend tree não pular de um clipe para outro.")]
        [SerializeField] float speedDamp = 0.1f;
        [Tooltip("Segundos no ar antes de tocar a animação de queda (degraus pequenos não contam).")]
        [SerializeField] float freeFallDelay = 0.15f;

        [Header("Corpo inteiro")]
        [SerializeField] string upperLayer = "Tronco";
        [SerializeField] string fullBodyLayer = "CorpoInteiro";
        [Tooltip("Até esta velocidade (m/s) a ação usa o corpo inteiro do clipe.")]
        [SerializeField] float fullBodyUntilSpeed = 0.6f;
        [Tooltip("Desta velocidade para cima, só o tronco (as pernas andam).")]
        [SerializeField] float upperBodyFromSpeed = 1.5f;
        [Tooltip("Segundos para passar de um para o outro.")]
        [SerializeField] float fullBodyBlendTime = 0.12f;

        Animator animator;
        CharacterCombat combat;
        readonly HashSet<int> parameters = new();
        float airTime;
        int upperIndex = -1, fullIndex = -1;
        float fullWeight, fullWeightVelocity;
        bool dead;
        int hitVariants, deathVariants, lastHit = -1, lastDeath = -1;

        /// <summary>O controller tem estados de levar golpe / de morrer (clipes do Mixamo presentes).</summary>
        public bool HasHitAnimation => hitVariants > 0;
        public bool HasDeathAnimation => deathVariants > 0;

        public void Configure(FootCharacter footCharacter) => character = footCharacter;

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (!character) character = GetComponentInParent<FootCharacter>();
            if (character) combat = character.GetComponent<CharacterCombat>();
            foreach (var p in animator.parameters) parameters.Add(p.nameHash);
            upperIndex = animator.GetLayerIndex(upperLayer);
            fullIndex = animator.GetLayerIndex(fullBodyLayer);
            hitVariants = CountStates("Golpeado_");
            deathVariants = CountStates("Morte_");
        }

        /// <summary>Quantos estados "prefixo0", "prefixo1"... o tronco tem.</summary>
        int CountStates(string prefix)
        {
            if (upperIndex < 0) return 0;
            int n = 0;
            while (animator.HasState(upperIndex, Animator.StringToHash(prefix + n))) n++;
            return n;
        }

        /// <summary>Sorteia uma variação diferente da anterior (com uma só, sempre a mesma).</summary>
        static int Pick(int count, ref int last)
        {
            int pick;
            if (count <= 1) pick = 0;
            else if (last < 0) pick = Random.Range(0, count);
            else
            {
                pick = Random.Range(0, count - 1);
                if (pick >= last) pick++;
            }
            last = pick;
            return pick;
        }

        void OnEnable()
        {
            if (character) character.Jumped += OnJumped;
            if (combat)
            {
                combat.Attacked += OnAttacked;
                combat.WeaponChanged += OnWeaponChanged;
                combat.Drawing += OnDrawing;
                combat.Holstering += OnHolstering;
                if (combat.Current) OnWeaponChanged(combat.Current);
            }
        }

        void OnDisable()
        {
            if (character) character.Jumped -= OnJumped;
            if (combat)
            {
                combat.Attacked -= OnAttacked;
                combat.WeaponChanged -= OnWeaponChanged;
                combat.Drawing -= OnDrawing;
                combat.Holstering -= OnHolstering;
            }
        }

        /// <summary>Levou um golpe (sem morrer): toca a reação, se houver.</summary>
        public void PlayHit()
        {
            if (dead || !HasHitAnimation) return;
            if (parameters.Contains(HitVariant)) animator.SetInteger(HitVariant, Pick(hitVariants, ref lastHit));
            SetTrigger(Hit);
        }

        /// <summary>Morreu: toca a queda (corpo inteiro) e para de reagir.</summary>
        public void PlayDeath()
        {
            dead = true;
            ResetTrigger(Hit);
            ResetTrigger(Attack);
            if (parameters.Contains(DeathVariant)) animator.SetInteger(DeathVariant, Pick(deathVariants, ref lastDeath));
            SetTrigger(Die);
        }

        /// <summary>Comeu uma ração: gole na garrafa (camada "Gole": braço direito e cabeça). Sem o clipe, nada acontece.</summary>
        public void PlayEat()
        {
            if (!dead) SetTrigger(Eat);
        }

        /// <summary>Levantou (reapareceu): volta ao normal.</summary>
        public void PlayRevive()
        {
            dead = false;
            ResetTrigger(Die);
            SetTrigger(Revive);
            if (combat && combat.Current) OnWeaponChanged(combat.Current);
        }

        void SetTrigger(int hash)
        {
            if (parameters.Contains(hash)) animator.SetTrigger(hash);
        }

        void ResetTrigger(int hash)
        {
            if (parameters.Contains(hash)) animator.ResetTrigger(hash);
        }

        void OnWeaponChanged(WeaponDefinition weapon)
        {
            // -1 = mãos vazias (no meio de uma troca).
            if (parameters.Contains(WeaponType)) animator.SetInteger(WeaponType, weapon ? weapon.animatorWeaponType : -1);
        }

        void OnAttacked(WeaponDefinition weapon, int variant)
        {
            if (parameters.Contains(WeaponType)) animator.SetInteger(WeaponType, weapon.animatorWeaponType);
            if (parameters.Contains(AttackVariant)) animator.SetInteger(AttackVariant, variant);
            if (parameters.Contains(AttackSpeed)) animator.SetFloat(AttackSpeed, weapon.attackAnimSpeed);
            SetTrigger(Attack);
        }

        void OnDrawing(WeaponDefinition weapon) => SetTrigger(Equip);

        void OnHolstering(WeaponDefinition weapon) => SetTrigger(Holster);

        void OnJumped()
        {
            if (parameters.Contains(Jump)) animator.SetBool(Jump, true);
        }

        void Update()
        {
            if (!character || !animator.runtimeAnimatorController) return;
            float dt = Time.deltaTime;
            bool grounded = character.IsGrounded;
            airTime = grounded ? 0f : airTime + dt;

            if (parameters.Contains(Speed)) animator.SetFloat(Speed, character.PlanarSpeed, speedDamp, dt);
            if (parameters.Contains(MotionSpeed)) animator.SetFloat(MotionSpeed, character.WalkingBackward ? -1f : 1f);
            if (parameters.Contains(Grounded)) animator.SetBool(Grounded, grounded || dead);
            if (parameters.Contains(FreeFall)) animator.SetBool(FreeFall, !dead && !grounded && airTime > freeFallDelay && character.VerticalVelocity < 0f);
            if (parameters.Contains(Jump) && grounded && character.VerticalVelocity <= 0f) animator.SetBool(Jump, false);
            if (combat)
            {
                if (parameters.Contains(Reloading)) animator.SetBool(Reloading, combat.ReloadAnimating);
                if (parameters.Contains(Aiming)) animator.SetBool(Aiming, combat.IsAimingPose);
            }
            UpdateFullBody(grounded, dt);
        }

        /// <summary>
        /// Corpo inteiro nos estados do tronco com tag "CorpoInteiro" ou "Mira", com o personagem parado e no chão;
        /// morto, sempre (a queda é do corpo todo).
        /// </summary>
        void UpdateFullBody(bool grounded, float dt)
        {
            if (fullIndex < 0 || upperIndex < 0) return;
            var state = animator.IsInTransition(upperIndex) ? animator.GetNextAnimatorStateInfo(upperIndex) : animator.GetCurrentAnimatorStateInfo(upperIndex);
            bool action = state.tagHash == FullBodyTag || state.tagHash == AimTag;
            float still = 1f - Mathf.InverseLerp(fullBodyUntilSpeed, upperBodyFromSpeed, character.PlanarSpeed);
            float target = dead && HasDeathAnimation ? 1f : action && grounded ? still : 0f;
            fullWeight = Mathf.SmoothDamp(fullWeight, target, ref fullWeightVelocity, fullBodyBlendTime, Mathf.Infinity, dt);
            animator.SetLayerWeight(fullIndex, fullWeight);
        }

        // Eventos de animação dos clipes do Starter Assets (passos e aterrissagem). Áudio entra no M6.
        void OnFootstep(AnimationEvent animationEvent) { }
        void OnLand(AnimationEvent animationEvent) { }
    }
}
