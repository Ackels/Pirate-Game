using Pirates.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pirates.UI
{
    /// <summary>
    /// Aplica a parte de tela e gráficos das <see cref="GameOptions"/> ao abrir cada cena e quando mudam. Qualidade mexe no
    /// URP asset em uso (em memória, partindo dos valores do projeto, guardados na primeira vez): <b>Alta</b> = o projeto como
    /// está (MSAA 2×, sombra 50 m, 2 cascatas); <b>Média</b> = sem MSAA e sombra a 80%; <b>Baixa</b> = também 80% da resolução,
    /// sombra a 60% e 1 cascata. No editor o URP asset volta ao original ao sair do Play (é um asset: ficaria salvo), e modo de
    /// tela e resolução só valem no jogo compilado.
    /// </summary>
    public static class GraphicsOptions
    {
        static UniversalRenderPipelineAsset urp;
        static int baseMsaa, baseCascades;
        static float baseScale, baseShadow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Apply();
            GameOptions.Changed += Apply;
            Application.quitting += Restore;
        }

        public static void Apply()
        {
            var o = GameOptions.Current;
            QualitySettings.vSyncCount = o.vSync ? 1 : 0;
            Application.targetFrameRate = !o.vSync && o.fpsLimit > 0 ? o.fpsLimit : -1;
            ApplyQuality(o.quality);
            if (!Application.isEditor) ApplyScreen(o);
        }

        static void ApplyQuality(GameOptions.Quality quality)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!asset) return;
            if (asset != urp)
            {
                // Primeira vez (ou outro perfil): o ponto de partida é o asset como o projeto deixou.
                Restore();
                urp = asset;
                baseMsaa = asset.msaaSampleCount;
                baseScale = asset.renderScale;
                baseShadow = asset.shadowDistance;
                baseCascades = asset.shadowCascadeCount;
            }
            bool high = quality == GameOptions.Quality.High, low = quality == GameOptions.Quality.Low;
            Set(high ? baseMsaa : 1, low ? baseScale * 0.8f : baseScale, baseShadow * (high ? 1f : low ? 0.6f : 0.8f),
                low ? 1 : baseCascades);
        }

        static void Set(int msaa, float scale, float shadow, int cascades)
        {
            if (urp.msaaSampleCount != msaa) urp.msaaSampleCount = msaa;
            if (!Mathf.Approximately(urp.renderScale, scale)) urp.renderScale = scale;
            if (!Mathf.Approximately(urp.shadowDistance, shadow)) urp.shadowDistance = shadow;
            if (urp.shadowCascadeCount != cascades) urp.shadowCascadeCount = cascades;
        }

        /// <summary>Devolve o URP asset aos valores do projeto (sair do Play no editor; trocar de perfil).</summary>
        static void Restore()
        {
            if (!urp) return;
            Set(baseMsaa, baseScale, baseShadow, baseCascades);
            urp = null;
        }

        static void ApplyScreen(GameOptions o)
        {
            if (o.screenMode == GameOptions.DefaultScreenMode && o.width <= 0) return;   // nunca mexido: como o jogo abriu
            var mode = o.screenMode == GameOptions.DefaultScreenMode ? Screen.fullScreenMode : (FullScreenMode)o.screenMode;
            int w = o.width > 0 ? o.width : Screen.currentResolution.width;
            int h = o.height > 0 ? o.height : Screen.currentResolution.height;
            if (mode == FullScreenMode.Windowed && o.width <= 0)
            {
                // Janela sem tamanho escolhido: 80% da tela.
                w = Mathf.RoundToInt(Screen.currentResolution.width * 0.8f);
                h = Mathf.RoundToInt(Screen.currentResolution.height * 0.8f);
            }
            if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode) Screen.SetResolution(w, h, mode);
        }
    }
}
