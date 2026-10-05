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
        CachedText[] markerLines = new CachedText[0];

        GameHud hud;
        VisualElement card;
        readonly List<(VisualElement row, VisualElement icon, Label text)> rows = new();
        readonly List<MarkerView> markers = new();
        int markersUsed;

        class MarkerView
        {
            public VisualElement root, arrow;
            public Label text;
            public float angle = float.NaN;
        }

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

        void OnObjectiveCompleted(MissionDefinition mission) =>
            Feed($"Objetivo cumprido: <b>{mission.title}</b>\n<size=75%>volte à taverna do porto para receber</size>", new Color(0.6f, 0.95f, 0.55f));

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
                string status = done ? "<color=#2f7a2a>pronta: entregue na taverna do porto</color>" : board.ProgressText(active);
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
            if (markerLines.Length < board.Active.Count) markerLines = new CachedText[board.Active.Count];
            for (int i = 0; i < board.Active.Count; i++)
            {
                var active = board.Active[i];
                var marker = WorldMarker.Find(board.IsComplete(active) ? HarborMarkerId : active.definition.targetId);
                if (!marker || !drawn.Add(marker)) continue;
                Vector3 to = marker.transform.position - PlayerPosition;
                to.y = 0f;
                PlaceMarker(cam, marker.LabelPosition, marker.Label, Mathf.RoundToInt(to.magnitude), ref markerLines[i]);
            }
            HideMarkers(markersUsed);
        }

        void HideMarkers(int from)
        {
            for (int i = from; i < markers.Count; i++) Hud.Show(markers[i].root, false);
        }

        MarkerView NextMarker()
        {
            if (markersUsed == markers.Count)
            {
                var view = new MarkerView { root = Hud.Box("hud-marker") };
                view.root.Add(Hud.Box("hud-marker__star"));
                view.text = Hud.Text("", "hud-marker__text");
                view.root.Add(view.text);
                view.arrow = Hud.Box("hud-marker__arrow");
                view.root.Add(view.arrow);
                hud.World.Add(view.root);
                markers.Add(view);
            }
            return markers[markersUsed++];
        }

        /// <summary>Marcador sobre o destino; fora da tela (ou atrás da câmera), preso na borda com uma seta para ele.</summary>
        void PlaceMarker(Camera cam, Vector3 world, string name, int meters, ref CachedText line)
        {
            const float margin = 80f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0f;
            if (behind) sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, -sp.z);
            bool inside = !behind && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
            float angle = float.NaN;
            if (!inside)
            {
                // Empurra para a borda na direção do destino (a partir do centro da tela).
                Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 dir = new Vector2(sp.x, sp.y) - center;
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                float scale = Mathf.Min((Screen.width * 0.5f - margin) / Mathf.Max(1f, Mathf.Abs(dir.x)),
                                        (Screen.height * 0.5f - margin) / Mathf.Max(1f, Mathf.Abs(dir.y)));
                // Atrás da câmera: sempre na borda (a projeção invertida pode cair no meio da tela).
                Vector2 edge = center + dir * (behind ? scale : Mathf.Min(1f, scale));
                sp = new Vector3(edge.x, edge.y, 1f);
                // A seta do pacote aponta para cima; a tela do painel tem y para baixo (girar positivo = horário).
                angle = Mathf.Round(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
            }

            var view = NextMarker();
            if (line.Stale(name, meters)) line.Text = $"{name}  <size=80%>{meters} m</size>";
            Hud.Set(view.text, line.Text);
            Vector2 p = RuntimePanelUtils.ScreenToPanel(hud.World.panel, new Vector2(sp.x, Screen.height - sp.y));
            // Preso na borda: o texto inteiro fica na tela (a largura é a do quadro anterior).
            Vector2 half = view.root.layout.size * 0.5f;
            Vector2 area = hud.World.layout.size;
            if (!inside && half.x > 0f && area.x > 0f)
            {
                p.x = Mathf.Clamp(p.x, half.x + 10f, area.x - half.x - 10f);
                p.y = Mathf.Clamp(p.y, half.y + 34f, area.y - half.y - 10f);
            }
            view.root.style.left = p.x;
            view.root.style.top = p.y;
            Hud.Show(view.root, true);
            bool showArrow = !float.IsNaN(angle);
            Hud.Show(view.arrow, showArrow);
            if (showArrow && angle != view.angle)
            {
                view.angle = angle;
                view.arrow.style.rotate = new Rotate(angle);
            }
        }
    }
}
