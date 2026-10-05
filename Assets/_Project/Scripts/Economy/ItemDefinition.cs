using System;
using UnityEngine;

namespace Pirates.Economy
{
    /// <summary>Gear = equipamento do capitão (mochila maior): como o esquema, um só e fica guardado.</summary>
    public enum ItemCategory { Resource, Crew, Schematic, Gear }

    public enum ItemRarity { Common, Uncommon, Rare, Epic }

    /// <summary>
    /// Um tipo de item: recurso (ouro, madeira...), tripulação resgatada ou esquema de construção.
    /// Os assets ficam em Data/Items; o M3 usa os mesmos para loja, base e upgrades.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Item", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        public ItemCategory category = ItemCategory.Resource;
        public ItemRarity rarity = ItemRarity.Common;
        [Tooltip("Cor do nome no HUD e nos rótulos sobre o item na água.")]
        public Color color = Color.white;
        [Tooltip("Espaço que cada unidade ocupa no porão. 0 = não ocupa (tripulação vai no alojamento, esquema é papel).")]
        [Min(0f)] public float spacePerUnit = 1f;
        [TextArea] public string description;
        [Tooltip("\"Descarregar tudo\" (e o desembarque no porto) deixa até esta quantidade a bordo: munição especial, madeira do " +
                 "carpinteiro. 0 = descarrega tudo. Descarregar item por item ignora isto.")]
        [Min(0)] public int keepAboard;
        [Tooltip("Mochila (equipamento): espaço da mochila do capitão com ela. 0 = não é mochila. Vale a maior que o jogador tiver.")]
        [Min(0f)] public float backpackCapacity;

        /// <summary>Esquemas e equipamentos: um só (ter dois não serve para nada).</summary>
        public bool IsUnique => category is ItemCategory.Schematic or ItemCategory.Gear;
    }

    [Serializable]
    public struct ItemStack
    {
        public ItemDefinition item;
        public int amount;

        public ItemStack(ItemDefinition item, int amount)
        {
            this.item = item;
            this.amount = amount;
        }
    }
}
