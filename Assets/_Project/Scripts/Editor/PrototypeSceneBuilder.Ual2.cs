using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Universal Animation Library 2 [Standard] do Quaternius (06/11, CC0, em <see cref="ThirdPartyImportSettings.Ual2Folder"/>): um FBX com
    /// o manequim e 43 clipes humanoides ("Armature|Sword_Regular_A" etc.), do mesmo autor e estilo dos nossos personagens. Guardado para os
    /// próximos passos da revisão das animações (defesa "Sword_Block", arremesso "OverhandThrow", "Chest_Open", "Consume", "TreeChopping_Loop",
    /// "Hit_Knockback", zumbi, escudo).
    /// <para>Testado para o combo do cutelo (passo 3, 06/11) e descartado: os cortes de espada ("Sword_Regular_A/B/C") giram o corpo inteiro
    /// ~130–160° (o "A" termina com o quadril a −130° e o peito a −155°, de costas), dentro ou fora da pose (com o Animator sem movimento pela
    /// raiz o Unity mostra o giro de qualquer jeito). Medido no jogo, trocando o clipe do "Golpe1" do capitão na hora (AnimatorOverrideController).</para>
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string Ual2File = ThirdPartyImportSettings.Ual2Folder + "/UAL2_Standard.fbx";

        /// <summary>Clipe do UAL2 pelo nome (sem o "Armature|"); null sem o pacote.</summary>
        static AnimationClip Ual2Clip(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Ual2File))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__") && (clip.name == name || clip.name.EndsWith("|" + name)))
                    return clip;
            return null;
        }
    }
}
