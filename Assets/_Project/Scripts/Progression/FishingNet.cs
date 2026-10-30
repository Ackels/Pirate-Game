using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Pesca com rede do navio parado (D2, passo 2, 30/10): com a rede de pesca (item do mercado, guardada em qualquer lugar), o navio
    /// quase parado dentro de um cardume (<see cref="FishingSpot"/>, as bolhas no mar), pelo menos 2 tripulantes e nenhum navio inimigo
    /// lutando por perto, F lança a rede pelo estibordo: ela desce da amurada, fica com as boias na linha d'água e, enquanto está lá,
    /// um peixe cai nela a cada alguns segundos (respingo), tirado do estoque do cardume, até a rede encher (mais tripulação, rede
    /// maior). F (ou Esc) puxa: a rede sobe com os peixes e tudo vai para o porão (o que não couber volta para o mar). Às vezes vem madeira
    /// boiando ou, raro, um pedaço de mapa. Acelerar, levar tiro ou sair do cardume puxa a rede na hora. Dois marujos ficam na amurada
    /// puxando o cabo (<see cref="DeckCrew.WorkNet"/>).
    /// </summary>
    [RequireComponent(typeof(ShipController), typeof(Inventory))]
    public class FishingNet : MonoBehaviour, IInteraction, INoWindowInteraction
    {
        public enum Phase { Idle, Lowering, Soaking, Hauling }

        /// <summary>O que pode cair na rede: item e peso no sorteio (o modelo mostrado ao puxar é o filho do lugar com o nome do item).</summary>
        [Serializable]
        public class Catch
        {
            public ItemDefinition item;
            [Min(0f)] public float weight = 1f;
            [Tooltip("Achado (madeira, pedaço de mapa): sai do sorteio dos achados, não do dos peixes.")]
            public bool extra;
            public int min = 1, max = 1;
            [Tooltip("Hora, tempo e regiões em que vem (vazio = sempre).")]
            public FishWhen when;
        }

        [SerializeField] ItemDefinition netItem;
        [Tooltip("Rede reforçada (prêmio do diário do pescador): segura mais peixes.")]
        [SerializeField] ItemDefinition strongNetItem;
        [SerializeField] int strongNetBonus = 4;
        [SerializeField] Catch[] catches = Array.Empty<Catch>();
        [Tooltip("Chance de cada coisa que cai na rede ser um achado (madeira, pedaço de mapa) em vez de um peixe.")]
        [SerializeField, Range(0f, 1f)] float extraChance = 0.08f;

        [Header("Visual (montado pelo gerador)")]
        [Tooltip("Filho do navio: a rede inteira (anda com o casco; o gerador não precisa saber o tamanho de cada casco).")]
        [SerializeField] Transform netRoot;
        [Tooltip("A malha da rede (escala = fundura × comprimento).")]
        [SerializeField] Transform netBody;
        [SerializeField] Transform floats;
        [SerializeField] LineRenderer[] ropes = Array.Empty<LineRenderer>();
        [Tooltip("Lugares dos peixes mostrados dentro da rede ao puxar.")]
        [SerializeField] Transform[] showSlots = Array.Empty<Transform>();

        [Header("Regras")]
        [SerializeField] float maxCastKnots = 1.5f;
        [Tooltip("Acima disto (nós) a rede é puxada sozinha.")]
        [SerializeField] float haulKnots = 3f;
        [SerializeField] int minCrew = 2;
        [Tooltip("Peixes que a rede segura: base + bônus × fração da tripulação.")]
        [SerializeField] int baseCapacity = 4, crewCapacity = 4;
        [Tooltip("Um peixe cai a cada tantos segundos.")]
        [SerializeField] Vector2 catchEvery = new(5f, 9f);
        [Tooltip("Navio inimigo perseguindo ou trocando tiros a menos disto (m) não deixa lançar.")]
        [SerializeField] float enemyRange = 220f;
        [SerializeField] float minFreeSpace = 1f;
        [Tooltip("1 = estibordo, −1 = bombordo (os canhões desse lado viram o lugar dos marujos da rede).")]
        [SerializeField] int side = 1;

        [Header("Tempos e medidas")]
        [SerializeField] float lowerSeconds = 1.4f, haulSeconds = 2.4f;
        [SerializeField] float netDepth = 2.6f, netLength = 4.5f;

        ShipController ship;
        Inventory hold;
        ShipCrew crew;
        DeckCrew deckCrew;
        Health health;
        BoxCollider hull;
        IOceanSampler ocean;
        IInteraction[] others = Array.Empty<IInteraction>();
        readonly List<ItemDefinition> caught = new();
        readonly List<ItemStack> landed = new();
        float phaseStart, nextCatch, nextScan, length;
        Vector3 railLocal;

        public Phase State { get; private set; }
        public bool HasNet => (netItem && PlayerStorage.Count(netItem) > 0) || StrongNet;
        /// <summary>O jogador tem a rede reforçada (prêmio do diário).</summary>
        public bool StrongNet => strongNetItem && PlayerStorage.Count(strongNetItem) > 0;
        public ItemDefinition NetItem => netItem;
        /// <summary>O cardume em que o navio está (ou o mais perto a até 120 m), atualizado a cada 0,25 s.</summary>
        public FishingSpot NearestSpot { get; private set; }
        public bool InsideSpot { get; private set; }
        /// <summary>O cardume onde a rede está na água.</summary>
        public FishingSpot CurrentSpot { get; private set; }
        /// <summary>Por que não dá para lançar aqui agora (vazio = dá, ou não há cardume).</summary>
        public string BlockReason { get; private set; } = "";
        public int Caught => caught.Count;
        public int Capacity => baseCapacity + Mathf.RoundToInt(crewCapacity * (crew ? crew.Fraction : 0f)) + (StrongNet ? strongNetBonus : 0);
        public float SpeedKnots => ship ? ship.SpeedKnots : 0f;
        public float MaxCastKnots => maxCastKnots;

        /// <summary>Outra interação do F do navio (destroços, porto, desembarque) está aberta ou disponível: ela vem primeiro.</summary>
        public bool OtherAvailable => OtherInteraction();

        public bool IsOpen => State != Phase.Idle;
        public bool CanOpen => State == Phase.Idle && InsideSpot && HasNet && BlockReason.Length == 0 && !OtherInteraction();

        /// <summary>Rede lançada.</summary>
        public static event Action<FishingNet> Cast;
        /// <summary>Algo caiu na rede.</summary>
        public static event Action<FishingNet, ItemDefinition> NetFilled;
        /// <summary>Rede puxada: o que foi para o porão e quantas unidades não couberam.</summary>
        public static event Action<FishingNet, IReadOnlyList<ItemStack>, int> Hauled;

        public void Configure(ItemDefinition net, ItemDefinition strongNet, Catch[] table, Transform root, Transform body, Transform floatRow,
            LineRenderer[] netRopes, Transform[] slots)
        {
            netItem = net;
            strongNetItem = strongNet;
            catches = table;
            netRoot = root;
            netBody = body;
            floats = floatRow;
            ropes = netRopes;
            showSlots = slots;
        }

        void Awake()
        {
            ship = GetComponent<ShipController>();
            hold = GetComponent<Inventory>();
            crew = GetComponent<ShipCrew>();
            deckCrew = GetComponent<DeckCrew>();
            health = GetComponent<Health>();
            var list = new List<IInteraction>();
            foreach (var i in GetComponents<IInteraction>()) if (!ReferenceEquals(i, this)) list.Add(i);
            others = list.ToArray();
            HideNet();
        }

        void OnEnable()
        {
            if (health) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health) health.Damaged -= OnDamaged;
            // Desembarcou ou trocou de cena com a rede na água: o que estava nela vai para o porão.
            if (State == Phase.Soaking || State == Phase.Lowering || State == Phase.Hauling) Land();
        }

        void OnDamaged(DamageInfo info)
        {
            if (State == Phase.Soaking || State == Phase.Lowering) BeginHaul();
        }

        public bool Open()
        {
            if (!CanOpen) return false;
            CurrentSpot = NearestSpot;
            CurrentSpot.InUse = true;
            caught.Clear();
            MeasureHull();
            State = Phase.Lowering;
            phaseStart = Time.time;
            nextCatch = Time.time + lowerSeconds + UnityEngine.Random.Range(catchEvery.x, catchEvery.y);
            netRoot.gameObject.SetActive(true);
            foreach (var s in showSlots) Show(s, null);
            if (deckCrew) deckCrew.WorkNet(side);
            Cast?.Invoke(this);
            return true;
        }

        /// <summary>F ou Esc com a rede na água: puxa.</summary>
        public void Close()
        {
            if (State == Phase.Lowering || State == Phase.Soaking) BeginHaul();
        }

        /// <summary>Teste e sandbox: enche a rede agora (com o que o cardume ainda tiver).</summary>
        public void FillNow()
        {
            if (State != Phase.Soaking && State != Phase.Lowering) return;
            while (caught.Count < Capacity && CatchOne()) { }
        }

        void Update()
        {
            if (ocean == null) Services.TryGet(out ocean);
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.25f;
                Scan();
            }
            switch (State)
            {
                case Phase.Lowering:
                    PlaceNet(Mathf.Clamp01((Time.time - phaseStart) / lowerSeconds), lowering: true);
                    if (Time.time - phaseStart >= lowerSeconds)
                    {
                        State = Phase.Soaking;
                        Splash(1.6f);
                    }
                    break;
                case Phase.Soaking:
                    PlaceNet(1f, lowering: true);
                    if (ship.SpeedKnots > haulKnots || !CurrentSpot || !CurrentSpot.isActiveAndEnabled)
                    {
                        BeginHaul();
                        break;
                    }
                    if (Time.time >= nextCatch)
                    {
                        nextCatch = Time.time + UnityEngine.Random.Range(catchEvery.x, catchEvery.y);
                        // Fora do círculo das bolhas não cai nada (o navio derivou).
                        if (caught.Count < Capacity && CurrentSpot.Contains(netRoot.position)) CatchOne();
                    }
                    break;
                case Phase.Hauling:
                    float t = Mathf.Clamp01((Time.time - phaseStart) / haulSeconds);
                    PlaceNet(1f - t, lowering: false);
                    if (t >= 1f) Land();
                    break;
            }
        }

        /// <summary>Cardume perto e o que impede de lançar.</summary>
        void Scan()
        {
            FishingSpot best = null;
            float bestDistance = 120f;
            for (int i = 0; i < FishingSpot.Active.Count; i++)
            {
                var s = FishingSpot.Active[i];
                if (s.Empty) continue;
                float d = s.FlatDistance(transform.position) - s.Radius;
                if (d < bestDistance) { bestDistance = d; best = s; }
            }
            NearestSpot = best;
            InsideSpot = best && best.Contains(transform.position);
            BlockReason = State != Phase.Idle ? "" : Reason();
        }

        string Reason()
        {
            if (!HasNet) return "sem rede de pesca (compre no mercado)";
            if (crew && crew.Count < minCrew) return $"precisa de {minCrew} tripulantes a bordo";
            if (!hold.Unlimited && hold.FreeSpace < minFreeSpace) return "porão cheio";
            if (EnemyNear()) return "inimigos por perto";
            if (ship.SpeedKnots > maxCastKnots) return $"recolha as velas e pare o navio ({ship.SpeedKnots:0.0} / {maxCastKnots:0.0} nós)";
            return "";
        }

        bool EnemyNear()
        {
            float range2 = enemyRange * enemyRange;
            for (int i = 0; i < EnemyShipAI.Active.Count; i++)
            {
                var ai = EnemyShipAI.Active[i];
                if (!ai || (ai.State != EnemyShipAI.AIState.Chase && ai.State != EnemyShipAI.AIState.Broadside)) continue;
                if ((ai.transform.position - transform.position).sqrMagnitude < range2) return true;
            }
            return false;
        }

        bool OtherInteraction()
        {
            foreach (var i in others)
                if (i.IsOpen || i.CanOpen) return true;
            return false;
        }

        /// <summary>Uma coisa cai na rede: peixe do cardume ou, às vezes, um achado.</summary>
        bool CatchOne()
        {
            if (!CurrentSpot) return false;
            bool extra = UnityEngine.Random.value < extraChance;
            var pick = Pick(extra);
            if (pick == null) return false;
            if (!extra && !CurrentSpot.Take()) return false;
            int amount = UnityEngine.Random.Range(Mathf.Max(1, pick.min), Mathf.Max(pick.min, pick.max) + 1);
            for (int i = 0; i < amount; i++) caught.Add(pick.item);
            // Respingo onde caiu: um ponto ao longo da rede.
            var p = netRoot.TransformPoint(new Vector3(0.6f, 0f, UnityEngine.Random.Range(-0.45f, 0.45f) * length));
            if (ocean != null) p.y = ocean.GetHeight(p);
            if (Services.TryGet(out CombatEffects fx)) fx.Plop(p, 0.9f);
            NetFilled?.Invoke(this, pick.item);
            return true;
        }

        Catch Pick(bool extra)
        {
            float total = 0f;
            foreach (var c in catches)
                if (c != null && c.item && c.extra == extra) total += Weight(c);
            if (total <= 0f) return null;
            float roll = UnityEngine.Random.value * total;
            foreach (var c in catches)
            {
                if (c == null || !c.item || c.extra != extra) continue;
                float w = Weight(c);
                if (w <= 0f) continue;
                roll -= w;
                if (roll <= 0f) return c;
            }
            return null;
        }

        /// <summary>Peso no sorteio, zero se o peixe não vem ali agora (região do navio, hora, tempo).</summary>
        float Weight(Catch c) => FishJournal.Allows(c.when, c.item, transform.position) ? c.weight : 0f;

        void BeginHaul()
        {
            State = Phase.Hauling;
            phaseStart = Time.time;
            Splash(1.4f);
            // Até 3 peixes diferentes aparecem dentro da rede subindo.
            int shown = 0;
            foreach (var s in showSlots) Show(s, null);
            for (int i = 0; i < caught.Count && shown < showSlots.Length; i++)
            {
                bool repeated = false;
                for (int k = 0; k < i; k++) if (caught[k] == caught[i]) repeated = true;
                if (!repeated && Show(showSlots[shown], caught[i])) shown++;
            }
        }

        /// <summary>O que estava na rede vai para o porão (o que não cabe volta para o mar).</summary>
        void Land()
        {
            landed.Clear();
            int lost = 0;
            for (int i = 0; i < caught.Count; i++)
            {
                var item = caught[i];
                int added = hold.Add(item, 1);
                if (added <= 0)
                {
                    lost++;
                    continue;
                }
                if (FishJournal.Current) FishJournal.Current.Log(item);
                int k = landed.FindIndex(s => s.item == item);
                if (k >= 0) landed[k] = new ItemStack(item, landed[k].amount + 1);
                else landed.Add(new ItemStack(item, 1));
            }
            caught.Clear();
            State = Phase.Idle;
            if (CurrentSpot) CurrentSpot.InUse = false;
            CurrentSpot = null;
            HideNet();
            if (deckCrew) deckCrew.StopNet();
            Hauled?.Invoke(this, landed, lost);
        }

        // ---------- Visual ----------

        /// <summary>Medidas do casco agora (cada classe de navio tem a sua): amurada do lado da rede, comprimento da rede.</summary>
        void MeasureHull()
        {
            if (!hull) hull = GetComponent<BoxCollider>();
            Vector3 c = hull ? hull.center : Vector3.zero, s = hull ? hull.size : new Vector3(4f, 3f, 12f);
            railLocal = new Vector3(side * (s.x * 0.5f + 0.1f), c.y + s.y * 0.5f, c.z);
            length = Mathf.Min(netLength, s.z * 0.4f);
        }

        /// <summary>
        /// Rede aberta na fração <paramref name="open"/> (0 = enrolada na amurada, 1 = na água com as boias na linha d'água). Descendo e
        /// subindo pelo mesmo caminho.
        /// </summary>
        void PlaceNet(float open, bool lowering)
        {
            if (!netRoot) return;
            float k = open * open * (3f - 2f * open);
            var outward = new Vector3(side * 0.45f, 0f, 0f);
            var railWorld = transform.TransformPoint(railLocal + outward);
            float water = ocean != null ? ocean.GetHeight(railWorld) : 0f;
            var pos = railWorld;
            pos.y = Mathf.Lerp(railWorld.y + 0.3f, water - 0.05f, k);
            netRoot.position = pos;
            // Pendurada em pé, mesmo com o navio balançando.
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f) netRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
            if (netBody) netBody.localScale = new Vector3(Mathf.Lerp(0.5f, netDepth, k), Mathf.Lerp(0.45f, netDepth, k), Mathf.Lerp(0.6f, 1f, k) * length);
            if (floats) floats.localScale = new Vector3(1f, 1f, Mathf.Lerp(0.6f, 1f, k) * length / Mathf.Max(0.01f, netLength));
            // Cabos da amurada até as pontas de cima da rede.
            for (int i = 0; i < ropes.Length; i++)
            {
                if (!ropes[i]) continue;
                float z = (i == 0 ? -0.5f : 0.5f) * length * Mathf.Lerp(0.6f, 1f, k);
                ropes[i].enabled = true;
                ropes[i].SetPosition(0, transform.TransformPoint(railLocal + new Vector3(0f, 0.2f, z)));
                ropes[i].SetPosition(1, netRoot.TransformPoint(new Vector3(0f, 0f, z)));
            }
        }

        void HideNet()
        {
            if (netRoot) netRoot.gameObject.SetActive(false);
            foreach (var r in ropes) if (r) r.enabled = false;
        }

        /// <summary>Liga no lugar só o modelo com o nome do item (nenhum com null); falso se o item não tem modelo ali.</summary>
        static bool Show(Transform slot, ItemDefinition item)
        {
            if (!slot) return false;
            bool found = false;
            for (int i = 0; i < slot.childCount; i++)
            {
                var child = slot.GetChild(i).gameObject;
                bool on = item && child.name == item.name;
                child.SetActive(on);
                found |= on;
            }
            return found;
        }

        void Splash(float size)
        {
            if (!netRoot || !Services.TryGet(out CombatEffects fx)) return;
            var p = netRoot.position;
            if (ocean != null) p.y = ocean.GetHeight(p);
            fx.Plop(p, size);
        }
    }
}
