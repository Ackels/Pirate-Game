using System;
using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Janela de saque com o mouse (UI Toolkit, H4 passo 5): em cima o prêmio raro em destaque (esquema ou equipamento, com Pegar); embaixo
    /// a fonte (baú ou destroços) e o destino (mochila ou porão) em grades de ícones como o armazém; clique escolhe e o item aparece num
    /// quadro com Pegar / Deixar (1, 10, Tudo); clique duplo leva tudo para o outro lado. Rodapé: pegar tudo que couber e fechar.
    /// Usada pelo LandLootHUD (baú) e pelo LootHUD (destroços); só remonta quando algum dos dois lados ou a escolha muda.
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

        const string Bad = "#a8321f";

        readonly HudWindow window;
        readonly Setup setup;
        ItemDefinition selected;
        bool wasOpen;

        public LootWindow(VisualElement layer, Setup windowSetup)
        {
            setup = windowSetup;
            window = new HudWindow(layer, "hud-window--loot");
            var prizes = Hud.Box("hud-loot-prizes");
            window.Body.Add(prizes);
            window.AddPane(prizes, Signature, BuildPrizes);
            var columns = Hud.Row("hud-window__columns");
            window.Body.Add(columns);
            window.AddPane(Win.Scroll(Win.Column(columns)), Signature, col => Grid(col, true));
            window.AddPane(Win.Scroll(Win.Column(columns)), Signature, col => Grid(col, false));
            var detail = Win.Section(window.Body, "hud-cargo-detail");
            window.AddPane(detail, Signature, BuildDetail);

            window.Footer.Add(Win.Button("Pegar tudo que couber", () => setup.takeAll()));
            Win.Spacer(window.Footer);
            window.Footer.Add(Hud.Text($"clique escolhe · clique duplo leva tudo para o outro lado · \"{setup.giveBack}\" {setup.giveBackHint}",
                "hud-window__hint"));
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[F]</b>", () => setup.close()));
        }

        /// <summary>A cada quadro: mostra/esconde e atualiza o que mudou.</summary>
        public void Update(bool open, string title)
        {
            if (open && !wasOpen) selected = null;   // outro baú: começa sem escolha
            wasOpen = open;
            window.Show(open);
            if (!open) return;
            window.SetTitle(title, "clique para mover os itens");
            window.RefreshAll();
        }

        long Signature()
        {
            long hash = Win.Mix(Win.Of(setup.target()), selected);
            var contents = setup.contents();
            if (contents == null) return hash;
            for (int i = 0; i < contents.Count; i++)
            {
                hash = Win.Mix(hash, contents[i].item);
                hash = Win.Mix(hash, contents[i].amount);
            }
            return hash;
        }

        int SourceCount(ItemDefinition item)
        {
            var contents = setup.contents();
            if (contents == null) return 0;
            int count = 0;
            for (int i = 0; i < contents.Count; i++)
                if (contents[i].item == item) count += contents[i].amount;
            return count;
        }

        /// <summary>Prêmio que merece destaque: esquema ou equipamento raro para cima.</summary>
        static bool IsPrize(ItemDefinition item) => item.IsUnique && item.rarity >= ItemRarity.Rare;

        static string Kind(ItemDefinition item) =>
            item.equipSlot != EquipSlot.None ? Rarity.SlotName(item.equipSlot).ToLowerInvariant()
            : item.category == ItemCategory.Schematic ? "esquema de construção"
            : item.category == ItemCategory.Gear ? "equipamento"
            : item.Group switch
            {
                ItemGroup.Ammo => "munição",
                ItemGroup.Food => "comida",
                ItemGroup.Treasure => "tesouro",
                ItemGroup.Crew => "tripulação",
                ItemGroup.Tool => "ferramenta",
                ItemGroup.Weapon => "arma",
                _ => "recurso",
            };

        /// <summary>Por que não dá para pegar (vazio = dá).</summary>
        string TakeBlock(ItemDefinition item, Inventory target)
        {
            if (target.MaxFit(item) > 0) return "";
            return item.IsUnique ? "você já tem"
                : item.category == ItemCategory.Crew ? $"<color={Bad}>beliches cheios</color>"
                : $"<color={Bad}>não cabe</color>";
        }

        void BuildPrizes(VisualElement box)
        {
            var contents = setup.contents();
            var target = setup.target();
            int shown = 0;
            if (contents != null)
                for (int i = 0; i < contents.Count; i++)
                {
                    var item = contents[i].item;
                    if (!IsPrize(item)) continue;
                    shown++;
                    var card = Hud.Row("hud-loot-prize");
                    card.pickingMode = PickingMode.Position;
                    var color = Rarity.ColorOf(item.rarity);
                    card.style.borderTopColor = color;
                    card.style.borderBottomColor = color;
                    card.style.borderLeftColor = color;
                    card.style.borderRightColor = color;
                    card.Add(Win.Icon(item, "hud-item__slot--big"));
                    var info = Hud.Box("hud-loot-prize__info");
                    card.Add(info);
                    info.Add(Hud.Text($"<color={Win.Ink(item.rarity)}>{Rarity.NameOf(item.rarity).ToUpperInvariant()}</color>  ·  {Kind(item)}",
                        "hud-loot-prize__tag"));
                    info.Add(Hud.Text($"<color={Win.Ink(item.rarity)}><b>{item.displayName}</b></color>", "hud-window__label", "hud-window__text"));
                    if (!string.IsNullOrEmpty(item.description))
                        info.Add(Hud.Text($"<i>{item.description}</i>", "hud-window__small", "hud-window__muted", "hud-window__text"));
                    string block = TakeBlock(item, target);
                    int amount = contents[i].amount;
                    if (block.Length > 0) card.Add(Hud.Text(block, "hud-item__note"));
                    else card.Add(Win.Button("Pegar", () => setup.take(item, amount)));
                    card.RegisterCallback<ClickEvent>(_ => selected = item);
                    box.Add(card);
                }
            box.style.display = shown > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Uma coluna: título, espaço (no destino) e a grade de ícones.</summary>
        void Grid(VisualElement col, bool source)
        {
            var target = setup.target();
            Win.Text(col, source ? $"{setup.sourceTitle}   <size=85%>pegar →</size>" : $"{setup.targetTitle}   <size=85%>← {setup.giveBack.ToLowerInvariant()}</size>",
                "hud-window__subtitle-strong");
            if (!source) Win.Capacity(col, target);
            var grid = Hud.Box("hud-equip__grid", "hud-cargo__grid");
            col.Add(grid);
            if (source)
            {
                var contents = setup.contents();
                if (contents == null || contents.Count == 0)
                {
                    Win.Text(col, setup.emptySource, "hud-window__empty");
                    return;
                }
                for (int i = 0; i < contents.Count; i++)
                {
                    var item = contents[i].item;
                    int amount = contents[i].amount;
                    var cell = Win.Cell(item, amount, item == selected, clicks =>
                    {
                        selected = item;
                        if (clicks >= 2) setup.take(item, amount);
                    });
                    if (TakeBlock(item, target).Length > 0) cell.AddToClassList("hud-loot__cell--blocked");
                    grid.Add(cell);
                }
                return;
            }
            if (target.Items.Count == 0) Win.Text(col, setup.emptyTarget, "hud-window__empty");
            for (int i = 0; i < target.Items.Count; i++)
            {
                var item = target.Items[i];
                int count = target.Count(item);
                grid.Add(Win.Cell(item, count, item == selected, clicks =>
                {
                    selected = item;
                    if (clicks >= 2 && !item.IsUnique) setup.discard(item, count);
                }));
            }
        }

        /// <summary>O item escolhido: ícone grande, nome, quanto tem em cada lado e os botões (ou o porquê de não dar).</summary>
        void BuildDetail(VisualElement box)
        {
            var target = setup.target();
            var item = selected;
            int inSource = item ? SourceCount(item) : 0, inTarget = item ? target.Count(item) : 0;
            if (!item || inSource + inTarget == 0)
            {
                Win.Text(box, $"Clique num item para ver e mover. \"Pegar tudo que couber\" leva o que der para {setup.targetTitle.ToLowerInvariant()}.",
                    "hud-window__empty");
                return;
            }
            var row = Win.Row(box, "hud-cargo-detail__row");
            row.Add(Win.Icon(item, "hud-item__slot--big"));
            var info = Hud.Box("hud-cargo-detail__info");
            row.Add(info);
            info.Add(Hud.Text($"<color={Win.Ink(item.rarity)}><b>{item.displayName}</b></color>   <size=85%>{Rarity.NameOf(item.rarity)} · {Kind(item)}</size>",
                "hud-window__label", "hud-window__text"));
            info.Add(Hud.Text($"{setup.sourceTitle.ToLowerInvariant()} <b>{inSource}</b>   ·   {setup.targetTitle.ToLowerInvariant()} <b>{inTarget}</b>" +
                              (item.spacePerUnit > 0f ? $"   ·   <size=85%>{item.spacePerUnit:0.##} de espaço cada</size>" : ""),
                "hud-window__small", "hud-window__text"));
            if (!string.IsNullOrEmpty(item.description))
                info.Add(Hud.Text($"<i>{item.description}</i>", "hud-window__small", "hud-window__muted", "hud-window__text"));

            var actions = Hud.Box("hud-cargo-detail__actions");
            row.Add(actions);
            var take = Win.Row(actions, "hud-cargo-detail__line");
            take.Add(Hud.Text("Pegar", "hud-cargo-detail__label"));
            string block = TakeBlock(item, target);
            if (inSource == 0) take.Add(Hud.Text("nada sobrou aqui", "hud-item__note"));
            else if (block.Length > 0) take.Add(Hud.Text(block, "hud-item__note"));
            else Buttons(take, Mathf.Min(inSource, target.MaxFit(item)), inSource, n => setup.take(item, n), "→", after: true);

            var back = Win.Row(actions, "hud-cargo-detail__line");
            back.Add(Hud.Text(setup.giveBack, "hud-cargo-detail__label"));
            if (inTarget == 0) back.Add(Hud.Text("nada pego", "hud-item__note"));
            else if (item.IsUnique) back.Add(Hud.Text(item.equipSlot != EquipSlot.None ? "equipamento (tecla I)" : "esquema guardado", "hud-item__note"));
            else Buttons(back, inTarget, inTarget, n => setup.discard(item, n), "←", after: false);
        }

        /// <summary>1 / 10 / Tudo; "Tudo" vira o número quando só cabe parte; com um só, um botão.</summary>
        static void Buttons(VisualElement row, int fit, int all, Func<int, int> move, string arrow, bool after)
        {
            string Label(string n) => after ? $"{n} {arrow}" : $"{arrow} {n}";
            row.Add(Win.Button(Label("1"), () => move(1), extraClass: "hud-button--small"));
            if (fit <= 1) return;
            if (fit > 10) row.Add(Win.Button(Label("10"), () => move(10), extraClass: "hud-button--small"));
            row.Add(Win.Button(Label(fit < all ? fit.ToString() : "Tudo"), () => move(fit), extraClass: "hud-button--small"));
        }
    }
}
