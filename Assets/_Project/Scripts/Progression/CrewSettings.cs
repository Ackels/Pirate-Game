using System;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Tripulação do navio do jogador (ajuste de game design): quantos cabem nos beliches, quanto a tripulação cheia melhora a
    /// recarga e a manobra (proporcional a quantos há a bordo; sem ninguém, o navio fica como sempre foi), quantos morrem com
    /// os tiros no casco e quanto custa contratar marinheiros na taverna. Os tripulantes são o item de sobreviventes no porão.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Crew Settings", fileName = "CrewSettings_")]
    public class CrewSettings : ScriptableObject
    {
        [Tooltip("Item que conta como tripulante a bordo (sobreviventes resgatados e marinheiros contratados).")]
        public ItemDefinition crewItem;
        [Tooltip("Beliches: máximo de tripulantes a bordo.")]
        [Min(0)] public int berths = 16;
        [Tooltip("Recarga dos canhões mais rápida com os beliches cheios (0,2 = 20% menos tempo). Metade da tripulação, metade disso.")]
        [Range(0f, 0.5f)] public float reloadBonusAtFull = 0.2f;
        [Tooltip("Aceleração e giro a mais com os beliches cheios (0,15 = +15%): mais braços nas velas e no leme.")]
        [Range(0f, 0.5f)] public float handlingBonusAtFull = 0.15f;
        [Tooltip("Dano no casco por tripulante morto, em média (cada tiro sorteia). 0 = ninguém morre.")]
        [Min(0f)] public float damagePerCasualty = 40f;
        [Tooltip("Preço de um marinheiro contratado na taverna.")]
        public ItemStack[] recruitCost = Array.Empty<ItemStack>();

        [Header("Carpinteiro a bordo (M9: consertar no mar, tecla R)")]
        [Tooltip("Item gasto no conserto (a madeira do porão).")]
        public ItemDefinition carpenterWood;
        [Tooltip("Casco consertado por unidade de madeira (no porto: 10 por 1 madeira + 5 ouro).")]
        [Min(0.1f)] public float carpenterHullPerWood = 5f;
        [Tooltip("Casco consertado por segundo.")]
        [Min(0.1f)] public float carpenterHullPerSecond = 2f;
        [Tooltip("Segundos sem levar tiro antes de o carpinteiro voltar ao trabalho.")]
        [Min(0f)] public float carpenterQuietSeconds = 8f;
        [Tooltip("Tripulantes a bordo para ter carpinteiro.")]
        [Min(0)] public int carpenterMinCrew = 2;
        [Tooltip("Segundos de descanso depois de um conserto (desligou ou terminou, tendo consertado algo) até poder ligar de novo.")]
        [Min(0f)] public float carpenterCooldownSeconds = 90f;
    }
}
