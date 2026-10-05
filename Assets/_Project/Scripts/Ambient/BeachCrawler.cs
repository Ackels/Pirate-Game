using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Caranguejo: anda de lado entre dois pontos da praia, para um pouco em cada ponta e volta. Segue o chão com um raio
    /// para baixo (o gerador põe os pontos na areia).
    /// </summary>
    public class BeachCrawler : MonoBehaviour
    {
        [SerializeField] Vector3 pointA, pointB;
        [SerializeField] float speed = 0.8f;

        float pauseUntil;
        bool towardB = true;

        public void Configure(Vector3 a, Vector3 b, float metersPerSecond)
        {
            pointA = a;
            pointB = b;
            speed = metersPerSecond;
        }

        void Start() => pauseUntil = Time.time + Random.value * 3f;

        void Update()
        {
            if (Time.time < pauseUntil) return;
            var target = towardB ? pointB : pointA;
            var flat = new Vector3(target.x - transform.position.x, 0f, target.z - transform.position.z);
            if (flat.magnitude < 0.1f)
            {
                towardB = !towardB;
                pauseUntil = Time.time + Random.Range(1f, 4f);
                return;
            }
            var step = flat.normalized;
            var pos = transform.position + step * Mathf.Min(flat.magnitude, speed * Time.deltaTime);
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            // Caranguejo anda de lado: o corpo fica atravessado na direção do passo.
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.Cross(Vector3.up, step)));
        }
    }
}
