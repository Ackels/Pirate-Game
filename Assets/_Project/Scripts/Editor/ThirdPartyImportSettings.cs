using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Configura sozinho os modelos de pacotes grátis em Assets/ThirdParty (Kenney, Quaternius; CC0): sem câmeras, luzes
    /// nem colisores (a física é a dos protótipos) e animação só se o FBX tiver. A maioria dos kits pinta tudo com uma
    /// textura de paleta (colormap): entram sem materiais (o gerador põe os nossos, URP). O Nature Kit e as ruínas do
    /// Quaternius pintam com uma cor por material: esses entram com materiais URP Lit da cor do FBX (o gerador troca por cópias compartilhadas).
    /// A paleta (colormap) entra sem filtro, sem mipmap e sem compressão: cada cor é um quadradinho, e misturar vizinhos
    /// (de longe ou comprimindo) mudaria a cor dos modelos. As peças de interface (UI/) entram com filtro bilinear; as
    /// texturas de partícula (VFX/) com filtro, mipmap e 256 px.
    /// Sons (Audio/): música em streaming (Vorbis, carrega em segundo plano); ambiente em loop comprimido na memória;
    /// efeitos curtos descomprimidos ao carregar (ADPCM). Tudo que toca no espaço (3D) vira mono — clipe estéreo numa
    /// fonte 3D perde um canal; interface e vinhetas (2D) ficam em estéreo.
    /// </summary>
    class ThirdPartyImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/ThirdParty";
        /// <summary>Kits com cor por material (sem textura).</summary>
        public const string ColorMaterialFolder = Folder + "/Kenney_NatureKit";
        /// <summary>Ultimate Modular Ruins do Quaternius (19/10): também uma cor por material.</summary>
        public const string RuinsFolder = Folder + "/Quaternius_ModularRuins";
        /// <summary>Animated Monster Pack do Quaternius (19/10): também uma cor por material.</summary>
        public const string AnimatedMonstersFolder = Folder + "/Quaternius_AnimatedMonsters";
        /// <summary>Peças de interface (Kenney UI Pack): filtro bilinear, não a paleta sem filtro dos modelos.</summary>
        public const string UiFolder = Folder + "/UI";
        /// <summary>Texturas de partícula (Kenney Particle Pack): filtro, mipmap e 256 px comprimido.</summary>
        public const string VfxFolder = Folder + "/VFX";

        string NormalizedPath => assetPath.Replace('\\', '/');
        bool InFolder => NormalizedPath.StartsWith(Folder + "/");
        bool UsesColorMaterials => NormalizedPath.StartsWith(ColorMaterialFolder + "/") || NormalizedPath.StartsWith(RuinsFolder + "/")
            || NormalizedPath.StartsWith(AnimatedMonstersFolder + "/");

        void OnPreprocessModel()
        {
            if (!InFolder) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = UsesColorMaterials
                ? ModelImporterMaterialImportMode.ImportViaMaterialDescription
                : ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
        }

        /// <summary>Personagens do Quaternius trazem as animações no FBX: os ciclos (parado, andar, correr, nadar) em loop.</summary>
        void OnPreprocessAnimation()
        {
            if (!InFolder) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) return;
            foreach (var clip in clips)
            {
                string n = clip.name.ToLowerInvariant();
                clip.loopTime = n.EndsWith("idle") || n.EndsWith("walk") || n.EndsWith("run") || n.Contains("swim");
            }
            importer.clipAnimations = clips;
        }

        public const string AudioFolder = Folder + "/Audio";

        void OnPreprocessAudio()
        {
            if (!NormalizedPath.StartsWith(AudioFolder + "/")) return;
            var importer = (AudioImporter)assetImporter;
            string sub = NormalizedPath.Substring(AudioFolder.Length + 1);
            var settings = importer.defaultSampleSettings;
            if (sub.StartsWith("Musica/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.forceToMono = false;
                importer.loadInBackground = true;
            }
            else if (sub.StartsWith("Clima/"))
            {
                // Chuva e trovões tocam em 2D: ficam em estéreo (loops longos comprimidos na memória).
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.forceToMono = false;
            }
            else if (sub.StartsWith("Ambiente/"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.55f;
                importer.forceToMono = true;
            }
            else
            {
                bool twoD = sub.StartsWith("Kenney_InterfaceSounds/") || sub.StartsWith("Kenney_MusicJingles/");
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                importer.forceToMono = !twoD;
            }
            importer.defaultSampleSettings = settings;
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
        {
            if (!UsesColorMaterials) return;
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            var color = description.TryGetProperty("DiffuseColor", out Vector4 diffuse)
                ? new Color(diffuse.x, diffuse.y, diffuse.z, 1f)
                : Color.white;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
        }

        void OnPreprocessTexture()
        {
            if (!InFolder) return;
            var importer = (TextureImporter)assetImporter;
            if (NormalizedPath.StartsWith(UiFolder + "/"))
            {
                // Interface: peças desenhadas (painéis, faixas, ícones) com filtro, para escalar com a tela sem serrilhar.
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (NormalizedPath.StartsWith(VfxFolder + "/"))
            {
                // Partículas: pequenas na tela e muitas por vez; 256 px com mipmap basta (o original é 512).
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Compressed;
                return;
            }
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
