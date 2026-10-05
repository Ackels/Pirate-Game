using System;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>O que um bônus de patente muda (valor em fração: 0,1 = +10%).</summary>
    public enum PerkEffect
    {
        /// <summary>Dano por bala dos canhões do navio.</summary>
        CannonDamagePercent,
        /// <summary>Velocidade máxima do navio.</summary>
        MaxSpeedPercent,
        /// <summary>Casco máximo do navio.</summary>
        HullPercent,
        /// <summary>Mercado paga mais (+valor) e cobra menos (−metade do valor).</summary>
        MarketPercent,
        /// <summary>Obras do estaleiro e da base mais rápidas.</summary>
        BuildSpeedPercent,
        /// <summary>Ouro das missões.</summary>
        MissionGoldPercent,
        /// <summary>Carpinteiro conserta mais rápido e descansa menos (descanso × 1 / (1 + valor)).</summary>
        CarpenterPercent,
        /// <summary>Espaço do porão.</summary>
        HoldPercent,
        /// <summary>Tiros no casco matam menos tripulantes (baixas ÷ (1 + valor)).</summary>
        CrewToughnessPercent,
    }

    /// <summary>
    /// Fama do capitão (M10): patentes (fama mínima de cada uma, a primeira é 0) e os bônus que se escolhe a cada patente nova.
    /// Quanto cada feito dá de fama fica em quem publica o evento (navio, acampamento, forte, tesouro) e em cada missão.
    /// O save guarda o id dos bônus: mudar um id faz o save esquecer aquele bônus.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Fame Settings", fileName = "FameSettings_")]
    public class FameSettings : ScriptableObject
    {
        [Serializable]
        public class Rank
        {
            public string title = "Grumete";
            [Tooltip("Fama mínima para esta patente (a primeira é 0).")]
            [Min(0)] public int fame;
            [Tooltip("Frase curta no painel (o que se diz de um capitão desta patente).")]
            public string description;
        }

        [Serializable]
        public class Perk
        {
            [Tooltip("Id no save (não mude depois de salvar).")]
            public string id = "bonus";
            public string title = "Bônus";
            [TextArea(1, 3)] public string description;
            public PerkEffect effect;
            [Tooltip("Fração: 0,1 = +10%.")]
            public float value = 0.1f;
        }

        public Rank[] ranks = Array.Empty<Rank>();
        public Perk[] perks = Array.Empty<Perk>();
        [Tooltip("Quantos bônus o painel oferece para escolher um, a cada patente nova.")]
        [Min(1)] public int perksOffered = 3;
        [Tooltip("Ouro das missões a mais por patente (0,1 = +10% por patente).")]
        [Min(0f)] public float missionGoldPerRank = 0.1f;
        [Tooltip("Nome do capitão quando o jogador não escolhe (saves de antes do M10, cena 05).")]
        public string defaultCaptainName = "Capitão Barbarossa";

        public Perk FindPerk(string id)
        {
            foreach (var p in perks)
                if (p != null && p.id == id) return p;
            return null;
        }
    }
}
