using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>Canal de volume de cada som (o volume final é cue × canal × geral).</summary>
    public enum SoundChannel { Music, Effects, Ambient, Interface }

    /// <summary>
    /// Um som do jogo (Data/Audio): variações sorteadas a cada vez, faixa de volume e de tom, canal e o alcance no espaço
    /// (2D = igual em qualquer lugar; 3D = de onde veio, some com a distância). Ajuste de game design: mexa aqui, não no
    /// código. Nunca altere em Play (fica salvo).
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Som", fileName = "Som_")]
    public class SoundCue : ScriptableObject
    {
        public AudioClip[] clips = System.Array.Empty<AudioClip>();
        [Tooltip("Volume de cada clipe (mesma ordem; vazio ou faltando = 1). Equilibra faixas de volumes diferentes no mesmo som (músicas).")]
        public float[] clipVolumes = System.Array.Empty<float>();
        public SoundChannel channel = SoundChannel.Effects;
        [Tooltip("Volume sorteado entre x e y (0–1).")]
        public Vector2 volume = new(0.8f, 1f);
        [Tooltip("Tom (velocidade) sorteado entre x e y: varia o som repetido.")]
        public Vector2 pitch = new(0.95f, 1.05f);
        [Tooltip("Desligado: som 2D (interface, música).")]
        public bool spatial = true;
        [Tooltip("Distância (m) até onde o som fica no volume cheio e onde ele some.")]
        public float minDistance = 4f, maxDistance = 40f;
        [Tooltip("Segundos mínimos entre dois toques deste som (evita metralhar).")]
        public float cooldown;

        [System.NonSerialized] float lastPlayed = float.NegativeInfinity;
        [System.NonSerialized] int lastClip = -1;

        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>Sorteia uma variação (sem repetir a anterior quando há mais de uma).</summary>
        public AudioClip Pick()
        {
            int i = PickIndex();
            return i < 0 ? null : clips[i];
        }

        /// <summary>Como <see cref="Pick"/>, devolvendo o índice (−1 sem clipes), para o volume do clipe.</summary>
        public int PickIndex()
        {
            if (!HasClips) return -1;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == lastClip) i = (i + 1) % clips.Length;
            lastClip = i;
            return i;
        }

        public float ClipVolume(int index) =>
            clipVolumes != null && index >= 0 && index < clipVolumes.Length ? clipVolumes[index] : 1f;

        public float PickVolume() => Random.Range(volume.x, volume.y);
        public float PickPitch() => Random.Range(pitch.x, pitch.y);

        /// <summary>Pode tocar agora (respeita o cooldown) e marca o toque.</summary>
        public bool TryConsume()
        {
            if (cooldown > 0f && Time.unscaledTime - lastPlayed < cooldown) return false;
            lastPlayed = Time.unscaledTime;
            return true;
        }

        /// <summary>
        /// Prioridade da voz (0 = mais importante): o projeto tem 32 vozes reais, as outras ficam mudas. Música nunca
        /// some; interface e efeitos vêm antes do ambiente.
        /// </summary>
        public static int Priority(SoundChannel channel) => channel switch
        {
            SoundChannel.Music => 0, SoundChannel.Interface => 40, SoundChannel.Effects => 128, _ => 200,
        };

        /// <summary>Aplica alcance, 2D/3D e prioridade numa fonte.</summary>
        public void ApplySpace(AudioSource source)
        {
            source.priority = Priority(channel);
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
            source.dopplerLevel = 0f;
        }
    }
}
