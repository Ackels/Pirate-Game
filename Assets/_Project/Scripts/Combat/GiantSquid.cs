using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    /// <summary>
    /// Lula gigante (chefe do mar): mora num covil em mar aberto. O navio do jogador que entra no covil é emboscado: a água ferve em
    /// volta dele, dois tentáculos sobem colados no casco e o agarram (o navio fica travado: só gira, não foge) e outros sobem dos
    /// lados e batem no convés (aviso: o tentáculo recua tremendo antes da pancada). A cabeça emerge de um lado a ~26 m (alvo da mira
    /// assistida), fica um tempo e troca de lado. Os tentáculos levam tiro à queima-roupa: a bordada de um lado acerta os tentáculos
    /// colados naquele bordo (um tiro por canhão a até <see cref="pointBlankReach"/> m dele, proa-popa), sem mira; isso tira a vida dele e
    /// uma parte da vida da lula (a mira de 20 m em diante nunca mostrava o tentáculo colado no casco); tentáculo destruído
    /// se debate, afunda e volta depois. Sem os dois agarrando, o navio se solta; a lula agarra de novo se ele não fugir para longe
    /// do covil. Com metade da vida ela se enfurece: urra, um tentáculo a mais e pancadas mais rápidas e fortes.
    /// Morta: tudo afunda, publica <see cref="EnemyDefeatedEvent"/> (fama, missão) e o saque (LootDropper) sai onde estava a cabeça.
    /// Volta ao covil depois de <see cref="respawnSeconds"/> com o jogador longe.
    /// O objeto raiz é a cabeça (anda de um lado para o outro); os tentáculos são filhos, mas posicionados no mundo a cada quadro.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class GiantSquid : MonoBehaviour
    {
        public enum Phase { Lurking, Ambush, Fight, Retreat, Dead }

        static readonly List<GiantSquid> active = new();
        public static IReadOnlyList<GiantSquid> Active => active;
        /// <summary>A lula atacou um navio (o HUD avisa).</summary>
        public static event Action<GiantSquid> Ambushed;
        public static event Action<GiantSquid> Enraged;
        public static event Action<GiantSquid> Defeated;

        [Header("Nome")]
        [SerializeField] string displayName = "Lula Gigante";
        [SerializeField] string title = "o terror das Águas Negras";
        [Tooltip("Id nas missões (EnemyDefeatedEvent).")]
        [SerializeField] string targetId = "lula_gigante";
        [SerializeField, Min(0)] int fame = 100;

        [Header("Partes")]
        [Tooltip("Navio do jogador (o alvo).")]
        [SerializeField] Transform target;
        [Tooltip("Modelo da cabeça (filho; sobe e desce no lugar).")]
        [SerializeField] Transform head;
        [SerializeField] AimTarget headAim;
        [SerializeField] SquidTentacle[] tentacles;

        [Header("Covil")]
        [Tooltip("Navio a menos disto (m) do covil é atacado.")]
        [SerializeField] float detectRadius = 55f;
        [Tooltip("Solto e mais longe que isto do covil, a lula desiste (cura e volta a esperar).")]
        [SerializeField] float leash = 200f;
        [Tooltip("Segundos (com o jogador a mais de 300 m) até voltar depois de morta.")]
        [SerializeField] float respawnSeconds = 480f;

        [Header("Emboscada e agarrão")]
        [SerializeField] float ambushSeconds = 2.4f;
        [Tooltip("Navio preso com um / dois tentáculos agarrando (ShipController.Restraint).")]
        [SerializeField] Vector2 restraint = new(0.75f, 0.95f);
        [Tooltip("Dano no casco a cada 2 s por tentáculo agarrando (aperto).")]
        [SerializeField] float crushDamage = 8f;
        [Tooltip("Adernar (rad/s²) para o lado de cada tentáculo agarrando e afundar um pouco (m/s²). Sem força num ponto: o casco tem " +
                 "pouca inércia de lado e capotava.")]
        [SerializeField] float grabHeel = 0.35f, grabSink = 0.6f;
        [Tooltip("Tranco da pancada no convés: giro (rad/s) para o lado dela e descida (m/s).")]
        [SerializeField] float slamRoll = 0.18f, slamDrop = 0.8f;
        [Tooltip("Segundos sem nenhum agarrando até tentar agarrar de novo (se o navio ainda estiver perto do covil).")]
        [SerializeField] float regrabSeconds = 14f;
        [Tooltip("Solto há mais que isto (s) sem ela conseguir agarrar de novo (navio entre o raio de reagarrar e o leash): desiste, " +
                 "se cura e volta a esperar. Sem isso dava para descansar ali e voltar.")]
        [SerializeField] float giveUpSeconds = 35f;

        [Header("Pancadas")]
        [SerializeField] float slamDamage = 30f;
        [Tooltip("Segundos do aviso (recua tremendo) e da descida.")]
        [SerializeField] float windUpSeconds = 1.1f, slamSeconds = 0.28f;
        [Tooltip("Segundos deitado no convés depois da pancada (o tentáculo fica exposto).")]
        [SerializeField] float pinnedSeconds = 0.9f;
        [Tooltip("Espera (s) de cada tentáculo entre uma pancada e outra.")]
        [SerializeField] Vector2 slamPause = new(1.3f, 2.4f);
        [Tooltip("Tentáculos batendo na fase 1 / fase 2.")]
        [SerializeField] Vector2Int slammers = new(2, 3);
        [Tooltip("Navio solto andando mais rápido que isto (m/s): os tentáculos que batem afundam (perderam o navio).")]
        [SerializeField] float loseSpeed = 2.5f;

        [Header("Tentáculos")]
        [Tooltip("Fração do dano no tentáculo que vai para a vida da lula.")]
        [SerializeField, Range(0f, 1f)] float tentacleShare = 0.4f;
        [Tooltip("Segundos até um tentáculo destruído voltar.")]
        [SerializeField] float regrowSeconds = 16f;
        [Tooltip("Metros da amurada até a base dos tentáculos que batem.")]
        [SerializeField] float slamReach = 4.2f;
        [Tooltip("Queima-roupa: canhões do bordo que dispara a até isto (m, proa-popa) de um tentáculo daquele lado acertam ele (um tiro cada).")]
        [SerializeField] float pointBlankReach = 2.5f;

        [Header("Cabeça")]
        [Tooltip("Metros de lado do navio onde a cabeça emerge.")]
        [SerializeField] float headDistance = 26f;
        [Tooltip("Segundos à mostra e submersa (fase 1).")]
        [SerializeField] float headUpSeconds = 11f, headDownSeconds = 6f;
        [Tooltip("Altura (local) da cabeça submersa e à mostra.")]
        [SerializeField] float headSunk = -12f, headShown = -0.8f;
        [SerializeField] float headRiseSeconds = 1.6f;

        [Header("Fase 2")]
        [SerializeField, Range(0f, 1f)] float phaseTwoAt = 0.5f;
        [SerializeField] float windUpTwo = 0.8f, slamDamageTwo = 38f;
        [SerializeField] Vector2 slamPauseTwo = new(0.9f, 1.6f);

        Health health;
        Rigidbody targetBody;
        Health targetHealth;
        ShipController targetShip;
        BoxCollider targetBox;
        IOceanSampler ocean;
        CombatEffects effects;
        Vector3 lair;
        float phaseStart, nextCheck, nextCrush, freeSince, headStateUntil, deadSince, nextBubble;
        bool headUp, headMoving, phaseTwo;
        Collider[] shipColliders;
        int headSide = 1;
        float headFrom, headTo, headMoveStart;
        readonly Dictionary<SquidTentacle, float> nextSlam = new();
        readonly Dictionary<SquidTentacle, float> regrowAt = new();
        /// <summary>Vida de cada tentáculo no último golpe (o tiro que o destrói só repassa o que ele ainda tinha).</summary>
        readonly Dictionary<SquidTentacle, float> armHealth = new();
        Vector3 lastSlamPoint;

        public Phase Current { get; private set; } = Phase.Lurking;
        public string DisplayName => displayName;
        public string Title => title;
        public Health Health => health;
        public bool PhaseTwo => phaseTwo;
        public bool HeadExposed => headUp && !headMoving;
        public Vector3 Lair => lair;
        public IReadOnlyList<SquidTentacle> Tentacles => tentacles;
        /// <summary>Lutando (o HUD mostra a barra).</summary>
        public bool IsEngaged => (Current == Phase.Ambush || Current == Phase.Fight) && !health.IsDead;
        /// <summary>Tentáculos agarrando o navio agora.</summary>
        public int Grabbing { get; private set; }

        public void Configure(Transform playerShip, Transform headModel, AimTarget aim, SquidTentacle[] arms)
        {
            target = playerShip;
            head = headModel;
            headAim = aim;
            tentacles = arms;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            lair = transform.position;
            if (target)
            {
                targetBody = target.GetComponent<Rigidbody>();
                targetHealth = target.GetComponent<Health>();
                targetShip = target.GetComponent<ShipController>();
                targetBox = target.GetComponent<BoxCollider>();
            }
            // Uma vez só (vivem junto com a lula). Todas as baterias do navio (as dos cascos desligados também: trocar de navio liga outras).
            if (target)
                foreach (var battery in target.GetComponentsInChildren<CannonBattery>(true))
                {
                    var b = battery;
                    b.Fired += () => OnBroadside(b);
                }
            foreach (var arm in tentacles)
            {
                var a = arm;
                a.GetComponent<Health>().Damaged += info => OnArmDamaged(a, info);
                a.GetComponent<Health>().Died += () => OnArmDied(a);
            }
        }

        void OnEnable()
        {
            active.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            active.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (targetShip) targetShip.Restraint = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            Ambushed = null;
            Enraged = null;
            Defeated = null;
        }

        void Start()
        {
            Services.TryGet(out ocean);
            Services.TryGet(out effects);
            for (int i = 0; i < tentacles.Length; i++) tentacles[i].Slot = i;
            ResetToLair();
        }

        float Water(Vector3 p) => ocean != null ? ocean.GetHeight(p) : 0f;

        bool TargetValid => target && target.gameObject.activeInHierarchy && targetHealth && !targetHealth.IsDead &&
                            !SafeZone.At(target.position);

        void SetPhase(Phase phase)
        {
            Current = phase;
            phaseStart = Time.time;
        }

        float PhaseTime => Time.time - phaseStart;

        // ---------- Sandbox / testes ----------

        /// <summary>Muda o covil para perto do navio e ataca agora (F1).</summary>
        public void SummonNear()
        {
            if (!target || health.IsDead) return;
            Vector3 p = target.position;
            lair = new Vector3(p.x, transform.position.y, p.z);
            ResetToLair();
            BeginAmbush();
        }

        public void EnrageNow()
        {
            if (!IsEngaged || phaseTwo) return;
            float wanted = health.Max * phaseTwoAt;
            if (health.Current > wanted) health.TakeDamage(new DamageInfo { amount = health.Current - wanted + 0.01f, point = transform.position });
        }

        // ---------- Ciclo ----------

        void Update()
        {
            switch (Current)
            {
                case Phase.Lurking:
                    if (Time.time >= nextCheck)
                    {
                        nextCheck = Time.time + 0.25f;
                        if (TargetValid && Flat(target.position - lair).magnitude < detectRadius) BeginAmbush();
                    }
                    break;
                case Phase.Ambush:
                    if (!TargetValid) { Retreat(); break; }
                    Bubbles(target.position, 7f, 0.25f);
                    if (PhaseTime >= ambushSeconds) StartGrab();
                    break;
                case Phase.Fight:
                    Fight();
                    break;
                case Phase.Retreat:
                    if (PhaseTime >= 3f) ResetToLair();
                    break;
                case Phase.Dead:
                    if (Time.time - deadSince > respawnSeconds && (!target || Flat(target.position - lair).magnitude > 300f))
                    {
                        health.Revive();
                        ResetToLair();
                    }
                    break;
            }
            UpdateHead();
        }

        void LateUpdate()
        {
            if (Current != Phase.Fight && Current != Phase.Ambush && Current != Phase.Retreat && Current != Phase.Dead) return;
            for (int i = 0; i < tentacles.Length; i++) TrackArm(tentacles[i]);
        }

        void FixedUpdate()
        {
            if (Current != Phase.Fight || !targetBody || Grabbing == 0) return;
            foreach (var arm in tentacles)
                if (arm.Current == SquidTentacle.Mode.Grab && arm.Alive)
                {
                    SlotLayout(arm.Slot, out float side, out _);
                    Vector3 forward = Flat(target.forward).normalized;
                    // Lado +1 (boreste) aderna para a direita = giro negativo em volta da proa.
                    targetBody.AddTorque(forward * (-side * grabHeel), ForceMode.Acceleration);
                    targetBody.AddForce(Vector3.down * grabSink, ForceMode.Acceleration);
                }
        }

        void BeginAmbush()
        {
            if (!TargetValid) return;
            targetBody = target.GetComponent<Rigidbody>();
            targetHealth = target.GetComponent<Health>();
            targetShip = target.GetComponent<ShipController>();
            targetBox = target.GetComponent<BoxCollider>();
            shipColliders = target.GetComponentsInChildren<Collider>(true);
            health.Invulnerable = false;
            SetPhase(Phase.Ambush);
            effects?.Roar(target.position);
            Ambushed?.Invoke(this);
        }

        /// <summary>Os dois primeiros tentáculos agarram; os outros sobem em volta e começam a bater.</summary>
        void StartGrab()
        {
            SetPhase(Phase.Fight);
            freeSince = -1f;
            for (int i = 0; i < tentacles.Length; i++)
            {
                var arm = tentacles[i];
                if (i < 2) RiseArm(arm, grab: true);
                else if (i - 2 < SlammerCount && arm.Current == SquidTentacle.Mode.Hidden && !regrowAt.ContainsKey(arm)) RiseArm(arm, grab: false);
            }
            headSide = Random.value < 0.5f ? 1 : -1;
            headUp = false;
            headStateUntil = Time.time + 2.5f;
        }

        int SlammerCount => phaseTwo ? slammers.y : slammers.x;

        void RiseArm(SquidTentacle arm, bool grab)
        {
            regrowAt.Remove(arm);
            Vector3 at = ArmBase(arm), aim = DeckPoint(arm.Slot);
            arm.Rise(at, aim, Water(at));
            armHealth[arm] = arm.Health.Current;
            effects?.Surge(new Vector3(at.x, Water(at), at.z), grab ? 1.2f : 1f);
            nextSlam[arm] = Time.time + 1.6f + Random.Range(0f, 1.5f);
            if (grab) pendingGrab.Add(arm);
        }

        readonly HashSet<SquidTentacle> pendingGrab = new();

        void Fight()
        {
            if (!TargetValid) { Retreat(); return; }

            Grabbing = 0;
            foreach (var arm in tentacles)
            {
                if (pendingGrab.Contains(arm) && arm.Current == SquidTentacle.Mode.Idle)
                {
                    pendingGrab.Remove(arm);
                    arm.Grab();
                    effects?.Impact(RailPoint(arm.Slot), Vector3.up);
                }
                if (arm.Current == SquidTentacle.Mode.Grab && arm.Alive) Grabbing++;
            }
            bool held = Grabbing > 0 || pendingGrab.Count > 0;
            if (targetShip) targetShip.Restraint = Grabbing == 0 ? 0f : Grabbing == 1 ? restraint.x : restraint.y;

            // Aperto: dano a cada 2 s por tentáculo agarrando.
            if (Grabbing > 0 && Time.time >= nextCrush)
            {
                nextCrush = Time.time + 2f;
                Hit(crushDamage * Grabbing, target.position + Vector3.up * 1.5f);
            }

            if (held) freeSince = -1f;
            else if (freeSince < 0f) freeSince = Time.time;

            float speed = targetBody ? Flat(targetBody.linearVelocity).magnitude : 0f;
            bool lost = !held && speed > loseSpeed;
            if (!held && Flat(target.position - lair).magnitude > leash) { Retreat(); return; }
            if (!held && freeSince >= 0f && Time.time - freeSince > giveUpSeconds) { Retreat(); return; }

            for (int i = 2; i < tentacles.Length; i++)
            {
                var arm = tentacles[i];
                bool wanted = i - 2 < SlammerCount;
                if (lost || !wanted)
                {
                    if (arm.Current != SquidTentacle.Mode.Hidden && arm.Current != SquidTentacle.Mode.Hurt) arm.Submerge();
                    continue;
                }
                // Um a mais na fase 2 (ou de volta depois de perder o navio de vista): sobe na hora; destruído espera a vez dele.
                if (arm.Current == SquidTentacle.Mode.Hidden && !regrowAt.ContainsKey(arm)) RiseArm(arm, grab: false);
                Slammer(arm);
            }

            // Tentáculos destruídos voltam; sem ninguém agarrando por um tempo, agarra de novo (se ainda estiver perto).
            foreach (var arm in tentacles)
            {
                if (arm.Current != SquidTentacle.Mode.Hidden || !regrowAt.TryGetValue(arm, out float when) || Time.time < when) continue;
                bool grabber = arm.Slot < 2;
                if (grabber || lost) continue;
                if (arm.Slot - 2 < SlammerCount) RiseArm(arm, grab: false);
            }
            if (!held && freeSince >= 0f && Time.time - freeSince >= regrabSeconds)
            {
                // Volta a agarrar: bolhas de aviso e os dois sobem de novo (perto do covil).
                if (Flat(target.position - lair).magnitude < leash * 0.8f)
                {
                    tentacles[0].Hide();
                    tentacles[1].Hide();
                    SetPhase(Phase.Ambush);
                    effects?.Roar(target.position);
                }
            }
        }

        void Slammer(SquidTentacle arm)
        {
            switch (arm.Current)
            {
                case SquidTentacle.Mode.Idle:
                    if (Time.time >= nextSlam.GetValueOrDefault(arm)) arm.WindUp(phaseTwo ? windUpTwo : windUpSeconds);
                    break;
                case SquidTentacle.Mode.WindUp:
                    if (arm.ModeTime >= (phaseTwo ? windUpTwo : windUpSeconds)) arm.Slam(slamSeconds);
                    break;
                case SquidTentacle.Mode.Slam:
                    if (arm.SlamLanded)
                    {
                        arm.Pin();
                        lastSlamPoint = arm.Tip;
                        Hit(phaseTwo ? slamDamageTwo : slamDamage, lastSlamPoint);
                        effects?.Impact(lastSlamPoint, Vector3.up);
                        effects?.Surge(lastSlamPoint, 0.6f);
                        if (targetBody)
                        {
                            SlotLayout(arm.Slot, out float side, out _);
                            targetBody.AddTorque(Flat(target.forward).normalized * (-side * slamRoll), ForceMode.VelocityChange);
                            targetBody.AddForce(Vector3.down * slamDrop, ForceMode.VelocityChange);
                        }
                    }
                    break;
                case SquidTentacle.Mode.Pinned:
                    if (arm.ModeTime >= pinnedSeconds)
                    {
                        arm.Idle();
                        Vector2 pause = phaseTwo ? slamPauseTwo : slamPause;
                        nextSlam[arm] = Time.time + Random.Range(pause.x, pause.y);
                    }
                    break;
            }
        }

        void Hit(float amount, Vector3 point)
        {
            if (!targetHealth || amount <= 0f) return;
            targetHealth.TakeDamage(new DamageInfo { amount = amount, point = point, direction = Vector3.down, source = gameObject });
        }

        void Retreat()
        {
            if (Current == Phase.Retreat || Current == Phase.Dead) return;
            SetPhase(Phase.Retreat);
            if (targetShip) targetShip.Restraint = 0f;
            Grabbing = 0;
            pendingGrab.Clear();
            foreach (var arm in tentacles) arm.Submerge();
            SinkHead();
        }

        /// <summary>Volta a esperar no covil: vida cheia, fase 1, tudo escondido.</summary>
        void ResetToLair()
        {
            SetPhase(Phase.Lurking);
            if (!health.IsDead) health.Revive();
            health.Invulnerable = true;
            phaseTwo = false;
            Grabbing = 0;
            pendingGrab.Clear();
            regrowAt.Clear();
            if (targetShip) targetShip.Restraint = 0f;
            foreach (var arm in tentacles) arm.Hide();
            transform.position = new Vector3(lair.x, Water(lair), lair.z);
            headUp = false;
            headMoving = false;
            SetHeadHeight(headSunk);
            if (headAim) headAim.Exposed = false;
            SetHeadColliders(false);
        }

        // ---------- Dano ----------

        void OnArmDamaged(SquidTentacle arm, DamageInfo info)
        {
            float before = armHealth.TryGetValue(arm, out float hp) ? hp : arm.Health.Max;
            float taken = Mathf.Max(0f, before - arm.Health.Current);
            armHealth[arm] = arm.Health.Current;
            if (health.IsDead || Current != Phase.Fight || taken <= 0f) return;
            health.TakeDamage(new DamageInfo
            {
                amount = taken * tentacleShare, point = info.point, direction = info.direction, source = info.source,
            });
        }

        /// <summary>
        /// Bordada do navio do jogador: cada tentáculo colado naquele bordo leva um tiro de cada canhão a até
        /// <see cref="pointBlankReach"/> m dele (proa-popa), com o dano da bala e da munição (metralha 1,5×, corrente 0,5×).
        /// </summary>
        void OnBroadside(CannonBattery battery)
        {
            if (Current != Phase.Fight || !target || health.IsDead) return;
            float side = (float)battery.Side;
            float perBall = battery.Stats.damagePerBall * (battery.Ammo ? battery.Ammo.damageMultiplier : 1f);
            Hull(out float hw, out float hl, out float deck);
            var muzzles = battery.Muzzles;
            foreach (var arm in tentacles)
            {
                if (!arm.Alive || !arm.Visible || arm.Current == SquidTentacle.Mode.Hurt || arm.Current == SquidTentacle.Mode.Sinking) continue;
                SlotLayout(arm.Slot, out float armSide, out float along);
                if (!Mathf.Approximately(armSide, side)) continue;
                float z = along * hl;
                int hits = 0;
                for (int i = 0; i < muzzles.Count; i++)
                    if (muzzles[i] && Mathf.Abs(target.InverseTransformPoint(muzzles[i].position).z - z) <= pointBlankReach) hits++;
                if (hits == 0) continue;
                bool grabber = arm.Slot < 2;
                Vector3 point = target.TransformPoint(new Vector3(side * (hw + (grabber ? 0.7f : slamReach)), deck + 0.8f, z));
                effects?.Impact(point, -target.right * side);
                arm.Health.TakeDamage(new DamageInfo
                {
                    amount = hits * perBall, point = point, direction = target.right * side, source = target.gameObject, ignites = true,
                });
            }
        }

        void OnArmDied(SquidTentacle arm)
        {
            arm.Kill();
            pendingGrab.Remove(arm);
            regrowAt[arm] = Time.time + regrowSeconds;
            effects?.Surge(arm.BasePoint, 1.2f);
            effects?.Roar(arm.Tip);
        }

        void OnDamaged(DamageInfo info)
        {
            if (phaseTwo || health.IsDead || health.Normalized > phaseTwoAt) return;
            phaseTwo = true;
            effects?.Roar(transform.position);
            Enraged?.Invoke(this);
        }

        void OnDied()
        {
            SetPhase(Phase.Dead);
            deadSince = Time.time;
            if (targetShip) targetShip.Restraint = 0f;
            Grabbing = 0;
            pendingGrab.Clear();
            foreach (var arm in tentacles) arm.Kill();
            effects?.Roar(transform.position);
            effects?.Surge(transform.position, 3f);
            SinkHead();
            EventBus.Publish(new EnemyDefeatedEvent { targetId = targetId, fame = fame });
            Defeated?.Invoke(this);
        }

        // ---------- Cabeça ----------

        void UpdateHead()
        {
            if (headMoving)
            {
                float t = Mathf.Clamp01((Time.time - headMoveStart) / headRiseSeconds);
                SetHeadHeight(Mathf.Lerp(headFrom, headTo, Mathf.SmoothStep(0f, 1f, t)));
                if (t >= 1f)
                {
                    headMoving = false;
                    if (!headUp && head) head.gameObject.SetActive(false);
                    if (headAim) headAim.Exposed = headUp;
                    SetHeadColliders(headUp);
                }
            }
            // Boia com a onda onde está.
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, Water(p), p.z);

            if (Current != Phase.Fight || headMoving || Time.time < headStateUntil) return;
            if (headUp)
            {
                SinkHead();
                headStateUntil = Time.time + headDownSeconds * (phaseTwo ? 0.7f : 1f);
                headSide = -headSide;
            }
            else if (Grabbing > 0) SurfaceHead();
        }

        void SurfaceHead()
        {
            // De lado do navio, a ~26 m (no través: os canhões daquele bordo alcançam), virada para ele.
            Vector3 right = Flat(target.right).normalized;
            Vector3 spot = target.position + right * (headSide * headDistance);
            spot.y = Water(spot);
            Vector3 look = Flat(target.position - spot);
            transform.SetPositionAndRotation(spot, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            headUp = true;
            MoveHead(headSunk, headShown);
            headStateUntil = Time.time + headRiseSeconds + headUpSeconds * (phaseTwo ? 0.8f : 1f);
            effects?.Surge(spot, 2.5f);
            effects?.Roar(spot);
        }

        void SinkHead()
        {
            if (!headUp && !headMoving) return;
            headUp = false;
            if (headAim) headAim.Exposed = false;
            SetHeadColliders(false);
            MoveHead(head.localPosition.y, headSunk);
            effects?.Surge(transform.position, 1.6f);
        }

        void MoveHead(float from, float to)
        {
            headFrom = from;
            headTo = to;
            headMoveStart = Time.time;
            headMoving = true;
        }

        void SetHeadHeight(float y)
        {
            if (!head) return;
            var p = head.localPosition;
            head.localPosition = new Vector3(p.x, y, p.z);
            head.gameObject.SetActive(y > headSunk + 0.01f || headMoving);
        }

        void SetHeadColliders(bool on)
        {
            if (!head) return;
            foreach (var c in head.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = on;
                // A cabeça atravessa o casco (as balas acertam por raio). O Unity desfaz o "ignorar" quando o colisor desliga: de novo a cada vez.
                if (on && shipColliders != null)
                    foreach (var s in shipColliders)
                        if (s) Physics.IgnoreCollision(c, s, true);
            }
        }

        // ---------- Onde fica cada tentáculo ----------

        /// <summary>Medidas do casco no espaço do navio: meia largura, meia proa-popa, altura do convés.</summary>
        void Hull(out float halfWidth, out float halfLength, out float deck)
        {
            if (targetBox)
            {
                halfWidth = targetBox.size.x * 0.5f;
                halfLength = targetBox.size.z * 0.5f;
                deck = targetBox.center.y + targetBox.size.y * 0.5f;
            }
            else
            {
                halfWidth = 2f;
                halfLength = 5f;
                deck = 1.5f;
            }
        }

        /// <summary>Lado (−1 bombordo, +1 boreste) e posição proa-popa (fração da meia-proa) de cada lugar.</summary>
        static void SlotLayout(int slot, out float side, out float along)
        {
            switch (slot)
            {
                case 0: side = -1f; along = 0.35f; break;   // agarra a bombordo, para a proa
                case 1: side = 1f; along = -0.35f; break;   // agarra a boreste, para a popa
                case 2: side = 1f; along = 0.55f; break;
                case 3: side = -1f; along = -0.5f; break;
                case 4: side = 1f; along = -0.75f; break;
                default: side = -1f; along = 0.75f; break;
            }
        }

        Vector3 ArmBase(SquidTentacle arm)
        {
            Hull(out float hw, out float hl, out _);
            SlotLayout(arm.Slot, out float side, out float along);
            bool grabber = arm.Slot < 2;
            // Agarrão: base funda colada no casco; pancada: na água, um pouco afastada da amurada.
            Vector3 local = grabber ? new Vector3(side * (hw + 0.6f), 0f, along * hl) : new Vector3(side * (hw + slamReach), 0f, along * hl);
            Vector3 world = target.TransformPoint(local);
            world.y = Water(world) - (grabber ? 4.5f : 0.6f);
            return world;
        }

        Vector3 RailPoint(int slot)
        {
            Hull(out float hw, out float hl, out float deck);
            SlotLayout(slot, out float side, out float along);
            return target.TransformPoint(new Vector3(side * hw * 0.95f, deck + 0.4f, along * hl));
        }

        /// <summary>Onde a ponta deita no convés (agarrão: atravessa para o outro lado; pancada: perto da linha do meio).</summary>
        Vector3 DeckPoint(int slot)
        {
            Hull(out float hw, out float hl, out float deck);
            SlotLayout(slot, out float side, out float along);
            bool grabber = slot < 2;
            var local = grabber ? new Vector3(-side * hw * 0.4f, deck + 0.2f, along * hl + 0.6f) : new Vector3(side * hw * 0.15f, deck + 0.25f, along * hl * 0.8f);
            return target.TransformPoint(local);
        }

        void TrackArm(SquidTentacle arm)
        {
            if (!arm.Visible || !target) return;
            // Afundando por causa da morte ou da volta: a base fica onde está.
            if (Current == Phase.Dead || Current == Phase.Retreat)
            {
                arm.Track(arm.BasePoint, DeckPoint(arm.Slot), RailPoint(arm.Slot), arm.WaterLevel);
                return;
            }
            Vector3 at = ArmBase(arm);
            arm.Track(at, DeckPoint(arm.Slot), RailPoint(arm.Slot), Water(at));
        }

        void Bubbles(Vector3 center, float radius, float every)
        {
            if (Time.time < nextBubble) return;
            nextBubble = Time.time + every;
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(radius * 0.5f, radius);
            var p = center + new Vector3(r.x, 0f, r.y);
            effects?.Surge(new Vector3(p.x, Water(p), p.z), 0.35f);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
