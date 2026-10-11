using Pirates.Core;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Volumes em uso agora (geral e por canal), em memória: o sandbox mexe aqui sem tocar no asset. Quem toca som
    /// multiplica pelo canal (<see cref="Volume"/>); os loops releem a cada quadro.
    /// </summary>
    public static class SoundMix
    {
        public static float Master = 1f, Music = 0.45f, Effects = 1f, Ambient = 0.8f, Interface = 0.7f;

        public static void Apply(SoundSettings s)
        {
            if (!s) return;
            Master = s.master;
            Music = s.music;
            Effects = s.effects;
            Ambient = s.ambient;
            Interface = s.interfaceVolume;
        }

        /// <summary>
        /// Volumes do menu Opções (<see cref="GameOptions"/>). Sem nada mexido ainda, as opções passam a mostrar os volumes
        /// do SoundSettings (aplicados antes) e nada muda.
        /// </summary>
        public static void Apply(GameOptions o)
        {
            if (o == null) return;
            if (!o.hasAudio)
            {
                o.master = Master;
                o.music = Music;
                o.effects = Effects;
                o.ambient = Ambient;
                o.interfaceVolume = Interface;
                return;
            }
            Master = o.master;
            Music = o.music;
            Effects = o.effects;
            Ambient = o.ambient;
            Interface = o.interfaceVolume;
        }

        public static float Volume(SoundChannel channel) => Master * channel switch
        {
            SoundChannel.Music => Music,
            SoundChannel.Ambient => Ambient,
            SoundChannel.Interface => Interface,
            _ => Effects,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Master = 1f; Music = 0.45f; Effects = 1f; Ambient = 0.8f; Interface = 0.7f;
        }
    }
}
