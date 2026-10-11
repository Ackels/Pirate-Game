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
    /// HUD das missões (UI Toolkit): cartão de pergaminho à direita com as ativas e o progresso, marcador (estrela, nome e
    /// distância) sobre o destino de cada uma (ou sobre o porto, quando é hora de entregar) — fora da tela, preso na borda
    /// com uma seta para ele — e anúncios (aceita, objetivo cumprido, paga). Com o porto aberto (a janela no navio ou uma
    /// casa do porto a pé), cartão e marcadores somem (cobririam a janela; a taverna mostra o mesmo).
    /// </summary>
    public class MissionHUD : MonoBehaviour
    {
        /// <summary>Marcador do porto: aparece quando uma missão está pronta para entregar.</summary>
        public const string HarborMarkerId = "porto";

        [SerializeField] MissionBoard board;
        [SerializeField] PlayerModeSwitcher switcher;

        HarborVisitor visitor;
        HarborBuildingVisitor buildingVisitor;

        // Linhas do cartão: o progresso depende de carga e de contadores sem evento, então refaz 4× por segundo
        // (ou na hora, se a lista de missões mudou).
        const float PanelRefresh = 0.25f;
        readonly List<MissionBoard.ActiveMission> panelFor = new();
        float panelBuiltAt = float.NegativeInfinity;
        readonly HashSet<WorldMarker> drawn = new();

        GameHud hud;
        VisualElement card;
        readonly List<(VisualElement row, VisualElement icon, Label text)> rows = new();
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
            if (card != null)
            {
                Hud.Show(card, false);
                HideMarkers(0);
            }
        }

        void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        void OnAccepted(MissionDefinition mission) => Feed($"Missão aceita: <b>{mission.title}</b>", HudColors.Cream);

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
            card = Hud.Card(paper: true, "Missões");
            card.AddToClassList("hud-missions");
            card.style.display = DisplayStyle.None;
            hud.TopRight.Add(card);
            return true;
        }

        void Update()
        {
            if (!board) return;
            if (card == null && !Build()) return;
            bool show = board.Active.Count > 0 && !HarborOpen;
            Hud.Show(card, show);
            if (!show)
            {
                HideMarkers(0);
                return;
            }
            RefreshPanel();
            UpdateMarkers();
        }

        void RefreshPanel()
        {
            if (!PanelChanged()) return;
            panelBuiltAt = Time.unscaledTime;
            panelFor.Clear();
            for (int i = 0; i < board.Active.Count; i++)
            {
                var active = board.Active[i];
                bool done = board.IsComplete(active);
                string status = !done ? board.ProgressText(active)
                    : active.definition.givenByNpc ? $"<color=#2f7a2a>pronta: fale com {active.definition.giver}</color>"
                    : "<color=#2f7a2a>pronta: entregue na taverna do porto</color>";
                if (i == rows.Count)
                {
                    var row = Hud.Row("hud-mission");
                    var icon = Hud.Box("hud-mission__icon");
                    var text = Hud.Text("", "hud-mission__text");
                    row.Add(icon);
                    row.Add(text);
                    card.Add(row);
                    rows.Add((row, icon, text));
                }
                var r = rows[i];
                Hud.Show(r.row, true);
                r.icon.EnableInClassList("hud-mission__icon--done", done);
                Hud.Set(r.text, $"<b>{active.definition.title}</b>\n{status}");
                panelFor.Add(active);
            }
            for (int i = board.Active.Count; i < rows.Count; i++) Hud.Show(rows[i].row, false);
        }

        bool PanelChanged()
        {
            if (Time.unscaledTime - panelBuiltAt >= PanelRefresh || panelFor.Count != board.Active.Count) return true;
            for (int i = 0; i < panelFor.Count; i++)
                if (panelFor[i] != board.Active[i]) return true;
            return false;
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
