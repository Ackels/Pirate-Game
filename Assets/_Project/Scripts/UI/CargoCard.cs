using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Mochila (a pé) ou porão (no navio) no canto de cima à direita. Refeito em 21/10 a pedido do Jhonata ("tá muito simplório"), no
    /// jeito dos encaixes da HUD a pé: placa de madeira com o nome, o espaço usado ("3,4 / 15", vermelho quando enche), a tecla I e uma
    /// grade de casinhas com o ícone de cada item (os da tela I; sem ícone, a cor e a inicial), a quantidade no canto e a moldura na cor
    /// da raridade. Os nomes e os detalhes ficam na aba Inventário. Só remonta quando o inventário muda (Inventory.Version).
    /// </summary>
    class CargoCard
    {
        public readonly VisualElement Root;
        readonly Label title, capacity, empty;
        readonly HudBar bar;
        readonly VisualElement grid;
        readonly List<Slot> slots = new();
        Inventory shown;
        int shownVersion = -1;
        string shownTitle, shownEmpty;

        /// <summary>Uma casinha da grade; guarda o que mostra para só mexer no que mudou.</summary>
        class Slot
        {
            public readonly VisualElement root, icon;
            readonly Label count, letter;
            ItemDefinition item;
            int amount = -1;

            public Slot(VisualElement parent)
            {
                root = Hud.Box("hud-bag__slot");
                icon = Hud.Box("hud-bag__icon");
                letter = Hud.Text("", "hud-bag__letter");
                icon.Add(letter);
                root.Add(icon);
                count = Hud.Text("", "hud-bag__count");
                root.Add(count);
                parent.Add(root);
            }

            public void Set(ItemDefinition value, int n)
            {
                Hud.Show(root, true);
                if (value != item)
                {
                    item = value;
                    var tex = value.icon;
                    icon.style.backgroundImage = tex ? new StyleBackground(tex) : new StyleBackground((Texture2D)null);
                    icon.style.backgroundColor = tex ? new StyleColor(Color.clear) : new StyleColor(value.color);
                    icon.EnableInClassList("hud-bag__icon--swatch", !tex);
                    Hud.Show(letter, !tex);
                    if (!tex) Hud.Set(letter, value.displayName.Substring(0, 1));
                    var border = value.rarity >= ItemRarity.Uncommon ? Rarity.ColorOf(value.rarity) : new Color(0.43f, 0.29f, 0.18f);
                    root.style.borderTopColor = border;
                    root.style.borderBottomColor = border;
                    root.style.borderLeftColor = border;
                    root.style.borderRightColor = border;
                    root.EnableInClassList("hud-bag__slot--rare", value.rarity >= ItemRarity.Uncommon);
                }
                if (n != amount)
                {
                    amount = n;
                    Hud.Show(count, n > 1);
                    Hud.Set(count, n >= 1000 ? $"{n / 1000f:0.#}k" : n.ToString());
                }
            }
        }

        public CargoCard(VisualElement parent)
        {
            Root = Hud.Box("hud-card", "hud-card--wood", "hud-bag");
            var head = Hud.Row("hud-bag__head");
            title = Hud.Text("", "hud-bag__title");
            head.Add(title);
            capacity = Hud.Text("", "hud-bag__capacity");
            head.Add(capacity);
            head.Add(Hud.Key("I"));
            Root.Add(head);
            bar = new HudBar("hud-bag__bar");
            Root.Add(bar);
            grid = Hud.Box("hud-bag__grid");
            Root.Add(grid);
            empty = Hud.Text("", "hud-bag__empty");
            Root.Add(empty);
            Root.style.display = DisplayStyle.None;
            // Embaixo da plaquinha da fama e do relógio, seja quem for montado primeiro (o relógio se põe logo depois da fama).
            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent[i].ClassListContains("hud-fame-chip") || parent[i].ClassListContains("hud-clock")) index = i + 1;
            parent.Insert(index, Root);
        }

        public void Hide() => Hud.Show(Root, false);

        public void Show(string name, Inventory inventory, string emptyText)
        {
            Hud.Show(Root, true);
            if (ReferenceEquals(inventory, shown) && inventory.Version == shownVersion
                && ReferenceEquals(name, shownTitle) && ReferenceEquals(emptyText, shownEmpty)) return;
            shown = inventory;
            shownVersion = inventory.Version;
            shownTitle = name;
            shownEmpty = emptyText;

            Hud.Set(title, name);
            float used = inventory.UsedSpace;
            bool full = !inventory.Unlimited && used >= inventory.Capacity - 0.01f;
            Hud.Show(bar, !inventory.Unlimited);
            if (!inventory.Unlimited) bar.SetDanger(used / Mathf.Max(0.01f, inventory.Capacity), greenWhenFull: false);
            Hud.Set(capacity, inventory.Unlimited ? $"{used:0.#}" : full ? $"{(name == "Mochila" ? "cheia" : "cheio")}  {used:0.#} / {inventory.Capacity:0}" : $"{used:0.#} / {inventory.Capacity:0}");
            Root.EnableInClassList("hud-bag--full", full);

            int n = 0;
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                var item = inventory.Items[i];
                if (!item) continue;
                if (n == slots.Count) slots.Add(new Slot(grid));
                slots[n++].Set(item, inventory.Count(item));
            }
            for (int i = n; i < slots.Count; i++) Hud.Show(slots[i].root, false);
            Hud.Show(grid, n > 0);
            Hud.Show(empty, n == 0);
            Hud.Set(empty, emptyText);
        }
    }
}
