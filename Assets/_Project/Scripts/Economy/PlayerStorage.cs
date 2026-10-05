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

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => stores.Clear();
    }
}
