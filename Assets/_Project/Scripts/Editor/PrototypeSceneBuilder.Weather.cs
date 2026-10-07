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
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string GeneratedAudioFolder = "Assets/_Project/Audio";
        const string WindLoopPath = GeneratedAudioFolder + "/Vento_Gerado.wav";
        /// <summary>Muda a receita do som? Suba a versão: o arquivo é refeito no mesmo lugar (mesmo GUID, o Som_Vento continua apontando).</summary>
        const string WindLoopVersion = "vento-v2";

        static WindSettings WindAsset() => LoadOrCreateAsset<WindSettings>(DataFolder + "/Vento.asset");

        static void CreateWind()
        {
            var go = new GameObject("Vento");
            go.AddComponent<WindSystem>().Configure(WindAsset());
            EnsureFolder(MaterialsFolder, "VFX");
            go.AddComponent<WindStreaks>().Configure(TrailMaterial());
        }

        /// <summary>
        /// Loop de vento feito aqui (sem gravação de terceiros): ruído com passa-baixa (o "sopro") + um passa-faixa ressonante que sobe e
        /// desce devagar (o assobio), com o volume ondulando. Todas as ondulações têm período que divide os 12 s do loop e o fim é
        /// misturado com o começo: dá a volta sem emenda. Refeito quando a versão (userData do importador) muda.
        /// </summary>
        static AudioClip WindLoopClip()
        {
            var existing = AssetImporter.GetAtPath(WindLoopPath);
            if (!File.Exists(WindLoopPath) || !existing || existing.userData != WindLoopVersion)
            {
                EnsureFolder("Assets/_Project", "Audio");
                const int rate = 44100, seconds = 12;
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
                // Fim misturado com o começo (potência constante): o loop fecha sem clique.
                var samples = new float[n];
                for (int i = 0; i < n; i++) samples[i] = raw[i];
                for (int i = 0; i < fade; i++)
                {
                    float k = (float)i / fade;
                    samples[i] = raw[i] * Mathf.Sqrt(k) + raw[n + i] * Mathf.Sqrt(1f - k);
                }
                // Volume: RMS ~ −18 dBFS, pico no máximo 0,95.
                double sum = 0;
                float peak = 0f;
                foreach (float s in samples)
                {
                    sum += s * s;
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }
                float rms = (float)Math.Sqrt(sum / n);
                float gain = Mathf.Min(0.125f / Mathf.Max(1e-6f, rms), 0.95f / Mathf.Max(1e-6f, peak));
                WriteWav(WindLoopPath, samples, gain, rate);
                AssetDatabase.ImportAsset(WindLoopPath);
                if (AssetImporter.GetAtPath(WindLoopPath) is AudioImporter importer)
                {
                    // PCM: o Vorbis pode deixar um tiquinho de silêncio na volta do loop.
                    var settings = importer.defaultSampleSettings;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.userData = WindLoopVersion;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(WindLoopPath);
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
