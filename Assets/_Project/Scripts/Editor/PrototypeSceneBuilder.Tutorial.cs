using Pirates.Controls;
using Pirates.LandCombat;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Tutorial;
using Pirates.UI;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Tutorial do jogo novo (11/10): o campo de treino do porto (bonecos para o cutelo, a pistola e o chute, cenas 05 e 07) e,
    /// na 07, o objeto "Tutorial" (TutorialGuide, guiado pelo Velho Bento) com o cartão e o marcador no HUD.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Campo de treino: área plana e livre a leste da vila, entre a rua de baixo e o estaleiro (conferido: sem casa a 5 m).</summary>
        static Vector2 TrainingYardSpot => new(40f, 18f);

        static Transform portTrainingYard;

        /// <summary>Três bonecos de treino e uma placa; antes do NavMesh do porto (os moradores desviam). Fica fora da vila estática: o boneco balança.</summary>
        static Transform PortTrainingYard(Transform parent)
        {
            var wood = GetMaterial("M_TargetWood", new Color(0.46f, 0.31f, 0.16f), 0.2f);
            var sack = GetMaterial("M_Sack", new Color(0.72f, 0.62f, 0.42f), 0.05f);
            var yard = new GameObject("CampoDeTreino").transform;
            yard.SetParent(parent, false);
            yard.position = PortGround(TrainingYardSpot);
            var spots = new[] { new Vector2(-3f, 0f), new Vector2(0f, 2f), new Vector2(3f, 0f) };
            for (int k = 0; k < spots.Length; k++)
                Dummy(yard, PortGround(TrainingYardSpot + spots[k]), 0f, $"Boneco_{k}", wood, sack);
            Model(Kit.Survival, "signpost-single", yard, PortGround(TrainingYardSpot + new Vector2(-5.5f, -2.5f)) - yard.position,
                30f, SurvivalScale, "Placa_Treino");
            portTrainingYard = yard;
            return yard;
        }

        /// <summary>O tutorial da cena 07 (depois dos NPCs e do quadro: precisa do Velho Bento e da conversa no personagem).</summary>
        static void AddTutorial(PlayerModeSwitcher switcher, GameObject hud)
        {
            var ship = switcher.Ship;
            var shore = ship ? ship.GetComponent<HarborShoreLeave>() : null;
            var bento = GameObject.Find("NPC_Velho_Bento")?.GetComponent<MissionGiver>();
            if (!bento) Debug.LogWarning("[Pirates] Tutorial sem o Velho Bento (os NPCs precisam do kit do Quaternius).");
            var tutorial = new GameObject("Tutorial").AddComponent<TutorialGuide>();
            LandingPoint beach = null;
            foreach (var point in Object.FindObjectsByType<LandingPoint>(FindObjectsInactive.Include))
                if (point.name == "Praia_Desembarque") beach = point;
            EnemyCamp post = null;
            foreach (var camp in Object.FindObjectsByType<EnemyCamp>(FindObjectsInactive.Include))
                if (camp.CampId == "posto_naufragio") post = camp;
            // Praia norte e posto do naufrágio: a praia sul fica a ~185 m do galeão Tormenta, que atacava o navio do novato.
            if (!beach || !post) Debug.LogWarning("[Pirates] Tutorial sem a praia norte ou o posto do naufrágio da Caveira (parte das ilhas).");
            tutorial.Configure(switcher, shore, bento, portTrainingYard, beach, post);
            hud.AddComponent<TutorialHUD>().Bind(tutorial, switcher);
        }
    }
}
