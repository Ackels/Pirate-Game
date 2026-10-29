using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Fileira de filtros por grupo de item (H4, 28/10, pedido do Jhonata: "poder filtrar por recursos, munições, armas" no mercado e no
    /// armazém, para não rodar a lista inteira). Mostra só os grupos que têm algo na lista, com a quantidade; o escolhido fica de uma
    /// abertura para a outra e volta a "Tudo" quando o grupo dele esvazia.
    /// </summary>
    class ItemFilter
    {
        static readonly (ItemGroup group, string name)[] Groups =
        {
            (ItemGroup.Auto, "Tudo"),
            (ItemGroup.Resource, "Recursos"),
            (ItemGroup.Ammo, "Munição"),
            (ItemGroup.Food, "Comida e pesca"),
            (ItemGroup.Weapon, "Armas"),
            (ItemGroup.Tool, "Ferramentas"),
            (ItemGroup.Schematic, "Esquemas"),
            (ItemGroup.Treasure, "Tesouros"),
            (ItemGroup.Crew, "Tripulação"),
        };

        readonly int[] counts = new int[Groups.Length];

        /// <summary>Auto = tudo.</summary>
        public ItemGroup Selected { get; set; }

        public long Signature => (long)Selected;

        public bool Shows(ItemDefinition item) => Selected == ItemGroup.Auto || item && item.Group == Selected;

        public bool Shows(ItemGroup group) => Selected == ItemGroup.Auto || group == Selected;

        /// <summary>
        /// Monta a fileira para estes itens (um por linha da lista; repetidos contam de novo). <paramref name="extraWeapons"/> = armas que
        /// não são item (a lâmina de começo da tela I).
        /// </summary>
        public void Build(VisualElement parent, IReadOnlyList<ItemDefinition> items, int extraWeapons = 0)
        {
            System.Array.Clear(counts, 0, counts.Length);
            counts[0] = extraWeapons;
            counts[IndexOf(ItemGroup.Weapon)] = extraWeapons;
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i]) continue;
                counts[0]++;
                int g = IndexOf(items[i].Group);
                if (g > 0) counts[g]++;
            }
            int selected = IndexOf(Selected);
            if (selected > 0 && counts[selected] == 0) Selected = ItemGroup.Auto;

            var row = Hud.Row("hud-filters");
            row.Add(Hud.Text("Filtrar:", "hud-filters__label"));
            for (int i = 0; i < Groups.Length; i++)
            {
                if (i > 0 && counts[i] == 0) continue;
                var group = Groups[i].group;
                var button = Win.Button($"{Groups[i].name}  <size=80%>{counts[i]}</size>", () => Selected = group, extraClass: "hud-filter");
                button.EnableInClassList("hud-filter--on", Selected == group);
                row.Add(button);
            }
            parent.Add(row);
        }

        static int IndexOf(ItemGroup group)
        {
            for (int i = 0; i < Groups.Length; i++)
                if (Groups[i].group == group) return i;
            return -1;
        }
    }
}
