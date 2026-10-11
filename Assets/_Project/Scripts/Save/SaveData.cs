using System;

namespace Pirates.Save
{
    /// <summary>
    /// O que vai no arquivo do jogo salvo (JSON). Só o progresso: as ilhas (acampamento, baús, caixotes) não entram e
    /// voltam cheias ao carregar. Itens, upgrades e construções são guardados pelo nome do asset (ex.: "Item_Ouro");
    /// missões pelo missionId. Renomear um desses assets faz o save esquecer o que era dele.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>Quando foi salvo (ISO 8601, horário local); só para mostrar no menu.</summary>
        public string savedAt;
        public string harborName;
        public float hull;
        /// <summary>Classe do navio (nome do asset ShipClass_*); vazio = o navio com que a cena nasce (saves antigos).</summary>
        public string shipClass;
        public ItemCount[] hold = Array.Empty<ItemCount>();
        public ItemCount[] backpack = Array.Empty<ItemCount>();
        public ItemCount[] warehouse = Array.Empty<ItemCount>();
        public LevelEntry[] upgrades = Array.Empty<LevelEntry>();
        public JobEntry shipyardJob = new();
        public LevelEntry[] buildings = Array.Empty<LevelEntry>();
        public JobEntry buildingJob = new();
        /// <summary>Encomenda da Forja: id = item do resultado da receita, level = quantas receitas.</summary>
        public JobEntry smithyOrder = new();
        public TimerEntry[] production = Array.Empty<TimerEntry>();
        public MissionEntry[] activeMissions = Array.Empty<MissionEntry>();
        public string[] finishedMissions = Array.Empty<string>();
        /// <summary>Missões repetíveis descansando: missionId e segundos até voltarem ao quadro.</summary>
        public TimerEntry[] missionCooldowns = Array.Empty<TimerEntry>();
        /// <summary>Mercado: quanto ainda compra de cada item (id do item; "seconds" = unidades restantes). Ausente = cheio.</summary>
        public TimerEntry[] marketDemand = Array.Empty<TimerEntry>();

        // Fama do capitão (M10). Save de antes não tem (hasFame falso): a fama começa com a das missões já entregues.
        public bool hasFame;
        public string captainName;
        public int fame;
        /// <summary>Bônus de patente escolhidos (FameSettings.Perk.id) e os oferecidos ainda sem escolha.</summary>
        public string[] perks = Array.Empty<string>();
        public string[] perkOffer = Array.Empty<string>();
        public int shipsSunk, campsCleared, treasuresFound, missionsDone;

        // Hora do dia (M11). Save de antes não tem (hasTime falso): começa na hora do jogo novo.
        public bool hasTime;
        public int day;
        public float hour;

        // Tutorial (11/10). Save de antes não tem (hasTutorial falso): sem tutorial. −1 = terminado ou pulado.
        public bool hasTutorial;
        public int tutorialStep = -1;
    }

    [Serializable]
    public struct ItemCount
    {
        public string id;
        public int amount;

        public ItemCount(string id, int amount)
        {
            this.id = id;
            this.amount = amount;
        }
    }

    [Serializable]
    public struct LevelEntry
    {
        public string id;
        public int level;

        public LevelEntry(string id, int level)
        {
            this.id = id;
            this.level = level;
        }
    }

    [Serializable]
    public struct TimerEntry
    {
        public string id;
        public float seconds;

        public TimerEntry(string id, float seconds)
        {
            this.id = id;
            this.seconds = seconds;
        }
    }

    [Serializable]
    public struct MissionEntry
    {
        public string id;
        public int progress;
        public bool objectiveDone;
    }

    /// <summary>Obra em andamento (estaleiro do navio ou construção da base). id vazio = nenhuma.</summary>
    [Serializable]
    public class JobEntry
    {
        public string id;
        public int level;
        public float duration, progress;
        public ItemCount[] paid = Array.Empty<ItemCount>();

        public bool Active => !string.IsNullOrEmpty(id);
    }
}
