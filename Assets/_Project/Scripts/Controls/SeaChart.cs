using System;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Carta náutica (mapa no M, 12/11): a imagem do mar feita pelo gerador (as costas de todas as ilhas, como uma carta de navegação) e
    /// os lugares com nome. A ilha só ganha nome na carta depois que o navio (ou o capitão) chega perto dela; os covis dos monstros só
    /// aparecem depois de achados. O save guarda o que já foi descoberto (<see cref="ISeaChart"/>).
    /// </summary>
    public class SeaChart : MonoBehaviour, ISeaChart
    {
        public enum PlaceKind { Island, Port, Lair }

        [Serializable]
        public class Place
        {
            public string id;
            public string name;
            public PlaceKind kind;
            public Vector3 position;
            [Tooltip("Chegar a esta distância (m) descobre o lugar.")]
            public float discoverRadius = 260f;
            [Tooltip("Conhecido desde o começo do jogo (o porto).")]
            public bool known;
        }

        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] Texture2D chart;
        [Tooltip("Área do mundo que a imagem cobre: x, z do canto de baixo à esquerda e o lado (m).")]
        [SerializeField] Vector3 chartArea = new(-1350f, -1250f, 2700f);
        [SerializeField] Place[] places = Array.Empty<Place>();
        [SerializeField] float checkSeconds = 0.5f;

        readonly HashSet<string> discovered = new();
        float nextCheck;

        public static SeaChart Current { get; private set; }

        public Texture2D Chart => chart;
        public IReadOnlyList<Place> Places => places;
        /// <summary>Muda quando algo é descoberto (o HUD remonta).</summary>
        public int Version { get; private set; }
        /// <summary>Um lugar novo na carta (faixa).</summary>
        public event Action<Place> Found;

        public void Configure(PlayerModeSwitcher modeSwitcher, Texture2D image, Vector3 area, Place[] list)
        {
            switcher = modeSwitcher;
            chart = image;
            chartArea = area;
            places = list;
        }

        /// <summary>Ponto do mundo → posição 0–1 na imagem (x para a direita, y para cima).</summary>
        public Vector2 ToChart(Vector3 world) => new((world.x - chartArea.x) / chartArea.z, (world.z - chartArea.y) / chartArea.z);

        public bool IsKnown(Place p) => p != null && (p.known || discovered.Contains(p.id));

        void Awake() => Current = this;
        void OnEnable() => Services.Register<ISeaChart>(this);
        void OnDisable() => Services.Unregister<ISeaChart>(this);

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (!switcher || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            Transform me = switcher.IsOnFoot ? (switcher.Character ? switcher.Character.transform : null) : (switcher.Ship ? switcher.Ship.transform : null);
            if (!me) return;
            var pos = me.position;
            for (int i = 0; i < places.Length; i++)
            {
                var p = places[i];
                if (p == null || IsKnown(p)) continue;
                float dx = p.position.x - pos.x, dz = p.position.z - pos.z;
                if (dx * dx + dz * dz > p.discoverRadius * p.discoverRadius) continue;
                Discover(p);
            }
        }

        /// <summary>Descobre agora (testes e sandbox).</summary>
        public void Discover(Place p)
        {
            if (p == null || !discovered.Add(p.id)) return;
            Version++;
            Found?.Invoke(p);
        }

        public void DiscoverAll()
        {
            foreach (var p in places) Discover(p);
        }

        // ---------- Save ----------

        public string[] Discovered
        {
            get
            {
                var list = new string[discovered.Count];
                discovered.CopyTo(list);
                return list;
            }
        }

        public void Restore(string[] ids)
        {
            discovered.Clear();
            if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) discovered.Add(id);
            Version++;
        }
    }
}
