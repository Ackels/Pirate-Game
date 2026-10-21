using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Monstro das catacumbas (C5, passo 2, 20/10): a alma penada e o morcego. Não andam no NavMesh nem usam o <c>EnemyFootAI</c>: voam por
    /// código. O <see cref="CryptEncounter"/> da sala acorda (<see cref="Wake"/>) quando o capitão entra e manda dormir quando ele sai (volta ao
    /// lugar com a vida cheia). O golpe deles passa pela guarda do capitão como o de um pirata (<see cref="Strike"/>). Morto, dá a fama e fica
    /// morto até a ruína voltar ao começo (<see cref="ResetToPost"/>).
    /// </summary>
    public abstract class CryptMonster : MonoBehaviour, IAssistTarget
    {
        /// <summary>Attack = começou a preparar o golpe (o aviso); Strike = o golpe saiu; Burst = as almas estouram do chão (Capelão).</summary>
        public enum Cue { Appear, Attack, Strike, Hurt, Die, Vanish, Burst }

        static readonly List<CryptMonster> active = new();
        /// <summary>Os monstros ligados (o aviso de golpe da UI percorre esta lista).</summary>
        public static IReadOnlyList<CryptMonster> Active => active;

        [SerializeField] protected Health health;
        [SerializeField] protected Animator animator;
        [Tooltip("Id do feito ao morrer (EnemyDefeatedEvent) e a fama que dá.")]
        [SerializeField] string defeatId = "monstro_catacumbas";
        [SerializeField] int fame = 1;
        [Tooltip("Só acorda chamado (as almas que o Capelão invoca): a sala não acorda, e morta não dá fama.")]
        [SerializeField] bool summoned;

        protected FootCharacter target;
        protected Health targetHealth;
        protected CharacterCombat targetCombat;
        protected Vector3 post;
        protected Quaternion postRotation;

        public CryptEncounter Encounter { get; internal set; }
        public bool IsAwake { get; private set; }
        public bool IsDead => health && health.IsDead;
        public Health Health => health;
        /// <summary>Espera ser invocado (o <see cref="CryptEncounter"/> não acorda).</summary>
        public bool WaitsForSummon => summoned;
        /// <summary>Alma penada (true) ou morcego (false): o som de cada um.</summary>
        public abstract bool IsGhost { get; }
        /// <summary>
        /// Segundos até o golpe que está sendo preparado acertar (−1 = não está atacando). A UI mostra o aviso e, perto do fim, a hora de
        /// erguer a guarda (20/10, Jhonata: "não dá para entender quando os inimigos estão batendo").
        /// </summary>
        public abstract float ImpactIn { get; }
        /// <summary>Altura do aviso acima do objeto (m).</summary>
        public virtual float WarningHeight => 2.3f;
        /// <summary>O golpe que vem não para na guarda (sair de baixo ou rolar): o aviso muda de cor.</summary>
        public virtual bool WarnUnblockable => false;
        /// <summary>Tom da voz (o Capelão é mais grave).</summary>
        public virtual float VoicePitch => 1f;
        /// <summary>Transparente (sem colisor): só a lâmina sagrada acerta.</summary>
        protected virtual bool IsPhased => false;
        /// <summary>Altura do meio do corpo acima do objeto (onde a lâmina sagrada procura).</summary>
        protected virtual float BodyHeight => 0.9f;

        /// <summary>Momentos que têm som (o <c>GameAudio</c> ouve).</summary>
        public static event Action<CryptMonster, Cue> Cued;

        public void ConfigureBase(Health life, Animator anim, string id, int fameReward, bool onlySummoned = false)
        {
            health = life;
            animator = anim;
            defeatId = id;
            fame = fameReward;
            summoned = onlySummoned;
        }

        protected virtual void Awake()
        {
            if (!health) health = GetComponent<Health>();
            post = transform.position;
            postRotation = transform.rotation;
        }

        protected virtual void OnEnable()
        {
            active.Add(this);
            CharacterCombat.HolySwing += OnHolySwing;
            if (!health) return;
            health.Died += OnDied;
            health.Damaged += OnDamaged;
        }

        protected virtual void OnDisable()
        {
            active.Remove(this);
            CharacterCombat.HolySwing -= OnHolySwing;
            if (!health) return;
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }

        public void Wake(FootCharacter captain)
        {
            if (IsDead || IsAwake || !captain) return;
            target = captain;
            targetHealth = captain.GetComponent<Health>();
            targetCombat = captain.GetComponent<CharacterCombat>();
            IsAwake = true;
            OnWake();
        }

        /// <summary>O capitão saiu da sala (ou morreu): volta ao lugar com a vida cheia.</summary>
        public void Sleep()
        {
            if (!IsAwake) return;
            IsAwake = false;
            if (Encounter) Encounter.EndAttack(this);
            if (!IsDead) health.Revive();
            OnSleep();
        }

        /// <summary>A ruína voltou ao começo: vivo de novo, no lugar.</summary>
        public void ResetToPost()
        {
            IsAwake = false;
            if (Encounter) Encounter.EndAttack(this);
            health.Revive();
            health.Invulnerable = false;
            transform.SetPositionAndRotation(post, postRotation);
            OnReset();
        }

        protected abstract void OnWake();
        protected abstract void OnSleep();
        protected abstract void OnReset();
        protected abstract void OnDeath();

        protected virtual void OnDamaged(DamageInfo info)
        {
            if (!health.IsDead) Raise(Cue.Hurt);
        }

        void OnDied()
        {
            IsAwake = false;
            if (Encounter) Encounter.EndAttack(this);
            Raise(Cue.Die);
            EventBus.Publish(new EnemyDefeatedEvent { targetId = defeatId, fame = summoned ? 0 : fame });
            OnDeath();
        }

        /// <summary>
        /// Golpe da Lâmina de São Telmo: transparente, sem colisor, ninguém acerta; esta confere se o corpo estava no arco do golpe e
        /// leva o dano mesmo assim (<see cref="OnHolyHit"/>).
        /// </summary>
        void OnHolySwing(CharacterCombat by, Vector3 chest, Vector3 direction, float range, float angle, float damage)
        {
            if (!IsAwake || IsDead || !IsPhased || !health) return;
            Vector3 to = transform.position + Vector3.up * BodyHeight - chest;
            if (Mathf.Abs(to.y) > 2f) return;
            to.y = 0f;
            if (to.magnitude > range + 0.45f) return;
            if (angle < 360f && to.sqrMagnitude > 0.0001f && Vector3.Angle(direction, to) > angle * 0.5f) return;
            OnHolyHit(new DamageInfo
            {
                amount = damage, point = transform.position + Vector3.up * BodyHeight, direction = direction, source = by.gameObject, holy = true,
            });
        }

        /// <summary>A lâmina sagrada pegou o corpo transparente: o padrão é levar o dano por cima da invencibilidade.</summary>
        protected virtual void OnHolyHit(DamageInfo info)
        {
            bool was = health.Invulnerable;
            health.Invulnerable = false;
            health.TakeDamage(info);
            if (!health.IsDead) health.Invulnerable = was;
        }

        protected void Raise(Cue cue) => Cued?.Invoke(this, cue);

        protected bool TargetAlive => target && target.isActiveAndEnabled && targetHealth && !targetHealth.IsDead;

        /// <summary>Ponto do peito do capitão (para onde voam e onde o golpe acerta).</summary>
        protected Vector3 TargetChest => target.transform.position + Vector3.up * 1.1f;

        /// <summary>
        /// Golpe no capitão: a guarda dele segura (arranhão) ou apara (nada passa; quem chama reage) como contra um pirata.
        /// <paramref name="from"/>: de onde o golpe vem (a bola de fogo-fátuo; padrão = o monstro); <paramref name="unblockable"/> = a
        /// guarda não segura (as almas que estouram do chão).
        /// </summary>
        protected CharacterCombat.GuardResult Strike(float damage, Vector3 point, Knockdown knockdown = Knockdown.None, Vector3? from = null,
            bool unblockable = false)
        {
            if (!TargetAlive) return CharacterCombat.GuardResult.None;
            Vector3 origin = from ?? transform.position;
            var guard = targetCombat && !unblockable ? targetCombat.Defend(origin, point) : CharacterCombat.GuardResult.None;
            if (guard == CharacterCombat.GuardResult.Parried) return guard;
            bool blocked = guard == CharacterCombat.GuardResult.Blocked;
            Vector3 direction = target.transform.position - origin;
            direction.y = 0f;
            targetHealth.TakeDamage(new DamageInfo
            {
                amount = damage * (targetCombat ? targetCombat.GuardScale(guard) : 1f), point = point,
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward, source = gameObject,
                blocked = blocked, knockdown = blocked ? Knockdown.None : knockdown,
            });
            return guard;
        }

        protected void Play(string state, float fade = 0.12f)
        {
            if (animator && animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(state, fade);
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Cued = null;
            active.Clear();
        }
    }
}
