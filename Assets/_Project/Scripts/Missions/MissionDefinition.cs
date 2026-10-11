using System;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Missions
{
    public enum MissionType
    {
        /// <summary>Afundar N navios inimigos.</summary>
        SinkShips,
        /// <summary>Derrotar todos os piratas de um acampamento (targetId = EnemyCamp.campId).</summary>
        ClearCamp,
        /// <summary>Levar itens no porão até o porto (saem do porão ao entregar).</summary>
        Deliver,
        /// <summary>Desenterrar um tesouro (targetId = BuriedTreasure.treasureId).</summary>
        FindTreasure,
        /// <summary>Derrotar um inimigo com nome (targetId = EnemyFootAI.BountyId: brutamontes, duelista).</summary>
        DefeatEnemy,
    }

    /// <summary>Um contrato do quadro do porto (dados de game design em Data/Missions).</summary>
    [CreateAssetMenu(menuName = "Pirates/Mission", fileName = "Missao_")]
    public class MissionDefinition : ScriptableObject
    {
        public string missionId = "missao";
        public string title = "Missão";
        [Tooltip("Quem dá a missão no porto.")]
        public string giver = "Marinheiro";
        [TextArea(2, 5)] public string description;
        public MissionType type;
        [Tooltip("Afundar navios: quantos.")]
        [Min(1)] public int count = 1;
        [Tooltip("Acampamento ou tesouro: id do alvo na cena (o mesmo do EnemyCamp ou do BuriedTreasure).")]
        public string targetId;
        [Tooltip("Nome do lugar para o jogador (ex.: Ilha da Caveira).")]
        public string targetName;
        [Tooltip("Entregar: o que levar no porão.")]
        public ItemStack[] delivery = Array.Empty<ItemStack>();
        [Tooltip("Pagamento ao entregar no porto (vai para o armazém).")]
        public ItemStack[] rewards = Array.Empty<ItemStack>();
        [Tooltip("Texto do objetivo no HUD (vazio = o do tipo, ex.: \"derrote os piratas em ...\").")]
        public string objectiveText;
        [Tooltip("Texto do objetivo cumprido (vazio = o do tipo, ex.: \"acampamento derrotado\").")]
        public string doneText;
        [Tooltip("Volta ao quadro depois de entregue (dá para fazer de novo).")]
        public bool repeatable = true;
        [Tooltip("Repetível: segundos de jogo depois de entregue até voltar ao quadro (sem isso, as entregas viravam ouro infinito).")]
        [Min(0f)] public float cooldownSeconds = 600f;
        [Header("Fama do capitão (M10)")]
        [Tooltip("Patente mínima para o contrato aparecer no quadro (0 = desde o começo).")]
        [Min(0)] public int requiredRank;
        [Tooltip("Fama ao entregar (cada vez que entrega uma repetível).")]
        [Min(0)] public int fameReward = 10;

        [Header("Conversa com o NPC (missão dada por alguém na vila)")]
        [Tooltip("Dada por um NPC (MissionGiver com o nome do 'giver'): não aparece no quadro da taverna e é entregue a ele, que paga.")]
        public bool givenByNpc;
        [Tooltip("Falas do NPC antes de oferecer (uma por página; F passa).")]
        [TextArea(2, 4)] public string[] offerLines = Array.Empty<string>();
        [TextArea(1, 3)] public string acceptLine, declineLine;
        [Tooltip("Aceita e ainda não cumprida.")]
        [TextArea(1, 3)] public string waitingLine;
        [Tooltip("Cumprida: o fechamento da história, ao entregar (a última página paga).")]
        [TextArea(2, 4)] public string[] doneLines = Array.Empty<string>();
        [Tooltip("Entregue e descansando (repetível) ou já feita de vez.")]
        [TextArea(1, 3)] public string restingLine;
    }
}
