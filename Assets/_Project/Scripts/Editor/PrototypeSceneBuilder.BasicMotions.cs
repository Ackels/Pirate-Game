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
            BasicRun = "Movement/Run/HumanM@Run01_Forward", BasicSprint = "Movement/Sprint/HumanM@Sprint01_Forward",
            BasicJumpBegin = "Movement/Jump/HumanM@Jump01 - Begin",
            BasicFall = "Movement/Jump/HumanM@Fall01", BasicJumpLand = "Movement/Jump/HumanM@Jump01 - Land";

        // Andar de lado: direção (a posição no blend tree é a velocidade natural de cada clipe, no "PassoX/PassoZ").
        static readonly (string clip, Vector2 dir)[] BasicStrafe =
        {
            ("Forward", new Vector2(0f, 1f)), ("Backward", new Vector2(0f, -1f)), ("Left", new Vector2(-1f, 0f)), ("Right", new Vector2(1f, 0f)),
            ("ForwardLeft", new Vector2(-0.7071f, 0.7071f)), ("ForwardRight", new Vector2(0.7071f, 0.7071f)),
            ("BackwardLeft", new Vector2(-0.7071f, -0.7071f)), ("BackwardRight", new Vector2(0.7071f, -0.7071f)),
        };

        /// <summary>
        /// Passada que acompanha a velocidade (06/11, revisão das animações do chat "Animador"): os clipes do pacote andam 1,9 (andar), 3,8
        /// (correr) e 5,7 m/s (disparar) num humano de escala 1; os nossos personagens são cabeçudos (escala humana 0,59) e o jogo os move a
        /// 2,4 (andar) e 6 m/s (correr): as pernas tocavam a ~40% do que o corpo andava e o pé apoiado escorregava 1,7–4 m/s. Agora a
        /// locomoção vai por "Passo" = velocidade ÷ (escala do objeto × escala humana do avatar), com os limites do blend tree na velocidade
        /// natural de cada clipe (medida no clipe com root motion): o pé fica parado no chão em qualquer velocidade até a disparada. Acima
        /// dela a "MotionSpeed" (o driver) acelera o ciclo até <see cref="StrideCadenceCap"/>. Os parâmetros "PassoMax" e "PassoLateralMax"
        /// levam no valor padrão a velocidade do clipe mais rápido de cada blend tree (o driver lê no Awake). "Speed" continua o de antes
        /// (velocidade ÷ escala do objeto) para as transições do tronco.
        /// </summary>
        const float StrideCadenceCap = 2f;
        static readonly float[] StrideFallback = { 1.9f, 3.8f, 5.69f };

        /// <summary>Velocidade natural (m/s, escala humana 1) da versão com root motion do clipe; sem ela, <paramref name="fallback"/>.</summary>
        static float NaturalSpeed(string relative, float fallback)
        {
            int slash = relative.LastIndexOf('/');
            string path = KevinMaleFolder + "/" + relative.Substring(0, slash) + "/RootMotion" + relative.Substring(slash) + " [RM].fbx";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.averageSpeed.magnitude > 0.1f)
                    return clip.averageSpeed.magnitude;
            return fallback;
        }

        static void SetDefaultFloat(AnimatorController controller, string name, float value)
        {
            AddParameter(controller, name, AnimatorControllerParameterType.Float);
            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++) if (parameters[i].name == name) parameters[i].defaultFloat = value;
            controller.parameters = parameters;
        }

        /// <summary>Parado, andar, correr e disparar pelo "Passo", nos limites da velocidade natural de cada clipe.</summary>
        static void UseStrideLocomotion(AnimatorController controller, AnimatorState state, Motion idle, Motion walk, Motion run, Motion sprint)
        {
            if (state.motion is not BlendTree tree) return;
            float walkAt = NaturalSpeed(BasicWalk, StrideFallback[0]), runAt = NaturalSpeed(BasicRun, StrideFallback[1]);
            float sprintAt = sprint ? NaturalSpeed(BasicSprint, StrideFallback[2]) : runAt;
            AddParameter(controller, "Passo", AnimatorControllerParameterType.Float);
            SetDefaultFloat(controller, "PassoMax", sprintAt);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Passo";
            tree.useAutomaticThresholds = false;
            var children = new List<ChildMotion>
            {
                new() { motion = idle, threshold = 0f, timeScale = 1f },
                new() { motion = walk, threshold = walkAt, timeScale = 1f },
                new() { motion = run, threshold = runAt, timeScale = 1f },
            };
            if (sprint) children.Add(new ChildMotion { motion = sprint, threshold = sprintAt, timeScale = 1f });
            tree.children = children.ToArray();
            EditorUtility.SetDirty(tree);
            AddParameter(controller, "MotionSpeed", AnimatorControllerParameterType.Float);
            state.speedParameterActive = true;
            state.speedParameter = "MotionSpeed";
        }

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
                        UseStrideLocomotion(controller, state, idle, walk, run, BasicMotion(BasicSprint));
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
                list.Add((walk, dir * NaturalSpeed($"Movement/Walk/HumanM@Walk01_{name}", StrideFallback[0])));
                list.Add((run, dir * NaturalSpeed($"Movement/Run/HumanM@Run01_{name}", StrideFallback[1])));
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
