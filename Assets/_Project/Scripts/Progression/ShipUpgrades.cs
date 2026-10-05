using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Níveis de upgrade do navio e aplicação nos stats. O navio passa a usar cópias em memória do ShipStats e
    /// do CannonStats: os assets em Data/ nunca mudam (no editor, mexer neles em Play ficaria salvo).
    /// Cada compra recalcula as cópias a partir dos valores base. A tripulação (ShipCrew, se houver) entra junto: recarga,
    /// aceleração e giro mudam com quantos há a bordo. Os bônus de patente do capitão (CaptainFame, M10) multiplicam dano,
    /// velocidade e casco por cima de tudo.
    /// </summary>
    [RequireComponent(typeof(ShipController), typeof(Health))]
    public class ShipUpgrades : MonoBehaviour
    {
        [Tooltip("Upgrades à venda no porto, na ordem da lista.")]
        [SerializeField] UpgradeDefinition[] available = Array.Empty<UpgradeDefinition>();

        readonly Dictionary<UpgradeDefinition, int> levels = new();
        ShipController ship;
        Health health;
        ShipCrew crew;
        CannonBattery[] batteries;
        ShipStats baseShip, shipCopy;
        CannonStats baseCannons, cannonCopy;
        float baseHull;
        bool initialized;
        ShipClassDefinition shipClass;
        CaptainFame fame;

        public IReadOnlyList<UpgradeDefinition> Available => available;

        public event Action<UpgradeDefinition, int> Upgraded;

        public void Configure(UpgradeDefinition[] upgrades) => available = upgrades;

        public int Level(UpgradeDefinition upgrade) => upgrade && levels.TryGetValue(upgrade, out int n) ? n : 0;
        public bool IsMaxed(UpgradeDefinition upgrade) => Level(upgrade) >= upgrade.MaxLevel;

        /// <summary>Próximo nível à venda (null se já está no máximo).</summary>
        public UpgradeDefinition.Level NextLevel(UpgradeDefinition upgrade) =>
            IsMaxed(upgrade) ? null : upgrade.levels[Level(upgrade)];

        /// <summary>Custo e obra crescem com o navio (ShipClassDefinition.upgradeCostMultiplier).</summary>
        public float CostMultiplier => shipClass ? shipClass.upgradeCostMultiplier : 1f;

        /// <summary>Custo do próximo nível neste navio (arredondado para cima; null se já está no máximo).</summary>
        public ItemStack[] NextCost(UpgradeDefinition upgrade)
        {
            var next = NextLevel(upgrade);
            if (next == null) return null;
            var cost = new ItemStack[next.cost.Length];
            for (int i = 0; i < cost.Length; i++)
                cost[i] = new ItemStack(next.cost[i].item, Mathf.CeilToInt(next.cost[i].amount * CostMultiplier - 1e-4f));
            return cost;
        }

        /// <summary>Segundos de obra do próximo nível neste navio (velocidade normal dos trabalhadores).</summary>
        public float NextBuildSeconds(UpgradeDefinition upgrade)
        {
            var next = NextLevel(upgrade);
            return next == null ? 0f : next.buildSeconds * CostMultiplier;
        }

        void Start() => EnsureInitialized();

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            ship = GetComponent<ShipController>();
            health = GetComponent<Health>();
            batteries = GetComponentsInChildren<CannonBattery>();
            baseShip = ship.Stats;
            baseCannons = batteries.Length > 0 ? batteries[0].Stats : null;
            baseHull = health.Max;
            if (TryGetComponent(out crew)) crew.Changed += Apply;
            fame = CaptainFame.Current;
            if (fame) fame.PerksChanged += Apply;
            Apply();
        }

        /// <summary>
        /// Navio novo (PlayerShipClass): outros valores base (stats, canhões, casco), outros canhões e quanto rendem e custam
        /// os upgrades nele. Quem troca de navio no estaleiro zera os níveis antes (ResetLevels).
        /// </summary>
        public void SetBase(ShipClassDefinition shipClassDefinition)
        {
            EnsureInitialized();
            shipClass = shipClassDefinition;
            if (shipClass.stats) baseShip = shipClass.stats;
            if (shipClass.cannons) baseCannons = shipClass.cannons;
            baseHull = shipClass.hullMax;
            batteries = GetComponentsInChildren<CannonBattery>();
            Apply();
        }

        /// <summary>Sobe um nível (o pagamento é feito por quem vende, ex.: o porto).</summary>
        public void AddLevel(UpgradeDefinition upgrade)
        {
            EnsureInitialized();
            if (!upgrade || IsMaxed(upgrade)) return;
            levels[upgrade] = Level(upgrade) + 1;
            Apply();
            Upgraded?.Invoke(upgrade, Level(upgrade));
        }

        /// <summary>Carregar o jogo: troca todos os níveis pelos salvos (sem o evento Upgraded).</summary>
        public void SetLevels(IEnumerable<KeyValuePair<UpgradeDefinition, int>> saved)
        {
            EnsureInitialized();
            levels.Clear();
            foreach (var (upgrade, level) in saved)
                if (upgrade && level > 0) levels[upgrade] = Mathf.Min(level, upgrade.MaxLevel);
            Apply();
        }

        /// <summary>Volta todos os upgrades ao nível 0 (modo sandbox).</summary>
        public void ResetLevels()
        {
            EnsureInitialized();
            levels.Clear();
            Apply();
        }

        /// <summary>
        /// Efeito total de um tipo de stat somando todos os upgrades comprados (+ um nível de prévia), no quanto rendem neste
        /// navio (multiplicador de cada tipo na classe do navio).
        /// </summary>
        float Total(UpgradeStat stat, UpgradeDefinition preview = null)
        {
            float total = 0f;
            foreach (var u in available)
            {
                if (!u || u.stat != stat) continue;
                int level = Level(u) + (u == preview && !IsMaxed(u) ? 1 : 0);
                if (level > 0) total += u.levels[level - 1].value;
            }
            return total * EffectMultiplier(stat);
        }

        float EffectMultiplier(UpgradeStat stat) => shipClass ? shipClass.UpgradeMultiplier(stat) : 1f;

        void Apply()
        {
            if (shipCopy) Destroy(shipCopy);
            shipCopy = Instantiate(baseShip);
            shipCopy.name = baseShip.name + " (upgrades)";
            shipCopy.maxSpeed = baseShip.maxSpeed * (1f + Total(UpgradeStat.MaxSpeedPercent) / 100f) * (1f + Perk(PerkEffect.MaxSpeedPercent));
            float handling = crew ? crew.HandlingMultiplier : 1f;
            shipCopy.acceleration = baseShip.acceleration * handling;
            shipCopy.turnRate = baseShip.turnRate * handling;
            ship.Configure(shipCopy);

            if (baseCannons)
            {
                if (cannonCopy) Destroy(cannonCopy);
                cannonCopy = Instantiate(baseCannons);
                cannonCopy.name = baseCannons.name + " (upgrades)";
                cannonCopy.damagePerBall = (baseCannons.damagePerBall + Total(UpgradeStat.CannonDamage)) * (1f + Perk(PerkEffect.CannonDamagePercent));
                cannonCopy.reloadTime = ReloadTime(null);
                float assist = Mathf.Clamp(Total(UpgradeStat.AimAssistPercent), 0f, 100f);
                cannonCopy.aimAssist = Mathf.Max(baseCannons.aimAssist, assist / 100f);
                cannonCopy.spreadDegrees = baseCannons.spreadDegrees * (1f - assist / 200f) * (1f - CappedSpreadReduction(null));
                foreach (var battery in batteries) battery.SetStats(cannonCopy);
            }

            health.SetMax((baseHull + Total(UpgradeStat.HullMax)) * (1f + Perk(PerkEffect.HullPercent)));
        }

        float Perk(PerkEffect effect) => fame ? fame.PerkValue(effect) : 0f;

        /// <summary>Texto do efeito atual e, com <paramref name="preview"/>, do próximo nível (para a loja).</summary>
        public string EffectText(UpgradeDefinition upgrade, bool preview)
        {
            EnsureInitialized();
            var p = preview ? upgrade : null;
            return upgrade.stat switch
            {
                UpgradeStat.HullMax => $"casco máx. {baseHull + Total(UpgradeStat.HullMax, p):0}",
                UpgradeStat.CannonDamage => baseCannons ? $"dano por bala {baseCannons.damagePerBall + Total(UpgradeStat.CannonDamage, p):0}" : "-",
                UpgradeStat.MaxSpeedPercent => $"velocidade máx. +{Total(UpgradeStat.MaxSpeedPercent, p):0}%",
                UpgradeStat.ReloadPercent => !baseCannons ? "-" : CappedSpreadReduction(p) > 0f
                    ? $"recarga {ReloadTime(p):0.0} s (limite) · espalhamento -{CappedSpreadReduction(p) * 100f:0}%"
                    : $"recarga {ReloadTime(p):0.0} s",
                UpgradeStat.AimAssistPercent => $"mira assistida {Total(UpgradeStat.AimAssistPercent, p):0}%",

                _ => "",
            };
        }

        /// <summary>Máximo de espalhamento que o teto da recarga devolve em precisão.</summary>
        const float MaxCappedSpreadReduction = 0.4f;

        float MinReload => shipClass ? shipClass.minReloadSeconds : 0f;

        /// <summary>Recarga com os upgrades (+ prévia) e a tripulação, sem o teto.</summary>
        float RawReloadTime(UpgradeDefinition preview) =>
            baseCannons.reloadTime * (1f - Total(UpgradeStat.ReloadPercent, preview) / 100f) * (crew ? crew.ReloadMultiplier : 1f);

        /// <summary>Recarga com os upgrades (+ prévia) e a tripulação a bordo agora, nunca abaixo da mínima da classe do navio.</summary>
        float ReloadTime(UpgradeDefinition preview) => Mathf.Max(MinReload, RawReloadTime(preview));

        /// <summary>
        /// O que o teto da recarga cortou vira precisão (decidido pelo Jhonata em 05/10: com a tripulação cheia, Artilheiros 3 e 4 já
        /// não mudavam a recarga): fração do ganho perdido = menos espalhamento, até <see cref="MaxCappedSpreadReduction"/>.
        /// </summary>
        float CappedSpreadReduction(UpgradeDefinition preview)
        {
            float min = MinReload, raw = RawReloadTime(preview);
            return min <= 0f || raw >= min ? 0f : Mathf.Min(MaxCappedSpreadReduction, 1f - raw / min);
        }

        void OnDestroy()
        {
            if (crew) crew.Changed -= Apply;
            if (fame) fame.PerksChanged -= Apply;
            if (shipCopy) Destroy(shipCopy);
            if (cannonCopy) Destroy(cannonCopy);
        }
    }
}
