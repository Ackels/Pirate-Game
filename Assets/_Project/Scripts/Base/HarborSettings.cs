using System;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// Regras do porto-base: custo do conserto do casco, preços do mercado da praça e o que o ferreiro da Forja faz
    /// (M9: munições especiais).
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Harbor Settings", fileName = "HarborSettings_")]
    public class HarborSettings : ScriptableObject
    {
        [Serializable]
        public class MarketEntry
        {
            public ItemDefinition item;
            [Tooltip("Ouro que o mercado cobra por unidade (0 = não vende).")]
            [Min(0)] public int buyPrice;
            [Tooltip("Ouro que o mercado paga por unidade (0 = não compra). Sempre menor que o preço de venda.")]
            [Min(0)] public int sellPrice;
            [Tooltip("Comprado vai direto para o porão, até onde couber (munição); senão vai para o armazém.")]
            public bool toHold;
            [Tooltip("Demanda: quantas unidades o mercado aceita comprar de você antes de saturar (0 = sem limite). Sem isso, a " +
                     "produção da Forja e do Paiol vendida virava ouro infinito.")]
            [Min(0)] public int demand = 20;
            [Tooltip("Segundos para a demanda voltar uma unidade.")]
            [Min(1f)] public float demandRefillSeconds = 45f;
            [Tooltip("Patente do capitão para o mercado vender isto (M10; 0 = sempre). Ex.: esquemas para quem tem fama.")]
            [Min(0)] public int requiredRank;
        }

        [Serializable]
        public class Recipe
        {
            public ItemStack result;
            public ItemStack[] cost = Array.Empty<ItemStack>();
            [Tooltip("Construção da base exigida (a Forja) e o nível mínimo.")]
            public BuildingDefinition requiredBuilding;
            [Min(1)] public int requiredLevel = 1;
            [Tooltip("Retirado vai direto para o porão, até onde couber (munição); senão vai para o armazém.")]
            public bool toHold = true;
            [Tooltip("Segundos de forja por receita (trabalhadores normais; o Alojamento acelera). Encomendar 5 = 5×.")]
            [Min(0f)] public float craftSeconds = 90f;
        }

        [Header("Conserto do casco (a cada 10 de vida)")]
        public ItemDefinition repairWood;
        [Min(0)] public int woodPer10Hull = 1;
        public ItemDefinition repairGold;
        [Min(0)] public int goldPer10Hull = 5;

        [Header("Mercado da praça (moeda = repairGold)")]
        public MarketEntry[] market = Array.Empty<MarketEntry>();

        [Header("Ferreiro da Forja")]
        public Recipe[] smithy = Array.Empty<Recipe>();

        [Header("Cozinha da taverna (D2, 30/10)")]
        [Tooltip("Pratos feitos na hora com peixe (e um pouco de ouro); vão para o porão. Construção e tempo não valem aqui.")]
        public Recipe[] kitchen = Array.Empty<Recipe>();
    }
}
