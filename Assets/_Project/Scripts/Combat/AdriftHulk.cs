using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Navio tomado na abordagem e largado à deriva (04/11, pedido do Jhonata: "se não afundar, tem que sumir depois de um tempo, senão
    /// não aparece outro barco"). A tripulação dele já se rendeu: o casco aberto pelos ganchos e pela luta embarca água e ele afunda sozinho
    /// <see cref="seconds"/> depois (ou antes, a tiros). Afundado de qualquer jeito, conta como navio afundado para as missões e a História
    /// (a IA dele está desligada e não avisa) e o respawner traz outro; a fama já veio na abordagem (aqui vai 0) e os destroços só têm
    /// madeira (LootDropper.Plundered).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class AdriftHulk : MonoBehaviour
    {
        [SerializeField] float seconds = 90f;

        Health health;
        EnemyShipAI ai;
        float sinkAt;
        bool reported;

        /// <summary>Segundos até afundar sozinho.</summary>
        public float SecondsLeft => Mathf.Max(0f, sinkAt - Time.time);

        public void Begin(float afterSeconds)
        {
            seconds = afterSeconds;
            sinkAt = Time.time + seconds;
            enabled = true;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            ai = GetComponent<EnemyShipAI>();
            sinkAt = Time.time + seconds;
        }

        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void Update()
        {
            if (health.IsDead || Time.time < sinkAt) return;
            health.Invulnerable = false;
            health.TakeDamage(new DamageInfo { amount = health.Max * 4f, point = transform.position });
        }

        void OnDied()
        {
            if (reported) return;
            reported = true;
            EventBus.Publish(new ShipSunkEvent
            {
                ship = gameObject, targetId = ai ? ai.TargetId : null, fame = 0, crown = ai && ai.Crown,
            });
        }
    }
}
