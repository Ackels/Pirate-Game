using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Ícone da abordagem sobre o navio inimigo (04/11, pedido do Jhonata: "um ícone, algum aviso melhor do que só aquilo escrito"):
    /// medalhão escuro com o aro na cor e um gancho de abordagem de três pontas com a corda, desenhado em vetor como os avisos de golpe;
    /// embaixo, uma linha curta ("[F] Abordar" ou o que falta). Dourado e pulsando = dá para abordar; apagado = ainda não.
    /// </summary>
    class BoardingIcon : VisualElement
    {
        static readonly Color Outline = new(0.08f, 0.04f, 0.02f, 0.95f);
        static readonly Color Backdrop = new(0.1f, 0.05f, 0.03f, 0.8f);
        static readonly Color Rope = new(0.86f, 0.74f, 0.52f, 1f);

        readonly VisualElement medal;
        readonly Label caption;
        Color color = Color.clear;

        public BoardingIcon()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-board-icon");
            medal = new VisualElement { pickingMode = PickingMode.Ignore };
            medal.AddToClassList("hud-board-icon__medal");
            medal.generateVisualContent += Draw;
            Add(medal);
            caption = new Label { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("hud-board-icon__text");
            Add(caption);
            style.display = DisplayStyle.None;
        }

        /// <summary>Põe sobre o ponto do mundo; fora da tela (ou atrás da câmera), esconde.</summary>
        public void Place(VisualElement layer, Camera cam, Vector3 world, Color tint, string text, bool pulse)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f || layer.panel == null)
            {
                Hide();
                return;
            }
            Vector2 p = RuntimePanelUtils.ScreenToPanel(layer.panel, new Vector2(screen.x, Screen.height - screen.y));
            if (tint != color)
            {
                color = tint;
                medal.MarkDirtyRepaint();
                caption.style.color = tint;
            }
            Hud.Set(caption, text);
            style.left = p.x;
            style.top = p.y;
            float k = pulse ? 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            medal.style.scale = new Scale(new Vector3(k, k, 1f));
            if (style.display == DisplayStyle.None) style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (style.display != DisplayStyle.None) style.display = DisplayStyle.None;
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = medal.contentRect;
            float s = Mathf.Min(rect.width, rect.height) / 100f;
            if (s <= 0f) return;
            var o = new Vector2(rect.x + (rect.width - 100f * s) * 0.5f, rect.y + (rect.height - 100f * s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            p.BeginPath();
            p.Arc(P(50f, 50f), 47f * s, 0f, 360f);
            p.fillColor = Backdrop;
            p.Fill();
            p.strokeColor = color;
            p.lineWidth = 5f * s;
            p.Stroke();

            // Corda: sai da argola e some pelo alto, ondulando.
            Stroke(p, s, 9f, 5f, Rope, path =>
            {
                path.MoveTo(P(50f, 22f));
                path.BezierCurveTo(P(58f, 14f), P(66f, 22f), P(72f, 12f));
            });

            // Haste, argola e as três pontas curvas do gancho.
            var metal = Color.Lerp(color, Color.white, 0.2f);
            Stroke(p, s, 14f, 8f, metal, path =>
            {
                path.MoveTo(P(50f, 28f));
                path.LineTo(P(50f, 70f));
            });
            Stroke(p, s, 12f, 6f, metal, path =>
            {
                path.MoveTo(P(50f, 70f));
                path.BezierCurveTo(P(36f, 80f), P(22f, 70f), P(24f, 54f));
            });
            Stroke(p, s, 12f, 6f, metal, path =>
            {
                path.MoveTo(P(50f, 70f));
                path.BezierCurveTo(P(64f, 80f), P(78f, 70f), P(76f, 54f));
            });
            Stroke(p, s, 12f, 6f, metal, path =>
            {
                path.MoveTo(P(50f, 70f));
                path.LineTo(P(50f, 84f));
            });
            p.BeginPath();
            p.Arc(P(50f, 24f), 6f * s, 0f, 360f);
            p.strokeColor = Outline;
            p.lineWidth = 9f * s;
            p.Stroke();
            p.strokeColor = metal;
            p.lineWidth = 4.5f * s;
            p.Stroke();
        }

        /// <summary>Traço com contorno escuro grosso e a cor por cima (lê na água clara e na escura).</summary>
        static void Stroke(Painter2D p, float s, float outer, float inner, Color tint, System.Action<Painter2D> path)
        {
            p.strokeColor = Outline;
            p.lineWidth = outer * s;
            p.BeginPath();
            path(p);
            p.Stroke();
            p.strokeColor = tint;
            p.lineWidth = inner * s;
            p.BeginPath();
            path(p);
            p.Stroke();
        }
    }
}
