using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Economy
{
    /// <summary>
    /// Onde o jogador guarda as coisas (porão, mochila, armazém do porto). Cada um se registra ao ligar. O sorteio do baú raro
    /// usa isto para não dar um esquema que o jogador já tem em qualquer lugar (antes só olhava o porão: esquema levado ao
    /// armazém podia vir de novo, inútil, no lugar do ouro de reserva).
    /// </summary>
    public static class PlayerStorage
    {
        static readonly List<Inventory> stores = new();

        public static void Register(Inventory inventory)
        {
            if (inventory && !stores.Contains(inventory)) stores.Add(inventory);
        }

        public static void Unregister(Inventory inventory) => stores.Remove(inventory);

        /// <summary>O jogador já tem o item em algum lugar dele.</summary>
        public static bool Has(ItemDefinition item)
        {
            for (int i = 0; i < stores.Count; i++)
                if (stores[i] && stores[i].Has(item)) return true;
            return false;
        }

        /// <summary>Quantos o jogador tem somando todos os lugares dele.</summary>
        public static int Count(ItemDefinition item)
        {
            int n = 0;
            for (int i = 0; i < stores.Count; i++)
                if (stores[i]) n += stores[i].Count(item);
            return n;
        }

        /// <summary>Tira <paramref name="amount"/> de onde houver (na ordem em que os lugares se registraram); devolve quanto tirou.</summary>
        public static int Take(ItemDefinition item, int amount)
        {
            int taken = 0;
            for (int i = 0; i < stores.Count && taken < amount; i++)
                if (stores[i]) taken += stores[i].Remove(item, amount - taken);
            return taken;
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => stores.Clear();
    }
}
