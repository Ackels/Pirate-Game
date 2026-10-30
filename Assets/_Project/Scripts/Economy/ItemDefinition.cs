using System;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Economy
{
    /// <summary>Gear = equipamento do capitão (mochila maior): como o esquema, um só e fica guardado.</summary>
    public enum ItemCategory { Resource, Crew, Schematic, Gear }

    /// <summary>Raridade (E1, 19/10: Lendário no fim, para não mudar o número das que já estão nos assets).</summary>
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>Grupo dos filtros das janelas (mercado, armazém; H4, 28/10). Auto = pelo resto do item (categoria, equipamento, comida).</summary>
    public enum ItemGroup { Auto, Resource, Ammo, Food, Treasure, Weapon, Tool, Schematic, Crew }

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
        [Tooltip("Filtro das janelas (mercado, armazém). Auto = esquema, tripulação, equipamento (arma se equipa, senão ferramenta), " +
                 "comida (se cura) ou recurso.")]
        public ItemGroup group;
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
        [Tooltip("Comida (peixe, D2): vida que cura ao comer com R quando a mochila não tem ração. 0 = não se come.")]
        [Min(0f)] public float foodHeal;
        [Header("Relíquia (30/10): equipada no encaixe Relíquia da tela I")]
        public RelicEffect relicEffect;
        [Tooltip("Fração: 0,15 = +15% de ouro; 0,5 = +50% de chance de baú e de equipamento.")]
        public float relicAmount;
        [Header("Prato da taverna (D2, 30/10)")]
        [Tooltip("Comido pela tela I: bônus por alguns minutos (fôlego, recarga, casco, furtividade, dano). None = não é prato.")]
        public MealEffect mealEffect;
        [Tooltip("Quanto: fôlego a mais (pontos), casco por segundo, ou fração (0,1 = 10%) nos outros.")]
        public float mealAmount;
        [Min(0f)] public float mealMinutes = 8f;
        [Header("Equipamento (E1)")]
        [Tooltip("Onde o capitão usa este item (lâmina, arma de fogo, traje, relíquia). None = não se equipa.")]
        public EquipSlot equipSlot;
        [Tooltip("O que o item equipa: a WeaponDefinition da lâmina (e, depois, a do traje ou da relíquia).")]
        public ScriptableObject equipment;

        /// <summary>Esquemas e equipamentos: um só (ter dois não serve para nada).</summary>
        public bool IsUnique => category is ItemCategory.Schematic or ItemCategory.Gear;

        /// <summary>O grupo do filtro: o escolhido no asset ou, em Auto, o que o resto do item diz.</summary>
        public ItemGroup Group => group != ItemGroup.Auto ? group : category switch
        {
            ItemCategory.Crew => ItemGroup.Crew,
            ItemCategory.Schematic => ItemGroup.Schematic,
            ItemCategory.Gear => equipSlot != EquipSlot.None ? ItemGroup.Weapon : ItemGroup.Tool,
            _ => foodHeal > 0f ? ItemGroup.Food : ItemGroup.Resource,
        };
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
