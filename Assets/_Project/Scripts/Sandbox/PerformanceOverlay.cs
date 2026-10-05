using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Sandbox
{
    /// <summary>
    /// Medidor de desempenho (F3): FPS médio, pior quadro, tempo de CPU e GPU e lixo de memória (GC) por quadro.
    /// Cria-se sozinho em qualquer cena; só existe no editor e em builds de desenvolvimento. Os números são da janela
    /// do jogo: no editor sem foco o Unity desacelera, então compare com o editor em foco ou numa build.
    /// </summary>
    public class PerformanceOverlay : MonoBehaviour
    {
        const float Window = 0.5f;

        bool visible;
        float elapsed, worst;
        int frames;
        string text = "";
        GUIStyle style;
        ProfilerRecorder gcAlloc;
        readonly FrameTiming[] timings = new FrameTiming[1];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("PerformanceOverlay (F3)");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceOverlay>();
        }

        void OnEnable()
        {
            useGUILayout = false;
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void OnDisable() => gcAlloc.Dispose();

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) visible = !visible;
            if (!visible) return;

            FrameTimingManager.CaptureFrameTimings();
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            frames++;
            if (dt > worst) worst = dt;
            if (elapsed < Window) return;

            string gpu = "—", cpu = "—";
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                cpu = timings[0].cpuMainThreadFrameTime.ToString("0.0");
                if (timings[0].gpuFrameTime > 0) gpu = timings[0].gpuFrameTime.ToString("0.0");
            }
            float gcKb = gcAlloc.Valid ? gcAlloc.LastValue / 1024f : 0f;
            text = $"{frames / elapsed:0} FPS  ({elapsed * 1000f / frames:0.0} ms, pior {worst * 1000f:0.0} ms)\n" +
                   $"CPU {cpu} ms   GPU {gpu} ms   GC {gcKb:0.0} KB/quadro";
            elapsed = 0f;
            frames = 0;
            worst = 0f;
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            var rect = new Rect(Screen.width - 330f, 8f, 322f, 44f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 8f, rect.height), text, style);
        }
    }
}
