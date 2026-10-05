using UnityEngine;

namespace Pirates.Core
{
    // Eventos do jogo (EventBus). Ids de alvo são textos combinados entre os dados (missões) e a cena
    // (ex.: o EnemyCamp e o BuriedTreasure têm o mesmo id que a missão que os usa).
    // "fame" = fama que o feito dá ao capitão (M10; quem publica sabe quanto vale: chalupa pouco, galeão muito).

    /// <summary>Um navio inimigo afundou. targetId: o do navio com nome (ex.: o brigue de uma missão); vazio = navio comum.</summary>
    public struct ShipSunkEvent
    {
        public GameObject ship;
        public string targetId;
        public int fame;
    }

    /// <summary>O último pirata de um acampamento caiu (ou o forte calou: mesmo evento, com o id do forte).</summary>
    public struct CampClearedEvent
    {
        public string campId;
        public int fame;
    }

    /// <summary>O jogador desenterrou um tesouro.</summary>
    public struct TreasureFoundEvent
    {
        public string treasureId;
        public int fame;
    }

    /// <summary>Uma missão foi aceita (o alvo dela se prepara: tesouro aparece, acampamento se repovoa, brigue volta ao mar).</summary>
    public struct MissionStartedEvent
    {
        public string missionId;
        public string targetId;
    }

    /// <summary>Uma missão foi entregue no porto (M10: a fama dela vai para o capitão).</summary>
    public struct MissionTurnedInEvent
    {
        public string missionId;
        public string title;
        public int fame;
    }
}
