using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Ícone do aviso de golpe sobre o inimigo (21/10, pedido do Jhonata: "algo mais na estética pirata", menor que o "!"), desenhado em vetor
    /// (Painter2D: nítido em qualquer tamanho e na cor que quiser): <b>espadas cruzadas</b> = golpe que a guarda segura (vermelhas no preparo,
    /// douradas e pulsando na hora de erguer a guarda) e <b>caveira com ossos cruzados</b> = golpe que a guarda não segura (azul: role).
    /// </summary>
    class StrikeWarningIcon : VisualElement
    {
        public enum Kind { Swords, Skull }

        static readonly Color Outline = new(0.08f, 0.04f, 0.02f, 0.95f);
        static readonly Color Backdrop = new(0.1f, 0.05f, 0.03f, 0.78f);
        Kind kind;
        Color color = Color.clear;

        public StrikeWarningIcon()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-strike-icon");
            generateVisualContent += Draw;
        }

        public void Set(Kind value, Color tint)
        {
            if (value == kind && tint == color) return;
            kind = value;
            color = tint;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            float s = Mathf.Min(rect.width, rect.height) / 100f;
            if (s <= 0f) return;
            var origin = new Vector2(rect.x + (rect.width - 100f * s) * 0.5f, rect.y + (rect.height - 100f * s) * 0.5f);
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            // Medalhão escuro com o aro na cor do aviso: o desenho lê em cima da areia clara e da mata escura.
            p.BeginPath();
            p.Arc(origin + new Vector2(50f, 50f) * s, 47f * s, 0f, 360f);
            p.fillColor = Backdrop;
            p.Fill();
            p.strokeColor = color;
            p.lineWidth = 5f * s;
            p.Stroke();
            if (kind == Kind.Swords)
            {
                Sabre(p, origin, s, new Vector2(66f, 66f), -45f, false);
                Sabre(p, origin, s, new Vector2(34f, 66f), 45f, true);
            }
            else Skull(p, origin, s);
        }

        // ---------- Espadas cruzadas ----------

        static readonly Vector2[] Blade = { new(-6f, 6f), new(-7f, -10f), new(-5f, -30f), new(0f, -43f), new(8f, -48f), new(7f, -30f), new(6.5f, -10f), new(6f, 6f) };

        /// <summary>Um sabre de lâmina curva, punho embaixo em <paramref name="hilt"/>, girado <paramref name="degrees"/>.</summary>
        void Sabre(Painter2D p, Vector2 origin, float s, Vector2 hilt, float degrees, bool mirror)
        {
            float rad = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            Vector2 At(float x, float y)
            {
                if (mirror) x = -x;
                return origin + (hilt + new Vector2(x * cos - y * sin, x * sin + y * cos)) * s;
            }
            // Lâmina (clara), guarda e punho (mais escuros), pomo.
            p.BeginPath();
            p.MoveTo(At(Blade[0].x, Blade[0].y));
            for (int i = 1; i < Blade.Length; i++) p.LineTo(At(Blade[i].x, Blade[i].y));
            p.ClosePath();
            FillAndOutline(p, Color.Lerp(color, Color.white, 0.25f), s);

            var dark = Color.Lerp(color, Outline, 0.35f);
            Box(p, At, -13f, 6f, 13f, 12f);
            FillAndOutline(p, dark, s);
            Box(p, At, -3.5f, 12f, 3.5f, 20f);
            FillAndOutline(p, dark, s);
            p.BeginPath();
            p.Arc(At(0f, 23f), 5f * s, 0f, 360f);
            FillAndOutline(p, dark, s);
        }

        static void Box(Painter2D p, System.Func<float, float, Vector2> at, float x0, float y0, float x1, float y1)
        {
            p.BeginPath();
            p.MoveTo(at(x0, y0));
            p.LineTo(at(x1, y0));
            p.LineTo(at(x1, y1));
            p.LineTo(at(x0, y1));
            p.ClosePath();
        }

        // ---------- Caveira com ossos cruzados ----------

        void Skull(Painter2D p, Vector2 o, float s)
        {
            Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
            // Ossos atrás: contorno escuro grosso e o osso por cima, com as pontas redondas.
            foreach (var (a, b) in new[] { (P(20f, 84f), P(80f, 58f)), (P(20f, 58f), P(80f, 84f)) })
            {
                p.strokeColor = Outline;
                p.lineWidth = 15f * s;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
                p.strokeColor = color;
                p.lineWidth = 9f * s;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
            // Cabeça e queixo.
            p.BeginPath();
            p.Arc(P(50f, 40f), 23f * s, 0f, 360f);
            FillAndOutline(p, color, s);
            p.BeginPath();
            p.MoveTo(P(37f, 54f));
            p.LineTo(P(63f, 54f));
            p.LineTo(P(61f, 72f));
            p.LineTo(P(39f, 72f));
            p.ClosePath();
            FillAndOutline(p, color, s);
            // Olhos, nariz e dentes.
            p.fillColor = Outline;
            p.BeginPath();
            p.Arc(P(40f, 42f), 7f * s, 0f, 360f);
            p.Fill();
            p.BeginPath();
            p.Arc(P(60f, 42f), 7f * s, 0f, 360f);
            p.Fill();
            p.BeginPath();
            p.MoveTo(P(50f, 50f));
            p.LineTo(P(46f, 57f));
            p.LineTo(P(54f, 57f));
            p.ClosePath();
            p.Fill();
            p.strokeColor = Outline;
            p.lineWidth = 2.5f * s;
            for (int i = 0; i < 3; i++)
            {
                float x = 44f + i * 6f;
                p.BeginPath();
                p.MoveTo(P(x, 63f));
                p.LineTo(P(x, 71f));
                p.Stroke();
            }
        }

        static void FillAndOutline(Painter2D p, Color fill, float s)
        {
            p.fillColor = fill;
            p.Fill();
            p.strokeColor = Outline;
            p.lineWidth = 3f * s;
            p.Stroke();
        }
    }

    /// <summary>Ícones de aviso de golpe presos a pontos do mundo (Begin, um Place por inimigo, End), reaproveitados.</summary>
    class WorldIconPool
    {
        readonly VisualElement layer;
        readonly List<StrikeWarningIcon> icons = new();
        readonly List<bool> big = new();
        int used;
        Camera cam;

        public WorldIconPool(VisualElement worldLayer) => layer = worldLayer;

        public bool Begin()
        {
            used = 0;
            cam = Camera.main;
            return cam && layer.panel != null;
        }

        /// <summary>Ícone sobre <paramref name="world"/>; <paramref name="now"/> = a hora de reagir (maior, pulsando).</summary>
        public void Place(Vector3 world, StrikeWarningIcon.Kind kind, Color color, bool now)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(layer.panel, new Vector2(screen.x, Screen.height - screen.y));
            if (used == icons.Count)
            {
                var created = new StrikeWarningIcon();
                layer.Add(created);
                icons.Add(created);
                big.Add(false);
            }
            var icon = icons[used];
            icon.Set(kind, color);
            if (big[used] != now)
            {
                big[used] = now;
                icon.EnableInClassList("hud-strike-icon--now", now);
            }
            icon.style.left = p.x;
            icon.style.top = p.y;
            // Na hora de reagir, pulsa (escala: não refaz o layout).
            float pulse = now ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 22f) : 1f;
            icon.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
            if (icon.style.display == DisplayStyle.None) icon.style.display = DisplayStyle.Flex;
            used++;
        }

        public void End()
        {
            for (int i = used; i < icons.Count; i++)
                if (icons[i].style.display != DisplayStyle.None) icons[i].style.display = DisplayStyle.None;
        }

        public void HideAll()
        {
            used = 0;
            End();
        }
    }
}
