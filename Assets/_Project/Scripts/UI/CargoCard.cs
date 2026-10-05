using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Cartão de pergaminho com um inventário (porão ou mochila): espaço usado com barra e a lista de itens (cor do item
    /// num quadradinho, nome, quantidade) e os esquemas. Só remonta quando o inventário muda (Inventory.Version).
    /// </summary>
    class CargoCard
    {
        public readonly VisualElement Root;
        readonly Label title, capacity, empty, schematicsTitle;
        readonly HudBar bar;
        readonly VisualElement list, schematics;
        readonly List<(VisualElement row, VisualElement swatch, Label name, Label count)> rows = new();
        readonly List<Label> schematicRows = new();
        Inventory shown;
        int shownVersion = -1;
        string shownTitle, shownEmpty;

        public CargoCard(VisualElement parent)
        {
            Root = Hud.Card(paper: true);
            Root.AddToClassList("hud-cargo");
            title = Hud.Text("", "hud-card__title");
            Root.Add(title);
            var capRow = Hud.Row();
            bar = new HudBar("hud-bar--grow");
            capRow.Add(bar);
            capacity = Hud.Text("", "hud-cargo__capacity");
            capRow.Add(capacity);
            Root.Add(capRow);
            empty = Hud.Text("", "hud-cargo__empty");
            Root.Add(empty);
            list = Hud.Box("hud-cargo__list");
            Root.Add(list);
            schematics = Hud.Box("hud-cargo__schematics");
            schematicsTitle = Hud.Text("Esquemas", "hud-cargo__subtitle");
            schematics.Add(schematicsTitle);
            Root.Add(schematics);
            Root.style.display = DisplayStyle.None;
            // Sempre no alto da coluna (as missões vêm embaixo), seja quem for montado primeiro.
            parent.Insert(0, Root);
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
            Hud.Show(bar, !inventory.Unlimited);
            if (!inventory.Unlimited) bar.SetDanger(used / UnityEngine.Mathf.Max(0.01f, inventory.Capacity), greenWhenFull: false);
            Hud.Set(capacity, inventory.Unlimited ? $"{used:0.#} (sem limite)" : $"{used:0.#} / {inventory.Capacity:0}");

            int resources = 0, schematicCount = 0;
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                var item = inventory.Items[i];
                if (item.category == ItemCategory.Schematic)
                {
                    if (schematicCount == schematicRows.Count)
                    {
                        var label = Hud.Text("", "hud-cargo__schematic");
                        schematics.Add(label);
                        schematicRows.Add(label);
                    }
                    Hud.Show(schematicRows[schematicCount], true);
                    Hud.Set(schematicRows[schematicCount], item.displayName);
                    schematicCount++;
                    continue;
                }
                if (resources == rows.Count)
                {
                    var row = Hud.Row("hud-cargo__row");
                    var swatch = Hud.Box("hud-swatch");
                    var n = Hud.Text("", "hud-cargo__name");
                    var c = Hud.Text("", "hud-cargo__count");
                    row.Add(swatch);
                    row.Add(n);
                    row.Add(c);
                    list.Add(row);
                    rows.Add((row, swatch, n, c));
                }
                var r = rows[resources++];
                Hud.Show(r.row, true);
                r.swatch.style.backgroundColor = item.color;
                Hud.Set(r.name, item.displayName);
                Hud.Set(r.count, inventory.Count(item).ToString());
            }
            for (int i = resources; i < rows.Count; i++) Hud.Show(rows[i].row, false);
            for (int i = schematicCount; i < schematicRows.Count; i++) Hud.Show(schematicRows[i], false);
            Hud.Show(schematics, schematicCount > 0);
            Hud.Show(empty, resources == 0 && schematicCount == 0);
            Hud.Set(empty, emptyText);
        }
    }
}
