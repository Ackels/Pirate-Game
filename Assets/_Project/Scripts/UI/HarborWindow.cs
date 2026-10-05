using System;
using System.Collections.Generic;
using Pirates.Base;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Missions;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Janela do porto (UI Toolkit), com o mouse. A pé (cena 07), cada casa mostra só a parte dela: Armazém = carga;
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
                    : "<size=85%>o armazém não tem limite; descarte o que sobrar (munição, ração, cartuchos)</size>", "hud-window__small", "hud-window__muted");
            });
            var columns = Hud.Row("hud-window__columns");
            body.Add(columns);
            long Sig() => Win.Mix(Win.Mix(Win.Of(hold), Win.Of(warehouse)), DiscardSignature());

            window.AddPane(Win.Column(columns), Sig, col =>
            {
                Win.Text(col, "No porão   <size=85%>descarregar →</size>", "hud-window__subtitle-strong");
                Win.Capacity(col, hold);
                if (hold.Items.Count == 0) Win.Text(col, "vazio", "hud-window__empty");
                for (int i = 0; i < hold.Items.Count; i++)
                {
                    var item = hold.Items[i];
                    int count = hold.Count(item);
                    var row = Win.Row(col, "hud-window__item-row");
                    Win.Item(row, item, $"{item.displayName}  <b>×{count}</b>");
                    if (discardMode)
                    {
                        if (CanDiscard(item, hold, dock)) DiscardButtons(row, hold, item, count);
                        continue;
                    }
                    if (item.category == ItemCategory.Crew) row.Add(Hud.Text("tripulação fica a bordo", "hud-item__note"));
                    else TransferButtons(row, count, n => visitor.Unload(item, n), "→", after: true);
                }
            });
            window.AddPane(Win.Column(columns), Sig, col =>
            {
                Win.Text(col, "No armazém da base   <size=85%>← carregar</size>", "hud-window__subtitle-strong");
                Win.Capacity(col, warehouse);
                if (warehouse.Items.Count == 0) Win.Text(col, "vazio", "hud-window__empty");
                for (int i = 0; i < warehouse.Items.Count; i++)
                {
                    var item = warehouse.Items[i];
                    int count = warehouse.Count(item);
                    var row = Win.Row(col, "hud-window__item-row");
                    Win.Item(row, item, $"{item.displayName}  <b>×{count}</b>");
                    if (discardMode)
                    {
                        if (CanDiscard(item, warehouse, dock)) DiscardButtons(row, warehouse, item, count);
                        continue;
                    }
                    if (item.IsUnique) row.Add(Hud.Text(item.category == ItemCategory.Gear ? "equipamento guardado na base" : "esquema guardado na base", "hud-item__note"));
                    else if (hold.MaxFit(item) <= 0) row.Add(Hud.Text($"<color={Bad}>não cabe no porão</color>", "hud-item__note"));
                    else TransferButtons(row, Mathf.Min(count, hold.MaxFit(item)), n => visitor.Load(item, n), "←", after: false);
                }
            });
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
                int wood = visitor.Available(settings.repairWood), gold = visitor.Available(settings.repairGold);
                Win.Text(b, $"Conserto: {settings.woodPer10Hull} madeira + {settings.goldPer10Hull} ouro a cada 10 de casco   " +
                            $"<size=85%>(você tem {wood} madeira e {gold} ouro, somando porão e armazém)</size>", "hud-window__small");
                var buttons = Win.Row(b);
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
            window.AddPane(summary, () => Win.Mix(Win.Mix(StockSignature(), visitor.CrewAboard), ShipSignature()), b => Win.Text(b,
                $"{UpgradesFor()}Disponível (armazém + porão):  {AvailableSummary()}   ·   tripulantes a bordo: {visitor.CrewAboard}", "hud-window__small"));

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
                foreach (var upgrade in upgrades.Available)
                {
                    if (!upgrade) continue;
                    int level = upgrades.Level(upgrade);
                    var next = upgrades.NextLevel(upgrade);
                    var card = Win.Section(list);
                    var top = Win.Row(card);
                    top.Add(Hud.Text($"<b>{upgrade.displayName}</b>", "hud-window__label"));
                    top.Add(Win.Pips(level, upgrade.MaxLevel));
                    string effect = next == null
                        ? $"{upgrades.EffectText(upgrade, false)}   <i>(máximo)</i>"
                        : $"{upgrades.EffectText(upgrade, false)}  →  <b>{upgrades.EffectText(upgrade, true)}</b>";
                    top.Add(Hud.Text(effect, "hud-window__small", "hud-grow", "hud-align-right"));
                    Win.Text(card, $"<i>{upgrade.description}</i>", "hud-window__small", "hud-window__muted");
                    if (next == null) continue;
                    var bottom = Win.Row(card);
                    string needs = next.requiredBuilding && !(dock.Buildings && dock.Buildings.Level(next.requiredBuilding) >= next.requiredBuildingLevel)
                        ? $"<color={Bad}>precisa de {next.requiredBuilding.displayName}{(next.requiredBuildingLevel > 1 ? $" nível {next.requiredBuildingLevel}" : "")} na base</color>   " : "";
                    bottom.Add(Hud.Text(needs + CostText(upgrades.NextCost(upgrade), next.crewRequired), "hud-window__small", "hud-grow"));
                    string reason = visitor.BuyBlockReason(upgrade);
                    string buy = $"Encomendar nível {level + 1}  <size=80%>({Win.Time(upgrades.NextBuildSeconds(upgrade) / dock.BuildSpeed)})</size>";
                    int nextLevel = level + 1;
                    bottom.Add(Win.Button(reason ?? buy, () =>
                    {
                        if (visitor.BuyUpgrade(upgrade)) Feed($"Obra encomendada: {upgrade.displayName} nível {nextLevel}");
                    }, reason == null, "hud-button--buy"));
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
                var bottom = Win.Row(card);
                string building = !target.requiredBuilding ? ""
                    : dock.Buildings && dock.Buildings.Has(target.requiredBuilding) ? $"<color={Good}>{target.requiredBuilding.displayName}</color> ok   "
                    : $"<color={Bad}>precisa de {target.requiredBuilding.displayName} na base</color>   ";
                string schematic = !target.schematic ? ""
                    : visitor.HasSchematic(target.schematic) ? $"esquema <color={Good}>{target.schematic.displayName}</color> ok   "
                    : $"<color={Bad}>precisa do esquema {target.schematic.displayName} (baú raro)</color>   ";
                bottom.Add(Hud.Text(building + schematic + CostText(target.cost, 0), "hud-window__small", "hud-grow"));
                string reason = visitor.ShipBlockReason(target);
                string buy = $"Construir  <size=80%>({Win.Time(target.buildSeconds / dock.BuildSpeed)})</size>";
                var t = target;
                bottom.Add(Win.Button(reason ?? buy, () =>
                {
                    if (visitor.BuyShip(t)) Feed($"Obra encomendada: {t.displayName}");
                }, reason == null, "hud-button--buy"));
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
            window.AddPane(summary, StockSignature, b => Win.Text(b, $"Disponível (armazém + porão):  {AvailableSummary()}", "hud-window__small"));

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
                foreach (var b in buildings.Available)
                {
                    if (!b) continue;
                    int level = buildings.Level(b);
                    var next = buildings.NextLevel(b);
                    var card = Win.Section(list);
                    var top = Win.Row(card);
                    top.Add(Hud.Text($"<b>{b.displayName}</b>", "hud-window__label"));
                    top.Add(Win.Pips(level, b.MaxLevel));
                    string effect = next == null
                        ? $"{buildings.EffectText(b, false)}   <i>(máximo)</i>"
                        : level == 0 ? $"→  <b>{buildings.EffectText(b, true)}</b>"
                        : $"{buildings.EffectText(b, false)}  →  <b>{buildings.EffectText(b, true)}</b>";
                    top.Add(Hud.Text(effect, "hud-window__small", "hud-grow", "hud-align-right"));
                    Win.Text(card, $"<i>{b.description}</i>", "hud-window__small", "hud-window__muted");
                    if (next == null) continue;
                    var bottom = Win.Row(card);
                    string schematic = !b.schematic ? ""
                        : visitor.HasSchematic(b.schematic) ? $"esquema <color={Good}>{b.schematic.displayName}</color> ok   "
                        : $"<color={Bad}>precisa do esquema {b.schematic.displayName} (baú raro)</color>   ";
                    bottom.Add(Hud.Text(schematic + CostText(next.cost, 0), "hud-window__small", "hud-grow"));
                    string reason = visitor.BuildingBlockReason(b);
                    string buy = $"Construir nível {level + 1}  <size=80%>({Win.Time(next.buildSeconds / buildings.WorkerSpeed)})</size>";
                    int nextLevel = level + 1;
                    var target = b;
                    bottom.Add(Win.Button(reason ?? buy, () =>
                    {
                        if (visitor.BuyBuilding(target)) Feed($"Obra iniciada: {target.displayName} nível {nextLevel}");
                    }, reason == null, "hud-button--buy"));
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
                buttons.Add(Hud.Text($"Marinheiro: {Win.Stacks(settings.recruitCost)} cada", "hud-window__small", "hud-grow"));
                if (reason != null)
                {
                    buttons.Add(Win.Button(reason, null, false, "hud-button--buy"));
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

        void AddMarket(VisualElement body, HarborSettings settings)
        {
            if (!settings || settings.market.Length == 0)
            {
                Win.Text(body, "o mercado está fechado", "hud-window__empty");
                return;
            }
            var gold = settings.repairGold;
            var summary = Win.Column(body, "hud-window__summary");
            window.AddPane(summary, StockSignature, b => Win.Text(b,
                $"Seu ouro (armazém + porão): <b>{visitor.Available(gold)}</b>   ·   <size=85%>o que você vende sai do armazém primeiro; o ouro " +
                "vai para o armazém. Munição comprada vai direto para o porão.</size>", "hud-window__small"));

            var scroll = Win.Scroll(body);
            var dock = visitor.Current;
            window.AddPane(scroll, () => Win.Mix(Win.Mix(StockSignature(), DemandSignature(dock, settings)), FameKey()), list =>
            {
                foreach (var entry in settings.market)
                {
                    if (entry?.item == null) continue;
                    var e = entry;
                    var card = Win.Section(list);
                    var top = Win.Row(card);
                    Win.Item(top, e.item, e.item.category == ItemCategory.Schematic ? $"Esquema: {e.item.displayName}"
                        : e.item.backpackCapacity > 0f ? $"{e.item.displayName}   <size=85%>{e.item.backpackCapacity:0} de espaço na mochila</size>"
                        : e.item.IsUnique ? e.item.displayName
                        : $"{e.item.displayName}   <size=85%>você tem {visitor.Available(e.item)}</size>");
                    top.Add(Hud.Text(PriceText(e), "hud-window__small", "hud-grow", "hud-align-right"));
                    string blocked = e.buyPrice > 0 ? visitor.MarketBlockReason(e) : null;
                    if (e.item.IsUnique)
                    {
                        // Esquema (M10, por patente): uma compra só.
                        if (!string.IsNullOrEmpty(e.item.description))
                            Win.Text(card, $"<i>{e.item.description}</i>", "hud-window__small", "hud-window__muted");
                        var only = Win.Row(card);
                        Win.Spacer(only);
                        only.Add(blocked != null
                            ? Win.Button(BlockText(blocked), null, false, "hud-button--buy")
                            : Win.Button($"Comprar  <size=80%>({visitor.BuyPrice(e)} ouro)</size>", () => Trade(visitor.Buy(e, 1), e, true), visitor.MaxBuy(e) >= 1, "hud-button--buy"));
                        continue;
                    }
                    if (e.sellPrice > 0 && e.demand > 0)
                    {
                        int wants = dock.Demand(e);
                        Win.Text(card, wants > 0 ? $"<size=85%>compra de você até mais {wants} (a procura volta com o tempo)</size>"
                                                 : $"<size=85%><color={Bad}>não quer mais por enquanto</color> (a procura volta com o tempo)</size>", "hud-window__small", "hud-window__muted");
                    }
                    var row = Win.Row(card);
                    int canBuy = visitor.MaxBuy(e), canSell = visitor.MaxSell(e);
                    if (e.buyPrice > 0 && blocked != null)
                        row.Add(Win.Button(BlockText(blocked), null, false, "hud-button--small"));
                    else if (e.buyPrice > 0)
                    {
                        int buy = visitor.BuyPrice(e);
                        row.Add(Win.Button($"Comprar 1  <size=80%>({buy})</size>", () => Trade(visitor.Buy(e, 1), e, true), canBuy >= 1, "hud-button--small"));
                        row.Add(Win.Button($"Comprar 10  <size=80%>({buy * 10})</size>", () => Trade(visitor.Buy(e, 10), e, true), canBuy >= 10, "hud-button--small"));
                    }
                    Win.Spacer(row);
                    if (e.sellPrice > 0)
                    {
                        int sell = visitor.SellPrice(e);
                        row.Add(Win.Button($"Vender 1  <size=80%>(+{sell})</size>", () => Trade(visitor.Sell(e, 1), e, false), canSell >= 1, "hud-button--small"));
                        row.Add(Win.Button($"Vender 10  <size=80%>(+{sell * 10})</size>", () => Trade(visitor.Sell(e, 10), e, false), canSell >= 10, "hud-button--small"));
                        row.Add(Win.Button($"Vender tudo  <size=80%>(+{sell * canSell})</size>", () => Trade(visitor.Sell(e, canSell), e, false), canSell >= 1, "hud-button--small"));
                    }
                }
            });
        }

        static long DemandSignature(HarborDock dock, HarborSettings settings)
        {
            long hash = 53;
            foreach (var e in settings.market)
                if (e != null && e.sellPrice > 0 && e.demand > 0) hash = Win.Mix(hash, dock.Demand(e));
            return hash;
        }

        string PriceText(HarborSettings.MarketEntry e)
        {
            var parts = new List<string>();
            if (e.buyPrice > 0) parts.Add($"vende por <b>{visitor.BuyPrice(e)}</b>");
            if (e.sellPrice > 0) parts.Add($"compra de você por <b>{visitor.SellPrice(e)}</b>");
            return string.Join("  ·  ", parts) + " ouro";
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
            window.AddPane(summary, StockSignature, b => Win.Text(b,
                $"Disponível (armazém + porão):  {AvailableSummary()}   ·   <size=85%>encomende e volte para retirar: o ferreiro trabalha " +
                "mesmo com você no mar. No mar: <b>1</b> bala comum, <b>2</b> corrente, <b>3</b> metralha; cada bordada gasta uma carga.</size>",
                "hud-window__small"));

            var orderBox = Win.Column(body, "hud-window__summary");
            window.AddPane(orderBox, () => SmithySignature(dock), b => AddSmithyOrder(b, dock));

            var scroll = Win.Scroll(body);
            window.AddPane(scroll, () => Win.Mix(Win.Mix(StockSignature(), BuildingLevelsSignature(dock.Buildings)), SmithySignature(dock)), list =>
            {
                foreach (var recipe in settings.smithy)
                {
                    if (recipe?.result.item == null) continue;
                    var r = recipe;
                    var card = Win.Section(list);
                    var top = Win.Row(card);
                    string amount = r.result.amount > 1 ? $" ×{r.result.amount}" : "";
                    Win.Item(top, r.result.item, $"{r.result.item.displayName}{amount}   <size=85%>no porão: {visitor.Hold.Count(r.result.item)}</size>");
                    if (!string.IsNullOrEmpty(r.result.item.description))
                        Win.Text(card, $"<i>{r.result.item.description}</i>", "hud-window__small", "hud-window__muted");
                    var bottom = Win.Row(card);
                    bottom.Add(Hud.Text($"{CostText(r.cost, 0)}   ·   <size=90%>{Win.Time(visitor.CraftSeconds(r, 1))} de forja cada</size>", "hud-window__small", "hud-grow"));
                    string reason = visitor.CraftBlockReason(r);
                    int max = visitor.MaxCraft(r);
                    if (reason != null)
                    {
                        bottom.Add(Win.Button(reason, null, false, "hud-button--buy"));
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

        void AddMissions(VisualElement body, MissionBoard board, Inventory warehouse)
        {
            var columns = Hud.Row("hud-window__columns");
            body.Add(columns);

            var left = Win.Column(columns);
            Win.Text(left, "Contratos no quadro", "hud-window__subtitle-strong");
            var scroll = Win.Scroll(left);
            window.AddPane(scroll, () => Win.Mix(Win.Mix(Win.Mix(Win.Mix(board.Active.Count, board.Finished.Count), HudDraw.Flag(board.CanAccept)), board.RestingKey()), board.Rank), list =>
            {
                var available = board.Available();
                var locked = board.Locked();
                if (available.Count == 0 && board.Cooldowns.Count == 0 && locked.Count == 0) Win.Text(list, "nenhum contrato agora: volte mais tarde", "hud-window__empty");
                foreach (var mission in available)
                {
                    var card = Win.Section(list);
                    Win.Text(card, $"<b>{mission.title}</b>   <size=85%><i>{mission.giver}</i></size>", "hud-window__label");
                    Win.Text(card, $"\"{mission.description}\"", "hud-window__small", "hud-window__muted");
                    var row = Win.Row(card);
                    row.Add(Hud.Text($"Paga: {Win.Stacks(board.Rewards(mission))}   <size=85%>· +{mission.fameReward} fama</size>", "hud-window__small", "hud-grow"));
                    var m = mission;
                    row.Add(Win.Button(board.CanAccept ? "Aceitar" : $"Máx. {board.MaxActive} ativas", () => board.Accept(m), board.CanAccept, "hud-button--small"));
                }
                // Repetíveis entregues descansando: aparecem apagadas, com quanto falta.
                foreach (var (mission, _) in board.Cooldowns)
                {
                    var card = Win.Section(list);
                    Win.Text(card, $"<b>{mission.title}</b>   <size=85%><i>{mission.giver}</i></size>", "hud-window__label", "hud-window__muted");
                    Win.Text(card, $"<i>já entregue: volta ao quadro em {RestingText(board.CooldownLeft(mission))}</i>", "hud-window__small", "hud-window__muted");
                }
                // Contratos de capitães famosos (M10): aparecem trancados, com a patente que falta.
                foreach (var mission in locked)
                {
                    var card = Win.Section(list);
                    Win.Text(card, $"<b>{mission.title}</b>   <size=85%><i>{mission.giver}</i></size>", "hud-window__label", "hud-window__muted");
                    Win.Text(card, $"<i>só para capitães com a patente <b>{board.RankTitle(mission.requiredRank)}</b> (tecla C: sua fama)</i>",
                        "hud-window__small", "hud-window__muted");
                }
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
                    var card = Win.Section(col);
                    Win.Text(card, $"<b>{mission.title}</b>   <size=85%><i>{mission.giver}</i></size>", "hud-window__label");
                    Win.Text(card, done ? $"<color={Good}>pronta para entregar</color>" : board.ProgressText(active), "hud-window__small");
                    var row = Win.Row(card);
                    var a = active;
                    row.Add(Win.Button($"Entregar  <size=80%>(+{Win.Stacks(board.Rewards(mission))}, +{mission.fameReward} fama)</size>", () => board.TurnIn(a, warehouse), done, "hud-button--small"));
                    row.Add(Win.Button("Desistir", () => board.Abandon(a), extraClass: "hud-button--small"));
                }
                Win.Spacer(col);
                Win.Text(col, "<i>Entregas contam o porão e o armazém; o pagamento vai para o armazém.</i>", "hud-window__small", "hud-window__muted");
            });
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

        string AvailableSummary()
        {
            var seen = new HashSet<ItemDefinition>();
            var parts = new List<string>();
            foreach (var inventory in new[] { visitor.Current.Warehouse, visitor.Hold })
            foreach (var item in inventory.Items)
            {
                if (item.category != ItemCategory.Resource || !seen.Add(item)) continue;
                parts.Add($"{item.displayName.ToLowerInvariant()} {visitor.Available(item)}");
            }
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "<i>nada</i>";
        }

        string CostText(ItemStack[] cost, int crewRequired)
        {
            var parts = new List<string>();
            foreach (var c in cost)
            {
                if (!c.item) continue;
                string text = $"{c.amount} {c.item.displayName.ToLowerInvariant()}";
                parts.Add(visitor.Available(c.item) >= c.amount ? text : $"<color={Bad}>{text}</color>");
            }
            if (crewRequired > 0)
            {
                string crew = $"{crewRequired} tripulantes a bordo";
                parts.Add(visitor.CrewAboard >= crewRequired ? crew : $"<color={Bad}>{crew}</color>");
            }
            return "Custo: " + string.Join(", ", parts);
        }

        static void Feed(string text)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, text, new Color(0.95f, 0.92f, 0.8f));
        }
    }
}
