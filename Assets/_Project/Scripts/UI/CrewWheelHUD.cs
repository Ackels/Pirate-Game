using Pirates.Controls;
using Pirates.LandCombat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Roda de comando dos marujos (30/10) no HUD: o anel de 6 fatias em volta do centro que o <see cref="CrewCommander"/> escolheu (fatia
    /// sob o mouse em dourado), o nome de cada ordem na fatia e, no meio, a ordem escolhida com a explicação e a que vale agora; apontando,
    /// uma plaquinha no alto diz o que fazer. Avisos quando a ordem sai ou não dá. Só remonta o que mudou.
    /// </summary>
    public class CrewWheelHUD : MonoBehaviour
    {
        static readonly Color CrewColor = new(0.55f, 0.8f, 1f);

        public static string Title(CrewOrder order) => order switch
        {
            CrewOrder.Follow => "Seguir",
            CrewOrder.Hold => "Ficar aqui",
            CrewOrder.MoveTo => "Ir até lá",
            CrewOrder.Attack => "Atacar alvo",
            CrewOrder.Guard => "Proteger",
            _ => "Não lutar",
        };

        static string Explain(CrewOrder order) => order switch
        {
            CrewOrder.Follow => "vêm atrás do capitão e brigam com quem estiver em alerta por perto",
            CrewOrder.Hold => "param onde estão e só lutam com quem chegar perto",
            CrewOrder.MoveTo => "aponte o lugar e clique: vão até lá e esperam",
            CrewOrder.Attack => "aponte um pirata e clique: os dois vão nele",
            CrewOrder.Guard => "colados no capitão; só batem em quem chega nele",
            _ => "seguem sem atacar (bom para passar escondido); só se defendem",
        };

        static string Said(CrewOrder order) => order switch
        {
            CrewOrder.Follow => "Marujos, comigo!",
            CrewOrder.Hold => "Marujos, fiquem aqui!",
            CrewOrder.MoveTo => "Marujos, para lá!",
            CrewOrder.Attack => "Marujos, peguem aquele!",
            CrewOrder.Guard => "Marujos, protejam o capitão!",
            _ => "Marujos, sem briga!",
        };

        CrewCommander commander;
        VisualElement root, hint, middle;
        WheelGraphic wheel;
        readonly Label[] sliceLabels = new Label[6];
        Label title, desc, now, hintText;
        int shownHover = -2;
        CrewOrder shownCurrent = (CrewOrder)(-1), shownPending = (CrewOrder)(-1);
        CrewCommander.Phase shownState = (CrewCommander.Phase)(-1);
        Vector2 shownCenter = new(-1f, -1f);

        void Update()
        {
            if (!commander)
            {
                commander = FindAnyObjectByType<CrewCommander>(FindObjectsInactive.Include);
                if (!commander) return;
                commander.Ordered += OnOrdered;
                commander.Refused += OnRefused;
            }
            var hud = GameHud.Current;
            if (!hud || hud.Windows == null) return;
            if (root == null) Build(hud);
            bool open = commander.isActiveAndEnabled && commander.State == CrewCommander.Phase.Wheel;
            bool pointing = commander.isActiveAndEnabled && commander.State == CrewCommander.Phase.Pointing;
            if (commander.State != shownState)
            {
                shownState = commander.State;
                root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                hint.style.display = pointing ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (pointing && commander.Pending != shownPending)
            {
                shownPending = commander.Pending;
                hintText.text = commander.Pending == CrewOrder.Attack
                    ? "<b>Atacar alvo</b>: aponte um pirata e clique   ·   <b>botão direito</b> / <b>Esc</b> cancela"
                    : "<b>Ir até lá</b>: aponte o lugar e clique   ·   <b>botão direito</b> / <b>Esc</b> cancela";
            }
            if (!open) return;
            Place(hud);
            if (commander.Hover != shownHover || commander.Current != shownCurrent)
            {
                shownHover = commander.Hover;
                shownCurrent = commander.Current;
                wheel.Hover = shownHover;
                wheel.MarkDirtyRepaint();
                for (int i = 0; i < sliceLabels.Length; i++)
                    sliceLabels[i].EnableInClassList("hud-crew-wheel__slice--hover", i == shownHover);
                if (shownHover >= 0)
                {
                    var order = CrewCommander.Slices[shownHover];
                    title.text = Title(order);
                    desc.text = Explain(order);
                }
                else
                {
                    title.text = "Marujos";
                    desc.text = "mova o mouse para uma ordem e solte o T";
                }
                now.text = $"agora: {Title(shownCurrent).ToLowerInvariant()}";
            }
        }

        void OnDestroy()
        {
            if (!commander) return;
            commander.Ordered -= OnOrdered;
            commander.Refused -= OnRefused;
        }

        void Build(GameHud hud)
        {
            root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-crew-wheel");
            wheel = new WheelGraphic(CrewCommander.Slices.Length);
            root.Add(wheel);
            for (int i = 0; i < sliceLabels.Length; i++)
            {
                var label = new Label(Title(CrewCommander.Slices[i])) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("hud-crew-wheel__slice");
                root.Add(label);
                sliceLabels[i] = label;
            }
            middle = new VisualElement { pickingMode = PickingMode.Ignore };
            middle.AddToClassList("hud-crew-wheel__middle");
            title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("hud-crew-wheel__title");
            desc = new Label { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("hud-crew-wheel__desc");
            now = new Label { pickingMode = PickingMode.Ignore };
            now.AddToClassList("hud-crew-wheel__now");
            middle.Add(title);
            middle.Add(now);
            root.Add(middle);
            root.Add(desc);
            root.style.display = DisplayStyle.None;
            hud.Windows.Add(root);

            hint = new VisualElement { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("hud-crew-hint");
            hintText = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
            hint.Add(hintText);
            hint.style.display = DisplayStyle.None;
            hud.Windows.Add(hint);
        }

        /// <summary>Põe a roda no centro escolhido, no tamanho do raio da tela (só quando mudam).</summary>
        void Place(GameHud hud)
        {
            Vector2 center = commander.Center;
            if (center == shownCenter || hud.Windows.panel == null) return;
            shownCenter = center;
            var panel = hud.Windows.panel;
            Vector2 c = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(center.x, Screen.height - center.y));
            Vector2 edge = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(center.x + commander.RadiusPixels, Screen.height - center.y));
            float r = Mathf.Max(60f, edge.x - c.x);
            root.style.left = c.x - r;
            root.style.top = c.y - r;
            root.style.width = root.style.height = 2f * r;
            wheel.Radius = r;
            wheel.MarkDirtyRepaint();
            float box = r * 0.46f;
            middle.style.left = r - box;
            middle.style.top = r - box;
            middle.style.width = middle.style.height = 2f * box;
            // A explicação vai numa plaquinha embaixo da roda (no meio não cabe).
            desc.style.left = r - 190f;
            desc.style.top = 2f * r + 10f;
            desc.style.width = 380f;
            float mid = r * 0.76f, step = 360f / sliceLabels.Length;
            for (int i = 0; i < sliceLabels.Length; i++)
            {
                float a = (i * step - 90f) * Mathf.Deg2Rad;
                var label = sliceLabels[i];
                label.style.left = r + Mathf.Cos(a) * mid - 70f;
                label.style.top = r + Mathf.Sin(a) * mid - 18f;
            }
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        static void OnOrdered(CrewOrder order, int count) =>
            Feed($"<b>{Said(order)}</b>\n<size=75%>{(count == 1 ? "o marujo" : "os marujos")}: {Explain(order)}</size>", CrewColor);

        static void OnRefused(string why) => Feed(why, HudColors.Bad);

        /// <summary>O anel de fatias (Painter2D): madeira escura com aro dourado, a fatia sob o mouse acesa.</summary>
        class WheelGraphic : VisualElement
        {
            static readonly Color Wood = new(0.13f, 0.07f, 0.04f, 0.86f), Lit = new(0.72f, 0.48f, 0.16f, 0.95f),
                Rim = new(0.95f, 0.76f, 0.35f, 0.95f), Center = new(0.09f, 0.05f, 0.03f, 0.9f);
            readonly int slices;
            public int Hover = -1;
            public float Radius = 160f;

            public WheelGraphic(int count)
            {
                slices = count;
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.top = style.right = style.bottom = 0f;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float r = Radius, inner = r * 0.52f, gap = 1.6f;
                var c = new Vector2(r, r);
                float step = 360f / slices;
                p.lineJoin = LineJoin.Round;
                for (int i = 0; i < slices; i++)
                {
                    float a0 = i * step - 90f - step * 0.5f + gap, a1 = a0 + step - 2f * gap;
                    p.BeginPath();
                    p.Arc(c, r, a0, a1);
                    p.Arc(c, inner, a1, a0, ArcDirection.CounterClockwise);
                    p.ClosePath();
                    p.fillColor = i == Hover ? Lit : Wood;
                    p.Fill();
                    p.strokeColor = Rim;
                    p.lineWidth = i == Hover ? 3.5f : 2f;
                    p.Stroke();
                }
                p.BeginPath();
                p.Arc(c, inner - 6f, 0f, 360f);
                p.fillColor = Center;
                p.Fill();
                p.strokeColor = Rim;
                p.lineWidth = 2f;
                p.Stroke();
            }
        }
    }
}
