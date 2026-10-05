using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Saco de saque no chão (caiu de um caixote ou barril quebrado). O personagem pega passando por cima o que couber
    /// na mochila (LandLooter); o resto fica no saco. Some quando esvazia.
    /// </summary>
    public class LootSack : MonoBehaviour
    {
        static readonly List<LootSack> active = new();
        public static IReadOnlyList<LootSack> Active => active;

        [Tooltip("Visual que balança e gira devagar (chama atenção).")]
        [SerializeField] Transform visual;

        readonly List<ItemStack> contents = new();
        Vector3 visualRest;

        public IReadOnlyList<ItemStack> Contents => contents;

        public void Configure(Transform bobbing) => visual = bobbing;

        public void Setup(List<ItemStack> cargo)
        {
            contents.Clear();
            contents.AddRange(cargo);
        }

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
            if (!visual) return;
            visual.localPosition = visualRest + Vector3.up * (0.06f * Mathf.Sin(Time.time * 2.5f));
            visual.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
        }
    }
}
