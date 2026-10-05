using System;
using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Loot
{
    /// <summary>
    /// O que um navio carrega e solta ao afundar, num campo de destroços único. Cada linha é sorteada
    /// separadamente (chance + quantidade); o baú raro sorteia, ao ser aberto, um esquema que o jogador ainda não tem.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Loot Table", fileName = "LootTable_")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public class Drop
        {
            public ItemDefinition item;
            [Range(0f, 1f)] public float chance = 1f;
            [Min(1)] public int minAmount = 1;
            [Min(1)] public int maxAmount = 1;
        }

        [Serializable]
        public class WeightedItem
        {
            public ItemDefinition item;
            [Min(0f)] public float weight = 1f;
        }

        public Drop[] drops = Array.Empty<Drop>();

        [Header("Baú raro")]
        [Range(0f, 1f)] public float chestChance = 0.2f;
        [Tooltip("Esquemas possíveis; peso maior = mais comum. Só sorteia os que o jogador ainda não tem.")]
        public WeightedItem[] schematics = Array.Empty<WeightedItem>();
        [Tooltip("Se o jogador já tem todos os esquemas, o baú vem com isto.")]
        public ItemStack chestFallback;

        [Header("Na água")]
        [Tooltip("Destroços que boiam onde o navio afundou (prefab gerado pelo menu de protótipo).")]
        public ShipWreck wreckPrefab;
        [Tooltip("Segundos até os destroços afundarem de vez (piscam nos últimos segundos; não somem com o saque aberto).")]
        public float lifetime = 120f;

        /// <summary>Sorteia a carga (sem o baú).</summary>
        public List<ItemStack> RollCargo()
        {
            var cargo = new List<ItemStack>();
            foreach (var drop in drops)
            {
                if (!drop.item || Random.value > drop.chance) continue;
                int amount = Random.Range(drop.minAmount, Mathf.Max(drop.minAmount, drop.maxAmount) + 1);
                cargo.Add(new ItemStack(drop.item, amount));
            }
            return cargo;
        }

        public bool RollChest() => Random.value < chestChance;

        /// <summary>
        /// Sorteia um esquema que o jogador ainda não tem (nem em <paramref name="owner"/>, nem em nenhum PlayerStorage: porão,
        /// mochila, armazém do porto); sem nenhum disponível, devolve o prêmio reserva.
        /// </summary>
        public ItemStack RollSchematic(Inventory owner)
        {
            float total = 0f;
            foreach (var s in schematics)
                if (IsAvailable(s, owner)) total += s.weight;

            float roll = Random.value * total;
            foreach (var s in schematics)
            {
                if (!IsAvailable(s, owner)) continue;
                roll -= s.weight;
                if (roll <= 0f) return new ItemStack(s.item, 1);
            }
            return chestFallback;
        }

        static bool IsAvailable(WeightedItem s, Inventory owner) =>
            s.item && s.weight > 0f && !(owner && owner.Has(s.item)) && !PlayerStorage.Has(s.item);
    }
}
