using System;
using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Janela grande do HUD (UI Toolkit) no meio da tela: moldura de madeira escura com o título em dourado (como os cartões do HUD) e,
    /// dentro, o pergaminho com o corpo e o rodapé de botões (28/10, pedido do Jhonata: "deixar mais no estilo pirata"). Fica na camada
    /// de janelas do <see cref="GameHud"/> e recebe o mouse (o resto do HUD deixa o clique passar para o jogo).
    /// O corpo é feito de <see cref="Pane"/>s: cada um se remonta só quando a "assinatura" dos dados que ele mostra muda.
    /// </summary>
    class HudWindow
    {
        public readonly VisualElement Root;
        /// <summary>O pergaminho dentro da moldura (corpo + rodapé).</summary>
        public readonly VisualElement Paper;
        public readonly VisualElement Body;
        public readonly VisualElement Footer;
        readonly Label title, subtitle;
        readonly List<Pane> panes = new();

        public HudWindow(VisualElement layer, string extraClass = null)
        {
            Root = Hud.Box("hud-window");
            if (extraClass != null) Root.AddToClassList(extraClass);
            Root.pickingMode = PickingMode.Position;
            var header = Hud.Box("hud-window__header");
            title = Hud.Text("", "hud-window__title");
            subtitle = Hud.Text("", "hud-window__subtitle");
            header.Add(title);
            header.Add(subtitle);
            Root.Add(header);
            Paper = Hud.Box("hud-window__paper");
            Paper.pickingMode = PickingMode.Position;
            Root.Add(Paper);
            Body = Hud.Box("hud-window__body");
            Paper.Add(Body);
            Footer = Hud.Box("hud-window__footer");
            Paper.Add(Footer);
            Root.style.display = DisplayStyle.None;
            layer.Add(Root);
        }

        public bool IsVisible => Root.style.display != DisplayStyle.None;

        public void SetTitle(string text, string sub = null)
        {
            Hud.Set(title, text);
            Hud.Show(subtitle, !string.IsNullOrEmpty(sub));
            if (!string.IsNullOrEmpty(sub)) Hud.Set(subtitle, sub);
        }

        public void Show(bool visible)
        {
            if (visible == IsVisible) return;
            Hud.Show(Root, visible);
            if (visible) RefreshAll(force: true);
        }

        public Pane AddPane(VisualElement parent, Func<long> signature, Action<VisualElement> build)
        {
            var pane = new Pane(parent, signature, build);
            panes.Add(pane);
            return pane;
        }

        /// <summary>Esvazia corpo e rodapé (troca de casa ou de aba): os painéis antigos deixam de existir.</summary>
        public void ClearContent()
        {
            panes.Clear();
            Body.Clear();
            Footer.Clear();
        }

        /// <summary>A cada quadro com a janela aberta: remonta só o que mudou.</summary>
        public void RefreshAll(bool force = false)
        {
            for (int i = 0; i < panes.Count; i++) panes[i].Refresh(force);
        }
    }

    /// <summary>Pedaço de janela que se remonta inteiro quando a assinatura muda (inventário, níveis, obra, tempo).</summary>
    class Pane
    {
        public readonly VisualElement Root;
        readonly Func<long> signature;
        readonly Action<VisualElement> build;
        long last;
        bool built;

        public Pane(VisualElement root, Func<long> signature, Action<VisualElement> build)
        {
            Root = root;
            this.signature = signature;
            this.build = build;
        }

        public void Refresh(bool force)
        {
            long now = signature();
            if (built && !force && now == last) return;
            built = true;
            last = now;
            Root.Clear();
            build(Root);
        }
    }

    /// <summary>Peças das janelas (botões, seções, linhas de item). Só são criadas quando um painel se remonta.</summary>
    static class Win
    {
        public static Button Button(string text, Action onClick, bool enabled = true, string extraClass = null)
        {
            var button = new Button(onClick) { text = text, focusable = false, enableRichText = true };
            button.AddToClassList("hud-button");
            if (extraClass != null) button.AddToClassList(extraClass);
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>Caixa clara dentro do pergaminho (um upgrade, um contrato, a obra em andamento).</summary>
        public static VisualElement Section(VisualElement parent, string extraClass = null)
        {
            var box = Hud.Box("hud-section");
            if (extraClass != null) box.AddToClassList(extraClass);
            box.pickingMode = PickingMode.Position;
            parent.Add(box);
            return box;
        }

        public static Label Text(VisualElement parent, string text, params string[] classes)
        {
            var label = Hud.Text(text, classes);
            label.AddToClassList("hud-window__text");
            parent.Add(label);
            return label;
        }

        public static VisualElement Row(VisualElement parent, string extraClass = null)
        {
            var row = Hud.Row(extraClass ?? "hud-window__row");
            parent.Add(row);
            return row;
        }

        public static VisualElement Column(VisualElement parent, string extraClass = null)
        {
            var col = Hud.Box("hud-window__column");
            if (extraClass != null) col.AddToClassList(extraClass);
            col.pickingMode = PickingMode.Position;
            parent.Add(col);
            return col;
        }

        public static void Spacer(VisualElement parent)
        {
            var space = Hud.Box("hud-spacer");
            parent.Add(space);
        }

        public static ScrollView Scroll(VisualElement parent)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("hud-scroll");
            parent.Add(scroll);
            return scroll;
        }

        /// <summary>Nome do item com o ícone dele num encaixe escuro com a borda na cor da raridade (sem ícone, a cor e a inicial).</summary>
        public static VisualElement Item(VisualElement parent, ItemDefinition item, string text)
        {
            var row = Hud.Row("hud-item");
            row.Add(Icon(item));
            row.Add(Hud.Text(text, "hud-item__name"));
            parent.Add(row);
            return row;
        }

        /// <summary>Encaixe do ícone do item (o mesmo jeito da mochila no HUD e da tela de inventário).</summary>
        public static VisualElement Icon(ItemDefinition item, string extraClass = null)
        {
            var slot = Hud.Box("hud-item__slot");
            if (extraClass != null) slot.AddToClassList(extraClass);
            var icon = Hud.Box("hud-item__icon");
            if (item.icon) icon.style.backgroundImage = new StyleBackground(item.icon);
            else
            {
                icon.AddToClassList("hud-item__icon--swatch");
                icon.style.backgroundColor = item.color;
                icon.Add(Hud.Text(item.displayName.Substring(0, 1), "hud-item__letter"));
            }
            slot.Add(icon);
            var border = item.rarity >= ItemRarity.Uncommon ? Rarity.ColorOf(item.rarity) : new Color(0.43f, 0.29f, 0.18f);
            slot.style.borderTopColor = border;
            slot.style.borderBottomColor = border;
            slot.style.borderLeftColor = border;
            slot.style.borderRightColor = border;
            return slot;
        }

        /// <summary>
        /// Casinha de grade (a da tela de inventário): ícone, moldura da raridade, quantidade no canto. <paramref name="clicked"/> recebe o
        /// número de cliques (2 = clique duplo).
        /// </summary>
        public static VisualElement Cell(ItemDefinition item, int count, bool selected, Action<int> clicked, Texture2D icon = null)
        {
            var cell = Hud.Box("hud-equip__cell");
            cell.pickingMode = PickingMode.Position;
            if (item && item.rarity >= ItemRarity.Uncommon)
            {
                var c = Rarity.ColorOf(item.rarity);
                cell.style.borderTopColor = c;
                cell.style.borderBottomColor = c;
                cell.style.borderLeftColor = c;
                cell.style.borderRightColor = c;
            }
            if (selected) cell.AddToClassList("hud-equip__cell--selected");
            cell.Add(CellIcon(icon ? icon : item ? item.icon : null, item));
            if (count > 1) cell.Add(Hud.Text(count >= 1000 ? $"{count / 1000f:0.#}k" : count.ToString(), "hud-equip__count"));
            if (clicked != null) cell.RegisterCallback<ClickEvent>(e => clicked(e.clickCount));
            return cell;
        }

        /// <summary>O ícone dentro da casinha (sem ícone: a cor do item numa bolinha com a inicial).</summary>
        public static VisualElement CellIcon(Texture2D icon, ItemDefinition item)
        {
            var box = Hud.Box("hud-equip__icon");
            box.pickingMode = PickingMode.Ignore;
            if (icon) box.style.backgroundImage = new StyleBackground(icon);
            else if (item)
            {
                box.style.backgroundColor = item.color;
                box.AddToClassList("hud-equip__icon--swatch");
                var letter = Hud.Text(item.displayName.Substring(0, 1), "hud-equip__initial");
                letter.pickingMode = PickingMode.Ignore;
                box.Add(letter);
            }
            return box;
        }

        /// <summary>Cor da raridade para texto no pergaminho (as do HUD são claras demais no papel).</summary>
        public static string Ink(ItemRarity rarity) => "#" + ColorUtility.ToHtmlStringRGB(rarity switch
        {
            ItemRarity.Uncommon => new Color(0.2f, 0.48f, 0.16f),
            ItemRarity.Rare => new Color(0.16f, 0.36f, 0.7f),
            ItemRarity.Epic => new Color(0.46f, 0.22f, 0.68f),
            ItemRarity.Legendary => new Color(0.72f, 0.46f, 0.04f),
            _ => new Color(0.35f, 0.25f, 0.16f),
        });

        /// <summary>Plaquinha de preço: a moeda de ouro e o valor.</summary>
        public static VisualElement Price(Texture2D coin, int amount, string extraClass = null)
        {
            var chip = Hud.Row("hud-price");
            if (extraClass != null) chip.AddToClassList(extraClass);
            var icon = Hud.Box("hud-price__coin");
            if (coin) icon.style.backgroundImage = new StyleBackground(coin);
            chip.Add(icon);
            chip.Add(Hud.Text(amount.ToString(), "hud-price__value"));
            return chip;
        }

        public static void Capacity(VisualElement parent, Inventory inventory)
        {
            var row = Hud.Row("hud-window__row");
            var bar = new HudBar("hud-bar--grow");
            row.Add(bar);
            var text = Hud.Text(inventory.Unlimited ? $"{inventory.UsedSpace:0.#} (sem limite)" : $"{inventory.UsedSpace:0.#} / {inventory.Capacity:0}",
                "hud-cargo__capacity");
            row.Add(text);
            if (inventory.Unlimited) bar.style.display = DisplayStyle.None;
            else bar.SetDanger(inventory.UsedSpace / Mathf.Max(0.01f, inventory.Capacity), greenWhenFull: false);
            parent.Add(row);
        }

        public static void Bar(VisualElement parent, float fraction, bool greenWhenFull = true)
        {
            var bar = new HudBar();
            bar.SetDanger(fraction, greenWhenFull);
            parent.Add(bar);
        }

        /// <summary>Bolinhas de nível: douradas as compradas.</summary>
        public static VisualElement Pips(int level, int max)
        {
            var pips = Hud.Box("hud-pips", "hud-pips--small");
            for (int i = 0; i < max; i++)
            {
                var pip = Hud.Box("hud-pip");
                if (i < level) pip.AddToClassList("hud-pip--on");
                pips.Add(pip);
            }
            return pips;
        }

        public static string Time(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return s >= 60 ? $"{s / 60}:{s % 60:00}" : $"{s} s";
        }

        public static string Stacks(ItemStack[] stacks)
        {
            var parts = new List<string>();
            foreach (var s in stacks) if (s.item) parts.Add($"{s.amount} {s.item.displayName.ToLowerInvariant()}");
            return parts.Count > 0 ? string.Join(", ", parts) : "nada";
        }

        /// <summary>Junta números numa assinatura (ordem importa).</summary>
        public static long Mix(long hash, long value) => unchecked(hash * 31 + value);

        public static long Mix(long hash, UnityEngine.Object obj) => unchecked(hash * 31 + (obj ? obj.GetHashCode() : 0));

        public static long Of(Inventory inventory) => inventory ? unchecked(inventory.Version * 7919L + inventory.GetHashCode()) : 0;
    }
}
