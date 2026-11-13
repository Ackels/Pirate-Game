using System;
using System.Collections;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// A névoa da Ilha Amaldiçoada (pacote H, passo 1, 06/11; HISTORIA.md, capítulo 6). Uma muralha de névoa em volta da ilha, a noroeste.
    /// Fechada (antes do capítulo 6, que abre depois da lula das Águas Negras): o navio que chega perto recebe o aviso e, se entra, "roda
    /// em círculos": a tela escurece e o navio volta para fora da névoa, virado para longe. Aberta: a muralha fica rala e o navio passa;
    /// o capitão que desembarca na ilha conta para a História (<see cref="PlaceReachedEvent"/>). Sem História na cena, fica aberta.
    /// </summary>
    public class CursedFog : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] string placeId = "ilha_amaldicoada";
        [Tooltip("Capítulo da História (0 em diante) que abre a névoa: o 6, A Ilha Amaldiçoada.")]
        [SerializeField] int openChapter = 5;
        [Tooltip("Capitão a pé até esta distância do centro = chegou na ilha.")]
        [SerializeField] float islandRadius = 95f;
        [Tooltip("Navio a esta distância do centro, com a névoa fechada: aviso.")]
        [SerializeField] float warnRadius = 300f;
        [Tooltip("Navio a esta distância do centro, com a névoa fechada: perdido na névoa (volta para fora).")]
        [SerializeField] float lostRadius = 215f;
        [Tooltip("Onde o navio reaparece: além do aviso, virado para fora.")]
        [SerializeField] float returnRadius = 330f;
        [SerializeField] ParticleSystem wall;
        [SerializeField] ParticleSystem mist;
        [Tooltip("Muralha aberta: fração da névoa de fechada.")]
        [SerializeField, Range(0f, 1f)] float openWall = 0.3f;
        [SerializeField] float fadeInSeconds = 0.7f, holdSeconds = 0.6f, fadeOutSeconds = 1f;

        float wallRate, mistRate;
        bool shownOpen, shownSet, reachedThisLanding;
        Coroutine lost;

        public static CursedFog Current { get; private set; }

        public string PlaceId => placeId;
        /// <summary>A rota está aberta (capítulo 6 alcançado).</summary>
        public bool IsOpen => !Services.TryGet(out IStory story) || story.Chapter >= openChapter;
        /// <summary>O navio do jogador está na névoa fechada, perto de se perder (o HUD avisa).</summary>
        public bool Warning { get; private set; }
        /// <summary>Tela preta enquanto o navio se perde (0–1).</summary>
        public float Fade { get; private set; }
        public bool IsLost => lost != null;
        public float DistanceToShip { get; private set; } = float.MaxValue;

        /// <summary>A névoa fez o navio voltar.</summary>
        public event Action Lost;
        /// <summary>O capitão pisou na ilha (uma vez por desembarque).</summary>
        public event Action Reached;

        public void Configure(PlayerModeSwitcher modeSwitcher, ParticleSystem fogWall, ParticleSystem innerMist)
        {
            switcher = modeSwitcher;
            wall = fogWall;
            mist = innerMist;
        }

        void Awake()
        {
            Current = this;
            if (wall) wallRate = wall.emission.rateOverTimeMultiplier;
            if (mist) mistRate = mist.emission.rateOverTimeMultiplier;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void OnDisable()
        {
            Warning = false;
            Fade = 0f;
            lost = null;
        }

        void Update()
        {
            bool open = IsOpen;
            if (!shownSet || open != shownOpen) ShowOpen(open);
            if (!switcher) return;
            var center = transform.position;

            if (switcher.IsOnFoot)
            {
                Warning = false;
                DistanceToShip = float.MaxValue;
                var captain = switcher.Character;
                if (!open || !captain || reachedThisLanding) return;
                if (Flat(captain.transform.position - center) > islandRadius) return;
                reachedThisLanding = true;
                EventBus.Publish(new PlaceReachedEvent { placeId = placeId });
                Reached?.Invoke();
                return;
            }

            reachedThisLanding = false;
            var ship = switcher.Ship;
            if (!ship)
            {
                Warning = false;
                return;
            }
            DistanceToShip = Flat(ship.transform.position - center);
            Warning = !open && DistanceToShip < warnRadius;
            if (!open && DistanceToShip < lostRadius && lost == null) lost = StartCoroutine(GetLost(ship));
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void ShowOpen(bool open)
        {
            shownSet = true;
            shownOpen = open;
            if (wall)
            {
                var e = wall.emission;
                e.rateOverTimeMultiplier = wallRate * (open ? openWall : 1f);
            }
            if (mist)
            {
                var e = mist.emission;
                e.rateOverTimeMultiplier = mistRate * (open ? 0.6f : 1f);
            }
        }

        /// <summary>Navio dentro da névoa fechada: escurece, põe o navio fora dela virado para longe, clareia.</summary>
        IEnumerator GetLost(ShipController ship)
        {
            for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
            {
                Fade = t / fadeInSeconds;
                yield return null;
            }
            Fade = 1f;
            if (ship)
            {
                var center = transform.position;
                var away = ship.transform.position - center;
                away.y = 0f;
                if (away.sqrMagnitude < 1f) away = Vector3.back;
                away.Normalize();
                var spot = center + away * returnRadius;
                spot.y = ship.transform.position.y;
                ship.Teleport(spot, Quaternion.LookRotation(away, Vector3.up));
                var follow = Services.TryGet(out ICameraFollow cam) ? cam : null;
                follow?.SnapToTarget();
            }
            Lost?.Invoke();
            for (float t = 0f; t < holdSeconds; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                Fade = 1f - t / fadeOutSeconds;
                yield return null;
            }
            Fade = 0f;
            lost = null;
        }

        /// <summary>Sandbox e testes: o navio se perde agora (se houver navio).</summary>
        public void LoseNow()
        {
            if (lost == null && switcher && switcher.Ship) lost = StartCoroutine(GetLost(switcher.Ship));
        }
    }
}
