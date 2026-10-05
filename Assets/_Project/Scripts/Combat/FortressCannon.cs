using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Canhão fixo da Fortaleza Costeira: gira para o inimigo vivo mais próximo ao alcance (antecipando o movimento)
    /// e dispara a bateria (CannonBattery, lado estibordo = eixo "right" deste objeto) com FireAtRange.
    /// Num forte inimigo (<see cref="TargetPlayer"/>), mira no navio do jogador em vez dos inimigos; não atira nele dentro
    /// de uma zona segura (porto, navio ancorado com o jogador em terra).
    /// </summary>
    public class FortressCannon : MonoBehaviour
    {
        [SerializeField] CannonBattery battery;
        [SerializeField] float range = 170f;
        [Tooltip("Graus por segundo que o canhão gira.")]
        [SerializeField] float turnSpeed = 45f;
        [Tooltip("Tolerância (graus) para disparar.")]
        [SerializeField] float fireArc = 3f;
        [Tooltip("Erro aleatório do alcance (fração).")]
        [SerializeField, Range(0f, 0.3f)] float rangeError = 0.05f;

        [Tooltip("Forte inimigo: o navio do jogador (vazio = Fortaleza do jogador, mira nos inimigos).")]
        [SerializeField] Transform playerShip;

        Health playerHealth;
        Rigidbody playerBody;

        public EnemyShipAI Target { get; private set; }
        /// <summary>Em quem está mirando agora (inimigo ou o navio do jogador; null se ninguém ao alcance).</summary>
        public Transform Aim { get; private set; }

        /// <summary>Forte inimigo: passa a mirar no navio do jogador.</summary>
        public void TargetPlayer(Transform ship) => playerShip = ship;

        void Awake()
        {
            if (!playerShip) return;
            playerShip.TryGetComponent(out playerHealth);
            playerShip.TryGetComponent(out playerBody);
        }

        public void Configure(CannonBattery cannonBattery, float fireRange)
        {
            battery = cannonBattery;
            range = fireRange;
        }

        void Update()
        {
            Rigidbody body = null;
            if (playerShip)
            {
                Target = null;
                Aim = PlayerInSight() ? playerShip : null;
                body = playerBody;
            }
            else
            {
                Target = FindTarget();
                Aim = Target ? Target.transform : null;
                if (Target) Target.TryGetComponent(out body);
            }
            if (!Aim || !battery) return;

            Vector3 origin = battery.transform.position;
            Vector3 toTarget = Flat(Aim.position - origin);
            // Onde o alvo vai estar quando a bala cair (tempo de voo de verdade, com a elevação; 3 voltas porque o tempo
            // depende da distância).
            Vector3 toAim = toTarget;
            if (body)
            {
                Vector3 velocity = Flat(body.linearVelocity);
                for (int i = 0; i < 3; i++) toAim = toTarget + velocity * battery.FlightTime(toAim.magnitude);
            }
            if (toAim.sqrMagnitude < 1f) return;
            // O lado de tiro é o "right": o canhão olha 90° à esquerda do alvo.
            var desired = Quaternion.LookRotation(toAim) * Quaternion.Euler(0f, -90f, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, desired) > fireArc) return;
            battery.FireAtRange(toAim.magnitude * (1f + Random.Range(-rangeError, rangeError)));
        }

        bool PlayerInSight()
        {
            if (!playerShip.gameObject.activeInHierarchy || (playerHealth && playerHealth.IsDead)) return false;
            if (Flat(playerShip.position - transform.position).magnitude > range) return false;
            return !SafeZone.At(playerShip.position);
        }

        EnemyShipAI FindTarget()
        {
            EnemyShipAI best = null;
            float bestDistance = range;
            for (int i = 0; i < EnemyShipAI.Active.Count; i++)
            {
                var ai = EnemyShipAI.Active[i];
                if (!ai || ai.IsDead) continue;
                float d = Flat(ai.transform.position - transform.position).magnitude;
                if (d > bestDistance) continue;
                best = ai;
                bestDistance = d;
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
