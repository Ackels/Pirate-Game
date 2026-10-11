using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Bomba de pólvora lançada (pacote A de 09/10): segurada na mão durante o preparo, voa num arco até o ponto mirado, para onde bater
    /// (chão, parede, telhado) e explode quando o pavio acaba (contado do lançamento), com a <see cref="Explosion"/> comum (menor que a do
    /// barril). Caiu na água: o pavio apaga (só o respingo). Cópias reaproveitadas (quem lança cria algumas no começo e liga/desliga).
    /// </summary>
    public class ThrownBomb : MonoBehaviour
    {
        [Tooltip("Explosão da bomba: menor que a do barril (raio 3,5 m, 55 → 10, derruba até 60% do raio).")]
        [SerializeField] Explosion.Spec explosion = new()
        {
            radius = 3.5f, centerDamage = 55f, edgeDamage = 10f, push = 7f, lift = 3f, knockdownWithin = 0.6f,
        };
        [Tooltip("Segundos de pavio contados do lançamento (voa ~0,6–1 s e ainda fica um instante no chão: dá para fugir).")]
        [SerializeField] float fuseSeconds = 1.6f;
        [Tooltip("Ponta acesa do pavio (pisca e cresce perto de explodir).")]
        [SerializeField] Transform fuseTip;
        [Tooltip("Raio (m) da bomba para parar no que bater.")]
        [SerializeField] float ballRadius = 0.14f;

        static readonly RaycastHit[] hits = new RaycastHit[8];
        Vector3 velocity, tipScale = Vector3.one, worldScale = Vector3.one;
        float launchedAt;
        bool flying, lit;
        GameObject thrower;
        Transform home;
        IOceanSampler ocean;

        public bool IsFree => !gameObject.activeSelf;
        public float FuseSeconds => fuseSeconds;
        public float Radius => explosion.radius;
        public float BallRadius => ballRadius;

        public void Configure(Transform tip) => fuseTip = tip;

        void Awake()
        {
            home = transform.parent;
            worldScale = transform.lossyScale;
            if (fuseTip) tipScale = fuseTip.localScale;
        }

        /// <summary>Na mão, apagada, enquanto o braço prepara o arremesso.</summary>
        public void Hold(Transform hand, Vector3 localPosition)
        {
            gameObject.SetActive(true);
            transform.SetParent(hand, false);
            // Os ossos do Quaternius têm escala 100 (e o modelo, 0,01): sem compensar, a bomba na mão sumia de tão pequena/grande.
            float boneScale = Mathf.Max(0.0001f, hand.lossyScale.x);
            transform.SetLocalPositionAndRotation(localPosition / boneScale, Quaternion.identity);
            transform.localScale = new Vector3(worldScale.x / boneScale, worldScale.y / boneScale, worldScale.z / boneScale);
            flying = lit = false;
        }

        /// <summary>Desistiu de lançar (derrubado no preparo): volta para a reserva.</summary>
        public void Cancel()
        {
            flying = lit = false;
            transform.SetParent(home, false);
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        /// <summary>Solta da mão e voa até <paramref name="target"/> em <paramref name="flightSeconds"/> (arco pela gravidade).</summary>
        public void Launch(Vector3 target, float flightSeconds, GameObject by)
        {
            thrower = by;
            Vector3 from = transform.position;
            transform.SetParent(home, true);
            float t = Mathf.Max(0.2f, flightSeconds);
            velocity = (target - from) / t - 0.5f * t * Physics.gravity;
            launchedAt = Time.time;
            flying = lit = true;
        }

        void Update()
        {
            if (!lit) return;
            float dt = Time.deltaTime;
            if (flying) Fly(dt);
            float left = fuseSeconds - (Time.time - launchedAt);
            if (fuseTip)
            {
                // Pisca mais rápido e cresce no fim do pavio.
                float urgency = 1f - Mathf.Clamp01(left / fuseSeconds);
                float blink = 0.75f + 0.35f * Mathf.Sin(Time.time * Mathf.Lerp(18f, 45f, urgency));
                fuseTip.localScale = tipScale * (blink * (1f + urgency * 0.8f));
            }
            if (left <= 0f) Explode();
        }

        void Fly(float dt)
        {
            Vector3 from = transform.position;
            velocity += Physics.gravity * dt;
            Vector3 step = velocity * dt;
            float length = step.magnitude;
            if (length > 0.0001f)
            {
                int count = Physics.SphereCastNonAlloc(from, ballRadius, step / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                RaycastHit hit = default;
                for (int i = 0; i < count; i++)
                {
                    if (thrower && hits[i].transform.IsChildOf(thrower.transform)) continue;
                    if (hits[i].distance < nearest && hits[i].distance > 0f)
                    {
                        nearest = hits[i].distance;
                        hit = hits[i];
                    }
                }
                if (nearest < float.MaxValue)
                {
                    // Bateu: fica onde parou (sem quicar), em cima do que acertou.
                    transform.position = from + step / length * nearest + hit.normal * 0.02f;
                    flying = false;
                    return;
                }
            }
            transform.position = from + step;
            transform.Rotate(new Vector3(540f, 0f, 220f) * dt, Space.Self);
            if (ocean == null) Services.TryGet(out ocean);
            if (ocean != null && transform.position.y < ocean.GetHeight(transform.position))
            {
                // Na água o pavio apaga.
                if (Services.TryGet(out CombatEffects effects)) effects.Splash(transform.position);
                Cancel();
            }
        }

        /// <summary>Explode agora (fim do pavio, testes).</summary>
        public void Explode()
        {
            Vector3 at = transform.position + Vector3.up * 0.2f;
            var by = thrower;
            Cancel();
            Explosion.Detonate(at, explosion, by);
        }
    }
}
