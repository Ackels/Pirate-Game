using System;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Progression
{
    public enum BuildingEffect
    {
        /// <summary>Multiplica a velocidade dos trabalhadores (todas as obras). Valor = multiplicador (1,5 = 50% mais rápido).</summary>
        WorkerSpeed,
        /// <summary>Libera os níveis de upgrade do navio que pedem esta construção. Valor sem uso.</summary>
        Unlock,
        /// <summary>Produz um item no armazém. Valor = segundos por unidade.</summary>
        ProduceItem,
        /// <summary>Canhões que defendem o porto. Valor = metros a mais na zona segura.</summary>
        CoastalDefense,
    }

    /// <summary>
    /// Uma construção da base, em níveis. Pode exigir um esquema (achado nos baús raros) no armazém ou no porão.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Building", fileName = "Building_")]
    public class BuildingDefinition : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            [Tooltip("Efeito neste nível (ver o tipo de efeito).")]
            public float value;
            public ItemStack[] cost = Array.Empty<ItemStack>();
            [Tooltip("Segundos de obra, na velocidade normal dos trabalhadores.")]
            [Min(0f)] public float buildSeconds = 30f;
        }

        public string displayName = "Construção";
        [TextArea] public string description;
        [Tooltip("Esquema necessário para construir (vazio = não precisa).")]
        public ItemDefinition schematic;
        public BuildingEffect effect;
        [Tooltip("Item produzido (só para Produce Item).")]
        public ItemDefinition producedItem;
        public Level[] levels = Array.Empty<Level>();

        public int MaxLevel => levels.Length;
    }
}
