using UnityEditor;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Configura sozinho os FBX do Mixamo colocados em Assets/_Project/Animation/Mixamo: rig Humanoid (para tocar no
    /// modelo do Starter Assets por retargeting), sem materiais, loop só nos ciclos (idle/run/walk/aim) e "no lugar" (posição e giro da raiz
    /// presos: quem move o personagem é o FootCharacter, não a animação).
    /// </summary>
    class MixamoImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Animation/Mixamo";

        bool InFolder => assetPath.Replace('\\', '/').StartsWith(Folder + "/");

        void OnPreprocessModel()
        {
            if (!InFolder) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
        }

        /// <summary>
        /// Ciclo = pose parada, andar, correr, de lado, mira; menos golpes, pulos, giros, rolamentos e transições ("crouch to standing idle",
        /// "run jump attack", "jump running": pacotes do Mixamo de 09/10).
        /// </summary>
        static bool IsCycle(string lower)
        {
            bool cycleWord = lower.Contains("idle") || lower.Contains("run") || lower.Contains("walk") || lower.Contains("aim") || lower.Contains("strafe");
            bool oneShot = lower.Contains("attack") || lower.Contains("jump") || lower.Contains(" to ") || lower.Contains("turn") || lower.Contains("roll");
            return cycleWord && !oneShot;
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                clip.name = clips.Length == 1 ? name : $"{name}_{i}";
                // Ciclos (correr, pose parada, mira) repetem; golpes, tiros, sacar/guardar tocam uma vez.
                string lower = name.ToLowerInvariant();
                clip.loopTime = IsCycle(lower);
                // "No lugar": giro, altura e posição da raiz ficam na pose (bake into pose), sem mover o objeto.
                // Pacotes de 09/10 (subpastas Combate/Espadao/Machado): vários clipes andam e giram (rolamento 5,5 m, derrubado 1,8 m e 125°,
                // ataques com salto 3 m). Com a raiz na pose o corpo sairia do lugar e voltaria num pulo; fora da pose, o Animator sem root
                // motion descarta o deslocamento: a animação fica no lugar e o FootCharacter move o personagem. A altura fica na pose (pulos).
                bool scriptMoves = assetPath.Replace('\\', '/').Substring(Folder.Length + 1).Contains("/");
                clip.lockRootRotation = !scriptMoves;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = !scriptMoves;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                // O gole do Mixamo é com a mão esquerda; no Barbarossa ela é o gancho, do lado do papagaio (a garrafa sumia atrás
                // dele e da barba): espelhado, ele bebe com a direita, do lado livre (a arma some da mão durante o gole).
                clip.mirror = lower.Contains("drinking");
                clips[i] = clip;
            }
            importer.clipAnimations = clips;
        }
    }
}
