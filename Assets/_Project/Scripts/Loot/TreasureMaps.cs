using System;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Loot
{
    /// <summary>
    /// Mapas do tesouro em pedaços (C1, 16/10; pacote C, exploração): o item "Pedaço de mapa" cai de baús, navios grandes e chefes.
    /// Juntando <see cref="piecesPerMap"/> em qualquer lugar do jogador (mochila, porão, armazém: <see cref="PlayerStorage"/>), o mapa
    /// se monta sozinho: os pedaços somem e um X aparece num dos lugares sorteados das ilhas (um <see cref="BuriedTreasure"/>, o mesmo
    /// dos tesouros de missão: F para cavar, o baú sobe). Um mapa por vez; cavado, o próximo começa a juntar. O save guarda o X marcado
    /// (<see cref="ITreasureMaps"/>).
    /// </summary>
    public class TreasureMaps : MonoBehaviour, ITreasureMaps
    {
        [Serializable]
        public class Spot
        {
            public BuriedTreasure treasure;
            [Tooltip("Onde fica, para o aviso e o cartão (\"Ilha Oeste, lado oeste\").")]
            public string place;
        }

        [SerializeField] ItemDefinition piece;
        [SerializeField, Min(2)] int piecesPerMap = 3;
        [SerializeField] Spot[] spots = Array.Empty<Spot>();
        [Tooltip("Segundos entre as contagens dos pedaços.")]
        [SerializeField] float checkSeconds = 0.5f;

        int active = -1, last = -1, pieces;
        float nextCheck;

        public static TreasureMaps Current { get; private set; }

        public ItemDefinition Piece => piece;
        public int PiecesPerMap => piecesPerMap;
        /// <summary>Pedaços que o jogador tem agora (em todos os lugares).</summary>
        public int Pieces => pieces;
        public bool HasMap => active >= 0;
        public Spot ActiveSpot => active >= 0 ? spots[active] : null;
        public string ActiveId => active >= 0 && spots[active].treasure ? spots[active].treasure.TreasureId : null;
        /// <summary>Muda quando os pedaços ou o mapa mudam (o HUD só remonta nisso).</summary>
        public int Version { get; private set; }

        /// <summary>O mapa se montou (o lugar do X).</summary>
        public event Action<Spot> Completed;
        /// <summary>O tesouro do mapa foi desenterrado.</summary>
        public event Action<Spot> Found;

        public void Configure(ItemDefinition mapPiece, Spot[] treasureSpots)
        {
            piece = mapPiece;
            spots = treasureSpots;
        }

        void Awake() => Current = this;

        void OnEnable()
        {
            Services.Register<ITreasureMaps>(this);
            EventBus.Subscribe<TreasureFoundEvent>(OnTreasureFound);
        }

        void OnDisable()
        {
            Services.Unregister<ITreasureMaps>(this);
            EventBus.Unsubscribe<TreasureFoundEvent>(OnTreasureFound);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (!piece || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            int n = PlayerStorage.Count(piece);
            if (n != pieces)
            {
                pieces = n;
                Version++;
            }
            if (active < 0 && pieces >= piecesPerMap) Assemble();
        }

        /// <summary>Junta os pedaços num mapa e marca o X (um lugar diferente do último).</summary>
        public bool Assemble()
        {
            if (active >= 0 || spots.Length == 0) return false;
            if (PlayerStorage.Take(piece, piecesPerMap) < piecesPerMap) return false;
            int index = Random.Range(0, spots.Length);
            if (spots.Length > 1 && index == last) index = (index + 1 + Random.Range(0, spots.Length - 1)) % spots.Length;
            Mark(index);
            pieces = PlayerStorage.Count(piece);
            Completed?.Invoke(spots[index]);
            return true;
        }

        void Mark(int index)
        {
            active = index;
            if (spots[index].treasure) spots[index].treasure.Bury();
            Version++;
        }

        void OnTreasureFound(TreasureFoundEvent e)
        {
            if (active < 0 || e.treasureId != ActiveId) return;
            var spot = spots[active];
            last = active;
            active = -1;
            Version++;
            Found?.Invoke(spot);
        }

        public void Restore(string activeId)
        {
            if (active >= 0 && spots[active].treasure) spots[active].treasure.Hide();
            active = -1;
            for (int i = 0; i < spots.Length; i++)
                if (spots[i].treasure && !string.IsNullOrEmpty(activeId) && spots[i].treasure.TreasureId == activeId) Mark(i);
            Version++;
        }

        /// <summary>Sandbox (F1): um pedaço na mão (no primeiro lugar do jogador que existir: o porão).</summary>
        public void GivePiece(Inventory where)
        {
            if (where && piece) where.Add(piece, 1);
        }

        /// <summary>Sandbox (F1): desfaz o mapa marcado.</summary>
        public void Cancel() => Restore(null);
    }
}
