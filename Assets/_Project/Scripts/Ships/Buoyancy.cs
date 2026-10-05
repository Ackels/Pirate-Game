using Pirates.Core;
using UnityEngine;

namespace Pirates.Ships
{
    /// <summary>
    /// Flutuação por pontos. Serve para qualquer corpo: navio, boia, barril, destroço.
    /// Com multiplier = 2 o equilíbrio fica com os pontos submersos até metade de floatDepth.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Buoyancy : MonoBehaviour
    {
        [SerializeField] Vector3[] floatPoints = { Vector3.zero };
        [Tooltip("Profundidade (m) em que um ponto gera empuxo total.")]
        [SerializeField] float floatDepth = 1f;
        [SerializeField] float buoyancyMultiplier = 2f;
        [Tooltip("Amortecimento vertical por ponto (evita quicar).")]
        [SerializeField] float verticalDamping = 1.5f;
        [Tooltip("Arrasto horizontal da água. Navios usam 0: o ShipController cuida disso.")]
        [SerializeField] float horizontalDrag = 0.5f;
        [SerializeField] float angularDrag = 0.8f;

        Rigidbody body;
        IOceanSampler ocean;

        /// <summary>0 = fora da água, 1 = todos os pontos no empuxo máximo.</summary>
        public float SubmergedRatio { get; private set; }

        /// <summary>Arrasto horizontal da água. Navios usam 0 enquanto o ShipController os pilota.</summary>
        public float HorizontalDrag
        {
            get => horizontalDrag;
            set => horizontalDrag = value;
        }

        /// <summary>Multiplica o empuxo. 1 = intacto; abaixo de 0,5 o corpo afunda (casco alagado).</summary>
        public float BuoyancyScale { get; set; } = 1f;

        Vector3 floodDirection;
        float floodAmount;
        float pointExtent = 1f;

        /// <summary>
        /// Alagamento desigual: os pontos do lado <paramref name="localDirection"/> perdem empuxo primeiro,
        /// fazendo o corpo afundar de proa, de popa ou tombar. amount 0 = nenhum, 1 = aquele lado sem empuxo.
        /// </summary>
        public void SetFlooding(Vector3 localDirection, float amount)
        {
            floodDirection = localDirection.sqrMagnitude > 0f ? localDirection.normalized : Vector3.zero;
            floodAmount = Mathf.Clamp01(amount);
        }

        public void Configure(Vector3[] points, float depth, float horizontalWaterDrag)
        {
            floatPoints = points;
            floatDepth = depth;
            horizontalDrag = horizontalWaterDrag;
        }

        /// <summary>Troca os pontos de flutuação em jogo (navio que troca de casco).</summary>
        public void SetFloatPoints(Vector3[] points)
        {
            floatPoints = points;
            pointExtent = 1f;
            MeasureExtent();
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            MeasureExtent();
        }

        void MeasureExtent()
        {
            foreach (var p in floatPoints)
                pointExtent = Mathf.Max(pointExtent, new Vector2(p.x, p.z).magnitude);
        }

        float PointScale(Vector3 localPoint)
        {
            if (floodAmount <= 0f) return BuoyancyScale;
            Vector3 flat = new Vector3(localPoint.x, 0f, localPoint.z) / pointExtent;
            float towardFlood = Mathf.Clamp01(0.5f + 0.5f * Vector3.Dot(flat, floodDirection));
            return BuoyancyScale * (1f - floodAmount * towardFlood);
        }

        void FixedUpdate()
        {
            if (ocean == null && !Services.TryGet(out ocean)) return;
            if (floatPoints.Length == 0) return;

            int count = floatPoints.Length;
            float massShare = body.mass / count;
            float weightShare = massShare * Physics.gravity.magnitude;
            float submerged = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 point = transform.TransformPoint(floatPoints[i]);
                float depth = ocean.GetHeight(point) - point.y;
                if (depth <= 0f) continue;

                float factor = Mathf.Clamp01(depth / floatDepth);
                submerged += factor;

                float verticalVelocity = body.GetPointVelocity(point).y;
                float lift = weightShare * buoyancyMultiplier * PointScale(floatPoints[i]) * factor
                           - verticalVelocity * verticalDamping * massShare * factor;
                body.AddForceAtPosition(Vector3.up * lift, point, ForceMode.Force);
            }

            SubmergedRatio = submerged / count;
            if (SubmergedRatio <= 0f) return;

            Vector3 v = body.linearVelocity;
            body.AddForce(new Vector3(-v.x, 0f, -v.z) * (horizontalDrag * SubmergedRatio), ForceMode.Acceleration);
            body.AddTorque(-body.angularVelocity * (angularDrag * SubmergedRatio), ForceMode.Acceleration);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (var p in floatPoints)
                Gizmos.DrawWireSphere(transform.TransformPoint(p), 0.15f);
        }
    }
}
