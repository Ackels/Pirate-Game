using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Toca sons rápidos (um disparo, um passo, um golpe) com um conjunto fixo de AudioSources reaproveitadas, num lugar
    /// do mundo (3D) ou na cabeça do jogador (2D). Cheio, rouba a fonte que está tocando há mais tempo. Fica registrado
    /// em Services; quem não acha (cena sem áudio) simplesmente não toca nada.
    /// </summary>
    public class SoundPlayer : MonoBehaviour
    {
        [SerializeField, Min(4)] int voices = 32;

        readonly List<AudioSource> pool = new();
        readonly List<float> startedAt = new();

        void Awake()
        {
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject($"Voz_{i}");
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pool.Add(source);
                startedAt.Add(float.NegativeInfinity);
            }
        }

        void OnEnable() => Services.Register(this);
        void OnDisable() => Services.Unregister(this);

        /// <summary>Atalho para quem só tem o cue: toca pelo SoundPlayer da cena, se houver.</summary>
        public static AudioSource At(SoundCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f) =>
            Services.TryGet(out SoundPlayer player) ? player.Play(cue, position, volumeScale, pitchScale) : null;

        /// <summary>Som 2D (interface, avisos): igual em qualquer lugar.</summary>
        public static AudioSource Ui(SoundCue cue, float volumeScale = 1f) =>
            Services.TryGet(out SoundPlayer player) ? player.Play(cue, player.transform.position, volumeScale, 1f, force2D: true) : null;

        public AudioSource Play(SoundCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f, bool force2D = false)
        {
            if (!cue || !cue.HasClips || !cue.TryConsume()) return null;
            var clip = cue.Pick();
            if (!clip) return null;
            int slot = FreeSlot();
            var source = pool[slot];
            source.transform.position = position;
            cue.ApplySpace(source);
            if (force2D) source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = Mathf.Clamp01(cue.PickVolume() * volumeScale * SoundMix.Volume(cue.channel));
            source.pitch = cue.PickPitch() * pitchScale;
            source.loop = false;
            source.Play();
            startedAt[slot] = Time.unscaledTime;
            return source;
        }

        int FreeSlot()
        {
            int oldest = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].isPlaying) return i;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
