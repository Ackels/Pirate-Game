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
        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");
        static readonly int StrafeBool = Animator.StringToHash("Strafe");
        static readonly int MotionSpeed = Animator.StringToHash("MotionSpeed");
        static readonly int Passo = Animator.StringToHash("Passo");
        static readonly int PassoX = Animator.StringToHash("PassoX");
        static readonly int PassoZ = Animator.StringToHash("PassoZ");
        static readonly int Cadencia = Animator.StringToHash("Cadencia");
        // Braços da corrida no ritmo das pernas (12/11): "CicloPerna" = fase do ciclo da locomoção (0–1) + "CicloPernaFase" (onde o pé
        // esquerdo vai à frente no clipe do tronco); o "CorrerComEspada" toca pelo tempo dele em vez do relógio próprio.
        static readonly int LegCycle = Animator.StringToHash("CicloPerna");
        static readonly int LocomotionState = Animator.StringToHash("Idle Walk Run Blend");
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
        static readonly int LeverState = Animator.StringToHash("PuxarAlavanca");
        /// <summary>"Pulling Lever" do Mixamo: a mão sai do quadril aos 0,8 s (o começo é parado).</summary>
        const float LeverClipStart = 0.8f;
        // Pesca (D2): clipes do Human Crafting Animations (Kevin Iglesias), do começo: lançar ("FishingThrow01"), esperar ("Fishing01 -
        // Loop"), brigar com o peixe ("FishingFighting01", em loop) e puxar ("FishingPullOut01", o puxão para cima e as mãos descendo).
        static readonly int FishCastState = Animator.StringToHash("PescarLancar");
        static readonly int FishFightState = Animator.StringToHash("PescarBriga");
        static readonly int FishReelState = Animator.StringToHash("PescarPuxar");
        static readonly int FishingBool = Animator.StringToHash("Fishing");
        // Coleta (D1): "MiningOneHand01_R - Wall" do Kevin Iglesias em loop (machadada de uma mão na altura da cintura).
        static readonly int GatherState = Animator.StringToHash("Coletar");
        static readonly int MineState = Animator.StringToHash("Minerar");
        static readonly int GatheringBool = Animator.StringToHash("Gathering");
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
        static readonly int GuardState = Animator.StringToHash("Defesa");
        static readonly int GuardHitState = Animator.StringToHash("DefesaImpacto");
        static readonly int ParryState = Animator.StringToHash("Aparar");
        static readonly int DrawState = Animator.StringToHash("Sacar");
        static readonly int HolsterState = Animator.StringToHash("Guardar");
        static readonly int DrawPistolState = Animator.StringToHash("SacarPistola");
        static readonly int DrawBackState = Animator.StringToHash("SacarCostas");
        static readonly int HolsterBackState = Animator.StringToHash("GuardarCostas");
        static bool IsChange(int h) => h == DrawState || h == HolsterState || h == DrawPistolState || h == DrawBackState || h == HolsterBackState;
        static readonly int EmptyState = Animator.StringToHash("Vazio");
        /// <summary>Segundo do corte para fora ("Stable Sword Outward Slash") em que o aparar começa (o mesmo ParrySwingStart do gerador).</summary>
        const float ParryClipStart = 0.72f;

        [SerializeField] FootCharacter character;
        [Tooltip("Suavização do Speed (s) para o blend tree não pular de um clipe para outro.")]
        [SerializeField] float speedDamp = 0.1f;
        [Tooltip("Segundos no ar antes de tocar a animação de queda (degraus pequenos não contam).")]
        [SerializeField] float freeFallDelay = 0.15f;
        [Tooltip("Mais rápido que o clipe mais rápido (a disparada), o ciclo das pernas acelera até este tanto (o pé continua no chão).")]
        [SerializeField] float maxCadence = 2f;

        [Header("Corpo inteiro")]
        [SerializeField] string upperLayer = "Tronco";
        [SerializeField] string fullBodyLayer = "CorpoInteiro";
        [Tooltip("Abaixo desta velocidade (m/s, no tamanho de um personagem comum) a ação passa a usar o corpo inteiro do clipe.")]
        [SerializeField] float fullBodyUntilSpeed = 0.3f;
        [Tooltip("Acima desta velocidade, só o tronco (as pernas andam). Entre as duas fica como estava (sem meio-termo: misturar a " +
                 "postura parada com as pernas andando fazia o pé escorregar).")]
        [SerializeField] float upperBodyFromSpeed = 0.7f;
        [Tooltip("Segundos para passar de um para o outro.")]
        [SerializeField] float fullBodyBlendTime = 0.12f;
        [Tooltip("Segundos para o corpo todo entrar nas ações em movimento (rolar, chutar): o mesmo tempo da transição delas.")]
        [SerializeField] float movingFullBodyBlend = 0.06f;

        Animator animator;
        CharacterCombat combat;
        readonly HashSet<int> parameters = new();
        float airTime;
        int upperIndex = -1, fullIndex = -1;
        float legPhase, legPhaseOffset;
        /// <summary>Ritmo próprio da pose parada (12/11): os piratas de um acampamento respiravam todos no mesmo compasso.</summary>
        float personalTempo = 1f;
        float fullWeight, fullWeightVelocity;
        bool stillPose = true;
        /// <summary>Escala do personagem (brutamontes 1,35, chefe 1,4): o passo da animação cresce junto, então o Speed vai dividido por ela.</summary>
        float strideScale = 1f;
        /// <summary>
        /// Passada que acompanha a velocidade (06/11): velocidade ÷ (escala do objeto × escala humana do avatar) = a velocidade na escala em que
        /// os clipes foram medidos; os blend trees têm os limites na velocidade natural de cada clipe, então o pé apoiado fica parado no chão.
        /// <see cref="passoMax"/> e <see cref="lateralMax"/> = o clipe mais rápido de cada um (valor padrão dos parâmetros "PassoMax" e
        /// "PassoLateralMax", posto pelo gerador); acima disso o ciclo acelera (<see cref="maxCadence"/>).
        /// </summary>
        float strideNorm = 1f, passoMax, lateralMax;
        bool dead;
        int hitVariants, deathVariants, lastHit = -1, lastDeath = -1;
        float knockedAt = float.NegativeInfinity, knockFall;

        /// <summary>O controller tem estados de levar golpe / de morrer (clipes do Mixamo presentes).</summary>
        public bool HasHitAnimation => hitVariants > 0;
        public bool HasDeathAnimation => deathVariants > 0;

        /// <summary>
        /// No meio de uma ação de corpo inteiro em movimento (rolar, chutar, arremessar, alavanca, cair, levantar, cambalear do aparo) ou na
        /// guarda: levar golpe não troca a animação (só o tranco do corpo), senão a pancada cortava o rolamento e a guarda no meio.
        /// </summary>
        public bool InAction
        {
            get
            {
                if (upperIndex < 0) return false;
                var state = TargetState();
                return state.tagHash == MovingFullBodyTag || state.shortNameHash == GuardState || state.shortNameHash == GuardHitState
                       || state.shortNameHash == ParryState;
            }
        }

        /// <summary>Numa ação de corpo inteiro em movimento (rolar, chutar, arremessar, alavanca, pesca, cair): sem a mão de apoio na arma.</summary>
        public bool InFullBodyMove => upperIndex >= 0 && TargetState().tagHash == MovingFullBodyTag;

        /// <summary>
        /// O tronco ainda está sacando, guardando ou sem a pose da arma (06/11): a troca de arma termina no combate antes de a animação de
        /// guardar acabar, e a mão de apoio ia buscar o cano do bacamarte com a mão direita ainda nas costas (o braço esquerdo virava 150° num
        /// quadro). A mão de apoio espera a pose da arma.
        /// </summary>
        /// <summary>O tronco está (ou está entrando) na guarda erguida ("Defesa", "DefesaImpacto"; o "Aparar" não conta).</summary>
        public bool InGuardPose
        {
            get
            {
                if (upperIndex < 0) return false;
                int target = TargetState().shortNameHash;
                return target == GuardState || target == GuardHitState;
            }
        }

        public bool InWeaponChange
        {
            get
            {
                if (upperIndex < 0) return false;
                int current = animator.GetCurrentAnimatorStateInfo(upperIndex).shortNameHash;
                int target = TargetState().shortNameHash;
                return IsChange(current) || IsChange(target) || target == EmptyState;
            }
        }

        AnimatorStateInfo TargetState() =>
            animator.IsInTransition(upperIndex) ? animator.GetNextAnimatorStateInfo(upperIndex) : animator.GetCurrentAnimatorStateInfo(upperIndex);

        public void Configure(FootCharacter footCharacter) => character = footCharacter;

        void Start()
        {
            // Variedade (12/11): quem não é o capitão começa a pose num ponto sorteado do ciclo e respira num ritmo um pouco diferente
            // (±10%); antes um acampamento inteiro parado mexia junto, como robôs.
            if (combat && combat.MeleeAssist)
            {
                // A cabeça do capitão é para onde NPCs e inimigos olham (HeadLook).
                Pirates.Core.HeadLook.Player = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : transform;
                return;
            }
            personalTempo = Random.Range(0.9f, 1.1f);
            if (animator && animator.isActiveAndEnabled && animator.runtimeAnimatorController)
                for (int l = 0; l < animator.layerCount; l++)
                    if (l == 0 || l == upperIndex) animator.Play(0, l, Random.value);
        }

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (!character) character = GetComponentInParent<FootCharacter>();
            if (character) combat = character.GetComponent<CharacterCombat>();
            if (character) strideScale = Mathf.Max(0.1f, character.transform.lossyScale.y);
            foreach (var p in animator.parameters)
            {
                parameters.Add(p.nameHash);
                if (p.name == "PassoMax") passoMax = p.defaultFloat;
                else if (p.name == "PassoLateralMax") lateralMax = p.defaultFloat;
                else if (p.name == "CicloPernaFase") legPhaseOffset = p.defaultFloat;
            }
            float human = animator.isHuman ? animator.humanScale : 1f;
            strideNorm = strideScale * Mathf.Max(0.1f, human);
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
            // O golpe que ia sair foi cancelado pela pancada: o trigger guardado não pode disparar o golpe depois.
            ResetTrigger(Attack);
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
            if (parameters.Contains(AttackVariant)) animator.SetInteger(AttackVariant, weapon.AnimatorVariantFor(variant));
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

        /// <summary>Puxar a alavanca da ruína (corpo todo, direto como o arremesso). Sem o clipe, nada acontece.</summary>
        public void PlayLever()
        {
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, LeverState)) return;
            ResetTrigger(Attack);
            animator.CrossFadeInFixedTime(LeverState, 0.15f, upperIndex, LeverClipStart);
        }

        /// <summary>Pesca: lança a linha (corpo todo) e fica esperando com a vara enquanto <see cref="SetFishing"/> estiver ligado.</summary>
        public void PlayFishCast()
        {
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, FishCastState)) return;
            ResetTrigger(Attack);
            SetFishing(true);
            animator.CrossFadeInFixedTime(FishCastState, 0.2f, upperIndex, 0f);
        }

        /// <summary>Pesca: briga com o peixe fisgado (segurando a vara aos trancos, em loop até puxar ou soltar).</summary>
        public void PlayFishFight()
        {
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, FishFightState)) return;
            animator.CrossFadeInFixedTime(FishFightState, 0.15f, upperIndex, 0f);
        }

        /// <summary>Pesca: puxa a linha para fora da água (peixe fisgado ou linha vazia) e volta à pose.</summary>
        public void PlayFishReel()
        {
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, FishReelState)) return;
            SetFishing(false);
            animator.CrossFadeInFixedTime(FishReelState, 0.12f, upperIndex, 0f);
        }

        /// <summary>Liga/desliga a espera com a vara (desligado, a espera volta à pose da arma).</summary>
        public void SetFishing(bool on)
        {
            if (parameters.Contains(FishingBool)) animator.SetBool(FishingBool, on);
        }

        /// <summary>
        /// Coleta: golpes de ferramenta em loop enquanto <see cref="SetGathering"/> estiver ligado: na parede (cortar árvore) ou, com
        /// <paramref name="ground"/>, no chão (minerar o veio no pé da pedra).
        /// </summary>
        public void PlayGather(bool ground = false)
        {
            int state = ground ? MineState : GatherState;
            if (dead || upperIndex < 0 || !animator.HasState(upperIndex, state)) return;
            ResetTrigger(Attack);
            SetGathering(true);
            animator.CrossFadeInFixedTime(state, 0.15f, upperIndex, 0f);
        }

        /// <summary>Liga/desliga os golpes de ferramenta (desligado, volta à pose da arma).</summary>
        public void SetGathering(bool on)
        {
            if (parameters.Contains(GatheringBool)) animator.SetBool(GatheringBool, on);
        }

        /// <summary>Golpe na guarda: o tranco da defesa (aparar também).</summary>
        void OnBlocked(Vector3 point, bool parried)
        {
            if (dead) return;
            // Aparou (revisão de 21/10): um golpe rápido para fora que desvia a lâmina (antes era o mesmo tranco da defesa e não parecia
            // aparar); volta à guarda se ela continua erguida.
            if (parried && upperIndex >= 0 && animator.HasState(upperIndex, ParryState))
            {
                ResetTrigger(BlockHit);
                animator.CrossFadeInFixedTime(ParryState, 0.05f, upperIndex, ParryClipStart);
                return;
            }
            SetTrigger(BlockHit);
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

            Vector3 velocity = character.AnimatedVelocity;
            if (parameters.Contains(Speed)) animator.SetFloat(Speed, velocity.magnitude / strideScale, speedDamp, dt);
            float cadence = 1f;
            if (parameters.Contains(Passo))
            {
                animator.SetFloat(Passo, velocity.magnitude / strideNorm, speedDamp, dt);
                float passo = animator.GetFloat(Passo);
                if (passoMax > 0.1f && passo > passoMax) cadence = Mathf.Min(maxCadence, passo / passoMax);
                // Parado, cada um no seu ritmo; andando volta ao 1 (a passada acompanha a velocidade).
                cadence *= Mathf.Lerp(personalTempo, 1f, Mathf.Clamp01(passo / 0.6f));
            }
            if (parameters.Contains(MotionSpeed)) animator.SetFloat(MotionSpeed, character.WalkingBackward ? -cadence : cadence);
            if (parameters.Contains(LegCycle))
            {
                // Fase das pernas: o tempo da locomoção (um passo de cada pé por ciclo em todos os clipes dela); fora dela (pulo, de lado)
                // segue no ritmo de uma corrida para não congelar.
                var legs = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                legPhase = legs.shortNameHash == LocomotionState ? legs.normalizedTime - Mathf.Floor(legs.normalizedTime) : Mathf.Repeat(legPhase + dt / 0.6f, 1f);
                animator.SetFloat(LegCycle, Mathf.Repeat(legPhase + legPhaseOffset, 1f));
            }
            // Andar de lado/de ré (estado "Lateral" da camada de base): velocidade no espaço do personagem.
            if (parameters.Contains(MoveX))
            {
                Vector3 local = character.transform.InverseTransformDirection(velocity) / strideScale;
                animator.SetFloat(MoveX, local.x, speedDamp, dt);
                animator.SetFloat(MoveZ, local.z, speedDamp, dt);
                if (parameters.Contains(PassoX))
                {
                    Vector3 stride = character.transform.InverseTransformDirection(velocity) / strideNorm;
                    animator.SetFloat(PassoX, stride.x, speedDamp, dt);
                    animator.SetFloat(PassoZ, stride.z, speedDamp, dt);
                    float side = new Vector2(animator.GetFloat(PassoX), animator.GetFloat(PassoZ)).magnitude;
                    if (parameters.Contains(Cadencia))
                        animator.SetFloat(Cadencia, lateralMax > 0.1f && side > lateralMax ? Mathf.Min(maxCadence, side / lateralMax) : 1f);
                }
                animator.SetBool(StrafeBool, character.IsStrafing && grounded && !dead);
            }
            if (parameters.Contains(Grounded)) animator.SetBool(Grounded, grounded || dead);
            if (parameters.Contains(Down)) animator.SetBool(Down, character.IsDown);
            if (parameters.Contains(FreeFall)) animator.SetBool(FreeFall, !dead && !grounded && airTime > freeFallDelay && character.VerticalVelocity < 0f);
            if (parameters.Contains(Jump) && grounded && character.VerticalVelocity <= 0f) animator.SetBool(Jump, false);
            if (combat)
            {
                if (parameters.Contains(Reloading)) animator.SetBool(Reloading, combat.ReloadAnimating);
                if (parameters.Contains(Aiming)) animator.SetBool(Aiming, combat.IsAimingPose);
                if (parameters.Contains(BlockBool)) animator.SetBool(BlockBool, combat.IsBlocking && !dead);
                RaiseGuard();
            }
            UpdateFullBody(grounded, dt);
        }

        /// <summary>
        /// Guarda erguida no jogo e a animação em outro estado (fim de um golpe, levando golpe, voltando de um chute): sobe a guarda direto.
        /// Pelo bool "Block" ela só entrava do pronto/correndo, então erguer a guarda logo depois de golpear não mostrava guarda nenhuma
        /// (medido: guarda aos 0,50 s do golpe aparecia aos 1,00 s), e o aparar ficava invisível.
        /// </summary>
        void RaiseGuard()
        {
            if (dead || !combat.IsBlocking || upperIndex < 0 || !animator.HasState(upperIndex, GuardState)) return;
            int target = TargetState().shortNameHash;
            if (target == GuardState || target == GuardHitState || target == ParryState) return;
            animator.CrossFadeInFixedTime(GuardState, 0.1f, upperIndex, 0f);
        }

        /// <summary>
        /// Corpo inteiro nos estados do tronco com tag "CorpoInteiro" ou "Mira", com o personagem parado e no chão;
        /// morto, sempre (a queda é do corpo todo).
        /// </summary>
        void UpdateFullBody(bool grounded, float dt)
        {
            if (fullIndex < 0 || upperIndex < 0) return;
            var state = TargetState();
            bool action = state.tagHash == FullBodyTag || state.tagHash == AimTag;
            // Parado ou andando, com folga entre os dois limites (sem tremer na fronteira).
            float speed = character.AnimatedVelocity.magnitude / strideScale;
            if (speed < fullBodyUntilSpeed) stillPose = true;
            else if (speed > upperBodyFromSpeed) stillPose = false;
            float target = dead && HasDeathAnimation ? 1f : state.tagHash == MovingFullBodyTag ? 1f : action && grounded && stillPose ? 1f : 0f;
            // Rolamento e chute: corpo todo junto com a transição (antes pulava de 0 a 1 num quadro e as pernas estalavam;
            // com a mistura lenta, os primeiros 0,1 s saíam meio corrida, meio rolamento).
            if (state.tagHash == MovingFullBodyTag && !dead)
            {
                fullWeight = Mathf.MoveTowards(fullWeight, 1f, dt / Mathf.Max(0.01f, movingFullBodyBlend));
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
