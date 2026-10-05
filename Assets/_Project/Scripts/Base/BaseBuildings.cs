using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// Construções da base (no mesmo objeto do HarborDock): níveis, fila de obras (uma por vez, andam mesmo com o
    /// jogador longe), produção no armazém e efeitos (trabalhadores, zona segura). Cada construção aparece na ilha
    /// quando fica pronta (objetos visuais ligados por nível).
    /// </summary>
    [RequireComponent(typeof(HarborDock))]
    public class BaseBuildings : MonoBehaviour
    {
        [Serializable]
        public class BuildingVisual
        {
            public BuildingDefinition building;
            [Tooltip("Objeto ligado a partir de cada nível (índice 0 = nível 1).")]
            public GameObject[] perLevel = Array.Empty<GameObject>();
            [Tooltip("Objeto ligado só exatamente naquele nível (índice 0 = nível 1): ex. o anel de boias da zona segura de cada nível.")]
            public GameObject[] exactLevel = Array.Empty<GameObject>();
        }

        public class BuildJob
        {
            public BuildingDefinition building;
            public int level;
            public ItemStack[] paid;
            public float duration;
            public float progress;

            public float Fraction => duration > 0f ? Mathf.Clamp01(progress / duration) : 1f;
        }

        [Tooltip("Construções disponíveis, na ordem da lista.")]
        [SerializeField] BuildingDefinition[] available = Array.Empty<BuildingDefinition>();
        [SerializeField] BuildingVisual[] visuals = Array.Empty<BuildingVisual>();

        readonly Dictionary<BuildingDefinition, int> levels = new();
        readonly Dictionary<BuildingDefinition, float> productionTimers = new();
        HarborDock dock;
        SafeZone zone;
        float baseZoneRadius;
        CaptainFame fame;

        public IReadOnlyList<BuildingDefinition> Available => available;
        /// <summary>Segundos já contados para o próximo item de cada construção que produz (para salvar).</summary>
        public IReadOnlyDictionary<BuildingDefinition, float> ProductionTimers => productionTimers;
        public BuildJob Job { get; private set; }
        public bool IsBusy => Job != null;
        /// <summary>Velocidade dos trabalhadores (Alojamento). Vale para estas obras e para o estaleiro do navio.</summary>
        public float WorkerSpeed { get; private set; } = 1f;
        public float JobSecondsLeft => Job == null ? 0f : Mathf.Max(0f, Job.duration - Job.progress) / WorkerSpeed;

        public event Action<BuildingDefinition, int> Built;

        public void Configure(BuildingDefinition[] buildings, BuildingVisual[] buildingVisuals)
        {
            available = buildings;
            visuals = buildingVisuals;
        }

        public int Level(BuildingDefinition building) => building && levels.TryGetValue(building, out int n) ? n : 0;
        public bool Has(BuildingDefinition building) => Level(building) > 0;
        public bool IsMaxed(BuildingDefinition building) => Level(building) >= building.MaxLevel;

        public BuildingDefinition.Level NextLevel(BuildingDefinition building) =>
            IsMaxed(building) ? null : building.levels[Level(building)];

        void Awake()
        {
            dock = GetComponent<HarborDock>();
            zone = GetComponent<SafeZone>();
            if (zone) baseZoneRadius = zone.Radius;
        }

        void Start()
        {
            // Bônus de patente "mestre de obras" (M10): trabalhadores mais rápidos.
            fame = CaptainFame.Current;
            if (fame) fame.PerksChanged += Apply;
            Apply();
        }

        void OnDestroy()
        {
            if (fame) fame.PerksChanged -= Apply;
        }

        /// <summary>Começa a obra (o pagamento já foi feito por quem chamou).</summary>
        public bool StartConstruction(BuildingDefinition building, ItemStack[] paid, float seconds)
        {
            if (IsBusy || !building || IsMaxed(building)) return false;
            Job = new BuildJob { building = building, level = Level(building) + 1, paid = paid, duration = Mathf.Max(0f, seconds) };
            if (Job.duration <= 0f) Complete();
            return true;
        }

        /// <summary>Cancela a obra e devolve o que foi pago ao armazém.</summary>
        public void CancelConstruction()
        {
            if (Job == null) return;
            foreach (var stack in Job.paid) dock.Warehouse.Add(stack.item, stack.amount);
            Job = null;
        }

        /// <summary>Modo sandbox: termina a obra na hora.</summary>
        public void FinishNow()
        {
            if (Job != null) Complete();
        }

        /// <summary>Carregar o jogo: níveis, obra em andamento e produção salvos (sem o evento Built).</summary>
        public void Restore(IEnumerable<KeyValuePair<BuildingDefinition, int>> savedLevels, BuildJob job,
            IEnumerable<KeyValuePair<BuildingDefinition, float>> timers)
        {
            levels.Clear();
            foreach (var (building, level) in savedLevels)
                if (building && level > 0) levels[building] = Mathf.Min(level, building.MaxLevel);
            productionTimers.Clear();
            foreach (var (building, seconds) in timers)
                if (building) productionTimers[building] = Mathf.Max(0f, seconds);
            Job = job;
            Apply();
        }

        /// <summary>Modo sandbox: derruba tudo (nível 0) e cancela a obra sem devolver.</summary>
        public void ResetAll()
        {
            Job = null;
            levels.Clear();
            productionTimers.Clear();
            Apply();
        }

        void Update()
        {
            if (Job != null)
            {
                Job.progress += Time.deltaTime * WorkerSpeed;
                if (Job.progress >= Job.duration) Complete();
            }
            Produce(Time.deltaTime);
        }

        void Produce(float dt)
        {
            foreach (var building in available)
            {
                if (!building || building.effect != BuildingEffect.ProduceItem || !building.producedItem) continue;
                int level = Level(building);
                if (level <= 0) continue;
                float interval = Mathf.Max(0.5f, building.levels[level - 1].value);
                float t = (productionTimers.TryGetValue(building, out float v) ? v : 0f) + dt;
                while (t >= interval)
                {
                    t -= interval;
                    dock.Warehouse.Add(building.producedItem, 1);
                }
                productionTimers[building] = t;
            }
        }

        void Complete()
        {
            var job = Job;
            Job = null;
            levels[job.building] = Level(job.building) + 1;
            Apply();
            Built?.Invoke(job.building, levels[job.building]);
        }

        /// <summary>Recalcula os efeitos a partir dos níveis atuais e liga os visuais.</summary>
        void Apply()
        {
            float speed = 1f, zoneBonus = 0f;
            foreach (var building in available)
            {
                int level = Level(building);
                if (!building || level <= 0) continue;
                float value = building.levels[level - 1].value;
                if (building.effect == BuildingEffect.WorkerSpeed) speed *= Mathf.Max(0.01f, value);
                if (building.effect == BuildingEffect.CoastalDefense) zoneBonus += value;
            }
            speed *= 1f + CaptainFame.Bonus(PerkEffect.BuildSpeedPercent);
            WorkerSpeed = speed;
            dock.BuildSpeed = speed;
            if (zone) zone.Configure(baseZoneRadius + zoneBonus);

            foreach (var v in visuals)
            {
                if (v == null || !v.building) continue;
                int level = Level(v.building);
                for (int i = 0; i < v.perLevel.Length; i++)
                    if (v.perLevel[i]) v.perLevel[i].SetActive(i < level);
                if (v.exactLevel == null) continue;
                for (int i = 0; i < v.exactLevel.Length; i++)
                    if (v.exactLevel[i]) v.exactLevel[i].SetActive(i == level - 1);
            }
        }

        /// <summary>Texto do efeito atual e, com <paramref name="preview"/>, do próximo nível (para a tela do porto).</summary>
        public string EffectText(BuildingDefinition building, bool preview)
        {
            int level = Level(building) + (preview && !IsMaxed(building) ? 1 : 0);
            if (level <= 0) return "não construído";
            float value = building.levels[level - 1].value;
            return building.effect switch
            {
                BuildingEffect.WorkerSpeed => $"trabalhadores {value:0.##}×",
                // Valor = nível dos upgrades do navio que fica liberado (o Estaleiro 1 antigo tinha 0: nível 3).
                BuildingEffect.Unlock => $"nível {(value > 0f ? value : 3f):0} dos upgrades do navio liberado",
                BuildingEffect.ProduceItem => building.producedItem ? $"1 {building.producedItem.displayName.ToLowerInvariant()} a cada {value:0} s" : "-",
                BuildingEffect.CoastalDefense => $"canhões defendem o porto, zona segura +{value:0} m",
                _ => "",
            };
        }
    }
}
