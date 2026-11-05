using System;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    /// <summary>
    /// A Esquadra Real (G, passo 3, 03/11): com o capitão procurado pela Coroa (<see cref="IWanted"/>, estrelas), navios de guerra de
    /// bandeira azul vêm atrás dele. Quantos e quais seguem as estrelas (<see cref="waves"/>: 1 estrela = nenhum, desde 04/11; 2 = uma
    /// corveta; 3 = uma fragata; 4 = fragata + corveta; 5 = a nau de guerra com uma fragata). Desde 04/11 eles procuram onde foi o crime e só perseguem quem veem: dá para
    /// despistar (<see cref="UpdateSight"/>). Chegam <see cref="arriveSeconds"/> depois de o jogador estar no mar procurado, de longe, atrás da câmera
    /// (como os caçadores); afundados, os que faltam voltam depois de <see cref="regroupSeconds"/> se ele continuar procurado. Afundar um
    /// navio da Coroa é crime (o WantedLevel soma). Sem estrelas (a procura caiu ou o Almirante perdoou), ou com o jogador no porto por
    /// <see cref="giveUpSeconds"/>, desistem: param de caçar e somem longe da câmera. Não contam para missões nem para a História (são da
    /// Coroa, não piratas: <see cref="ShipSunkEvent.crown"/>). Nada é salvo: carregar o jogo começa sem esquadra (as estrelas são salvas).
    /// </summary>
    public class RoyalNavy : MonoBehaviour
    {
        [Serializable]
        public class ShipType
        {
            public string label = "Corveta da Coroa";
            [Tooltip("Molde (desligado, fora do mapa); cada navio é uma cópia dele.")]
            public GameObject template;
            [Min(1f)] public float hull = 220f;
            public CannonStats cannons;
            [Min(0)] public int fame = 8;
            [Range(0f, 0.6f)] public float leadError = 0.25f;
            [Range(0f, 0.3f)] public float rangeError = 0.05f;
            public Vector2 reaction = new(0.3f, 0.7f);
        }

        [Serializable]
        public class Wave
        {
            [Tooltip("Índices de shipTypes que vêm com esta quantidade de estrelas.")]
            public int[] ships = Array.Empty<int>();
        }

        [SerializeField] Transform player;
        [SerializeField] ShipType[] shipTypes = Array.Empty<ShipType>();
        [Tooltip("Uma por estrela: a 1ª vale para 1 estrela, a 2ª para 2... (acima do fim da lista, a última).")]
        [SerializeField] Wave[] waves = Array.Empty<Wave>();
        [Tooltip("Segundos no mar procurado até a esquadra chegar.")]
        [SerializeField] float arriveSeconds = 15f;
        [Tooltip("Segundos até os navios afundados serem trocados por outros (se o capitão continua procurado).")]
        [SerializeField] float regroupSeconds = 120f;
        [SerializeField] float spawnDistance = 220f;
        [Tooltip("Jogador no porto (ou ancorado, a pé) por este tempo: a esquadra desiste.")]
        [SerializeField] float giveUpSeconds = 90f;
        [Tooltip("Desistindo: some a esta distância da câmera ou depois de leaveSeconds.")]
        [SerializeField] float vanishDistance = 230f;
        [SerializeField] float leaveSeconds = 60f;

        [Header("Despistar (04/11)")]
        [Tooltip("Até onde um navio da Coroa vê o capitão (persegue dentro disso; fora, procura onde ele foi visto por último).")]
        [SerializeField] float sightRange = 350f;
        [Tooltip("Segundos fora da vista de todos os navios até a esquadra perder a pista.")]
        [SerializeField] float loseSeconds = 60f;
        [Tooltip("Sem pista, chegam a esta distância do último lugar onde o capitão foi visto (ou do crime).")]
        [SerializeField] float searchSpawnDistance = 140f;
        [Tooltip("De quanto em quanto tempo os navios que não o veem rumam para o último lugar visto.")]
        [SerializeField] float radioSeconds = 4f;

        class Live
        {
            public GameObject ship;
            public Health health;
            public EnemyShipAI ai;
            public int type;
            public float leavingSince = -1f;
        }

        readonly List<Live> fleet = new();
        readonly List<int> missing = new();
        float timer, safeFor;
        bool wasWanted;
        Camera cam;
        // Último lugar onde o capitão foi visto (ou o do crime); quando foi visto; pista perdida.
        Vector3 lastKnown;
        bool hasLastKnown, trailLost;
        float lastSeenTime = -999f, radioTimer;

        public static RoyalNavy Current { get; private set; }

        /// <summary>Navios chegaram (rótulos, para o aviso do HUD).</summary>
        public static event Action<IReadOnlyList<string>> Arrived;
        /// <summary>Um navio da Coroa afundou (o navio, pelo jogador?).</summary>
        public static event Action<GameObject, bool> ShipSunk;
        /// <summary>A esquadra desistiu (estrelas zeraram ou o capitão ficou no porto).</summary>
        public static event Action GaveUp;
        /// <summary>O capitão saiu da vista da esquadra por tempo bastante: ela perdeu a pista (procura no último lugar visto).</summary>
        public static event Action TrailLost;
        /// <summary>Um navio da Coroa viu o capitão de novo depois de perder a pista.</summary>
        public static event Action Spotted;

        /// <summary>Algum navio da Coroa vê o capitão agora.</summary>
        public bool SeesPlayer => Hunting > 0 && Time.time - lastSeenTime < 0.6f;
        /// <summary>A esquadra perdeu a pista (a procura cai mais rápido).</summary>
        public bool IsTrailLost => trailLost && Hunting > 0;
        /// <summary>Segundos fora da vista até perder a pista (−1 = vendo agora, pista já perdida ou sem navios).</summary>
        public float LoseIn => Hunting == 0 || trailLost || SeesPlayer ? -1f : Mathf.Max(0f, loseSeconds - (Time.time - lastSeenTime));

        /// <summary>Navios da Coroa no mar caçando agora.</summary>
        public int Hunting
        {
            get
            {
                int n = 0;
                for (int i = 0; i < fleet.Count; i++)
                    if (fleet[i].leavingSince < 0f) n++;
                return n;
            }
        }
        public IReadOnlyList<ShipType> Types => shipTypes;
        /// <summary>Segundos até a próxima leva chegar (−1 = nenhuma a caminho).</summary>
        public float NextArrival => missing.Count > 0 ? timer : -1f;
        public string LabelOf(GameObject ship)
        {
            for (int i = 0; i < fleet.Count; i++)
                if (fleet[i].ship == ship) return shipTypes[fleet[i].type].label;
            return null;
        }
        public bool IsRoyal(GameObject ship) => LabelOf(ship) != null;
        public int Count => fleet.Count;
        public GameObject ShipAt(int i) => fleet[i].ship;
        public bool LeavingAt(int i) => fleet[i].leavingSince >= 0f;

        public void Configure(Transform target, ShipType[] types, Wave[] starWaves)
        {
            player = target;
            shipTypes = types;
            waves = starWaves;
        }

        void Awake() => Current = this;

        void OnEnable()
        {
            MerchantShip.AttackedByPlayer += OnMerchantAttacked;
            MerchantShip.Sunk += OnMerchantSunk;
        }

        void OnDisable()
        {
            MerchantShip.AttackedByPlayer -= OnMerchantAttacked;
            MerchantShip.Sunk -= OnMerchantSunk;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // O crime marca onde a Coroa vai procurar.
        void OnMerchantAttacked(MerchantShip ship) => Witness(ship.transform.position);

        void OnMerchantSunk(MerchantShip ship, bool byPlayer)
        {
            if (byPlayer) Witness(ship.transform.position);
        }

        void Witness(Vector3 point)
        {
            lastKnown = point;
            hasLastKnown = true;
        }

        void Update()
        {
            if (!player) return;
            Prune();
            int stars = Services.TryGet(out IWanted wanted) ? wanted.Stars : 0;
            if (stars <= 0)
            {
                if (wasWanted) Dismiss();
                wasWanted = false;
                hasLastKnown = false;
                trailLost = false;
                missing.Clear();
                UpdateLeaving();
                return;
            }
            if (!wasWanted)
            {
                wasWanted = true;
                timer = arriveSeconds;
            }
            // Carregou o jogo procurado (sem o crime visto nesta sessão): procuram onde ele está agora.
            if (!hasLastKnown) Witness(player.position);
            UpdateLeaving();
            UpdateSight();

            if (SafeZone.At(player.position))
            {
                safeFor += Time.deltaTime;
                if (safeFor >= giveUpSeconds && Hunting > 0) Dismiss();
                return;
            }
            safeFor = 0f;

            Missing(stars);
            if (missing.Count == 0)
            {
                timer = Mathf.Max(timer, Mathf.Min(arriveSeconds, regroupSeconds));
                return;
            }
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            SpawnMissing();
            timer = regroupSeconds;
        }

        /// <summary>
        /// Despistar (04/11, pedido do Jhonata: "pode ficar chato sempre ser perseguido"): um navio da Coroa só persegue quem ele vê
        /// (<see cref="sightRange"/>). Visto, o lugar fica marcado; os que não o veem rumam para lá (pelo "rádio", a cada
        /// <see cref="radioSeconds"/>). Fora da vista de todos por <see cref="loseSeconds"/>, a esquadra perde a pista: continua
        /// procurando no último lugar visto, e a procura (WantedLevel) cai mais rápido.
        /// </summary>
        void UpdateSight()
        {
            if (Hunting == 0) return;
            float range2 = sightRange * sightRange;
            bool seen = false;
            for (int i = 0; i < fleet.Count && !seen; i++)
                if (fleet[i].leavingSince < 0f && (fleet[i].ship.transform.position - player.position).sqrMagnitude < range2)
                    seen = true;
            if (seen)
            {
                lastKnown = player.position;
                hasLastKnown = true;
                lastSeenTime = Time.time;
                if (trailLost)
                {
                    trailLost = false;
                    Spotted?.Invoke();
                }
            }
            else if (!trailLost && Time.time - lastSeenTime >= loseSeconds)
            {
                trailLost = true;
                TrailLost?.Invoke();
            }

            radioTimer -= Time.deltaTime;
            if (radioTimer > 0f) return;
            radioTimer = radioSeconds;
            for (int i = 0; i < fleet.Count; i++)
            {
                var f = fleet[i];
                if (f.leavingSince >= 0f || f.ai.State != EnemyShipAI.AIState.Patrol) continue;
                f.ai.SearchAround(lastKnown);
            }
        }

        /// <summary>Quais navios da leva das estrelas de agora ainda não estão no mar (os que estão indo embora não contam).</summary>
        void Missing(int stars)
        {
            missing.Clear();
            if (waves.Length == 0) return;
            var wave = waves[Mathf.Clamp(stars, 1, waves.Length) - 1];
            foreach (int type in wave.ships) missing.Add(type);
            for (int i = 0; i < fleet.Count; i++)
                if (fleet[i].leavingSince < 0f) missing.Remove(fleet[i].type);
        }

        void SpawnMissing()
        {
            var labels = new List<string>();
            for (int i = 0; i < missing.Count; i++)
            {
                int type = missing[i];
                if (type < 0 || type >= shipTypes.Length || !shipTypes[type].template) continue;
                Spawn(type, i);
                labels.Add(shipTypes[type].label);
            }
            missing.Clear();
            if (labels.Count == 0) return;
            // Chegou leva nova: a contagem para despistar começa agora.
            lastSeenTime = Time.time;
            trailLost = false;
            radioTimer = 0f;
            Arrived?.Invoke(labels);
        }

        /// <summary>Sandbox (F1): chama a leva das estrelas de agora (ao menos a de 1 estrela) na hora.</summary>
        public void SpawnNow()
        {
            if (!player) return;
            // 1 estrela não manda navio (04/11): o F1 chama ao menos a leva de 2.
            int stars = Services.TryGet(out IWanted wanted) ? Mathf.Max(2, wanted.Stars) : 2;
            if (!hasLastKnown) Witness(player.position);
            Missing(stars);
            SpawnMissing();
            timer = regroupSeconds;
        }

        /// <summary>Sandbox (F1): um navio do tipo dado (0 corveta, 1 fragata, 2 nau) agora, mesmo sem estrelas (a abordagem de teste).</summary>
        public GameObject SpawnTypeNow(int type)
        {
            if (!player || type < 0 || type >= shipTypes.Length) return null;
            if (!hasLastKnown) Witness(player.position);
            Spawn(type, fleet.Count);
            return fleet[fleet.Count - 1].ship;
        }

        void Spawn(int type, int slot)
        {
            var spec = shipTypes[type];
            var point = SpawnPoint(slot);
            var look = player.position - point;
            look.y = 0f;
            point.y = spec.template.transform.position.y;
            var ship = Instantiate(spec.template, point, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            ship.name = spec.template.name.Replace("_Molde", "");
            ship.SetActive(true);

            var health = ship.GetComponent<Health>();
            health.SetMax(spec.hull);
            health.SetCurrent(spec.hull);
            if (spec.cannons)
                foreach (var battery in ship.GetComponentsInChildren<CannonBattery>(true)) battery.SetStats(spec.cannons);
            var ai = ship.GetComponent<EnemyShipAI>();
            ai.SetFame(spec.fame);
            ai.ConfigureGunnery(spec.rangeError, spec.reaction, 16f, 0f, spec.leadError);
            // Só persegue quem vê; senão procura em volta do último lugar visto (despistar, 04/11).
            ai.Hunt(sightRange);
            ai.SearchAround(lastKnown);
            var live = new Live { ship = ship, health = health, ai = ai, type = type };
            health.Died += () => OnSunk(live);
            fleet.Add(live);
        }

        // Os navios da Coroa só brigam com o jogador: quem afunda um é ele.
        void OnSunk(Live live) => ShipSunk?.Invoke(live.ship, true);

        void Prune()
        {
            for (int i = fleet.Count - 1; i >= 0; i--)
            {
                var f = fleet[i];
                if (!f.ship || !f.health || f.health.IsDead) fleet.RemoveAt(i);
            }
        }

        /// <summary>Desistem: param de caçar (voltam a patrulhar onde estão) e somem longe da câmera.</summary>
        void Dismiss()
        {
            bool any = false;
            for (int i = 0; i < fleet.Count; i++)
            {
                if (fleet[i].leavingSince >= 0f) continue;
                fleet[i].leavingSince = Time.time;
                fleet[i].ai.Hunt(0f);
                any = true;
            }
            safeFor = 0f;
            timer = arriveSeconds;
            if (any) GaveUp?.Invoke();
        }

        void UpdateLeaving()
        {
            if (!cam) cam = Camera.main;
            for (int i = fleet.Count - 1; i >= 0; i--)
            {
                var f = fleet[i];
                if (f.leavingSince < 0f) continue;
                bool far = cam && (f.ship.transform.position - cam.transform.position).sqrMagnitude > vanishDistance * vanishDistance;
                if (!far && Time.time - f.leavingSince < leaveSeconds) continue;
                Destroy(f.ship);
                fleet.RemoveAt(i);
            }
        }

        /// <summary>
        /// Com o capitão longe do último lugar visto (ou do crime), a leva chega lá e procura em volta (despistar); perto, chega longe,
        /// de preferência atrás da câmera, em água aberta. Cada navio da leva um pouco para o lado do outro.
        /// </summary>
        Vector3 SpawnPoint(int slot)
        {
            if (hasLastKnown && (lastKnown - player.position).sqrMagnitude > sightRange * sightRange)
            {
                for (int step = 0; step < 18; step++)
                {
                    float angle = slot * 120f + step * 40f;
                    var p = lastKnown + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * searchSpawnDistance;
                    if (!Land(p) && (p - player.position).sqrMagnitude > sightRange * sightRange * 0.5f) return p;
                }
            }
            if (!cam) cam = Camera.main;
            var back = cam ? -cam.transform.forward : -player.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
            back = Quaternion.AngleAxis((slot % 2 == 0 ? 1f : -1f) * 25f * slot, Vector3.up) * back;
            var fallback = player.position + back * spawnDistance;
            for (int step = 0; step < 18; step++)
            {
                float angle = (step + 1) / 2 * 20f * (step % 2 == 0 ? 1f : -1f);
                var p = player.position + Quaternion.AngleAxis(angle, Vector3.up) * back * spawnDistance;
                if (!Land(p)) return p;
            }
            return fallback;
        }

        static bool Land(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out var hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.point.y > -4f) return true;
            return Physics.CheckSphere(new Vector3(p.x, 0f, p.z), 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
