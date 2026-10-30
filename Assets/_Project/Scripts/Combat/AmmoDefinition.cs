using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Munição especial dos canhões (M9): muda a bordada inteira de uma bateria. Sem munição (null) = bala comum, como sempre.
    /// Corrente: menos dano no casco, mas rasga as velas (o navio acertado fica lento). Metralha: vários projéteis pequenos por
    /// canhão, curta distância, mais dano e abate a tripulação (o navio acertado recarrega mais devagar). Quem consome e de
    /// onde vem (item no porão) é do ShipAmmo, no navio do jogador.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Ammo", fileName = "Ammo_")]
    public class AmmoDefinition : ScriptableObject
    {
        public string displayName = "Munição";
        [Tooltip("Dano no casco × isto (somando os projéteis de um canhão).")]
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("Espalhamento da bordada × isto.")]
        [Min(0f)] public float spreadMultiplier = 1f;
        [Tooltip("Alcance máximo (m); 0 = o dos canhões.")]
        [Min(0f)] public float maxRange;
        [Tooltip("Projéteis por canhão (metralha: vários pequenos; o dano do canhão se divide entre eles).")]
        [Min(1)] public int pellets = 1;
        [Tooltip("Tamanho da bala na tela × isto.")]
        [Min(0.1f)] public float visualScale = 1f;
        [Tooltip("Visual de bala de corrente: duas meias-balas presas por uma corrente, girando no voo.")]
        public bool chainVisual;
        [Tooltip("Fração das velas rasgada por projétil que acerta (somada até o limite do ShipDebuffs).")]
        [Min(0f)] public float sailDamagePerHit;
        [Tooltip("Fração da tripulação abatida por projétil que acerta (somada até o limite do ShipDebuffs).")]
        [Min(0f)] public float crewDamagePerHit;
        [Tooltip("Incendiária (30/10): o navio atingido queima isto de casco por segundo, por burnSeconds (um acerto novo recomeça o tempo).")]
        [Min(0f)] public float burnPerSecond;
        [Min(0f)] public float burnSeconds;
    }
}
