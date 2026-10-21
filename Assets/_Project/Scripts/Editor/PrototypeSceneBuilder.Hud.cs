using Pirates.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Pirates.EditorTools
{
    /// <summary>
    /// HUD de jogo em UI Toolkit: o PanelSettings (escala com a tela, referência 1600×900), o tema e as fontes SDF
    /// (Pirata One nos títulos, Alegreya no texto, com negrito e itálico ligados), todos em <c>_Project/UI</c>. O visual fica
    /// em <c>_Project/UI/Hud.uss</c> (escrito à mão; usa as peças do Kenney UI Pack em ThirdParty/UI e essas fontes).
    /// Assets existentes são mantidos (as fontes dinâmicas ganham letras novas sozinhas).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string HudFolder = Root + "/UI";
        const string HudFontsFolder = HudFolder + "/Fonts";
        const string HudFontSource = ThirdPartyImportSettings.UiFolder + "/Fonts";

        /// <summary>Letras pré-geradas no atlas (o resto entra sozinho quando aparece).</summary>
        const string HudCharset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "ÁÀÂÃÄÉÈÊÍÓÒÔÕÖÚÙÛÜÇáàâãäéèêíóòôõöúùûüçºª°·×—–…“”‘’«»";

        [MenuItem("Pirates/Protótipo/HUD - Criar assets do HUD (fontes, painel)")]
        public static void BuildHudAssets()
        {
            HudAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("[Pirates] Assets do HUD prontos em " + HudFolder);
        }

        /// <summary>Raiz do HUD (UIDocument + GameHud): os HUDs de cada sistema põem as peças nela.</summary>
        static GameHud CreateGameHud()
        {
            var (panel, style) = HudAssets();
            var go = new GameObject("GameHUD");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var hud = go.AddComponent<GameHud>();
            hud.Configure(style);
            // Relógio do dia (M11): some sozinho na cena sem ciclo.
            go.AddComponent<ClockHUD>();
            // Aviso de golpe ("!"/"!!") dos inimigos de terra e das catacumbas (revisão das animações, 21/10).
            go.AddComponent<CryptWarningHUD>();
            // Tela de equipamento (E1): se o capitão já foi montado (cena 06; na 07 ele vem depois e põe ela).
            AttachEquipmentHud();
            return hud;
        }

        static (PanelSettings panel, StyleSheet style) HudAssets()
        {
            EnsureFolder(Root, "UI");
            EnsureFolder(HudFolder, "Fonts");

            var body = HudFont("Alegreya/Alegreya-Regular.ttf", "Alegreya SDF");
            var bold = HudFont("Alegreya/Alegreya-Bold.ttf", "Alegreya Bold SDF");
            var italic = HudFont("Alegreya/Alegreya-Italic.ttf", "Alegreya Italic SDF");
            var boldItalic = HudFont("Alegreya/Alegreya-BoldItalic.ttf", "Alegreya BoldItalic SDF");
            HudFont("PirataOne/PirataOne-Regular.ttf", "PirataOne SDF");
            // <b> e <i> no texto usam as fontes de verdade, não negrito/itálico falsos.
            var weights = body.fontWeightTable;
            if (weights[7].regularTypeface != bold || weights[4].italicTypeface != italic || weights[7].italicTypeface != boldItalic)
            {
                weights[4].italicTypeface = italic;
                weights[7].regularTypeface = bold;
                weights[7].italicTypeface = boldItalic;
                EditorUtility.SetDirty(body);
            }

            string themePath = HudFolder + "/HudTheme.tss";
            if (!System.IO.File.Exists(themePath))
            {
                System.IO.File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);

            var panel = LoadOrCreateAsset<PanelSettings>(HudFolder + "/HudPanelSettings.asset");
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            // 1600×900: em tela cheia Full HD fica 1,2× (letra confortável); numa janela pequena do editor não some.
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 0;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);

            // O .uss aponta para as fontes: relê depois de criá-las (na primeira vez elas ainda não existiam).
            string stylePath = HudFolder + "/Hud.uss";
            AssetDatabase.ImportAsset(stylePath, ImportAssetOptions.ForceUpdate);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);
            if (!style) Debug.LogError($"[Pirates] Falta o estilo do HUD em {stylePath}.");
            return (panel, style);
        }

        static FontAsset HudFont(string sourceFile, string assetName)
        {
            string path = $"{HudFontsFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (existing) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{HudFontSource}/{sourceFile}");
            if (!font) throw new System.IO.FileNotFoundException($"Fonte do HUD não encontrada: {HudFontSource}/{sourceFile}");
            // SDF: nítida em qualquer tamanho. 64 pt de amostra com folga 8 dá contorno sem serrilhar; o atlas cresce sozinho.
            var asset = FontAsset.CreateFontAsset(font, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(HudCharset, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[Pirates] {assetName}: sem as letras \"{missing}\" (usa a fonte reserva).");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
