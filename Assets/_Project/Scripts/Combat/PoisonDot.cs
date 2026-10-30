using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Veneno correndo num alvo (lâmina com veneno de baiacu, 30/10): tira <see cref="perSecond"/> de vida a cada segundo até o tempo acabar
    /// (um golpe novo recomeça o tempo, não soma), com bolhas verdes. O dano vem marcado como bloqueado: sem tranco nem reação de golpe a cada
    /// pingo. Posto no alvo na primeira vez (com a <see cref="Health"/>) e reaproveitado.
    /// </summary>
    public class PoisonDot : MonoBehaviour
    {
        Health health;
        GameObject source;
        float perSecond, until, nextTick;

        public bool Active => enabled && Time.time < until;

        /// <summary>Envenena <paramref name="target"/> (ou recomeça o tempo do veneno que já está nele).</summary>
        public static void Apply(Health target, float damagePerSecond, float seconds, GameObject from)
        {
            if (!target || target.IsDead || damagePerSecond <= 0f || seconds <= 0f) return;
            if (!target.TryGetComponent(out PoisonDot dot)) dot = target.gameObject.AddComponent<PoisonDot>();
            dot.health = target;
            dot.source = from;
            dot.perSecond = Mathf.Max(dot.Active ? dot.perSecond : 0f, damagePerSecond);
            if (!dot.Active) dot.nextTick = Time.time + 1f;
            // Um pingo por segundo, o último junto com o fim (5 s = 5 pingos).
            dot.until = Time.time + seconds + 0.1f;
            dot.enabled = true;
            dot.Puff();
        }

        void Update()
        {
            if (!health || health.IsDead || Time.time >= until)
            {
                enabled = false;
                return;
            }
            if (Time.time < nextTick) return;
            nextTick += 1f;
            var point = transform.position + Vector3.up * 1.2f;
            health.TakeDamage(new DamageInfo { amount = perSecond, point = point, direction = Vector3.up, source = source, blocked = true, poison = true });
            Puff();
        }

        void Puff()
        {
            if (Services.TryGet(out CombatEffects fx)) fx.Poison(transform.position + Vector3.up * 1.3f);
        }
    }
}
