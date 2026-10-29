using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Locomoção do Human Basic Motions FREE (Kevin Iglesias, Asset Store; 27/10, pedido do Jhonata: "deixando mais realista e fluido"),
    /// no lugar da do Starter Assets, para todos os humanoides (capitão e inimigos): parado ("Idle01"), andar e correr para a frente
    /// ("Walk01_Forward", "Run01_Forward", nos mesmos limites da blend tree: 0 / andar / correr), pulo (começo, queda em loop e aterrissagem) e o
    /// andar de lado ("Lateral", R4) com o conjunto inteiro do pacote em 8 direções, andando e correndo (antes eram clipes avulsos dos pacotes do
    /// machado e do espadão do Mixamo). As personagens do modelo da Anne (duelista, mosqueteira da Guarda Negra) usam as versões femininas
    /// ("HumanF@", override "Humana"). Medido no capitão (pé apoiado): o Walk01 anda quase o mesmo que o Walk_N do Starter (0,80 × 0,76) e o
    /// Run01 ~1,6× mais por passada que o Run_N (escorrega menos nos 6 m/s). Refeito a cada geração (o controller é preservado).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string KevinMaleFolder = "Assets/ThirdParty/Loja/KevinIglesias/Human Animations/Animations/Male";

        const string BasicIdle = "Idles/HumanM@Idle01", BasicWalk = "Movement/Walk/HumanM@Walk01_Forward",
            BasicRun = "Movement/Run/HumanM@Run01_Forward", BasicJumpBegin = "Movement/Jump/HumanM@Jump01 - Begin",
            BasicFall = "Movement/Jump/HumanM@Fall01", BasicJumpLand = "Movement/Jump/HumanM@Jump01 - Land";

        // Andar de lado: direção e posição no espaço do MoveX/MoveZ (mesma escala dos clipes do Mixamo de antes: andando ~1,5, correndo ~3,5).
        static readonly (string clip, Vector2 dir)[] BasicStrafe =
        {
            ("Forward", new Vector2(0f, 1f)), ("Backward", new Vector2(0f, -1f)), ("Left", new Vector2(-1f, 0f)), ("Right", new Vector2(1f, 0f)),
            ("ForwardLeft", new Vector2(-0.7071f, 0.7071f)), ("ForwardRight", new Vector2(0.7071f, 0.7071f)),
            ("BackwardLeft", new Vector2(-0.7071f, -0.7071f)), ("BackwardRight", new Vector2(0.7071f, -0.7071f)),
        };
        const float BasicStrafeWalk = 1.5f, BasicStrafeRun = 3.5f;

        /// <summary>Clipe do pacote (caminho a partir da pasta do homem, sem .fbx); <paramref name="female"/> = a versão "HumanF@" na pasta Female.</summary>
        static AnimationClip BasicMotion(string relative, bool female = false)
        {
            string path = KevinMaleFolder + "/" + relative + ".fbx";
            if (female) path = path.Replace("/Male/", "/Female/").Replace("HumanM@", "HumanF@");
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        /// <summary>Troca os clipes da camada de base (locomoção e pulo) pelos do pacote; sem o pacote, fica o Starter.</summary>
        static void UseBasicMotions(AnimatorController controller)
        {
            var idle = BasicMotion(BasicIdle);
            var walk = BasicMotion(BasicWalk);
            var run = BasicMotion(BasicRun);
            if (!idle || !walk || !run) return;
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                var state = child.state;
                switch (state.name)
                {
                    case "Idle Walk Run Blend":
                        SetTreeMotions(state.motion as BlendTree, idle, walk, run);
                        break;
                    case "JumpLand":
                        SetTreeMotions(state.motion as BlendTree, BasicMotion(BasicJumpLand) ?? idle, walk, run);
                        break;
                    case "JumpStart":
                        if (BasicMotion(BasicJumpBegin)) state.motion = BasicMotion(BasicJumpBegin);
                        break;
                    case "InAir":
                        if (BasicMotion(BasicFall)) state.motion = BasicMotion(BasicFall);
                        break;
                }
            }
        }

        static void SetTreeMotions(BlendTree tree, params Motion[] motions)
        {
            if (!tree || tree.children.Length != motions.Length) return;
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) children[i].motion = motions[i];
            tree.children = children;
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Clipes do andar de lado (8 direções andando e correndo); vazio sem o pacote.</summary>
        static List<(AnimationClip clip, Vector2 at)> BasicStrafeClips()
        {
            var list = new List<(AnimationClip, Vector2)>();
            foreach (var (name, dir) in BasicStrafe)
            {
                var walk = BasicMotion($"Movement/Walk/HumanM@Walk01_{name}");
                var run = BasicMotion($"Movement/Run/HumanM@Run01_{name}");
                if (!walk || !run) return new List<(AnimationClip, Vector2)>();
                list.Add((walk, dir * BasicStrafeWalk));
                list.Add((run, dir * BasicStrafeRun));
            }
            return list;
        }

        /// <summary>
        /// Override com as versões femininas de todos os clipes do pacote que o controller usa (mesmo arquivo com "HumanF@"), para as
        /// personagens do modelo da Anne (refeito a cada geração, no mesmo arquivo).
        /// </summary>
        static RuntimeAnimatorController FemaleAnimator(RuntimeAnimatorController controller)
        {
            if (!controller || controller is AnimatorOverrideController) return controller;
            string path = $"{AnimationFolder}/Humana.overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            bool created = !overrides;
            if (created) overrides = new AnimatorOverrideController(controller);
            else overrides.runtimeAnimatorController = controller;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var clip in overrides.animationClips)
            {
                AnimationClip female = null;
                string clipPath = AssetDatabase.GetAssetPath(clip);
                // Também as poses de segurar arma ("Masked Poses", ao lado da pasta Male: só o nome muda).
                if ((clipPath.StartsWith(KevinMaleFolder) || clipPath.Contains("/Masked Poses/")) && clipPath.Contains("HumanM@"))
                {
                    string femalePath = clipPath.Replace("/Male/", "/Female/").Replace("HumanM@", "HumanF@");
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(femalePath))
                        if (o is AnimationClip c && !c.name.StartsWith("__preview__")) female = c;
                }
                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, female));
            }
            overrides.ApplyOverrides(pairs);
            if (created) AssetDatabase.CreateAsset(overrides, path);
            else EditorUtility.SetDirty(overrides);
            return overrides;
        }
    }
}
