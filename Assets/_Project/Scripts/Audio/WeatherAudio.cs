using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Sons do clima (M11): dois loops 2D, a chuva (volume pela chuva do <see cref="IWeather"/>, um pouco mais baixa na tempestade) e a chuva
    /// pesada da tempestade por cima (pela tempestade), e o trovão depois de cada raio (chega 0,3–1,4 s depois; mais baixo quando o raio caiu
    /// mais longe). Debaixo da terra (<see cref="CoveredArea"/>) a chuva some, o trovão chega abafado e toca o ar das catacumbas (C5).
    /// </summary>
    public class WeatherAudio : MonoBehaviour
    {
        [SerializeField] SoundCue rain, storm, thunder;
        [Tooltip("Loop de debaixo da terra (catacumbas: ar parado, ronco grave e pingos com eco).")]
        [SerializeField] SoundCue covered;
        [SerializeField, Range(0f, 1f)] float coveredVolume = 0.8f;
        [Tooltip("Trovão × isto debaixo da terra.")]
        [SerializeField, Range(0f, 1f)] float coveredThunder = 0.3f;
        [Tooltip("Volume da chuva cheia e da chuva pesada com a tempestade cheia (× o volume do som e do canal).")]
        [SerializeField, Range(0f, 1f)] float rainVolume = 0.8f, stormVolume = 0.9f;
        [Tooltip("Quanto a chuva leve baixa na tempestade cheia (a pesada cobre).")]
        [SerializeField, Range(0f, 1f)] float rainDuck = 0.35f;

        struct PendingThunder
        {
            public float at, volume;
        }

        readonly List<PendingThunder> pending = new(4);
        AudioSource rainSource, stormSource, coveredSource;
        IWeather hooked;
        float rainLevel, stormLevel, coveredLevel;

        public void Configure(SoundCue rainSound, SoundCue stormSound, SoundCue thunderSound, SoundCue coveredSound)
        {
            covered = coveredSound;
            rain = rainSound;
            storm = stormSound;
            thunder = thunderSound;
        }

        void Start()
        {
            rainSource = Loop(rain);
            stormSource = Loop(storm);
            coveredSource = Loop(covered);
            // Trovões carregados já no começo: sem isso o 1º da sessão lê o disco na hora e pode chegar atrasado.
            if (thunder && thunder.HasClips)
                for (int i = 0; i < thunder.clips.Length; i++)
                    if (thunder.clips[i]) thunder.clips[i].LoadAudioData();
        }

        AudioSource Loop(SoundCue cue)
        {
            if (!cue || !cue.HasClips) return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = cue.Pick();
            source.loop = true;
            source.playOnAwake = false;
            cue.ApplySpace(source);
            source.volume = 0f;
            source.time = Random.Range(0f, source.clip.length);
            source.Play();
            return source;
        }

        void OnDestroy()
        {
            if (hooked != null) hooked.LightningStruck -= OnStrike;
        }

        void Update()
        {
            bool has = Services.TryGet(out IWeather weather);
            if (has && hooked == null)
            {
                hooked = weather;
                hooked.LightningStruck += OnStrike;
            }
            bool under = CoveredArea.Inside;
            float wet = has && !under ? weather.Rain : 0f, heavy = has && !under ? weather.Storm : 0f;
            // Passando pelo alçapão a troca é rápida (a tela escurece ~1 s); o clima lá fora muda devagar.
            float fade = Time.unscaledDeltaTime * (under ? 2f : 0.4f);
            if (rainSource)
            {
                float target = rainVolume * wet * (1f - rainDuck * heavy) * rain.volume.y * SoundMix.Volume(rain.channel);
                rainLevel = Mathf.MoveTowards(rainLevel, target, fade);
                rainSource.volume = rainLevel;
            }
            if (stormSource)
            {
                float target = stormVolume * heavy * storm.volume.y * SoundMix.Volume(storm.channel);
                stormLevel = Mathf.MoveTowards(stormLevel, target, fade);
                stormSource.volume = stormLevel;
            }
            if (coveredSource)
            {
                float target = under ? coveredVolume * covered.volume.y * SoundMix.Volume(covered.channel) : 0f;
                coveredLevel = Mathf.MoveTowards(coveredLevel, target, Time.unscaledDeltaTime * 1.5f);
                coveredSource.volume = coveredLevel;
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                SoundPlayer.Ui(thunder, pending[i].volume * (CoveredArea.Inside ? coveredThunder : 1f));
                pending.RemoveAt(i);
            }
        }

        void OnStrike(Vector3 point, float distance)
        {
            if (!thunder) return;
            pending.Add(new PendingThunder
            {
                // O raio cai perto da tela, mas soa como um raio de longe: o trovão chega 0,3–1,4 s depois.
                at = Time.time + Random.Range(0.3f, 1.4f),
                volume = Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(15f, 60f, distance)),
            });
        }
    }
}
