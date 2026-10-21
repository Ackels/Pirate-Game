using System;
using UnityEngine;

namespace Pirates.Economy
{
    /// <summary>Gear = equipamento do capitão (mochila maior): como o esquema, um só e fica guardado.</summary>
    public enum ItemCategory { Resource, Crew, Schematic, Gear }

    /// <summary>Raridade (E1, 19/10: Lendário no fim, para não mudar o número das que já estão nos assets).</summary>
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

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
        [Tooltip("Ícone da grade do inventário (tela I), fotografado do modelo 3D pelo gerador. Vazio = o quadradinho da cor.")]
        public Texture2D icon;
        [Tooltip("\"Descarregar tudo\" (e o desembarque no porto) deixa até esta quantidade a bordo: munição especial, madeira do " +
                 "carpinteiro. 0 = descarrega tudo. Descarregar item por item ignora isto.")]
        [Min(0)] public int keepAboard;
        [Tooltip("Mochila (equipamento): espaço da mochila do capitão com ela. 0 = não é mochila. Vale a maior que o jogador tiver.")]
        [Min(0f)] public float backpackCapacity;
        [Header("Equipamento (E1)")]
        [Tooltip("Onde o capitão usa este item (lâmina, arma de fogo, traje, relíquia). None = não se equipa.")]
        public EquipSlot equipSlot;
        [Tooltip("O que o item equipa: a WeaponDefinition da lâmina (e, depois, a do traje ou da relíquia).")]
        public ScriptableObject equipment;

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
