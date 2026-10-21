using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// O que o capitão usa a pé (ração, cartuchos, chumbo, bomba) e o "guardar a mochila" no armazém do porto (21/10, pedido do Jhonata:
    /// antes a mochila só esvaziava embarcando, e só depois a carga ia do porão para o armazém).
    /// </summary>
    public static class CaptainSupplies
    {
        /// <summary>Item que o capitão gasta a pé (fica na mochila no "guardar o saque").</summary>
        public static bool IsSupply(Component captain, ItemDefinition item)
        {
            if (!captain || !item) return false;
            if (captain.TryGetComponent(out FoodEater food) && food.Food == item) return true;
            if (captain.TryGetComponent(out BombThrower bombs) && bombs.Bomb == item) return true;
            return captain.TryGetComponent(out PistolAmmo ammo) && ammo.Uses(item);
        }

        /// <summary>
        /// Passa da mochila para <paramref name="to"/> (o armazém não tem limite; o que não couber fica). <paramref name="supplies"/> falso =
        /// só o saque (ração, munição e bombas ficam). Devolve o que passou.
        /// </summary>
        public static List<ItemStack> Store(Component captain, Inventory backpack, Inventory to, bool supplies)
        {
            var moved = new List<ItemStack>();
            if (!backpack || !to) return moved;
            foreach (var item in new List<ItemDefinition>(backpack.Items))
            {
                if (!supplies && IsSupply(captain, item)) continue;
                int n = Move(backpack, to, item, backpack.Count(item));
                if (n > 0) moved.Add(new ItemStack(item, n));
            }
            return moved;
        }

        /// <summary>Move até <paramref name="amount"/> de um item; devolve quantos passaram.</summary>
        public static int Move(Inventory from, Inventory to, ItemDefinition item, int amount)
        {
            if (!from || !to || !item || amount <= 0) return 0;
            int n = to.Add(item, Mathf.Min(amount, from.Count(item)));
            if (n > 0) from.Remove(item, n);
            return n;
        }
    }
}
