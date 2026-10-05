using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.LandCombat;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Música de fundo pelo momento do jogo, trocando com fade cruzado: <b>porto</b> (dentro da zona do porto, no navio
    /// ou a pé), <b>mar</b> (navegando fora dele), <b>ilha</b> (a pé fora do porto) e <b>combate</b> (pirata de terra te
    /// perseguindo por perto ou navio inimigo vindo para cima de você), que segura alguns segundos depois do último sinal.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public enum Mood { Port, Sea, Island, Combat }

        [SerializeField] SoundCue port, sea, island, combat;
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Navio do jogador (cenas sem personagem a pé).")]
        [SerializeField] Transform playerShip;
        [SerializeField] float fadeSeconds = 2.5f;
        [Tooltip("Segundos de música de combate depois do último inimigo engajado.")]
        [SerializeField] float combatHold = 8f;
        [SerializeField] float footCombatRange = 60f, shipCombatRange = 220f;

        readonly AudioSource[] decks = new AudioSource[2];
        readonly float[] deckVolume = new float[2];
        // Volume do som (cue) que cada faixa está tocando.
        readonly float[] deckCueVolume = new float[2];
        int active = -1;
        SoundCue playing;
        float combatUntil = float.NegativeInfinity;

        public Mood Current { get; private set; } = Mood.Sea;

        public void Configure(SoundCue portMusic, SoundCue seaMusic, SoundCue islandMusic, SoundCue combatMusic,
            PlayerModeSwitcher modes, Transform ship)
        {
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
            Current = Time.time < combatUntil ? Mood.Combat : InPort(me) ? Mood.Port : onFoot ? Mood.Island : Mood.Sea;
            var want = Current switch { Mood.Combat => combat, Mood.Port => port, Mood.Island => island, _ => sea };
            if (want != playing) Switch(want);

            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            for (int i = 0; i < 2; i++)
            {
                deckVolume[i] = Mathf.MoveTowards(deckVolume[i], i == active ? 1f : 0f, step);
                decks[i].volume = deckVolume[i] * deckCueVolume[i] * SoundMix.Volume(SoundChannel.Music);
                if (i != active && deckVolume[i] <= 0f && decks[i].isPlaying) decks[i].Stop();
            }
        }

        void Switch(SoundCue cue)
        {
            playing = cue;
            if (!cue || !cue.HasClips)
            {
                active = -1;
                return;
            }
            active = active == 0 ? 1 : 0;
            var deck = decks[active];
            deck.clip = cue.Pick();
            deck.pitch = 1f;
            deckCueVolume[active] = cue.volume.y;
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
