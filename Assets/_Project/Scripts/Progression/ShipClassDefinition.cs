using System;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Uma classe de navio do jogador (Chalupa, Escuna...): os valores base que os upgrades usam (stats, canhões, casco),
    /// porão e beliches, e o que custa construir no estaleiro do porto (esquema achado no baú raro, recursos, tempo de obra).
    /// O casco físico e o visual de cada classe ficam no navio da cena (PlayerShipClass, montado pelo gerador).
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Ship Class", fileName = "ShipClass_")]
    public class ShipClassDefinition : ScriptableObject
    {
        public string displayName = "Navio";
        [TextArea] public string description;
        [Tooltip("Ordem das classes: o estaleiro só oferece as de nível maior que o navio atual.")]
        public int tier;

        [Header("Navio")]
        public ShipStats stats;
        public CannonStats cannons;
        [Min(1f)] public float hullMax = 150f;
        [Min(0f)] public float holdCapacity = 30f;
        [Min(0)] public int crewBerths = 16;

        [Header("Upgrades neste navio (começam do zero ao trocar de navio)")]
        [Tooltip("Quanto rende o upgrade de casco neste navio: 1,2 = 20% a mais que na chalupa.")]
        [Min(0f)] public float hullUpgradeMultiplier = 1f;
        [Tooltip("Quanto rende o upgrade de dano dos canhões (os canhões a mais do navio maior já somam dano).")]
        [Min(0f)] public float damageUpgradeMultiplier = 1f;
        [Tooltip("Quanto rende o upgrade de velocidade (velame).")]
        [Min(0f)] public float speedUpgradeMultiplier = 1f;
        [Tooltip("Quanto rende o upgrade de recarga (artilheiros).")]
        [Min(0f)] public float reloadUpgradeMultiplier = 1f;
        [Tooltip("Quanto rende o upgrade de mira assistida (o máximo continua 100%).")]
        [Min(0f)] public float aimUpgradeMultiplier = 1f;
        [Tooltip("Custo e tempo de obra dos upgrades neste navio (multiplica os do upgrade).")]
        [Min(0.1f)] public float upgradeCostMultiplier = 1f;
        [Tooltip("Recarga mínima (s) somando upgrades e tripulação: sem ela, escuna + artilheiros 4 + tripulação cheia recarregava " +
                 "em 1,6 s e o chefe caía em segundos (decidido pelo Jhonata em 05/10: 2,5 s).")]
        [Min(0f)] public float minReloadSeconds = 2.5f;

        /// <summary>Quanto rende neste navio um upgrade do tipo <paramref name="stat"/>.</summary>
        public float UpgradeMultiplier(UpgradeStat stat) => stat switch
        {
            UpgradeStat.HullMax => hullUpgradeMultiplier,
            UpgradeStat.CannonDamage => damageUpgradeMultiplier,
            UpgradeStat.MaxSpeedPercent => speedUpgradeMultiplier,
            UpgradeStat.ReloadPercent => reloadUpgradeMultiplier,
            UpgradeStat.AimAssistPercent => aimUpgradeMultiplier,
            _ => 1f,
        };

        [Header("Construção no estaleiro")]
        [Tooltip("Esquema necessário (no armazém ou no porão). Vazio = não precisa.")]
        public ItemDefinition schematic;
        [Tooltip("Construção da base exigida (o Estaleiro). Vazio = nenhuma.")]
        public BuildingDefinition requiredBuilding;
        [Tooltip("Nível mínimo dessa construção (o galeão pede o Estaleiro 2).")]
        [Min(1)] public int requiredBuildingLevel = 1;
        public ItemStack[] cost = Array.Empty<ItemStack>();
        [Tooltip("Segundos de obra, na velocidade normal dos trabalhadores (só anda com o navio na zona do porto).")]
        [Min(0f)] public float buildSeconds = 180f;
    }
}
