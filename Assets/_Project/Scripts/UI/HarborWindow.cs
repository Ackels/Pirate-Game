using System;
using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Missions;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Janela do porto (UI Toolkit), com o mouse. A pé (cena 07), cada casa mostra só a parte dela: Armazém = carga (e a mochila do
    /// capitão, que se guarda direto no armazém);
    /// Estaleiro = conserto + upgrades do navio; Mestre de obras = construções da base; Taverna = contratar marinheiros +
    /// quadro de missões.
    /// No navio atracado (cena 05), tudo em abas. Cada pedaço só se remonta quando o que ele mostra muda (carga, níveis,
    /// obra, segundos da obra, missões).
    /// </summary>
    class HarborWindow
    {
        enum Tab { Cargo, Upgrades, Buildings, Missions, Market, Smithy }

        const string Bad = "#a8321f", Good = "#2f7a2a";

        readonly HudWindow window;
        readonly HarborVisitor visitor;
        readonly HarborBuildingVisitor building;
        readonly ShipUpgrades upgrades;
        readonly float partialRepair;
        Tab tab;
        long shownKey = long.MinValue;
        // Armazém: modo de descarte (itens que o mercado não compra e esquemas repetidos) e o botão esperando confirmação.
        bool discardMode;
        Inventory confirmInventory;
        ItemDefinition confirmItem;
        float confirmUntil;
        // Filtros por grupo (H4): o do armazém vale para o porão e o armazém juntos.
        readonly ItemFilter cargoFilter = new(), marketFilter = new();
        readonly List<ItemDefinition> filterItems = new();
        // Armazém em grade (H4, passo 2): o item escolhido mostra os botões embaixo.
        ItemDefinition cargoSelected;

        public HarborWindow(VisualElement layer, HarborVisitor shipVisitor, HarborBuildingVisitor onFoot, float partialRepairHull)
        {
            visitor = shipVisitor;
            building = onFoot;
            upgrades = visitor.GetComponent<ShipUpgrades>();
            partialRepair = partialRepairHull;
            window = new HudWindow(layer, "hud-window--harbor");
        }

        static string ServiceName(HarborService service) => service switch
        {
            HarborService.Shipyard => "upgrades do navio e conserto",
            HarborService.Construction => "construções da base",
            HarborService.Missions => "marinheiros e quadro de missões",
            HarborService.Market => "comprar e vender",
            HarborService.Smithy => "munições especiais",
            _ => "carga do porão",
        };

        /// <summary>A cada quadro. <paramref name="house"/> = casa aberta a pé; null = janela do navio (abas).</summary>
        public void Update(bool open, HarborBuilding house)
        {
            var dock = visitor.Current;
            open &= dock;
            window.Show(open);
            if (!open)
            {
                shownKey = long.MinValue;
                return;
            }
            long key = house ? Win.Mix(1, house) : 100 + (int)tab;
            if (key != shownKey)
            {
                shownKey = key;
                Rebuild(house, dock);
            }
            window.RefreshAll();
        }

        void Rebuild(HarborBuilding house, HarborDock dock)
        {
            window.ClearContent();
            var body = window.Body;
            var footer = window.Footer;
            Services.TryGet(out MissionBoard board);

            if (house)
            {
                window.SetTitle(house.DisplayName, ServiceName(house.Service));
                switch (house.Service)
                {
                    case HarborService.Cargo:
                        AddCargo(body, dock);
                        footer.Add(Win.Button("Descarregar tudo", visitor.UnloadAll));
                        break;
                    case HarborService.Shipyard:
                        AddRepair(body, dock.Settings);
                        if (upgrades) AddUpgrades(body, dock);
                        else Win.Text(body, "este navio não tem upgrades", "hud-window__empty");
                        break;
                    case HarborService.Construction:
                        if (dock.Buildings) AddBuildings(body, dock.Buildings);
                        else Win.Text(body, "este porto não tem construções", "hud-window__empty");
                        break;
                    case HarborService.Market:
                        AddMarket(body, dock.Settings);
                        break;
                    case HarborService.Smithy:
                        AddSmithy(body, dock);
                        break;
                    default:
                        AddRecruit(body);
                        if (board) AddMissions(body, board, dock.Warehouse);
                        else Win.Text(body, "nenhum contrato neste porto", "hud-window__empty");
                        break;
                }
                Win.Spacer(footer);
                footer.Add(Win.Button("Sair  <b>[F]</b>", building.Close));
                return;
            }

            window.SetTitle(dock.DisplayName, "clique para mover itens e comprar");
            var tabs = Hud.Row("hud-tabs");
            body.Add(tabs);
            AddTab(tabs, Tab.Cargo, "Carga e conserto");
            if (upgrades) AddTab(tabs, Tab.Upgrades, "Upgrades do navio");
            if (dock.Buildings) AddTab(tabs, Tab.Buildings, "Construções da base");
            if (board) AddTab(tabs, Tab.Missions, "Missões");
            bool market = dock.Settings && dock.Settings.market.Length > 0, smithy = dock.Settings && dock.Settings.smithy.Length > 0;
            if (market) AddTab(tabs, Tab.Market, "Mercado");
            if (smithy) AddTab(tabs, Tab.Smithy, "Forja");
            switch (tab)
            {
                case Tab.Market when market:
                    AddMarket(body, dock.Settings);
                    break;
                case Tab.Smithy when smithy:
                    AddSmithy(body, dock);
                    break;
                case Tab.Upgrades when upgrades:
                    AddUpgrades(body, dock);
                    break;
                case Tab.Buildings when dock.Buildings:
                    AddBuildings(body, dock.Buildings);
                    break;
                case Tab.Missions when board:
                    AddMissions(body, board, dock.Warehouse);
                    break;
                default:
                    tab = Tab.Cargo;
                    AddCargo(body, dock);
                    AddRepair(body, dock.Settings);
                    AddRecruit(body);
                    footer.Add(Win.Button("Descarregar tudo", visitor.UnloadAll));
                    break;
            }
            Win.Spacer(footer);
            footer.Add(Win.Button("Desatracar  <b>[F]</b>", visitor.Close));
        }

        void AddTab(VisualElement bar, Tab which, string text)
        {
            var button = Win.Button(text, () => tab = which, extraClass: "hud-tab");
            button.EnableInClassList("hud-tab--on", tab == which);
            bar.Add(button);
        }

        // ---------- Carga ----------

        /// <summary>
        /// Armazém (H4, passo 2): porão e armazém em grade de ícones (como a mochila e a tela I); o item escolhido aparece embaixo com o
        /// ícone grande, quanto tem de cada lado e os botões de levar (e de descartar, no modo de descarte). Clique duplo leva tudo para o
        /// outro lado.
        /// </summary>
        void AddCargo(VisualElement body, HarborDock dock)
        {
            var hold = visitor.Hold;
            var warehouse = dock.Warehouse;
            var bar = Win.Column(body, "hud-window__summary");
            window.AddPane(bar, () => HudDraw.Flag(discardMode), b =>
            {
                var row = Win.Row(b);
                row.Add(Win.Button(discardMode ? "Pronto (parar de descartar)" : "Descartar itens…", () =>
                {
                    discardMode = !discardMode;
                    confirmItem = null;
                }, true, "hud-button--small"));
                Win.Text(row, discardMode
                    ? "<size=85%>só o que o mercado não compra (munição, ração, cartuchos) e esquemas repetidos; <b>não devolve nada</b></size>"
                    : "<size=85%>clique escolhe · clique duplo leva tudo para o outro lado · o armazém não tem limite</size>", "hud-window__small", "hud-window__muted");
            });
            var backpack = building && building.IsOpen ? building.GetComponent<Inventory>() : null;
            if (backpack) AddBackpack(body, backpack, warehouse, dock);
            var filters = Hud.Box("hud-filters__box");
            body.Add(filters);
            window.AddPane(filters, () => Win.Mix(Win.Mix(Win.Of(hold), Win.Of(warehouse)), cargoFilter.Signature), b =>
            {
                filterItems.Clear();
                filterItems.AddRange(hold.Items);
                filterItems.AddRange(warehouse.Items);
                cargoFilter.Build(b, filterItems);
            });
            var columns = Hud.Row("hud-window__columns");
            body.Add(columns);
            long Sig() => Win.Mix(Win.Mix(Win.Mix(Win.Mix(Win.Of(hold), Win.Of(warehouse)), DiscardSignature()), cargoFilter.Signature), cargoSelected);

            window.AddPane(ScrollColumn(columns), Sig, col => CargoGrid(col, hold, "No porão", "descarregar →",
                item => item.category != ItemCategory.Crew ? visitor.Unload(item, hold.Count(item)) : 0));
            window.AddPane(ScrollColumn(columns), Sig, col => CargoGrid(col, warehouse, "No armazém da base", "← carregar",
                item => item.IsUnique ? 0 : visitor.Load(item, Mathf.Min(warehouse.Count(item), hold.MaxFit(item)))));
            var detail = Win.Section(body, "hud-cargo-detail");
            window.AddPane(detail, Sig, b => CargoDetail(b, hold, warehouse, dock));
        }

        /// <summary>Uma coluna: título, espaço usado e a grade do que passa no filtro.</summary>
        void CargoGrid(VisualElement col, Inventory inventory, string title, string hint, Func<ItemDefinition, int> moveAll)
        {
            Win.Text(col, $"{title}   <size=85%>{hint}</size>", "hud-window__subtitle-strong");
            Win.Capacity(col, inventory);
            var grid = Hud.Box("hud-equip__grid", "hud-cargo__grid");
            col.Add(grid);
            int shown = 0;
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                var item = inventory.Items[i];
                if (!cargoFilter.Shows(item)) continue;
                shown++;
                grid.Add(Win.Cell(item, inventory.Count(item), item == cargoSelected, clicks =>
                {
                    cargoSelected = item;
                    confirmItem = null;
                    if (clicks >= 2 && !discardMode) moveAll(item);
                }));
            }
            if (shown == 0) Win.Text(col, inventory.Items.Count == 0 ? "vazio" : "nada deste tipo", "hud-window__empty");
        }

        /// <summary>O item escolhido: ícone grande, nome, quanto tem em cada lado e os botões (ou o porquê de não dar).</summary>
        void CargoDetail(VisualElement box, Inventory hold, Inventory warehouse, HarborDock dock)
        {
            var item = cargoSelected;
            int inHold = item ? hold.Count(item) : 0, inWarehouse = item ? warehouse.Count(item) : 0;
            if (!item || inHold + inWarehouse == 0)
            {
                Win.Text(box, "Clique num item do porão ou do armazém para ver e mover.", "hud-window__empty");
                return;
            }
            var row = Win.Row(box, "hud-cargo-detail__row");
            row.Add(Win.Icon(item, "hud-item__slot--big"));
            var info = Hud.Box("hud-cargo-detail__info");
            row.Add(info);
            info.Add(Hud.Text($"<color={Win.Ink(item.rarity)}><b>{item.displayName}</b></color>   <size=85%>{Rarity.NameOf(item.rarity)}</size>",
                "hud-window__label", "hud-window__text"));
            info.Add(Hud.Text($"no porão <b>{inHold}</b>   ·   no armazém <b>{inWarehouse}</b>" +
                              (item.spacePerUnit > 0f ? $"   ·   <size=85%>{item.spacePerUnit:0.##} de espaço cada</size>" : ""),
                "hud-window__small", "hud-window__text"));
            if (!string.IsNullOrEmpty(item.description))
                info.Add(Hud.Text($"<i>{item.description}</i>", "hud-window__small", "hud-window__muted", "hud-window__text"));

            var actions = Hud.Box("hud-cargo-detail__actions");
            row.Add(actions);
            if (discardMode)
            {
                bool any = false;
                if (inHold > 0 && CanDiscard(item, hold, dock)) any |= DiscardLine(actions, "do porão", hold, item, inHold);
                if (inWarehouse > 0 && CanDiscard(item, warehouse, dock)) any |= DiscardLine(actions, "do armazém", warehouse, item, inWarehouse);
                if (!any) actions.Add(Hud.Text("não dá para descartar (o mercado compra, ou é único)", "hud-item__note"));
                return;
            }
            var unload = Win.Row(actions, "hud-cargo-detail__line");
            unload.Add(Hud.Text("Descarregar no armazém", "hud-cargo-detail__label"));
            if (item.category == ItemCategory.Crew) unload.Add(Hud.Text("tripulação fica a bordo", "hud-item__note"));
            else if (inHold == 0) unload.Add(Hud.Text("nada no porão", "hud-item__note"));
            else TransferButtons(unload, inHold, n => visitor.Unload(item, n), "→", after: true);

            var load = Win.Row(actions, "hud-cargo-detail__line");
            load.Add(Hud.Text("Carregar no porão", "hud-cargo-detail__label"));
            if (item.IsUnique) load.Add(Hud.Text(item.category == ItemCategory.Gear ? "equipamento fica na base" : "esquema fica na base", "hud-item__note"));
            else if (inWarehouse == 0) load.Add(Hud.Text("nada no armazém", "hud-item__note"));
            else if (hold.MaxFit(item) <= 0) load.Add(Hud.Text($"<color={Bad}>não cabe no porão</color>", "hud-item__note"));
            else TransferButtons(load, Mathf.Min(inWarehouse, hold.MaxFit(item)), n => visitor.Load(item, n), "←", after: false);
        }

        bool DiscardLine(VisualElement parent, string from, Inventory inventory, ItemDefinition item, int count)
        {
            var line = Win.Row(parent, "hud-cargo-detail__line");
            line.Add(Hud.Text($"Descartar {from}", "hud-cargo-detail__label"));
            DiscardButtons(line, inventory, item, count);
            return true;
        }

        /// <summary>Coluna com rolagem (com a mochila em cima, porão e armazém cheios não cabem e as linhas se espremiam).</summary>
        static VisualElement ScrollColumn(VisualElement parent) => Win.Scroll(Win.Column(parent));

        // ---------- Mochila → armazém (a pé) ----------

        /// <summary>
        /// A pé no Armazém (21/10, pedido do Jhonata): a mochila passa direto para o armazém (antes só embarcando, e depois do porão).
        /// "Guardar o saque" deixa na mochila o que o capitão usa a pé (ração, munição, bombas); "Guardar tudo" leva tudo; cada item tem o
        /// seu botão. No modo de descarte, os itens da mochila também podem ser jogados fora.
        /// </summary>
        void AddBackpack(VisualElement body, Inventory backpack, Inventory warehouse, HarborDock dock)
        {
            var box = Win.Section(body, "hud-backpack");
            window.AddPane(box, () => Win.Mix(Win.Mix(Win.Of(backpack), Win.Of(warehouse)), DiscardSignature()), b =>
            {
                int loot = 0;
                for (int i = 0; i < backpack.Items.Count; i++)
                    if (!CaptainSupplies.IsSupply(building, backpack.Items[i])) loot++;
                var head = Win.Row(b, "hud-backpack__head");
                head.Add(Hud.Text("Na mochila", "hud-window__subtitle-strong"));
                head.Add(Hud.Text(backpack.Unlimited ? "" : $"{backpack.UsedSpace:0.#} / {backpack.Capacity:0}", "hud-cargo__capacity", "hud-grow"));
                head.Add(Win.Button("Guardar o saque no armazém", () => StoreBackpack(backpack, warehouse, false), loot > 0, "hud-button--small"));
                head.Add(Win.Button("Guardar tudo", () => StoreBackpack(backpack, warehouse, true), backpack.Items.Count > 0, "hud-button--small"));
                if (backpack.Items.Count == 0)
                {
                    Win.Text(b, "mochila vazia", "hud-window__empty");
                    return;
                }
                var chips = Hud.Box("hud-backpack__chips");
                b.Add(chips);
                for (int i = 0; i < backpack.Items.Count; i++)
                {
                    var item = backpack.Items[i];
                    int count = backpack.Count(item);
                    var chip = Hud.Row("hud-backpack__chip");
                    chips.Add(chip);
                    bool supply = CaptainSupplies.IsSupply(building, item);
                    Win.Item(chip, item, $"{item.displayName}  <b>×{count}</b>" + (supply ? "  <size=80%><i>(usa a pé)</i></size>" : ""))
                        .AddToClassList("hud-grow");
                    if (discardMode)
                    {
                        if (CanDiscard(item, backpack, dock)) DiscardButtons(chip, backpack, item, count);
                        continue;
                    }
                    chip.Add(Win.Button("Guardar", () => StoreOne(backpack, warehouse, item), extraClass: "hud-button--small"));
                }
            });
        }

        void StoreBackpack(Inventory backpack, Inventory warehouse, bool supplies)
        {
            var moved = CaptainSupplies.Store(building, backpack, warehouse, supplies);
            if (moved.Count == 0) return;
            var parts = new List<string>();
            foreach (var s in moved) parts.Add($"{s.amount} {s.item.displayName}");
            Feed($"Guardado no armazém: {string.Join(", ", parts)}");
        }

        void StoreOne(Inventory backpack, Inventory warehouse, ItemDefinition item)
        {
            int n = CaptainSupplies.Move(backpack, warehouse, item, backpack.Count(item));
            if (n > 0) Feed($"Guardado no armazém: {n} {item.displayName}");
        }

        // ---------- Descartar (armazém) ----------

        /// <summary>
        /// Pode jogar fora: o que o mercado não compra (sem entrada ou preço de venda 0: munição, ração, cartuchos), nunca tripulação,
        /// ouro, equipamento nem esquema único; esquema só se for repetido (no porão, com o armazém já tendo um).
        /// </summary>
        bool CanDiscard(ItemDefinition item, Inventory from, HarborDock dock)
        {
            if (!item || item.category is ItemCategory.Crew or ItemCategory.Gear) return false;
            var settings = dock.Settings;
            if (settings && item == settings.repairGold) return false;
            if (item.category == ItemCategory.Schematic) return from != dock.Warehouse && dock.Warehouse.Has(item);
            if (settings)
                foreach (var e in settings.market)
                    if (e?.item == item && e.sellPrice > 0) return false;
            return true;
        }

        /// <summary>−1 / −10 / Tudo; "Tudo" e esquema pedem confirmação (o botão vira "Confirmar?" por 3 s).</summary>
        void DiscardButtons(VisualElement row, Inventory from, ItemDefinition item, int count)
        {
            bool needsConfirm = item.category == ItemCategory.Schematic;
            bool confirming = confirmItem == item && confirmInventory == from && Time.unscaledTime < confirmUntil;
            if (!needsConfirm)
            {
                row.Add(Win.Button("−1", () => Discard(from, item, 1), extraClass: "hud-button--small"));
                if (count > 10) row.Add(Win.Button("−10", () => Discard(from, item, 10), extraClass: "hud-button--small"));
            }
            string all = needsConfirm ? "Descartar" : "Tudo";
            row.Add(Win.Button(confirming ? "<color=#ffd36b>Confirmar?</color>" : all, () =>
            {
                if (confirming) Discard(from, item, from.Count(item));
                else
                {
                    confirmItem = item;
                    confirmInventory = from;
                    confirmUntil = Time.unscaledTime + 3f;
                }
            }, extraClass: "hud-button--small"));
        }

        void Discard(Inventory from, ItemDefinition item, int amount)
        {
            confirmItem = null;
            int n = from.Remove(item, amount);
            if (n > 0) Feed($"Descartou {n}× {item.displayName.ToLowerInvariant()}");
        }

        /// <summary>Modo de descarte e o botão em confirmação (some sozinho em 3 s: a janela remonta).</summary>
        long DiscardSignature() =>
            !discardMode ? 0 : Win.Mix(Win.Mix(1, confirmItem), HudDraw.Flag(confirmItem && Time.unscaledTime < confirmUntil));

        /// <summary>Botões 1 / 10 / Tudo; <paramref name="max"/> é o máximo que dá para mover agora.</summary>
        static void TransferButtons(VisualElement row, int max, Func<int, int> move, string arrow, bool after)
        {
            string Label(string n) => after ? $"{n} {arrow}" : $"{arrow} {n}";
            row.Add(Win.Button(Label("1"), () => move(1), extraClass: "hud-button--small"));
            if (max > 10) row.Add(Win.Button(Label("10"), () => move(10), extraClass: "hud-button--small"));
            row.Add(Win.Button(Label("Tudo"), () => move(max), extraClass: "hud-button--small"));
        }

        // ---------- Conserto ----------

        void AddRepair(VisualElement body, HarborSettings settings)
        {
            var hull = visitor.Hull;
            var box = Win.Section(body);
            long Sig() => Win.Mix(Win.Mix(HudDraw.Units(hull.Current), HudDraw.Units(hull.Max)),
                Win.Mix(Win.Of(visitor.Hold), Win.Of(visitor.Current.Warehouse)));
            window.AddPane(box, Sig, b =>
            {
                var row = Win.Row(b);
                row.Add(Hud.Text("<b>Casco</b>", "hud-window__label"));
                var bar = new HudBar("hud-bar--grow");
                bar.SetDanger(hull.Normalized, greenWhenFull: true);
                row.Add(bar);
                row.Add(Hud.Text($"{hull.Current:0} / {hull.Max:0}", "hud-window__value"));
                if (!settings) return;
                var buttons = Win.Row(b, "hud-repair__row");
                buttons.Add(Hud.Text("Conserto a cada 10 de casco", "hud-cost__label"));
                var per10 = Hud.Row("hud-costs");
                if (settings.repairWood) per10.Add(CostChip(settings.repairWood, settings.woodPer10Hull, visitor.Available(settings.repairWood)));
                if (settings.repairGold) per10.Add(CostChip(settings.repairGold, settings.goldPer10Hull, visitor.Available(settings.repairGold)));
                buttons.Add(per10);
                Win.Spacer(buttons);
                if (visitor.MissingHull <= 0f)
                {
                    buttons.Add(Hud.Text("<i>casco intacto</i>", "hud-window__small"));
                    return;
                }
                RepairButton(buttons, $"Consertar {Mathf.Min(partialRepair, visitor.MissingHull):0}", partialRepair);
                RepairButton(buttons, "Consertar tudo", visitor.MissingHull);
                if (visitor.AffordableRepair(10f) <= 0f) buttons.Add(Hud.Text($"<color={Bad}>falta madeira ou ouro</color>", "hud-window__small"));
            });
        }

        void RepairButton(VisualElement row, string text, float hull)
        {
            float can = visitor.AffordableRepair(hull);
            var (wood, gold) = visitor.RepairCost(can > 0f ? can : hull);
            string partial = can > 0f && can < Mathf.Min(hull, visitor.MissingHull) ? $"só +{can:0}: " : "";
            row.Add(Win.Button($"{text}  <size=80%>({partial}{wood} madeira, {gold} ouro)</size>", () => visitor.Repair(hull), can > 0f));
        }

        // ---------- Upgrades do navio ----------

        long LevelsSignature()
        {
            long hash = 17;
            var list = upgrades.Available;
            for (int i = 0; i < list.Count; i++) hash = Win.Mix(hash, upgrades.Level(list[i]));
            return hash;
        }

        long BuildingLevelsSignature(BaseBuildings buildings)
        {
            long hash = 29;
            if (!buildings) return hash;
            var list = buildings.Available;
            for (int i = 0; i < list.Count; i++) hash = Win.Mix(hash, buildings.Level(list[i]));
            return hash;
        }

        long StockSignature() => Win.Mix(Win.Of(visitor.Hold), Win.Of(visitor.Current ? visitor.Current.Warehouse : null));

        void AddUpgrades(VisualElement body, HarborDock dock)
        {
            var summary = Win.Column(body, "hud-window__summary");
            window.AddPane(summary, () => Win.Mix(Win.Mix(StockSignature(), visitor.CrewAboard), ShipSignature()), b =>
            {
                string title = UpgradesFor();
                if (title.Length > 0) Win.Text(b, title.TrimEnd(' ', '·'), "hud-window__small");
                StockChips(b, true);
            });

            var jobBox = Win.Section(body, "hud-section--job");
            window.AddPane(jobBox, () => Win.Mix(Win.Mix(dock.Job?.GetHashCode() ?? 0, Mathf.CeilToInt(dock.JobSecondsLeft)), HudDraw.Flag(dock.JobIsWorking)), b =>
            {
                var job = dock.Job;
                if (job == null)
                {
                    Win.Text(b, $"<b>Estaleiro livre</b>   <size=85%>(uma obra por vez; trabalhadores {dock.BuildSpeed:0.##}×)</size>", "hud-window__small");
                    return;
                }
                var row = Win.Row(b);
                string state = dock.JobIsWorking ? $"faltam {Win.Time(dock.JobSecondsLeft)}" : $"<color={Bad}>pausada: o navio saiu da zona do porto</color>";
                row.Add(Hud.Text($"<b>Em obra:</b> {job.Title}   {state}", "hud-window__small", "hud-grow"));
                row.Add(Win.Button("Cancelar (devolve o custo)", visitor.CancelConstruction, extraClass: "hud-button--small"));
                Win.Bar(b, job.Fraction);
            });

            var scroll = Win.Scroll(body);
            window.AddPane(scroll, () => Win.Mix(Win.Mix(StockSignature(), LevelsSignature()),
                Win.Mix(Win.Mix(dock.Job != null ? 1 : 0, visitor.CrewAboard), Win.Mix(BuildingLevelsSignature(dock.Buildings), ShipSignature()))), list =>
            {
                AddShips(list, dock);
                var grid = Hud.Box("hud-cards");
                list.Add(grid);
                foreach (var upgrade in upgrades.Available)
                {
                    if (!upgrade) continue;
                    int level = upgrades.Level(upgrade);
                    var next = upgrades.NextLevel(upgrade);
                    var card = WorkCard(grid, null, upgrade.displayName, level, upgrade.MaxLevel,
                        upgrades.EffectText(upgrade, false), next != null ? upgrades.EffectText(upgrade, true) : null, upgrade.description);
                    if (next == null) continue;
                    if (next.requiredBuilding && !(dock.Buildings && dock.Buildings.Level(next.requiredBuilding) >= next.requiredBuildingLevel))
                        card.Add(Hud.Text($"precisa de {next.requiredBuilding.displayName}{(next.requiredBuildingLevel > 1 ? $" nível {next.requiredBuildingLevel}" : "")} na base",
                            "hud-work__needs"));
                    var bottom = Win.Row(card, "hud-work__bottom");
                    bottom.Add(CostChips(upgrades.NextCost(upgrade), next.crewRequired));
                    Win.Spacer(bottom);
                    string reason = visitor.BuyBlockReason(upgrade);
                    string buy = $"Nível {level + 1}  <size=80%>({Win.Time(upgrades.NextBuildSeconds(upgrade) / dock.BuildSpeed)})</size>";
                    int nextLevel = level + 1;
                    bottom.Add(Win.Button(reason ?? buy, () =>
                    {
                        if (visitor.BuyUpgrade(upgrade)) Feed($"Obra encomendada: {upgrade.displayName} nível {nextLevel}");
                    }, reason == null, "hud-button--small"));
                }
            });
        }

        // ---------- Navio novo (estaleiro) ----------

        /// <summary>"Upgrades da Escuna · " quando o navio tem classe (os níveis são deste navio).</summary>
        string UpgradesFor()
        {
            var classes = visitor.ShipClass;
            return classes && classes.Current ? $"<b>Upgrades da {classes.Current.displayName}</b>   ·   " : "";
        }

        long ShipSignature()
        {
            var classes = visitor.ShipClass;
            return classes && classes.Current ? Win.Mix(classes.Classes.Count, classes.Current) : 0;
        }

        /// <summary>Cartões dos navios maiores que o atual: esquema, custo, tempo de obra e o que muda.</summary>
        void AddShips(VisualElement list, HarborDock dock)
        {
            var classes = visitor.ShipClass;
            var mine = classes ? classes.Current : null;
            if (!mine) return;
            foreach (var target in classes.Classes)
            {
                if (!target || target.tier <= mine.tier) continue;
                var card = Win.Section(list, "hud-section--ship");
                var top = Win.Row(card);
                top.Add(Hud.Text($"<b>Navio novo: {target.displayName}</b>", "hud-window__label"));
                top.Add(Hud.Text($"substitui o seu navio ({mine.displayName})", "hud-window__small", "hud-grow", "hud-align-right"));
                Win.Text(card, $"<i>{target.description}</i>", "hud-window__small", "hud-window__muted");
                Win.Text(card, Compare(mine, target), "hud-window__small");
                Win.Text(card, $"<color={Bad}>Os upgrades voltam ao nível 0 no navio novo</color>: compre de novo. {UpgradeBonusText(target)}",
                    "hud-window__small");
                var bottom = Win.Row(card, "hud-work__bottom");
                var needs = Hud.Row("hud-costs");
                if (target.schematic) needs.Add(CostChip(target.schematic, 1, visitor.HasSchematic(target.schematic) ? 1 : 0, "esquema"));
                if (target.requiredBuilding)
                {
                    bool built = dock.Buildings && dock.Buildings.Has(target.requiredBuilding);
                    needs.Add(Hud.Text(built ? $"<color={Good}>{target.requiredBuilding.displayName} ✓</color>"
                        : $"<color={Bad}>precisa de {target.requiredBuilding.displayName}</color>", "hud-cost__label"));
                }
                bottom.Add(needs);
                bottom.Add(CostChips(target.cost, 0));
                Win.Spacer(bottom);
                string reason = visitor.ShipBlockReason(target);
                string buy = $"Construir  <size=80%>({Win.Time(target.buildSeconds / dock.BuildSpeed)})</size>";
                var t = target;
                bottom.Add(Win.Button(reason ?? buy, () =>
                {
                    if (visitor.BuyShip(t)) Feed($"Obra encomendada: {t.displayName}");
                }, reason == null, "hud-button--small"));
            }
        }

        /// <summary>"Nele rendem mais: casco, velocidade e recarga ×1,2." (só os tipos com multiplicador diferente de 1, e o custo).</summary>
        static string UpgradeBonusText(ShipClassDefinition c)
        {
            var groups = new Dictionary<float, List<string>>();
            void Add(string name, float m)
            {
                if (Mathf.Approximately(m, 1f)) return;
                if (!groups.TryGetValue(m, out var list)) groups[m] = list = new List<string>();
                list.Add(name);
            }
            Add("casco", c.hullUpgradeMultiplier);
            Add("dano", c.damageUpgradeMultiplier);
            Add("velocidade", c.speedUpgradeMultiplier);
            Add("recarga", c.reloadUpgradeMultiplier);
            Add("mira", c.aimUpgradeMultiplier);
            var parts = new List<string>();
            foreach (var (m, names) in groups) parts.Add($"{string.Join(", ", names)} ×{m:0.##}");
            string text = parts.Count > 0 ? $"Nele rendem mais: {string.Join("; ", parts)}." : "Nele rendem o mesmo que na chalupa.";
            if (!Mathf.Approximately(c.upgradeCostMultiplier, 1f)) text += $" Custam ×{c.upgradeCostMultiplier:0.##}.";
            return text;
        }

        /// <summary>"casco 150 → 260 · canhões 4 → 6 por bordo · ..." (valores base, sem upgrades).</summary>
        static string Compare(ShipClassDefinition from, ShipClassDefinition to)
        {
            var parts = new List<string> { $"casco {from.hullMax:0} → <b>{to.hullMax:0}</b>" };
            if (from.cannons && to.cannons) parts.Add($"canhões {from.cannons.cannonsPerSide} → <b>{to.cannons.cannonsPerSide}</b> por bordo");
            parts.Add($"porão {from.holdCapacity:0} → <b>{to.holdCapacity:0}</b>");
            parts.Add($"beliches {from.crewBerths} → <b>{to.crewBerths}</b>");
            if (from.stats && to.stats)
            {
                parts.Add($"velocidade {from.stats.maxSpeed:0.#} → <b>{to.stats.maxSpeed:0.#}</b>");
                parts.Add($"giro {from.stats.turnRate:0} → <b>{to.stats.turnRate:0}</b>°/s");
            }
            return string.Join("  ·  ", parts);
        }

        // ---------- Construções da base ----------

        void AddBuildings(VisualElement body, BaseBuildings buildings)
        {
            var summary = Win.Column(body, "hud-window__summary");
            window.AddPane(summary, StockSignature, b => StockChips(b, false));

            var jobBox = Win.Section(body, "hud-section--job");
            window.AddPane(jobBox, () => Win.Mix(buildings.Job?.GetHashCode() ?? 0, Mathf.CeilToInt(buildings.JobSecondsLeft)), b =>
            {
                var job = buildings.Job;
                if (job == null)
                {
                    Win.Text(b, $"<b>Obras da base livres</b>   <size=85%>(uma por vez; continuam mesmo com o navio longe; trabalhadores {buildings.WorkerSpeed:0.##}×)</size>",
                        "hud-window__small");
                    return;
                }
                var row = Win.Row(b);
                row.Add(Hud.Text($"<b>Em obra:</b> {job.building.displayName} nível {job.level}   faltam {Win.Time(buildings.JobSecondsLeft)}", "hud-window__small", "hud-grow"));
                row.Add(Win.Button("Cancelar (devolve o custo)", visitor.CancelBuilding, extraClass: "hud-button--small"));
                Win.Bar(b, job.Fraction);
            });

            var scroll = Win.Scroll(body);
            window.AddPane(scroll, () => Win.Mix(Win.Mix(StockSignature(), BuildingLevelsSignature(buildings)), buildings.Job != null ? 1 : 0), list =>
            {
                var grid = Hud.Box("hud-cards");
                list.Add(grid);
                foreach (var b in buildings.Available)
                {
                    if (!b) continue;
                    int level = buildings.Level(b);
                    var next = buildings.NextLevel(b);
                    var card = WorkCard(grid, b.schematic, b.displayName, level, b.MaxLevel,
                        level > 0 ? buildings.EffectText(b, false) : "não construído", next != null ? buildings.EffectText(b, true) : null, b.description);
                    if (next == null) continue;
                    var bottom = Win.Row(card, "hud-work__bottom");
                    if (b.schematic && level == 0) bottom.Add(CostChip(b.schematic, 1, visitor.HasSchematic(b.schematic) ? 1 : 0, "esquema"));
                    bottom.Add(CostChips(next.cost, 0));
                    Win.Spacer(bottom);
                    string reason = visitor.BuildingBlockReason(b);
                    string buy = $"{(level == 0 ? "Construir" : $"Nível {level + 1}")}  <size=80%>({Win.Time(next.buildSeconds / buildings.WorkerSpeed)})</size>";
                    int nextLevel = level + 1;
                    var target = b;
                    bottom.Add(Win.Button(reason ?? buy, () =>
                    {
                        if (visitor.BuyBuilding(target)) Feed($"Obra iniciada: {target.displayName} nível {nextLevel}");
                    }, reason == null, "hud-button--small"));
                }
            });
        }

        // ---------- Tripulação (taverna) ----------

        void AddRecruit(VisualElement body)
        {
            var crew = visitor.Crew;
            if (!crew || !crew.Settings) return;
            var settings = crew.Settings;
            var box = Win.Section(body);
            window.AddPane(box, () => Win.Mix(Win.Mix(crew.Count, crew.Berths), StockSignature()), b =>
            {
                var row = Win.Row(b);
                row.Add(Hud.Text("<b>Tripulação</b>", "hud-window__label"));
                var bar = new HudBar("hud-bar--grow");
                bar.SetColor(HudColors.Gold);
                bar.Set(crew.Fraction);
                row.Add(bar);
                row.Add(Hud.Text($"{crew.Count} / {crew.Berths}", "hud-window__value"));
                Win.Text(b, $"Com {crew.Count} a bordo: recarga <b>-{(1f - crew.ReloadMultiplier) * 100f:0}%</b>, velas e leme <b>+{(crew.HandlingMultiplier - 1f) * 100f:0}%</b>   " +
                            $"<size=85%>(beliches cheios: -{settings.reloadBonusAtFull * 100f:0}% e +{settings.handlingBonusAtFull * 100f:0}%)</size>", "hud-window__small");
                Win.Text(b, "<i>Tiros no casco matam tripulantes. Resgate sobreviventes dos destroços ou contrate marinheiros aqui.</i>",
                    "hud-window__small", "hud-window__muted");

                var buttons = Win.Row(b);
                int can = visitor.RecruitAffordable;
                string reason = visitor.RecruitBlockReason();
                buttons.Add(Hud.Text("Marinheiro", "hud-cost__label"));
                buttons.Add(CostChips(settings.recruitCost, 0));
                buttons.Add(Hud.Text("cada", "hud-cost__label"));
                Win.Spacer(buttons);
                if (reason != null)
                {
                    buttons.Add(Hud.Text(reason, "hud-market__blocked"));
                    return;
                }
                RecruitButton(buttons, 1, settings);
                if (can > 5) RecruitButton(buttons, 5, settings);
                if (can > 1) RecruitButton(buttons, can, settings, can < crew.FreeBerths ? null : $"Encher os beliches ({can})");
            });
        }

        void RecruitButton(VisualElement row, int amount, CrewSettings settings, string text = null)
        {
            text ??= amount == 1 ? "Contratar 1" : $"Contratar {amount}";
            row.Add(Win.Button($"{text}  <size=80%>({CostTimes(settings.recruitCost, amount)})</size>", () =>
            {
                int hired = visitor.Recruit(amount);
                if (hired > 0) Feed(hired == 1 ? "Contratado: 1 marinheiro" : $"Contratados: {hired} marinheiros");
            }, extraClass: "hud-button--small"));
        }

        static string CostTimes(ItemStack[] cost, int amount)
        {
            var parts = new List<string>();
            foreach (var c in cost)
                if (c.item) parts.Add($"{c.amount * amount} {c.item.displayName.ToLowerInvariant()}");
            return string.Join(", ", parts);
        }

        // ---------- Mercado (praça) ----------

        /// <summary>
        /// Mercado (H4, passo 2): uma ficha por mercadoria, duas por linha: ícone grande na moldura da raridade, nome, quanto você tem, a
        /// procura (barra) e as linhas Comprar / Vender com o preço na plaquinha da moeda de ouro e os botões 1 / 10 / Tudo. Esquemas e
        /// equipamentos têm uma compra só.
        /// </summary>
        void AddMarket(VisualElement body, HarborSettings settings)
        {
            if (!settings || settings.market.Length == 0)
            {
                Win.Text(body, "o mercado está fechado", "hud-window__empty");
                return;
            }
            var gold = settings.repairGold;
            var coin = gold ? gold.icon : null;
            var summary = Win.Column(body, "hud-window__summary");
            window.AddPane(summary, StockSignature, b =>
            {
                var row = Win.Row(b, "hud-market__purse");
                row.Add(Hud.Text("Seu ouro", "hud-market__verb"));
                row.Add(Win.Price(coin, visitor.Available(gold), "hud-price--big"));
                Win.Text(row, "<size=85%>armazém + porão · o que você vende sai do armazém primeiro e o ouro vai para o armazém; munição " +
                              "comprada vai direto para o porão</size>", "hud-window__small", "hud-window__muted");
            });

            var filters = Hud.Box("hud-filters__box");
            body.Add(filters);
            window.AddPane(filters, () => marketFilter.Signature, b =>
            {
                filterItems.Clear();
                foreach (var entry in settings.market)
                    if (entry?.item) filterItems.Add(entry.item);
                marketFilter.Build(b, filterItems);
            });
            var scroll = Win.Scroll(body);
            var dock = visitor.Current;
            window.AddPane(scroll, () => Win.Mix(Win.Mix(Win.Mix(StockSignature(), DemandSignature(dock, settings)), FameKey()), marketFilter.Signature), list =>
            {
                var grid = Hud.Box("hud-market");
                list.Add(grid);
                foreach (var entry in settings.market)
                    if (entry?.item && marketFilter.Shows(entry.item)) MarketCard(grid, entry, dock, gold, coin);
            });
        }

        void MarketCard(VisualElement grid, HarborSettings.MarketEntry e, HarborDock dock, ItemDefinition gold, Texture2D coin)
        {
            var item = e.item;
            var card = Win.Section(grid, "hud-market__card");
            var top = Win.Row(card, "hud-market__top");
            top.Add(Win.Icon(item, "hud-item__slot--big"));
            var info = Hud.Box("hud-market__info");
            top.Add(info);
            string name = item.category == ItemCategory.Schematic ? $"Esquema: {item.displayName}" : item.displayName;
            info.Add(Hud.Text($"<color={Win.Ink(item.rarity)}><b>{name}</b></color>", "hud-market__name"));
            string sub = item.backpackCapacity > 0f ? $"mochila com {item.backpackCapacity:0} de espaço"
                : item.IsUnique ? $"{Rarity.NameOf(item.rarity)} · {(item.category == ItemCategory.Gear ? "equipamento" : "esquema de construção")} · um só"
                : $"você tem <b>{visitor.Available(item)}</b>";
            info.Add(Hud.Text(sub, "hud-market__sub"));
            if (e.sellPrice > 0 && e.demand > 0) DemandLine(info, dock.Demand(e), e.demand);
            string blocked = e.buyPrice > 0 ? visitor.MarketBlockReason(e) : null;

            if (item.IsUnique)
            {
                if (!string.IsNullOrEmpty(item.description))
                    card.Add(Hud.Text($"<i>{item.description}</i>", "hud-window__small", "hud-window__muted", "hud-window__text", "hud-market__desc"));
                var only = Win.Row(card, "hud-market__line");
                only.Add(Hud.Text("Comprar", "hud-market__verb"));
                only.Add(Win.Price(coin, visitor.BuyPrice(e)));
                Win.Spacer(only);
                int missing = visitor.BuyPrice(e) - visitor.Available(gold);
                if (blocked != null) only.Add(Hud.Text(BlockText(blocked), "hud-market__blocked"));
                else if (missing > 0) only.Add(Hud.Text($"faltam {missing} de ouro", "hud-market__blocked"));
                else only.Add(Win.Button("Comprar", () => Trade(visitor.Buy(e, 1), e, true), visitor.MaxBuy(e) >= 1, "hud-button--small"));
                return;
            }
            if (e.buyPrice > 0)
            {
                var buy = Win.Row(card, "hud-market__line");
                buy.Add(Hud.Text("Comprar", "hud-market__verb"));
                buy.Add(Win.Price(coin, visitor.BuyPrice(e)));
                Win.Spacer(buy);
                int can = visitor.MaxBuy(e);
                if (blocked != null) buy.Add(Hud.Text(BlockText(blocked), "hud-market__blocked"));
                else
                {
                    buy.Add(Win.Button("1", () => Trade(visitor.Buy(e, 1), e, true), can >= 1, "hud-button--small"));
                    buy.Add(Win.Button("10", () => Trade(visitor.Buy(e, 10), e, true), can >= 10, "hud-button--small"));
                }
            }
            if (e.sellPrice > 0)
            {
                var sell = Win.Row(card, "hud-market__line");
                sell.Add(Hud.Text("Vender", "hud-market__verb"));
                sell.Add(Win.Price(coin, visitor.SellPrice(e)));
                Win.Spacer(sell);
                int can = visitor.MaxSell(e);
                sell.Add(Win.Button("1", () => Trade(visitor.Sell(e, 1), e, false), can >= 1, "hud-button--small"));
                sell.Add(Win.Button("10", () => Trade(visitor.Sell(e, 10), e, false), can >= 10, "hud-button--small"));
                sell.Add(Win.Button("Tudo", () => Trade(visitor.Sell(e, visitor.MaxSell(e)), e, false), can >= 1, "hud-button--small"));
            }
            else if (e.buyPrice > 0)
                card.Add(Hud.Text("o mercado não compra de volta", "hud-market__sub", "hud-market__note"));
        }

        /// <summary>Quanto o mercado ainda compra de você: texto e barra (cheia = compra tudo; volta com o tempo).</summary>
        static void DemandLine(VisualElement parent, int wants, int max)
        {
            var row = Hud.Row("hud-market__demand");
            row.Add(Hud.Text(wants > 0 ? $"procura: compra mais {wants}" : $"<color={Bad}>sem procura agora</color>", "hud-market__sub"));
            var bar = new HudBar("hud-bar--thin");
            bar.AddToClassList("hud-market__demand-bar");
            bar.SetDanger((float)wants / Mathf.Max(1, max), greenWhenFull: true);
            row.Add(bar);
            parent.Add(row);
        }

        static long DemandSignature(HarborDock dock, HarborSettings settings)
        {
            long hash = 53;
            foreach (var e in settings.market)
                if (e != null && e.sellPrice > 0 && e.demand > 0) hash = Win.Mix(hash, dock.Demand(e));
            return hash;
        }

        /// <summary>"patente Corsário" → "pede a patente Corsário"; o resto ("você já tem") como está.</summary>
        static string BlockText(string reason) => reason.StartsWith("patente") ? $"pede a {reason}" : reason;

        /// <summary>Patente e bônus do capitão (M10): preços e itens do mercado mudam com eles.</summary>
        static long FameKey()
        {
            var fame = CaptainFame.Current;
            return fame ? Win.Mix(fame.Rank, fame.Perks.Count) : 0;
        }

        void Trade(int amount, HarborSettings.MarketEntry entry, bool bought)
        {
            if (amount <= 0) return;
            // O preço é o de agora (com o bônus de negociante): a compra já aconteceu com ele.
            Feed(bought ? $"Comprou {amount} {entry.item.displayName.ToLowerInvariant()} (-{amount * visitor.BuyPrice(entry)} ouro)"
                        : $"Vendeu {amount} {entry.item.displayName.ToLowerInvariant()} (+{amount * visitor.SellPrice(entry)} ouro)");
        }

        // ---------- Ferreiro (Forja) ----------

        void AddSmithy(VisualElement body, HarborDock dock)
        {
            var settings = dock.Settings;
            if (!settings || settings.smithy.Length == 0)
            {
                Win.Text(body, "o ferreiro não faz nada aqui", "hud-window__empty");
                return;
            }
            var summary = Win.Column(body, "hud-window__summary");
            window.AddPane(summary, StockSignature, b =>
            {
                StockChips(b, false);
                Win.Text(b, "<size=85%>Encomende e volte para retirar: o ferreiro trabalha mesmo com você no mar. No mar: <b>1</b> bala comum, " +
                            "<b>2</b> corrente, <b>3</b> metralha; cada bordada gasta uma carga.</size>", "hud-window__small", "hud-window__muted");
            });

            var orderBox = Win.Column(body, "hud-window__summary");
            window.AddPane(orderBox, () => SmithySignature(dock), b => AddSmithyOrder(b, dock));

            var scroll = Win.Scroll(body);
            window.AddPane(scroll, () => Win.Mix(Win.Mix(StockSignature(), BuildingLevelsSignature(dock.Buildings)), SmithySignature(dock)), list =>
            {
                var grid = Hud.Box("hud-cards");
                list.Add(grid);
                foreach (var recipe in settings.smithy)
                {
                    if (recipe?.result.item == null) continue;
                    var r = recipe;
                    var item = r.result.item;
                    // Receita (H4, passo 4): o que sai em ícone grande, os materiais em fichas e o tempo de forja.
                    var card = Win.Section(grid, "hud-cards__card");
                    var top = Win.Row(card, "hud-work__top");
                    top.Add(Win.Icon(item, "hud-item__slot--big"));
                    var info = Hud.Box("hud-work__info");
                    top.Add(info);
                    info.Add(Hud.Text($"<b>{item.displayName}</b>{(r.result.amount > 1 ? $"  <size=85%>×{r.result.amount} por receita</size>" : "")}", "hud-work__name"));
                    info.Add(Hud.Text($"no porão: <b>{visitor.Hold.Count(item)}</b>   ·   {Win.Time(visitor.CraftSeconds(r, 1))} de forja", "hud-market__sub"));
                    if (!string.IsNullOrEmpty(item.description))
                        card.Add(Hud.Text($"<i>{item.description}</i>", "hud-window__small", "hud-window__muted", "hud-window__text", "hud-market__desc"));
                    var bottom = Win.Row(card, "hud-work__bottom");
                    bottom.Add(CostChips(r.cost, 0));
                    Win.Spacer(bottom);
                    string reason = visitor.CraftBlockReason(r);
                    int max = visitor.MaxCraft(r);
                    if (reason != null)
                    {
                        bottom.Add(Hud.Text(reason, "hud-market__blocked"));
                        continue;
                    }
                    bottom.Add(Win.Button("Encomendar 1", () => Forged(visitor.Craft(r, 1), r), true, "hud-button--small"));
                    if (max >= 5) bottom.Add(Win.Button("5", () => Forged(visitor.Craft(r, 5), r), true, "hud-button--small"));
                    if (max > 1 && max != 5) bottom.Add(Win.Button($"{max}", () => Forged(visitor.Craft(r, max), r), true, "hud-button--small"));
                }
            });
        }

        void Forged(int times, HarborSettings.Recipe recipe)
        {
            if (times <= 0) return;
            Feed($"Encomendado: {times * recipe.result.amount}× {recipe.result.item.displayName.ToLowerInvariant()} " +
                 $"(pronto em {Win.Time(visitor.Current ? visitor.Current.SmithySecondsLeft : 0f)})");
        }

        /// <summary>Muda com a encomenda (ou falta dela), com os segundos que faltam e quando fica pronta.</summary>
        static long SmithySignature(HarborDock dock)
        {
            var o = dock.Smithy;
            if (o == null) return 7;
            return Win.Mix(Win.Mix(o.Result.amount, HudDraw.Flag(o.Ready)), Mathf.CeilToInt(dock.SmithySecondsLeft));
        }

        /// <summary>A encomenda do ferreiro: livre, trabalhando (barra e tempo, cancelar) ou pronta para retirar.</summary>
        void AddSmithyOrder(VisualElement parent, HarborDock dock)
        {
            var o = dock.Smithy;
            var box = Win.Section(parent, "hud-section--job");
            if (o == null)
            {
                Win.Text(box, "<b>Ferreiro livre</b>   <size=85%>encomende abaixo; uma encomenda por vez</size>", "hud-window__small");
                return;
            }
            string what = $"{o.Result.amount}× {o.Result.item.displayName.ToLowerInvariant()}";
            var row = Win.Row(box);
            row.Add(Win.Icon(o.Result.item));
            if (o.Ready)
            {
                row.Add(Hud.Text($"<color={Good}><b>Pronto:</b></color> {what}", "hud-window__label", "hud-grow"));
                row.Add(Win.Button("Retirar", () =>
                {
                    var got = visitor.CollectCraft();
                    if (got.item) Feed($"Retirou {got.amount}× {got.item.displayName.ToLowerInvariant()} da Forja");
                }, true, "hud-button--buy"));
                return;
            }
            row.Add(Hud.Text($"<b>Forjando:</b> {what}   <size=85%>falta {Win.Time(dock.SmithySecondsLeft)}</size>", "hud-window__label", "hud-grow"));
            row.Add(Win.Button("Cancelar", visitor.CancelCraft, true, "hud-button--small"));
            var bar = new HudBar();
            bar.Set(o.Fraction);
            bar.SetColor(new Color(0.85f, 0.6f, 0.15f));
            box.Add(bar);
        }

        // ---------- Missões (taverna) ----------

        /// <summary>
        /// Taverna (H4, passo 4): à esquerda o quadro de madeira com os contratos pregados como cartazes (alfinete, papel um pouco torto,
        /// pagamento em fichas e a fama); os que descansam e os de patente alta aparecem apagados com o carimbo. À direita, as suas missões.
        /// </summary>
        void AddMissions(VisualElement body, MissionBoard board, Inventory warehouse)
        {
            var columns = Hud.Row("hud-window__columns");
            body.Add(columns);

            var left = Win.Column(columns, "hud-board");
            left.Add(Hud.Text("Quadro de contratos", "hud-board__title"));
            var scroll = Win.Scroll(left);
            window.AddPane(scroll, () => Win.Mix(Win.Mix(Win.Mix(Win.Mix(board.Active.Count, board.Finished.Count), HudDraw.Flag(board.CanAccept)), board.RestingKey()), board.Rank), list =>
            {
                var available = board.Available();
                var locked = board.Locked();
                if (available.Count == 0 && board.Cooldowns.Count == 0 && locked.Count == 0) list.Add(Hud.Text("nenhum contrato agora: volte mais tarde", "hud-board__empty"));
                int n = 0;
                foreach (var mission in available)
                {
                    var poster = Poster(list, mission, n++, null);
                    poster.Add(Hud.Text($"\"{mission.description}\"", "hud-poster__text"));
                    var row = Win.Row(poster, "hud-poster__bottom");
                    row.Add(RewardChips(board, mission));
                    Win.Spacer(row);
                    var m = mission;
                    if (board.CanAccept) row.Add(Win.Button("Aceitar", () => board.Accept(m), true, "hud-button--small"));
                    else row.Add(Hud.Text($"máx. {board.MaxActive} ativas", "hud-market__blocked"));
                }
                // Repetíveis entregues descansando: apagadas, com quanto falta.
                foreach (var (mission, _) in board.Cooldowns)
                {
                    if (mission.givenByNpc) continue;   // as dos NPCs da vila não são do quadro
                    Poster(list, mission, n++, $"volta em {RestingText(board.CooldownLeft(mission))}");
                }
                // Contratos de capitães famosos (M10): trancados, com a patente que falta.
                foreach (var mission in locked)
                    Poster(list, mission, n++, $"patente {board.RankTitle(mission.requiredRank)}");
            });

            var right = Win.Column(columns);
            window.AddPane(right, () => MissionsSignature(board), col =>
            {
                Win.Text(col, $"Suas missões   <size=85%>({board.Active.Count}/{board.MaxActive})</size>", "hud-window__subtitle-strong");
                if (board.Active.Count == 0) Win.Text(col, "nenhuma: aceite um contrato no quadro", "hud-window__empty");
                for (int i = 0; i < board.Active.Count; i++)
                {
                    var active = board.Active[i];
                    var mission = active.definition;
                    bool done = board.IsComplete(active);
                    var card = Win.Section(col, "hud-mission");
                    card.EnableInClassList("hud-mission--done", done);
                    card.Add(Hud.Text($"<b>{mission.title}</b>", "hud-work__name"));
                    card.Add(Hud.Text($"<i>{mission.giver}</i>", "hud-market__sub"));
                    card.Add(Hud.Text(done ? $"<color={Good}><b>pronta para entregar</b>{(mission.givenByNpc ? $" a {mission.giver}, na vila" : "")}</color>"
                        : board.ProgressText(active), "hud-work__effect"));
                    var row = Win.Row(card, "hud-work__bottom");
                    row.Add(RewardChips(board, mission));
                    Win.Spacer(row);
                    var a = active;
                    // Missão de NPC da vila: entrega com ele (ele conta o fim da história e paga), não no quadro.
                    if (mission.givenByNpc) row.Add(Hud.Text($"<i>entregue a {mission.giver}</i>", "hud-window__small", "hud-window__muted"));
                    else row.Add(Win.Button("Entregar", () => board.TurnIn(a, warehouse), done, "hud-button--small"));
                    row.Add(Win.Button("Desistir", () => board.Abandon(a), extraClass: "hud-button--small"));
                }
                Win.Spacer(col);
                Win.Text(col, "<i>Entregas contam o porão e o armazém; o pagamento vai para o armazém.</i>", "hud-window__small", "hud-window__muted");
            });
        }

        /// <summary>Cartaz pregado no quadro: alfinete, título, quem paga; <paramref name="stamp"/> = apagado com o carimbo (descanso, patente).</summary>
        static VisualElement Poster(VisualElement list, MissionDefinition mission, int index, string stamp)
        {
            var poster = Hud.Box("hud-poster");
            poster.pickingMode = PickingMode.Position;
            poster.style.rotate = new Rotate(new Angle(index % 3 == 0 ? -1.2f : index % 3 == 1 ? 0.9f : -0.4f, AngleUnit.Degree));
            if (stamp != null) poster.AddToClassList("hud-poster--off");
            poster.Add(Hud.Box("hud-poster__pin"));
            poster.Add(Hud.Text(mission.title, "hud-poster__title"));
            poster.Add(Hud.Text(mission.giver, "hud-poster__giver"));
            if (stamp != null) poster.Add(Hud.Text(stamp, "hud-poster__stamp"));
            list.Add(poster);
            return poster;
        }

        /// <summary>O pagamento em fichas (ouro e itens, com a patente) e a fama.</summary>
        static VisualElement RewardChips(MissionBoard board, MissionDefinition mission)
        {
            var row = Hud.Row("hud-costs");
            foreach (var stack in board.Rewards(mission))
                if (stack.item && stack.amount > 0) row.Add(Chip(stack.item, $"+{stack.amount}"));
            if (mission.fameReward > 0)
            {
                var fame = Hud.Row("hud-cost", "hud-cost--fame");
                fame.Add(Hud.Text($"+{mission.fameReward} fama", "hud-cost__value"));
                row.Add(fame);
            }
            return row;
        }

        static string RestingText(float seconds)
        {
            int minutes = Mathf.CeilToInt(seconds / 60f);
            return minutes <= 1 ? "menos de 1 min" : $"{minutes} min";
        }

        long MissionsSignature(MissionBoard board)
        {
            long hash = Win.Mix(board.Active.Count, StockSignature());
            for (int i = 0; i < board.Active.Count; i++)
            {
                var a = board.Active[i];
                hash = Win.Mix(hash, a.definition);
                hash = Win.Mix(hash, a.progress * 2 + (a.objectiveDone ? 1 : 0));
            }
            return hash;
        }

        // ---------- Textos ----------

        /// <summary>
        /// Cartão de obra (upgrade ou construção; H4, passo 3): ícone (o do esquema, se tem), nome, bolinhas de nível, o efeito de agora →
        /// o do próximo nível (ou "máximo") e a descrição. Quem chama põe os requisitos, o custo e o botão embaixo.
        /// </summary>
        VisualElement WorkCard(VisualElement grid, ItemDefinition icon, string name, int level, int max, string now, string next, string description)
        {
            var card = Win.Section(grid, "hud-cards__card");
            var top = Win.Row(card, "hud-work__top");
            if (icon) top.Add(Win.Icon(icon, "hud-item__slot--big"));
            var info = Hud.Box("hud-work__info");
            top.Add(info);
            var head = Hud.Row("hud-work__head");
            head.Add(Hud.Text($"<b>{name}</b>", "hud-work__name"));
            Win.Spacer(head);
            head.Add(Win.Pips(level, max));
            info.Add(head);
            info.Add(Hud.Text(next == null ? $"{now}   <i>(máximo)</i>" : $"<color=#7a5a3c>{now}</color>  →  <color={Good}><b>{next}</b></color>",
                "hud-work__effect"));
            if (!string.IsNullOrEmpty(description))
                card.Add(Hud.Text($"<i>{description}</i>", "hud-window__small", "hud-window__muted", "hud-window__text", "hud-market__desc"));
            return card;
        }

        /// <summary>Custo em fichas: ícone e quantidade; vermelho, com quanto você tem, o que falta.</summary>
        VisualElement CostChips(ItemStack[] cost, int crewRequired)
        {
            var row = Hud.Row("hud-costs");
            foreach (var c in cost)
                if (c.item) row.Add(CostChip(c.item, c.amount, visitor.Available(c.item)));
            var crewItem = visitor.Crew ? visitor.Crew.CrewItem : null;
            if (crewRequired > 0 && crewItem) row.Add(CostChip(crewItem, crewRequired, visitor.CrewAboard, "a bordo"));
            return row;
        }

        static VisualElement CostChip(ItemDefinition item, int amount, int have, string label = null)
        {
            bool missing = have < amount;
            string text = label == "esquema" ? (missing ? "falta o esquema" : "esquema ✓")
                : missing ? $"{amount} <size=80%>(tem {have})</size>"
                : label != null ? $"{amount} {label}" : amount.ToString();
            return Chip(item, text, missing);
        }

        static VisualElement Chip(ItemDefinition item, string text, bool missing = false)
        {
            var chip = Hud.Row("hud-cost");
            if (missing) chip.AddToClassList("hud-cost--missing");
            chip.Add(Win.Icon(item, "hud-item__slot--small"));
            chip.Add(Hud.Text(text, "hud-cost__value"));
            return chip;
        }

        /// <summary>"Você tem": os recursos (ouro, madeira, pólvora, ferro) do armazém + porão em fichas; tripulantes a bordo, se pedido.</summary>
        void StockChips(VisualElement parent, bool crew)
        {
            var row = Win.Row(parent, "hud-work__stock");
            row.Add(Hud.Text("Você tem", "hud-cost__label"));
            var chips = Hud.Row("hud-costs");
            row.Add(chips);
            var seen = new HashSet<ItemDefinition>();
            foreach (var inventory in new[] { visitor.Current.Warehouse, visitor.Hold })
            {
                if (!inventory) continue;
                for (int i = 0; i < inventory.Items.Count; i++)
                {
                    var item = inventory.Items[i];
                    if (!item || item.Group != ItemGroup.Resource || !seen.Add(item)) continue;
                    chips.Add(Chip(item, visitor.Available(item).ToString()));
                }
            }
            var crewItem = visitor.Crew ? visitor.Crew.CrewItem : null;
            if (crew && crewItem) chips.Add(Chip(crewItem, $"{visitor.CrewAboard} a bordo"));
        }

        static void Feed(string text)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, text, new Color(0.95f, 0.92f, 0.8f));
        }
    }
}
