using System;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Explosão de pólvora (barril, bomba): dano em área que cai em linha reta do centro até a borda, derruba quem está a pé perto do
    /// centro, empurra os personagens (<see cref="IPushable"/>), acende o que é de pólvora (<see cref="DamageInfo.ignites"/>) e avisa o
    /// <see cref="CombatEffects"/> (efeito e som). Parede, casa, muralha e morro protegem; personagem, caixote e barril não.
    /// Um dano por <see cref="IDamageable"/> e um empurrão por personagem; nada aloca (buffers estáticos).
    /// </summary>
    public static class Explosion
    {
        [Serializable]
        public struct Spec
        {
            [Tooltip("Raio (m).")] public float radius;
            [Tooltip("Dano no centro e na borda do raio. O capitão tem 100 de vida; os piratas, 60.")] public float centerDamage, edgeDamage;
            [Tooltip("Empurrão (m/s) no centro, para fora, e para cima.")] public float push, lift;
            [Tooltip("Fração do raio até onde derruba quem está a pé (arremessado de costas).")]
            [Range(0f, 1f)] public float knockdownWithin;
        }

        static readonly Collider[] hits = new Collider[64];
        static readonly RaycastHit[] cover = new RaycastHit[16];
        static readonly HashSet<IDamageable> damaged = new();
        static readonly HashSet<IPushable> pushed = new();

        /// <summary>Explode em <paramref name="center"/>. <paramref name="self"/> não leva o próprio dano (o barril que explodiu).</summary>
        public static void Detonate(Vector3 center, in Spec spec, GameObject source, IDamageable self = null)
        {
            if (Services.TryGet(out CombatEffects effects)) effects.Explosion(center, spec.radius);
            damaged.Clear();
            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, spec.radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = hits[i];
                var target = col.GetComponentInParent<IDamageable>();
                if (target == self || (target != null && damaged.Contains(target))) target = null;
                var pushable = col.GetComponentInParent<IPushable>();
                if (pushable != null && pushed.Contains(pushable)) pushable = null;
                // Só gasta a conta (e o raio de cobertura) com quem leva dano ou empurrão: chão, árvores e pedras ficam de fora.
                if (target == null && pushable == null) continue;
                // Pela caixa (ClosestPoint não vale em MeshCollider côncavo, como o chão da ilha).
                Vector3 closest = col.bounds.ClosestPoint(center);
                Vector3 offset = closest - center;
                float distance = offset.magnitude;
                if (distance > spec.radius || Sheltered(col, center)) continue;
                float t = distance / spec.radius;
                Vector3 away = offset.sqrMagnitude > 0.0001f ? offset / distance : Vector3.up;
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

                if (target != null && damaged.Add(target))
                    target.TakeDamage(new DamageInfo
                    {
                        amount = Mathf.Lerp(spec.centerDamage, spec.edgeDamage, t),
                        point = closest,
                        direction = away,
                        source = source,
                        ignites = true,
                        knockdown = t < spec.knockdownWithin ? Knockdown.Blast : Knockdown.None,
                    });
                if (pushable != null && pushed.Add(pushable))
                    pushable.Push(away * (spec.push * (1f - 0.6f * t)) + Vector3.up * (spec.lift * (1f - t)));
            }
            damaged.Clear();
            pushed.Clear();
        }

        /// <summary>
        /// Atrás de parede: raio do centro até o meio do colisor do alvo (o ponto mais perto da caixa, rente ao chão, raspava no chão em
        /// ladeira). Bloqueia o que não leva dano (paliçada, casa, morro) e a muralha do forte.
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
    }
}
