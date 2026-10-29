using System.Collections.Generic;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Human Melee Animations FREE (Kevin Iglesias, Asset Store; 27/10, pedido do Jhonata: "melhorar as animações melee dos golpes do nosso
    /// personagem e dos inimigos"). O pacote grátis tem poucos golpes; entra assim, sem trocar o que já foi aprovado:
    /// <b>postura de luta</b> com a espada de uma mão ("CombatIdle1H01", em loop: espada baixa à frente, respirando) no lugar da pose congelada no
    /// fim do saque ("EspadaPronta": todos de cutelo, sabre e adaga, capitão e inimigos); <b>terceiro golpe dos piratas</b> e dos sabres da
    /// Guarda Negra ("Attack1H01_R": corte forte da direita, impacto aos 0,29 s do clipe; estado "Golpe4", AttackVariant 30, do começo do
    /// clipe), sorteado com os dois cortes de antes (o capitão continua com o combo dele); mais uma <b>reação ao golpe</b> ("CombatDamage01") e mais uma
    /// <b>morte</b> ("Death01", cai de costas) entre as sorteadas. De fora: o golpe da mão esquerda (no capitão é o gancho), as posturas de duas
    /// mãos e de punhos e o golpe de duas mãos (os do espadão foram aprovados em 23/10).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string MeleeReady = "Combat/1H/HumanM@CombatIdle1H01", MeleeSlash = "Combat/1H/HumanM@Attack1H01_R",
            MeleeHit = "Combat/HumanM@CombatDamage01", MeleeDeath = "Combat/HumanM@Death01";
        const int EnemySlashVariant = 30;
        const float MeleeSlashImpact = 0.29f, MeleeSlashSpeed = 1f;

        /// <summary>Postura de luta do pacote no estado de pronto (em loop; antes era o fim do saque congelado pelo "PoseTime").</summary>
        static void UseMeleeReadyPose(AnimatorState ready)
        {
            var clip = BasicMotion(MeleeReady);
            if (!clip) return;
            ready.motion = clip;
            ready.timeParameterActive = false;
        }

        /// <summary>"Golpe4": o corte forte do pacote, para o terceiro golpe do cutelo dos inimigos (AttackVariant 30).</summary>
        static void AddMeleeSlash(AnimatorStateMachine sm, AnimatorState ready)
        {
            var clip = BasicMotion(MeleeSlash);
            if (!clip) return;
            var state = sm.AddState("Golpe4", new Vector3(600f, -220f));
            state.motion = clip;
            state.tag = FullBodyTag;
            state.speed = MeleeSlashSpeed;
            state.speedParameterActive = true;
            state.speedParameter = "AttackSpeed";
            Any(sm, state, 0f, 0.1f, ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 0),
                ("AttackVariant", AnimatorConditionMode.Equals, EnemySlashVariant));
            Exit(state, ready, Mathf.Min(0.95f, (MeleeSlashImpact + 0.5f) / clip.length), 0.25f);
        }

        /// <summary>Cutelo dos inimigos (piratas, Guarda Negra) com o terceiro golpe (refeito a cada geração: o asset é preservado).</summary>
        static WeaponDefinition EnemyMeleeMoves(WeaponDefinition w)
        {
            if (!w || !BasicMotion(MeleeSlash)) return w;
            float delay = w.hitDelay;
            w.attackVariants = 3;
            // Sorteado, sem repetir o anterior: a IA dos piratas emenda no máximo 2 golpes e, em sequência, o terceiro quase nunca saía.
            w.randomVariants = true;
            w.animatorVariants = new[] { 0, 1, EnemySlashVariant };
            w.variantHitDelays = new[] { delay, delay, MeleeSlashImpact / (MeleeSlashSpeed * Mathf.Max(0.1f, w.attackAnimSpeed)) };
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>Reação e morte do pacote como mais uma variação sorteada (Golpeado_N e Morte_N depois das do Mixamo).</summary>
        static void AddMeleeReactions(AnimatorStateMachine sm, AnimatorState empty, List<string> hits, List<string> deaths)
        {
            var hit = BasicMotion(MeleeHit);
            if (hit)
            {
                var state = sm.AddState($"Golpeado_{hits.Count}", new Vector3(900f, 60f * hits.Count));
                state.motion = hit;
                state.speed = 1.3f;
                state.tag = FullBodyTag;
                Any(sm, state, 0f, 0.1f, ("Hit", AnimatorConditionMode.If, 0), ("HitVariant", AnimatorConditionMode.Equals, hits.Count));
                Exit(state, empty, 0.8f, 0.2f);
                hits.Add(hit.name);
            }
            var death = BasicMotion(MeleeDeath);
            if (death)
            {
                var state = sm.AddState($"Morte_{deaths.Count}", new Vector3(1200f, 60f * deaths.Count));
                state.motion = death;
                state.speed = 1.1f;
                state.tag = FullBodyTag;
                var toDeath = sm.AddAnyStateTransition(state);
                toDeath.hasExitTime = false;
                toDeath.duration = 0.15f;
                toDeath.canTransitionToSelf = false;
                toDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
                toDeath.AddCondition(AnimatorConditionMode.Equals, deaths.Count, "DeathVariant");
                toDeath.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
                deaths.Add(death.name);
            }
        }
    }
}
