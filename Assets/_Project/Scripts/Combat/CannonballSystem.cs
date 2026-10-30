using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Simula todas as balas de canhão num único loop (sem Rigidbody por bala):
    /// integra a balística, detecta colisão por raycast e respingo na água.
    /// Os visuais vêm de um pool.
    /// </summary>
    public class CannonballSystem : MonoBehaviour
    {
        struct Ball
        {
            public Vector3 position, velocity;
            public float age, lifetime, damage, mass, sailDamage, crewDamage, burnPerSecond, burnSeconds;
            public Rigidbody shooter;
            /// <summary>Respingo ao cair na água (na metralha, só um projétil por canhão: senão são 16 de uma vez). Acerto sempre tem
            /// efeito; balinhas sem respingo (metralha) respeitam um intervalo curto entre um e outro.</summary>
            public bool effects;
            public Transform visual;
            /// <summary>Bala de corrente: o visual gira (spin em graus).</summary>
            public bool chain;
            public float spin;
        }

        [SerializeField] Material ballMaterial;
        [SerializeField] Material trailMaterial;
        [SerializeField] float visualRadius = 0.25f;

        const float TrailWidth = 0.35f;
        /// <summary>Segundos mínimos entre efeitos de acerto das balinhas de metralha (vários acertos no mesmo instante viram um).</summary>
        const float PelletImpactInterval = 0.06f;
        float nextPelletImpact;

        readonly List<Ball> balls = new();
        readonly Stack<Transform> pool = new();
        readonly Stack<Transform> chainPool = new();
        /// <summary>Giro da bala de corrente (graus por segundo).</summary>
        const float ChainSpin = 900f;
        readonly RaycastHit[] hits = new RaycastHit[8];
        IOceanSampler ocean;
        CombatEffects effects;

        public int ActiveCount => balls.Count;

        public void Configure(Material ball, Material trail)
        {
            ballMaterial = ball;
            trailMaterial = trail;
        }

        void OnEnable() => Services.Register(this);
        void OnDisable() => Services.Unregister(this);

        /// <param name="sailDamage">Munição especial: velas rasgadas por acerto (DamageInfo.sailDamage).</param>
        /// <param name="crewDamage">Munição especial: tripulação abatida por acerto (DamageInfo.crewDamage).</param>
        /// <param name="scale">Tamanho na tela (metralha: projéteis pequenos).</param>
        /// <param name="withEffects">Respingo e lascas (efeito e som) quando cai ou acerta.</param>
        public void Fire(Vector3 position, Vector3 velocity, float damage, float mass, float lifetime, Rigidbody shooter,
            float sailDamage = 0f, float crewDamage = 0f, float scale = 1f, bool withEffects = true, bool chain = false,
            float burnPerSecond = 0f, float burnSeconds = 0f)
        {
            Transform visual;
            if (chain)
            {
                visual = chainPool.Count > 0 ? chainPool.Pop() : CreateChainVisual();
                visual.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
                visual.gameObject.SetActive(true);
                foreach (var t in visual.GetComponentsInChildren<TrailRenderer>()) t.Clear();
            }
            else
            {
                visual = pool.Count > 0 ? pool.Pop() : CreateVisual();
                visual.position = position;
                visual.localScale = Vector3.one * (visualRadius * 2f * scale);
                visual.gameObject.SetActive(true);
                var trail = visual.GetComponent<TrailRenderer>();
                trail.Clear();
                trail.widthMultiplier = TrailWidth * scale;
            }

            balls.Add(new Ball
            {
                position = position, velocity = velocity, damage = damage, mass = mass,
                lifetime = lifetime, shooter = shooter, visual = visual, sailDamage = sailDamage, crewDamage = crewDamage,
                effects = withEffects, chain = chain, spin = Random.Range(0f, 360f), burnPerSecond = burnPerSecond, burnSeconds = burnSeconds,
            });
        }

        void Update()
        {
            if (ocean == null) Services.TryGet(out ocean);
            if (effects == null) Services.TryGet(out effects);

            float dt = Time.deltaTime;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball b = balls[i];
                Vector3 previous = b.position;
                Ballistics.Step(ref b.position, ref b.velocity, dt);
                b.age += dt;

                if (TryGetHit(previous, b.position, b.shooter, out RaycastHit hit))
                {
                    ApplyHit(b, hit);
                    Despawn(i);
                    continue;
                }

                if (ocean != null)
                {
                    float water = ocean.GetHeight(b.position);
                    if (b.position.y <= water)
                    {
                        if (b.effects) effects?.Splash(new Vector3(b.position.x, water, b.position.z));
                        Despawn(i);
                        continue;
                    }
                }

                if (b.age >= b.lifetime)
                {
                    Despawn(i);
                    continue;
                }

                b.visual.position = b.position;
                if (b.chain)
                {
                    // Gira no plano do voo, como uma boleadeira: as duas meias-balas varrem de um lado para o outro.
                    b.spin += ChainSpin * dt;
                    if (b.velocity.sqrMagnitude > 0.01f) b.visual.rotation = Quaternion.LookRotation(b.velocity) * Quaternion.Euler(0f, b.spin, 0f);
                }
                balls[i] = b;
            }
        }

        bool TryGetHit(Vector3 from, Vector3 to, Rigidbody shooter, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return false;

            int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (shooter != null && hits[i].rigidbody == shooter) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    nearest = hits[i];
                }
            }
            return best < float.MaxValue;
        }

        void ApplyHit(in Ball b, in RaycastHit hit)
        {
            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(b.velocity * b.mass, hit.point, ForceMode.Impulse);

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(new DamageInfo
            {
                amount = b.damage,
                point = hit.point,
                direction = b.velocity.normalized,
                source = b.shooter ? b.shooter.gameObject : null,
                sailDamage = b.sailDamage,
                crewDamage = b.crewDamage,
                ignites = true,
                burnPerSecond = b.burnPerSecond,
                burnSeconds = b.burnSeconds,
            });

            if (b.effects || Time.time >= nextPelletImpact)
            {
                if (!b.effects) nextPelletImpact = Time.time + PelletImpactInterval;
                effects?.Impact(hit.point, hit.normal);
            }
        }

        void Despawn(int index)
        {
            Transform visual = balls[index].visual;
            visual.gameObject.SetActive(false);
            (balls[index].chain ? chainPool : pool).Push(visual);
            int last = balls.Count - 1;
            balls[index] = balls[last];
            balls.RemoveAt(last);
        }

        Transform CreateVisual()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cannonball";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (visualRadius * 2f);
            if (ballMaterial) go.GetComponent<Renderer>().sharedMaterial = ballMaterial;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.widthMultiplier = TrailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.4f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.colorGradient = Fade(new Color(0.85f, 0.85f, 0.85f), 0.5f);
            if (trailMaterial) trail.sharedMaterial = trailMaterial;
            return go.transform;
        }

        /// <summary>Bala de corrente: duas meias-balas de ferro nas pontas de uma corrente curta (cilindro fino), cada uma com rastro.</summary>
        Transform CreateChainVisual()
        {
            var root = new GameObject("ChainShot").transform;
            root.SetParent(transform, false);
            const float half = 0.6f;
            for (int s = -1; s <= 1; s += 2)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "MeiaBala";
                Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(root, false);
                ball.transform.localPosition = new Vector3(s * half, 0f, 0f);
                ball.transform.localScale = Vector3.one * (visualRadius * 1.6f);
                if (ballMaterial) ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;
                var trail = ball.AddComponent<TrailRenderer>();
                trail.time = 0.18f;
                trail.widthMultiplier = TrailWidth * 0.5f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.minVertexDistance = 0.2f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.colorGradient = Fade(new Color(0.8f, 0.8f, 0.82f), 0.45f);
                if (trailMaterial) trail.sharedMaterial = trailMaterial;
            }
            var link = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            link.name = "Corrente";
            Destroy(link.GetComponent<Collider>());
            link.transform.SetParent(root, false);
            link.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            link.transform.localScale = new Vector3(0.07f, half, 0.07f);
            if (ballMaterial) link.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        static Gradient Fade(Color color, float alpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
