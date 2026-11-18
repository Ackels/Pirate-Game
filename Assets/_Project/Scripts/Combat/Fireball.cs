using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Bola de fogo do mago esqueleto (18/11, pedido do Jhonata: "em vez dele usar arma... ele soltar umas magias de fogo"). Sai do alto do
    /// cajado e voa reto, devagar o bastante para sair da frente ou rolar; estoura no primeiro que não é do time do mago (chão, parede, o
    /// capitão, um marujo, um barril) ou no fim do alcance: estouro pequeno de fogo (Explosion, acende barris), que a guarda não segura e que
    /// não fere os outros mortos. O prefab (feito pelo gerador) traz a esfera em brasa, a luz e o rastro de chamas.
    /// </summary>
    public class Fireball : MonoBehaviour, IProjectile
    {
        [Tooltip("Raio da bola para o que ela pega no caminho (m).")]
        [SerializeField] float radius = 0.28f;
        [Tooltip("Estouro: o dano do centro vem da arma; aqui raio, dano na borda e empurrão.")]
        [SerializeField] Explosion.Spec blast = new() { radius = 1.9f, centerDamage = 20f, edgeDamage = 6f, push = 3.5f, lift = 1f, knockdownWithin = 0f };
        [Tooltip("Rastro e luz que ficam um instante depois do estouro (somem junto).")]
        [SerializeField] float lingerSeconds = 0.6f;

        static readonly RaycastHit[] hits = new RaycastHit[16];

        GameObject source;
        Transform sourceRoot;
        ITeamMember team;
        Vector3 velocity;
        float left;
        bool done;

        public void Launch(GameObject from, Vector3 velocity, float damage, float range)
        {
            source = from;
            sourceRoot = from ? from.transform.root : null;
            team = from ? from.GetComponentInParent<ITeamMember>() : null;
            this.velocity = velocity;
            blast.centerDamage = damage;
            left = Mathf.Max(0.5f, range);
        }

        void Update()
        {
            if (done) return;
            float step = velocity.magnitude * Time.deltaTime;
            if (step <= 0f) return;
            Vector3 dir = velocity.normalized;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, dir, hits, step, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (sourceRoot && hit.transform.IsChildOf(sourceRoot)) continue;
                if (Friendly(hit.collider)) continue;
                // Começou encostado (distância 0): estoura onde a bola está.
                float d = hit.distance;
                if (d < nearest) { nearest = d; point = d > 0f ? hit.point : transform.position; }
            }
            if (nearest < float.MaxValue)
            {
                Detonate(point - dir * 0.15f);
                return;
            }
            transform.position += dir * step;
            left -= step;
            if (left <= 0f) Detonate(transform.position);
        }

        bool Friendly(Collider col)
        {
            if (team == null) return false;
            var member = col.GetComponentInParent<ITeamMember>();
            return member != null && member.Team == team.Team;
        }

        void Detonate(Vector3 at)
        {
            done = true;
            Explosion.Detonate(at, blast, source, null, spareSourceTeam: true);
            // A esfera e a luz somem na hora; as chamas que já saíram terminam sozinhas.
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, lingerSeconds);
        }
    }
}
