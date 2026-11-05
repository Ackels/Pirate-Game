using System;
using System.Collections.Generic;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.Controls
{
    /// <summary>
    /// Palco da abordagem (04/11, passo 1): os dois navios lado a lado em tamanho grande (o modelo do kit × 3, com chão, rampas
    /// nas escadas e amuradas de colisão feitos pelo gerador), longe do mapa, ligados por uma prancha. Um convés por classe do
    /// navio do jogador (o da esquerda) e um por tipo de navio inimigo (o da direita); só os dois da abordagem ficam ligados.
    /// O NavMesh é feito na hora (com a tela preta), só com o que está ligado.
    /// </summary>
    public class BoardingStage : MonoBehaviour
    {
        [Serializable]
        public class Deck
        {
            [Tooltip("Navio do jogador: nome do asset da classe (ShipClass_Chalupa...); inimigo: o tipo (chalupa...).")]
            public string id;
            public GameObject root;
            [Tooltip("Onde o capitão aparece (no navio do jogador) e onde ele volta (F perto daqui).")]
            public Transform arrive;
            [Tooltip("Navio inimigo (passo 2): a tripulação no convés, num EnemyCamp; liga depois do NavMesh.")]
            public EnemyCamp crew;
        }

        [SerializeField] Deck[] playerDecks = Array.Empty<Deck>();
        [SerializeField] Deck[] enemyDecks = Array.Empty<Deck>();
        [SerializeField] GameObject plank;
        [SerializeField] LandingPoint landing;
        [Tooltip("Volume do NavMesh feito na hora (espaço do palco).")]
        [SerializeField] Vector3 navCenter = new(0f, 4f, 0f), navSize = new(60f, 20f, 70f);

        NavMeshDataInstance navInstance;
        NavMeshData navData;
        readonly List<NavMeshBuildSource> sources = new();
        readonly List<NavMeshBuildMarkup> markups = new();

        readonly List<EnemyFootAI> onDeck = new();
        bool defeatAnnounced;

        public bool Showing { get; private set; }
        /// <summary>Piratas no convés nesta abordagem (a metralha tira alguns) e quantos ainda estão de pé.</summary>
        public int CrewTotal => onDeck.Count;
        public int CrewAlive
        {
            get
            {
                int n = 0;
                foreach (var m in onDeck) if (m && !m.IsDead) n++;
                return n;
            }
        }
        public bool CrewDefeated => Showing && onDeck.Count > 0 && CrewAlive == 0;
        public EnemyCamp Crew => EnemyDeck?.crew;
        /// <summary>A tripulação inimiga caiu toda (uma vez por abordagem).</summary>
        public event Action Defeated;
        public Deck PlayerDeck { get; private set; }
        public Deck EnemyDeck { get; private set; }
        public LandingPoint Landing => landing;

        public void Configure(Deck[] player, Deck[] enemy, GameObject bridge, LandingPoint point)
        {
            playerDecks = player;
            enemyDecks = enemy;
            plank = bridge;
            landing = point;
        }

        public bool Has(string enemyKind) => Find(enemyDecks, enemyKind, false) != null;

        void Awake() => HideAll();

        void OnDisable() => Hide();

        /// <summary>Liga o convés do jogador (pela classe; sem ele, o primeiro) e o do inimigo e devolve onde o capitão desce.</summary>
        public LandingPoint Show(string playerClass, string enemyKind, int crewCount = int.MaxValue)
        {
            HideAll();
            PlayerDeck = Find(playerDecks, playerClass, true);
            EnemyDeck = Find(enemyDecks, enemyKind, true);
            if (PlayerDeck == null || EnemyDeck == null) return null;
            PlayerDeck.root.SetActive(true);
            EnemyDeck.root.SetActive(true);
            if (plank) plank.SetActive(true);
            // Alcance de embarque negativo: o "Voltar ao navio" comum (BoatBoarding) nunca vale aqui, só o da abordagem (com a tela escura
            // e soltando os navios). Com 0, parado exatamente na chegada os dois apareciam.
            landing.Configure("Abordagem", PlayerDeck.arrive, 0f, -1f);
            Physics.SyncTransforms();
            BuildNavMesh();
            ShowCrew(crewCount);
            Showing = true;
            return landing;
        }

        /// <summary>Os piratas no posto, com a vida cheia; só os primeiros <paramref name="count"/> ficam no convés.</summary>
        void ShowCrew(int count)
        {
            onDeck.Clear();
            defeatAnnounced = false;
            var camp = EnemyDeck?.crew;
            if (!camp) return;
            // Liga depois do NavMesh (os agentes nascem nele).
            camp.gameObject.SetActive(true);
            var members = camp.Members;
            for (int i = 0; i < members.Count; i++)
                if (members[i]) members[i].gameObject.SetActive(true);
            camp.ResetAll();
            for (int i = 0; i < members.Count; i++)
            {
                if (!members[i]) continue;
                bool keep = i < Mathf.Max(1, count);
                members[i].gameObject.SetActive(keep);
                if (keep) onDeck.Add(members[i]);
            }
        }

        /// <summary>A tripulação vem toda para cima do capitão (a abordagem é um ataque).</summary>
        public void RouseCrew()
        {
            var camp = EnemyDeck?.crew;
            if (camp && onDeck.Count > 0) camp.Provoke(onDeck[0]);
            foreach (var m in onDeck) if (m) m.HearAlarm();
        }

        void Update()
        {
            if (defeatAnnounced || !CrewDefeated) return;
            defeatAnnounced = true;
            Defeated?.Invoke();
        }

        public void Hide()
        {
            if (navInstance.valid) navInstance.Remove();
            navInstance = default;
            if (!Showing) return;
            Showing = false;
            HideAll();
        }

        /// <summary>Distância no chão até o ponto de volta (no navio do jogador).</summary>
        public float ReturnDistance(Vector3 position)
        {
            if (PlayerDeck == null || !PlayerDeck.arrive) return float.PositiveInfinity;
            Vector3 d = position - PlayerDeck.arrive.position;
            d.y = 0f;
            return d.magnitude;
        }

        void HideAll()
        {
            foreach (var d in playerDecks) if (d.root) d.root.SetActive(false);
            foreach (var d in enemyDecks)
            {
                if (d.crew) d.crew.gameObject.SetActive(false);
                if (d.root) d.root.SetActive(false);
            }
            if (plank) plank.SetActive(false);
        }

        static Deck Find(Deck[] decks, string id, bool fallback)
        {
            foreach (var d in decks)
                if (d.root && d.id == id) return d;
            return fallback && decks.Length > 0 && decks[0].root ? decks[0] : null;
        }

        void BuildNavMesh()
        {
            sources.Clear();
            NavMeshBuilder.CollectSources(transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var settings = NavMesh.GetSettingsByIndex(0);
            // Os limites ficam no espaço do NavMeshData (posição e giro do palco); as fontes, no mundo.
            if (!navData) navData = new NavMeshData(settings.agentTypeID);
            navData.position = transform.position;
            navData.rotation = transform.rotation;
            if (!NavMeshBuilder.UpdateNavMeshData(navData, settings, sources, new Bounds(navCenter, navSize))) return;
            navInstance = NavMesh.AddNavMeshData(navData);
        }
    }
}
