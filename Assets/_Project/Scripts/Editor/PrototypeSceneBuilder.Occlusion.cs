using System.Collections.Generic;
using Pirates.CameraSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// O que pode tapar o personagem fica translúcido (CameraOccluder + CameraOcclusionFader na câmera): casas, árvores,
    /// palmeiras, pedras grandes, torres, cabanas, ruínas, o naufrágio e a fumaça. Cada material URP Lit usado ganha uma
    /// cópia transparente em Materials/Fade ("_Fade"), regerada a cada build para acompanhar o original.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string FadeMaterialsFolder = MaterialsFolder + "/Fade";
        static readonly Dictionary<Material, Material> fadeMaterials = new();

        /// <summary>Marca <paramref name="root"/> (e os filhos) como algo que fica translúcido quando tapa o alvo da câmera.</summary>
        static CameraOccluder Occluder(GameObject root, bool moving = false)
        {
            var originals = new List<Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m && !originals.Contains(m)) originals.Add(m);
            var faded = originals.ConvertAll(FadeMaterial);
            if (!root.TryGetComponent(out CameraOccluder occluder)) occluder = root.AddComponent<CameraOccluder>();
            occluder.Configure(originals.ToArray(), faded.ToArray(), moving);
            return occluder;
        }

        /// <summary>Cópia transparente de um material URP Lit (null para outros shaders: esses não ficam translúcidos).</summary>
        static Material FadeMaterial(Material source)
        {
            if (!source || source.shader.name != "Universal Render Pipeline/Lit") return null;
            if (fadeMaterials.TryGetValue(source, out var cached) && cached) return cached;
            EnsureFolder(MaterialsFolder, "Fade");
            string path = $"{FadeMaterialsFolder}/{source.name}_Fade.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !mat;
            if (created) mat = new Material(source) { name = source.name + "_Fade" };
            else mat.CopyPropertiesFromMaterial(source);

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            SetIfPresent(mat, "_AlphaClip", 0f);
            SetIfPresent(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetIfPresent(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetIfPresent(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetIfPresent(mat, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetIfPresent(mat, "_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.renderQueue = (int)RenderQueue.Transparent;

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            fadeMaterials[source] = mat;
            return mat;
        }
    }
}
