using System;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>Vida genérica: casco de navio, balsa, barril. Recebe dano via IDamageable.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public float Normalized => Current / maxHealth;
        public bool IsDead { get; private set; }
        public float LastDamageTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Debug (modo sandbox): ignora todo dano.</summary>
        public bool Invulnerable { get; set; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action<float> Healed;
        /// <summary>A vida foi trocada pela do jogo salvo (sem dano nem cura).</summary>
        public event Action Restored;

        public void Configure(float max) => maxHealth = max;

        /// <summary>Carregar o jogo: põe a vida salva (pelo menos 1), sem efeito de dano nem de cura.</summary>
        public void SetCurrent(float value)
        {
            IsDead = false;
            Current = Mathf.Clamp(value, 1f, maxHealth);
            Restored?.Invoke();
        }

        void Awake() => Current = maxHealth;

        /// <summary>
        /// Muda a vida máxima em jogo (upgrade de casco). O que aumentar entra como vida nova; diminuir só corta o excesso.
        /// </summary>
        public void SetMax(float max)
        {
            max = Mathf.Max(1f, max);
            float gained = max - maxHealth;
            maxHealth = max;
            if (IsDead) return;
            Current = Mathf.Min(max, Current + Mathf.Max(0f, gained));
            if (gained > 0f) Healed?.Invoke(gained);
        }

        /// <summary>Volta com a vida cheia (boneco de treino que se levanta; reaparecer no M4.4).</summary>
        public void Revive()
        {
            IsDead = false;
            float gained = maxHealth - Current;
            Current = maxHealth;
            if (gained > 0f) Healed?.Invoke(gained);
        }

        /// <summary>Recupera vida (conserto no porto). Não ressuscita: casco afundando não volta.</summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f || Current >= maxHealth) return;
            float healed = Mathf.Min(amount, maxHealth - Current);
            Current += healed;
            Healed?.Invoke(healed);
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead || Invulnerable || info.amount <= 0f) return;
            Current = Mathf.Max(0f, Current - info.amount);
            LastDamageTime = Time.time;
            Damaged?.Invoke(info);

            if (Current > 0f) return;
            IsDead = true;
            Died?.Invoke();
        }
    }
}
