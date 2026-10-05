using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Navio chefe: nome e capitão para o HUD (barra no topo da tela durante a luta) e escolta que entra quando o casco
    /// cai abaixo de <see cref="escortAt"/>. As chalupas da escolta são cópias de um molde desligado, feitas quando o chefe
    /// aparece (fora de combate) e só ligadas na hora: nada é instanciado no meio da luta. Elas chegam por trás do chefe,
    /// do lado oposto ao alvo, na água (testa terra em volta), e daí em diante são inimigos comuns (contam para "afundar
    /// navios"). Escolta não usada some com o chefe.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(EnemyShipAI))]
    public class ShipBoss : MonoBehaviour
    {
        static readonly List<ShipBoss> active = new();
        /// <summary>Chefes na cena (o HUD mostra a barra do que está lutando).</summary>
        public static IReadOnlyList<ShipBoss> Active => active;

        [SerializeField] string shipName = "Galeão";
        [SerializeField] string captainName = "";
        [Tooltip("Molde desligado de uma chalupa inimiga completa (casco, canhões, IA, saque).")]
        [SerializeField] GameObject escortTemplate;
        [SerializeField, Min(0)] int escortCount = 2;
        [Tooltip("A escolta entra quando o casco fica abaixo desta fração.")]
        [SerializeField, Range(0f, 1f)] float escortAt = 0.5f;
        [Tooltip("Metros atrás do chefe (do lado oposto ao alvo) onde a escolta aparece.")]
        [SerializeField] float escortDistance = 70f;

        Health health;
        EnemyShipAI ai;
        readonly List<GameObject> escorts = new();
        static readonly Collider[] probe = new Collider[16];

        public string ShipName => shipName;
        public string CaptainName => captainName;
        public Health Health => health;
        public bool EscortCalled { get; private set; }
        /// <summary>Lutando: vivo e perseguindo ou trocando tiros com o alvo.</summary>
        public bool IsEngaged => health && !health.IsDead && ai &&
                                 (ai.State == EnemyShipAI.AIState.Chase || ai.State == EnemyShipAI.AIState.Broadside);

        /// <summary>A escolta de algum chefe entrou na luta (o HUD avisa).</summary>
        public static event Action<ShipBoss> EscortArrived;

        public void Configure(string ship, string captain, GameObject escortMold, int count, float at)
        {
            shipName = ship;
            captainName = captain;
            escortTemplate = escortMold;
            escortCount = count;
            escortAt = at;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            ai = GetComponent<EnemyShipAI>();
        }

        void OnEnable()
        {
            active.Add(this);
            health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            active.Remove(this);
            health.Damaged -= OnDamaged;
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            EscortArrived = null;
        }

        void Start()
        {
            // Escolta pronta e desligada desde já (o chefe aparece longe da luta: início da cena ou reaparição).
            if (!escortTemplate) return;
            for (int i = 0; i < escortCount; i++)
            {
                var escort = Instantiate(escortTemplate, transform.position + Vector3.down * 50f, Quaternion.identity);
                escort.name = $"{escortTemplate.name}_{name}_{i + 1}";
                escort.SetActive(false);
                escorts.Add(escort);
            }
        }

        void OnDamaged(Pirates.Core.DamageInfo _)
        {
            if (EscortCalled || health.IsDead || health.Normalized > escortAt || escorts.Count == 0) return;
            EscortCalled = true;
            CallEscort();
        }

        void CallEscort()
        {
            Vector3 away = transform.forward;
            if (ai.Target)
            {
                away = transform.position - ai.Target.position;
                away.y = 0f;
            }
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away);

            for (int i = 0; i < escorts.Count; i++)
            {
                var escort = escorts[i];
                if (!escort) continue;
                float lateral = (i % 2 == 0 ? 1f : -1f) * (35f + 15f * (i / 2));
                Vector3 point = OpenWater(transform.position + away * escortDistance + side * lateral);
                point.y = escortTemplate.transform.position.y;
                Vector3 look = ai.Target ? ai.Target.position - point : -away;
                look.y = 0f;
                escort.transform.SetPositionAndRotation(point, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : -away));
                escort.SetActive(true);
            }
            escorts.Clear();
            EscortArrived?.Invoke(this);
        }

        /// <summary>O ponto, ou o mais perto dele (girando em volta do chefe) sem terra num raio de 12 m.</summary>
        Vector3 OpenWater(Vector3 wanted)
        {
            Vector3 center = transform.position;
            Vector3 offset = wanted - center;
            for (int step = 0; step < 12; step++)
            {
                float angle = (step + 1) / 2 * 30f * (step % 2 == 0 ? 1f : -1f);
                Vector3 p = center + Quaternion.AngleAxis(angle, Vector3.up) * offset;
                if (!HasLand(p)) return p;
            }
            return wanted;
        }

        static bool HasLand(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * 1f, 12f, probe, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!probe[i].attachedRigidbody) return true;
            return false;
        }

        void OnDestroy()
        {
            // Escolta que não chegou a entrar some junto (a próxima reaparição traz outra).
            foreach (var escort in escorts)
                if (escort) Destroy(escort);
            escorts.Clear();
        }
    }
}
