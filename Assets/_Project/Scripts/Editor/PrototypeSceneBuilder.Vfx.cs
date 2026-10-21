using Pirates.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Efeitos visuais (M6.11): materiais das partículas em <c>Materials/VFX</c> (URP Particles/Unlit com as texturas do
    /// Kenney Particle Pack; aditivo para clarão, brilho e estrela; alfa para fumaça, fogo, água, espuma e rastro do cutelo;
    /// lascas com luz) e o objeto "VFX" com o <see cref="GameVfx"/>, em todas as cenas (rastro dos navios em todas; tiros
    /// e efeitos a pé onde há combate e personagem).
    /// Os materiais são refeitos a cada build (só cor, textura e mistura: nada de ajuste de jogo neles).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string VfxTextures = ThirdPartyImportSettings.VfxFolder + "/Kenney_ParticlePack";

        static void AddVfx()
        {
            EnsureFolder(MaterialsFolder, "VFX");
            var materials = new GameVfx.Materials
            {
                smoke = FxMaterial("M_FX_Fumaca", "smoke_04", additive: false),
                blast = FxMaterial("M_FX_Clarao", "fire_01", additive: true),
                glow = FxMaterial("M_FX_Brilho", "circle_05", additive: true),
                water = FxMaterial("M_FX_Agua", "smoke_04", additive: false),
                drops = FxMaterial("M_FX_Gotas", "circle_05", additive: false),
                ring = FxMaterial("M_FX_Anel", "circle_02", additive: false),
                // Fogo com mistura normal: aditivo some em cima do casco claro.
                fire = FxMaterial("M_FX_Fogo", "flame_05", additive: false),
                splinters = SplinterMaterial(),
                splinterMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                stars = FxMaterial("M_FX_Estrela", "star_06", additive: true),
                trail = TrailMaterial(),
            };
            var go = new GameObject("VFX");
            go.AddComponent<GameVfx>().Configure(materials);
            go.AddComponent<HitFlash>();
        }

        static Material FxMaterial(string name, string texture, bool additive)
        {
            string path = $"{MaterialsFolder}/VFX/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !mat;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            SetIfPresent(mat, "_Surface", 1f);
            SetIfPresent(mat, "_Blend", additive ? 2f : 0f);
            SetIfPresent(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetIfPresent(mat, "_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetIfPresent(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetIfPresent(mat, "_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetIfPresent(mat, "_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            var tex = texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{VfxTextures}/{texture}.png") : null;
            if (!tex && texture != null) Debug.LogWarning($"[Pirates] Textura de partícula não encontrada: {texture}");
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Rastro do cutelo: fita branca sem textura (a forma vem da cor por vértice), vista dos dois lados.</summary>
        static Material TrailMaterial()
        {
            var mat = FxMaterial("M_FX_Rastro", null, additive: false);
            SetIfPresent(mat, "_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lascas de madeira: cubinhos opacos com luz (a cor de cada lasca vem da partícula).</summary>
        static Material SplinterMaterial()
        {
            string path = $"{MaterialsFolder}/VFX/M_FX_Lascas.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")) { name = "M_FX_Lascas" };
            SetIfPresent(mat, "_Surface", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
