using Pirates.Core;
using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Trabalhador do cenário que repete um golpe (13/11, carpinteiros do estaleiro): toca o estado do Animator do começo a cada ciclo, com
    /// uma pausa curta sorteada entre um e outro, e avisa a martelada (<see cref="WorkStrike"/>) no instante do impacto, só com a câmera
    /// perto. Pausa às vezes para descansar (em pé, a pose do próprio estado parado).
    /// </summary>
    public class Worker : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] string workState = "Martelar";
        [SerializeField] string restState = "Parado";
        [Tooltip("Parte do ciclo (0–1) em que o golpe bate.")]
        [Range(0f, 1f)] [SerializeField] float impactPhase = 0.45f;
        [SerializeField] Vector2 pauseBetween = new(0.05f, 0.5f);
        [Tooltip("Golpes seguidos antes de descansar.")]
        [SerializeField] Vector2Int strikesBeforeRest = new(5, 12);
        [SerializeField] Vector2 restSeconds = new(3f, 7f);
        [SerializeField] bool heavy;
        [Tooltip("No que o golpe bate: madeira (carpinteiro), pedra (pedreiro) ou metal (ferreiro na bigorna).")]
        [SerializeField] WorkMaterial material;
        [SerializeField] Transform strikePoint;
        [SerializeField] float hearDistance = 30f;

        int workHash, restHash, left;
        float cycleStart = -1f, cycleLength = 1f, nextCycle;
        bool struck, resting;

        [Tooltip("Duração do golpe (o clipe), em segundos.")]
        [SerializeField] float cycleSeconds = 1f;

        public void Configure(Animator anim, string work, string rest, float impact, float seconds, bool heavyStrike, Transform point, WorkMaterial on = WorkMaterial.Wood)
        {
            material = on;
            cycleSeconds = seconds;
            animator = anim;
            workState = work;
            restState = rest;
            impactPhase = impact;
            heavy = heavyStrike;
            strikePoint = point;
        }

        /// <summary>Ritmo do trabalho: pausa entre golpes, golpes antes de descansar, descanso e até onde se ouve.</summary>
        public void SetPacing(Vector2 pause, Vector2Int strikes, Vector2 rest, float hear)
        {
            pauseBetween = pause;
            strikesBeforeRest = strikes;
            restSeconds = rest;
            hearDistance = hear;
        }

        void Start()
        {
            workHash = Animator.StringToHash(workState);
            restHash = Animator.StringToHash(restState);
            left = Random.Range(strikesBeforeRest.x, strikesBeforeRest.y + 1);
            nextCycle = Time.time + Random.Range(0f, 1.2f);
            if (animator) animator.speed = Random.Range(0.92f, 1.08f);
        }

        void Update()
        {
            if (!animator || !animator.isActiveAndEnabled) return;
            float now = Time.time;
            if (cycleStart >= 0f)
            {
                float t = (now - cycleStart) / cycleLength;
                if (!struck && t >= impactPhase)
                {
                    struck = true;
                    var cam = Camera.main;
                    var p = strikePoint ? strikePoint.position : transform.position;
                    if (cam && (cam.transform.position - p).sqrMagnitude < hearDistance * hearDistance) WorkStrike.Raise(p, heavy, material);
                }
                if (t < 1f) return;
                cycleStart = -1f;
                if (--left <= 0)
                {
                    left = Random.Range(strikesBeforeRest.x, strikesBeforeRest.y + 1);
                    resting = true;
                    animator.CrossFadeInFixedTime(restHash, 0.25f);
                    nextCycle = now + Random.Range(restSeconds.x, restSeconds.y);
                }
                else nextCycle = now + Random.Range(pauseBetween.x, pauseBetween.y);
            }
            if (now < nextCycle) return;
            // Um golpe: o estado do começo (o clipe não precisa de loop).
            animator.CrossFadeInFixedTime(workHash, resting ? 0.25f : 0.08f, 0, 0f);
            resting = false;
            cycleLength = Mathf.Max(0.3f, cycleSeconds / Mathf.Max(0.01f, animator.speed));
            cycleStart = now;
            struck = false;
        }
    }
}
