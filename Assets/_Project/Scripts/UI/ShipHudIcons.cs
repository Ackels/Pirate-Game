using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Ícones da HUD do navio (21/10), desenhados em vetor (Painter2D, como o aviso de golpe): o <b>timão</b> do medalhão (gira com o
    /// leme pelo style.rotate), o <b>canhão</b> dos cartões de bordo, a <b>bala</b> comum e a <b>rosa dos ventos</b> com o navio de proa
    /// para cima. Desenhados uma vez; só a cor muda (Set).
    /// </summary>
    class ShipIcon : VisualElement
    {
        public enum Kind { Wheel, Cannon, Ball, Compass }

        static readonly Color Ink = new(0.08f, 0.04f, 0.02f, 0.95f);
        readonly Kind kind;
        readonly bool mirror;
        Color color;

        public ShipIcon(Kind iconKind, Color tint, bool mirrored = false)
        {
            kind = iconKind;
            color = tint;
            mirror = mirrored;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-ship-icon");
            generateVisualContent += Draw;
        }

        public void Set(Color tint)
        {
            if (tint == color) return;
            color = tint;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            float s = Mathf.Min(rect.width, rect.height) / 100f;
            if (s <= 0f) return;
            var o = new Vector2(rect.x + (rect.width - 100f * s) * 0.5f, rect.y + (rect.height - 100f * s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(mirror ? 100f - x : x, y) * s;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            switch (kind)
            {
                case Kind.Wheel: Wheel(p, P, s); break;
                case Kind.Cannon: Cannon(p, P, s); break;
                case Kind.Ball: Ball(p, P, s); break;
                default: Compass(p, P, s); break;
            }
        }

        // ---------- Timão: 8 raios com punhos, aro duplo e o cubo ----------

        void Wheel(Painter2D p, System.Func<float, float, Vector2> P, float s)
        {
            var c = new Vector2(50f, 50f);
            var dark = Color.Lerp(color, Ink, 0.45f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                var tip = c + dir * 44f;
                Stroke(p, P(c.x, c.y), P(tip.x, tip.y), dark, 9f * s);
                Stroke(p, P(c.x, c.y), P(tip.x, tip.y), color, 5f * s);
                // Raio de cima (o "raio-rei"): punho maior e vermelho, para ver o timão girar (os 8 raios são iguais).
                bool king = i == 0;
                p.BeginPath();
                var knob = c + dir * (king ? 41f : 42f);
                p.Arc(P(knob.x, knob.y), (king ? 7.5f : 5.5f) * s, 0f, 360f);
                p.fillColor = king ? new Color(0.86f, 0.22f, 0.16f) : color;
                p.Fill();
                p.strokeColor = Ink;
                p.lineWidth = 2f * s;
                p.Stroke();
            }
            Ring(p, P(50f, 50f), 30f * s, 7f * s, color, s);
            p.BeginPath();
            p.Arc(P(50f, 50f), 10f * s, 0f, 360f);
            p.fillColor = color;
            p.Fill();
            p.strokeColor = Ink;
            p.lineWidth = 2.5f * s;
            p.Stroke();
            p.BeginPath();
            p.Arc(P(50f, 50f), 4f * s, 0f, 360f);
            p.fillColor = dark;
            p.Fill();
        }

        static void Ring(Painter2D p, Vector2 center, float radius, float width, Color tint, float s)
        {
            p.BeginPath();
            p.Arc(center, radius, 0f, 360f);
            p.strokeColor = Ink;
            p.lineWidth = width + 4f * s;
            p.Stroke();
            p.BeginPath();
            p.Arc(center, radius, 0f, 360f);
            p.strokeColor = tint;
            p.lineWidth = width;
            p.Stroke();
        }

        static void Stroke(Painter2D p, Vector2 a, Vector2 b, Color tint, float width)
        {
            p.strokeColor = tint;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        // ---------- Canhão de lado (boca para a direita; espelhado no bombordo) ----------

        void Cannon(Painter2D p, System.Func<float, float, Vector2> P, float s)
        {
            var dark = Color.Lerp(color, Ink, 0.5f);
            // Carreta de madeira
            p.BeginPath();
            p.MoveTo(P(14f, 70f));
            p.LineTo(P(62f, 70f));
            p.LineTo(P(56f, 54f));
            p.LineTo(P(22f, 54f));
            p.ClosePath();
            Fill(p, new Color(0.55f, 0.33f, 0.17f), s);
            // Cano: afunila até a boca, com o reforço da boca
            p.BeginPath();
            p.MoveTo(P(12f, 36f));
            p.LineTo(P(80f, 41f));
            p.LineTo(P(80f, 55f));
            p.LineTo(P(12f, 62f));
            p.ClosePath();
            Fill(p, color, s);
            p.BeginPath();
            p.MoveTo(P(78f, 37f));
            p.LineTo(P(90f, 37f));
            p.LineTo(P(90f, 59f));
            p.LineTo(P(78f, 59f));
            p.ClosePath();
            Fill(p, color, s);
            p.BeginPath();
            p.Arc(P(10f, 49f), 7f * s, 0f, 360f);
            Fill(p, color, s);
            // Rodas
            foreach (float x in new[] { 24f, 52f })
            {
                p.BeginPath();
                p.Arc(P(x, 74f), 10f * s, 0f, 360f);
                Fill(p, dark, s);
                p.BeginPath();
                p.Arc(P(x, 74f), 3f * s, 0f, 360f);
                p.fillColor = color;
                p.Fill();
            }
        }

        static void Fill(Painter2D p, Color fill, float s)
        {
            p.fillColor = fill;
            p.Fill();
            p.strokeColor = Ink;
            p.lineWidth = 3f * s;
            p.Stroke();
        }

        // ---------- Bala de canhão ----------

        void Ball(Painter2D p, System.Func<float, float, Vector2> P, float s)
        {
            p.BeginPath();
            p.Arc(P(50f, 52f), 32f * s, 0f, 360f);
            Fill(p, color, s);
            p.BeginPath();
            p.Arc(P(40f, 42f), 9f * s, 0f, 360f);
            p.fillColor = Color.Lerp(color, Color.white, 0.45f);
            p.Fill();
        }

        // ---------- Rosa dos ventos com o navio no meio (proa para cima) ----------

        void Compass(Painter2D p, System.Func<float, float, Vector2> P, float s)
        {
            p.BeginPath();
            p.Arc(P(50f, 50f), 47f * s, 0f, 360f);
            p.fillColor = new Color(0.1f, 0.06f, 0.04f, 0.75f);
            p.Fill();
            p.strokeColor = color;
            p.lineWidth = 3f * s;
            p.Stroke();
            // Marcas a cada 45° (as da proa, popa e lados maiores)
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                float inner = i % 4 == 0 ? 36f : i % 2 == 0 ? 39f : 42f;
                var a0 = new Vector2(50f, 50f) + dir * inner;
                var a1 = new Vector2(50f, 50f) + dir * 45f;
                Stroke(p, P(a0.x, a0.y), P(a1.x, a1.y), Color.Lerp(color, Ink, i % 4 == 0 ? 0f : 0.35f), (i % 4 == 0 ? 3f : 1.8f) * s);
            }
            // O navio visto de cima: proa pontuda para cima
            p.BeginPath();
            p.MoveTo(P(50f, 27f));
            p.BezierCurveTo(P(59f, 36f), P(60f, 52f), P(58f, 70f));
            p.LineTo(P(42f, 70f));
            p.BezierCurveTo(P(40f, 52f), P(41f, 36f), P(50f, 27f));
            p.ClosePath();
            Fill(p, new Color(0.62f, 0.4f, 0.22f), s);
            Stroke(p, P(50f, 38f), P(50f, 62f), new Color(0.97f, 0.91f, 0.79f, 0.85f), 2f * s);
        }
    }

    /// <summary>Seta do vento sobre a rosa: atravessa o mostrador, ponta para onde o vento sopra (o elemento gira).</summary>
    class WindArrow : VisualElement
    {
        static readonly Color Ink = new(0.08f, 0.04f, 0.02f, 0.9f);
        Color color;

        public WindArrow(Color tint)
        {
            color = tint;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-nav__arrow");
            generateVisualContent += Draw;
        }

        public void Set(Color tint)
        {
            if (tint == color) return;
            color = tint;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            float s = Mathf.Min(rect.width, rect.height) / 100f;
            if (s <= 0f) return;
            var o = new Vector2(rect.x + (rect.width - 100f * s) * 0.5f, rect.y + (rect.height - 100f * s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            // Cauda com penas embaixo, ponta larga em cima (fora do navio, perto do aro).
            p.BeginPath();
            p.MoveTo(P(50f, 4f));
            p.LineTo(P(61f, 20f));
            p.LineTo(P(52.5f, 20f));
            p.LineTo(P(52.5f, 86f));
            p.LineTo(P(60f, 96f));
            p.LineTo(P(50f, 90f));
            p.LineTo(P(40f, 96f));
            p.LineTo(P(47.5f, 86f));
            p.LineTo(P(47.5f, 20f));
            p.LineTo(P(39f, 20f));
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
            p.strokeColor = Ink;
            p.lineWidth = 2f * s;
            p.Stroke();
        }
    }
}
