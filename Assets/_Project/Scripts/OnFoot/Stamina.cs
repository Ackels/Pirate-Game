using System;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Fôlego do capitão a pé (pacote A, passo 6, 09/10): rolar e segurar a guarda gastam; recupera sozinho depois de um instante sem
    /// gastar. Aparar devolve um pouco. Zerado com a guarda erguida = guarda quebrada (CharacterCombat). Não é salvo: começa cheio ao
    /// desembarcar, ao carregar e ao acordar na praia. Quem não tem este componente (piratas da IA) não tem limite.
    /// </summary>
    public class Stamina : MonoBehaviour
    {
        [SerializeField] float max = 100f;
        [Tooltip("Recupera por segundo, depois de regenDelay segundos sem gastar (segurando a guarda não recupera).")]
        [SerializeField] float regenPerSecond = 18f, regenDelay = 0.6f;

        [Header("Custos")]
        [Tooltip("Um rolamento (com menos que isso não rola).")]
        [SerializeField] float dodgeCost = 25f;
        [Tooltip("Guarda erguida: por segundo e por golpe segurado.")]
        [SerializeField] float guardPerSecond = 6f, blockHitCost = 6f;
        [Tooltip("Aparar devolve (quem sabe aparar fica em vantagem).")]
        [SerializeField] float parryRefund = 15f;

        [Header("Guarda quebrada (fôlego zerado com a guarda erguida)")]
        [Tooltip("Segundos sem poder erguer a guarda de novo.")]
        [SerializeField] float guardBreakSeconds = 1.2f;
        [Tooltip("Fração do dano do golpe que quebrou a guarda.")]
        [SerializeField, Range(0f, 1f)] float guardBreakDamage = 0.5f;

        float lastSpend = float.NegativeInfinity;

        public float Current { get; private set; }
        /// <summary>Máximo com o bônus de comida (caldeirada, 30/10).</summary>
        public float Max => max + BonusMax;
        /// <summary>Fôlego a mais no máximo (prato da taverna), posto por quem cuida das refeições.</summary>
        public float BonusMax { get; set; }
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public float DodgeCost => dodgeCost;
        public float GuardPerSecond => guardPerSecond;
        public float BlockHitCost => blockHitCost;
        public float ParryRefund => parryRefund;
        public float GuardBreakSeconds => guardBreakSeconds;
        public float GuardBreakDamage => guardBreakDamage;
        public bool CanDodge => Current >= dodgeCost;
        /// <summary>Última vez que faltou fôlego para algo (a barra pisca).</summary>
        public float LastDenied { get; private set; } = float.NegativeInfinity;

        /// <summary>Faltou fôlego para uma ação (HUD).</summary>
        public event Action Denied;

        void Awake() => Current = Max;

        void OnEnable() => Refill();

        public void Refill()
        {
            Current = Max;
            lastSpend = float.NegativeInfinity;
        }

        /// <summary>Gasta se tiver tudo; senão avisa (Denied) e não gasta.</summary>
        public bool TrySpend(float amount)
        {
            if (Current < amount)
            {
                Deny();
                return false;
            }
            Spend(amount);
            return true;
        }

        /// <summary>Gasta o que der (até zerar) e segura a recuperação por regenDelay.</summary>
        public void Spend(float amount)
        {
            Current = Mathf.Max(0f, Current - amount);
            lastSpend = Time.time;
        }

        public void Refund(float amount) => Current = Mathf.Min(Max, Current + amount);

        public void Deny()
        {
            LastDenied = Time.time;
            Denied?.Invoke();
        }

        void Update()
        {
            if (Current > Max) Current = Max;   // o prato acabou
            if (Current < Max && Time.time - lastSpend >= regenDelay) Current = Mathf.Min(Max, Current + regenPerSecond * Time.deltaTime);
        }
    }
}
