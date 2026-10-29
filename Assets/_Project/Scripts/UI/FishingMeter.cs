using Pirates.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// O painel da briga com o peixe (D2, 27/10, à moda do Stardew Valley; refeito no estilo pirata do HUD a pedido do Jhonata), ao lado
    /// do capitão: cartão de madeira escura com a plaquinha "Fisgado!", a vara de bambu com a carretilha (gira enquanto o jogador segura),
    /// a faixa de água desenhada (ondas, bolhas subindo, algas e areia no fundo, borda de corda), a barra com moldura de corda, o peixe na
    /// cor da raridade e o medidor de latão com rebites (vermelho → dourado → verde). Treme com o peixe fora da barra. Desenhos em vetor
    /// (Painter2D); o resto só mexe no estilo quando o valor muda.
    /// </summary>
    class FishingMeter : VisualElement
    {
        const float Width = 150f, Height = 372f;
        static readonly Color Ink = new(0.12f, 0.07f, 0.03f, 1f);
        static readonly Color Brass = new(0.86f, 0.66f, 0.28f, 1f);
        static readonly Color BrassDark = new(0.5f, 0.34f, 0.12f, 1f);

        readonly WaterTrack water;
        readonly CatchBar bar;
        readonly FishIcon fish;
        readonly RodAndReel reel;
        readonly VisualElement fill;
        float shownBar = -1f, shownSize = -1f, shownFish = -1f, shownFill = -1f;
        bool shownInside = true;

        public FishingMeter()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-fishing");
            style.position = Position.Absolute;
            style.width = Width;
            style.height = Height;

            var title = new Label("Fisgado!") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("hud-fishing__title");
            Add(title);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("hud-fishing__row");
            Add(row);

            reel = new RodAndReel();
            reel.style.width = 24f;
            reel.style.marginRight = 2f;
            row.Add(reel);

            water = new WaterTrack();
            water.style.width = 60f;
            water.style.marginRight = 8f;
            row.Add(water);

            bar = new CatchBar();
            bar.style.position = Position.Absolute;
            bar.style.left = 5f;
            bar.style.right = 5f;
            water.Add(bar);

            fish = new FishIcon();
            fish.style.position = Position.Absolute;
            fish.style.left = 12f;
            fish.style.width = 34f;
            fish.style.height = 22f;
            // O centro do peixe fica na altura dele (bottom = a posição; meio peixe para baixo).
            fish.style.translate = new Translate(0f, Length.Percent(50f));
            water.Add(fish);

            var meter = new BrassTube();
            meter.style.width = 20f;
            row.Add(meter);

            // O líquido fica entre os dois rebites (desenhados pelo tubo, por baixo dos filhos).
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.style.position = Position.Absolute;
            slot.style.left = slot.style.right = 4f;
            slot.style.top = slot.style.bottom = 13f;
            meter.Add(slot);
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.position = Position.Absolute;
            fill.style.left = fill.style.right = fill.style.bottom = 0f;
            fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = 3f;
            fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 3f;
            slot.Add(fill);
        }

        /// <summary>Põe o painel ao lado do ponto (unidades do painel) e atualiza com a briga do <paramref name="rod"/>.</summary>
        public void Show(FishingRod rod, Vector2 nextTo, Color fishColor)
        {
            if (style.display == DisplayStyle.None) style.display = DisplayStyle.Flex;
            var root = parent;
            float maxX = root != null ? root.layout.width - Width - 8f : float.MaxValue;
            float maxY = root != null ? root.layout.height - Height - 8f : float.MaxValue;
            float x = Mathf.Clamp(nextTo.x + 70f, 8f, Mathf.Max(8f, maxX));
            float y = Mathf.Clamp(nextTo.y - Height * 0.6f, 8f, Mathf.Max(8f, maxY));
            // Treme com o peixe fora da barra.
            if (!rod.FightInside)
            {
                x += Mathf.Sin(Time.time * 61f) * 2f;
                y += Mathf.Sin(Time.time * 47f) * 1.5f;
            }
            style.left = x;
            style.top = y;

            if (Changed(ref shownBar, rod.FightBar) | Changed(ref shownSize, rod.FightBarSize))
            {
                bar.style.bottom = Length.Percent(rod.FightBar * 100f);
                bar.style.height = Length.Percent(rod.FightBarSize * 100f);
            }
            if (rod.FightInside != shownInside)
            {
                shownInside = rod.FightInside;
                bar.SetInside(shownInside);
            }
            if (Changed(ref shownFish, rod.FightFish)) fish.style.bottom = Length.Percent(Mathf.Clamp(rod.FightFish * 100f, 0f, 100f));
            fish.SetColor(fishColor);
            if (Changed(ref shownFill, rod.FightProgress))
            {
                float p = rod.FightProgress;
                fill.style.height = Length.Percent(p * 100f);
                fill.style.backgroundColor = p < 0.5f
                    ? Color.Lerp(new Color(0.78f, 0.22f, 0.16f), Brass, p * 2f)
                    : Color.Lerp(Brass, new Color(0.38f, 0.78f, 0.3f), (p - 0.5f) * 2f);
            }
            // Bolhas e ondas andam; a carretilha gira mais rápido segurando.
            water.Tick();
            reel.Tick(rod.FightHolding);
        }

        public void Hide()
        {
            if (style.display != DisplayStyle.None) style.display = DisplayStyle.None;
        }

        static bool Changed(ref float shown, float value)
        {
            if (Mathf.Abs(shown - value) < 0.0015f) return false;
            shown = value;
            return true;
        }

        /// <summary>Corda: traço grosso cor de cânhamo com riscos escuros na diagonal (as voltas do trançado).</summary>
        static void Rope(Painter2D p, Vector2 a, Vector2 b, float width)
        {
            p.lineCap = LineCap.Round;
            p.lineWidth = width + 2f;
            p.strokeColor = Ink;
            Line(p, a, b);
            p.lineWidth = width;
            p.strokeColor = new Color(0.82f, 0.66f, 0.4f, 1f);
            Line(p, a, b);
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            Vector2 dir = d / len, n = new(-dir.y, dir.x);
            p.lineWidth = 1.2f;
            p.strokeColor = new Color(0.45f, 0.3f, 0.14f, 1f);
            p.lineCap = LineCap.Butt;
            for (float s = width * 0.5f; s < len - width * 0.3f; s += width * 0.9f)
            {
                Vector2 c = a + dir * s;
                Line(p, c - n * width * 0.45f - dir * width * 0.3f, c + n * width * 0.45f + dir * width * 0.3f);
            }
        }

        static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        static void Rivet(Painter2D p, Vector2 at, float r)
        {
            p.fillColor = BrassDark;
            p.BeginPath();
            p.Arc(at, r + 1f, 0f, 360f);
            p.Fill();
            p.fillColor = Brass;
            p.BeginPath();
            p.Arc(at, r, 0f, 360f);
            p.Fill();
            p.fillColor = new Color(1f, 0.92f, 0.65f, 1f);
            p.BeginPath();
            p.Arc(at - new Vector2(r * 0.3f, r * 0.3f), r * 0.35f, 0f, 360f);
            p.Fill();
        }

        /// <summary>A faixa de água: azul em faixas (mais escuro no fundo), ondinhas na superfície, bolhas subindo, algas e areia, borda de corda.</summary>
        class WaterTrack : VisualElement
        {
            static readonly Color[] Bands =
            {
                new(0.33f, 0.66f, 0.78f), new(0.26f, 0.56f, 0.72f), new(0.2f, 0.46f, 0.66f), new(0.16f, 0.37f, 0.58f),
                new(0.13f, 0.29f, 0.5f), new(0.1f, 0.22f, 0.42f),
            };
            static readonly Vector3[] BubbleSeeds = { new(0.22f, 0.1f, 2.2f), new(0.7f, 0.45f, 1.6f), new(0.45f, 0.8f, 2.6f), new(0.82f, 0.25f, 1.9f), new(0.3f, 0.6f, 1.4f) };
            float time;

            public WaterTrack()
            {
                pickingMode = PickingMode.Ignore;
                style.overflow = Overflow.Hidden;
                generateVisualContent += Draw;
            }

            public void Tick()
            {
                time = Time.time;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                float w = r.width, h = r.height;
                if (w < 4f || h < 4f) return;
                var p = ctx.painter2D;
                // Água em faixas.
                float bh = h / Bands.Length;
                for (int i = 0; i < Bands.Length; i++)
                {
                    p.fillColor = Bands[i];
                    p.BeginPath();
                    p.MoveTo(new Vector2(0f, i * bh));
                    p.LineTo(new Vector2(w, i * bh));
                    p.LineTo(new Vector2(w, (i + 1) * bh + 1f));
                    p.LineTo(new Vector2(0f, (i + 1) * bh + 1f));
                    p.ClosePath();
                    p.Fill();
                }
                // Superfície com ondinhas e o céu claro em cima.
                p.fillColor = new Color(0.72f, 0.88f, 0.92f, 1f);
                p.BeginPath();
                p.MoveTo(Vector2.zero);
                p.LineTo(new Vector2(w, 0f));
                const int waves = 8;
                for (int i = waves; i >= 0; i--)
                {
                    float x = w * i / waves;
                    p.LineTo(new Vector2(x, 9f + Mathf.Sin(time * 3f + i * 1.3f) * 2.5f));
                }
                p.ClosePath();
                p.Fill();
                // Areia no fundo.
                p.fillColor = new Color(0.86f, 0.72f, 0.48f, 1f);
                p.BeginPath();
                p.MoveTo(new Vector2(0f, h));
                p.LineTo(new Vector2(0f, h - 10f));
                p.BezierCurveTo(new Vector2(w * 0.3f, h - 16f), new Vector2(w * 0.6f, h - 6f), new Vector2(w, h - 12f));
                p.LineTo(new Vector2(w, h));
                p.ClosePath();
                p.Fill();
                // Algas balançando.
                p.lineCap = LineCap.Round;
                p.lineWidth = 3f;
                p.strokeColor = new Color(0.22f, 0.55f, 0.3f, 1f);
                for (int k = 0; k < 3; k++)
                {
                    float bx = w * (0.18f + k * 0.33f), sway = Mathf.Sin(time * 1.7f + k * 2f) * 4f;
                    float top = h - 34f - k % 2 * 12f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(bx, h - 8f));
                    p.BezierCurveTo(new Vector2(bx - 5f, h - 18f), new Vector2(bx + 5f + sway * 0.5f, top + 10f), new Vector2(bx + sway, top));
                    p.Stroke();
                }
                // Bolhas subindo.
                p.lineWidth = 1.2f;
                p.strokeColor = new Color(0.85f, 0.95f, 1f, 0.75f);
                foreach (var b in BubbleSeeds)
                {
                    float k = Mathf.Repeat(time / (b.z * 2f) + b.y, 1f);
                    float y = Mathf.Lerp(h - 14f, 14f, k);
                    float x = w * b.x + Mathf.Sin(time * 2.4f + b.y * 9f) * 3f;
                    p.BeginPath();
                    p.Arc(new Vector2(x, y), 1.6f + k * 1.6f, 0f, 360f);
                    p.Stroke();
                }
                // Borda de corda.
                float m = 2.5f;
                Rope(p, new Vector2(m, m), new Vector2(w - m, m), 4f);
                Rope(p, new Vector2(w - m, m), new Vector2(w - m, h - m), 4f);
                Rope(p, new Vector2(w - m, h - m), new Vector2(m, h - m), 4f);
                Rope(p, new Vector2(m, h - m), new Vector2(m, m), 4f);
            }
        }

        /// <summary>A barra que pega o peixe: verde-mar translúcido com corda em cima e embaixo (mais apagada com o peixe fora).</summary>
        class CatchBar : VisualElement
        {
            bool inside = true;

            public CatchBar()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetInside(bool value)
            {
                inside = value;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                float w = r.width, h = r.height;
                if (h < 4f) return;
                var p = ctx.painter2D;
                p.fillColor = inside ? new Color(0.45f, 0.9f, 0.5f, 0.5f) : new Color(0.45f, 0.9f, 0.5f, 0.25f);
                p.BeginPath();
                p.MoveTo(new Vector2(1f, 2f));
                p.LineTo(new Vector2(w - 1f, 2f));
                p.LineTo(new Vector2(w - 1f, h - 2f));
                p.LineTo(new Vector2(1f, h - 2f));
                p.ClosePath();
                p.Fill();
                p.lineWidth = 1.5f;
                p.strokeColor = inside ? new Color(0.75f, 1f, 0.7f, 0.9f) : new Color(0.75f, 1f, 0.7f, 0.45f);
                Line(p, new Vector2(1.5f, 3f), new Vector2(1.5f, h - 3f));
                Line(p, new Vector2(w - 1.5f, 3f), new Vector2(w - 1.5f, h - 3f));
                Rope(p, new Vector2(1f, 3f), new Vector2(w - 1f, 3f), 5f);
                Rope(p, new Vector2(1f, h - 3f), new Vector2(w - 1f, h - 3f), 5f);
            }
        }

        /// <summary>Medidor: tubo de latão com fundo escuro, marcas a cada quarto, rebites e uma âncora em cima (a meta).</summary>
        class BrassTube : VisualElement
        {
            public BrassTube()
            {
                pickingMode = PickingMode.Ignore;
                style.overflow = Overflow.Hidden;
                generateVisualContent += Draw;
                style.backgroundColor = new Color(0.1f, 0.06f, 0.03f, 1f);
                style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 6f;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                float w = r.width, h = r.height;
                if (h < 10f) return;
                var p = ctx.painter2D;
                // Moldura de latão.
                p.lineWidth = 3f;
                p.strokeColor = BrassDark;
                p.BeginPath();
                p.MoveTo(new Vector2(1.5f, 1.5f));
                p.LineTo(new Vector2(w - 1.5f, 1.5f));
                p.LineTo(new Vector2(w - 1.5f, h - 1.5f));
                p.LineTo(new Vector2(1.5f, h - 1.5f));
                p.ClosePath();
                p.Stroke();
                p.lineWidth = 1.5f;
                p.strokeColor = Brass;
                p.BeginPath();
                p.MoveTo(new Vector2(2.5f, 2.5f));
                p.LineTo(new Vector2(w - 2.5f, 2.5f));
                p.LineTo(new Vector2(w - 2.5f, h - 2.5f));
                p.LineTo(new Vector2(2.5f, h - 2.5f));
                p.ClosePath();
                p.Stroke();
                // Marcas a cada quarto.
                p.lineWidth = 1.5f;
                p.strokeColor = new Color(0.95f, 0.85f, 0.6f, 0.55f);
                for (int i = 1; i < 4; i++)
                {
                    float y = h - (h - 8f) * i / 4f - 4f;
                    Line(p, new Vector2(3f, y), new Vector2(w * 0.45f, y));
                }
                Rivet(p, new Vector2(w * 0.5f, 7f), 2.6f);
                Rivet(p, new Vector2(w * 0.5f, h - 7f), 2.6f);
            }
        }

        /// <summary>Vara de bambu em pé ao lado da água, com a carretilha embaixo; a manivela gira (mais rápido segurando).</summary>
        class RodAndReel : VisualElement
        {
            float angle, last;

            public RodAndReel()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void Tick(bool holding)
            {
                float now = Time.unscaledTime;
                float dt = last > 0f ? Mathf.Min(0.1f, now - last) : 0f;
                last = now;
                angle += dt * (holding ? 900f : 140f);
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                float w = r.width, h = r.height;
                if (h < 20f) return;
                var p = ctx.painter2D;
                float cx = w * 0.5f;
                // Haste (afina para cima) com os nós do bambu.
                p.lineCap = LineCap.Round;
                p.strokeColor = Ink;
                p.lineWidth = 7f;
                Line(p, new Vector2(cx, 8f), new Vector2(cx, h - 30f));
                p.strokeColor = new Color(0.85f, 0.7f, 0.38f, 1f);
                p.lineWidth = 5f;
                Line(p, new Vector2(cx, 8f), new Vector2(cx, h - 30f));
                p.strokeColor = new Color(0.55f, 0.4f, 0.18f, 1f);
                p.lineWidth = 2f;
                for (float y = 34f; y < h - 40f; y += 38f) Line(p, new Vector2(cx - 3f, y), new Vector2(cx + 3f, y));
                // Ponteira e a linha descendo pela água (lado direito).
                p.strokeColor = new Color(0.95f, 0.95f, 0.9f, 0.9f);
                p.lineWidth = 1f;
                Line(p, new Vector2(cx, 8f), new Vector2(w + 4f, 14f));
                // Cabo de couro.
                p.strokeColor = Ink;
                p.lineWidth = 9f;
                Line(p, new Vector2(cx, h - 30f), new Vector2(cx, h - 4f));
                p.strokeColor = new Color(0.42f, 0.22f, 0.12f, 1f);
                p.lineWidth = 7f;
                Line(p, new Vector2(cx, h - 30f), new Vector2(cx, h - 4f));
                // Carretilha: disco de latão com a manivela girando.
                Vector2 c = new(cx, h - 44f);
                float rr = Mathf.Min(10f, w * 0.45f);
                p.fillColor = BrassDark;
                p.BeginPath();
                p.Arc(c, rr + 1.5f, 0f, 360f);
                p.Fill();
                p.fillColor = Brass;
                p.BeginPath();
                p.Arc(c, rr, 0f, 360f);
                p.Fill();
                p.fillColor = BrassDark;
                p.BeginPath();
                p.Arc(c, rr * 0.35f, 0f, 360f);
                p.Fill();
                float a = angle * Mathf.Deg2Rad;
                Vector2 tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rr * 0.85f);
                p.strokeColor = Ink;
                p.lineWidth = 2.5f;
                Line(p, c, tip);
                p.fillColor = new Color(0.42f, 0.22f, 0.12f, 1f);
                p.BeginPath();
                p.Arc(tip, 2.6f, 0f, 360f);
                p.Fill();
            }
        }

        /// <summary>Peixinho em vetor (corpo, rabo, barbatana e olho), virado para a esquerda.</summary>
        class FishIcon : VisualElement
        {
            Color color = Color.clear;

            public FishIcon()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetColor(Color value)
            {
                if (value == color) return;
                color = value;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                float w = r.width, h = r.height;
                var p = ctx.painter2D;
                p.lineWidth = 2f;
                p.strokeColor = Ink;
                p.fillColor = color;
                // Rabo.
                p.BeginPath();
                p.MoveTo(new Vector2(w * 0.7f, h * 0.5f));
                p.LineTo(new Vector2(w * 0.98f, h * 0.12f));
                p.LineTo(new Vector2(w * 0.92f, h * 0.5f));
                p.LineTo(new Vector2(w * 0.98f, h * 0.88f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                // Corpo (elipse por dois arcos de Bézier).
                p.BeginPath();
                p.MoveTo(new Vector2(w * 0.04f, h * 0.5f));
                p.BezierCurveTo(new Vector2(w * 0.2f, h * 0.02f), new Vector2(w * 0.6f, h * 0.02f), new Vector2(w * 0.76f, h * 0.5f));
                p.BezierCurveTo(new Vector2(w * 0.6f, h * 0.98f), new Vector2(w * 0.2f, h * 0.98f), new Vector2(w * 0.04f, h * 0.5f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                // Barbatana de cima e as escamas.
                p.BeginPath();
                p.MoveTo(new Vector2(w * 0.32f, h * 0.18f));
                p.LineTo(new Vector2(w * 0.46f, h * 0.0f));
                p.LineTo(new Vector2(w * 0.52f, h * 0.2f));
                p.Stroke();
                p.lineWidth = 1f;
                p.strokeColor = new Color(0f, 0f, 0f, 0.35f);
                p.BeginPath();
                p.Arc(new Vector2(w * 0.5f, h * 0.5f), h * 0.22f, -70f, 70f);
                p.Stroke();
                // Olho.
                p.fillColor = Color.white;
                p.BeginPath();
                p.Arc(new Vector2(w * 0.2f, h * 0.42f), Mathf.Max(2f, h * 0.13f), 0f, 360f);
                p.Fill();
                p.fillColor = Ink;
                p.BeginPath();
                p.Arc(new Vector2(w * 0.19f, h * 0.42f), Mathf.Max(1.2f, h * 0.07f), 0f, 360f);
                p.Fill();
            }
        }
    }
}
