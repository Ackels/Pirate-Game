using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Esqueletos do KayKit (Skeletons 1.1, CC0, em ThirdParty/KayKit_Skeletons; pedido do Jhonata em 04/11: "fica muito maçante o mesmo
    /// tipo de inimigo"): a tripulação amaldiçoada do galeão Tormenta. O rig já vem em pose T com ossos de nome padrão (hips, spine, chest,
    /// head, upperarm.l...): o avatar humanoide sai direto, e eles usam o nosso Animator (as mesmas animações e o combate dos piratas).
    /// Mão = o osso "wrist" (o "hand" do kit é a palma); "Quadril_Ref" no quadril, como nos personagens do Quaternius.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string KayKitFolder = "Assets/ThirdParty/KayKit_Skeletons/Models";
        const float KayKitScale = 0.75f;

        /// <summary>KayKit Adventurers 2.0 (CC0, baixado pelo Jhonata em 04/11): mesmo rig dos esqueletos; uma textura por personagem.</summary>
        const string KayKitAdventurersFolder = "Assets/ThirdParty/KayKit_Adventurers/Models";

        static class KayKitCharacters
        {
            public const string Warrior = "Skeleton_Warrior", Mage = "Skeleton_Mage", Rogue = "Skeleton_Rogue", Minion = "Skeleton_Minion";
            public const string Barbarian = "Barbarian", Knight = "Knight", Bandit = "Rogue", Hooded = "Rogue_Hooded", Ranger = "Ranger",
                Wizard = "Mage";
        }

        static string KayKitPath(string model)
        {
            if (model == null) return null;
            foreach (var folder in new[] { KayKitFolder, KayKitAdventurersFolder })
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{model}.fbx")) return $"{folder}/{model}.fbx";
            return null;
        }

        static bool IsKayKit(string model) => KayKitPath(model) != null;

        /// <summary>Material do personagem: a textura dele (esqueletos: a do pacote; aventureiros: barbarian_texture, knight_texture...).</summary>
        static Material KayKitBody(string model)
        {
            if (model.StartsWith("Skeleton_")) return KayKitMaterial;
            string key = model.Replace("_Hooded", "").ToLowerInvariant();
            return GetTexturedMaterial("M_KayKit_" + model,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{KayKitAdventurersFolder}/Textures/{key}_texture.png"), 0.12f);
        }

        static Material KayKitMaterial => GetTexturedMaterial("M_KayKit_Esqueleto",
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{KayKitFolder}/Textures/skeleton_texture.png"), 0.12f);

        /// <summary>Olhos acesos (amarelo da maldição; o kit pinta os olhos de amarelo na textura e eles brilham no material).</summary>
        static Material KayKitEyes()
        {
            var mat = GetTexturedMaterial("M_KayKit_Olhos", AssetDatabase.LoadAssetAtPath<Texture2D>($"{KayKitFolder}/Textures/skeleton_texture.png"), 0.1f);
            if (!KeepsEmission(mat) && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.22f) * 1.8f);
                Emissive(mat);
            }
            return mat;
        }

        /// <summary>Cópia do esqueleto do KayKit ("Model") com o material e o avatar humanoide.</summary>
        static GameObject HumanoidKayKit(string model, Transform parent)
        {
            string path = KayKitPath(model);
            // Os esqueletos do kit têm ~2,4 m (cabeçudos): na escala 0,75 ficam do tamanho dos piratas (~1,8 m). Na importação, para os
            // ossos continuarem em escala 1 (as armas e a bainha são medidas no espaço deles).
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !Mathf.Approximately(importer.globalScale, KayKitScale))
            {
                importer.globalScale = KayKitScale;
                importer.SaveAndReimport();
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Model";
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var body = KayKitBody(model);
            var eyes = KayKitEyes();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = r.name.EndsWith("_Eyes") ? eyes : body;
                r.sharedMaterials = slots;
            }
            if (!go.TryGetComponent(out Animator animator)) animator = go.AddComponent<Animator>();
            animator.avatar = KayKitAvatar(go, model);
            return go;
        }

        static Avatar KayKitAvatar(GameObject go, string model)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name, t);
            Transform B(string name) => bones.TryGetValue(name, out var t) ? t : throw new System.Exception($"{model}: sem o osso {name}");
            var original = new Dictionary<Transform, Quaternion>();
            foreach (var t in bones.Values) original[t] = t.localRotation;

            var hipsRef = new GameObject(HipsReference).transform;
            hipsRef.SetParent(B("hips"), false);
            // Mãos e quadril na orientação das do boneco do Starter na pose T (as pegadas e a bainha são medidas nessa base).
            var root = go.transform;
            StarterTPose(out var starterLeftHand, out var starterRightHand, out var starterHips);
            B("wrist.l").rotation = root.rotation * starterLeftHand;
            B("wrist.r").rotation = root.rotation * starterRightHand;
            hipsRef.rotation = root.rotation * starterHips;

            var skeleton = new List<SkeletonBone>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            var map = new (string human, string bone)[]
            {
                ("Hips", "hips"), ("Spine", "spine"), ("Chest", "chest"), ("Head", "head"),
                ("LeftUpperArm", "upperarm.l"), ("LeftLowerArm", "lowerarm.l"), ("LeftHand", "wrist.l"),
                ("RightUpperArm", "upperarm.r"), ("RightLowerArm", "lowerarm.r"), ("RightHand", "wrist.r"),
                ("LeftUpperLeg", "upperleg.l"), ("LeftLowerLeg", "lowerleg.l"), ("LeftFoot", "foot.l"), ("LeftToes", "toes.l"),
                ("RightUpperLeg", "upperleg.r"), ("RightLowerLeg", "lowerleg.r"), ("RightFoot", "foot.r"), ("RightToes", "toes.r"),
            };
            var human = new List<HumanBone>();
            foreach (var (h, b) in map)
            {
                var bone = new HumanBone { humanName = h, boneName = b };
                bone.limit.useDefaultValues = true;
                human.Add(bone);
            }
            var description = new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton.ToArray(),
                upperArmTwist = AvatarTwistShare, lowerArmTwist = AvatarTwistShare, upperLegTwist = AvatarTwistShare, lowerLegTwist = AvatarTwistShare,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var built = AvatarBuilder.BuildHumanAvatar(go, description);
            built.name = model + "_Humano";
            foreach (var kv in original) if (kv.Key) kv.Key.localRotation = kv.Value;
            if (!built.isValid || !built.isHuman) throw new System.Exception($"{model}: avatar humanoide inválido");
            return SaveAvatar(built);
        }
    }
}
