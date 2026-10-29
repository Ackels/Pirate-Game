using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.Ambient
{
    /// <summary>
    /// Vigia da luz (29/10): no primeiro build, depois de ~1 min de jogo, toda a cena 3D ficou em cores puras face por face (magenta, ciano,
    /// verde), personagens inclusive, com a interface normal: o jeito da GPU com luz de valor inválido (NaN/infinito). A cada 0,5 s confere
    /// as luzes ligadas, a luz ambiente, a névoa, o reflexo e a câmera; achou valor inválido, conserta (desliga a luz ou volta a um valor
    /// seguro) e anota uma vez no log (Player.log no jogo compilado) quem foi, para achar a causa. Criado sozinho ao carregar a cena.
    /// </summary>
    public class LightingGuard : MonoBehaviour
    {
        const float CheckEvery = 0.5f, RescanEvery = 5f;

        static LightingGuard instance;
        readonly List<Light> lights = new();
        readonly HashSet<string> reported = new();
        float nextCheck, nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance) return;
            var go = new GameObject("VigiaDaLuz");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LightingGuard>();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now < nextCheck) return;
            nextCheck = now + CheckEvery;
            if (now >= nextScan)
            {
                nextScan = now + RescanEvery;
                lights.Clear();
                lights.AddRange(FindObjectsByType<Light>(FindObjectsInactive.Exclude));
            }
            for (int i = 0; i < lights.Count; i++)
                if (lights[i] && lights[i].isActiveAndEnabled) CheckLight(lights[i]);
            CheckEnvironment();
        }

        void CheckLight(Light light)
        {
            var t = light.transform;
            bool badIntensity = !Finite(light.intensity) || light.intensity > 1000f;
            bool badColor = !Finite(light.color);
            bool badPlace = !Finite(t.position) || !Finite(t.rotation) || !Finite(light.range);
            if (!badIntensity && !badColor && !badPlace) return;
            Report(PathOf(t), $"intensidade {light.intensity}, cor {light.color}, posição {t.position}, giro {t.rotation}, alcance {light.range}");
            if (badIntensity) light.intensity = 0f;
            if (badColor) light.color = Color.white;
            if (badPlace) light.enabled = false;
        }

        void CheckEnvironment()
        {
            var probe = RenderSettings.ambientProbe;
            for (int c = 0; c < 3; c++)
            for (int k = 0; k < 9; k++)
            {
                float v = probe[c, k];
                if (Finite(v) && Mathf.Abs(v) < 100f) continue;
                Report("luz ambiente", $"coeficiente [{c},{k}] = {v}");
                var safe = new SphericalHarmonicsL2();
                safe.AddAmbientLight(new Color(0.32f, 0.34f, 0.4f));
                RenderSettings.ambientProbe = safe;
                c = 3;
                break;
            }
            if (!Finite(RenderSettings.fogDensity))
            {
                Report("névoa", $"densidade {RenderSettings.fogDensity}");
                RenderSettings.fogDensity = 0.002f;
            }
            if (!Finite(RenderSettings.fogColor))
            {
                Report("névoa", $"cor {RenderSettings.fogColor}");
                RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.8f);
            }
            if (!Finite(RenderSettings.reflectionIntensity))
            {
                Report("reflexo", $"intensidade {RenderSettings.reflectionIntensity}");
                RenderSettings.reflectionIntensity = 1f;
            }
            var cam = Camera.main;
            if (cam && (!Finite(cam.transform.position) || !Finite(cam.transform.rotation)))
                Report("câmera " + PathOf(cam.transform), $"posição {cam.transform.position}, giro {cam.transform.rotation}");
        }

        void Report(string what, string values)
        {
            if (!reported.Add(what)) return;
            Debug.LogWarning($"[VigiaDaLuz] valor inválido em {what}: {values} (consertado)");
        }

        static string PathOf(Transform t) => t.parent ? $"{PathOf(t.parent)}/{t.name}" : t.name;

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w);
        static bool Finite(Color c) => Finite(c.r) && Finite(c.g) && Finite(c.b);
    }
}
