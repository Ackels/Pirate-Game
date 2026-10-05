using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Missions
{
    /// <summary>
    /// Quadro de missões do porto: contratos dos NPCs, até maxActive aceitos ao mesmo tempo. O progresso vem dos eventos
    /// do jogo (EventBus: navio afundado, acampamento derrotado, tesouro achado) e, nas entregas, do que está no porão.
    /// Entregar e receber o pagamento só no porto (aba Missões do HarborHUD); o pagamento vai para o armazém. Repetível
    /// entregue descansa (MissionDefinition.cooldownSeconds, tempo de jogo) antes de voltar ao quadro.
    /// </summary>
    public class MissionBoard : MonoBehaviour
    {
        /// <summary>Missão aceita e o quanto já foi feito.</summary>
        public class ActiveMission
        {
            public MissionDefinition definition;
            public int progress;
            public bool objectiveDone;
        }

        [SerializeField] MissionDefinition[] offered = Array.Empty<MissionDefinition>();
        [SerializeField, Min(1)] int maxActive = 3;
        [Tooltip("Porão do navio: as entregas saem daqui (e o progresso delas é o que está nele).")]
        [SerializeField] Inventory hold;
        [Tooltip("Armazém do porto (opcional): a carga já descarregada também conta para as entregas, e sai primeiro dele.")]
        [SerializeField] Inventory warehouse;
        [Tooltip("Item do pagamento que rende mais com a patente do capitão (o ouro; M10). Vazio = pagamento fixo.")]
        [SerializeField] ItemDefinition bonusItem;

        readonly List<ActiveMission> active = new();
        readonly HashSet<MissionDefinition> finished = new();
        readonly Dictionary<MissionDefinition, float> cooldowns = new();
        readonly List<MissionDefinition> expired = new();

        public IReadOnlyList<ActiveMission> Active => active;
        /// <summary>Todos os contratos deste quadro (também os trancados por patente e os já entregues).</summary>
        public IReadOnlyList<MissionDefinition> Offered => offered;
        /// <summary>Já entregues pelo menos uma vez (as não repetíveis saem do quadro).</summary>
        public IReadOnlyCollection<MissionDefinition> Finished => finished;
        public int MaxActive => maxActive;
        public bool CanAccept => active.Count < maxActive;
        /// <summary>Repetíveis entregues descansando: segundos até voltarem ao quadro (para salvar e para o quadro mostrar).</summary>
        public IReadOnlyDictionary<MissionDefinition, float> Cooldowns => cooldowns;

        /// <summary>Objetivo cumprido em campo: falta voltar ao porto para receber.</summary>
        public event Action<MissionDefinition> ObjectiveCompleted;
        public event Action<MissionDefinition> Accepted;
        /// <summary>Entregue no porto: o que foi pago.</summary>
        public event Action<MissionDefinition, IReadOnlyList<ItemStack>> TurnedIn;

        public void Configure(MissionDefinition[] missions, Inventory shipHold, Inventory harborWarehouse = null, ItemDefinition rankBonusItem = null)
        {
            offered = missions;
            hold = shipHold;
            warehouse = harborWarehouse;
            bonusItem = rankBonusItem;
        }

        // ---------- Patente do capitão (M10) ----------

        /// <summary>Patente atual (0 sem a fama do capitão na cena: tudo que pede patente fica trancado).</summary>
        public int Rank => Services.TryGet(out ICaptainRank rank) ? rank.Rank : 0;

        public string RankTitle(int rank) => Services.TryGet(out ICaptainRank r) ? r.RankTitle(rank) : $"patente {rank}";

        /// <summary>A patente já deixa aceitar este contrato.</summary>
        public bool IsUnlocked(MissionDefinition mission) => mission && mission.requiredRank <= Rank;

        /// <summary>
        /// Quanto vem de um item do pagamento agora (o ouro rende mais com a patente e os bônus). Entregas pagam fixo: com o
        /// bônus, comprar a carga no mercado (ou fazer com a produção da base) e entregar dava lucro sem sair do porto.
        /// </summary>
        public int RewardAmount(MissionDefinition mission, ItemStack reward)
        {
            if (!reward.item || reward.item != bonusItem || (mission && mission.type == MissionType.Deliver) ||
                !Services.TryGet(out ICaptainRank rank)) return reward.amount;
            return Mathf.RoundToInt(reward.amount * Mathf.Max(0f, rank.MissionGoldMultiplier));
        }

        /// <summary>Pagamento de agora, já com a patente (para o quadro mostrar).</summary>
        public ItemStack[] Rewards(MissionDefinition mission)
        {
            var list = new ItemStack[mission.rewards.Length];
            for (int i = 0; i < list.Length; i++) list[i] = new ItemStack(mission.rewards[i].item, RewardAmount(mission, mission.rewards[i]));
            return list;
        }

        /// <summary>Contratos que ainda pedem patente maior (o quadro mostra trancados, com a patente que falta).</summary>
        public List<MissionDefinition> Locked()
        {
            var list = new List<MissionDefinition>();
            foreach (var m in offered)
                if (m && !IsUnlocked(m) && (m.repeatable || !finished.Contains(m))) list.Add(m);
            list.Sort((a, b) => a.requiredRank.CompareTo(b.requiredRank));
            return list;
        }

        /// <summary>Quanto há de um item para as entregas (porão + armazém).</summary>
        int Stock(ItemDefinition item) => (hold ? hold.Count(item) : 0) + (warehouse ? warehouse.Count(item) : 0);

        void OnEnable()
        {
            Services.Register(this);
            EventBus.Subscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Subscribe<CampClearedEvent>(OnCampCleared);
            EventBus.Subscribe<TreasureFoundEvent>(OnTreasureFound);
        }

        void OnDisable()
        {
            Services.Unregister(this);
            EventBus.Unsubscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Unsubscribe<CampClearedEvent>(OnCampCleared);
            EventBus.Unsubscribe<TreasureFoundEvent>(OnTreasureFound);
        }

        void Update()
        {
            if (cooldowns.Count == 0) return;
            float dt = Time.deltaTime;
            foreach (var (mission, seconds) in cooldowns)
                if (seconds - dt <= 0f) expired.Add(mission);
            for (int i = 0; i < expired.Count; i++) cooldowns.Remove(expired[i]);
            expired.Clear();
            // Sem alocar: as chaves não mudam aqui, só os valores.
            foreach (var mission in offered)
                if (mission && cooldowns.TryGetValue(mission, out float left)) cooldowns[mission] = left - dt;
        }

        /// <summary>Contratos no quadro agora (nem aceitos, nem entregues de vez, nem descansando).</summary>
        public List<MissionDefinition> Available()
        {
            var list = new List<MissionDefinition>();
            foreach (var m in offered)
                if (m && IsUnlocked(m) && !IsActive(m) && (m.repeatable || !finished.Contains(m)) && !cooldowns.ContainsKey(m)) list.Add(m);
            return list;
        }

        /// <summary>
        /// Muda quando uma missão entra ou sai do descanso ou quando o minuto que falta muda (o quadro só se remonta aí). Sem
        /// alocar: percorre o Dictionary concreto (pela interface, o foreach aloca a cada quadro).
        /// </summary>
        public long RestingKey()
        {
            long hash = 41;
            foreach (var (mission, seconds) in cooldowns)
                hash = unchecked(hash * 31 + (mission ? mission.GetHashCode() : 0) * 7 + Mathf.CeilToInt(seconds / 60f));
            return hash;
        }

        /// <summary>Segundos até a missão voltar ao quadro (0 = já está lá ou não descansa).</summary>
        public float CooldownLeft(MissionDefinition mission) =>
            mission && cooldowns.TryGetValue(mission, out float left) ? Mathf.Max(0f, left) : 0f;

        public bool IsActive(MissionDefinition mission) => active.Exists(a => a.definition == mission);

        /// <summary>Contrato deste quadro pelo missionId (null se não há).</summary>
        public MissionDefinition Find(string missionId)
        {
            foreach (var m in offered)
                if (m && m.missionId == missionId) return m;
            return null;
        }

        /// <summary>
        /// Carregar o jogo: missões aceitas (com o progresso) e entregues, sem os eventos do HUD. Os alvos das que ainda
        /// faltam se preparam de novo (o X do tesouro reaparece).
        /// </summary>
        public void Restore(IEnumerable<ActiveMission> saved, IEnumerable<MissionDefinition> turnedIn,
            IEnumerable<KeyValuePair<MissionDefinition, float>> resting = null)
        {
            active.Clear();
            finished.Clear();
            cooldowns.Clear();
            foreach (var m in turnedIn)
                if (m) finished.Add(m);
            if (resting != null)
                foreach (var (m, seconds) in resting)
                    if (m && seconds > 0f) cooldowns[m] = seconds;
            foreach (var m in saved)
            {
                if (m?.definition == null || IsActive(m.definition) || !CanAccept) continue;
                active.Add(m);
                if (!m.objectiveDone)
                    EventBus.Publish(new MissionStartedEvent { missionId = m.definition.missionId, targetId = m.definition.targetId });
            }
        }

        public bool Accept(MissionDefinition mission)
        {
            if (!mission || !CanAccept || IsActive(mission) || !IsUnlocked(mission)) return false;
            active.Add(new ActiveMission { definition = mission });
            Accepted?.Invoke(mission);
            // O alvo se prepara: o tesouro é enterrado e o X aparece; o acampamento derrotado se repovoa.
            EventBus.Publish(new MissionStartedEvent { missionId = mission.missionId, targetId = mission.targetId });
            return true;
        }

        public void Abandon(ActiveMission mission) => active.Remove(mission);

        /// <summary>Pronta para entregar no porto.</summary>
        public bool IsComplete(ActiveMission mission) =>
            mission.definition.type == MissionType.Deliver ? HasDelivery(mission.definition) : mission.objectiveDone;

        /// <summary>Entrega no porto: tira a carga pedida do porão e põe o pagamento em <paramref name="payTo"/>.</summary>
        public bool TurnIn(ActiveMission mission, Inventory payTo)
        {
            if (mission == null || !IsComplete(mission) || !payTo) return false;
            var def = mission.definition;
            if (def.type == MissionType.Deliver)
                foreach (var stack in def.delivery)
                {
                    int left = stack.amount - (warehouse ? warehouse.Remove(stack.item, stack.amount) : 0);
                    if (left > 0) hold.Remove(stack.item, left);
                }
            var paid = new List<ItemStack>();
            foreach (var reward in def.rewards)
            {
                int n = payTo.Add(reward.item, RewardAmount(def, reward));
                if (n > 0) paid.Add(new ItemStack(reward.item, n));
            }
            active.Remove(mission);
            finished.Add(def);
            if (def.repeatable && def.cooldownSeconds > 0f) cooldowns[def] = def.cooldownSeconds;
            TurnedIn?.Invoke(def, paid);
            EventBus.Publish(new MissionTurnedInEvent { missionId = def.missionId, title = def.title, fame = def.fameReward });
            return true;
        }

        /// <summary>Texto do progresso ("1/2 navios afundados", "Madeira 12/20, Ferro 4/4").</summary>
        public string ProgressText(ActiveMission mission)
        {
            var def = mission.definition;
            // Texto próprio da missão (ex.: forte: "cale os canhões..." / "forte calado"), menos nas entregas (contam itens).
            if (def.type != MissionType.Deliver)
            {
                string custom = mission.objectiveDone ? def.doneText : def.objectiveText;
                if (!string.IsNullOrEmpty(custom)) return custom;
            }
            switch (def.type)
            {
                case MissionType.SinkShips when !string.IsNullOrEmpty(def.targetId):
                    return mission.objectiveDone ? $"{def.targetName} afundado" : $"afunde o {def.targetName}";
                case MissionType.SinkShips:
                    return $"{Mathf.Min(mission.progress, def.count)}/{def.count} navios afundados";
                case MissionType.ClearCamp:
                    return mission.objectiveDone ? "acampamento derrotado" : $"derrote os piratas em {def.targetName}";
                case MissionType.FindTreasure:
                    return mission.objectiveDone ? "tesouro desenterrado" : $"cave o tesouro em {def.targetName}";
                default:
                    var parts = new List<string>();
                    foreach (var stack in def.delivery)
                        parts.Add($"{stack.item.displayName} {Mathf.Min(Stock(stack.item), stack.amount)}/{stack.amount}");
                    return (warehouse ? "no porão e no armazém: " : "no porão: ") + string.Join(", ", parts);
            }
        }

        /// <summary>Debug (sandbox): cumpre os objetivos ativos (entregas: põe a carga no porão, se couber).</summary>
        public void CompleteAll()
        {
            foreach (var m in active)
            {
                if (m.definition.type == MissionType.Deliver)
                {
                    foreach (var stack in m.definition.delivery)
                        hold.Add(stack.item, Mathf.Max(0, stack.amount - Stock(stack.item)));
                    continue;
                }
                m.progress = m.definition.count;
                Complete(m);
            }
        }

        /// <summary>Debug (sandbox): esquece aceitas, entregues e o descanso das repetíveis.</summary>
        public void ResetAll()
        {
            active.Clear();
            finished.Clear();
            cooldowns.Clear();
        }

        bool HasDelivery(MissionDefinition def)
        {
            if (!hold) return false;
            foreach (var stack in def.delivery)
                if (Stock(stack.item) < stack.amount) return false;
            return true;
        }

        void OnShipSunk(ShipSunkEvent evt)
        {
            bool counted = false;
            foreach (var m in active)
            {
                if (m.definition.type != MissionType.SinkShips || m.objectiveDone) continue;
                // Missão com alvo (o brigue) só conta aquele navio. Sem alvo, qualquer um conta, mas só para uma delas (a
                // aceita primeiro): com as três ativas, cada navio pagava três vezes.
                bool targeted = !string.IsNullOrEmpty(m.definition.targetId);
                if (targeted ? m.definition.targetId != evt.targetId : counted) continue;
                if (!targeted) counted = true;
                m.progress++;
                if (m.progress >= m.definition.count) Complete(m);
            }
        }

        void OnCampCleared(CampClearedEvent evt) => CompleteTarget(MissionType.ClearCamp, evt.campId);

        void OnTreasureFound(TreasureFoundEvent evt) => CompleteTarget(MissionType.FindTreasure, evt.treasureId);

        void CompleteTarget(MissionType type, string targetId)
        {
            foreach (var m in active)
                if (m.definition.type == type && m.definition.targetId == targetId && !m.objectiveDone) Complete(m);
        }

        void Complete(ActiveMission mission)
        {
            if (mission.objectiveDone) return;
            mission.objectiveDone = true;
            ObjectiveCompleted?.Invoke(mission.definition);
        }
    }
}
