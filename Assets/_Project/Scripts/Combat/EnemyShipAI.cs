using System.Collections.Generic;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// IA de navio inimigo. Pilota pelo mesmo IShipInput do jogador (mesma física) e usa as mesmas baterias.
    /// Patrulha → persegue quando vê o alvo → manobra para pôr o alvo de través e dispara → foge com pouca vida.
    /// Respeita zonas seguras (SafeZone): não ataca alvo dentro delas e sai se entrar.
    /// Mira: a bala sai perpendicular ao casco e herda a velocidade do navio, então o que importa é onde o alvo vai estar
    /// quando ela cair, visto de quem atira (velocidade do alvo menos a nossa, pelo tempo de voo de verdade). A IA vira o
    /// través para esse ponto e só dispara com ele dentro do casco do alvo (antes mirava onde o alvo estava: quem andava em
    /// volta dela nunca era acertado).
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class EnemyShipAI : MonoBehaviour, IShipInput
    {
        public enum AIState { Patrol, Chase, Broadside, Flee }

        static readonly List<EnemyShipAI> active = new();

        /// <summary>Inimigos ativos na cena (a mira assistida procura alvos aqui).</summary>
        public static IReadOnlyList<EnemyShipAI> Active => active;

        // Índices de ShipStats.sailLevels: 0 Ré, 1 Recolhidas, 2 Meia vela, 3 Vela cheia.
        const int HalfSail = 2, FullSail = 3;

        [SerializeField] Transform target;
        [SerializeField] CannonBattery port;
        [SerializeField] CannonBattery starboard;
        [Tooltip("Id do navio nas missões (ex.: brigue_santa_clara). Vazio = navio comum (conta para \"afundar navios\").")]
        [SerializeField] string targetId;
        [Tooltip("Fama do capitão ao afundar este navio (M10).")]
        [SerializeField, Min(0)] int fame = 6;

        [Header("Percepção")]
        [SerializeField] float detectRange = 220f;
        [SerializeField] float loseRange = 320f;

        [Header("Combate")]
        [Tooltip("Distância que tenta manter durante a troca de tiros.")]
        [SerializeField] float preferredRange = 70f;
        [SerializeField] float maxFireRange = 150f;
        [SerializeField] float minFireRange = 20f;
        [Tooltip("Tolerância máxima (graus) entre o través e o ponto antecipado para disparar. Perto, vale a do tamanho do alvo.")]
        [SerializeField] float fireArcDegrees = 12f;
        [Tooltip("Meia largura (m) do alvo que conta como acerto: a tolerância do disparo é o ângulo que ela ocupa na distância.")]
        [SerializeField] float targetHalfLength = 4f;
        [Tooltip("Trocando tiros: graus de erro do rumo que já pedem leme todo (menor = vira mais firme para alinhar o través).")]
        [SerializeField] float broadsideRudderDegrees = 6f;
        [Tooltip("Segundos antes do fim da recarga em que para de corrigir a distância e vira o través para o alvo.")]
        [SerializeField] float aimLeadTime = 2f;
        [Tooltip("Erro da antecipação (fração do quanto o alvo anda durante o voo), sorteado a cada disparo. 0 = perfeita.")]
        [SerializeField, Range(0f, 0.6f)] float leadError = 0.35f;
        [Tooltip("Erro aleatório do alcance (fração). 0 = tiro perfeito.")]
        [SerializeField, Range(0f, 0.3f)] float rangeError = 0.1f;
        [Tooltip("Segundos alinhado antes de disparar (mín, máx). Dá tempo ao jogador de reagir.")]
        [SerializeField] Vector2 reactionTime = new(0.4f, 1.1f);
        [Tooltip("Foge com vida abaixo desta fração.")]
        [SerializeField, Range(0f, 1f)] float fleeHealth = 0.25f;

        [Header("Patrulha")]
        [SerializeField] float patrolRadius = 80f;
        [Tooltip("Folga (m) que mantém da borda das zonas seguras.")]
        [SerializeField] float safeZoneMargin = 25f;

        [Header("Evitar obstáculos")]
        [SerializeField] float avoidDistance = 45f;
        [SerializeField] float avoidProbeAngle = 35f;
        [SerializeField] float avoidProbeRadius = 2.5f;

        ShipController ship;
        Rigidbody body;
        Health health;
        Rigidbody targetBody;
        Health targetHealth;
        ShipCommand command;
        Vector3 patrolCenter, patrolPoint;
        float alignedTime = -1f, reactionDelay, leadBias;

        public AIState State { get; private set; } = AIState.Patrol;
        public bool IsAvoiding { get; private set; }
        /// <summary>Último erro (graus) entre o través e o ponto antecipado, e a tolerância para disparar (testes, gizmo).</summary>
        public float AimError { get; private set; }
        public float AimTolerance { get; private set; }
        /// <summary>O alvo está numa zona segura: a IA desistiu dele por enquanto.</summary>
        public bool TargetIsSafe { get; private set; }
        public bool IsDead => health && health.IsDead;
        public string TargetId => targetId;
        /// <summary>Navio que esta IA caça (o do jogador).</summary>
        public Transform Target => target;

        public void Configure(Transform targetShip, CannonBattery portBattery, CannonBattery starboardBattery)
        {
            target = targetShip;
            port = portBattery;
            starboard = starboardBattery;
        }

        /// <summary>
        /// Pontaria e coragem (o brigue é mais perigoso que a chalupa): erro do alcance (fração), segundos alinhado antes de
        /// disparar, abertura máxima do través para disparar (graus), vida em que foge e erro da antecipação (fração; negativo
        /// mantém o atual).
        /// </summary>
        public void ConfigureGunnery(float rangeErrorFraction, Vector2 reaction, float fireArc, float fleeAt, float leadErrorFraction = -1f)
        {
            rangeError = Mathf.Clamp(rangeErrorFraction, 0f, 0.3f);
            reactionTime = reaction;
            fireArcDegrees = fireArc;
            fleeHealth = Mathf.Clamp01(fleeAt);
            if (leadErrorFraction >= 0f) leadError = Mathf.Clamp(leadErrorFraction, 0f, 0.6f);
        }

        /// <summary>Navio com nome nas missões (o brigue): o afundamento sai com este id.</summary>
        public void SetTargetId(string id) => targetId = id;

        /// <summary>Fama que afundar este navio dá (M10).</summary>
        public int Fame => fame;
        public void SetFame(int value) => fame = Mathf.Max(0, value);

        public ShipCommand GetCommand() => command;

        void Awake()
        {
            ship = GetComponent<ShipController>();
            body = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            leadBias = Random.Range(-leadError, leadError);
            if (target)
            {
                targetBody = target.GetComponentInParent<Rigidbody>();
                targetHealth = target.GetComponentInParent<Health>();
            }
            patrolCenter = transform.position;
            PickPatrolPoint();
        }

        void OnEnable()
        {
            active.Add(this);
            if (health) health.Died += OnDied;
        }

        void OnDisable()
        {
            active.Remove(this);
            if (health) health.Died -= OnDied;
        }

        // Missões (afundar navios) ouvem isto.
        void OnDied() => EventBus.Publish(new ShipSunkEvent { ship = gameObject, targetId = targetId, fame = fame });

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void Update()
        {
            if ((health && health.IsDead) || !ship.enabled)
            {
                command = default;
                return;
            }

            Vector3 position = transform.position;
            Vector3 forward = Flat(transform.forward).normalized;
            bool hasTarget = target && target.gameObject.activeInHierarchy && !(targetHealth && targetHealth.IsDead);
            TargetIsSafe = hasTarget && SafeZone.At(target.position);
            if (TargetIsSafe) hasTarget = false;
            Vector3 toTarget = hasTarget ? Flat(target.position - position) : Vector3.zero;
            float distance = toTarget.magnitude;

            UpdateState(hasTarget, distance);

            Vector3 desired;
            int sail;
            switch (State)
            {
                case AIState.Chase:
                    desired = Flat(PredictTarget(3f) - position);
                    sail = FullSail;
                    break;
                case AIState.Broadside:
                    // O través vira para onde o alvo vai estar quando a bala cair, não para onde ele está.
                    Vector3 lead = LeadOffset(toTarget);
                    float leadDistance = Mathf.Max(0.01f, lead.magnitude);
                    desired = BroadsideHeading(lead / leadDistance, distance, KeepRange(lead / leadDistance));
                    sail = HalfSail;
                    TryFire(lead, leadDistance);
                    break;
                case AIState.Flee:
                    desired = -toTarget;
                    sail = FullSail;
                    break;
                default:
                    if (Flat(patrolPoint - position).magnitude < 20f) PickPatrolPoint();
                    desired = Flat(patrolPoint - position);
                    sail = HalfSail;
                    break;
            }

            // Perto de uma zona segura: sai dela (ou não entra), em qualquer estado.
            var zone = SafeZone.At(position, safeZoneMargin);
            if (zone)
            {
                desired = Flat(position - zone.transform.position);
                sail = HalfSail;
                if (State == AIState.Patrol) PickPatrolPoint();
            }

            desired = AvoidObstacles(forward, desired.sqrMagnitude > 0.001f ? desired.normalized : forward);

            float angle = Vector3.SignedAngle(forward, desired, Vector3.up);
            // Trocando tiros, leme mais firme: com o alvo girando em volta, o leme proporcional suave ficava ~12° atrás
            // do través e a IA nunca alinhava.
            float fullRudder = State == AIState.Broadside && !IsAvoiding && !zone ? broadsideRudderDegrees : 25f;
            command = new ShipCommand
            {
                steer = Mathf.Clamp(angle / fullRudder, -1f, 1f),
                sailDelta = System.Math.Sign(sail - ship.SailIndex),
            };
        }

        void UpdateState(bool hasTarget, float distance)
        {
            if (!hasTarget || distance > loseRange)
            {
                State = AIState.Patrol;
                return;
            }
            if (health && health.Normalized < fleeHealth)
            {
                State = AIState.Flee;
                return;
            }

            State = State switch
            {
                AIState.Patrol when distance < detectRange => AIState.Chase,
                AIState.Chase when distance < preferredRange * 1.5f => AIState.Broadside,
                AIState.Broadside when distance > preferredRange * 2.2f => AIState.Chase,
                AIState.Flee => AIState.Chase,
                _ => State,
            };
        }

        /// <summary>
        /// Corrige a distância só recarregando: com o bordo daquele lado carregado (ou quase), aponta o través direto para o alvo.
        /// Antes corrigia sempre: com o jogador colado ao lado (< preferredRange), a proa ficava ~15–35° longe dele e o través
        /// nunca alinhava (1 bordada por minuto).
        /// </summary>
        bool KeepRange(Vector3 toTargetDir)
        {
            var battery = Vector3.Dot(transform.right, toTargetDir) >= 0f ? starboard : port;
            return !battery || (battery.State != BatteryState.Ready && battery.ReloadRemaining > aimLeadTime);
        }

        /// <summary>Rumo que deixa o alvo de través; com <paramref name="keepRange"/>, corrigido para manter a distância preferida.</summary>
        Vector3 BroadsideHeading(Vector3 toTargetDir, float distance, bool keepRange)
        {
            int side = Vector3.Dot(transform.right, toTargetDir) >= 0f ? 1 : -1;
            // Com o alvo a estibordo (side = 1), o rumo é o alvo girado 90° para bombordo.
            Vector3 heading = Vector3.Cross(toTargetDir * side, Vector3.up);
            if (!keepRange) return heading;
            float bend = Mathf.Clamp((distance - preferredRange) / preferredRange, -1f, 1f) * 35f;
            Vector3 toward = bend > 0f ? toTargetDir : -toTargetDir;
            return Vector3.RotateTowards(heading, toward, Mathf.Abs(bend) * Mathf.Deg2Rad, 0f);
        }

        /// <summary>
        /// Onde o alvo vai estar (a partir do centro deste navio) quando a bala cair: a bala herda a nossa velocidade, então
        /// conta a velocidade do alvo menos a nossa, pelo tempo de voo da bala até lá (3 voltas: o tempo depende da distância).
        /// O erro de antecipação deste disparo (leadBias) erra para mais ou para menos.
        /// </summary>
        Vector3 LeadOffset(Vector3 toTarget)
        {
            var battery = starboard ? starboard : port;
            if (!battery) return toTarget;
            Vector3 relative = (targetBody ? Flat(targetBody.linearVelocity) : Vector3.zero) - (body ? Flat(body.linearVelocity) : Vector3.zero);
            relative *= 1f + leadBias;
            Vector3 lead = toTarget;
            for (int i = 0; i < 3; i++) lead = toTarget + relative * battery.FlightTime(lead.magnitude);
            return lead;
        }

        void TryFire(Vector3 lead, float leadDistance)
        {
            Vector3 leadDir = lead / leadDistance;
            int side = Vector3.Dot(transform.right, leadDir) >= 0f ? 1 : -1;
            CannonBattery battery = side > 0 ? starboard : port;
            Vector3 beam = Flat(transform.right * side).normalized;
            // Só dispara com o ponto antecipado dentro do alvo: o ângulo que meio casco ocupa nessa distância (mais o
            // espalhamento da bordada), limitado pela abertura máxima.
            float tolerance = battery
                ? Mathf.Min(fireArcDegrees, Mathf.Atan2(targetHalfLength, leadDistance) * Mathf.Rad2Deg + battery.Stats.spreadDegrees)
                : 0f;
            AimError = Vector3.Angle(beam, leadDir);
            AimTolerance = tolerance;
            bool aligned = battery && battery.State == BatteryState.Ready
                && AimError < tolerance
                && leadDistance > minFireRange && leadDistance < maxFireRange;

            if (!aligned)
            {
                alignedTime = -1f;
                return;
            }
            if (alignedTime < 0f)
            {
                alignedTime = Time.time;
                reactionDelay = Random.Range(reactionTime.x, reactionTime.y);
            }
            if (Time.time - alignedTime < reactionDelay) return;

            // Distância no través, a partir das bocas dos canhões (ficam na borda, não no centro do navio).
            Vector3 fromMuzzles = lead - Flat(battery.MuzzleOrigin - transform.position);
            float range = Vector3.Dot(fromMuzzles, beam) * (1f + Random.Range(-rangeError, rangeError));
            if (!battery.FireAtRange(range)) return;
            alignedTime = -1f;
            leadBias = Random.Range(-leadError, leadError);
        }

        Vector3 PredictTarget(float seconds)
        {
            Vector3 velocity = targetBody ? Flat(targetBody.linearVelocity) : Vector3.zero;
            return target.position + velocity * seconds;
        }

        /// <summary>Sensores à frente: se o rumo desejado bate em terra, desvia para o lado mais livre.</summary>
        Vector3 AvoidObstacles(Vector3 forward, Vector3 desired)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            float center = Probe(origin, forward);
            float left = Probe(origin, Quaternion.AngleAxis(-avoidProbeAngle, Vector3.up) * forward);
            float right = Probe(origin, Quaternion.AngleAxis(avoidProbeAngle, Vector3.up) * forward);
            float wanted = Probe(origin, desired);

            IsAvoiding = center < avoidDistance || wanted < avoidDistance;
            if (!IsAvoiding) return desired;

            float turn = right >= left ? 80f : -80f;
            return Quaternion.AngleAxis(turn, Vector3.up) * forward;
        }

        float Probe(Vector3 origin, Vector3 direction)
        {
            // Só terreno estático conta como obstáculo (corpos com Rigidbody são navios, barris, boias).
            if (Physics.SphereCast(origin, avoidProbeRadius, direction, out RaycastHit hit, avoidDistance, ~0,
                    QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                return hit.distance;
            return float.MaxValue;
        }

        void PickPatrolPoint()
        {
            // Nunca patrulha dentro de zona segura (tenta alguns pontos; no pior caso fica no centro da patrulha).
            for (int i = 0; i < 8; i++)
            {
                Vector2 r = Random.insideUnitCircle * patrolRadius;
                patrolPoint = patrolCenter + new Vector3(r.x, 0f, r.y);
                if (!SafeZone.At(patrolPoint, safeZoneMargin)) return;
            }
            patrolPoint = patrolCenter;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, preferredRange);
        }
    }
}
