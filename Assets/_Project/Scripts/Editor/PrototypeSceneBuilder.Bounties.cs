using System.Collections.Generic;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Missions;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Missões dos inimigos novos do pacote B (10/10, pedido do Jhonata: "montar missões para todos esses inimigos novos"), na taverna da
    /// cena 07: derrotar o brutamontes da Caveira, a duelista dos contrabandistas e o brutamontes do forte (tipo DefeatEnemy: o inimigo
    /// tem um <see cref="EnemyFootAI.BountyId"/> e levanta de novo no posto quando a missão é aceita) e limpar o cemitério amaldiçoado à
    /// noite (ClearCamp, o acampamento do cemitério). Cada alvo ganha um WorldMarker parado no posto dele (o marcador no próprio
    /// personagem sumiria com o acampamento dormindo longe da câmera).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string BruteSkullId = "brutamontes_caveira", BruteFortId = "brutamontes_forte", DuelistHideoutId = "duelista_oeste";
        const string GraveyardMissionId = "cemiterio_caveira";

        /// <summary>Dá nome de missão ao membro do acampamento cujo posto começa com <paramref name="prefix"/> e põe o marcador no posto.</summary>
        static void AddBounty(EnemyCamp camp, string prefix, string id, string label)
        {
            if (!camp) return;
            foreach (var ai in camp.Members)
            {
                if (!ai || !ai.name.StartsWith(prefix)) continue;
                ai.ConfigureBounty(id);
                var marker = new GameObject("Marcador_" + id);
                marker.transform.SetParent(camp.transform, true);
                marker.transform.position = ai.transform.position;
                marker.AddComponent<WorldMarker>().Configure(id, label, 4.5f);
                return;
            }
            Debug.LogWarning($"[Pirates] Missão {id}: ninguém com o posto \"{prefix}...\" em {camp.DisplayName}.");
        }

        /// <summary>Os quatro contratos dos inimigos novos.</summary>
        static IEnumerable<MissionDefinition> BountyMissions(LootItems i)
        {
            var bombs = LoadItem("Item_Bomba");
            var cartridges = LoadItem("Item_Cartuchos");
            yield return Mission("Missao_CarrascoCaveira", m =>
            {
                m.missionId = BruteSkullId;
                m.title = "O carrasco da Caveira";
                m.giver = "Velho Bento";
                m.description = "Um homem-tubarão do tamanho de uma porta guarda o pátio do acampamento da Caveira com um machado de duas lâminas. " +
                                "Já partiu três dos meus ao meio. O machado avisa antes de descer: apare na hora certa, ou role. Derrube-o.";
                m.type = MissionType.DefeatEnemy;
                m.targetId = BruteSkullId;
                m.targetName = "Ilha da Caveira";
                m.objectiveText = "derrote o brutamontes do acampamento da Caveira";
                m.doneText = "brutamontes derrotado";
                // Ouro revisto em 10/10 (aprovado pelo Jhonata): um alvo só pagava mais que o acampamento inteiro (era 260 + 8 ferro, 10 min).
                m.rewards = new[] { new ItemStack(i.gold, 190), new ItemStack(i.iron, 6) };
                m.cooldownSeconds = 900f;
                m.requiredRank = 1;
                m.fameReward = 35;
            });
            yield return Mission("Missao_LaminaEnseada", m =>
            {
                m.missionId = DuelistHideoutId;
                m.title = "A lâmina da enseada";
                m.giver = "Dona Mercês, do armazém";
                m.description = "Os contrabandistas da Ilha Oeste arrumaram uma duelista de adaga. Ninguém encosta nela: apara, rola e volta " +
                                "estocando. Dizem que um bom chute abre a guarda. Acabe com ela e o esconderijo perde a coragem.";
                m.type = MissionType.DefeatEnemy;
                m.targetId = DuelistHideoutId;
                m.targetName = "Ilha Oeste";
                m.objectiveText = "derrote a duelista do esconderijo da Ilha Oeste";
                m.doneText = "duelista derrotada";
                m.rewards = bombs
                    ? new[] { new ItemStack(i.gold, 160), new ItemStack(bombs, 3) }
                    : new[] { new ItemStack(i.gold, 180) };
                m.cooldownSeconds = 900f;
                m.requiredRank = 1;
                m.fameReward = 30;
            });
            yield return Mission("Missao_GuardiaoForte", m =>
            {
                m.missionId = BruteFortId;
                m.title = "O guardião do forte";
                m.giver = "Almirante Teodoro";
                m.description = "Mesmo com as muralhas no chão, o forte da Ilha Norte tem um guardião: outro brutamontes de machado, no pátio, " +
                                "com a guarnição em volta. Desembarque e derrube-o.";
                m.type = MissionType.DefeatEnemy;
                m.targetId = BruteFortId;
                m.targetName = "Ilha Norte";
                m.objectiveText = "derrote o brutamontes do forte da Ilha Norte";
                m.doneText = "guardião do forte derrotado";
                m.rewards = new[] { new ItemStack(i.gold, 230), new ItemStack(i.powder, 8) };
                m.cooldownSeconds = 900f;
                m.requiredRank = 2;
                m.fameReward = 40;
            });
            yield return Mission("Missao_NoiteCemiterio", m =>
            {
                m.missionId = GraveyardMissionId;
                m.title = "Noite no cemitério";
                m.giver = "Padre Anselmo";
                m.description = "Ao lado da trilha das ruínas da Caveira há um cemitério de piratas. De noite os mortos saem das covas. " +
                                "Acabe com eles antes do amanhecer, e leve pólvora: o que cai de tiro ou de bomba não levanta mais.";
                m.type = MissionType.ClearCamp;
                m.targetId = GraveyardMissionId;
                m.targetName = "Ilha da Caveira";
                m.objectiveText = "limpe o cemitério da Caveira (os mortos saem à noite)";
                m.doneText = "cemitério em paz";
                m.rewards = cartridges
                    ? new[] { new ItemStack(i.gold, 240), new ItemStack(cartridges, 20) }
                    : new[] { new ItemStack(i.gold, 260) };
                m.requiredRank = 1;
                m.fameReward = 35;
            });
        }
    }
}
