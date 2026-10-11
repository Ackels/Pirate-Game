using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Tentáculo da lula gigante: o modelo do kit Quaternius (15 ossos em fila) movido só por código. A forma é uma curva (Bezier
    /// cúbica, quatro pontos no mundo) e cada osso aponta para o ponto da curva na distância dele desde a base; curva mais curta
    /// que o tentáculo = a ponta segue reta (no agarrão ela passa por cima do convés). O dono (<see cref="GiantSquid"/>) diz o modo
    /// e onde ficam a base e o alvo; o tentáculo só monta a curva de cada modo e corre atrás dela (suave, rápido na pancada).
    /// Tem <see cref="Combat.Health"/> própria, mas **nenhum colisor**: a lula dá o dano do tiro à queima-roupa (o bordo que dispara
    /// acerta os tentáculos colados nele). Colisor aqui empurrava o casco e afundava o navio (o "ignorar colisão" do Unity se desfaz
    /// quando o tentáculo some e volta).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class SquidTentacle : MonoBehaviour
    {
        public enum Mode { Hidden, Rising, Idle, WindUp, Slam, Pinned, Grab, Hurt, Sinking }

        [Tooltip("Filho com os renderers e os colisores (desligado quando o tentáculo está escondido).")]
        [SerializeField] GameObject model;
        [Tooltip("Ossos da base à ponta (Tentacle1 ... Tentacle15_end).")]
        [SerializeField] Transform[] bones;
        [Tooltip("Segundos subindo da água.")]
        [SerializeField] float riseSeconds = 1.1f;
        [Tooltip("Segundos se debatendo ao ser destruído, antes de afundar.")]
        [SerializeField] float hurtSeconds = 0.7f;
        [SerializeField] float sinkSpeed = 7f;

        Health health;
        Quaternion[] bind;
        float[] along;
        float length;
        readonly Vector3[] ctrl = new Vector3[4];
        readonly Vector3[] goal = new Vector3[4];
        const int TableSize = 24;
        readonly Vector3[] table = new Vector3[TableSize + 1];
        readonly float[] tableLength = new float[TableSize + 1];
        float follow = 6f, modeStart, seed, sunk;
        bool posed;

        // O que o dono informou neste quadro.
        Vector3 basePoint, target, rail;
        float waterLevel;

        public Mode Current { get; private set; } = Mode.Hidden;
        public float ModeTime => Time.time - modeStart;
        public Health Health => health;
        public bool Alive => health && !health.IsDead;
        /// <summary>Metros do tentáculo inteiro (com a escala).</summary>
        public float Length => length;
        /// <summary>Lugar que o dono reservou para este tentáculo (agarrão a bombordo/boreste, posto em volta do navio).</summary>
        public int Slot { get; set; } = -1;
        public Vector3 Tip => bones[bones.Length - 1].position;
        public bool Visible => Current != Mode.Hidden;
        /// <summary>A pancada chegou ao fim (o dono dá o dano uma vez).</summary>
        public bool SlamLanded => Current == Mode.Slam && ModeTime >= slamSeconds;

        float windUpSeconds = 1.1f, slamSeconds = 0.28f;

        public void Configure(GameObject modelRoot, Transform[] chain)
        {
            model = modelRoot;
            bones = chain;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            seed = Random.Range(0f, 100f);
            int n = bones.Length;
            bind = new Quaternion[n];
            along = new float[n];
            for (int i = 0; i < n; i++) bind[i] = bones[i].localRotation;
            for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector3.Distance(bones[i - 1].position, bones[i].position);
            length = along[n - 1];
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            Hide();
        }

        /// <summary>Some na hora (sem efeito): começo, volta ao covil.</summary>
        public void Hide()
        {
            Current = Mode.Hidden;
            if (model) model.SetActive(false);
            posed = false;
        }

        void SetMode(Mode mode, float speed)
        {
            Current = mode;
            modeStart = Time.time;
            follow = speed;
        }

        /// <summary>Sobe da água em <paramref name="at"/> (vida cheia de novo) e fica em pé olhando para <paramref name="aim"/>.</summary>
        public void Rise(Vector3 at, Vector3 aim, float water)
        {
            // Sempre de vida cheia (um que sobreviveu à luta anterior voltava com a vida de antes).
            if (health.IsDead) health.Revive();
            else health.SetCurrent(health.Max);
            basePoint = at;
            target = aim;
            waterLevel = water;
            if (model) model.SetActive(true);
            SetMode(Mode.Rising, 30f);
            BuildGoal();
            Snap();
        }

        public void Idle() => SetMode(Mode.Idle, 3f);

        public void WindUp(float seconds)
        {
            windUpSeconds = seconds;
            SetMode(Mode.WindUp, 4f);
        }

        public void Slam(float seconds)
        {
            slamSeconds = seconds;
            SetMode(Mode.Slam, 1f / Mathf.Max(0.05f, seconds) * 2.2f);
        }

        public void Pin() => SetMode(Mode.Pinned, 20f);

        /// <summary>Agarra o casco: sobe colado nele e passa por cima da amurada até o convés.</summary>
        public void Grab() => SetMode(Mode.Grab, 5f);

        /// <summary>Destruído (ou a lula morreu): se debate e afunda.</summary>
        public void Kill()
        {
            if (Current == Mode.Hidden || Current == Mode.Hurt || Current == Mode.Sinking) return;
            SetMode(Mode.Hurt, 10f);
        }

        /// <summary>Volta para baixo d'água sem se debater (a lula foi embora).</summary>
        public void Submerge()
        {
            if (Current == Mode.Hidden || Current == Mode.Sinking) return;
            sunk = 0f;
            SetMode(Mode.Sinking, 4f);
        }

        /// <summary>
        /// Onde fica a base (na água), para onde ele olha / bate (<paramref name="aim"/>) e, no agarrão, o ponto em cima da amurada
        /// (<paramref name="railPoint"/>). Chamado pelo dono a cada quadro.
        /// </summary>
        public void Track(Vector3 at, Vector3 aim, Vector3 railPoint, float water)
        {
            basePoint = at;
            target = aim;
            rail = railPoint;
            waterLevel = water;
        }

        void LateUpdate()
        {
            if (Current == Mode.Hidden) return;
            float t = ModeTime;
            switch (Current)
            {
                case Mode.Rising:
                    if (t >= riseSeconds) Idle();
                    break;
                case Mode.Hurt:
                    if (t >= hurtSeconds) { sunk = 0f; SetMode(Mode.Sinking, 6f); }
                    break;
                case Mode.Sinking:
                    sunk += sinkSpeed * Time.deltaTime;
                    if (sunk > length + 2f) { Hide(); return; }
                    break;
            }
            BuildGoal();
            float k = 1f - Mathf.Exp(-follow * Time.deltaTime);
            for (int i = 0; i < 4; i++) ctrl[i] = posed ? Vector3.Lerp(ctrl[i], goal[i], k) : goal[i];
            posed = true;
            Pose();
        }

        void Snap()
        {
            for (int i = 0; i < 4; i++) ctrl[i] = goal[i];
            posed = true;
            Pose();
        }

        /// <summary>A curva pedida para o modo atual.</summary>
        void BuildGoal()
        {
            Vector3 up = Vector3.up;
            Vector3 flat = target - basePoint;
            flat.y = 0f;
            Vector3 f = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(up, f);
            float h = length * 0.72f;
            float time = Time.time + seed;
            Vector3 b = basePoint;
            Vector3 wave = side * (Mathf.Sin(time * 0.9f) * 1.1f) + f * (Mathf.Sin(time * 0.67f + 1.3f) * 0.7f);

            switch (Current)
            {
                case Mode.WindUp:
                {
                    // Recua e sobe, tremendo: o aviso da pancada.
                    float r = Mathf.Clamp01(ModeTime / Mathf.Max(0.05f, windUpSeconds));
                    Vector3 tremble = side * (Mathf.Sin(time * 23f) * 0.25f * r);
                    goal[0] = b;
                    goal[1] = b + up * h * 0.5f;
                    goal[2] = b + up * h * 0.95f - f * (1f + 2.5f * r) + tremble;
                    goal[3] = b + up * h * (1f + 0.15f * r) - f * (2f + 3.5f * r) + tremble;
                    break;
                }
                case Mode.Slam:
                case Mode.Pinned:
                {
                    // Desce por cima e deita no alvo (o convés).
                    goal[0] = b;
                    goal[1] = b + up * h * 0.55f + f * 0.8f;
                    goal[2] = Vector3.Lerp(b + up * h * 0.8f, target, 0.55f) + up * 3f;
                    goal[3] = target;
                    break;
                }
                case Mode.Grab:
                {
                    // Base funda colada no casco, sobe pela lateral, passa a amurada e deita atravessando o convés.
                    float squeeze = Mathf.Sin(time * 2.2f) * 0.25f;
                    goal[0] = b;
                    goal[1] = b + up * Mathf.Max(2f, rail.y - b.y) * 0.8f;
                    goal[2] = rail + up * (0.9f + squeeze);
                    goal[3] = target + up * 0.2f;
                    break;
                }
                case Mode.Hurt:
                {
                    // Se debate: a ponta chicoteia para os lados.
                    Vector3 lash = side * (Mathf.Sin(time * 17f) * 3f) + f * (Mathf.Sin(time * 11f) * 2f);
                    goal[0] = b;
                    goal[1] = b + up * h * 0.5f;
                    goal[2] = b + up * h * 0.85f + lash * 0.5f;
                    goal[3] = b + up * h + lash;
                    break;
                }
                default:
                {
                    // Em pé, ondulando, com a ponta curvada para o alvo.
                    goal[0] = b;
                    goal[1] = b + up * h * 0.45f;
                    goal[2] = b + up * h * 0.85f - f * 1.2f + wave * 0.6f;
                    goal[3] = b + up * h + f * 2.2f + wave;
                    break;
                }
            }

            // Subindo: a curva toda começa funda e chega à superfície; afundando, o contrário.
            float drop = 0f;
            if (Current == Mode.Rising) drop = (1f - Mathf.SmoothStep(0f, 1f, ModeTime / riseSeconds)) * (length + 1f);
            else if (Current == Mode.Sinking) drop = sunk;
            if (drop > 0f)
                for (int i = 0; i < 4; i++) goal[i] -= up * drop;
        }

        static Vector3 Bezier(Vector3[] p, float t)
        {
            float u = 1f - t;
            return u * u * u * p[0] + 3f * u * u * t * p[1] + 3f * u * t * t * p[2] + t * t * t * p[3];
        }

        /// <summary>Cada osso aponta para o ponto da curva na distância dele (tabela por comprimento de arco).</summary>
        void Pose()
        {
            tableLength[0] = 0f;
            table[0] = ctrl[0];
            for (int i = 1; i <= TableSize; i++)
            {
                table[i] = Bezier(ctrl, i / (float)TableSize);
                tableLength[i] = tableLength[i - 1] + Vector3.Distance(table[i - 1], table[i]);
            }

            transform.position = ctrl[0];
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = bind[i];
            int seg = 1;
            for (int i = 0; i + 1 < bones.Length; i++)
            {
                Vector3 want = PointAt(along[i + 1], ref seg);
                Vector3 from = bones[i + 1].position - bones[i].position;
                Vector3 to = want - bones[i].position;
                if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) continue;
                bones[i].rotation = Quaternion.FromToRotation(from, to) * bones[i].rotation;
            }
        }

        Vector3 PointAt(float distance, ref int seg)
        {
            float total = tableLength[TableSize];
            if (distance >= total)
            {
                // Curva mais curta que o tentáculo: segue reto na direção da ponta.
                Vector3 dir = table[TableSize] - table[TableSize - 1];
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
                return table[TableSize] + dir * (distance - total);
            }
            while (seg < TableSize && tableLength[seg] < distance) seg++;
            float a = tableLength[seg - 1], b = tableLength[seg];
            return Vector3.Lerp(table[seg - 1], table[seg], b > a ? (distance - a) / (b - a) : 0f);
        }

        /// <summary>Altura da água que o dono informou (efeitos ao subir e afundar).</summary>
        public float WaterLevel => waterLevel;
        public Vector3 BasePoint => basePoint;
    }
}
