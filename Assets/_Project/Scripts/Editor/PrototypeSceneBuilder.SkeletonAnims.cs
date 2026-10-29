using System.Collections.Generic;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Animações próprias dos esqueletos amaldiçoados e mão de apoio nas armas de duas mãos (27/10, pedido do Jhonata: "melhorar as
    /// animações dos esqueletos" com o Skeleton Animations FREE e "ajustar armas... na mão" com o IK Helper Tool, ambos do Kevin Iglesias,
    /// em ThirdParty/Loja/KevinIglesias).
    /// Esqueletos: um AnimatorOverrideController do nosso controller troca o parado e a pose de luta (corcunda, cutelo baixo), o levar golpe
    /// e a morte (desaba num monte); o golpe é o "Attack01" em estados próprios ("Esqueleto_Golpe0/1", AttackVariant 20/21) que entram do
    /// começo do clipe (as transições do cutelo comum começam adiantadas e pulariam o preparo). O andar do pacote é um arrastar de ~0,4 m/s:
    /// nos esqueletos, que andam a 2 e correm a 4,6, escorregaria, então as pernas continuam as do Starter.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string SkeletonAnimFolder = "Assets/ThirdParty/Loja/KevinIglesias/Skeleton Animations/Animations";
        const int SkeletonVariantOffset = 20;

        // "Attack01" (1,43 s): preparo com o cutelo para trás e o corte para a frente aos ~0,47 s (mão mais rápida). O segundo golpe do
        // combo entra 0,12 s adiantado e mais rápido (resposta curta, como o 2º corte dos piratas).
        static readonly (float start, float speed)[] SkeletonAttacks = { (0f, 1f), (0.12f, 1.2f) };
        const float SkeletonImpact = 0.47f;

        // Clipes do nosso controller (nome do clipe) que os esqueletos trocam e pelo quê.
        static readonly (string from, string to)[] SkeletonOverrides =
        {
            ("Idle", "Skeleton@Idle01"), ("HumanM@Idle01", "Skeleton@Idle01"), ("HumanM@CombatIdle1H01", "Skeleton@Idle01"),
            ("HumanM@CombatDamage01", "Skeleton@Damage01"), ("HumanM@Death01", "Skeleton@Death01"),
            ("X Bot@Withdrawing Sword", "Skeleton@Idle01"),
            ("X Bot@Head Hit", "Skeleton@Damage01"), ("X Bot@Hit To Body", "Skeleton@Damage01"),
            ("X Bot@Rib Hit", "Skeleton@Damage01"), ("X Bot@Stomach Hit", "Skeleton@Damage01"),
            ("X Bot@Dying Backwards", "Skeleton@Death01"), ("X Bot@Dying", "Skeleton@Death01"),
            ("X Bot@Shoulder Hit And Fall", "Skeleton@Death01"),
        };

        // Mão de apoio (WeaponDefinition.supportOffset, fração do comprimento a partir da mão direita): medido em 27/10, a mão direita fica a
        // ~32% da coronha e a esquerda dos clipes a 61–78% (o bacamarte/mosquete); no espadão a direita fica no pomo e a esquerda vai logo acima.
        const float RifleSupport = 0.3f, GreatswordSupport = 0.07f;

        /// <summary>Clipe do Skeleton Animations pelo fim do nome do arquivo ("Skeleton@Attack01").</summary>
        static AnimationClip SkeletonClip(string file)
        {
            if (!AssetDatabase.IsValidFolder(SkeletonAnimFolder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { SkeletonAnimFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileNameWithoutExtension(path).Equals(file, System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        /// <summary>Golpes do esqueleto no tronco (WeaponType 0, AttackVariant 20/21), do começo do clipe, com a velocidade do AttackSpeed.</summary>
        static void AddSkeletonAttacks(AnimatorStateMachine sm, AnimatorState ready)
        {
            var clip = SkeletonClip("Skeleton@Attack01");
            if (!clip) return;
            for (int v = 0; v < SkeletonAttacks.Length; v++)
            {
                var (start, speed) = SkeletonAttacks[v];
                var state = sm.AddState($"Esqueleto_Golpe{v}", new Vector3(1150f, -60f + 80f * v));
                state.motion = clip;
                state.tag = FullBodyTag;
                state.speed = speed;
                state.speedParameterActive = true;
                state.speedParameter = "AttackSpeed";
                Any(sm, state, start / clip.length, 0.08f,
                    ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 0),
                    ("AttackVariant", AnimatorConditionMode.Equals, SkeletonVariantOffset + v));
                Exit(state, ready, Mathf.Min(0.95f, (SkeletonImpact + 0.5f) / clip.length), 0.25f);
            }
        }

        /// <summary>Cutelo dos esqueletos com os golpes deles (refeito a cada geração: o asset é preservado).</summary>
        static WeaponDefinition SkeletonMoves(WeaponDefinition w)
        {
            if (!w || !SkeletonClip("Skeleton@Attack01")) return w;
            w.attackAnimSpeed = 1f;
            w.attackVariants = SkeletonAttacks.Length;
            w.animatorVariants = new int[SkeletonAttacks.Length];
            w.variantHitDelays = new float[SkeletonAttacks.Length];
            for (int v = 0; v < SkeletonAttacks.Length; v++)
            {
                w.animatorVariants[v] = SkeletonVariantOffset + v;
                w.variantHitDelays[v] = (SkeletonImpact - SkeletonAttacks[v].start) / SkeletonAttacks[v].speed;
            }
            w.hitDelay = w.variantHitDelays[0];
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>Override do nosso controller para os esqueletos (refeito a cada geração, no mesmo arquivo).</summary>
        static RuntimeAnimatorController SkeletonAnimator(RuntimeAnimatorController controller)
        {
            if (!controller || !SkeletonClip("Skeleton@Idle01")) return controller;
            string path = $"{AnimationFolder}/Esqueleto.overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            bool created = !overrides;
            if (created) overrides = new AnimatorOverrideController(controller);
            else overrides.runtimeAnimatorController = controller;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var clip in overrides.animationClips)
            {
                AnimationClip to = null;
                foreach (var (from, file) in SkeletonOverrides)
                    if (clip.name == from) to = SkeletonClip(file);
                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, to));
            }
            overrides.ApplyOverrides(pairs);
            if (created) AssetDatabase.CreateAsset(overrides, path);
            else EditorUtility.SetDirty(overrides);
            return overrides;
        }

        /// <summary>Arma com mão de apoio (refeito a cada geração).</summary>
        static WeaponDefinition WithSupportHand(WeaponDefinition w, float offset)
        {
            if (w && !Mathf.Approximately(w.supportOffset, offset))
            {
                w.supportOffset = offset;
                EditorUtility.SetDirty(w);
            }
            return w;
        }

        /// <summary>"IK Pass" nas camadas do tronco e do corpo inteiro (a mão de apoio vale nas duas; a de base é coberta por elas).</summary>
        static void EnableSupportHandIK(AnimatorController controller)
        {
            var layers = controller.layers;
            for (int i = 1; i < layers.Length; i++)
                if (layers[i].name == AttackLayerName || layers[i].name == FullBodyLayerName) layers[i].iKPass = true;
            controller.layers = layers;
        }
    }
}
