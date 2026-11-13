using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    /// <summary>
    /// Navio mercante (G, passo 1, 03/11; plano de 16/10: "mercantes neutros com carga boa"): neutro, sem canhões, navega de porto em porto
    /// (pontos de rota no mar perto de cada ilha) a meia vela, desviando de terra como a IA dos piratas. Levou tiro: foge do atirador a vela
    /// cheia por <see cref="fleeSeconds"/> (ou até ficar longe) e depois volta à rota. Afundado, deixa os destroços com a carga (LootDropper) e
    /// avisa <see cref="Sunk"/> e o <see cref="MerchantSunkEvent"/>; o primeiro tiro do jogador avisa <see cref="AttackedByPlayer"/> (o
    /// nível de procurado vai ouvir isso, passo 2). Pilota pelo mesmo IShipInput dos outros navios.
    /// </summary>
    public class MerchantShip : MonoBehaviour, IShipInput
    {
        public enum Mode { Sailing, Fleeing }

        static readonly List<MerchantShip> active = new();
        public static IReadOnlyList<MerchantShip> Active => active;

        /// <summary>O jogador atirou num mercante (uma vez por fuga).</summary>
        public static event Action<MerchantShip> AttackedByPlayer;
        /// <summary>Um mercante afundou (com quem afundou ser o jogador ou não).</summary>
        public static event Action<MerchantShip, bool> Sunk;

        [SerializeField] string displayName = "Mercante";
        [Tooltip("Pontos de rota no mar (perto dos portos e ilhas): vai de um a outro, sorteado.")]
        [SerializeField] Vector3[] ports = Array.Empty<Vector3>();
        [Tooltip("O navio do jogador (para saber quem atirou).")]
        [SerializeField] Transform player;
        [SerializeField] float arriveRadius = 45f;
        [Header("Fuga")]
        [SerializeField] float fleeSeconds = 40f, fleeDistance = 220f;
        [Header("Desvio de terra")]
        [SerializeField] float avoidDistance = 30f, avoidProbeAngle = 35f, avoidProbeRadius = 3f;

        // Índices de ShipStats.sailLevels: 0 Ré, 1 Recolhidas, 2 Meia vela, 3 Vela cheia.
        const int HalfSail = 2, FullSail = 3;

        [Header("Encalhado")]
        [Tooltip("Mais lento que isto (m/s) com a vela aberta por stuckSeconds = encalhado: ré com o leme todo por reverseSeconds.")]
        [SerializeField] float stuckSpeed = 1f, stuckSeconds = 4f, reverseSeconds = 5f;

        ShipController ship;
        Rigidbody body;
        float stuckFor, reverseUntil, reverseSteer;
        Health health;
        ShipCommand command;
        int destination = -1;
        Vector3 threat;
        float fleeUntil;
        bool hitByPlayer;

        public Mode Current { get; private set; }
        public string DisplayName => displayName;
        public bool IsDead => health && health.IsDead;
        /// <summary>Já levou tiro do jogador nesta viagem.</summary>
        public bool HitByPlayer => hitByPlayer;

        public ShipCommand GetCommand() => command;

        public void Configure(string label, Vector3[] routePorts, Transform playerShip)
        {
            displayName = label;
            ports = routePorts;
            player = playerShip;
        }

        void Awake()
        {
            ship = GetComponent<ShipController>();
            body = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            active.Add(this);
            if (health)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
            if (TryGetComponent(out AimTarget aim)) aim.Exposed = true;
            if (destination < 0) PickDestination();
        }

        void OnDisable()
        {
            active.Remove(this);
            if (health)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            AttackedByPlayer = null;
            Sunk = null;
        }

        bool IsPlayer(GameObject source) => source && player && source.transform.root == player.root;

        void OnDamaged(DamageInfo info)
        {
            // Antes de olhar se afundou: o tiro que afunda de uma vez também é do jogador (o naufrágio conta como crime dele).
            if (IsPlayer(info.source) && !hitByPlayer)
            {
                hitByPlayer = true;
                AttackedByPlayer?.Invoke(this);
            }
            if (health.IsDead) return;
            Current = Mode.Fleeing;
            fleeUntil = Time.time + fleeSeconds;
            threat = info.source ? info.source.transform.position : transform.position - transform.forward * 50f;
        }

        void OnDied()
        {
            if (TryGetComponent(out AimTarget aim)) aim.Exposed = false;
            Sunk?.Invoke(this, hitByPlayer);
            EventBus.Publish(new MerchantSunkEvent { ship = gameObject, byPlayer = hitByPlayer });
        }

        void Update()
        {
            if ((health && health.IsDead) || !ship.enabled)
            {
                command = default;
                return;
            }
            Vector3 position = transform.position;
            Vector3 forward = Flat(transform.forward).normalized;
            Vector3 desired;
            int sail;
            if (Current == Mode.Fleeing)
            {
                // A ameaça é quem atirou: se for um navio, acompanha onde ele está agora.
                if (player && IsPlayerThreat()) threat = player.position;
                Vector3 away = Flat(position - threat);
                if (Time.time > fleeUntil || away.magnitude > fleeDistance)
                {
                    Current = Mode.Sailing;
                    // Viagem nova (12/11): atacar de novo conta de novo (antes só o primeiro ataque da vida do navio dava procura).
                    hitByPlayer = false;
                    PickDestination();
                }
                desired = away;
                sail = FullSail;
            }
            else
            {
                if (destination < 0 || Flat(ports[destination] - position).magnitude < arriveRadius) PickDestination();
                desired = destination >= 0 ? Flat(ports[destination] - position) : forward;
                sail = HalfSail;
            }

            desired = AvoidObstacles(position, forward, desired.sqrMagnitude > 0.001f ? desired.normalized : forward);
            float angle = Vector3.SignedAngle(forward, desired, Vector3.up);

            // Encalhado (encostado numa pedra o desvio não enxerga mais nada: a sonda nasce dentro dela): dá ré com o leme todo
            // por uns segundos e volta à rota.
            if (Time.time < reverseUntil)
            {
                command = new ShipCommand { steer = reverseSteer, sailDelta = Math.Sign(0 - ship.SailIndex) };
                return;
            }
            if (body && body.linearVelocity.magnitude < stuckSpeed && ship.SailIndex >= HalfSail) stuckFor += Time.deltaTime;
            else stuckFor = 0f;
            if (stuckFor > stuckSeconds)
            {
                stuckFor = 0f;
                reverseUntil = Time.time + reverseSeconds;
                reverseSteer = Random.value < 0.5f ? -1f : 1f;
            }

            command = new ShipCommand
            {
                steer = Mathf.Clamp(angle / 25f, -1f, 1f),
                sailDelta = Math.Sign(sail - ship.SailIndex),
            };
        }

        bool IsPlayerThreat() => hitByPlayer && Flat(player.position - threat).magnitude < 80f;

        /// <summary>Próximo porto: qualquer um que não seja onde já está (nem perto).</summary>
        void PickDestination()
        {
            if (ports.Length == 0)
            {
                destination = -1;
                return;
            }
            Vector3 position = transform.position;
            for (int i = 0; i < 8; i++)
            {
                int pick = Random.Range(0, ports.Length);
                if (Flat(ports[pick] - position).magnitude > arriveRadius * 2f)
                {
                    destination = pick;
                    return;
                }
            }
            destination = (destination + 1) % ports.Length;
        }

        Vector3 AvoidObstacles(Vector3 position, Vector3 forward, Vector3 desired)
        {
            // Logo acima da água: pega também rochedos baixos.
            Vector3 origin = new(position.x, 0.6f, position.z);
            float center = Probe(origin, forward);
            float wanted = Probe(origin, desired);
            if (center >= avoidDistance && wanted >= avoidDistance) return desired;
            float left = Probe(origin, Quaternion.AngleAxis(-avoidProbeAngle, Vector3.up) * forward);
            float right = Probe(origin, Quaternion.AngleAxis(avoidProbeAngle, Vector3.up) * forward);
            return Quaternion.AngleAxis(right >= left ? 80f : -80f, Vector3.up) * forward;
        }

        float Probe(Vector3 origin, Vector3 direction)
        {
            // Só terreno estático conta (corpos com Rigidbody são navios, barris, boias).
            if (Physics.SphereCast(origin, avoidProbeRadius, direction, out RaycastHit hit, avoidDistance, ~0,
                    QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                return hit.distance;
            return float.MaxValue;
        }

        /// <summary>Testes: o porto para onde está indo.</summary>
        public Vector3 Destination => destination >= 0 ? ports[destination] : transform.position;

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
