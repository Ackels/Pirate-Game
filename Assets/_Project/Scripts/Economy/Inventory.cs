using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Economy
{
    /// <summary>
    /// Porão do navio: quantidade de cada item e espaço ocupado (cada item tem um espaço por unidade).
    /// Gastar, salvar e porão por classe de navio entram no M3/M6.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Tooltip("Espaço total do porão. Ex.: 1 madeira = 1, 1 pólvora = 2, 1 ferro = 3, 20 de ouro = 1.")]
        [SerializeField, Min(0f)] float capacity = 40f;
        [Tooltip("Sem limite de espaço (armazém da base).")]
        [SerializeField] bool unlimited;

        readonly Dictionary<ItemDefinition, int> counts = new();
        readonly List<ItemDefinition> order = new();
        readonly Dictionary<ItemDefinition, int> limits = new();

        /// <summary>Itens na ordem em que entraram no porão (o HUD lista nesta ordem).</summary>
        public IReadOnlyList<ItemDefinition> Items => order;
        public float Capacity => capacity;
        public float UsedSpace { get; private set; }
        public float FreeSpace => unlimited ? float.PositiveInfinity : Mathf.Max(0f, capacity - UsedSpace);
        public bool Unlimited => unlimited;
        /// <summary>Sobe a cada mudança (itens, quantidades ou espaço): o HUD só remonta a lista quando muda.</summary>
        public int Version { get; private set; }

        public event Action<ItemStack> Added;

        public void Configure(float holdCapacity, bool unlimitedSpace = false)
        {
            capacity = holdCapacity;
            unlimited = unlimitedSpace;
            Version++;
        }

        /// <summary>
        /// Máximo de unidades de um item, além do espaço (beliches da tripulação). Negativo tira o limite. Não corta o que já
        /// está no porão: só barra entrar mais.
        /// </summary>
        public void SetLimit(ItemDefinition item, int max)
        {
            if (!item) return;
            if (max < 0) limits.Remove(item);
            else limits[item] = max;
            Version++;
        }

        public int Count(ItemDefinition item) => item && counts.TryGetValue(item, out int n) ? n : 0;

        public bool Has(ItemDefinition item) => Count(item) > 0;

        /// <summary>Quantas unidades ainda cabem (esquema já conhecido = 0).</summary>
        public int MaxFit(ItemDefinition item)
        {
            if (!item) return 0;
            if (item.IsUnique) return Has(item) ? 0 : 1;
            int byLimit = limits.TryGetValue(item, out int max) ? Mathf.Max(0, max - Count(item)) : int.MaxValue;
            if (item.spacePerUnit <= 0f || unlimited) return byLimit;
            // Folga pequena para 0,05 × 20 não dar 0,9999 e perder uma unidade.
            return Mathf.Min(byLimit, Mathf.FloorToInt(FreeSpace / item.spacePerUnit + 1e-4f));
        }

        /// <summary>Põe no porão o quanto couber e devolve quantas unidades entraram.</summary>
        public int Add(ItemDefinition item, int amount)
        {
            amount = Mathf.Min(amount, MaxFit(item));
            if (amount <= 0) return 0;

            if (!counts.ContainsKey(item)) order.Add(item);
            counts[item] = Count(item) + amount;
            UsedSpace += item.spacePerUnit * amount;
            Version++;
            Added?.Invoke(new ItemStack(item, amount));
            return amount;
        }

        /// <summary>
        /// Carregar o jogo: troca todo o conteúdo pelo salvo, sem o evento Added (o HUD não mostra "pegou") e sem cortar
        /// pelo espaço (nada salvo se perde, mesmo que o limite tenha mudado).
        /// </summary>
        public void SetContents(IEnumerable<ItemStack> stacks)
        {
            counts.Clear();
            order.Clear();
            UsedSpace = 0f;
            foreach (var stack in stacks)
            {
                if (!stack.item || stack.amount <= 0) continue;
                bool known = counts.ContainsKey(stack.item);
                if (known && stack.item.IsUnique) continue;
                if (!known) order.Add(stack.item);
                int amount = stack.item.IsUnique ? 1 : stack.amount;
                counts[stack.item] = Count(stack.item) + amount;
                UsedSpace += stack.item.spacePerUnit * amount;
            }
            Version++;
        }

        /// <summary>Tira do porão e devolve quantas unidades saíram.</summary>
        public int Remove(ItemDefinition item, int amount)
        {
            amount = Mathf.Min(amount, Count(item));
            if (amount <= 0) return 0;

            int left = Count(item) - amount;
            if (left > 0) counts[item] = left;
            else
            {
                counts.Remove(item);
                order.Remove(item);
            }
            UsedSpace = Mathf.Max(0f, UsedSpace - item.spacePerUnit * amount);
            Version++;
            return amount;
        }
    }
}
