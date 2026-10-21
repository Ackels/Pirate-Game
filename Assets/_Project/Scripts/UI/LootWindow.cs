using System;
using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Janela de saque com o mouse (UI Toolkit): à esquerda o que há na fonte (baú ou destroços) com +1 / +10 / Tudo,
    /// à direita o destino (mochila ou porão) com o espaço e -1 / -10 / devolver. Rodapé: pegar tudo que couber e fechar.
    /// Usada pelo LandLootHUD (baú) e pelo LootHUD (destroços); só remonta quando algum dos dois lados muda.
    /// </summary>
    class LootWindow
    {
        public struct Setup
        {
            public string sourceTitle, targetTitle, emptySource, emptyTarget, giveBack, giveBackHint;
            public Func<IReadOnlyList<ItemStack>> contents;
            public Func<Inventory> target;
            public Func<ItemDefinition, int, int> take, discard;
            public Action takeAll, close;
        }

        readonly HudWindow window;
        readonly Setup setup;

        public LootWindow(VisualElement layer, Setup windowSetup)
        {
            setup = windowSetup;
            window = new HudWindow(layer, "hud-window--loot");
            var columns = Hud.Row("hud-window__columns");
            window.Body.Add(columns);
            window.AddPane(Win.Column(columns), Signature, BuildSource);
            window.AddPane(Win.Column(columns), Signature, BuildTarget);

            window.Footer.Add(Win.Button("Pegar tudo que couber", () => setup.takeAll()));
            Win.Spacer(window.Footer);
            window.Footer.Add(Hud.Text($"\"{setup.giveBack}\" {setup.giveBackHint}", "hud-window__hint"));
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[F]</b>", () => setup.close()));
        }

        /// <summary>A cada quadro: mostra/esconde e atualiza o que mudou.</summary>
        public void Update(bool open, string title)
        {
            window.Show(open);
            if (!open) return;
            window.SetTitle(title, "clique para mover os itens");
            window.RefreshAll();
        }

        long Signature()
        {
            long hash = Win.Of(setup.target());
            var contents = setup.contents();
            if (contents == null) return hash;
            for (int i = 0; i < contents.Count; i++)
            {
                hash = Win.Mix(hash, contents[i].item);
                hash = Win.Mix(hash, contents[i].amount);
            }
            return hash;
        }

        void BuildSource(VisualElement col)
        {
            Win.Text(col, setup.sourceTitle, "hud-window__subtitle-strong");
            var contents = setup.contents();
            var target = setup.target();
            if (contents == null || contents.Count == 0)
            {
                Win.Text(col, setup.emptySource, "hud-window__empty");
                return;
            }
            for (int i = 0; i < contents.Count; i++)
            {
                var stack = contents[i];
                var item = stack.item;
                var row = Win.Row(col, "hud-window__item-row");
                var name = Win.Item(row, item, $"{item.displayName}  <b>×{stack.amount}</b>");
                name.Add(Hud.Text(item.equipSlot != EquipSlot.None || item.rarity >= ItemRarity.Rare && item.IsUnique
                    ? $"{Rarity.NameOf(item.rarity)} · {(item.equipSlot != EquipSlot.None ? Rarity.SlotName(item.equipSlot).ToLowerInvariant() : "esquema")}"
                    : HudDraw.SpaceText(item), "hud-item__note"));
                int fit = target.MaxFit(item);
                if (fit <= 0)
                {
                    row.Add(Hud.Text(item.IsUnique ? "já tem"
                        : item.category == ItemCategory.Crew ? "<color=#a8321f>beliches cheios</color>"
                        : "<color=#a8321f>não cabe</color>", "hud-item__note"));
                    continue;
                }
                int amount = stack.amount;
                row.Add(Win.Button("+1", () => setup.take(item, 1), extraClass: "hud-button--small"));
                if (amount > 10) row.Add(Win.Button("+10", () => setup.take(item, 10), extraClass: "hud-button--small"));
                row.Add(Win.Button(fit < amount ? $"+{fit}" : "Tudo", () => setup.take(item, amount), extraClass: "hud-button--small"));
            }
        }

        void BuildTarget(VisualElement col)
        {
            var target = setup.target();
            Win.Text(col, setup.targetTitle, "hud-window__subtitle-strong");
            Win.Capacity(col, target);
            if (target.Items.Count == 0) Win.Text(col, setup.emptyTarget, "hud-window__empty");
            for (int i = 0; i < target.Items.Count; i++)
            {
                var item = target.Items[i];
                int count = target.Count(item);
                var row = Win.Row(col, "hud-window__item-row");
                Win.Item(row, item, $"{item.displayName}  <b>×{count}</b>");
                if (item.IsUnique)
                {
                    row.Add(Hud.Text(item.equipSlot != EquipSlot.None ? "equipamento (tecla I)" : "esquema guardado", "hud-item__note"));
                    continue;
                }
                row.Add(Win.Button("-1", () => setup.discard(item, 1), extraClass: "hud-button--small"));
                if (count > 10) row.Add(Win.Button("-10", () => setup.discard(item, 10), extraClass: "hud-button--small"));
                row.Add(Win.Button(setup.giveBack, () => setup.discard(item, count), extraClass: "hud-button--small"));
            }
        }
    }
}
