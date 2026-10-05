using System;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Progression
{
    /// <summary>
    /// Tripulação do navio do jogador: os tripulantes são o item de sobreviventes no porão (o save já guarda), limitado pelos
    /// beliches. Quanto mais gente a bordo, mais rápida a recarga e melhor a manobra (o ShipUpgrades aplica nos stats); os
    /// tiros no casco matam tripulantes. Zero tripulantes = o navio sem bônus nenhum.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Health))]
    public class ShipCrew : MonoBehaviour
    {
        [SerializeField] CrewSettings settings;

        Inventory hold;
        Health health;
        int seenVersion = -1;
        int berthsOverride = -1;

        public CrewSettings Settings => settings;
        public ItemDefinition CrewItem => settings ? settings.crewItem : null;
        public int Count { get; private set; }
        /// <summary>Beliches do casco atual (a classe do navio pode mudar; sem ela, os do CrewSettings).</summary>
        public int Berths => berthsOverride >= 0 ? berthsOverride : settings ? settings.berths : 0;
        public int FreeBerths => Mathf.Max(0, Berths - Count);
        /// <summary>Quanto dos beliches está ocupado (0 a 1): o bônus é proporcional.</summary>
        public float Fraction => Berths > 0 ? Mathf.Clamp01((float)Count / Berths) : 0f;
        /// <summary>Multiplica o tempo de recarga (1 = sem bônus).</summary>
        public float ReloadMultiplier => settings ? 1f - settings.reloadBonusAtFull * Fraction : 1f;
        /// <summary>Multiplica aceleração e giro (1 = sem bônus).</summary>
        public float HandlingMultiplier => settings ? 1f + settings.handlingBonusAtFull * Fraction : 1f;

        /// <summary>O número a bordo mudou (resgate, contratação, baixa, jogo carregado).</summary>
        public event Action Changed;
        /// <summary>Tripulantes mortos por um tiro no casco.</summary>
        public event Action<int> Lost;

        public void Configure(CrewSettings crewSettings) => settings = crewSettings;

        /// <summary>Navio novo (outra classe): outro número de beliches. Quem já estava a bordo não sai.</summary>
        public void SetBerths(int berths)
        {
            berthsOverride = Mathf.Max(0, berths);
            if (CrewItem) hold.SetLimit(CrewItem, Berths);
            Changed?.Invoke();
        }

        void Awake()
        {
            hold = GetComponent<Inventory>();
            health = GetComponent<Health>();
            if (CrewItem) hold.SetLimit(CrewItem, Berths);
            Recount();
        }

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void Update()
        {
            if (hold.Version != seenVersion) Recount();
        }

        void Recount()
        {
            seenVersion = hold.Version;
            int count = hold.Count(CrewItem);
            if (count == Count) return;
            Count = count;
            Changed?.Invoke();
        }

        /// <summary>Cada tiro mata em média dano / damagePerCasualty tripulantes (a parte quebrada é sorteada).</summary>
        void OnDamaged(DamageInfo info)
        {
            if (Count <= 0 || !settings || settings.damagePerCasualty <= 0f || health.IsDead) return;
            // Bônus de patente "tripulação calejada" (M10): menos baixas por tiro.
            float expected = info.amount / settings.damagePerCasualty / (1f + CaptainFame.Bonus(PerkEffect.CrewToughnessPercent));
            int dead = Mathf.FloorToInt(expected);
            if (Random.value < expected - dead) dead++;
            dead = hold.Remove(CrewItem, Mathf.Min(dead, Count));
            if (dead <= 0) return;
            Recount();
            Lost?.Invoke(dead);
        }
    }
}
