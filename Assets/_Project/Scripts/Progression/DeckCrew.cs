using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Progression
{
    /// <summary>
    /// Marujos no convés (M12, passo 1: só visual). A tripulação do <see cref="ShipCrew"/> aparece como marujos andando no convés do navio do
    /// jogador: quantos aparecem segue a fração dos beliches ocupados (até <see cref="Layout.maxSailors"/> do casco). Andam entre pontos do
    /// convés e param (às vezes conversam: sim/não com a cabeça); ao mudar a vela, dois correm ao mastro e puxam; em combate (tiro dado ou
    /// levado) cada um corre para trás de um canhão: no disparo leva o tranco, vira, abaixa para pegar a bala nova e volta socando até a
    /// bateria ficar pronta; tiro no casco faz alguns
    /// se encolherem; baixa na tripulação = um marujo cai e some.
    /// <para>
    /// Tudo no espaço do navio (filhos dele: balançam junto), sem física nem NavMesh: o gerador mede o convés de cada casco
    /// (<see cref="Layout"/>: grade de pontos livres em cada nível, estações dos mastros e dos canhões no convés principal) e os caminhos são
    /// uma busca na grade, dentro do mesmo nível (sem escadas: quem está na popa fica na popa, de vigia; os canhões e os cabos são do convés
    /// principal, onde fica a maioria). Os marujos são montados pelo gerador (sem colisor); longe da câmera dormem (Animator desligado).
    /// </para>
    /// </summary>
    public class DeckCrew : MonoBehaviour
    {
        [Serializable]
        public struct Station
        {
            public Vector3 position;
            public float yaw;
            /// <summary>Canhão: −1 bombordo, 1 estibordo (<see cref="BroadsideSide"/>); 0 = mastro.</summary>
            public int side;
        }

        /// <summary>Convés de um casco, medido pelo gerador (espaço do navio).</summary>
        [Serializable]
        public class Layout
        {
            public ShipClassDefinition shipClass;
            public int maxSailors = 5;
            public float deckY;
            public Vector2 gridMin;
            public float cell = 0.25f;
            public int cols, rows;
            public bool[] walkable = Array.Empty<bool>();
            /// <summary>Altura do chão em cada célula (NaN = sem casco). Níveis diferentes (popa, proa) são áreas à parte, sem escada.</summary>
            public float[] heights = Array.Empty<float>();
            public Station[] rigging = Array.Empty<Station>();
            public Station[] guns = Array.Empty<Station>();
        }

        [SerializeField] Layout[] layouts = Array.Empty<Layout>();
        [SerializeField] Animator[] sailors = Array.Empty<Animator>();
        [SerializeField] float walkSpeed = 1.1f, runSpeed = 3.2f;
        [Tooltip("Quanto tempo depois do último tiro (dado ou levado) os marujos continuam nos canhões (s).")]
        [SerializeField] float combatSeconds = 25f;
        [Tooltip("Câmera mais longe que isto (fora de combate): marujos parados (Animator desligado). Mirando os canhões a câmera vai até o " +
                 "ponto de mira, a mais de 100 m do navio: por isso em combate eles nunca dormem.")]
        [SerializeField] float sleepDistance = 250f;
        [Tooltip("Quanto tempo eles puxam os cabos ao mudar a vela (s).")]
        [SerializeField] float riggingSeconds = 3f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int LocomotionId = Animator.StringToHash("Locomocao");
        static readonly int PullId = Animator.StringToHash("Puxar");
        static readonly int DuckId = Animator.StringToHash("Abaixar");
        static readonly int HitId = Animator.StringToHash("Tranco");
        static readonly int DeathId = Animator.StringToHash("Morte");
        static readonly int YesId = Animator.StringToHash("Sim");
        static readonly int NoId = Animator.StringToHash("Nao");

        enum Task { Idle, Wander, Rigging, Gun, Net }

        sealed class Sailor
        {
            public Animator animator;
            public Transform transform;
            public bool shown, dying;
            public Task task;
            public int station = -1;
            public readonly Vector3[] path = new Vector3[48];
            public int pathLength, pathIndex;
            public bool running, arrived;
            public float wait, busyUntil, taskUntil, speed, yaw, firedAt = float.NegativeInfinity;
            public bool fetched;
            public Vector3 goal;
        }

        readonly List<Sailor> crewOnDeck = new();
        readonly List<int> freeCells = new();
        /// <summary>Células livres de cada área (nível) do convés; montadas ao trocar de casco.</summary>
        readonly List<List<int>> zoneCells = new();
        readonly CannonBattery[] batteries = new CannonBattery[2];
        int[] previous = Array.Empty<int>(), queue = Array.Empty<int>(), zone = Array.Empty<int>();
        int mainZone = -1;
        Layout layout;
        ShipController ship;
        ShipCrew crew;
        Health health;
        PlayerShipClass shipClass;
        Transform deck;
        Camera cam;
        int pendingDeaths, shownTarget = -1;
        float combatUntil = -999f, nextSleepCheck;
        bool asleep;

        public void Configure(Layout[] deckLayouts, Animator[] sailorModels)
        {
            layouts = deckLayouts;
            sailors = sailorModels;
        }

        public bool InCombat => Time.time < combatUntil;
        public int Shown { get { int n = 0; foreach (var s in crewOnDeck) if (s.shown && !s.dying) n++; return n; } }

        void Awake()
        {
            ship = GetComponent<ShipController>();
            crew = GetComponent<ShipCrew>();
            health = GetComponent<Health>();
            shipClass = GetComponent<PlayerShipClass>();
            foreach (var a in sailors)
            {
                if (!a) continue;
                a.keepAnimatorStateOnDisable = true;
                // O código anda e gira o suporte (pai do modelo): alguns clipes do kit (tranco, abaixar) mexem na raiz do modelo e
                // anulavam o giro feito nele.
                var holder = a.transform.parent;
                holder.gameObject.SetActive(false);
                crewOnDeck.Add(new Sailor { animator = a, transform = holder });
            }
            if (crewOnDeck.Count > 0) deck = crewOnDeck[0].transform.parent;
        }

        void OnEnable()
        {
            if (ship)
            {
                ship.SailChanged += OnSailChanged;
                ship.HullChanged += OnHullChanged;
            }
            if (health) health.Damaged += OnDamaged;
            if (crew) crew.Lost += OnCrewLost;
            // Religado depois de desligado: as baterias saíram no OnDisable (no Start o OnHullChanged liga).
            if (layout != null) HookBatteries();
        }

        void OnDisable()
        {
            if (ship)
            {
                ship.SailChanged -= OnSailChanged;
                ship.HullChanged -= OnHullChanged;
            }
            if (health) health.Damaged -= OnDamaged;
            if (crew) crew.Lost -= OnCrewLost;
            UnhookBatteries();
        }

        void Start() => OnHullChanged();

        // ---------- Casco, baterias, quantos aparecem ----------

        void OnHullChanged()
        {
            var current = shipClass ? shipClass.Current : null;
            layout = null;
            foreach (var l in layouts)
                if (l != null && (l.shipClass == current || layout == null)) layout = l;
            if (layout == null || layout.cols * layout.rows == 0) return;

            int cells = layout.cols * layout.rows;
            if (previous.Length < cells)
            {
                previous = new int[cells];
                queue = new int[cells];
                zone = new int[cells];
            }
            freeCells.Clear();
            for (int i = 0; i < cells; i++)
                if (layout.walkable[i]) freeCells.Add(i);
            MarkZones();

            HookBatteries();

            // Casco novo: todo mundo reaparece num ponto livre do convés novo (a maioria no principal).
            int placed = 0;
            foreach (var s in crewOnDeck)
            {
                if (!s.shown) continue;
                PlaceRandom(s, placed++ <= layout.guns.Length);
                Idle(s, Random.Range(0.5f, 3f));
            }
            shownTarget = -1;
        }

        void HookBatteries()
        {
            UnhookBatteries();
            foreach (var b in GetComponentsInChildren<CannonBattery>())
                batteries[b.Side == BroadsideSide.Port ? 0 : 1] = b;
            if (batteries[0]) batteries[0].Fired += OnPortFired;
            if (batteries[1]) batteries[1].Fired += OnStarboardFired;
        }

        void UnhookBatteries()
        {
            if (batteries[0]) batteries[0].Fired -= OnPortFired;
            if (batteries[1]) batteries[1].Fired -= OnStarboardFired;
            batteries[0] = batteries[1] = null;
        }

        int Desired()
        {
            if (!crew || layout == null || crew.Count <= 0 || crew.Berths <= 0) return 0;
            int max = Mathf.Min(layout.maxSailors, crewOnDeck.Count);
            return Mathf.Clamp(Mathf.CeilToInt(crew.Fraction * max), 1, max);
        }

        void SyncCount()
        {
            int target = Desired();
            if (target == shownTarget && pendingDeaths == 0) return;
            shownTarget = target;
            int alive = Shown;
            // Sobra gente no convés: baixa = cai morto; descarregou/vendeu = some.
            for (int i = crewOnDeck.Count - 1; i >= 0 && alive > target; i--)
            {
                var s = crewOnDeck[i];
                if (!s.shown || s.dying) continue;
                if (pendingDeaths > 0)
                {
                    pendingDeaths--;
                    Die(s);
                }
                else Hide(s);
                alive--;
            }
            pendingDeaths = 0;
            for (int i = 0; i < crewOnDeck.Count && alive < target; i++)
            {
                var s = crewOnDeck[i];
                if (s.shown) continue;
                s.shown = true;
                s.dying = false;
                s.transform.gameObject.SetActive(true);
                s.animator.enabled = !asleep;
                PlaceRandom(s, OnMainDeck() <= layout.guns.Length);
                Idle(s, Random.Range(0.3f, 2f));
                alive++;
            }
        }

        void Hide(Sailor s)
        {
            s.shown = false;
            s.dying = false;
            s.station = -1;
            s.transform.gameObject.SetActive(false);
        }

        void Die(Sailor s)
        {
            s.dying = true;
            s.station = -1;
            s.pathLength = 0;
            s.busyUntil = Time.time + 2.6f;
            s.animator.SetFloat(SpeedId, 0f);
            s.animator.CrossFadeInFixedTime(DeathId, 0.1f);
        }

        // ---------- Eventos ----------

        void OnSailChanged(int index)
        {
            if (layout == null || layout.rigging.Length == 0 || InCombat) return;
            // Dois (os mais perto de um mastro e que não estão no canhão) correm para os cabos.
            int sent = 0;
            for (int r = 0; r < layout.rigging.Length && sent < 2; r++)
            {
                if (Taken(r, Task.Rigging)) continue;
                Sailor best = null;
                float bestDistance = float.MaxValue;
                foreach (var s in crewOnDeck)
                {
                    if (!s.shown || s.dying || s.task == Task.Rigging || s.task == Task.Gun || s.task == Task.Net || ZoneOf(s) != mainZone) continue;
                    float d = (s.transform.localPosition - layout.rigging[r].position).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = s; }
                }
                if (best == null) break;
                Assign(best, Task.Rigging, r, true);
                best.taskUntil = float.PositiveInfinity;
                sent++;
            }
        }

        /// <summary>
        /// Rede de pesca (D2, passo 2): os dois marujos mais perto vão à amurada do bordo <paramref name="side"/> (−1 bombordo, 1 estibordo:
        /// os lugares dos canhões do meio desse lado) e puxam o cabo até <see cref="StopNet"/>.
        /// </summary>
        public void WorkNet(int side)
        {
            if (layout == null || InCombat) return;
            int sent = 0;
            while (sent < 2)
            {
                int station = -1;
                float bestMiddle = float.MaxValue;
                for (int g = 0; g < layout.guns.Length; g++)
                {
                    if (layout.guns[g].side != side || Taken(g, Task.Net)) continue;
                    float middle = Mathf.Abs(layout.guns[g].position.z);
                    if (middle < bestMiddle) { bestMiddle = middle; station = g; }
                }
                if (station < 0) break;
                Sailor best = null;
                float bestDistance = float.MaxValue;
                foreach (var s in crewOnDeck)
                {
                    if (!s.shown || s.dying || s.task == Task.Net || s.task == Task.Gun || ZoneOf(s) != mainZone) continue;
                    float d = (s.transform.localPosition - layout.guns[station].position).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = s; }
                }
                if (best == null) break;
                Assign(best, Task.Net, station, true);
                sent++;
            }
        }

        /// <summary>Rede recolhida: quem estava nela volta a passear.</summary>
        public void StopNet()
        {
            foreach (var s in crewOnDeck)
                if (s.shown && !s.dying && s.task == Task.Net) Idle(s, Random.Range(0.5f, 2f));
        }

        void OnPortFired() => OnFired(-1);
        void OnStarboardFired() => OnFired(1);

        void OnFired(int side)
        {
            StartCombat();
            foreach (var s in crewOnDeck)
            {
                if (!s.shown || s.dying || s.task != Task.Gun || !s.arrived || layout.guns[s.station].side != side) continue;
                // Coice do canhão: tranco para trás; depois o ciclo da recarga no Update (pega a bala atrás e soca).
                s.firedAt = Time.time;
                s.fetched = false;
                s.busyUntil = Time.time + 0.45f;
                s.animator.CrossFadeInFixedTime(HitId, 0.06f);
            }
        }

        void OnDamaged(DamageInfo info)
        {
            StartCombat();
            foreach (var s in crewOnDeck)
            {
                if (!s.shown || s.dying || Random.value > 0.4f) continue;
                s.busyUntil = Time.time + 0.6f;
                s.animator.CrossFadeInFixedTime(HitId, 0.08f);
            }
        }

        void OnCrewLost(int count) => pendingDeaths += count;

        void StartCombat()
        {
            bool starting = !InCombat;
            combatUntil = Time.time + combatSeconds;
            if (!starting || layout == null || layout.guns.Length == 0) return;
            // Cada um vai para um canhão, alternando os bordos; quem sobra continua no que estava.
            int next = 0;
            foreach (var s in crewOnDeck)
            {
                if (!s.shown || s.dying || ZoneOf(s) != mainZone) continue;
                int station = GunFor(next++);
                if (station < 0) break;
                Assign(s, Task.Gun, station, true);
            }
            // Quem ficou parado em cima do lugar de um artilheiro sai da frente.
            foreach (var s in crewOnDeck)
            {
                if (!s.shown || s.dying || s.task == Task.Gun || !s.arrived) continue;
                foreach (var g in layout.guns)
                    if ((g.position - s.transform.localPosition).sqrMagnitude < 0.5f)
                    {
                        Wander(s);
                        break;
                    }
            }
        }

        int OnMainDeck()
        {
            int n = 0;
            foreach (var s in crewOnDeck)
                if (s.shown && !s.dying && ZoneOf(s) == mainZone) n++;
            return n;
        }

        int ZoneOf(Sailor s)
        {
            int c = NearestFree(CellOf(s.transform.localPosition));
            return c < 0 ? -1 : zone[c];
        }

        /// <summary>Áreas ligadas da grade (mesmo nível); a principal é a dos canhões.</summary>
        void MarkZones()
        {
            int cells = layout.cols * layout.rows, next = 0;
            for (int i = 0; i < cells; i++) zone[i] = -1;
            for (int i = 0; i < cells; i++)
            {
                if (!layout.walkable[i] || zone[i] >= 0) continue;
                int head = 0, tail = 0;
                queue[tail++] = i;
                zone[i] = next;
                while (head < tail)
                {
                    int c = queue[head++], cx = c % layout.cols, cz = c / layout.cols;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= layout.cols || nz >= layout.rows) continue;
                        int n = nz * layout.cols + nx;
                        if (zone[n] >= 0 || !layout.walkable[n] || Mathf.Abs(layout.heights[n] - layout.heights[c]) >= 0.06f) continue;
                        zone[n] = next;
                        queue[tail++] = n;
                    }
                }
                next++;
            }
            zoneCells.Clear();
            for (int z = 0; z < next; z++) zoneCells.Add(new List<int>());
            for (int i = 0; i < cells; i++)
                if (zone[i] >= 0) zoneCells[zone[i]].Add(i);
            var anchor = layout.guns.Length > 0 ? layout.guns[0].position : new Vector3(0f, layout.deckY, 0f);
            int a = NearestFree(CellOf(anchor));
            mainZone = a >= 0 ? zone[a] : 0;
        }

        /// <summary>n-ésimo canhão na ordem bombordo, estibordo, bombordo... (do meio para as pontas).</summary>
        int GunFor(int n)
        {
            if (n >= layout.guns.Length) return -1;
            int port = 0, starboard = 0;
            for (int i = 0; i < layout.guns.Length; i++)
                if (layout.guns[i].side < 0) port++;
                else starboard++;
            int wantSide = n % 2 == 0 ? -1 : 1, k = n / 2;
            if ((wantSide < 0 ? port : starboard) <= k) wantSide = -wantSide;
            int seen = 0;
            for (int i = 0; i < layout.guns.Length; i++)
            {
                if (layout.guns[i].side != wantSide) continue;
                if (seen++ == k) return i;
            }
            return -1;
        }

        bool Taken(int station, Task task)
        {
            foreach (var s in crewOnDeck)
                if (s.shown && !s.dying && s.task == task && s.station == station) return true;
            return false;
        }

        // ---------- Quadro a quadro ----------

        void Update()
        {
            if (layout == null || crewOnDeck.Count == 0) return;
            SyncCount();

            if (Time.time >= nextSleepCheck)
            {
                nextSleepCheck = Time.time + 0.5f;
                if (!cam) cam = Camera.main;
                bool far = !InCombat && cam && (cam.transform.position - transform.position).sqrMagnitude > sleepDistance * sleepDistance;
                if (far != asleep)
                {
                    asleep = far;
                    foreach (var s in crewOnDeck)
                        if (s.shown) s.animator.enabled = !far;
                }
            }
            if (asleep) return;

            float now = Time.time, dt = Time.deltaTime;
            bool combat = InCombat;
            foreach (var s in crewOnDeck)
            {
                if (!s.shown) continue;
                if (s.dying)
                {
                    if (now >= s.busyUntil) Hide(s);
                    continue;
                }
                if (!combat && s.task == Task.Gun) Idle(s, Random.Range(0.5f, 2.5f));
                if (now < s.busyUntil)
                {
                    SetSpeed(s, 0f, dt);
                    continue;
                }
                if (!s.arrived)
                {
                    Walk(s, dt);
                    continue;
                }
                switch (s.task)
                {
                    case Task.Idle:
                        SetSpeed(s, 0f, dt);
                        Turn(s, s.yaw, dt);
                        s.wait -= dt;
                        if (s.wait > 0f) break;
                        if (Random.value < 0.25f)
                        {
                            // Conversa: sim ou não com a cabeça, e fica mais um pouco.
                            s.animator.CrossFadeInFixedTime(Random.value < 0.5f ? YesId : NoId, 0.2f);
                            s.busyUntil = now + 1.6f;
                            s.wait = Random.Range(1.5f, 4f);
                        }
                        else Wander(s);
                        break;
                    case Task.Wander:
                        Idle(s, Random.Range(2f, 6f));
                        break;
                    case Task.Rigging:
                        Turn(s, layout.rigging[s.station].yaw, dt);
                        if (float.IsPositiveInfinity(s.taskUntil)) s.taskUntil = now + riggingSeconds;
                        if (now >= s.taskUntil) Idle(s, Random.Range(0.5f, 2f));
                        else Repeat(s, PullId);
                        break;
                    case Task.Gun:
                        Gunner(s, now, dt);
                        break;
                    case Task.Net:
                        // De frente para fora, puxando o cabo da rede.
                        SetSpeed(s, 0f, dt);
                        Turn(s, layout.guns[s.station].yaw, dt);
                        Repeat(s, PullId);
                        break;
                }
            }
        }

        /// <summary>
        /// Artilheiro na recarga (só com as animações do kit): depois do tranco do disparo vira para dentro, abaixa para pegar a bala nova
        /// (Duck), volta para o canhão e soca a bala (o soco) até a bateria ficar pronta. Chegou com a bateria já recarregando: só soca.
        /// </summary>
        void Gunner(Sailor s, float now, float dt)
        {
            float gunYaw = layout.guns[s.station].yaw;
            var battery = batteries[layout.guns[s.station].side < 0 ? 0 : 1];
            if (!battery || battery.State != BatteryState.Reloading)
            {
                SetSpeed(s, 0f, dt);
                Turn(s, gunYaw, dt);
                return;
            }
            float t = now - s.firedAt;
            if (t < 1.45f)
            {
                SetSpeed(s, 0f, dt);
                // Vira para dentro, meio de lado (para onde ficam as balas).
                Turn(s, gunYaw + 160f, dt);
                if (t >= 0.8f && !s.fetched)
                {
                    s.fetched = true;
                    s.animator.CrossFadeInFixedTime(DuckId, 0.12f);
                }
                return;
            }
            Turn(s, gunYaw, dt);
            if (t < 1.9f) SetSpeed(s, 0f, dt);
            else Repeat(s, PullId);
        }

        /// <summary>Ação em laço (puxar o cabo, socar a bala): começa de novo quando acaba.</summary>
        void Repeat(Sailor s, int state)
        {
            var info = s.animator.GetCurrentAnimatorStateInfo(0);
            if (s.animator.IsInTransition(0)) return;
            if (info.shortNameHash != state || info.normalizedTime >= 0.92f) s.animator.CrossFadeInFixedTime(state, 0.15f);
        }

        void SetSpeed(Sailor s, float target, float dt)
        {
            s.speed = Mathf.MoveTowards(s.speed, target, dt * 6f);
            s.animator.SetFloat(SpeedId, s.speed);
        }

        void Turn(Sailor s, float yaw, float dt)
        {
            // Pelo ângulo (RotateTowards travava num giro de 180°, de costas para o canhão).
            var t = s.transform;
            t.localRotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(t.localEulerAngles.y, yaw, 360f * dt), 0f);
        }

        void Walk(Sailor s, float dt)
        {
            float speed = s.running ? runSpeed : walkSpeed;
            SetSpeed(s, speed, dt);
            var t = s.transform;
            var p = t.localPosition;
            var to = s.path[s.pathIndex];
            var delta = to - p;
            delta.y = 0f;
            float step = s.speed * dt;
            if (delta.sqrMagnitude <= step * step)
            {
                t.localPosition = to;
                if (++s.pathIndex >= s.pathLength)
                {
                    s.arrived = true;
                    s.wait = Random.Range(2f, 6f);
                }
                return;
            }
            var dir = delta.normalized;
            t.localPosition = new Vector3(p.x + dir.x * step, to.y, p.z + dir.z * step);
            Turn(s, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, dt);
        }

        void Idle(Sailor s, float seconds)
        {
            if (!s.animator.IsInTransition(0) && s.animator.GetCurrentAnimatorStateInfo(0).shortNameHash != LocomotionId)
                s.animator.CrossFadeInFixedTime(LocomotionId, 0.25f);
            s.task = Task.Idle;
            s.station = -1;
            s.arrived = true;
            s.pathLength = 0;
            s.wait = seconds;
            s.yaw = s.transform.localEulerAngles.y + Random.Range(-60f, 60f);
        }

        void Wander(Sailor s)
        {
            var from = s.transform.localPosition;
            int own = ZoneOf(s);
            if (own < 0 || own >= zoneCells.Count) return;
            var cellsHere = zoneCells[own];
            // Convés pequeno: passos curtos (a partir de ~0,6 m) também valem.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var p = CellCenter(cellsHere[Random.Range(0, cellsHere.Count)]);
                float d = (p - from).sqrMagnitude;
                if (d < 0.36f || d > 16f || Crowded(p, s)) continue;
                if (!FindPath(s, p)) continue;
                s.task = Task.Wander;
                s.station = -1;
                s.running = false;
                return;
            }
            s.wait = Random.Range(1f, 3f);
        }

        void Assign(Sailor s, Task task, int station, bool run)
        {
            var target = task == Task.Rigging ? layout.rigging[station].position : layout.guns[station].position;
            s.task = task;
            s.station = station;
            s.running = run;
            // Chamado no meio de uma conversa: larga e vai.
            s.busyUntil = 0f;
            if (!s.animator.IsInTransition(0) && s.animator.GetCurrentAnimatorStateInfo(0).shortNameHash != LocomotionId)
                s.animator.CrossFadeInFixedTime(LocomotionId, 0.15f);
            // Sem caminho pela grade (outro nível): fica onde está.
            if (!FindPath(s, target)) Idle(s, Random.Range(1f, 3f));
        }

        bool Crowded(Vector3 p, Sailor self)
        {
            foreach (var o in crewOnDeck)
            {
                if (o == self || !o.shown) continue;
                var other = o.arrived ? o.transform.localPosition : o.path[Mathf.Max(0, o.pathLength - 1)];
                if ((other - p).sqrMagnitude < 0.45f) return true;
            }
            return false;
        }

        void PlaceRandom(Sailor s, bool mainDeck)
        {
            if (freeCells.Count == 0) return;
            var p = CellCenter(freeCells[0]);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int cell = freeCells[Random.Range(0, freeCells.Count)];
                if (mainDeck && zone[cell] != mainZone && attempt < 20) continue;
                p = CellCenter(cell);
                if (!Crowded(p, s)) break;
            }
            s.transform.SetLocalPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            s.pathLength = 0;
            s.arrived = true;
            s.speed = 0f;
        }

        // ---------- Grade do convés ----------

        Vector3 CellCenter(int cell) => new(
            layout.gridMin.x + (cell % layout.cols + 0.5f) * layout.cell, layout.heights.Length > cell ? layout.heights[cell] : layout.deckY,
            layout.gridMin.y + (cell / layout.cols + 0.5f) * layout.cell);

        int CellOf(Vector3 p)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((p.x - layout.gridMin.x) / layout.cell), 0, layout.cols - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((p.z - layout.gridMin.y) / layout.cell), 0, layout.rows - 1);
            return cz * layout.cols + cx;
        }

        bool Free(Vector3 p, int area)
        {
            int cx = Mathf.FloorToInt((p.x - layout.gridMin.x) / layout.cell), cz = Mathf.FloorToInt((p.z - layout.gridMin.y) / layout.cell);
            if (cx < 0 || cz < 0 || cx >= layout.cols || cz >= layout.rows) return false;
            int c = cz * layout.cols + cx;
            return layout.walkable[c] && zone[c] == area;
        }

        bool LineFree(Vector3 a, Vector3 b, int area)
        {
            float length = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            int steps = Mathf.CeilToInt(length / (layout.cell * 0.5f));
            for (int i = 1; i < steps; i++)
                if (!Free(Vector3.Lerp(a, b, i / (float)steps), area)) return false;
            return true;
        }

        /// <summary>Busca em largura na grade (8 vizinhos) e depois corta os cantos onde a linha reta passa livre.</summary>
        bool FindPath(Sailor s, Vector3 goal)
        {
            var start = s.transform.localPosition;
            int from = NearestFree(CellOf(start)), to = NearestFree(CellOf(goal));
            if (from < 0 || to < 0 || zone[from] != zone[to]) return false;
            int area = zone[from];
            int cells = layout.cols * layout.rows;
            for (int i = 0; i < cells; i++) previous[i] = -2;
            int head = 0, tail = 0;
            queue[tail++] = from;
            previous[from] = -1;
            while (head < tail && previous[to] == -2)
            {
                int c = queue[head++], cx = c % layout.cols, cz = c / layout.cols;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= layout.cols || nz >= layout.rows) continue;
                    int n = nz * layout.cols + nx;
                    if (previous[n] != -2 || !layout.walkable[n] || zone[n] != area) continue;
                    // Diagonal só se os dois lados também estão livres (não corta quina).
                    if (dx != 0 && dz != 0 && (zone[cz * layout.cols + nx] != area || zone[nz * layout.cols + cx] != area)) continue;
                    previous[n] = c;
                    queue[tail++] = n;
                }
            }
            if (previous[to] == -2) return false;

            // Caminho de trás para frente (na fila, reaproveitada), depois só os pontos onde a linha reta deixa de passar.
            int count = 0;
            for (int c = to; c >= 0 && count < queue.Length; c = previous[c]) queue[count++] = c;
            int length = 0;
            var last = start;
            int k = count - 1;
            while (k > 0 && length < s.path.Length - 1)
            {
                int far = k - 1;
                while (far > 0 && LineFree(last, CellCenter(queue[far - 1]), area)) far--;
                last = CellCenter(queue[far]);
                s.path[length++] = last;
                k = far;
            }
            s.path[length++] = new Vector3(goal.x, CellCenter(to).y, goal.z);
            s.pathLength = length;
            s.pathIndex = 0;
            s.arrived = false;
            s.goal = goal;
            return true;
        }

        int NearestFree(int cell)
        {
            if (layout.walkable[cell]) return cell;
            int cx = cell % layout.cols, cz = cell / layout.cols;
            for (int r = 1; r < 6; r++)
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int nx = cx + dx, nz = cz + dz;
                if (nx < 0 || nz < 0 || nx >= layout.cols || nz >= layout.rows) continue;
                if (layout.walkable[nz * layout.cols + nx]) return nz * layout.cols + nx;
            }
            return -1;
        }
    }
}
