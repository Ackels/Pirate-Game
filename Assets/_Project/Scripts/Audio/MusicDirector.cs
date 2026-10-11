using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.LandCombat;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Música de fundo pelo momento do jogo, trocando com fade cruzado: <b>título</b> (menu inicial e prólogo),
    /// <b>porto</b> (dentro da zona do porto, no navio ou a pé), <b>mar</b> (navegando fora dele), <b>ilha</b> (a pé fora do
    /// porto) e <b>combate</b> (pirata de terra te perseguindo por perto ou navio inimigo vindo para cima de você), que segura
    /// alguns segundos depois do último sinal. Som com várias músicas vira uma lista: no fim de uma, sorteia outra do mesmo
    /// momento (sem repetir a anterior) e emenda com o fade; com uma só, ela fica em loop.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public enum Mood { Title, Port, Sea, Island, Combat }

        [SerializeField] SoundCue title, port, sea, island, combat;
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Navio do jogador (cenas sem personagem a pé).")]
        [SerializeField] Transform playerShip;
        [SerializeField] float fadeSeconds = 2.5f;
        [Tooltip("Segundos de música de combate depois do último inimigo engajado.")]
        [SerializeField] float combatHold = 8f;
        [SerializeField] float footCombatRange = 60f, shipCombatRange = 220f;
        [Tooltip("Faixa que parou antes do fim só troca depois de tocar isto (s): editor pausado ou som sem saída deixam isPlaying falso.")]
        [SerializeField] float minTrackSeconds = 20f;

        readonly AudioSource[] decks = new AudioSource[2];
        readonly float[] deckVolume = new float[2];
        // Volume do som (cue × clipe) que cada faixa está tocando.
        readonly float[] deckCueVolume = new float[2];
        int active = -1;
        SoundCue playing;
        float switchedAt = float.NegativeInfinity;
        float combatUntil = float.NegativeInfinity;

        public Mood Current { get; private set; } = Mood.Sea;

        /// <summary>Clipe que está tocando agora (testes).</summary>
        public AudioClip NowPlaying => active >= 0 ? decks[active].clip : null;

        public void Configure(SoundCue titleMusic, SoundCue portMusic, SoundCue seaMusic, SoundCue islandMusic, SoundCue combatMusic,
            PlayerModeSwitcher modes, Transform ship)
        {
            title = titleMusic;
            port = portMusic;
            sea = seaMusic;
            island = islandMusic;
            combat = combatMusic;
            switcher = modes;
            playerShip = ship;
        }

        void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"Musica_{i}");
                go.transform.SetParent(transform, false);
                decks[i] = go.AddComponent<AudioSource>();
                decks[i].playOnAwake = false;
                decks[i].loop = true;
                decks[i].spatialBlend = 0f;
                decks[i].ignoreListenerPause = true;
                decks[i].priority = SoundCue.Priority(SoundChannel.Music);
            }
        }

        void Update()
        {
            Vector3 me = PlayerPosition();
            if (InCombat(me)) combatUntil = Time.time + combatHold;

            bool onFoot = switcher && switcher.IsOnFoot;
            Current = TitleScreen.Showing && title ? Mood.Title
                : Time.time < combatUntil ? Mood.Combat : InPort(me) ? Mood.Port : onFoot ? Mood.Island : Mood.Sea;
            var want = Current switch
            {
                Mood.Title => title, Mood.Combat => combat, Mood.Port => port, Mood.Island => island, _ => sea,
            };
            if (want != playing || Ending()) Switch(want);

            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            for (int i = 0; i < 2; i++)
            {
                deckVolume[i] = Mathf.MoveTowards(deckVolume[i], i == active ? 1f : 0f, step);
                decks[i].volume = deckVolume[i] * deckCueVolume[i] * SoundMix.Volume(SoundChannel.Music);
                if (i != active && deckVolume[i] <= 0f && decks[i].isPlaying) decks[i].Stop();
            }
        }

        /// <summary>A música da lista está nos últimos segundos (ou já parou): hora de emendar a próxima.</summary>
        bool Ending()
        {
            if (active < 0 || !playing || playing.clips.Length < 2) return false;
            var deck = decks[active];
            if (!deck.clip) return false;
            if (deck.isPlaying) return deck.time >= deck.clip.length - fadeSeconds;
            // Parou sem chegar ao fim (revisão do chat de otimização, 11/10): com o editor pausado ou sem dispositivo de áudio
            // tocando de verdade, isPlaying fica falso e a troca acontecia a cada quadro.
            return Time.unscaledTime - switchedAt >= minTrackSeconds;
        }

        void Switch(SoundCue cue)
        {
            playing = cue;
            switchedAt = Time.unscaledTime;
            if (!cue || !cue.HasClips)
            {
                active = -1;
                return;
            }
            active = active == 0 ? 1 : 0;
            var deck = decks[active];
            int index = cue.PickIndex();
            deck.clip = cue.clips[index];
            deck.loop = cue.clips.Length == 1;
            deck.pitch = 1f;
            deckCueVolume[active] = cue.volume.y * cue.ClipVolume(index);
            deckVolume[active] = 0f;
            deck.Play();
        }

        Vector3 PlayerPosition()
        {
            if (switcher)
            {
                if (switcher.IsOnFoot && switcher.Character) return switcher.Character.transform.position;
                if (switcher.Ship) return switcher.Ship.transform.position;
            }
            return playerShip ? playerShip.position : transform.position;
        }

        static bool InPort(Vector3 position)
        {
            for (int i = 0; i < HarborDock.Active.Count; i++)
                if (HarborDock.Active[i].Zone && HarborDock.Active[i].Zone.Contains(position)) return true;
            return false;
        }

        bool InCombat(Vector3 me)
        {
            float foot2 = footCombatRange * footCombatRange;
            foreach (var ai in EnemyFootAI.Active)
                if (ai && !ai.IsDead && ai.IsAlerted && (ai.transform.position - me).sqrMagnitude < foot2) return true;
            for (int i = 0; i < EnemyFort.Active.Count; i++)
                if (EnemyFort.Active[i].Engaging) return true;
            float ship2 = shipCombatRange * shipCombatRange;
            for (int i = 0; i < EnemyShipAI.Active.Count; i++)
            {
                var ai = EnemyShipAI.Active[i];
                if (!ai || ai.IsDead || ai.TargetIsSafe) continue;
                if (ai.State is not (EnemyShipAI.AIState.Chase or EnemyShipAI.AIState.Broadside)) continue;
                if ((ai.transform.position - me).sqrMagnitude < ship2) return true;
            }
            return false;
        }
    }
}
