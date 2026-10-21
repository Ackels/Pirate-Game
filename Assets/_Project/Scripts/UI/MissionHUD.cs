using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// HUD das missões (UI Toolkit): marcador (estrela, nome e distância) sobre o destino de cada uma (ou sobre o porto, quando é
    /// hora de entregar) — fora da tela, preso na borda com uma seta para ele — e anúncios (aceita, objetivo cumprido, paga). A lista
    /// e o texto das missões ficam na aba Missões da tela do capitão (tecla J, <see cref="MissionJournal"/>; 21/10, pedido do
    /// Jhonata: o cartão à direita ficava o tempo todo na tela e se embolava com o resto). Com o porto aberto, os marcadores somem.
    /// </summary>
    public class MissionHUD : MonoBehaviour
    {
        /// <summary>Marcador do porto: aparece quando uma missão está pronta para entregar.</summary>
        public const string HarborMarkerId = "porto";

        [SerializeField] MissionBoard board;
        [SerializeField] PlayerModeSwitcher switcher;

        HarborVisitor visitor;
        HarborBuildingVisitor buildingVisitor;

        readonly HashSet<WorldMarker> drawn = new();

        GameHud hud;
        MissionJournal journal;
        readonly List<WorldPointer> markers = new();
        int markersUsed;

        public void Bind(MissionBoard missionBoard, PlayerModeSwitcher modeSwitcher)
        {
            board = missionBoard;
            switcher = modeSwitcher;
        }

        void OnEnable()
        {
            if (!board) return;
            board.Accepted += OnAccepted;
            board.ObjectiveCompleted += OnObjectiveCompleted;
            board.TurnedIn += OnTurnedIn;
        }

        void OnDisable()
        {
            if (!board) return;
            board.Accepted -= OnAccepted;
            board.ObjectiveCompleted -= OnObjectiveCompleted;
            board.TurnedIn -= OnTurnedIn;
            journal?.SetOpen(false);
            HideMarkers(0);
        }

        void OnDestroy() => journal?.Unregister();

        /// <summary>Aba Missões aberta (testes pela CLI).</summary>
        public bool JournalOpen => journal != null && journal.IsOpen;

        /// <summary>J: abre, fecha ou troca para a aba Missões (testes pela CLI).</summary>
        public void ToggleJournal() => CaptainBook.Toggle(CaptainBook.Tab.Missions);

        void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        void OnAccepted(MissionDefinition mission) => Feed($"Missão aceita: <b>{mission.title}</b>\n<size=75%>J abre o diário</size>", HudColors.Cream);

        void OnObjectiveCompleted(MissionDefinition mission)
        {
            string where = mission.givenByNpc ? $"volte a {mission.giver}, na vila do porto" : "volte à taverna do porto para receber";
            Feed($"Objetivo cumprido: <b>{mission.title}</b>\n<size=75%>{where}</size>", new Color(0.6f, 0.95f, 0.55f));
        }

        void OnTurnedIn(MissionDefinition mission, IReadOnlyList<ItemStack> paid)
        {
            var parts = new List<string>();
            foreach (var s in paid) parts.Add($"+{s.amount} {s.item.displayName}");
            Feed($"Missão entregue: <b>{mission.title}</b>\n<size=75%>{string.Join(", ", parts)} no armazém</size>", HudColors.Gold);
        }

        bool HarborOpen =>
            ((visitor || (switcher && switcher.Ship && switcher.Ship.TryGetComponent(out visitor))) && visitor.IsOpen)
            || ((buildingVisitor || (switcher && switcher.Character && switcher.Character.TryGetComponent(out buildingVisitor)))
                && buildingVisitor.IsOpen);

        Vector3 PlayerPosition => switcher && switcher.IsOnFoot ? switcher.Character.transform.position
            : switcher && switcher.Ship ? switcher.Ship.transform.position : Vector3.zero;

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            journal = new MissionJournal(hud, board);
            return true;
        }

        void Update()
        {
            if (!board) return;
            if (journal == null && !Build()) return;
            journal.Tick();
            bool show = board.Active.Count > 0 && !HarborOpen;
            if (!show)
            {
                HideMarkers(0);
                return;
            }
            UpdateMarkers();
        }

        /// <summary>Nome e distância sobre o destino de cada missão ativa (pronta: sobre o porto).</summary>
        void UpdateMarkers()
        {
            var cam = Camera.main;
            markersUsed = 0;
            if (!cam || hud.World.panel == null)
            {
                HideMarkers(0);
                return;
            }
            drawn.Clear();
            for (int i = 0; i < board.Active.Count; i++)
            {
                var active = board.Active[i];
                // Pronta: aponta para o porto (quadro) ou para o NPC que deu a missão (o WorldMarker dele, pelo nome).
                var def = active.definition;
                WorldMarker marker;
                if (!board.IsComplete(active)) marker = WorldMarker.Find(def.targetId);
                else
                {
                    var giver = def.givenByNpc ? MissionGiver.Find(def.giver) : null;
                    marker = giver ? giver.Marker : null;
                    if (!marker) marker = WorldMarker.Find(HarborMarkerId);
                }
                if (!marker || !drawn.Add(marker)) continue;
                Vector3 to = marker.transform.position - PlayerPosition;
                to.y = 0f;
                NextMarker().Place(hud, cam, marker.LabelPosition, marker.Label, Mathf.RoundToInt(to.magnitude));
            }
            HideMarkers(markersUsed);
        }

        void HideMarkers(int from)
        {
            for (int i = from; i < markers.Count; i++) markers[i].Hide();
        }

        WorldPointer NextMarker()
        {
            if (markersUsed == markers.Count) markers.Add(new WorldPointer(hud));
            return markers[markersUsed++];
        }
    }
}
