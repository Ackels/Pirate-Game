using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// Cais do porto-base: ponto de atracação com o armazém da base (Inventory sem limite no mesmo objeto) e o
    /// estaleiro, que constrói um upgrade ou um navio novo por vez. A obra só avança com o navio dentro da zona segura do porto
    /// (SafeZone no mesmo objeto). O navio do jogador usa tudo isso pelo HarborVisitor.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class HarborDock : MonoBehaviour
    {
        /// <summary>Upgrade ou navio novo em obra: o que foi pago fica guardado para devolver se cancelar.</summary>
        public class ConstructionJob
        {
            public UpgradeDefinition upgrade;
            /// <summary>Navio novo (no lugar de um upgrade): ao ficar pronto, o navio do jogador vira esta classe.</summary>
            public ShipClassDefinition shipClass;
            public int level;
            public ShipUpgrades ship;
            public ItemStack[] paid;
            public float duration;
            public float progress;

            public float Fraction => duration > 0f ? Mathf.Clamp01(progress / duration) : 1f;
            /// <summary>Nome da obra para o HUD ("Velame novo nível 2", "Navio novo: Escuna").</summary>
            public string Title => shipClass ? $"Navio novo: {shipClass.displayName}" : upgrade ? $"{upgrade.displayName} nível {level}" : "";
        }

        /// <summary>
        /// Encomenda ao ferreiro da Forja: anda mesmo com o navio longe (como as construções da base) e fica pronta na Forja,
        /// esperando o jogador retirar. Uma por vez; o que foi pago volta se cancelar.
        /// </summary>
        public class SmithyOrder
        {
            public HarborSettings.Recipe recipe;
            public int times;
            public ItemStack[] paid;
            public float duration;
            public float progress;

            public bool Ready => progress >= duration;
            public float Fraction => duration > 0f ? Mathf.Clamp01(progress / duration) : 1f;
            /// <summary>Quanto sai da forja (receita × vezes).</summary>
            public ItemStack Result => new(recipe.result.item, recipe.result.amount * times);
        }

        static readonly List<HarborDock> active = new();
        public static IReadOnlyList<HarborDock> Active => active;

        [SerializeField] string displayName = "Porto";
        [SerializeField] HarborSettings settings;
        [Tooltip("Distância (m) do ponto de atracação em que o navio pode atracar.")]
        [SerializeField] float dockRadius = 20f;
        [Tooltip("Velocidade dos trabalhadores do estaleiro (1 = normal, 2 = obras na metade do tempo). " +
                 "As construções da base (M3.3) aumentam isto.")]
        [SerializeField, Min(0.01f)] float buildSpeed = 1f;
        [Tooltip("Vaga do navio (relativa ao ponto de atracação): onde ele aparece ao carregar o jogo e no F2 do sandbox.")]
        [SerializeField] Vector3 berthOffset = new(8f, 0f, 4f);
        [SerializeField] float berthYaw;

        SafeZone zone;
        // Mercado: quanto ainda compra de cada item (cheio no começo; volta com o tempo).
        readonly Dictionary<ItemDefinition, float> demand = new();

        public string DisplayName => displayName;
        public HarborSettings Settings => settings;
        public float DockRadius => dockRadius;
        public Inventory Warehouse { get; private set; }
        public SafeZone Zone => zone;
        /// <summary>Construções da base (null se este porto não tem).</summary>
        public BaseBuildings Buildings { get; private set; }

        public float BuildSpeed
        {
            get => buildSpeed;
            set => buildSpeed = Mathf.Max(0.01f, value);
        }

        public ConstructionJob Job { get; private set; }
        public bool IsBusy => Job != null;
        /// <summary>A obra está andando (o navio está na zona do porto).</summary>
        public bool JobIsWorking { get; private set; }
        public float JobSecondsLeft => Job == null ? 0f : Mathf.Max(0f, Job.duration - Job.progress) / buildSpeed;
        /// <summary>Vaga do navio, ao lado do píer, parado e pronto para atracar (F).</summary>
        public Pose Berth => new(transform.position + berthOffset, Quaternion.Euler(0f, berthYaw, 0f));

        public event Action<ConstructionJob> ConstructionStarted;
        public event Action<ConstructionJob> ConstructionFinished;

        /// <summary>Encomenda da Forja (null = ferreiro livre).</summary>
        public SmithyOrder Smithy { get; private set; }
        public float SmithySecondsLeft => Smithy == null ? 0f : Mathf.Max(0f, Smithy.duration - Smithy.progress) / buildSpeed;
        public event Action<SmithyOrder> SmithyStarted;
        /// <summary>A encomenda ficou pronta (falta retirar na Forja).</summary>
        public event Action<SmithyOrder> SmithyReady;

        public void Configure(string harborName, HarborSettings harborSettings, float radius)
        {
            displayName = harborName;
            settings = harborSettings;
            dockRadius = radius;
        }

        void Awake()
        {
            Warehouse = GetComponent<Inventory>();
            zone = GetComponent<SafeZone>();
            Buildings = GetComponent<BaseBuildings>();
            // Esquema guardado no armazém conta como "já tem" no sorteio do baú raro.
            PlayerStorage.Register(Warehouse);
        }

        void OnDestroy() => PlayerStorage.Unregister(Warehouse);

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>Começa a obra (o pagamento já foi feito por quem chamou).</summary>
        public bool StartConstruction(UpgradeDefinition upgrade, ShipUpgrades ship, ItemStack[] paid, float seconds)
        {
            if (IsBusy || !upgrade || !ship) return false;
            Job = new ConstructionJob
            {
                upgrade = upgrade,
                level = ship.Level(upgrade) + 1,
                ship = ship,
                paid = paid,
                duration = Mathf.Max(0f, seconds),
            };
            ConstructionStarted?.Invoke(Job);
            if (Job.duration <= 0f) Complete();
            return true;
        }

        /// <summary>Começa a construir um navio novo (o pagamento já foi feito por quem chamou).</summary>
        public bool StartShipConstruction(ShipClassDefinition shipClass, ShipUpgrades ship, ItemStack[] paid, float seconds)
        {
            if (IsBusy || !shipClass || !ship) return false;
            Job = new ConstructionJob { shipClass = shipClass, ship = ship, paid = paid, duration = Mathf.Max(0f, seconds) };
            ConstructionStarted?.Invoke(Job);
            if (Job.duration <= 0f) Complete();
            return true;
        }

        /// <summary>Cancela a obra e devolve o que foi pago ao armazém.</summary>
        public void CancelConstruction()
        {
            if (Job == null) return;
            foreach (var stack in Job.paid)
                Warehouse.Add(stack.item, stack.amount);
            Job = null;
            JobIsWorking = false;
        }

        /// <summary>Carregar o jogo: a obra salva (ou nenhuma), sem o evento ConstructionStarted.</summary>
        public void RestoreJob(ConstructionJob job)
        {
            Job = job;
            JobIsWorking = false;
        }

        // ---------- Forja ----------

        /// <summary>Encomenda ao ferreiro (o pagamento já foi feito por quem chamou).</summary>
        public bool StartSmithy(HarborSettings.Recipe recipe, int times, ItemStack[] paid, float seconds)
        {
            if (Smithy != null || recipe?.result.item == null || times <= 0) return false;
            Smithy = new SmithyOrder { recipe = recipe, times = times, paid = paid, duration = Mathf.Max(0f, seconds) };
            SmithyStarted?.Invoke(Smithy);
            if (Smithy.Ready) SmithyReady?.Invoke(Smithy);
            return true;
        }

        /// <summary>Tira a encomenda pronta (quem chamou entrega o resultado). null se não está pronta.</summary>
        public SmithyOrder TakeSmithy()
        {
            if (Smithy == null || !Smithy.Ready) return null;
            var order = Smithy;
            Smithy = null;
            return order;
        }

        /// <summary>Cancela a encomenda que ainda não ficou pronta e devolve o que foi pago ao armazém.</summary>
        public void CancelSmithy()
        {
            if (Smithy == null || Smithy.Ready) return;
            foreach (var stack in Smithy.paid)
                Warehouse.Add(stack.item, stack.amount);
            Smithy = null;
        }

        /// <summary>Carregar o jogo: a encomenda salva (ou nenhuma), sem eventos.</summary>
        public void RestoreSmithy(SmithyOrder order) => Smithy = order;

        void UpdateSmithy(float dt)
        {
            if (Smithy == null || Smithy.Ready) return;
            Smithy.progress = Mathf.Min(Smithy.duration, Smithy.progress + dt * buildSpeed);
            if (Smithy.Ready) SmithyReady?.Invoke(Smithy);
        }

        /// <summary>Modo sandbox: termina a obra (e a encomenda da Forja) na hora.</summary>
        public void FinishNow()
        {
            if (Smithy != null && !Smithy.Ready) UpdateSmithy(float.MaxValue / 4f);
            if (Job != null) Complete();
        }

        /// <summary>Quantas unidades o mercado ainda compra deste item agora (sem limite: int.MaxValue).</summary>
        public int Demand(HarborSettings.MarketEntry entry)
        {
            if (entry == null || !entry.item || entry.demand <= 0) return int.MaxValue;
            return Mathf.FloorToInt(demand.TryGetValue(entry.item, out float left) ? left : entry.demand);
        }

        /// <summary>Itens com a demanda abaixo do cheio e quanto falta (para salvar: sair e voltar não pode encher de novo).</summary>
        public IReadOnlyDictionary<ItemDefinition, float> DemandLeft => demand;

        /// <summary>Carregar o jogo: a demanda salva (os itens que não estão aqui ficam cheios).</summary>
        public void RestoreDemand(IEnumerable<KeyValuePair<ItemDefinition, float>> saved)
        {
            demand.Clear();
            foreach (var (item, left) in saved)
                if (item) demand[item] = Mathf.Max(0f, left);
        }

        /// <summary>O mercado comprou <paramref name="amount"/>: a demanda cai.</summary>
        public void UseDemand(HarborSettings.MarketEntry entry, int amount)
        {
            if (entry == null || !entry.item || entry.demand <= 0) return;
            float left = demand.TryGetValue(entry.item, out float d) ? d : entry.demand;
            demand[entry.item] = Mathf.Max(0f, left - amount);
        }

        void RefillDemand(float dt)
        {
            if (!settings || settings.market.Length == 0 || demand.Count == 0) return;
            foreach (var entry in settings.market)
            {
                if (entry == null || !entry.item || !demand.TryGetValue(entry.item, out float left)) continue;
                left += dt / Mathf.Max(1f, entry.demandRefillSeconds);
                if (left >= entry.demand) demand.Remove(entry.item);
                else demand[entry.item] = left;
            }
        }

        void Update()
        {
            RefillDemand(Time.deltaTime);
            UpdateSmithy(Time.deltaTime);
            if (Job == null) return;
            if (!Job.ship)
            {
                Job = null;
                return;
            }
            JobIsWorking = !zone || zone.Contains(Job.ship.transform.position);
            if (!JobIsWorking) return;
            Job.progress += Time.deltaTime * buildSpeed;
            if (Job.progress >= Job.duration) Complete();
        }

        void Complete()
        {
            var job = Job;
            Job = null;
            JobIsWorking = false;
            if (job.shipClass)
            {
                // Navio novo sai do estaleiro com o casco cheio e os upgrades no zero (compra de novo, rendendo mais nele).
                if (job.ship.TryGetComponent(out PlayerShipClass hull)) hull.SetClass(job.shipClass, newShip: true);
            }
            else job.ship.AddLevel(job.upgrade);
            ConstructionFinished?.Invoke(job);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, dockRadius);
        }
    }
}
