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
                clip.loopTime = lower.Contains("idle") || lower.Contains("run") || lower.Contains("walk") || lower.Contains("aim");
                // "No lugar": giro, altura e posição da raiz ficam na pose (bake into pose), sem mover o objeto.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
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
