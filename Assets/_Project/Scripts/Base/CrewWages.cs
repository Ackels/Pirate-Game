using System;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// Salário da tripulação (12/11; onde gastar o ouro, pedido do Jhonata: "com base na quantidade de tripulante" e "a cada tantos dias"):
    /// a cada <see cref="payEveryDays"/> dias de jogo chega o dia de pagamento: <see cref="wagePerCrew"/> de ouro por tripulante a bordo
    /// fica devido e se paga na taverna (pedido do Jhonata; armazém, depois porão). Atrasado mais de <see cref="graceHours"/> horas de
    /// jogo, a moral cai (o bônus da tripulação diminui: <see cref="ShipCrew.Morale"/>); com moral zero, um marujo deserta a cada
    /// <see cref="desertEveryHours"/> horas. Pago, a moral volta aos poucos. O save guarda o devido, a moral e o próximo dia de pagamento.
    /// </summary>
    public class CrewWages : MonoBehaviour, ICrewWages
    {
        [SerializeField] ShipCrew crew;
        [SerializeField] HarborDock dock;
        [SerializeField] ItemDefinition gold;
        [Tooltip("Ouro por tripulante a bordo a cada dia de pagamento.")]
        [SerializeField] int wagePerCrew = 10;
        [Tooltip("Dias de jogo entre os pagamentos (um dia de jogo ≈ 20 min reais).")]
        [SerializeField] float payEveryDays = 3f;
        [Tooltip("Horas de jogo de folga depois do dia de pagamento antes de a moral cair.")]
        [SerializeField] float graceHours = 24f;
        [Tooltip("Moral perdida por hora de jogo com o salário atrasado (0–1).")]
        [SerializeField] float moraleLossPerHour = 0.025f;
        [Tooltip("Moral recuperada por hora de jogo sem dívida.")]
        [SerializeField] float moraleGainPerHour = 0.04f;
        [SerializeField] float desertEveryHours = 8f;

        [Header("Rodada na taverna (13/11)")]
        [Tooltip("Ouro por tripulante a bordo para pagar uma rodada no balcão.")]
        [SerializeField] int roundPerCrew = 3;
        [Tooltip("Moral que a rodada devolve na hora (0–1).")]
        [SerializeField] float roundMorale = 0.3f;
        [Tooltip("Horas de jogo em que a tripulação fica animada: a moral não cai, mesmo com o salário atrasado.")]
        [SerializeField] float roundCheerHours = 24f;

        float lastHours = -1f, nextPayday = -1f, dueSince = -1f, desertTimer, cheerUntil = -1f;

        public static CrewWages Current { get; private set; }

        /// <summary>Ouro devido à tripulação agora.</summary>
        public float Debt { get; private set; }
        public float Morale => crew ? crew.Morale : 1f;
        public int WagePerCrew => wagePerCrew;
        public float PayEveryDays => payEveryDays;
        public int CrewCount => crew ? crew.Count : 0;
        /// <summary>Horas de jogo até o próximo dia de pagamento.</summary>
        public float HoursToPayday => nextPayday < 0f || lastHours < 0f ? payEveryDays * 24f : Mathf.Max(0f, nextPayday - lastHours);
        public bool Late => Debt > 0f && dueSince >= 0f && lastHours - dueSince > graceHours;

        /// <summary>Chegou o dia de pagamento (quanto ficou devido).</summary>
        public event Action<int> PaydayCame;
        /// <summary>Pagou (quanto) e quanto ainda deve.</summary>
        public event Action<int, int> Paid;
        public event Action Deserted;

        public void Configure(ShipCrew shipCrew, HarborDock harborDock, ItemDefinition goldItem)
        {
            crew = shipCrew;
            dock = harborDock;
            gold = goldItem;
        }

        void Awake() => Current = this;

        void OnEnable() => Services.Register<ICrewWages>(this);
        void OnDisable() => Services.Unregister<ICrewWages>(this);

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        static float TotalHours(ITimeOfDay time) => (time.Day - 1) * 24f + time.Hour;

        void Update()
        {
            if (!crew || !Services.TryGet(out ITimeOfDay time)) return;
            float now = TotalHours(time);
            if (lastHours < 0f || now < lastHours || now - lastHours > 48f)
            {
                // Começo, jogo carregado ou relógio pulado pelo F1: recomeça a contagem dali.
                lastHours = now;
                if (restoreToPayday > 0f) { nextPayday = now + restoreToPayday; restoreToPayday = -1f; }
                if (restoreLate >= 0f) { dueSince = now - restoreLate; restoreLate = -1f; }
                if (nextPayday < 0f || nextPayday < now - payEveryDays * 24f) nextPayday = now + payEveryDays * 24f;
                if (Debt > 0f && dueSince < 0f) dueSince = now;
                return;
            }
            float dh = now - lastHours;
            if (dh <= 0f) return;
            lastHours = now;

            if (now >= nextPayday)
            {
                nextPayday += payEveryDays * 24f;
                int due = crew.Count * wagePerCrew;
                if (due > 0)
                {
                    if (Debt <= 0f) dueSince = now;
                    Debt += due;
                    PaydayCame?.Invoke(due);
                }
            }

            float morale = crew.Morale;
            if (Late && now >= cheerUntil) morale -= moraleLossPerHour * dh;
            else if (Debt <= 0f) morale += moraleGainPerHour * dh;
            crew.SetMorale(morale);

            if (crew.Morale <= 0f && crew.Count > 0)
            {
                desertTimer += dh;
                if (desertTimer >= desertEveryHours)
                {
                    desertTimer = 0f;
                    var hold = crew.GetComponent<Inventory>();
                    if (hold && crew.CrewItem && hold.Remove(crew.CrewItem, 1) > 0) Deserted?.Invoke();
                }
            }
            else desertTimer = 0f;
        }

        /// <summary>Ouro que o capitão tem para pagar (armazém + porão).</summary>
        public int GoldAvailable => gold ? (dock ? dock.Warehouse.Count(gold) : 0) + (crew && crew.GetComponent<Inventory>() ? crew.GetComponent<Inventory>().Count(gold) : 0) : 0;

        /// <summary>Paga o que der agora (botão da taverna). Armazém primeiro, depois o porão.</summary>
        public int PayNow()
        {
            int owed = Mathf.CeilToInt(Debt);
            if (owed <= 0 || !gold) return 0;
            int paid = 0;
            if (dock) paid += dock.Warehouse.Remove(gold, owed);
            var hold = crew ? crew.GetComponent<Inventory>() : null;
            if (paid < owed && hold) paid += hold.Remove(gold, owed - paid);
            Debt = Mathf.Max(0f, Debt - paid);
            if (Debt <= 0f) dueSince = -1f;
            Paid?.Invoke(paid, Mathf.CeilToInt(Debt));
            return paid;
        }

        // ---------- Rodada no balcão da taverna (13/11) ----------

        /// <summary>Quanto custa uma rodada para quem está a bordo agora.</summary>
        public int RoundCost => CrewCount * roundPerCrew;
        public float RoundMorale => roundMorale;
        public float RoundCheerHours => roundCheerHours;
        /// <summary>Horas de jogo que faltam da última rodada (0 = acabou).</summary>
        public float CheerHoursLeft => cheerUntil < 0f || lastHours < 0f ? 0f : Mathf.Max(0f, cheerUntil - lastHours);

        /// <summary>Por que não dá para pagar a rodada agora (null = dá).</summary>
        public string RoundBlockReason()
        {
            if (CrewCount <= 0) return "sem tripulação a bordo";
            if (CheerHoursLeft > 0f) return $"a tripulação ainda está animada (~{Mathf.CeilToInt(CheerHoursLeft * 50f / 60f)} min)";
            if (GoldAvailable < RoundCost) return $"faltam {RoundCost - GoldAvailable} de ouro";
            return null;
        }

        /// <summary>Paga a rodada (armazém, depois porão): moral sobe e não cai por <see cref="roundCheerHours"/> horas de jogo.</summary>
        public bool BuyRound()
        {
            if (RoundBlockReason() != null || !Spend(RoundCost)) return false;
            if (crew) crew.SetMorale(Mathf.Min(1f, crew.Morale + roundMorale));
            float now = lastHours >= 0f ? lastHours : (Services.TryGet(out ITimeOfDay time) ? TotalHours(time) : 0f);
            cheerUntil = now + roundCheerHours;
            return true;
        }

        /// <summary>Gasta ouro do armazém e depois do porão (falso = não tinha tudo, nada é gasto).</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (!gold || GoldAvailable < amount) return false;
            int paid = dock ? dock.Warehouse.Remove(gold, amount) : 0;
            var hold = crew ? crew.GetComponent<Inventory>() : null;
            if (paid < amount && hold) paid += hold.Remove(gold, amount - paid);
            return paid >= amount;
        }

        public ItemDefinition Gold => gold;

        /// <summary>Testes e F1: o dia de pagamento chega agora.</summary>
        public void PaydayNow()
        {
            if (lastHours >= 0f) nextPayday = lastHours;
        }

        // ---------- Save ----------

        /// <summary>Horas de jogo do salário atrasado (para o save).</summary>
        public float HoursLate => Debt > 0f && dueSince >= 0f && lastHours >= 0f ? lastHours - dueSince : 0f;

        public void Restore(bool has, float debt, float morale, float hoursToPayday, float hoursLate)
        {
            Debt = has ? Mathf.Max(0f, debt) : 0f;
            if (crew) crew.SetMorale(has ? morale : 1f);
            restoreToPayday = has && hoursToPayday > 0f ? hoursToPayday : -1f;
            restoreLate = Debt > 0f ? Mathf.Max(0f, hoursLate) : -1f;
            lastHours = -1f;
            nextPayday = -1f;
            dueSince = -1f;
            desertTimer = 0f;
        }

        float restoreToPayday = -1f, restoreLate = -1f;
    }
}
