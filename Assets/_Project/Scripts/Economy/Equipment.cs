using System;
using UnityEngine;

namespace Pirates.Economy
{
    /// <summary>Onde o capitão usa um item de equipamento (E1, 19/10). Traje e relíquia ficam para os passos E4/E5.</summary>
    public enum EquipSlot { None, Blade, Firearm, Outfit, Relic }

    /// <summary>Nome e cor de cada raridade (baús, janelas, tela de equipamento).</summary>
    public static class Rarity
    {
        public static readonly Color Common = new(0.86f, 0.84f, 0.8f);
        public static readonly Color Uncommon = new(0.5f, 0.86f, 0.45f);
        public static readonly Color Rare = new(0.45f, 0.72f, 1f);
        public static readonly Color Epic = new(0.82f, 0.52f, 1f);
        public static readonly Color Legendary = new(1f, 0.74f, 0.22f);

        public static Color ColorOf(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => Uncommon,
            ItemRarity.Rare => Rare,
            ItemRarity.Epic => Epic,
            ItemRarity.Legendary => Legendary,
            _ => Common,
        };

        public static string NameOf(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => "Incomum",
            ItemRarity.Rare => "Raro",
            ItemRarity.Epic => "Épico",
            ItemRarity.Legendary => "Lendário",
            _ => "Comum",
        };

        /// <summary>"#rrggbb" da raridade, para texto com &lt;color&gt;.</summary>
        public static string Hex(ItemRarity rarity) => "#" + ColorUtility.ToHtmlStringRGB(ColorOf(rarity));

        static readonly string[] slotNames = { "", "Lâmina", "Arma de fogo", "Traje", "Relíquia" };
        public static string SlotName(EquipSlot slot) => slotNames[(int)slot];
    }

    /// <summary>
    /// O que o capitão tem equipado (E1, 19/10): um item por lugar (lâmina, arma de fogo, traje, relíquia). Estático como o
    /// <see cref="PlayerStorage"/>: o personagem a pé fica desligado no navio e o save precisa ler e devolver mesmo assim. Item equipado
    /// que o jogador não tem mais (em nenhum <see cref="PlayerStorage"/>) não vale: quem aplica cai no padrão (o cutelo).
    /// </summary>
    public static class PlayerLoadout
    {
        static readonly ItemDefinition[] equipped = new ItemDefinition[Enum.GetValues(typeof(EquipSlot)).Length];

        /// <summary>Muda a cada troca (HUD e o personagem conferem por aqui).</summary>
        public static int Version { get; private set; }
        public static event Action Changed;

        /// <summary>O item no lugar (null = o padrão). Não confere se o jogador ainda o tem: veja <see cref="Active"/>.</summary>
        public static ItemDefinition Get(EquipSlot slot) => slot == EquipSlot.None ? null : equipped[(int)slot];

        /// <summary>O item no lugar, só se o jogador ainda o tem guardado em algum lugar.</summary>
        public static ItemDefinition Active(EquipSlot slot)
        {
            var item = Get(slot);
            return item && PlayerStorage.Has(item) ? item : null;
        }

        public static bool IsEquipped(ItemDefinition item) => item && item.equipSlot != EquipSlot.None && equipped[(int)item.equipSlot] == item;

        /// <summary>Equipa (troca o que estava no lugar dele). Só item de equipamento que o jogador tem.</summary>
        public static bool Equip(ItemDefinition item)
        {
            if (!item || item.equipSlot == EquipSlot.None || !PlayerStorage.Has(item)) return false;
            if (equipped[(int)item.equipSlot] == item) return true;
            equipped[(int)item.equipSlot] = item;
            Bump();
            return true;
        }

        /// <summary>Volta o lugar ao padrão (o cutelo de sempre).</summary>
        public static void Unequip(EquipSlot slot)
        {
            if (slot == EquipSlot.None || !equipped[(int)slot]) return;
            equipped[(int)slot] = null;
            Bump();
        }

        /// <summary>Ids (nome do asset) do que está equipado, para o save.</summary>
        public static string[] Capture()
        {
            int n = 0;
            foreach (var item in equipped) if (item) n++;
            var ids = new string[n];
            n = 0;
            foreach (var item in equipped) if (item) ids[n++] = item.name;
            return ids;
        }

        /// <summary>Devolve o que o save diz (ids desconhecidos ficam de fora). null = nada equipado (jogo novo).</summary>
        public static void Restore(string[] ids, Func<string, ItemDefinition> find)
        {
            Array.Clear(equipped, 0, equipped.Length);
            if (ids != null && find != null)
                foreach (var id in ids)
                {
                    var item = find(id);
                    if (item && item.equipSlot != EquipSlot.None) equipped[(int)item.equipSlot] = item;
                }
            Bump();
        }

        static void Bump()
        {
            Version++;
            Changed?.Invoke();
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Array.Clear(equipped, 0, equipped.Length);
            Version = 0;
            Changed = null;
        }
    }
}
