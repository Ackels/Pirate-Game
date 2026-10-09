using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Barril de pólvora (pacote A de 09/10: "mais coisas a pé"): tiro, bala de canhão ou outra explosão acendem
    /// (<see cref="DamageInfo.ignites"/>; golpe de cutelo não) e, depois de um pavio curto, ele explode: dano em área que cai com a
    /// distância (jogador, piratas, caixotes, outros barris: explosão em cadeia, cada um com o seu atraso), empurrão nos
    /// personagens (<see cref="IPushable"/>) e o aviso do <see cref="CombatEffects"/> para o efeito e o som. Fica no lugar com o
    /// visual escondido até <see cref="Restore"/> (acampamento repovoado, sandbox).
    /// </summary>
    public class PowderKeg : MonoBehaviour, IDamageable
    {
        static readonly List<PowderKeg> active = new();
        public static IReadOnlyList<PowderKeg> Active => active;

        [SerializeField] GameObject visual;
        [Tooltip("Raio da explosão (m).")]
        [SerializeField] float radius = 5f;
        [Tooltip("Dano no centro e na borda do raio (cai em linha reta). O capitão tem 100 de vida; os piratas, 60.")]
        [SerializeField] float centerDamage = 80f, edgeDamage = 12f;
        [Tooltip("Empurrão (m/s) no centro, para fora, e para cima.")]
        [SerializeField] float push = 9f, lift = 3.5f;
        [Tooltip("Fração do raio até onde a explosão derruba quem está a pé (arremessado de costas, levanta em ~3 s).")]
        [SerializeField, Range(0f, 1f)] float knockdownWithin = 0.6f;
        [Tooltip("Pavio (s) depois de acender: um tiro, e o atraso entre barris de uma cadeia.")]
        [SerializeField] Vector2 fuseSeconds = new(0.1f, 0.25f);

        static readonly Collider[] hits = new Collider[64];
        static readonly RaycastHit[] cover = new RaycastHit[16];
        static readonly HashSet<IDamageable> damaged = new();
        static readonly HashSet<IPushable> pushed = new();

        Collider[] colliders;
        float explodeAt = -1f;

        public bool IsSpent { get; private set; }
        public float Radius => radius;

        public void Configure(GameObject whole) => visual = whole;

        void Awake() => colliders = GetComponents<Collider>();
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public void TakeDamage(in DamageInfo info)
        {
            if (!info.ignites || IsSpent || explodeAt >= 0f) return;
            explodeAt = Time.time + Random.Range(fuseSeconds.x, fuseSeconds.y);
        }

        void Update()
        {
            if (explodeAt < 0f || Time.time < explodeAt) return;
            explodeAt = -1f;
            Explode();
        }

        /// <summary>Explode agora (sandbox, testes).</summary>
        public void Explode()
        {
            if (IsSpent) return;
            IsSpent = true;
            if (visual) visual.SetActive(false);
            foreach (var c in colliders) c.enabled = false;

            Vector3 center = transform.position + Vector3.up * 0.5f;
            if (Services.TryGet(out CombatEffects effects)) effects.Explosion(center, radius);

            damaged.Clear();
            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = hits[i];
                var target = col.GetComponentInParent<IDamageable>();
                if ((Object)target == this || (target != null && damaged.Contains(target))) target = null;
                var pushable = col.GetComponentInParent<IPushable>();
                if (pushable != null && pushed.Contains(pushable)) pushable = null;
                // Só gasta a conta (e o raio de cobertura) com quem leva dano ou empurrão: chão, árvores e pedras ficam de fora.
                if (target == null && pushable == null) continue;
                // Pela caixa (ClosestPoint não vale em MeshCollider côncavo, como o chão da ilha).
                Vector3 closest = col.bounds.ClosestPoint(center);
                Vector3 offset = closest - center;
                float distance = offset.magnitude;
                if (distance > radius || Sheltered(col, center)) continue;
                float t = distance / radius;
                Vector3 away = offset.sqrMagnitude > 0.0001f ? offset / distance : Vector3.up;
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

                if (target != null && damaged.Add(target))
                    target.TakeDamage(new DamageInfo
                    {
                        amount = Mathf.Lerp(centerDamage, edgeDamage, t),
                        point = closest,
                        direction = away,
                        source = gameObject,
                        ignites = true,
                        // Perto (até ~3 m): arremessado de costas.
                        knockdown = t < knockdownWithin ? Knockdown.Blast : Knockdown.None,
                    });
                if (pushable != null && pushed.Add(pushable))
                    pushable.Push(away * (push * (1f - 0.6f * t)) + Vector3.up * (lift * (1f - t)));
            }
            damaged.Clear();
            pushed.Clear();
        }

        /// <summary>
        /// Atrás de parede (paliçada, casa, muralha, morro): a explosão não passa. Raio do centro até o meio do colisor do alvo (o ponto mais
        /// perto da caixa rente ao chão raspava no chão em ladeira). Personagens, caixotes e outros barris não protegem; muralha do forte sim.
        /// </summary>
        static bool Sheltered(Collider target, Vector3 center)
        {
            Vector3 to = target.bounds.center - center;
            float length = to.magnitude;
            if (length < 0.3f) return false;
            int count = Physics.RaycastNonAlloc(center, to / length, cover, length - 0.1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = cover[i].collider;
                if (col == target || col is CharacterController) continue;
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable is EnemyFort) return true;
            }
            return false;
        }

        /// <summary>Inteiro de novo, no lugar.</summary>
        public void Restore()
        {
            IsSpent = false;
            explodeAt = -1f;
            if (visual) visual.SetActive(true);
            foreach (var c in colliders) c.enabled = true;
        }
    }
}
