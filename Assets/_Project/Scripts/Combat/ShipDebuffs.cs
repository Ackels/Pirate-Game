using System;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Estragos da munição especial num navio inimigo (M9): bala de corrente rasga as velas (velocidade máxima menor) e
    /// metralha abate a tripulação (recarga mais lenta). Somam a cada acerto até o limite e se recuperam devagar depois de um
    /// tempo sem acerto daquele tipo (velas e tripulação "consertadas" a bordo: para manter o efeito é preciso continuar acertando;
    /// decidido pelo Jhonata em 05/10). O que reaparece é uma cópia nova, sem estragos.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(ShipController))]
    public class ShipDebuffs : MonoBehaviour
    {
        [Tooltip("Máximo de velas rasgadas (0,5 = metade da velocidade máxima).")]
        [SerializeField, Range(0f, 0.9f)] float maxSailDamage = 0.5f;
        [Tooltip("Máximo de tripulação abatida (0,6 = recarga 60% mais lenta).")]
        [SerializeField, Range(0f, 2f)] float maxCrewDamage = 0.6f;
        [Tooltip("Segundos sem acerto daquele tipo antes de começar a recuperar.")]
        [SerializeField, Min(0f)] float recoverDelay = 10f;
        [Tooltip("Quanto se recupera por segundo (0,03 = 3% da vela ou da tripulação por segundo).")]
        [SerializeField, Min(0f)] float recoverPerSecond = 0.03f;

        Health health;
        ShipController ship;
        CannonBattery[] batteries;
        float lastSailHit, lastCrewHit;

        /// <summary>Fração das velas rasgada (0 = inteiras).</summary>
        public float SailDamage { get; private set; }
        /// <summary>Fração da tripulação abatida (0 = completa).</summary>
        public float CrewDamage { get; private set; }

        /// <summary>Mudou o estrago (para o HUD).</summary>
        public event Action Changed;

        void Awake()
        {
            health = GetComponent<Health>();
            ship = GetComponent<ShipController>();
            batteries = GetComponentsInChildren<CannonBattery>(true);
        }

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(DamageInfo info)
        {
            if (info.sailDamage <= 0f && info.crewDamage <= 0f) return;
            if (info.sailDamage > 0f) lastSailHit = Time.time;
            if (info.crewDamage > 0f) lastCrewHit = Time.time;
            SailDamage = Mathf.Min(maxSailDamage, SailDamage + info.sailDamage);
            CrewDamage = Mathf.Min(maxCrewDamage, CrewDamage + info.crewDamage);
            ApplyEffects();
        }

        void Update()
        {
            if (SailDamage <= 0f && CrewDamage <= 0f) return;
            float heal = recoverPerSecond * Time.deltaTime;
            if (SailDamage > 0f && Time.time - lastSailHit > recoverDelay) SailDamage = Mathf.Max(0f, SailDamage - heal);
            if (CrewDamage > 0f && Time.time - lastCrewHit > recoverDelay) CrewDamage = Mathf.Max(0f, CrewDamage - heal);
            ApplyEffects();
        }

        void ApplyEffects()
        {
            ship.SpeedMultiplier = 1f - SailDamage;
            foreach (var battery in batteries)
                if (battery) battery.ReloadMultiplier = 1f + CrewDamage;
            Changed?.Invoke();
        }
    }
}
