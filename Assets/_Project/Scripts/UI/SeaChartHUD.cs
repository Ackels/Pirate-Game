using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Mapa no M (12/11): a carta náutica na tela do capitão (aba Mapa). A imagem das costas, o seu navio (seta na direção da proa), os
    /// nomes das ilhas já visitadas ("Ilha desconhecida" nas outras), os covis achados e os alvos de agora: contratos ativos, a História
    /// e o X do mapa do tesouro. Faixa "Lugar descoberto" ao chegar perto de um lugar novo.
    /// </summary>
    public class SeaChartHUD : MonoBehaviour
    {
        const float MapSize = 470f;
        static readonly Color Ink = new(0.27f, 0.18f, 0.1f);
        static readonly Color Gold = new(0.95f, 0.72f, 0.2f);
        static readonly Color Red = new(0.75f, 0.12f, 0.08f);

        [SerializeField] SeaChart chart;
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] float bannerSeconds = 4.5f;

        GameHud hud;
        HudWindow window;
        HudBanner banner;
        VisualElement map, overlay;
        ShipArrow arrow;
        float bannerUntil, nextMarkers;
        bool open;
        long markerSignature = -1;
        readonly List<(string label, Vector3 pos, Color color, string symbol)> targets = new();

        public void Bind(SeaChart seaChart, PlayerModeSwitcher modeSwitcher)
        {
            chart = seaChart;
            switcher = modeSwitcher;
        }

        public bool IsOpen => open;

        /// <summary>M: a aba Mapa da tela do capitão.</summary>
        public void Toggle() => CaptainBook.Toggle(CaptainBook.Tab.Map);

        void OnEnable()
        {
            if (chart) chart.Found += OnFound;
        }

        void OnDisable()
        {
            if (chart) chart.Found -= OnFound;
            SetOpen(false);
        }

        void OnDestroy() => CaptainBook.Unregister(CaptainBook.Tab.Map);

        void Update()
        {
            if (!chart) return;
            if (window == null && !Build()) return;
            var input = GameInput.Instance;
            if (input.GameplayEnabled)
            {
                if (input.Map.WasPressedThisFrame()) Toggle();
                else if (open && input.Cancel.WasPressedThisFrame()) SetOpen(false);
            }
            else if (open) SetOpen(false);
            if (open)
            {
                if (Time.unscaledTime >= nextMarkers)
                {
                    nextMarkers = Time.unscaledTime + 0.5f;
                    RefreshTargets();
                }
                window.RefreshAll();
                UpdateShip();
                hud.LowerPickups();
            }
            if (bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
        }

        void SetOpen(bool value)
        {
            if (value && window == null) return;
            if (open == value) return;
            open = value;
            ScreenPanel.Open = value;
            if (value)
            {
                CaptainBook.RefreshTabs(window);
                markerSignature = -1;
                RefreshTargets();
            }
            window?.Show(value);
            if (hud != null && hud.Prompts != null) Hud.Show(hud.Prompts, !value);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            banner = new HudBanner();
            hud.Banners.Add(banner);
            window = new HudWindow(hud.Windows, "hud-window--chart");
            window.SetTitle("Carta do Mar de Prata", "M ou Esc fecha");
            CaptainBook.AddTabs(window, CaptainBook.Tab.Map);
            CaptainBook.Register(CaptainBook.Tab.Map, () => open, SetOpen);

            var row = Hud.Row();
            row.style.alignItems = Align.FlexStart;
            window.Body.Add(row);

            map = new VisualElement { pickingMode = PickingMode.Ignore };
            map.style.width = MapSize;
            map.style.height = MapSize;
            map.style.flexShrink = 0;
            map.style.backgroundImage = new StyleBackground(chart.Chart);
            map.style.borderTopWidth = map.style.borderBottomWidth = map.style.borderLeftWidth = map.style.borderRightWidth = 3;
            var edge = new Color(0.35f, 0.24f, 0.12f);
            map.style.borderTopColor = map.style.borderBottomColor = map.style.borderLeftColor = map.style.borderRightColor = edge;
            row.Add(map);
            overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = 0;
            overlay.style.width = overlay.style.height = MapSize;
            map.Add(overlay);
            map.Add(new CompassRose { style = { position = Position.Absolute, right = 10, top = 10, width = 70, height = 70 } });
            arrow = new ShipArrow();
            map.Add(arrow);

            var side = Hud.Box("hud-grow");
            side.style.marginLeft = 18;
            row.Add(side);
            window.AddPane(overlay, () => markerSignature * 31 + chart.Version, BuildOverlay);
            window.AddPane(side, () => markerSignature * 31 + chart.Version, BuildLegend);

            window.Footer.Add(Hud.Text("A ilha ganha nome na carta quando você chega perto dela. Os covis dos monstros só aparecem depois de achados.",
                "hud-window__hint"));
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[M]</b>", () => SetOpen(false)));
            return true;
        }

        // ---------- Alvos (contratos, História, mapa do tesouro) ----------

        void RefreshTargets()
        {
            targets.Clear();
            long sig = 17;
            var board = FindAnyObjectByType<MissionBoard>();
            if (board)
                for (int i = 0; i < board.Active.Count; i++)
                {
                    var def = board.Active[i].definition;
                    if (!def || board.IsComplete(board.Active[i]) || string.IsNullOrEmpty(def.targetId)) continue;
                    var m = WorldMarker.Find(def.targetId);
                    if (!m) continue;
                    targets.Add((def.title, m.transform.position, Gold, "o"));
                    sig = sig * 31 + def.targetId.GetHashCode();
                }
            if (StoryLine.Current)
            {
                string id = StoryLine.Current.NextTargetId;
                var m = string.IsNullOrEmpty(id) ? null : WorldMarker.Find(id);
                if (m)
                {
                    targets.Add(("História", m.transform.position, new Color(0.35f, 0.6f, 0.95f), "o"));
                    sig = sig * 31 + id.GetHashCode();
                }
            }
            if (TreasureMaps.Current && TreasureMaps.Current.HasMap)
            {
                string id = TreasureMaps.Current.ActiveId;
                var m = string.IsNullOrEmpty(id) ? null : WorldMarker.Find(id);
                if (m)
                {
                    targets.Add(("Mapa do tesouro", m.transform.position, Red, "X"));
                    sig = sig * 31 + id.GetHashCode();
                }
            }
            markerSignature = sig;
        }

        Vector2 Pixel(Vector3 world)
        {
            var uv = chart.ToChart(world);
            return new Vector2(uv.x * MapSize, (1f - uv.y) * MapSize);
        }

        void BuildOverlay(VisualElement root)
        {
            foreach (var p in chart.Places)
            {
                if (p == null) continue;
                bool known = chart.IsKnown(p);
                if (!known && p.kind == SeaChart.PlaceKind.Lair) continue;
                string text = known ? p.name : "Ilha desconhecida";
                var color = p.kind == SeaChart.PlaceKind.Lair ? Red : Ink;
                Tag(root, Pixel(p.position), text, color, p.kind == SeaChart.PlaceKind.Lair ? "o" : null, known ? 15 : 12, !known);
            }
            // Alvos: só a bolinha (o nome fica na lista ao lado; no mapa se embolava com o da ilha).
            foreach (var t in targets)
            {
                var at = Pixel(t.pos);
                var dot = Dot(t.color, 14f, t.symbol == "X" ? "X" : null);
                dot.style.position = Position.Absolute;
                dot.style.left = at.x - 7f;
                dot.style.top = at.y - 7f;
                root.Add(dot);
            }
        }

        static void Tag(VisualElement root, Vector2 at, string text, Color color, string symbol, int size, bool italic)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = at.x - 90f;
            box.style.top = at.y - (symbol != null ? 12f : 9f);
            box.style.width = 180f;
            box.style.alignItems = Align.Center;
            if (symbol != null) box.Add(Dot(color, 13f, symbol == "X" ? "X" : null));
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.color = color;
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = italic ? FontStyle.Italic : FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            box.Add(l);
            root.Add(box);
        }

        /// <summary>Bolinha com aro escuro (e uma letra dentro, se couber): o marcador da carta (a fonte não tem ★ nem ☠).</summary>
        static VisualElement Dot(Color color, float size, string letter = null)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.style.width = d.style.height = size;
            d.style.backgroundColor = color;
            d.style.borderTopLeftRadius = d.style.borderTopRightRadius = d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = size * 0.5f;
            d.style.borderTopWidth = d.style.borderBottomWidth = d.style.borderLeftWidth = d.style.borderRightWidth = 2f;
            d.style.borderTopColor = d.style.borderBottomColor = d.style.borderLeftColor = d.style.borderRightColor = new Color(0.2f, 0.12f, 0.05f);
            d.style.marginBottom = 1f;
            d.style.justifyContent = Justify.Center;
            d.style.alignItems = Align.Center;
            if (!string.IsNullOrEmpty(letter) && letter.Length == 1 && char.IsLetter(letter[0]))
            {
                var l = new Label(letter) { pickingMode = PickingMode.Ignore };
                l.style.fontSize = size * 0.6f;
                l.style.color = Color.white;
                l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
                d.Add(l);
            }
            return d;
        }

        static void LegendLine(VisualElement side, Color dot, string text, bool muted = false)
        {
            var row = Hud.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;
            var d = Dot(dot, 11f);
            d.style.marginRight = 6;
            row.Add(d);
            row.Add(Hud.Text(text, "hud-window__small", muted ? "hud-window__muted" : "hud-window__text"));
            side.Add(row);
        }

        void BuildLegend(VisualElement side)
        {
            Win.Text(side, "Lugares", "hud-window__subtitle-strong");
            int known = 0;
            foreach (var p in chart.Places)
            {
                if (p == null || !chart.IsKnown(p)) continue;
                known++;
                LegendLine(side, p.kind == SeaChart.PlaceKind.Lair ? Red : Ink, p.name);
            }
                        if (known == 0) Win.Text(side, "<i>nenhum ainda</i>", "hud-window__small", "hud-window__muted");
            Win.Text(side, "Agora", "hud-window__subtitle-strong");
            if (targets.Count == 0) Win.Text(side, "<i>nenhum alvo: pegue um contrato na taverna ou com os moradores</i>", "hud-window__small", "hud-window__muted");
            foreach (var t in targets) LegendLine(side, t.color, t.label);
            LegendLine(side, new Color(0.75f, 0.45f, 0.08f), "o seu navio (seta)", true);
        }

        void UpdateShip()
        {
            Transform me = switcher ? (switcher.IsOnFoot ? (switcher.Character ? switcher.Character.transform : null) : (switcher.Ship ? switcher.Ship.transform : null)) : null;
            if (!me)
            {
                arrow.style.display = DisplayStyle.None;
                return;
            }
            arrow.style.display = DisplayStyle.Flex;
            var at = Pixel(me.position);
            arrow.style.left = at.x - ShipArrow.Size * 0.5f;
            arrow.style.top = at.y - ShipArrow.Size * 0.5f;
            arrow.style.rotate = new Rotate(new Angle(me.eulerAngles.y, AngleUnit.Degree));
        }

        void OnFound(SeaChart.Place p)
        {
            if (banner == null) return;
            banner.Show(p.name, p.kind == SeaChart.PlaceKind.Lair ? "covil marcado na carta (M)" : "ilha marcada na carta (M)");
            bannerUntil = Time.unscaledTime + bannerSeconds;
        }

        /// <summary>A seta do navio (proa para cima, gira com o rumo).</summary>
        sealed class ShipArrow : VisualElement
        {
            public const float Size = 26f;

            public ShipArrow()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.width = Size;
                style.height = Size;
                generateVisualContent += Draw;
            }

            static void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                p.fillColor = new Color(0.75f, 0.45f, 0.08f);
                p.strokeColor = new Color(0.2f, 0.12f, 0.05f);
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(new Vector2(Size * 0.5f, 2f));
                p.LineTo(new Vector2(Size - 4f, Size - 3f));
                p.LineTo(new Vector2(Size * 0.5f, Size * 0.72f));
                p.LineTo(new Vector2(4f, Size - 3f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }

        /// <summary>Rosa dos ventos no canto da carta (N para cima).</summary>
        sealed class CompassRose : VisualElement
        {
            public CompassRose()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                var n = new Label("N") { pickingMode = PickingMode.Ignore };
                n.style.position = Position.Absolute;
                n.style.left = 29;
                n.style.top = -16;
                n.style.color = Ink;
                n.style.fontSize = 14;
                n.style.unityFontStyleAndWeight = FontStyle.Bold;
                Add(n);
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                var c = r.center;
                float big = Mathf.Min(r.width, r.height) * 0.5f, small = big * 0.28f;
                var p = ctx.painter2D;
                p.lineWidth = 1.5f;
                p.strokeColor = Ink;
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    var tip = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * big;
                    var l = c + new Vector2(Mathf.Sin(a - Mathf.PI * 0.5f), -Mathf.Cos(a - Mathf.PI * 0.5f)) * small;
                    var rr = c + new Vector2(Mathf.Sin(a + Mathf.PI * 0.5f), -Mathf.Cos(a + Mathf.PI * 0.5f)) * small;
                    p.fillColor = i == 0 ? Red : Ink;
                    p.BeginPath(); p.MoveTo(c); p.LineTo(l); p.LineTo(tip); p.ClosePath(); p.Fill();
                    p.fillColor = i == 0 ? new Color(0.95f, 0.85f, 0.7f) : new Color(0.85f, 0.76f, 0.58f);
                    p.BeginPath(); p.MoveTo(c); p.LineTo(rr); p.LineTo(tip); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(l); p.LineTo(tip); p.LineTo(rr); p.Stroke();
                }
            }
        }
    }
}
