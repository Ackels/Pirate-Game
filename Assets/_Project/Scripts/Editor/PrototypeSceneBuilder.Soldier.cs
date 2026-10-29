using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Armas de fogo com o Human Soldier Animations FREE (Kevin Iglesias, Asset Store; 27/10, pedido do Jhonata: "seguir com o Human
    /// Soldier nas armas de fogo"), passo 1: a pistola (capitão e atiradores). Mira com o braço esticado à frente ("Gun_Aim01", no lugar do
    /// "Pistol Aim" do Mixamo), tiro com o coice curto ("Gun_Aim01_Shoot01", no lugar do "Shooting") e recarga com o cano para baixo e a outra
    /// mão socando a carga ("Gun_Reload01", no lugar do "Gunplay", que girava a pistola no dedo). A pose parada (pistola baixa) continua a do
    /// Mixamo: o pacote não tem pistola sem mirar. A mira procedural (<c>CharacterAimRig</c>) mede o braço na pose nova. As personagens do
    /// modelo da Anne pegam as versões "HumanF@" pelo override "Humana".
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string SoldierGunAim = "Combat/Gun/HumanM@Gun_Aim01", SoldierGunShoot = "Combat/Gun/HumanM@Gun_Aim01_Shoot01",
            SoldierGunReload = "Combat/Gun/HumanM@Gun_Reload01";

        /// <summary>Recarga do capitão (s): a do pacote (1,7 s) vai devagar para durar o mesmo; os piratas (3 s) seguram o fim.</summary>
        const float SoldierPistolReloadSeconds = 2.5f;

        /// <summary>Pose de mira da pistola para medir o braço (a do pacote; sem ele, a do Mixamo).</summary>
        static AnimationClip PistolAimClip() => BasicMotion(SoldierGunAim) ?? MixamoClip(ClipPistolAim);

        // Passo 2 (28/10): bacamarte, bacamarteiros e mosqueteira com as poses de rifle (coronha no ombro, cano para a frente).
        const string SoldierRifleAim = "Combat/Rifle/HumanM@Rifle_Aim01", SoldierRifleShoot = "Combat/Rifle/HumanM@Rifle_Aim01_Shoot01",
            SoldierRifleReload = "Combat/Rifle/HumanM@Rifle_Reload01", SoldierRifleHold = "HumanM@WeaponHold_Rifle01";

        /// <summary>Recarga do bacamarte do capitão (s; <c>Weapon_Bacamarte.reloadTime</c>): os bacamarteiros (4,5 s) seguram o fim.</summary>
        const float SoldierRifleReloadSeconds = 3.5f;

        /// <summary>
        /// Pose de mira de rifle para medir a pegada e o braço (a do pacote; sem ele, a do Mixamo). Na do Mixamo o punho fica girado e,
        /// com a pegada medida lá, o bacamarte apontava para o chão nas poses do pacote.
        /// </summary>
        static AnimationClip RifleAimClip() => BasicMotion(SoldierRifleAim) ?? MixamoClip(ClipRifleAim);

        /// <summary>Pose da pasta "Masked Poses" do pacote (ao lado da pasta Male), ou null.</summary>
        static AnimationClip KevinMaskedPose(string file)
        {
            string path = KevinMaleFolder.Substring(0, KevinMaleFolder.LastIndexOf('/')) + "/Masked Poses/" + file + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        /// <summary>Troca os clipes da mira, do tiro e da recarga do bacamarte pelos do pacote (sem ele, ficam os do Mixamo).</summary>
        static void UseSoldierRifle(AnimatorStateMachine sm, string prefix)
        {
            AnimatorState idle = null, run = null, aim = null, shoot = null, reload = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == prefix + "Parado") idle = child.state;
                else if (child.state.name == prefix + "Correndo") run = child.state;
                else if (child.state.name == prefix + "Mirando") aim = child.state;
                else if (child.state.name == prefix + "Tiro") shoot = child.state;
                else if (child.state.name == prefix + "Recarga") reload = child.state;
            }
            var aimClip = BasicMotion(SoldierRifleAim);
            var shootClip = BasicMotion(SoldierRifleShoot);
            var reloadClip = BasicMotion(SoldierRifleReload);
            // Parado e correndo: a pose de segurar rifle (arma atravessada na frente da barriga, cano para a esquerda) só no tronco; as
            // pernas são as da locomoção. Com a pegada medida na mira do pacote, o "Rifle Idle" do Mixamo inclinava a arma ~20° e o
            // "Rifle Run" a deitava (~75°).
            var holdClip = KevinMaskedPose(SoldierRifleHold);
            if (holdClip)
            {
                if (idle)
                {
                    idle.motion = holdClip;
                    idle.tag = "";
                }
                if (run) run.motion = holdClip;
            }
            if (aim && aimClip) aim.motion = aimClip;
            if (shoot && shootClip)
            {
                shoot.motion = shootClip;
                shoot.speed = 1f;
            }
            if (reload && reloadClip)
            {
                reload.motion = reloadClip;
                reload.speed = reloadClip.length / SoldierRifleReloadSeconds;
            }
        }

        /// <summary>Troca os clipes dos estados da pistola pelos do pacote (sem ele, ficam os do Mixamo).</summary>
        static void UseSoldierPistol(AnimatorState aim, AnimatorState shoot, AnimatorState reload)
        {
            var aimClip = BasicMotion(SoldierGunAim);
            var shootClip = BasicMotion(SoldierGunShoot);
            var reloadClip = BasicMotion(SoldierGunReload);
            if (aimClip) aim.motion = aimClip;
            if (shootClip)
            {
                shoot.motion = shootClip;
                shoot.speed = 1f;
            }
            if (reloadClip)
            {
                reload.motion = reloadClip;
                reload.speed = reloadClip.length / SoldierPistolReloadSeconds;
            }
        }
    }
}
