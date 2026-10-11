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
        static readonly int DodgeTrigger = Animator.StringToHash("Dodge");
        static readonly int KickTrigger = Animator.StringToHash("Kick");
        static readonly int ThrowState = Animator.StringToHash("Arremessar");
        /// <summary>Segundo do clipe "Throw Object" em que começa o preparo (o mesmo ThrowStart do gerador).</summary>
        const float ThrowClipStart = 1.3f;
        static readonly int Knock = Animator.StringToHash("Knock");
        static readonly int KnockVariant = Animator.StringToHash("KnockVariant");
        static readonly int Down = Animator.StringToHash("Down");
        static readonly int DeadBool = Animator.StringToHash("Dead");
        static readonly int BlockBool = Animator.StringToHash("Block");
        static readonly int BlockHit = Animator.StringToHash("BlockHit");
        static readonly int ParriedTrigger = Animator.StringToHash("Parried");
        static readonly int TauntTrigger = Animator.StringToHash("Taunt");
        /// <summary>
        /// Segundos do começo da queda até sair dela para o levantar, por variante (0 = chute, 1 = explosão, 2 = explosão com levantar rápido):
        /// morreu antes disso, a queda termina deitada e ele fica (iguais às saídas do gerador).
        /// </summary>
        static readonly float[] KnockFallSeconds = { 0.8f, 1.5f, 0.47f };
        static readonly int FullBodyTag = Animator.StringToHash("CorpoInteiro");
        static readonly int AimTag = Animator.StringToHash("Mira");
        /// <summary>Corpo inteiro mesmo andando (rolamento: o personagem se move durante a ação).</summary>
        static readonly int MovingFullBodyTag = Animator.StringToHash("CorpoInteiroAndando");

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
        float knockedAt = float.NegativeInfinity, knockFall;

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
            if (character)
            {
                character.Jumped += OnJumped;
                character.Dodged += OnDodged;
            }
            if (combat)
            {
                combat.Attacked += OnAttacked;
                combat.WeaponChanged += OnWeaponChanged;
                combat.Drawing += OnDrawing;
                combat.Holstering += OnHolstering;
                combat.Kicked += OnKicked;
                combat.Blocked += OnBlocked;
                combat.Parried += OnParried;
                if (combat.Current) OnWeaponChanged(combat.Current);
            }
        }

        void OnDisable()
        {
            if (character)
            {
                character.Jumped -= OnJumped;
                character.Dodged -= OnDodged;
            }
            if (combat)
            {
                combat.Attacked -= OnAttacked;
                combat.WeaponChanged -= OnWeaponChanged;
                combat.Drawing -= OnDrawing;
                combat.Holstering -= OnHolstering;
                combat.Kicked -= OnKicked;
                combat.Blocked -= OnBlocked;
                combat.Parried -= OnParried;
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
            if (parameters.Contains(DeadBool)) animator.SetBool(DeadBool, true);
            // Morreu no meio da queda (explosão que mata, golpe logo depois do chute): a queda termina deitada e ele não levanta
            // (o "Dead" segura a saída para o levantar). Já deitado ou levantando: "MortoNoChao". De pé: a morte normal.
            if (character && character.IsDown && Time.time - knockedAt < knockFall && parameters.Contains(DeadBool)) return;
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
            knockedAt = float.NegativeInfinity;
            if (parameters.Contains(DeadBool)) animator.SetBool(DeadBool, false);
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
            if (parameters.Contains(AttackVariant)) animator.SetInteger(AttackVariant, variant + weapon.animatorVariantOffset);
            if (parameters.Contains(AttackSpeed)) animator.SetFloat(AttackSpeed, weapon.attackAnimSpeed);
            SetTrigger(Attack);
        }

        void OnDrawing(WeaponDefinition weapon) => SetTrigger(Equip);

        void OnHolstering(WeaponDefinition weapon) => SetTrigger(Holster);

        /// <summary>Derrubado: cai (chute = cambaleia e senta; explosão = arremessado de costas) e levanta. Falso se o controller não tem os estados.</summary>
        public bool PlayKnockdown(int variant)
        {
            if (dead || !parameters.Contains(Knock)) return false;
            variant = Mathf.Clamp(variant, 0, KnockFallSeconds.Length - 1);
            knockedAt = Time.time;
            knockFall = KnockFallSeconds[variant];
            ResetTrigger(Hit);
            ResetTrigger(Attack);
            ResetTrigger(KickTrigger);
            if (parameters.Contains(KnockVariant)) animator.SetInteger(KnockVariant, variant);
            SetTrigger(Knock);
            return true;
        }

        /// <summary>Arremesso de bomba por cima do ombro (corpo todo). Sem o clipe, nada acontece.</summary>
        public void PlayThrow()
        {
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, ThrowState)) return;
            ResetTrigger(Attack);
            // Direto (não pelo trigger): no meio de outra transição o "Any State" espera ela acabar e o braço atrasava ~0,2 s da bomba.
            // Começa no preparo do clipe (1,3 s); a camada do corpo inteiro é sincronizada e acompanha.
            animator.CrossFadeInFixedTime(ThrowState, 0.12f, upperIndex, ThrowClipStart);
        }

        /// <summary>Golpe na guarda: o tranco da defesa (aparar também).</summary>
        void OnBlocked(Vector3 point, bool parried)
        {
            if (!dead) SetTrigger(BlockHit);
        }

        /// <summary>O golpe deste personagem foi aparado: cambaleia para trás.</summary>
        void OnParried()
        {
            if (dead) return;
            ResetTrigger(Attack);
            SetTrigger(ParriedTrigger);
        }

        /// <summary>
        /// Esqueleto amaldiçoado saindo da cova: "Erguer{variante}" (Zombie Stand Up) direto, já deitado (<paramref name="startSeconds"/> do
        /// clipe), corpo todo. Chamado logo depois do Revive: o trigger dele sairia do estado na hora.
        /// </summary>
        public void PlayRise(int variant, float startSeconds)
        {
            if (dead || upperIndex < 0) return;
            int hash = Animator.StringToHash(RiseStatePrefix + variant);
            if (!animator.HasState(upperIndex, hash)) return;
            ResetTrigger(Revive);
            ResetTrigger(Hit);
            ResetTrigger(Attack);
            animator.CrossFadeInFixedTime(hash, 0f, upperIndex, startSeconds);
            fullWeight = 1f;
            fullWeightVelocity = 0f;
            if (fullIndex >= 0) animator.SetLayerWeight(fullIndex, 1f);
        }

        /// <summary>Estados de sair da cova no tronco: "Erguer0", "Erguer1"...</summary>
        public const string RiseStatePrefix = "Erguer";

        static readonly int PowerUpState = Animator.StringToHash("Espadao_Furia");

        /// <summary>Chefe se enfurecendo (fase 2): "Power Up" do espadão, corpo todo, direto (sem esperar transição). Sem o estado, nada.</summary>
        public void PlayPowerUp()
        {
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, PowerUpState)) return;
            ResetTrigger(Attack);
            ResetTrigger(Hit);
            animator.CrossFadeInFixedTime(PowerUpState, 0.15f, upperIndex, 0f);
            fullWeight = 1f;
            fullWeightVelocity = 0f;
            if (fullIndex >= 0) animator.SetLayerWeight(fullIndex, 1f);
        }

        /// <summary>Provocação (grito de guerra do brutamontes ao ver o capitão). Sem o estado, nada acontece.</summary>
        public void PlayTaunt()
        {
            if (!dead) SetTrigger(TauntTrigger);
        }

        void OnKicked()
        {
            if (!dead) SetTrigger(KickTrigger);
        }

        void OnDodged()
        {
            if (dead) return;
            ResetTrigger(Attack);
            SetTrigger(DodgeTrigger);
        }

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
            if (parameters.Contains(Down)) animator.SetBool(Down, character.IsDown);
            if (parameters.Contains(FreeFall)) animator.SetBool(FreeFall, !dead && !grounded && airTime > freeFallDelay && character.VerticalVelocity < 0f);
            if (parameters.Contains(Jump) && grounded && character.VerticalVelocity <= 0f) animator.SetBool(Jump, false);
            if (combat)
            {
                if (parameters.Contains(Reloading)) animator.SetBool(Reloading, combat.ReloadAnimating);
                if (parameters.Contains(Aiming)) animator.SetBool(Aiming, combat.IsAimingPose);
                if (parameters.Contains(BlockBool)) animator.SetBool(BlockBool, combat.IsBlocking && !dead);
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
            float target = dead && HasDeathAnimation ? 1f : state.tagHash == MovingFullBodyTag ? 1f : action && grounded ? still : 0f;
            // Rolamento: corpo todo na hora (com a mistura, os primeiros 0,1 s saíam meio corrida, meio rolamento).
            if (state.tagHash == MovingFullBodyTag && !dead)
            {
                fullWeight = 1f;
                fullWeightVelocity = 0f;
            }
            else fullWeight = Mathf.SmoothDamp(fullWeight, target, ref fullWeightVelocity, fullBodyBlendTime, Mathf.Infinity, dt);
            animator.SetLayerWeight(fullIndex, fullWeight);
        }

        // Eventos de animação dos clipes do Starter Assets (passos e aterrissagem). Áudio entra no M6.
        void OnFootstep(AnimationEvent animationEvent) { }
        void OnLand(AnimationEvent animationEvent) { }
    }
}
