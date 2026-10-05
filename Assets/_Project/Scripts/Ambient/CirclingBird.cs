using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Gaivota: voa em círculo (raio e altura que oscilam devagar) em volta de um ponto e bate as asas de vez em quando
    /// (planando no resto do tempo). As asas giram em Z.
    /// </summary>
    public class CirclingBird : MonoBehaviour
    {
        [SerializeField] Vector3 center;
        [SerializeField] float radius = 20f, height = 14f, speed = 6f;
        [SerializeField] Transform leftWing, rightWing;

        float angle, phase, direction;

        public void Configure(Vector3 circleCenter, float circleRadius, float flightHeight, float metersPerSecond,
            Transform left, Transform right)
        {
            center = circleCenter;
            radius = circleRadius;
            height = flightHeight;
            speed = metersPerSecond;
            leftWing = left;
            rightWing = right;
        }

        void Start()
        {
            angle = Random.value * Mathf.PI * 2f;
            phase = Random.value * 100f;
            direction = Random.value < 0.5f ? 1f : -1f;
        }

        void Update()
        {
            float t = Time.time + phase;
            angle += direction * speed / Mathf.Max(1f, radius) * Time.deltaTime;
            float r = radius * (1f + 0.15f * Mathf.Sin(t * 0.13f));
            float y = height + 2f * Mathf.Sin(t * 0.21f);
            var pos = center + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * direction;
            // Inclina para dentro da curva.
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, 18f * direction));

            // Bate as asas ~2 s a cada 6 s; planando, asas um pouco para cima.
            bool flapping = Mathf.Repeat(t, 6f) < 2f;
            float wing = flapping ? Mathf.Sin(t * 14f) * 35f : 8f;
            if (leftWing) leftWing.localRotation = Quaternion.Euler(0f, 0f, wing);
            if (rightWing) rightWing.localRotation = Quaternion.Euler(0f, 0f, -wing);
        }
    }
}
