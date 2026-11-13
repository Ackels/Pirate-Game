using UnityEditor.Animations;
using UnityEngine;
using Pirates.OnFoot;

namespace Pirates.EditorTools
{
    public static partial class PrototypeSceneBuilder
    {
        // ---------- Sacar as outras armas (12/11, chat "Animador"; Jhonata: "animações para quando o capitão vai sacar alguma arma") ----------
        // Só o cutelo (e as lâminas de uma mão) tinham saque animado; pistola, bacamarte, machados e espadões apareciam na mão de uma vez.
        // Pistola: "Draw Sword 1" do Sword and Shield (Mixamo): a mão passa a 7 cm do cinto (direita do quadril, onde a pistola fica) aos 0,18 s
        // e sobe com ela; o clipe inteiro tem 0,5 s. Costas (bacamarte, machados, espadões): "2Hand-Sword-Unsheath-Back" da Explosive (a mão
        // chega a 14 cm de trás do ombro direito aos 0,33 s, 1 s) e o "Sheath-Back" para guardar (solta aos 0,42 s). A pistola guarda na hora.

        const string PistolDrawClip = "Draw Sword 1";
        const float PistolDrawGrab = 0.18f, PistolDrawLength = 0.5f, PistolDrawSpeed = 1f;
        const float BackDrawGrab = 0.33f, BackSheathRelease = 0.42f, BackDrawLength = 1f, BackDrawSpeed = 1.2f;
        /// <summary>
        /// O "Unsheath-Back" termina com as mãos altas (é de um espadão erguido): com o bacamarte o cano atravessava a cabeça do capitão
        /// (12/11, foto do Jhonata). Sai logo depois de pegar a arma (fração do clipe) e mistura para a pose de segurar em <see cref="BackDrawBlend"/>.
        /// </summary>
        const float BackDrawExit = 0.45f, BackDrawBlend = 0.3f;

        static void AddDrawStates(AnimatorStateMachine sm, AnimatorState empty)
        {
            var pistol = MixamoClip(PistolDrawClip);
            if (pistol)
            {
                var s = sm.AddState("SacarPistola", new Vector3(900f, 100f));
                s.motion = pistol;
                s.speed = PistolDrawSpeed;
                s.tag = FullBodyTag;
                Any(sm, s, 0f, 0.12f, ("Equip", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 1));
                Exit(s, empty, 0.9f, 0.2f);
            }
            var draw = ExplosiveClip("Unsheath-Back-Unarmed");
            if (draw)
            {
                var s = sm.AddState("SacarCostas", new Vector3(900f, 180f));
                s.motion = draw;
                s.speed = BackDrawSpeed;
                s.tag = FullBodyTag;
                Any(sm, s, 0f, 0.15f, ("Equip", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Greater, 1));
                Exit(s, empty, BackDrawExit, BackDrawBlend);
            }
            var sheath = ExplosiveClip("Sheath-Back-Unarmed");
            if (sheath)
            {
                var s = sm.AddState("GuardarCostas", new Vector3(900f, 260f));
                s.motion = sheath;
                s.speed = BackDrawSpeed;
                s.tag = FullBodyTag;
                Any(sm, s, 0f, 0.15f, ("Holster", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Greater, 1));
                Exit(s, empty, 0.9f, 0.25f);
            }
            ApplyDrawTimings();
        }

        /// <summary>Saque animado nas armas do capitão (assets preservados: refeito a cada geração).</summary>
        static void ApplyDrawTimings()
        {
            foreach (var name in new[] { "Weapon_Pistola", "Weapon_Bacamarte", "Weapon_MachadoAbordagem", "Weapon_MachadoCarrasco", "Weapon_Montante", "Weapon_EspadaoBarbaNegra" })
            {
                var w = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{DataFolder}/Weapons/{name}.asset");
                if (w) { SetDrawTimings(w, name == "Weapon_Pistola"); UnityEditor.EditorUtility.SetDirty(w); }
            }
        }

        static void SetDrawTimings(WeaponDefinition w, bool pistol)
        {
            w.animatedDraw = true;
            if (pistol)
            {
                w.drawGrabTime = PistolDrawGrab / PistolDrawSpeed;
                w.drawDuration = PistolDrawLength * 0.9f / PistolDrawSpeed;
                w.instantHolster = true;
            }
            else
            {
                w.drawGrabTime = BackDrawGrab / BackDrawSpeed;
                w.drawDuration = BackDrawLength * BackDrawExit / BackDrawSpeed + BackDrawBlend;
                w.holsterReleaseTime = BackSheathRelease / BackDrawSpeed;
                w.holsterDuration = BackDrawLength * 0.9f / BackDrawSpeed;
                w.instantHolster = false;
            }
        }
    }
}
