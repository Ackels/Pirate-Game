using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Saco de saque no chão (caiu de um caixote ou barril quebrado, ou o capitão largou itens da mochila). O personagem pega passando
    /// por cima o que couber na mochila (LandLooter); o resto fica no saco. Some quando esvazia. O saco largado pelo capitão (21/10) tem
    /// prazo: pisca no fim e some numa nuvem de poeira (sem prazo, dava para guardar saque sem limite pelo mapa), e só pode ser pego de
    /// volta depois que o dono se afasta (senão voltava para a mochila na hora).
    /// </summary>
    public class LootSack : MonoBehaviour
    {
        static readonly List<LootSack> active = new();
        public static IReadOnlyList<LootSack> Active => active;

        [Tooltip("Visual que balança e gira devagar (chama atenção).")]
        [SerializeField] Transform visual;

        [Tooltip("Últimos segundos do prazo em que o saco pisca.")]
        [SerializeField] float blinkSeconds = 20f;

        readonly List<ItemStack> contents = new();
        Vector3 visualRest;
        float expiresAt = float.PositiveInfinity;

        public IReadOnlyList<ItemStack> Contents => contents;
        /// <summary>Tem prazo (largado pelo capitão).</summary>
        public bool Expires => !float.IsPositiveInfinity(expiresAt);
        public float SecondsLeft => Mathf.Max(0f, expiresAt - Time.time);
        /// <summary>Quem largou: não pega de volta enquanto não se afastar (LandLooter chama Release).</summary>
        public Transform HeldFrom { get; private set; }

        public void Configure(Transform bobbing) => visual = bobbing;

        public void Setup(List<ItemStack> cargo)
        {
            contents.Clear();
            contents.AddRange(cargo);
        }

        /// <summary>Saco largado: some em <paramref name="lifetime"/> s; <paramref name="owner"/> só pega depois de se afastar.</summary>
        public void Setup(List<ItemStack> cargo, float lifetime, Transform owner)
        {
            Setup(cargo);
            expiresAt = Time.time + Mathf.Max(1f, lifetime);
            HeldFrom = owner;
        }

        /// <summary>Junta mais itens no mesmo saco (largar de novo perto dele). O prazo não renova.</summary>
        public void Add(ItemStack stack)
        {
            for (int i = 0; i < contents.Count; i++)
                if (contents[i].item == stack.item)
                {
                    contents[i] = new ItemStack(stack.item, contents[i].amount + stack.amount);
                    return;
                }
            contents.Add(stack);
        }

        public void Release() => HeldFrom = null;

        public void Hold(Transform owner) => HeldFrom = owner;

        void Awake()
        {
            if (visual) visualRest = visual.localPosition;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        /// <summary>Passa para o inventário o que couber; devolve o que entrou e diz se sobrou algo.</summary>
        public List<ItemStack> TakeInto(Inventory inventory, out bool leftovers)
        {
            var moved = new List<ItemStack>();
            for (int i = contents.Count - 1; i >= 0; i--)
            {
                var stack = contents[i];
                int n = inventory.Add(stack.item, stack.amount);
                if (n <= 0) continue;
                moved.Add(new ItemStack(stack.item, n));
                if (n >= stack.amount) contents.RemoveAt(i);
                else contents[i] = new ItemStack(stack.item, stack.amount - n);
            }
            leftovers = contents.Count > 0;
            if (!leftovers) Destroy(gameObject);
            return moved;
        }

        void Update()
        {
            if (Expires) Countdown();
            if (!visual) return;
            visual.localPosition = visualRest + Vector3.up * (0.06f * Mathf.Sin(Time.time * 2.5f));
            visual.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
        }

        void Countdown()
        {
            float left = expiresAt - Time.time;
            if (left <= 0f)
            {
                if (Services.TryGet(out CombatEffects fx)) fx.Unearth(transform.position, false);
                Destroy(gameObject);
                return;
            }
            if (!visual) return;
            // Pisca cada vez mais rápido no fim do prazo.
            bool on = left > blinkSeconds || Mathf.Repeat(Time.time * Mathf.Lerp(6f, 2f, left / blinkSeconds), 1f) < 0.6f;
            if (visual.gameObject.activeSelf != on) visual.gameObject.SetActive(on);
        }
    }
}
