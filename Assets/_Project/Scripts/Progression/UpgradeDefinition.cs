using System;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Progression
{
    public enum UpgradeStat
    {
        /// <summary>Vida máxima do casco: valor somado.</summary>
        HullMax,
        /// <summary>Dano por bala de canhão: valor somado.</summary>
        CannonDamage,
        /// <summary>Velocidade máxima: porcentagem a mais.</summary>
        MaxSpeedPercent,
        /// <summary>Tempo de recarga: porcentagem a menos.</summary>
        ReloadPercent,
        /// <summary>Mira assistida em % (100 = total, com antecipação); a dispersão cai metade disso.</summary>
        AimAssistPercent,
    }

    /// <summary>
    /// Um upgrade do navio comprado no porto, em níveis. O valor de cada nível é o total acumulado
    /// (nível 2 = +6 de dano no total, não +6 sobre o nível 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Upgrade", fileName = "Upgrade_")]
    public class UpgradeDefinition : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            [Tooltip("Efeito total neste nível (vida/dano somados; velocidade/recarga em %).")]
            public float value;
            public ItemStack[] cost = Array.Empty<ItemStack>();
            [Tooltip("Sobreviventes que precisam estar a bordo (não são gastos).")]
            [Min(0)] public int crewRequired;
            [Tooltip("Segundos de obra no estaleiro, na velocidade normal dos trabalhadores.")]
            [Min(0f)] public float buildSeconds = 30f;
            [Tooltip("Construção da base exigida para este nível (vazio = nenhuma).")]
            public BuildingDefinition requiredBuilding;
            [Tooltip("Nível mínimo dessa construção (nível 4 dos upgrades: Estaleiro nível 2).")]
            [Min(1)] public int requiredBuildingLevel = 1;
        }

        public string displayName = "Upgrade";
        [TextArea] public string description;
        public UpgradeStat stat;
        public Level[] levels = Array.Empty<Level>();

        public int MaxLevel => levels.Length;
    }
}
