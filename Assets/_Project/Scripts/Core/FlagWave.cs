using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Bandeira que tremula com o vento (clima, 17/11). Vale para as dos navios e as do cenário (porto, forte, acampamentos): o objeto
    /// gira em volta do mastro (o eixo Y local, na origem) até o pano apontar para onde o vento aparente sopra (o vento do
    /// <see cref="IWind"/> menos a velocidade do navio, se houver um Rigidbody acima), e o pano ondula numa cópia da malha: a onda corre do
    /// mastro para a ponta, mais rápida e mais curta com vento forte; com pouco vento o pano cai junto do mastro. Perto da câmera e visível
    /// só (o resto fica parado na última pose).
    /// </summary>
    [DisallowMultipleComponent]
    public class FlagWave : MonoBehaviour
    {
        [Tooltip("Direção do pano a partir do mastro, no espaço do objeto (bandeira do navio: -Z; do cenário: -X).")]
        [SerializeField] Vector3 clothAxis = Vector3.back;
        [Tooltip("Raio do mastro: o que estiver mais perto do eixo não mexe.")]
        [SerializeField] float poleRadius = 0.2f;
        [Tooltip("Abaixo desta altura (espaço do objeto) nada mexe (o mastro).")]
        [SerializeField] float clothBottom = float.NegativeInfinity;
        [Tooltip("Virar para o vento (falso = só ondula; para bandeira presa numa parede).")]
        [SerializeField] bool turnWithWind = true;
        [Tooltip("Distância máxima da câmera para ondular.")]
        [SerializeField] float maxDistance = 140f;

        static readonly List<FlagWave> active = new();
        public static IReadOnlyList<FlagWave> Active => active;

        MeshFilter filter;
        Renderer rend;
        Mesh mesh;
        Vector3[] rest, verts;
        float[] weight;
        float length;
        Vector3 side;
        Rigidbody body;
        Quaternion baseRotation;
        float yaw, phase, gust;
        bool ready;

        /// <summary>Gerador: como esta bandeira é montada.</summary>
        public void Configure(Vector3 axis, float pole, float bottom, bool turn)
        {
            clothAxis = axis;
            poleRadius = pole;
            clothBottom = bottom;
            turnWithWind = turn;
        }

        void Awake()
        {
            filter = GetComponent<MeshFilter>();
            rend = GetComponent<Renderer>();
            baseRotation = transform.localRotation;
            body = GetComponentInParent<Rigidbody>();
            phase = Random.value * 50f;
            Prepare();
        }

        void Prepare()
        {
            if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return;
            mesh = Instantiate(filter.sharedMesh);
            mesh.name = filter.sharedMesh.name + "_Vento";
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            rest = mesh.vertices;
            verts = (Vector3[])rest.Clone();
            weight = new float[rest.Length];
            var axis = clothAxis.normalized;
            side = Vector3.Cross(Vector3.up, axis).normalized;
            length = 0f;
            foreach (var v in rest) length = Mathf.Max(length, Vector3.Dot(v, axis));
            float span = Mathf.Max(0.01f, length - poleRadius);
            for (int i = 0; i < rest.Length; i++)
            {
                float d = Vector3.Dot(rest[i], axis);
                weight[i] = rest[i].y < clothBottom ? 0f : Mathf.Clamp01((d - poleRadius) / span);
            }
            // Caixa folgada uma vez (a onda e a queda ficam dentro dela): sem RecalculateBounds a cada quadro.
            var b = mesh.bounds;
            b.Expand(new Vector3(length, length, length));
            mesh.bounds = b;
            ready = true;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        void LateUpdate()
        {
            if (!ready) return;
            var cam = Camera.main;
            if (!cam || (transform.position - cam.transform.position).sqrMagnitude > maxDistance * maxDistance || (rend && !rend.isVisible)) return;

            // Vento aparente no mundo (m/s): o vento verdadeiro menos o movimento do navio.
            Vector3 wind = Vector3.zero;
            float storm = 0f;
            if (Services.TryGet(out IWind w)) wind = w.Direction * Mathf.Lerp(1.5f, 12f, w.Strength);
            if (Services.TryGet(out IWeather weather)) storm = weather.Storm;
            if (body) wind -= body.linearVelocity;
            wind.y = 0f;
            float speed = wind.magnitude;
            float f = Mathf.Clamp01(speed / 12f);
            float dt = Time.deltaTime;

            if (turnWithWind && speed > 0.3f && transform.parent)
            {
                // O pano (clothAxis) aponta para onde o vento vai, no plano do mastro.
                var parent = transform.parent;
                var restAxis = parent.TransformDirection(baseRotation * clothAxis);
                var up = parent.TransformDirection(baseRotation * Vector3.up);
                float target = Vector3.SignedAngle(Vector3.ProjectOnPlane(restAxis, up), Vector3.ProjectOnPlane(wind, up), up);
                gust = Mathf.Lerp(gust, (Mathf.PerlinNoise(phase, Time.time * 0.6f) - 0.5f) * Mathf.Lerp(30f, 10f, f), dt * 2f);
                yaw = Mathf.LerpAngle(yaw, target + gust, 1f - Mathf.Exp(-dt * Mathf.Lerp(1.2f, 4f, f)));
                transform.localRotation = baseRotation * Quaternion.AngleAxis(yaw, Vector3.up);
            }

            // Onda: corre do mastro para a ponta; vento forte = mais rápida, mais curta e mais baixa (o pano fica esticado).
            phase += dt * Mathf.Lerp(3f, 13f, f) * (1f + storm * 0.6f);
            float waves = Mathf.Lerp(1.2f, 2.4f, f);
            float k = waves * Mathf.PI * 2f / Mathf.Max(0.2f, length);
            float amp = length * Mathf.Lerp(0.09f, 0.15f, f) * (1f + storm * 0.5f);
            float droop = (1f - f) * (1f - f);
            var axis = clothAxis.normalized;
            for (int i = 0; i < rest.Length; i++)
            {
                float wgt = weight[i];
                if (wgt <= 0f) { verts[i] = rest[i]; continue; }
                var v = rest[i];
                float d = Vector3.Dot(v, axis);
                float wave = Mathf.Sin(phase - d * k) * amp * wgt;
                // Sem vento o pano cai: a ponta desce e chega perto do mastro.
                float fall = droop * wgt * d * 0.75f;
                v += side * wave - axis * fall * 0.6f + Vector3.down * fall;
                verts[i] = v;
            }
            mesh.SetVertices(verts);
            mesh.RecalculateNormals();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();
    }
}
