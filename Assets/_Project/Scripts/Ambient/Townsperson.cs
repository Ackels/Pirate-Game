using UnityEngine;
using UnityEngine.AI;

namespace Pirates.Ambient
{
    /// <summary>
    /// Morador da vila (só visual, não é atacado): com <see cref="NavMeshAgent"/> e paradas, anda de uma parada à outra
    /// pelo NavMesh do porto e fica um tempo em cada uma; sem agente, fica no lugar. Parado, de vez em quando gesticula
    /// (acena, "sim", "não": estados do Animator com esses nomes) e vira um pouco para os lados. O Animator recebe a
    /// velocidade em "Speed" (parado ↔ andar).
    /// </summary>
    public class Townsperson : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly string[] Gestures = { "Wave", "Yes", "No" };

        [SerializeField] Animator animator;
        [Tooltip("Paradas no mundo (vazio: fica no lugar).")]
        [SerializeField] Vector3[] stops = System.Array.Empty<Vector3>();
        [SerializeField] Vector2 waitSeconds = new(3f, 9f);
        [SerializeField, Range(0f, 1f)] float gestureChance = 0.4f;
        [SerializeField] float lookAroundDegrees = 25f;
        [Tooltip("Mais longe que isto (m) da câmera, para na pose em que está: sem animação nem caminhada (economia de CPU).")]
        [SerializeField] float sleepDistance = 150f;

        const float WakeMargin = 20f, SleepCheckInterval = 0.5f;

        NavMeshAgent agent;
        int next;
        bool walking, walker, asleep;
        float waitUntil, nextGesture, nextSleepCheck;
        Quaternion home, target;

        public void Configure(Animator model, Vector3[] walkStops)
        {
            animator = model;
            stops = walkStops ?? System.Array.Empty<Vector3>();
        }

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent && (stops.Length == 0 || !agent.isOnNavMesh)) agent.enabled = false;
            walker = agent && agent.enabled;
            // Moradores conferem a distância em momentos diferentes.
            nextSleepCheck = Time.unscaledTime + Random.value * SleepCheckInterval;
            if (animator)
            {
                animator.speed = Random.Range(0.9f, 1.1f);
                animator.Play(0, 0, Random.value);
            }
            next = stops.Length > 0 ? Random.Range(0, stops.Length) : 0;
            home = target = transform.rotation;
            waitUntil = Time.time + Random.Range(0f, waitSeconds.y);
            nextGesture = Time.time + Random.Range(2f, 8f);
        }

        void Update()
        {
            if (Time.unscaledTime >= nextSleepCheck) CheckDistance();
            if (asleep) return;

            bool canWalk = agent && agent.enabled;
            if (animator) animator.SetFloat(SpeedId, canWalk ? agent.velocity.magnitude : 0f);

            if (walking)
            {
                if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f) return;
                walking = false;
                home = target = transform.rotation;
                waitUntil = Time.time + Random.Range(waitSeconds.x, waitSeconds.y);
                nextGesture = Time.time + Random.Range(0.5f, 2f);
                return;
            }

            if (canWalk && Time.time >= waitUntil)
            {
                agent.SetDestination(stops[next]);
                next = (next + 1 + Random.Range(0, Mathf.Max(1, stops.Length - 1))) % stops.Length;
                walking = true;
                return;
            }

            // Parado: gesticula às vezes e olha em volta.
            if (Time.time >= nextGesture)
            {
                if (animator && Random.value < gestureChance)
                    animator.CrossFadeInFixedTime(Gestures[Random.Range(0, Gestures.Length)], 0.2f);
                else
                    target = home * Quaternion.Euler(0f, Random.Range(-lookAroundDegrees, lookAroundDegrees), 0f);
                nextGesture = Time.time + Random.Range(4f, 10f);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 40f * Time.deltaTime);
        }

        void CheckDistance()
        {
            nextSleepCheck = Time.unscaledTime + SleepCheckInterval;
            var cam = Camera.main;
            if (!cam) return;
            Vector3 d = cam.transform.position - transform.position;
            d.y = 0f;
            float limit = asleep ? sleepDistance - WakeMargin : sleepDistance;
            bool far = d.sqrMagnitude > limit * limit;
            if (far != asleep) SetAsleep(far);
        }

        void SetAsleep(bool value)
        {
            asleep = value;
            if (animator)
            {
                if (value)
                {
                    // Grava uma pose antes de parar: fora da tela o Animator não mexe os ossos e o morador ficaria em pose T.
                    var culling = animator.cullingMode;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Update(0f);
                    animator.cullingMode = culling;
                    animator.keepAnimatorStateOnDisable = true;
                }
                animator.enabled = !value;
            }
            if (!walker) return;
            // O agente desligado perde o caminho: ao acordar, espera um pouco e escolhe a próxima parada.
            agent.enabled = !value;
            if (!value)
            {
                walking = false;
                waitUntil = Time.time + Random.Range(0f, waitSeconds.x);
            }
        }
    }
}
