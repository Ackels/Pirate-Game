using System.Collections.Generic;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Brutamontes de duas mãos (27/10, pedido do Jhonata: "estranho a forma como ele segura e bate", com os pacotes Dwarf Melee/Basic
    /// Motions FREE do Kevin Iglesias): WeaponType 5, só dele (o capitão continua com o machado de uma mão no 3). Parado em guarda com o
    /// machado duplo nas duas mãos ("CombatIdle2H01" do Human Melee, de quadril reto), correndo com o machado atravessado no peito
    /// ("WeaponHold2H01", pose dos braços do Human Melee feita para ir por cima da corrida) e três golpes de duas mãos sorteados: a <b>machadada de cima</b> do anão ("Attack2H01"), o
    /// <b>varrido</b> e a <b>diagonal</b> com preparo por cima do ombro do espadão da Explosive ("Attack4", "Attack6"; o capitão usa o 11, o 9 e
    /// o 5). Tocados mais devagar que o clipe para o impacto cair onde caía antes (0,70 / 0,60 / 0,77 s; o revés do Mixamo era 1,0 s). A mão
    /// esquerda vai ao cabo pela <see cref="SupportHandIK"/>: escorrega até onde a animação a põe e gira o pulso para o cabo passar pelo punho.
    /// Tentado e descartado (27/10, ajustes pedidos): a guarda do anão (mãos no quadril, braço esticado pelo IK), a guarda da Explosive (de
    /// lado: andando, a torção do peito sobrava e os braços cruzavam, e o pé torcido ficava de sola para cima) e as pernas do anão (joelho bem
    /// dobrado: o pé de nadadeira do tubarão ficava enrolado embaixo do corpo); as pernas são as do Human Basic Motions, como as dos piratas.
    /// A corrida do espadão da Explosive também saiu: o machado ia pendurado de ponta para baixo e a mão esquerda não chegava no cabo.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string KevinDwarfFolder = "Assets/ThirdParty/Loja/KevinIglesias/Dwarf Animations/Animations/Male";
        const string BruteGuard = "Combat/2H/HumanM@CombatIdle2H01", BruteRun = "../Masked Poses/HumanM@WeaponHold2H01";
        const int BruteWeaponType = 5, BruteStateOffset = 6;

        // Impacto = mão direita mais rápida, medido no brutamontes (s do clipe); velocidade do estado = impacto que queremos ÷ esse.
        static readonly (string clip, bool dwarf, float impact, float speed)[] BruteAttacks =
        {
            ("Combat/2H/DwarfM@Attack2H01", true, 0.56f, 0.8f),
            ("Attack4", false, 0.52f, 0.87f),
            ("Attack6", false, 0.46f, 0.6f),
        };

        /// <summary>Quanto o golpe segue depois do impacto antes de voltar à guarda (s de jogo).</summary>
        const float BruteFollow = 0.55f;

        /// <summary>
        /// Mão esquerda no cabo, abaixo da direita: as mãos ficam a ~0,26–0,36 m uma da outra, sem a escala do brutamontes; o machado (× 0,95,
        /// ~1 m sem a escala) tem a cabeça para o lado mais longe da direita, então o negativo vai para o cabo (é o mais longe que ela escorrega).
        /// </summary>
        const float BruteSupport = -0.3f;

        const float BruteAxeScale = 0.95f;
        /// <summary>Pegada da mão direita no cabo do kit (y −0,29 no fim do cabo, cabeças a partir de 0,2): logo abaixo das cabeças.</summary>
        static readonly Vector3 BruteAxeGripPoint = new(0f, 0.1f, 0f);

        static AnimationClip DwarfClip(string relative)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{KevinDwarfFolder}/{relative}.fbx"))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        /// <summary>Pose dos braços da corrida (fora da pasta Male: "../" sai dela).</summary>
        static AnimationClip BruteRunClip()
        {
            string path = KevinMaleFolder.Substring(0, KevinMaleFolder.LastIndexOf('/')) + BruteRun.Substring(2) + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        static AnimationClip BruteAttackClip(int v) => BruteAttacks[v].dwarf ? DwarfClip(BruteAttacks[v].clip) : ExplosiveClip(BruteAttacks[v].clip);

        /// <summary>
        /// "Bruto_Pronto" (guarda), "Bruto_Correndo" e os golpes "Machado_Golpe6..8" (nome que o <see cref="PostureLimiter"/> conhece) pelo
        /// AttackVariant 0–2 com WeaponType 5, do começo do clipe. Sem o Human Melee, nada (o brutamontes fica sem pose de arma).
        /// </summary>
        static void AddBruteAxe(AnimatorStateMachine sm, AnimatorState empty)
        {
            var idle = BasicMotion(BruteGuard);
            if (!idle) return;
            var ready = sm.AddState("Bruto_Pronto", new Vector3(-900f, 420f));
            ready.motion = idle;
            ready.tag = FullBodyTag;
            Go(empty, ready, 0.25f, ("WeaponType", AnimatorConditionMode.Equals, BruteWeaponType));
            Go(ready, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, BruteWeaponType));
            var runClip = BruteRunClip();
            if (runClip)
            {
                var run = sm.AddState("Bruto_Correndo", new Vector3(-900f, 500f));
                run.motion = runClip;
                Go(ready, run, 0.25f, ("Speed", AnimatorConditionMode.Greater, 3f));
                Go(run, ready, 0.25f, ("Speed", AnimatorConditionMode.Less, 2.6f));
                Go(run, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, BruteWeaponType));
            }
            for (int v = 0; v < BruteAttacks.Length; v++)
            {
                var clip = BruteAttackClip(v);
                if (!clip) continue;
                var (_, _, impact, speed) = BruteAttacks[v];
                var swing = sm.AddState($"Machado_Golpe{BruteStateOffset + v}", new Vector3(-1200f, 420f + 80f * v));
                swing.motion = clip;
                swing.speed = speed;
                swing.tag = FullBodyTag;
                swing.speedParameterActive = true;
                swing.speedParameter = "AttackSpeed";
                Any(sm, swing, 0f, HeavyAttackBlend, ("Attack", AnimatorConditionMode.If, 0),
                    ("WeaponType", AnimatorConditionMode.Equals, BruteWeaponType), ("AttackVariant", AnimatorConditionMode.Equals, v));
                Exit(swing, ready, Mathf.Min(0.95f, (impact + BruteFollow * speed) / clip.length), 0.3f);
            }
        }

        /// <summary>Tempos e mão de apoio do machado do brutamontes (refeito a cada geração: o asset é preservado).</summary>
        static WeaponDefinition BruteMoves(WeaponDefinition w)
        {
            if (!w || !BasicMotion(BruteGuard)) return w;
            w.animatorWeaponType = BruteWeaponType;
            w.attackVariants = BruteAttacks.Length;
            w.animatorVariants = new int[0];
            w.variantHitDelays = System.Array.ConvertAll(BruteAttacks, a => a.impact / a.speed);
            w.hitDelay = w.variantHitDelays[0];
            w.attackAnimSpeed = 1f;
            w.randomVariants = true;
            w.supportOffset = BruteSupport;
            w.supportSlides = true;
            w.supportSlideGap = 0.12f;
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>
        /// Eixo do cabo (para a cabeça) e o plano das lâminas no espaço do osso da mão direita: o cabo passa pelas duas mãos (da esquerda
        /// para a direita) no pronto, na corrida e no impacto de cada golpe (procura numa esfera de direções; a pior pose pesa 2×), e as
        /// lâminas no plano do movimento da mão no impacto. false sem os clipes.
        /// </summary>
        static bool BruteAxeGrip(Transform model, out Vector3 handle, out Vector3 edge)
        {
            handle = Vector3.forward;
            edge = Vector3.up;
            var temp = MeasureCopy(model);
            if (!temp) return false;
            var anim = temp.GetComponent<Animator>();
            // A guarda pesa 3× (é a pose que fica na tela).
            var samples = new List<(AnimationClip clip, float t, bool impact)>
            {
                (BasicMotion(BruteGuard), 0.1f, false), (BasicMotion(BruteGuard), 0.3f, false), (BasicMotion(BruteGuard), 0.5f, false),
                (BasicMotion(BruteGuard), 0.7f, false), (BasicMotion(BruteGuard), 0.9f, false), (BasicMotion(BruteGuard), 1.15f, false),
                (BruteRunClip(), 0.1f, false), (BruteRunClip(), 0.1f, false),
            };
            for (int v = 0; v < BruteAttacks.Length; v++) samples.Add((BruteAttackClip(v), BruteAttacks[v].impact, true));
            var rots = new List<Quaternion>();
            var wants = new List<Vector3>();
            var moves = new List<Vector3>();
            foreach (var (clip, t, impact) in samples)
            {
                if (!clip) continue;
                var right = anim.GetBoneTransform(HumanBodyBones.RightHand);
                var left = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                clip.SampleAnimation(temp, t - 0.03f);
                Vector3 before = right.position;
                clip.SampleAnimation(temp, t);
                rots.Add(right.rotation);
                wants.Add((right.position - left.position).normalized);
                moves.Add(impact ? right.position - before : Vector3.zero);
            }
            Object.DestroyImmediate(temp);
            if (rots.Count < 4) return false;

            float best = float.NegativeInfinity;
            const int n = 4000;
            for (int i = 0; i < n; i++)
            {
                float z = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(1f - z * z), phi = i * 2.39996f;
                var dir = new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);
                float score = 0f, worst = 9f;
                for (int s = 0; s < rots.Count; s++)
                {
                    float d = Vector3.Dot(rots[s] * dir, wants[s]);
                    score += d;
                    worst = Mathf.Min(worst, d);
                }
                score += worst * 2f;
                if (score <= best) continue;
                best = score;
                handle = dir;
            }
            var sum = Vector3.zero;
            for (int s = 0; s < rots.Count; s++)
                if (moves[s] != Vector3.zero)
                    sum += Quaternion.Inverse(rots[s]) * Vector3.ProjectOnPlane(moves[s], rots[s] * handle).normalized;
            var e = Vector3.ProjectOnPlane(sum, handle);
            edge = e.sqrMagnitude > 0.0001f ? e.normalized : Vector3.ProjectOnPlane(Vector3.up, handle).normalized;
            return true;
        }
    }
}
