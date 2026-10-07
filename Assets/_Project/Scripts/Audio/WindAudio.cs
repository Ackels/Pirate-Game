using Pirates.Controls;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Som do vento (M11, clima): um loop 2D no canal de ambiente que sobe de volume e de tom com a força do <see cref="IWind"/>, com rajadas
    /// (o volume sobe e desce devagar sozinho, mais com vento forte). Em terra fica mais baixo (abrigado pela ilha). Sem vento na cena, mudo.
    /// </summary>
    public class WindAudio : MonoBehaviour
    {
        [SerializeField] SoundCue sound;
        [Tooltip("Volume com calmaria e com vento forte (× o volume do som e do canal de ambiente).")]
        [SerializeField, Range(0f, 1f)] float calmVolume = 0.12f, strongVolume = 1f;
        [Tooltip("Tom com calmaria e com vento forte.")]
        [SerializeField] float calmPitch = 0.82f, strongPitch = 1.12f;
        [Tooltip("Quanto as rajadas mexem no volume com vento forte (0 = nada).")]
        [SerializeField, Range(0f, 1f)] float gustDepth = 0.45f;
        [Tooltip("Volume × isto a pé (abrigado pela ilha).")]
        [SerializeField, Range(0f, 1f)] float onFootVolume = 0.55f;

        AudioSource source;
        PlayerModeSwitcher switcher;
        float volume;

        public void Configure(SoundCue windSound) => sound = windSound;

        void Start()
        {
            switcher = FindAnyObjectByType<PlayerModeSwitcher>();
            if (!sound || !sound.HasClips) return;
            source = gameObject.AddComponent<AudioSource>();
            source.clip = sound.Pick();
            source.loop = true;
            source.playOnAwake = false;
            sound.ApplySpace(source);
            source.volume = 0f;
            // Começa num ponto qualquer do loop (não repete sempre o mesmo começo).
            source.time = Random.Range(0f, source.clip.length);
            source.Play();
        }

        void Update()
        {
            if (!source) return;
            float target = 0f, pitch = 1f;
            if (Services.TryGet(out IWind wind))
            {
                float s = wind.Strength;
                // Rajadas: ruído lento (uns 4 s por rajada), mais fundas com vento forte.
                float gust = (Mathf.PerlinNoise(Time.time * 0.23f, 3.1f) - 0.5f) * 2f * gustDepth * s;
                target = Mathf.Lerp(calmVolume, strongVolume, Mathf.Pow(s, 1.3f)) * (1f + gust);
                pitch = Mathf.Lerp(calmPitch, strongPitch, s) + gust * 0.12f;
                if (switcher && switcher.IsOnFoot) target *= onFootVolume;
                target *= sound.volume.y * SoundMix.Volume(sound.channel);
            }
            volume = Mathf.MoveTowards(volume, target, Time.unscaledDeltaTime * 0.5f);
            source.volume = volume;
            source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        }
    }
}
