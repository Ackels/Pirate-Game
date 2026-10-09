using System;
using System.IO;
using Pirates.Ambient;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Clima (M11, passo 2). Parte 1, vento: o WindSystem (ajustes em Data/Vento) em todas as cenas; a fumaça do cenário vai com ele
    /// (WindDrift, posto pelo Smoke); navios, espuma do mar, fumaça dos tiros e HUD leem o IWind sozinhos. Riscos de vento na tela
    /// (WindStreaks, fitas do material do rastro do cutelo) e o som do vento (WindAudio no objeto "Audio", com o loop gerado aqui).
    /// Partes 2 e 3, chuva e tempestade: o WeatherSystem (ajustes em Data/Clima) com o clarão e o risco do raio, a chuva na tela
    /// (RainEffect); os sons de chuva, tempestade e trovão (WeatherAudio no objeto "Audio") são gravações CC0 em ThirdParty/Audio/Clima.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string GeneratedAudioFolder = "Assets/_Project/Audio";
        const string WindLoopPath = GeneratedAudioFolder + "/Vento_Gerado.wav";
        /// <summary>Muda a receita do som? Suba a versão: o arquivo é refeito no mesmo lugar (mesmo GUID, o Som_Vento continua apontando).</summary>
        const string WindLoopVersion = "vento-v2";
        const int AudioRate = 44100;

        static WindSettings WindAsset() => LoadOrCreateAsset<WindSettings>(DataFolder + "/Vento.asset");

        static void CreateWind()
        {
            var go = new GameObject("Vento");
            go.AddComponent<WindSystem>().Configure(WindAsset());
            EnsureFolder(MaterialsFolder, "VFX");
            go.AddComponent<WindStreaks>().Configure(TrailMaterial());
        }

        static WeatherSettings WeatherAsset() => LoadOrCreateAsset<WeatherSettings>(DataFolder + "/Clima.asset");

        /// <summary>Clima: tempo que muda, clarão do raio (luz direcional desligada), risco do raio e a chuva na tela.</summary>
        static void CreateWeather()
        {
            var go = new GameObject("Clima");
            var flashGo = new GameObject("Clarao");
            flashGo.transform.SetParent(go.transform, false);
            var flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Directional;
            flash.shadows = LightShadows.None;
            flash.intensity = 0f;
            flash.enabled = false;

            EnsureFolder(MaterialsFolder, "VFX");
            var boltGo = new GameObject("Raio");
            boltGo.transform.SetParent(go.transform, false);
            var bolt = boltGo.AddComponent<LineRenderer>();
            bolt.sharedMaterial = FxMaterial("M_FX_Raio", null, additive: true);
            bolt.useWorldSpace = true;
            bolt.alignment = LineAlignment.View;
            bolt.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0.7f));
            bolt.startColor = bolt.endColor = new Color(0.9f, 0.93f, 1f, 1f);
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.receiveShadows = false;
            bolt.positionCount = 0;
            bolt.enabled = false;

            go.AddComponent<WeatherSystem>().Configure(WeatherAsset(), flash, bolt);
            // Gota = bolinha suave esticada pela queda; anel e pinguinho com os mesmos materiais dos efeitos da água (GameVfx).
            go.AddComponent<RainEffect>().Configure(FxMaterial("M_FX_Chuva", "circle_05", additive: false),
                FxMaterial("M_FX_Anel", "circle_02", additive: false), FxMaterial("M_FX_Gotas", "circle_05", additive: false));
        }

        /// <summary>
        /// Loop de vento feito aqui (sem gravação de terceiros): ruído com passa-baixa (o "sopro") + um passa-faixa ressonante que sobe e
        /// desce devagar (o assobio), com o volume ondulando. Todas as ondulações têm período que divide os 12 s do loop e o fim é
        /// misturado com o começo: dá a volta sem emenda. Refeito quando a versão (userData do importador) muda.
        /// </summary>
        static AudioClip WindLoopClip() => GeneratedClip(WindLoopPath, WindLoopVersion, 0.125f, () =>
        {
                const int rate = AudioRate, seconds = 12;
                int n = rate * seconds, fade = rate;
                var raw = new float[n + fade];
                var rng = new System.Random(1717);
                float lowpass = 0f, lowpass2 = 0f, low = 0f, band = 0f;
                float lpA = Mathf.Exp(-2f * Mathf.PI * 380f / rate);
                const float q = 0.22f;   // 1/Q do assobio
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    // Duas etapas de passa-baixa (12 dB por oitava): sopro grave, sem o chiado agudo de uma etapa só.
                    lowpass = lpA * lowpass + (1f - lpA) * white;
                    lowpass2 = lpA * lowpass2 + (1f - lpA) * lowpass;
                    float center = 430f + 170f * Mathf.Sin(2f * Mathf.PI * t / 6f) + 90f * Mathf.Sin(2f * Mathf.PI * t / 2.4f + 1.3f)
                                   + 55f * Mathf.Sin(2f * Mathf.PI * t / 4f + 0.4f);
                    float f = 2f * Mathf.Sin(Mathf.PI * center / rate);
                    low += f * band;
                    float high = white - low - q * band;
                    band += f * high;
                    float whistle = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 6f + 2f);
                    float swell = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * t / 4f + 0.7f);
                    raw[i] = (lowpass2 * 6f + band * 0.18f * whistle) * swell;
                }
                return SeamlessLoop(raw, n, fade);
        });

        /// <summary>Fim misturado com o começo (potência constante): o loop fecha sem clique. <paramref name="raw"/> tem n + fade amostras.</summary>
        static float[] SeamlessLoop(float[] raw, int n, int fade)
        {
            var samples = new float[n];
            for (int i = 0; i < n; i++) samples[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                samples[i] = raw[i] * Mathf.Sqrt(k) + raw[n + i] * Mathf.Sqrt(1f - k);
            }
            return samples;
        }

        /// <summary>
        /// Som feito aqui (sem gravação de terceiros) salvo como WAV em _Project/Audio: só refaz quando a versão (userData do importador)
        /// muda, no mesmo arquivo (mesmo GUID: os SoundCue continuam apontando). Volume: RMS em <paramref name="targetRms"/> (0 = só pelo
        /// pico), pico no máximo 0,95. Loop em PCM (o Vorbis pode deixar um tiquinho de silêncio na volta); avulso em ADPCM.
        /// </summary>
        static AudioClip GeneratedClip(string path, string version, float targetRms, Func<float[]> make, bool loop = true)
        {
            var existing = AssetImporter.GetAtPath(path);
            if (!File.Exists(path) || !existing || existing.userData != version)
            {
                EnsureFolder("Assets/_Project", "Audio");
                var samples = make();
                double sum = 0;
                float peak = 0f;
                foreach (float s in samples)
                {
                    sum += s * s;
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }
                float rms = (float)Math.Sqrt(sum / samples.Length);
                float gain = 0.95f / Mathf.Max(1e-6f, peak);
                if (targetRms > 0f) gain = Mathf.Min(targetRms / Mathf.Max(1e-6f, rms), gain);
                WriteWav(path, samples, gain, AudioRate);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is AudioImporter importer)
                {
                    var settings = importer.defaultSampleSettings;
                    settings.compressionFormat = loop ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.userData = version;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static void WriteWav(string path, float[] samples, float gain, int rate)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // mono
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s * gain, -1f, 1f) * 32767f));
        }
    }
}
