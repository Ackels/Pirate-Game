using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Progression
{
    /// <summary>
    /// Fama do capitão (M10): nome, fama, patente, bônus escolhidos e a conta dos feitos. Ganha fama afundando navios,
    /// derrotando acampamentos, calando fortes, desenterrando tesouros e entregando missões (EventBus: cada evento diz quanto
    /// vale). Cada patente nova libera contratos e itens da loja (quem confere são o quadro e o mercado) e um bônus para
    /// escolher entre alguns sorteados (o painel da tecla C). Fica num objeto próprio da cena, sempre ligado (o navio desliga
    /// em terra). Quem usa os bônus pergunta <see cref="Bonus"/> e ouve <see cref="PerksChanged"/>.
    /// </summary>
    public class CaptainFame : MonoBehaviour, ICaptainRank
    {
        public const int MaxNameLength = 24;

        [SerializeField] FameSettings settings;

        readonly List<FameSettings.Perk> perks = new();
        readonly List<FameSettings.Perk> offer = new();
        readonly List<FameSettings.Perk> pool = new();

        /// <summary>A fama da cena (null nas cenas sem ela: 01 a 04 e 06).</summary>
        public static CaptainFame Current { get; private set; }

        public FameSettings Settings => settings;
        public string CaptainName { get; private set; }
        public int Fame { get; private set; }
        public int Rank { get; private set; }
        public int ShipsSunk { get; private set; }
        public int CampsCleared { get; private set; }
        public int TreasuresFound { get; private set; }
        public int MissionsDone { get; private set; }
        /// <summary>Bônus escolhidos, na ordem.</summary>
        public IReadOnlyList<FameSettings.Perk> Perks => perks;
        /// <summary>Bônus oferecidos agora (vazio se não há escolha pendente).</summary>
        public IReadOnlyList<FameSettings.Perk> Offer => offer;
        /// <summary>Patentes que ainda não deram o bônus delas.</summary>
        public int PendingPerks => Mathf.Max(0, Rank - perks.Count);
        public int RankCount => settings ? Mathf.Max(1, settings.ranks.Length) : 1;
        public bool IsMaxRank => Rank >= RankCount - 1;
        /// <summary>Fama mínima de uma patente.</summary>
        public int RankFame(int rank) => settings && rank >= 0 && rank < settings.ranks.Length ? settings.ranks[rank].fame : 0;
        /// <summary>Quanto já andou da patente atual até a próxima (1 na última).</summary>
        public float RankProgress
        {
            get
            {
                if (IsMaxRank) return 1f;
                int from = RankFame(Rank), to = RankFame(Rank + 1);
                return to > from ? Mathf.Clamp01((Fame - from) / (float)(to - from)) : 1f;
            }
        }
        /// <summary>Sobe a cada mudança (fama, nome, bônus): o painel só se remonta quando muda.</summary>
        public int Version { get; private set; }

        /// <summary>Ganhou fama: quanto e por quê ("navio afundado").</summary>
        public event Action<int, string> FameGained;
        /// <summary>Subiu de patente jogando (carregar o jogo não avisa).</summary>
        public event Action<int> RankUp;
        /// <summary>Os bônus mudaram (escolheu, carregou, zerou): navio, mercado e obras recalculam.</summary>
        public event Action PerksChanged;
        public event Action Changed;

        public void Configure(FameSettings fameSettings) => settings = fameSettings;

        void Awake()
        {
            Current = this;
            CaptainName = DefaultName;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void OnEnable()
        {
            Services.Register<ICaptainRank>(this);
            EventBus.Subscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Subscribe<CampClearedEvent>(OnCampCleared);
            EventBus.Subscribe<TreasureFoundEvent>(OnTreasureFound);
            EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
            EventBus.Subscribe<MissionTurnedInEvent>(OnMissionTurnedIn);
        }

        void OnDisable()
        {
            Services.Unregister<ICaptainRank>(this);
            EventBus.Unsubscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Unsubscribe<CampClearedEvent>(OnCampCleared);
            EventBus.Unsubscribe<TreasureFoundEvent>(OnTreasureFound);
            EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
            EventBus.Unsubscribe<MissionTurnedInEvent>(OnMissionTurnedIn);
        }

        string DefaultName => settings && !string.IsNullOrWhiteSpace(settings.defaultCaptainName) ? settings.defaultCaptainName : "Capitão";

        /// <summary>Nome limpo (sem espaços nas pontas, até <see cref="MaxNameLength"/> letras); vazio = o padrão.</summary>
        public string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength).Trim();
            return name.Length > 0 ? name : DefaultName;
        }

        // ---------- Patentes ----------

        public string RankTitle(int rank) =>
            settings && rank >= 0 && rank < settings.ranks.Length && settings.ranks[rank] != null ? settings.ranks[rank].title : $"Patente {rank}";

        public string RankDescription(int rank) =>
            settings && rank >= 0 && rank < settings.ranks.Length && settings.ranks[rank] != null ? settings.ranks[rank].description : "";

        public float MissionGoldMultiplier =>
            1f + (settings ? settings.missionGoldPerRank : 0f) * Rank + PerkValue(PerkEffect.MissionGoldPercent);

        /// <summary>Ganha fama (o HUD mostra "+N fama" e a patente pode subir).</summary>
        public void Add(int amount, string reason)
        {
            if (amount <= 0) return;
            Fame += amount;
            FameGained?.Invoke(amount, reason);
            UpdateRank(announce: true);
            Touch();
        }

        void UpdateRank(bool announce)
        {
            int rank = 0;
            for (int i = 1; i < RankCount; i++)
                if (Fame >= RankFame(i)) rank = i;
            if (rank != Rank)
            {
                bool up = rank > Rank;
                Rank = rank;
                if (up && announce) RankUp?.Invoke(rank);
            }
            EnsureOffer();
        }

        // ---------- Bônus ----------

        /// <summary>Soma dos bônus escolhidos de um tipo (0 = nenhum).</summary>
        public float PerkValue(PerkEffect effect)
        {
            float total = 0f;
            for (int i = 0; i < perks.Count; i++)
                if (perks[i].effect == effect) total += perks[i].value;
            return total;
        }

        /// <summary>Bônus da fama da cena (0 sem ela).</summary>
        public static float Bonus(PerkEffect effect) => Current ? Current.PerkValue(effect) : 0f;

        public bool HasPerk(FameSettings.Perk perk) => perks.Contains(perk);

        /// <summary>Escolhe um dos bônus oferecidos (gasta uma patente pendente).</summary>
        public bool ChoosePerk(FameSettings.Perk perk)
        {
            if (perk == null || PendingPerks <= 0 || !offer.Contains(perk)) return false;
            perks.Add(perk);
            offer.Clear();
            EnsureOffer();
            PerksChanged?.Invoke();
            Touch();
            return true;
        }

        /// <summary>Com patente pendente e sem oferta, sorteia os bônus para escolher (só entre os que ainda não tem).</summary>
        void EnsureOffer()
        {
            if (PendingPerks <= 0)
            {
                offer.Clear();
                return;
            }
            if (offer.Count > 0 || !settings) return;
            pool.Clear();
            foreach (var p in settings.perks)
                if (p != null && !perks.Contains(p)) pool.Add(p);
            int n = Mathf.Min(settings.perksOffered, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int pick = Random.Range(i, pool.Count);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
                offer.Add(pool[i]);
            }
        }

        // ---------- Feitos ----------

        void OnShipSunk(ShipSunkEvent evt)
        {
            ShipsSunk++;
            if (evt.fame > 0) Add(evt.fame, "navio afundado");
            else Touch();
        }

        void OnCampCleared(CampClearedEvent evt)
        {
            CampsCleared++;
            string reason = "inimigos derrotados";
            foreach (var fort in EnemyFort.Active)
                if (fort && fort.FortId == evt.campId) reason = "forte calado";
            if (evt.fame > 0) Add(evt.fame, reason);
            else Touch();
        }

        void OnTreasureFound(TreasureFoundEvent evt)
        {
            TreasuresFound++;
            if (evt.fame > 0) Add(evt.fame, "tesouro desenterrado");
            else Touch();
        }

        /// <summary>Monstro do mar derrotado (a lula gigante); os inimigos de terra com nome publicam com fama 0.</summary>
        void OnEnemyDefeated(EnemyDefeatedEvent evt)
        {
            if (evt.fame > 0) Add(evt.fame, "monstro derrotado");
        }

        void OnMissionTurnedIn(MissionTurnedInEvent evt)
        {
            MissionsDone++;
            if (evt.fame > 0) Add(evt.fame, $"missão \"{evt.title}\"");
            else Touch();
        }

        void Touch()
        {
            Version++;
            Changed?.Invoke();
        }

        // ---------- Jogo novo / salvo ----------

        public struct Stats
        {
            public int shipsSunk, campsCleared, treasuresFound, missionsDone;
        }

        public Stats Counts => new() { shipsSunk = ShipsSunk, campsCleared = CampsCleared, treasuresFound = TreasuresFound, missionsDone = MissionsDone };

        /// <summary>Jogo novo: nome escolhido no menu, fama zero, sem bônus.</summary>
        public void ResetAll(string captainName)
        {
            Restore(captainName, 0, Array.Empty<string>(), Array.Empty<string>(), default);
        }

        public void SetName(string captainName)
        {
            CaptainName = CleanName(captainName);
            Touch();
        }

        /// <summary>Carregar o jogo (sem o aviso de patente nova). Ids de bônus que não existem mais são ignorados.</summary>
        public void Restore(string captainName, int fame, IEnumerable<string> perkIds, IEnumerable<string> offerIds, Stats stats)
        {
            CaptainName = CleanName(captainName);
            Fame = Mathf.Max(0, fame);
            ShipsSunk = stats.shipsSunk;
            CampsCleared = stats.campsCleared;
            TreasuresFound = stats.treasuresFound;
            MissionsDone = stats.missionsDone;
            perks.Clear();
            offer.Clear();
            Rank = 0;
            UpdateRank(announce: false);
            if (settings)
            {
                foreach (string id in perkIds)
                {
                    var p = settings.FindPerk(id);
                    if (p != null && !perks.Contains(p) && perks.Count < Rank) perks.Add(p);
                }
                offer.Clear();
                if (PendingPerks > 0)
                    foreach (string id in offerIds)
                    {
                        var p = settings.FindPerk(id);
                        if (p != null && !perks.Contains(p) && !offer.Contains(p)) offer.Add(p);
                    }
            }
            EnsureOffer();
            PerksChanged?.Invoke();
            Touch();
        }
    }
}
