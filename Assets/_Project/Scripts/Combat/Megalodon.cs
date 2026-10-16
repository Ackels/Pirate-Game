using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    /// <summary>
    /// Megalodonte (chefe do mar, cena 07; F.1 passo 2b, pedido do Jhonata em 16/10: o tubarão do kit Quaternius "bem maior, grande
    /// mesmo", com cara de megalodonte). Espera no fundo da Fossa, a nordeste. Navio perto: sobe e circula o navio com o dorso e a barbatana
    /// fora d'água; de tempos em tempos para, abre a bocarra (aviso: bolhas e espuma na frente) e investe contra o costado (dano e tranco
    /// no casco), depois mergulha e volta a circular. Com metade da vida fica furioso: mais rápido, investe mais e às vezes salta para fora
    /// d'água ao lado do navio (aviso: a água estoura onde ele vai cair). Só leva dano perto da superfície (no fundo é invencível). Longe do
    /// covil, desiste (cura e volta). Morto: boia de barriga para cima e afunda; publica <see cref="EnemyDefeatedEvent"/> e o saque
    /// (LootDropper) sai onde estava; volta depois de <see cref="respawnSeconds"/> com o jogador longe (ou na hora, com a missão aceita).
    /// Move-se por código (sem Rigidbody): o corpo atravessa o casco (as balas acertam o colisor), o tranco é aplicado direto no navio.
    /// </summary>
    public class Megalodon : MonoBehaviour
    {
        public enum Phase { Lurking, Rising, Circle, WindUp, Charge, Dive, Breach, Retreat, Dead }

        static readonly List<Megalodon> active = new();
        public static IReadOnlyList<Megalodon> Active => active;
        public static event Action<Megalodon> Spotted, Enraged, Defeated;

        [Header("Quem é")]
        [SerializeField] string displayName = "Megalodonte";
        [SerializeField] string title = "o rei das profundezas";
        [Tooltip("Id nas missões (EnemyDefeatedEvent).")]
        [SerializeField] string targetId = "megalodonte";
        [SerializeField, Min(0)] int fame = 120;

        [Header("Peças")]
        [Tooltip("Navio do jogador (o alvo).")]
        [SerializeField] Transform target;
        [Tooltip("Modelo animado (filho, em escala).")]
        [SerializeField] Transform model;
        [SerializeField] Animator animator;
        [SerializeField] Transform headBone, jawBone;
        [SerializeField] AimTarget aim;

        [Header("Covil")]
        [Tooltip("Navio a menos disto (m) do covil é atacado.")]
        [SerializeField] float detectRadius = 75f;
        [Tooltip("Navio mais longe que isto do covil: ele desiste (cura e volta).")]
        [SerializeField] float leash = 230f;
        [Tooltip("Segundos (com o jogador a mais de 300 m) até voltar depois de morto.")]
        [SerializeField] float respawnSeconds = 1800f;

        [Header("Nado")]
        [Tooltip("Raio da volta em torno do navio. 40 m e 7,5/10 m/s = ~11/14°/s em volta do navio, abaixo do giro dos navios do jogador " +
                 "(com 34 m e 9/12 ele dava a volta mais rápido do que o navio virava e não dava para manter o través nele).")]
        [SerializeField] float circleRadius = 40f;
        [Tooltip("Velocidade circulando: normal / furioso (m/s).")]
        [SerializeField] Vector2 circleSpeed = new(7.5f, 10f);
        [Tooltip("Altura do centro do corpo em relação à água: circulando (dorso e barbatana de fora) e no fundo.")]
        [SerializeField] float cruiseDepth = -2.4f, deepDepth = -16f;
        [Tooltip("Segundos subindo do fundo.")]
        [SerializeField] float riseSeconds = 2.5f;
        [Tooltip("Giro (graus/s) nadando e investindo.")]
        [SerializeField] float turnRate = 55f, chargeTurnRate = 110f;

        [Header("Investida")]
        [Tooltip("Segundos circulando entre investidas: normal (mín, máx) / furioso.")]
        [SerializeField] Vector2 chargeEvery = new(7f, 11f), chargeEveryTwo = new(4.5f, 7f);
        [Tooltip("Aviso (s) antes de investir: normal / furioso.")]
        [SerializeField] float windUpSeconds = 1.7f, windUpTwo = 1.2f;
        [SerializeField] float chargeSpeed = 24f;
        [Tooltip("Quanto ele sobe (m) investindo, em relação ao nado: a cabeça sai da água.")]
        [SerializeField] float chargeRise = 1.8f;
        [Tooltip("Dano no casco da investida: normal / furioso.")]
        [SerializeField] float ramDamage = 30f, ramDamageTwo = 40f;
        [Tooltip("Tranco da investida no navio: empurrão de lado (m/s) e giro (rad/s) para adernar e virar.")]
        [SerializeField] float ramPush = 2.5f, ramRoll = 0.45f, ramYaw = 0.25f;
        [Tooltip("Investida que não acerta em tantos segundos: desiste e mergulha.")]
        [SerializeField] float chargeTimeout = 3.2f;
        [SerializeField] float diveSeconds = 3f;

        [Header("Salto (furioso)")]
        [SerializeField, Range(0f, 1f)] float breachChance = 0.4f;
        [Tooltip("Aviso (s): a água estoura onde ele vai cair.")]
        [SerializeField] float breachWarning = 1.2f;
        [SerializeField] float breachSeconds = 2.4f, breachHeight = 16f;
        [SerializeField] float breachDamage = 45f, breachRoll = 0.4f;

        [Header("Fase 2")]
        [SerializeField, Range(0.1f, 0.9f)] float phaseTwoAt = 0.5f;

        [Header("Cara de megalodonte")]
        [Tooltip("Escala da cabeça e da mandíbula (ossos do kit), aplicada depois da animação.")]
        [SerializeField] float headScale = 1.25f, jawScale = 1.35f;
        [Tooltip("Mandíbula aberta (graus no X local do osso): nadando e mordendo/investindo.")]
        [SerializeField] float jawOpen = -14f, jawBite = -32f;

        Health health;
        Health targetHealth;
        Rigidbody targetBody;
        BoxCollider targetBox;
        IOceanSampler ocean;
        CombatEffects effects;
        Collider[] bodyColliders = Array.Empty<Collider>();
        Collider[] shipColliders;
        Vector3 lair, heading = Vector3.forward, breachFrom, breachTo, lastSeenTarget;
        float depth, phaseStart, nextCharge, deadSince, nextBubble, jawAngle, circleDir = 1f;
        bool phaseTwo, breachWarned, rammed;

        static readonly int SwimState = Animator.StringToHash("Nadar"), FastState = Animator.StringToHash("NadarRapido"),
            BiteState = Animator.StringToHash("Morder");
        int shownState;

        public string DisplayName => displayName;
        public string Title => title;
        public Health Health => health;
        public Phase Current { get; private set; } = Phase.Lurking;
        public bool PhaseTwo => phaseTwo;
        public Vector3 Lair => lair;
        public bool IsEngaged => Current is Phase.Rising or Phase.Circle or Phase.WindUp or Phase.Charge or Phase.Dive or Phase.Breach &&
                                 !health.IsDead;
        /// <summary>Perto da superfície (dá para acertar e mirar).</summary>
        public bool Exposed => Current is Phase.Circle or Phase.WindUp or Phase.Charge or Phase.Breach;

        public void Configure(Transform playerShip, Transform visual, Animator anim, Transform head, Transform jaw, AimTarget aimTarget)
        {
            target = playerShip;
            model = visual;
            animator = anim;
            headBone = head;
            jawBone = jaw;
            aim = aimTarget;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            lair = transform.position;
            bodyColliders = GetComponentsInChildren<Collider>(true);
            if (target)
            {
                targetHealth = target.GetComponent<Health>();
                targetBody = target.GetComponent<Rigidbody>();
                targetBox = target.GetComponent<BoxCollider>();
                shipColliders = target.GetComponentsInChildren<Collider>(true);
            }
        }

        void OnEnable()
        {
            active.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void OnDisable()
        {
            active.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            Spotted = null;
            Enraged = null;
            Defeated = null;
        }

        void Start()
        {
            Services.TryGet(out ocean);
            Services.TryGet(out effects);
            // O corpo atravessa o casco (as balas acertam por raio); o tranco da investida é aplicado direto no navio.
            if (shipColliders != null)
                foreach (var c in bodyColliders)
                foreach (var s in shipColliders)
                    if (c && s) Physics.IgnoreCollision(c, s, true);
            ResetToLair();
        }

        /// <summary>Missão contra ele aceita com ele morto: volta ao covil sem esperar o tempo (assim que o jogador estiver longe de lá).</summary>
        void OnMissionStarted(MissionStartedEvent evt)
        {
            if (evt.targetId == targetId && Current == Phase.Dead) deadSince = float.NegativeInfinity;
        }

        // ---------- Sandbox (F1) ----------

        /// <summary>Muda o covil para onde o navio está e sobe agora.</summary>
        public void SummonNear()
        {
            if (!target || health.IsDead) return;
            lair = Flat(target.position) + Vector3.up * transform.position.y;
            ResetToLair();
            transform.position = Flat(target.position) + Flat(Random.insideUnitSphere).normalized * circleRadius + Vector3.up * deepDepth;
            BeginRise();
        }

        public void EnrageNow()
        {
            if (!IsEngaged || phaseTwo) return;
            float wanted = health.Max * phaseTwoAt;
            bool shield = health.Invulnerable;
            health.Invulnerable = false;
            if (health.Current > wanted) health.TakeDamage(new DamageInfo { amount = health.Current - wanted + 0.01f, point = transform.position });
            health.Invulnerable = shield && !health.IsDead && !Exposed;
        }

        // ---------- Ciclo ----------

        float Water(Vector3 p) => ocean != null ? ocean.GetHeight(p) : 0f;
        float PhaseTime => Time.time - phaseStart;

        bool TargetValid => target && target.gameObject.activeInHierarchy && targetHealth && !targetHealth.IsDead &&
                            !SafeZone.At(target.position);

        void SetPhase(Phase phase)
        {
            Current = phase;
            phaseStart = Time.time;
            bool exposed = Exposed;
            if (aim) aim.Exposed = exposed;
            if (!health.IsDead) health.Invulnerable = !exposed;
        }

        void Update()
        {
            switch (Current)
            {
                case Phase.Lurking:
                    if (TargetValid && Flat(target.position - lair).magnitude < detectRadius) BeginRise();
                    break;
                case Phase.Rising:
                    if (!StillHunting()) break;
                    depth = Mathf.Lerp(deepDepth, cruiseDepth, Mathf.SmoothStep(0f, 1f, PhaseTime / riseSeconds));
                    Swim(CircleHeading(), CurrentCircleSpeed, turnRate);
                    if (PhaseTime >= riseSeconds) BeginCircle();
                    break;
                case Phase.Circle:
                    if (!StillHunting()) break;
                    depth = cruiseDepth;
                    Swim(CircleHeading(), CurrentCircleSpeed, turnRate);
                    if (Time.time >= nextCharge)
                    {
                        float distance = Flat(target.position - transform.position).magnitude;
                        if (phaseTwo && Random.value < breachChance && distance < circleRadius * 1.3f) BeginBreach();
                        else BeginWindUp();
                    }
                    break;
                case Phase.WindUp:
                    if (!StillHunting()) break;
                    depth = cruiseDepth;
                    // Desacelera virando o focinho para o costado; bolhas e espuma na frente (o aviso).
                    Swim(Flat(RamPoint() - transform.position), CurrentCircleSpeed * 0.35f, chargeTurnRate);
                    Bubbles(transform.position + heading * HalfLength, 0.25f, 1.1f);
                    if (PhaseTime >= (phaseTwo ? windUpTwo : windUpSeconds)) BeginCharge();
                    break;
                case Phase.Charge:
                    if (!StillHunting()) break;
                    // Investindo mais alto: a cabeça com a bocarra aberta sai da água.
                    depth = cruiseDepth + chargeRise;
                    Swim(Flat(RamPoint() - transform.position), chargeSpeed, chargeTurnRate);
                    if (!rammed && Flat(target.position - (transform.position + heading * HalfLength * 0.8f)).magnitude < HullReach) Ram();
                    if (rammed || PhaseTime >= chargeTimeout) BeginDive();
                    break;
                case Phase.Dive:
                    depth = Mathf.MoveTowards(depth, deepDepth * 0.6f, 8f * Time.deltaTime);
                    // Afasta-se mergulhando e volta pelo outro lado.
                    Swim(target ? Flat(transform.position - target.position) : heading, chargeSpeed * 0.5f, turnRate);
                    if (PhaseTime >= diveSeconds)
                    {
                        if (!StillHunting()) break;
                        circleDir = Random.value < 0.5f ? 1f : -1f;
                        BeginRise();
                    }
                    break;
                case Phase.Breach:
                    UpdateBreach();
                    break;
                case Phase.Retreat:
                    depth = Mathf.MoveTowards(depth, deepDepth, 6f * Time.deltaTime);
                    Swim(Flat(lair - transform.position), CurrentCircleSpeed, turnRate);
                    if (PhaseTime >= 4f) ResetToLair();
                    break;
                case Phase.Dead:
                    UpdateDead();
                    break;
            }
            if (Current != Phase.Breach && Current != Phase.Dead) PlaceAtDepth();
            UpdateAnimation();
        }

        void LateUpdate()
        {
            // Cara de megalodonte: cabeça e mandíbula maiores e a bocarra aberta (depois da animação do kit).
            float want = Current is Phase.WindUp or Phase.Charge or Phase.Breach ? jawBite : Current == Phase.Dead ? jawBite * 0.6f : jawOpen;
            jawAngle = Mathf.MoveTowards(jawAngle, want, 70f * Time.deltaTime);
            if (headBone) headBone.localScale = Vector3.one * headScale;
            if (jawBone)
            {
                jawBone.localScale = Vector3.one * jawScale;
                jawBone.localRotation *= Quaternion.Euler(jawAngle, 0f, 0f);
            }
        }

        /// <summary>Continua a luta? (navio vivo, fora do porto e perto do covil). Senão, volta ao covil.</summary>
        bool StillHunting()
        {
            if (TargetValid && Flat(target.position - lair).magnitude <= leash) return true;
            Retreat();
            return false;
        }

        float CurrentCircleSpeed => phaseTwo ? circleSpeed.y : circleSpeed.x;
        float HalfLength => model ? 9f * Mathf.Max(0.1f, model.lossyScale.z / 3.5f) : 9f;

        /// <summary>Meia largura do casco + uma folga: a cabeça a menos disto do navio = acertou.</summary>
        float HullReach => (targetBox ? targetBox.size.x * 0.5f : 3f) + 4f;

        /// <summary>Tangente ao círculo em volta do navio, corrigindo a distância.</summary>
        Vector3 CircleHeading()
        {
            if (!target) return heading;
            Vector3 radial = Flat(transform.position - target.position);
            float r = radial.magnitude;
            if (r < 0.01f) return heading;
            radial /= r;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * circleDir;
            return (tangent + radial * Mathf.Clamp((circleRadius - r) * 0.08f, -1f, 1f)).normalized;
        }

        /// <summary>Meio do costado mais perto dele, adiantado pela velocidade do navio.</summary>
        Vector3 RamPoint()
        {
            Vector3 p = target.position;
            if (targetBody) p += Flat(targetBody.linearVelocity) * 0.6f;
            return p;
        }

        void Swim(Vector3 desired, float speed, float turn)
        {
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.0001f)
                heading = Vector3.RotateTowards(heading, desired.normalized, turn * Mathf.Deg2Rad * Time.deltaTime, 0f);
            heading.y = 0f;
            heading.Normalize();
            transform.position += heading * (speed * Time.deltaTime);
        }

        void PlaceAtDepth()
        {
            Vector3 p = transform.position;
            p.y = Water(p) + depth;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(heading, Vector3.up));
        }

        // ---------- Fases ----------

        void BeginRise()
        {
            if (Current == Phase.Lurking)
            {
                Spotted?.Invoke(this);
                effects?.Surge(Flat(transform.position) + Vector3.up * Water(transform.position), 1.5f);
            }
            if (target) heading = Flat(Vector3.Cross(Vector3.up, Flat(transform.position - target.position).normalized) * circleDir).normalized;
            if (heading.sqrMagnitude < 0.01f) heading = transform.forward;
            SetPhase(Phase.Rising);
        }

        void BeginCircle()
        {
            nextCharge = Time.time + RandomIn(phaseTwo ? chargeEveryTwo : chargeEvery);
            SetPhase(Phase.Circle);
        }

        void BeginWindUp()
        {
            SetPhase(Phase.WindUp);
            effects?.Roar(transform.position + heading * HalfLength);
        }

        void BeginCharge()
        {
            rammed = false;
            SetPhase(Phase.Charge);
            effects?.Surge(transform.position + heading * HalfLength + Vector3.up * 1f, 1.3f);
        }

        void BeginDive()
        {
            SetPhase(Phase.Dive);
            effects?.Surge(transform.position + Vector3.up * 1.5f, 1.8f);
        }

        void Retreat()
        {
            if (Current == Phase.Retreat || Current == Phase.Dead) return;
            SetPhase(Phase.Retreat);
        }

        void ResetToLair()
        {
            if (!health.IsDead && health.Current < health.Max) health.SetCurrent(health.Max);
            phaseTwo = false;
            depth = deepDepth;
            Vector3 p = lair;
            p.y = Water(p) + deepDepth;
            transform.position = p;
            heading = transform.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
            heading.Normalize();
            if (model) model.localRotation = Quaternion.identity;
            SetPhase(Phase.Lurking);
        }

        void Ram()
        {
            rammed = true;
            Vector3 hit = target.position + Flat(transform.position - target.position).normalized * (HullReach - 3f) + Vector3.up * 1.5f;
            float damage = phaseTwo ? ramDamageTwo : ramDamage;
            if (targetHealth) targetHealth.TakeDamage(new DamageInfo { amount = damage, point = hit, direction = heading, source = gameObject });
            if (targetBody)
            {
                // Empurra o casco para o lado da investida, aderna e vira (aceleração direta: força num ponto capotava o galeão, como na lula).
                targetBody.AddForce(heading * ramPush, ForceMode.VelocityChange);
                Vector3 along = Flat(target.forward).normalized;
                float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(along, Vector3.up), heading));
                targetBody.AddTorque(along * (side * ramRoll) + Vector3.up * (side * ramYaw), ForceMode.VelocityChange);
            }
            effects?.Impact(hit, -heading);
            effects?.Surge(hit, 1.4f);
            shownState = 0;
            Play(BiteState);
        }

        // ---------- Salto (furioso) ----------

        void BeginBreach()
        {
            // Cai colado ao costado do navio, do lado em que ele está; o salto passa por cima da água do lado de lá.
            Vector3 toShark = Flat(transform.position - target.position);
            Vector3 side = toShark.sqrMagnitude > 0.01f ? toShark.normalized : target.right;
            breachTo = target.position + side * (HullReach * 0.5f) + (targetBody ? Flat(targetBody.linearVelocity) * (breachWarning + breachSeconds * 0.8f) : Vector3.zero);
            breachFrom = transform.position;
            breachWarned = false;
            rammed = false;
            SetPhase(Phase.Breach);
            depth = deepDepth * 0.4f;
        }

        void UpdateBreach()
        {
            float t = PhaseTime;
            if (t < breachWarning)
            {
                // Mergulha um pouco e corre para baixo do ponto de partida; a água estoura onde ele vai cair.
                depth = Mathf.MoveTowards(depth, deepDepth * 0.4f, 8f * Time.deltaTime);
                Swim(Flat(breachFrom - transform.position), CurrentCircleSpeed, chargeTurnRate);
                Bubbles(breachTo, 0.15f, 1.6f);
                PlaceAtDepth();
                return;
            }
            if (!breachWarned)
            {
                breachWarned = true;
                breachFrom = transform.position;
                effects?.Roar(breachFrom);
                effects?.Surge(new Vector3(breachFrom.x, Water(breachFrom), breachFrom.z), 2.4f);
            }
            float u = Mathf.Clamp01((t - breachWarning) / breachSeconds);
            // Arco: sai da água, passa por cima do ponto de queda e mergulha logo depois dele.
            Vector3 end = breachTo + Flat(breachTo - breachFrom).normalized * 6f;
            Vector3 flat = Vector3.Lerp(Flat(breachFrom), Flat(end), u);
            float water = Water(flat);
            float y = water + Mathf.Lerp(breachFrom.y - Water(breachFrom), deepDepth * 0.4f, u) + 4f * u * (1f - u) * breachHeight;
            Vector3 p = new(flat.x, y, flat.z);
            Vector3 dir = Flat(end - breachFrom);
            if (dir.sqrMagnitude > 0.01f) heading = dir.normalized;
            float pitch = Mathf.Lerp(-35f, 40f, u);
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f));
            if (!rammed && u >= 0.62f)
            {
                rammed = true;
                effects?.Surge(new Vector3(breachTo.x, Water(breachTo), breachTo.z), 3f);
                if (TargetValid && Flat(target.position - breachTo).magnitude < HullReach + 2f)
                {
                    Vector3 hit = target.position + Vector3.up * 2f;
                    if (targetHealth) targetHealth.TakeDamage(new DamageInfo { amount = breachDamage, point = hit, direction = Vector3.down, source = gameObject });
                    if (targetBody)
                    {
                        Vector3 along = Flat(target.forward).normalized;
                        float s = Mathf.Sign(Vector3.Dot(Vector3.Cross(along, Vector3.up), heading));
                        targetBody.AddTorque(along * (s * breachRoll), ForceMode.VelocityChange);
                        targetBody.AddForce(Vector3.down * 1.2f, ForceMode.VelocityChange);
                    }
                    effects?.Impact(hit, Vector3.up);
                }
            }
            if (u >= 1f)
            {
                depth = deepDepth * 0.4f;
                BeginDive();
            }
        }

        // ---------- Dano, fúria e morte ----------

        void OnDamaged(DamageInfo info)
        {
            if (phaseTwo || health.IsDead || health.Normalized > phaseTwoAt) return;
            phaseTwo = true;
            effects?.Roar(transform.position);
            effects?.Surge(transform.position + Vector3.up * 2f, 2.5f);
            nextCharge = Mathf.Min(nextCharge, Time.time + 1.5f);
            Enraged?.Invoke(this);
        }

        void OnDied()
        {
            SetPhase(Phase.Dead);
            deadSince = Time.time;
            if (aim) aim.Exposed = false;
            effects?.Roar(transform.position);
            effects?.Surge(transform.position + Vector3.up * 2f, 3f);
            EventBus.Publish(new EnemyDefeatedEvent { targetId = targetId, fame = fame });
            Defeated?.Invoke(this);
        }

        void UpdateDead()
        {
            float t = PhaseTime;
            if (t < 12f)
            {
                // Boia de barriga para cima e afunda devagar.
                float roll = Mathf.Lerp(0f, 180f, Mathf.SmoothStep(0f, 1f, t / 2.5f));
                float sink = t < 5f ? cruiseDepth : Mathf.Lerp(cruiseDepth, deepDepth * 1.5f, (t - 5f) / 7f);
                Vector3 p = transform.position;
                p.y = Water(p) + sink;
                transform.SetPositionAndRotation(p, Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, 0f, roll));
                return;
            }
            if (Time.time - deadSince > respawnSeconds && (!target || Flat(target.position - lair).magnitude > 300f))
            {
                health.Revive();
                ResetToLair();
            }
        }

        // ---------- Visual ----------

        void UpdateAnimation()
        {
            if (!animator) return;
            int want = Current switch
            {
                Phase.WindUp or Phase.Charge or Phase.Breach or Phase.Dive => FastState,
                Phase.Dead => 0,
                _ => SwimState,
            };
            if (Current == Phase.Charge && rammed) want = BiteState;
            if (want == 0)
            {
                animator.speed = Mathf.MoveTowards(animator.speed, 0f, Time.deltaTime);
                return;
            }
            animator.speed = 1f;
            Play(want);
        }

        void Play(int state)
        {
            if (!animator || shownState == state) return;
            shownState = state;
            animator.CrossFadeInFixedTime(state, 0.25f);
        }

        void Bubbles(Vector3 at, float every, float size)
        {
            if (Time.time < nextBubble || effects == null) return;
            nextBubble = Time.time + every;
            effects.Surge(new Vector3(at.x, Water(at), at.z) + Flat(Random.insideUnitSphere) * 2f, size);
        }

        static float RandomIn(Vector2 range) => Random.Range(range.x, range.y);
        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
