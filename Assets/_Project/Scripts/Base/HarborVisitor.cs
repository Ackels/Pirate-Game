using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// Fica no navio do jogador: perto do cais e devagar, atraca no porto. Lá descarrega o porão no
    /// armazém da base (sem limite), carrega de volta o que precisar, conserta o casco com madeira e ouro
    /// e compra upgrades (ShipUpgrades); contrata marinheiros para a tripulação (ShipCrew, na taverna). Os pagamentos saem
    /// primeiro do armazém, depois do porão.
    /// Com <c>menuOnDock</c> (cena 05), F atraca e abre a janela do porto com tudo; sem ele (cena 07), quem atraca é o
    /// "atracar e desembarcar" (HarborShoreLeave) e cada parte do porto abre a pé, na casa dela (HarborBuildingVisitor).
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(ShipController), typeof(Health))]
    public class HarborVisitor : MonoBehaviour, IInteraction
    {
        [Tooltip("Velocidade máxima (nós) para atracar.")]
        [SerializeField] float maxSpeedKnots = 3f;
        [Tooltip("Folga antes de desatracar sozinho ao se afastar ou acelerar (m e nós).")]
        [SerializeField] float closeMargin = 5f;
        [Tooltip("F atraca e abre a janela do porto (sem personagem a pé). Desligado: o porto se usa a pé, nas casas.")]
        [SerializeField] bool menuOnDock = true;

        Inventory hold;
        ShipController ship;
        Health health;
        ShipUpgrades upgrades;
        ShipCrew crew;
        PlayerShipClass shipClass;

        public Inventory Hold => hold;
        /// <summary>Classes de navio (null se o navio não troca de casco).</summary>
        public PlayerShipClass ShipClass => shipClass;
        /// <summary>Tripulação do navio (null se o navio não tem o componente).</summary>
        public ShipCrew Crew => crew;
        /// <summary>Upgrades do navio (null se o navio não tem o componente).</summary>
        public ShipUpgrades Upgrades => upgrades;
        public Health Hull => health;
        public float MaxSpeedKnots => maxSpeedKnots;
        public float SpeedKnots => ship.SpeedKnots;
        /// <summary>Cais ao alcance (null se nenhum).</summary>
        public HarborDock Nearby { get; private set; }
        /// <summary>Cais em que o navio está atracado (null se não está).</summary>
        public HarborDock Current { get; private set; }
        public bool IsDocked => Current;
        /// <summary>Janela do porto aberta no navio (só com menuOnDock; a pé, as casas abrem cada parte).</summary>
        public bool IsOpen => menuOnDock && Current;
        public bool MenuOnDock => menuOnDock;
        public bool TooFast => ship.SpeedKnots > maxSpeedKnots;
        /// <summary>Perto do cais, devagar e inteiro: dá para atracar.</summary>
        public bool CanDock => Nearby && !TooFast && !health.IsDead;
        public bool CanOpen => menuOnDock && CanDock;
        public float MissingHull => health.Max - health.Current;

        /// <summary>Item que foi para o armazém (true) ou voltou para o porão (false).</summary>
        public event Action<ItemStack, bool> Transferred;
        /// <summary>Vida recuperada, madeira e ouro gastos.</summary>
        public event Action<float, int, int> Repaired;
        /// <summary>Mercado: item comprado (quantidade positiva) ou vendido (negativa) e o ouro (gasto negativo, recebido positivo).</summary>
        public event Action<ItemStack, int> Traded;
        /// <summary>Ferreiro: o que foi retirado da Forja (pronto).</summary>
        public event Action<ItemStack> Crafted;
        /// <summary>Ferreiro: encomenda feita (o que vai sair).</summary>
        public event Action<ItemStack> CraftOrdered;
        /// <summary>Cozinha da taverna: prato feito (já no porão).</summary>
        public event Action<ItemStack> Cooked;
        /// <summary>Atracou e saiu do porto. O jogo salva nos dois.</summary>
        public event Action<HarborDock> Docked, Undocked;

        public void Configure(bool openMenuOnDock) => menuOnDock = openMenuOnDock;

        void Awake()
        {
            hold = GetComponent<Inventory>();
            ship = GetComponent<ShipController>();
            health = GetComponent<Health>();
            TryGetComponent(out upgrades);
            TryGetComponent(out crew);
            TryGetComponent(out shipClass);
        }

        void Update()
        {
            Nearby = FindNearest();
            if (!Current) return;
            bool lost = !Current.isActiveAndEnabled || health.IsDead
                        || Distance(Current) > Current.DockRadius + closeMargin
                        || ship.SpeedKnots > maxSpeedKnots + closeMargin;
            if (lost) Close();
        }

        // Sem o evento: desligar (sair do Play, trocar de cena) não é sair do porto, e não pode salvar no meio da descarga da cena.
        void OnDisable() => Current = null;

        public bool Open() => menuOnDock && Dock(false);

        /// <summary>
        /// Atraca no cais ao alcance (já atracado nele: só devolve true). Com <paramref name="unloadHold"/>, o porão vai
        /// inteiro para o armazém ao chegar (antes de salvar).
        /// </summary>
        public bool Dock(bool unloadHold)
        {
            if (!CanDock) return false;
            if (Current == Nearby) return true;
            if (Current) Close();
            Current = Nearby;
            if (unloadHold) UnloadAll();
            Docked?.Invoke(Current);
            return true;
        }

        public void Close()
        {
            var left = Current;
            Current = null;
            if (left) Undocked?.Invoke(left);
        }

        // ---------- Porão ↔ armazém ----------

        /// <summary>Porão → armazém. Tripulação não desembarca (fica a bordo).</summary>
        public int Unload(ItemDefinition item, int amount)
        {
            if (!Current || !item || item.category == ItemCategory.Crew) return 0;
            int moved = Current.Warehouse.Add(item, Mathf.Min(amount, hold.Count(item)));
            if (moved <= 0) return 0;
            hold.Remove(item, moved);
            Transferred?.Invoke(new ItemStack(item, moved), true);
            return moved;
        }

        /// <summary>Armazém → porão, até onde couber. Esquemas ficam na base (são para construir lá).</summary>
        public int Load(ItemDefinition item, int amount)
        {
            if (!Current || !item || item.IsUnique) return 0;
            int moved = hold.Add(item, Mathf.Min(amount, Current.Warehouse.Count(item)));
            if (moved <= 0) return 0;
            Current.Warehouse.Remove(item, moved);
            Transferred?.Invoke(new ItemStack(item, moved), false);
            return moved;
        }

        /// <summary>Descarrega o porão no armazém, deixando a bordo o que o item pede (ItemDefinition.keepAboard: munição, madeira).</summary>
        public void UnloadAll()
        {
            if (!Current) return;
            foreach (var item in new List<ItemDefinition>(hold.Items))
            {
                int amount = hold.Count(item) - item.keepAboard;
                if (amount > 0) Unload(item, amount);
            }
        }

        // ---------- Conserto ----------

        /// <summary>Quanto há de um item somando armazém e porão (o conserto gasta do armazém primeiro).</summary>
        public int Available(ItemDefinition item) =>
            !item ? 0 : hold.Count(item) + (Current ? Current.Warehouse.Count(item) : 0);

        /// <summary>Custo para consertar <paramref name="hull"/> de vida (cobrado a cada 10, arredondando para cima).</summary>
        public (int wood, int gold) RepairCost(float hull)
        {
            var s = Current ? Current.Settings : null;
            if (!s) return (0, 0);
            int steps = RepairSteps(hull);
            return (steps * s.woodPer10Hull, steps * s.goldPer10Hull);
        }

        /// <summary>Quanto de vida dá para consertar com o que há, até <paramref name="hull"/>.</summary>
        public float AffordableRepair(float hull)
        {
            var s = Current ? Current.Settings : null;
            if (!s) return 0f;
            int steps = RepairSteps(hull);
            if (s.woodPer10Hull > 0) steps = Mathf.Min(steps, Available(s.repairWood) / s.woodPer10Hull);
            if (s.goldPer10Hull > 0) steps = Mathf.Min(steps, Available(s.repairGold) / s.goldPer10Hull);
            return Mathf.Min(steps * 10f, Mathf.Min(hull, MissingHull));
        }

        /// <summary>Conserta até <paramref name="hull"/> de vida, o quanto der para pagar. Devolve a vida recuperada.</summary>
        public float Repair(float hull)
        {
            float amount = AffordableRepair(hull);
            if (amount <= 0f) return 0f;
            var (wood, gold) = RepairCost(amount);
            var s = Current.Settings;
            Spend(s.repairWood, wood);
            Spend(s.repairGold, gold);
            health.Heal(amount);
            Repaired?.Invoke(amount, wood, gold);
            return amount;
        }

        // ---------- Upgrades ----------

        /// <summary>Tripulantes a bordo (sobreviventes e marinheiros; exigidos por alguns upgrades, não são gastos).</summary>
        public int CrewAboard
        {
            get
            {
                int n = 0;
                var items = hold.Items;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].category == ItemCategory.Crew) n += hold.Count(items[i]);
                return n;
            }
        }

        /// <summary>Por que não dá para comprar o próximo nível agora (null = pode comprar).</summary>
        public string BuyBlockReason(UpgradeDefinition upgrade)
        {
            if (!Current) return "atraque no porto";
            if (!upgrades || !upgrade) return "indisponível";
            var next = upgrades.NextLevel(upgrade);
            if (next == null) return "nível máximo";
            if (Current.IsBusy) return Current.Job.upgrade == upgrade ? "em obra" : "estaleiro ocupado";
            if (next.requiredBuilding && !(Current.Buildings && Current.Buildings.Level(next.requiredBuilding) >= next.requiredBuildingLevel))
                return next.requiredBuildingLevel > 1
                    ? $"precisa de {next.requiredBuilding.displayName} nível {next.requiredBuildingLevel}"
                    : $"precisa de {next.requiredBuilding.displayName}";
            if (CrewAboard < next.crewRequired) return $"precisa de {next.crewRequired} tripulantes a bordo";
            foreach (var c in upgrades.NextCost(upgrade))
                if (c.item && Available(c.item) < c.amount) return $"falta {c.item.displayName.ToLowerInvariant()}";
            return null;
        }

        /// <summary>
        /// Paga e encomenda o próximo nível ao estaleiro do porto. Fica pronto depois do tempo de obra
        /// (só conta com o navio na zona do porto).
        /// </summary>
        public bool BuyUpgrade(UpgradeDefinition upgrade)
        {
            if (BuyBlockReason(upgrade) != null) return false;
            // Custo e obra do navio atual (navio maior, upgrade mais caro).
            var cost = upgrades.NextCost(upgrade);
            foreach (var c in cost) Spend(c.item, c.amount);
            return Current.StartConstruction(upgrade, upgrades, cost, upgrades.NextBuildSeconds(upgrade));
        }

        /// <summary>Cancela a obra do porto atracado; o custo volta para o armazém.</summary>
        public void CancelConstruction()
        {
            if (Current) Current.CancelConstruction();
        }

        // ---------- Navio novo (estaleiro) ----------

        /// <summary>Por que não dá para construir este navio agora (null = pode).</summary>
        public string ShipBlockReason(ShipClassDefinition target)
        {
            if (!Current) return "atraque no porto";
            if (!shipClass || !upgrades || !target) return "indisponível";
            var mine = shipClass.Current;
            if (target == mine) return "é o seu navio";
            if (mine && target.tier <= mine.tier) return "seu navio já é maior";
            if (Current.IsBusy) return Current.Job.shipClass == target ? "em obra" : "estaleiro ocupado";
            int level = Mathf.Max(1, target.requiredBuildingLevel);
            if (target.requiredBuilding && !(Current.Buildings && Current.Buildings.Level(target.requiredBuilding) >= level))
                return level > 1 ? $"precisa de {target.requiredBuilding.displayName} nível {level}" : $"precisa de {target.requiredBuilding.displayName}";
            if (!HasSchematic(target.schematic)) return $"precisa do esquema {target.schematic.displayName}";
            foreach (var c in target.cost)
                if (c.item && Available(c.item) < c.amount) return $"falta {c.item.displayName.ToLowerInvariant()}";
            return null;
        }

        /// <summary>Paga e encomenda o navio ao estaleiro. Fica pronto depois do tempo de obra (só com o navio na zona do porto).</summary>
        public bool BuyShip(ShipClassDefinition target)
        {
            if (ShipBlockReason(target) != null) return false;
            foreach (var c in target.cost) Spend(c.item, c.amount);
            return Current.StartShipConstruction(target, upgrades, (ItemStack[])target.cost.Clone(), target.buildSeconds);
        }

        // ---------- Mercado (praça) ----------

        ItemDefinition Gold => Current && Current.Settings ? Current.Settings.repairGold : null;

        /// <summary>Preço que o mercado cobra agora (bônus de patente "negociante": metade do bônus de desconto; M10).</summary>
        public int BuyPrice(HarborSettings.MarketEntry entry) =>
            entry == null || entry.buyPrice <= 0 ? 0
                : Mathf.Max(1, Mathf.RoundToInt(entry.buyPrice * (1f - 0.5f * CaptainFame.Bonus(PerkEffect.MarketPercent))));

        /// <summary>Quanto o mercado paga agora (bônus de patente "negociante": + o bônus; M10).</summary>
        public int SellPrice(HarborSettings.MarketEntry entry) =>
            entry == null || entry.sellPrice <= 0 ? 0
                : Mathf.RoundToInt(entry.sellPrice * (1f + CaptainFame.Bonus(PerkEffect.MarketPercent)));

        /// <summary>Por que o mercado não vende isto agora (patente baixa, esquema que já tem; null = vende).</summary>
        public string MarketBlockReason(HarborSettings.MarketEntry entry)
        {
            if (entry?.item == null || entry.buyPrice <= 0) return "não vende";
            if (entry.item.IsUnique && PlayerStorage.Has(entry.item)) return "você já tem";
            // Mochila: não vende a menor (ou igual) para quem já tem uma maior.
            if (entry.item.backpackCapacity > 0f && Current && Current.Settings)
                foreach (var other in Current.Settings.market)
                    if (other?.item && other.item != entry.item && other.item.backpackCapacity >= entry.item.backpackCapacity && PlayerStorage.Has(other.item))
                        return "você já tem uma maior";
            var fame = CaptainFame.Current;
            int rank = fame ? fame.Rank : 0;
            if (entry.requiredRank > rank)
                return $"patente {(fame ? fame.RankTitle(entry.requiredRank) : entry.requiredRank.ToString())}";
            return null;
        }

        /// <summary>Quantas unidades dá para comprar com o ouro (armazém + porão); esquema, uma só.</summary>
        public int MaxBuy(HarborSettings.MarketEntry entry)
        {
            if (!Current || !Gold || MarketBlockReason(entry) != null) return 0;
            int n = Available(Gold) / BuyPrice(entry);
            return entry.item.IsUnique ? Mathf.Min(1, n) : n;
        }

        /// <summary>Quantas unidades dá para vender agora (armazém + porão, até a demanda do mercado).</summary>
        public int MaxSell(HarborSettings.MarketEntry entry) =>
            !Current || !Gold || entry?.item == null || entry.sellPrice <= 0 ? 0 : Mathf.Min(Available(entry.item), Current.Demand(entry));

        /// <summary>Compra até <paramref name="amount"/> (o quanto der para pagar). Devolve quantas vieram.</summary>
        public int Buy(HarborSettings.MarketEntry entry, int amount)
        {
            amount = Mathf.Min(amount, MaxBuy(entry));
            if (amount <= 0) return 0;
            int price = BuyPrice(entry);
            Spend(Gold, amount * price);
            Deliver(entry.item, amount, entry.toHold);
            Traded?.Invoke(new ItemStack(entry.item, amount), -amount * price);
            return amount;
        }

        /// <summary>Vende até <paramref name="amount"/> (sai do armazém primeiro); o ouro vai para o armazém.</summary>
        public int Sell(HarborSettings.MarketEntry entry, int amount)
        {
            amount = Mathf.Min(amount, MaxSell(entry));
            if (amount <= 0) return 0;
            int price = SellPrice(entry);
            Spend(entry.item, amount);
            Current.UseDemand(entry, amount);
            Current.Warehouse.Add(Gold, amount * price);
            Traded?.Invoke(new ItemStack(entry.item, -amount), amount * price);
            return amount;
        }

        // ---------- Ferreiro (Forja) ----------

        /// <summary>Por que não dá para fazer agora (null = pode).</summary>
        public string CraftBlockReason(HarborSettings.Recipe recipe)
        {
            if (!Current) return "atraque no porto";
            if (recipe?.result.item == null) return "indisponível";
            if (!HasBuilding(recipe)) return recipe.requiredLevel > 1
                ? $"precisa de {recipe.requiredBuilding.displayName} nível {recipe.requiredLevel}"
                : $"precisa de {recipe.requiredBuilding.displayName}";
            if (Current.Smithy != null) return Current.Smithy.Ready ? "retire a encomenda pronta" : "ferreiro ocupado";
            // Peça única (relíquia): uma só.
            if (recipe.result.item.IsUnique && PlayerStorage.Count(recipe.result.item) > 0) return "você já tem";
            foreach (var c in recipe.cost)
                if (c.item && Available(c.item) < c.amount) return $"falta {c.item.displayName.ToLowerInvariant()}";
            return null;
        }

        /// <summary>Quantas vezes dá para fazer com o que há (armazém + porão).</summary>
        public const int MaxCraftOrder = 10;

        public int MaxCraft(HarborSettings.Recipe recipe)
        {
            if (!Current || recipe?.result.item == null || !HasBuilding(recipe) || Current.Smithy != null) return 0;
            // Até 10 receitas por encomenda (sem isso, "Fazer máx." com o armazém cheio virava horas de forja); peça única, uma.
            int n = recipe.result.item.IsUnique ? (PlayerStorage.Count(recipe.result.item) > 0 ? 0 : 1) : MaxCraftOrder;
            foreach (var c in recipe.cost)
                if (c.item && c.amount > 0) n = Mathf.Min(n, Available(c.item) / c.amount);
            return n;
        }

        /// <summary>Faz a receita até <paramref name="times"/> vezes (o quanto der). Devolve quantas.</summary>
        /// <summary>
        /// Encomenda <paramref name="times"/> receitas ao ferreiro (o quanto der para pagar; paga na hora). Fica pronto depois de
        /// <c>craftSeconds × times</c> (o Alojamento acelera), mesmo com o navio longe; retira na Forja (CollectCraft).
        /// </summary>
        public int Craft(HarborSettings.Recipe recipe, int times)
        {
            times = Mathf.Min(times, MaxCraft(recipe));
            if (times <= 0) return 0;
            var paid = new ItemStack[recipe.cost.Length];
            for (int i = 0; i < paid.Length; i++)
            {
                var c = recipe.cost[i];
                Spend(c.item, c.amount * times);
                paid[i] = new ItemStack(c.item, c.amount * times);
            }
            if (!Current.StartSmithy(recipe, times, paid, recipe.craftSeconds * times)) return 0;
            CraftOrdered?.Invoke(new ItemStack(recipe.result.item, recipe.result.amount * times));
            return times;
        }

        /// <summary>Tempo de forja (trabalhadores de agora) para <paramref name="times"/> receitas.</summary>
        public float CraftSeconds(HarborSettings.Recipe recipe, int times) =>
            recipe == null ? 0f : recipe.craftSeconds * times / (Current ? Current.BuildSpeed : 1f);

        /// <summary>Retira a encomenda pronta: munição para o porão até caber, o resto para o armazém. Devolve o que saiu.</summary>
        public ItemStack CollectCraft()
        {
            var order = Current ? Current.TakeSmithy() : null;
            if (order == null) return default;
            var result = order.Result;
            Deliver(result.item, result.amount, order.recipe.toHold);
            Crafted?.Invoke(result);
            return result;
        }

        public void CancelCraft()
        {
            if (Current) Current.CancelSmithy();
        }

        bool HasBuilding(HarborSettings.Recipe recipe) =>
            !recipe.requiredBuilding || (Current.Buildings && Current.Buildings.Level(recipe.requiredBuilding) >= recipe.requiredLevel);

        /// <summary>Munição vai para o porão até onde couber; o resto (e o que não é munição) vai para o armazém.</summary>
        void Deliver(ItemDefinition item, int amount, bool toHold)
        {
            if (toHold) amount -= hold.Add(item, amount);
            if (amount > 0) Current.Warehouse.Add(item, amount);
        }

        // ---------- Cozinha (taverna, D2 30/10) ----------

        /// <summary>Por que não dá para cozinhar agora (null = pode).</summary>
        public string CookBlockReason(HarborSettings.Recipe recipe)
        {
            if (!Current) return "atraque no porto";
            if (recipe?.result.item == null) return "indisponível";
            foreach (var c in recipe.cost)
                if (c.item && Available(c.item) < c.amount) return $"falta {c.item.displayName.ToLowerInvariant()}";
            return null;
        }

        /// <summary>Quantos pratos dá para fazer com o que há (armazém + porão), até 10 de uma vez.</summary>
        public int MaxCook(HarborSettings.Recipe recipe)
        {
            if (!Current || recipe?.result.item == null) return 0;
            int n = MaxCraftOrder;
            foreach (var c in recipe.cost)
                if (c.item && c.amount > 0) n = Mathf.Min(n, Available(c.item) / c.amount);
            return n;
        }

        /// <summary>Cozinha na hora até <paramref name="times"/> receitas (paga do armazém e do porão); o prato vai para o porão. Devolve quantas.</summary>
        public int Cook(HarborSettings.Recipe recipe, int times)
        {
            times = Mathf.Min(times, MaxCook(recipe));
            if (times <= 0) return 0;
            foreach (var c in recipe.cost) Spend(c.item, c.amount * times);
            var result = new ItemStack(recipe.result.item, recipe.result.amount * times);
            Deliver(result.item, result.amount, true);
            Cooked?.Invoke(result);
            return times;
        }

        // ---------- Tripulação (taverna) ----------

        /// <summary>Quantos marinheiros dá para contratar agora: beliches livres e dinheiro (armazém + porão).</summary>
        public int RecruitAffordable
        {
            get
            {
                if (!Current || !crew || !crew.CrewItem) return 0;
                int n = crew.FreeBerths;
                foreach (var c in crew.Settings.recruitCost)
                    if (c.item && c.amount > 0) n = Mathf.Min(n, Available(c.item) / c.amount);
                return n;
            }
        }

        /// <summary>Por que não dá para contratar agora (null = pode).</summary>
        public string RecruitBlockReason()
        {
            if (!Current) return "atraque no porto";
            if (!crew || !crew.CrewItem) return "indisponível";
            if (crew.FreeBerths <= 0) return "beliches cheios";
            foreach (var c in crew.Settings.recruitCost)
                if (c.item && Available(c.item) < c.amount) return $"falta {c.item.displayName.ToLowerInvariant()}";
            return null;
        }

        /// <summary>Contrata até <paramref name="amount"/> marinheiros (o quanto couber e der para pagar). Devolve quantos vieram.</summary>
        public int Recruit(int amount)
        {
            amount = Mathf.Min(amount, RecruitAffordable);
            if (amount <= 0) return 0;
            foreach (var c in crew.Settings.recruitCost) Spend(c.item, c.amount * amount);
            return hold.Add(crew.CrewItem, amount);
        }

        // ---------- Construções da base ----------

        /// <summary>Esquema guardado no armazém do porto atracado ou no porão.</summary>
        public bool HasSchematic(ItemDefinition schematic) =>
            !schematic || hold.Has(schematic) || (Current && Current.Warehouse.Has(schematic));

        /// <summary>Por que não dá para construir o próximo nível agora (null = pode).</summary>
        public string BuildingBlockReason(BuildingDefinition building)
        {
            if (!Current) return "atraque no porto";
            var buildings = Current.Buildings;
            if (!buildings || !building) return "indisponível";
            var next = buildings.NextLevel(building);
            if (next == null) return "nível máximo";
            if (!HasSchematic(building.schematic)) return $"precisa do esquema {building.schematic.displayName}";
            if (buildings.IsBusy) return buildings.Job.building == building ? "em obra" : "obras ocupadas";
            foreach (var c in next.cost)
                if (c.item && Available(c.item) < c.amount) return $"falta {c.item.displayName.ToLowerInvariant()}";
            return null;
        }

        /// <summary>Paga e manda os trabalhadores construírem o próximo nível (anda mesmo com o navio longe).</summary>
        public bool BuyBuilding(BuildingDefinition building)
        {
            if (BuildingBlockReason(building) != null) return false;
            var buildings = Current.Buildings;
            var next = buildings.NextLevel(building);
            foreach (var c in next.cost) Spend(c.item, c.amount);
            return buildings.StartConstruction(building, (ItemStack[])next.cost.Clone(), next.buildSeconds);
        }

        public void CancelBuilding()
        {
            if (Current && Current.Buildings) Current.Buildings.CancelConstruction();
        }

        int RepairSteps(float hull) => Mathf.CeilToInt(Mathf.Min(hull, MissingHull) / 10f - 1e-4f);

        void Spend(ItemDefinition item, int amount)
        {
            if (!item || amount <= 0) return;
            amount -= Current.Warehouse.Remove(item, amount);
            hold.Remove(item, amount);
        }

        HarborDock FindNearest()
        {
            HarborDock best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < HarborDock.Active.Count; i++)
            {
                var dock = HarborDock.Active[i];
                float d = Distance(dock);
                if (d > dock.DockRadius || d > bestDistance) continue;
                best = dock;
                bestDistance = d;
            }
            return best;
        }

        float Distance(HarborDock dock)
        {
            Vector3 d = dock.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
