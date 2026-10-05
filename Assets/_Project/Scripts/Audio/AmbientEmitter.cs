using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Som de ambiente parado num lugar: em loop (fogueira, pássaros da mata) ou em toques soltos de tempos em tempos
    /// (gaivotas, ondas na praia). Só toca com o ouvido (ListenerAnchor) dentro do alcance do som: longe, fica mudo e não
    /// gasta voz. O volume acompanha o canal a cada quadro.
    /// </summary>
    public class AmbientEmitter : MonoBehaviour
    {
        [SerializeField] SoundCue cue;
        [Tooltip("Ligado: um clipe em loop. Desligado: toques soltos a cada intervalo sorteado.")]
        [SerializeField] bool loop = true;
        [Tooltip("Segundos entre toques soltos (sorteado entre x e y).")]
        [SerializeField] Vector2 interval = new(4f, 10f);
        [Tooltip("Volume deste emissor (multiplica o do som).")]
        [SerializeField, Range(0f, 2f)] float volumeScale = 1f;

        AudioSource source;
        float baseVolume, nextShot;

        public void Configure(SoundCue sound, bool looping, Vector2 secondsBetween, float volume)
        {
            cue = sound;
            loop = looping;
            interval = secondsBetween;
            volumeScale = volume;
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (cue) cue.ApplySpace(source);
            nextShot = Time.time + Random.Range(0f, interval.y);
        }

        void Update()
        {
            if (!cue || !cue.HasClips) return;
            var ear = ListenerAnchor.Current;
            bool inRange = !cue.spatial || (ear && (ear.transform.position - transform.position).sqrMagnitude <= cue.maxDistance * cue.maxDistance);
            if (loop) UpdateLoop(inRange);
            else if (inRange && Time.time >= nextShot)
            {
                nextShot = Time.time + Random.Range(interval.x, interval.y);
                var clip = cue.Pick();
                source.pitch = cue.PickPitch();
                source.PlayOneShot(clip, Mathf.Clamp01(cue.PickVolume() * volumeScale * SoundMix.Volume(cue.channel)));
            }
        }

        void UpdateLoop(bool inRange)
        {
            if (!inRange)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            if (!source.isPlaying)
            {
                source.clip = cue.Pick();
                if (!source.clip) return;
                source.loop = true;
                source.pitch = cue.PickPitch();
                baseVolume = cue.PickVolume();
                // Começa num ponto qualquer: dois emissores iguais não ficam em fase.
                source.time = Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
            source.volume = Mathf.Clamp01(baseVolume * volumeScale * SoundMix.Volume(cue.channel));
        }
    }
}
