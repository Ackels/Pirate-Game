using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Peças do HUD em UI Toolkit, com o visual em Hud.uss (madeira escura, pergaminho, faixa vermelha, letras do Pirata One).
    /// Quem monta escolhe as classes; quem atualiza só mexe em texto/valor quando eles mudam.
    /// </summary>
    static class Hud
    {
        public static VisualElement Box(params string[] classes)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) box.AddToClassList(c);
            return box;
        }

        public static Label Text(string text, params string[] classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            foreach (var c in classes) label.AddToClassList(c);
            return label;
        }

        /// <summary>Cartão de madeira escura (status) ou de pergaminho (listas), com título opcional.</summary>
        public static VisualElement Card(bool paper, string title = null)
        {
            var card = Box("hud-card", paper ? "hud-card--paper" : "hud-card--wood");
            if (title != null) card.Add(Text(title, "hud-card__title"));
            return card;
        }

        /// <summary>Tecla num botão redondo ("F", "Q").</summary>
        public static Label Key(string key) => Text(key, "hud-key");

        /// <summary>Linha horizontal: rótulo à esquerda, o resto ao lado.</summary>
        public static VisualElement Row(params string[] classes)
        {
            var row = Box(classes);
            row.AddToClassList("hud-row");
            return row;
        }

        public static void Show(VisualElement element, bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.resolvedStyle.display != display || element.style.display.keyword == StyleKeyword.Null)
                element.style.display = display;
        }

        /// <summary>Muda o texto só se mudou (o painel refaz a malha do texto a cada troca).</summary>
        public static void Set(Label label, string text)
        {
            if (!ReferenceEquals(label.text, text) && label.text != text) label.text = text;
        }
    }

    /// <summary>Barra com fundo escuro e preenchimento colorido (vida, espaço, recarga).</summary>
    class HudBar : VisualElement
    {
        readonly VisualElement fill;
        float shown = -1f;
        Color color;

        public HudBar(string extraClass = null)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-bar");
            if (extraClass != null) AddToClassList(extraClass);
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("hud-bar__fill");
            Add(fill);
        }

        public void Set(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - shown) < 0.002f) return;
            shown = fraction;
            fill.style.width = Length.Percent(fraction * 100f);
        }

        public void SetColor(Color value)
        {
            if (value == color) return;
            color = value;
            fill.style.backgroundColor = value;
        }

        /// <summary>Verde cheio → amarelo → vermelho vazio (vida), ou o contrário (espaço ocupado).</summary>
        public void SetDanger(float fraction, bool greenWhenFull)
        {
            Set(fraction);
            float danger = greenWhenFull ? 1f - fraction : fraction;
            SetColor(danger < 0.5f ? HudColors.Good : danger < 0.8f ? HudColors.Warn : HudColors.Bad);
        }
    }

    /// <summary>Aviso de ação embaixo da tela: tecla (opcional) + texto, numa placa de madeira.</summary>
    class HudPrompt : VisualElement
    {
        readonly Label key, text;

        public HudPrompt()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-prompt");
            key = Hud.Key("F");
            text = Hud.Text("", "hud-prompt__text");
            Add(key);
            Add(text);
            style.display = DisplayStyle.None;
        }

        public void Show(string keyText, string message)
        {
            Hud.Show(key, keyText != null);
            if (keyText != null) Hud.Set(key, keyText);
            Hud.Set(text, message);
            Hud.Show(this, true);
        }

        public void Hide() => Hud.Show(this, false);
    }

    /// <summary>Faixa vermelha grande no meio da tela (navio afundou, derrotado).</summary>
    class HudBanner : VisualElement
    {
        readonly Label title, subtitle;

        public HudBanner()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-banner");
            title = Hud.Text("", "hud-banner__title");
            subtitle = Hud.Text("", "hud-banner__subtitle");
            Add(title);
            Add(subtitle);
            style.display = DisplayStyle.None;
        }

        public void Show(string titleText, string subtitleText = null)
        {
            Hud.Set(title, titleText);
            Hud.Show(subtitle, !string.IsNullOrEmpty(subtitleText));
            if (!string.IsNullOrEmpty(subtitleText)) Hud.Set(subtitle, subtitleText);
            Hud.Show(this, true);
        }

        public void Hide() => Hud.Show(this, false);
    }

    /// <summary>
    /// Rótulos presos a pontos do mundo (nome do porto, baú, destino da missão). A cada quadro: Begin, um Place por
    /// rótulo, End (esconde os que sobraram). As peças são reaproveitadas, nada é criado depois do primeiro uso.
    /// </summary>
    class WorldLabelPool
    {
        readonly VisualElement layer;
        readonly string className;
        readonly List<Label> labels = new();
        readonly List<Color> colors = new();
        int used;
        Camera cam;

        public WorldLabelPool(VisualElement worldLayer, string extraClass = null)
        {
            layer = worldLayer;
            className = extraClass;
        }

        public bool Begin()
        {
            used = 0;
            cam = Camera.main;
            return cam && layer.panel != null;
        }

        /// <summary>Põe um rótulo sobre <paramref name="world"/>; devolve false se o ponto está atrás da câmera.</summary>
        public bool Place(Vector3 world, string text, Color color)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return false;
            PlaceScreen(new Vector2(screen.x, Screen.height - screen.y), text, color);
            return true;
        }

        /// <summary>Ponto da tela com origem no canto de cima (como o IMGUI).</summary>
        public Label PlaceScreen(Vector2 screenTopLeft, string text, Color color)
        {
            Vector2 p = RuntimePanelUtils.ScreenToPanel(layer.panel, screenTopLeft);
            if (used == labels.Count)
            {
                var label = Hud.Text("", "hud-world-label");
                if (className != null) label.AddToClassList(className);
                layer.Add(label);
                labels.Add(label);
                colors.Add(Color.clear);
            }
            var l = labels[used];
            Hud.Set(l, text);
            if (colors[used] != color)
            {
                colors[used] = color;
                l.style.color = color;
            }
            l.style.left = p.x;
            l.style.top = p.y;
            if (l.style.display == DisplayStyle.None) l.style.display = DisplayStyle.Flex;
            used++;
            return l;
        }

        public void End()
        {
            for (int i = used; i < labels.Count; i++)
                if (labels[i].style.display != DisplayStyle.None) labels[i].style.display = DisplayStyle.None;
        }

        public void HideAll()
        {
            used = 0;
            End();
        }
    }

    static class HudColors
    {
        public static readonly Color Cream = new(0.97f, 0.91f, 0.79f);
        public static readonly Color Gold = new(1f, 0.83f, 0.42f);
        public static readonly Color Ink = new(0.23f, 0.14f, 0.08f);
        public static readonly Color Good = new(0.42f, 0.74f, 0.33f);
        public static readonly Color Warn = new(0.95f, 0.72f, 0.2f);
        public static readonly Color Bad = new(0.86f, 0.27f, 0.2f);
        public static readonly Color Muted = new(0.82f, 0.8f, 0.76f);
    }
}
