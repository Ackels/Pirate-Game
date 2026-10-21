using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Solta um saco de saque com itens fixos ao morrer (16/10, pedido do Jhonata: cada espadachim da Guarda Negra deixa 1 ração, para o
    /// capitão chegar ao Barba-Negra com um pouco de vida). O personagem pega passando por cima, como o saco dos caixotes. Levantou de novo
    /// (acampamento repovoado) e caiu: solta outro.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DropOnDeath : MonoBehaviour
    {
        [SerializeField] LootSack sackPrefab;
        [SerializeField] ItemStack[] items = System.Array.Empty<ItemStack>();

        Health health;

        public void Configure(LootSack sack, ItemStack[] drop)
        {
            sackPrefab = sack;
            items = drop ?? System.Array.Empty<ItemStack>();
        }

        void Awake() => health = GetComponent<Health>();
        void OnEnable() => health.Died += Drop;
        void OnDisable() => health.Died -= Drop;

        void Drop()
        {
            if (!sackPrefab || items.Length == 0) return;
            // Equipamento (E2: o machado do brutamontes, o espadão do Barba-Negra) só cai uma vez: quem já tem não ganha outro.
            var drop = new List<ItemStack>(items.Length);
            foreach (var stack in items)
                if (stack.item && !(stack.item.category == ItemCategory.Gear && PlayerStorage.Has(stack.item))) drop.Add(stack);
            if (drop.Count == 0) return;
            var sack = Instantiate(sackPrefab, transform.position + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            sack.Setup(drop);
        }
    }
}
