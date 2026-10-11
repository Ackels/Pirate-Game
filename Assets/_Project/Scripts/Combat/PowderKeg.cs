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
        [Tooltip("Explosão: raio 5 m, 80 → 12 de dano, empurrão 9 + 3,5 para cima, derruba até 60% do raio (~3 m).")]
        [SerializeField] Explosion.Spec explosion = new()
        {
            radius = 5f, centerDamage = 80f, edgeDamage = 12f, push = 9f, lift = 3.5f, knockdownWithin = 0.6f,
        };
        [Tooltip("Pavio (s) depois de acender: um tiro, e o atraso entre barris de uma cadeia.")]
        [SerializeField] Vector2 fuseSeconds = new(0.1f, 0.25f);

        Collider[] colliders;
        float explodeAt = -1f;

        public bool IsSpent { get; private set; }
        public float Radius => explosion.radius;

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

            Explosion.Detonate(transform.position + Vector3.up * 0.5f, explosion, gameObject, this);
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
