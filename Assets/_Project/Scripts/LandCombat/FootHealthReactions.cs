using System;
using System.Collections;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// O que a vida faz com um personagem a pé (jogador ou pirata). Levou golpe: reação (animação "Golpeado" do Mixamo
    /// ou, sem ela, um tranco do corpo por código) e, se configurado, atordoamento: o golpe que ia sair é cancelado.
    /// Morreu: cai (animação "Morte" ou queda por código), fica fora de ação e sem colisor. Revive() levanta de novo
    /// com a vida cheia (reaparecer na praia, sandbox).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(FootCharacter))]
    public class FootHealthReactions : MonoBehaviour
    {
        [Tooltip("Visual do personagem (filho com o Animator).")]
        [SerializeField] Transform model;
        [Tooltip("Segundos sem atacar depois de levar um golpe (o golpe que ia sair é cancelado). 0 = não interrompe.")]
        [SerializeField] float staggerSeconds = 0.6f;
        [Tooltip("Fração da velocidade enquanto atordoado.")]
        [SerializeField, Range(0f, 1f)] float staggerSpeed = 0.3f;

        [Header("Derrubado (pacote A, 09/10)")]
        [Tooltip("Segundos no chão até ficar de pé: chute (cambaleia e cai sentado) e explosão (arremessado de costas). Iguais às animações.")]
        [SerializeField] float stumbleSeconds = 1.5f, blastSeconds = 2.9f;
        [Tooltip("Levanta rápido da explosão (o capitão: ~1,3 s; os piratas ficam blastSeconds) e fica invencível um instante ao levantar.")]
        [SerializeField] bool quickRecovery;
        [SerializeField] float quickBlastSeconds = 1.3f, getUpInvulnerable = 0.5f;
        [Tooltip("Depois de levantar, segundos em que não cai de novo (chute atrás de chute travava o pirata no chão: revisão de 09/10).")]
        [SerializeField] float knockImmunity = 2f;
        [Tooltip("Pesado (brutamontes): o chute não derruba (vira golpe comum); a explosão derruba.")]
        [SerializeField] bool ignoreStumble;
        [Tooltip("Inabalável (chefe): nada derruba, nem a explosão (o dano passa).")]
        [SerializeField] bool ignoreKnockdown;

        [Header("Provisório (sem clipes do Mixamo)")]
        [Tooltip("Graus que o corpo tomba para trás ao levar golpe.")]
        [SerializeField] float flinchDegrees = 14f;
        [SerializeField] float flinchRecovery = 9f;
        [SerializeField] float fallSeconds = 0.55f;

        Health health;
        FootCharacter character;
        CharacterCombat combat;
        CharacterAnimatorDriver driver;
        Quaternion modelRest;
        Vector3 modelRestPosition, hitAxis = Vector3.right;
        float flinch;
        Coroutine falling;
        GameObject lastAttacker;
        /// <summary>Esta peça ligou o Invulnerable da vida pelo rolamento (o F1 também liga: só desligamos o que ligamos).</summary>
        bool dodgeShield;

        /// <summary>Capitão do jogador: levanta rápido da explosão e fica invencível um instante ao levantar.</summary>
        public void SetQuickRecovery(bool value) => quickRecovery = value;

        /// <summary>Pesado (brutamontes): chute não derruba.</summary>
        public void SetHeavy(bool value) => ignoreStumble = value;

        /// <summary>Inabalável (chefe): nada derruba.</summary>
        public void SetUnstoppable(bool value) => ignoreKnockdown = value;

        public Health Health => health;
        public bool IsDead => health.IsDead;
        /// <summary>Morreu (quem matou, se souber).</summary>
        public event Action<GameObject> Died;

        public void Configure(Transform visual, float stagger)
        {
            model = visual;
            staggerSeconds = stagger;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            character = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            if (model) driver = model.GetComponent<CharacterAnimatorDriver>();
            if (model)
            {
                modelRest = model.localRotation;
                modelRestPosition = model.localPosition;
            }
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void OnDamaged(DamageInfo info)
        {
            lastAttacker = info.source;
            // Tomba para o lado contrário de onde veio o golpe.
            Vector3 push = info.direction;
            push.y = 0f;
            if (push.sqrMagnitude < 0.0001f && info.source) push = transform.position - info.source.transform.position;
            push.y = 0f;
            if (push.sqrMagnitude < 0.0001f) push = -transform.forward;
            hitAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, push.normalized));
            if (health.IsDead) return;
            // No chão: leva o dano, mas sem tranco nem queda nova (não fica preso caindo).
            if (character.IsDown) return;
            // Na guarda: só o arranhão (a reação é a da defesa, no CharacterAnimatorDriver).
            if (info.blocked) return;
            // Pesado (brutamontes): chute não derruba (vira um golpe comum); explosão derruba.
            bool knocks = info.knockdown != Knockdown.None && !ignoreKnockdown && !(ignoreStumble && info.knockdown == Knockdown.Stumble);
            if (knocks && Time.time >= character.DownUntil + knockImmunity)
            {
                bool blast = info.knockdown == Knockdown.Blast;
                float seconds = !blast ? stumbleSeconds : quickRecovery ? quickBlastSeconds : blastSeconds;
                int variant = !blast ? 0 : quickRecovery ? 2 : 1;
                if (combat) combat.Interrupt(seconds);
                if (driver && driver.PlayKnockdown(variant)) character.KnockDown(seconds);
                else
                {
                    // Sem os clipes: só o tranco e um atordoamento curto.
                    flinch = 1f;
                    character.SetSpeedMultiplier(staggerSpeed, 1f);
                }
                return;
            }

            if (staggerSeconds > 0f)
            {
                if (combat) combat.Interrupt(staggerSeconds);
                character.SetSpeedMultiplier(staggerSpeed, staggerSeconds * 0.6f);
            }
            // Quem não é interrompido (o jogador) não perde a animação do próprio golpe: o golpe sai igual.
            bool keepsAttacking = staggerSeconds <= 0f && combat && combat.IsAttacking;
            // Rolando, na guarda (golpe de lado), chutando, arremessando, puxando alavanca, levantando ou cambaleando do aparo: a
            // animação da ação continua e o golpe é só o tranco (a pancada cortava o rolamento no meio; revisão de 21/10).
            bool inAction = character.IsDodging || (combat && combat.IsBlocking) || (driver && driver.InAction);
            // Golpe pelas costas: as reações do Mixamo são todas de frente (o corpo ia para trás, na direção de quem bateu); fica o tranco.
            if (Vector3.Angle(transform.forward, -push) > 110f) inAction = true;
            // Inabalável (chefe): nem a animação de levar golpe, só o tranco curto do corpo.
            if (driver && driver.HasHitAnimation && !keepsAttacking && !inAction && !ignoreKnockdown) driver.PlayHit();
            else flinch = 1f;
        }

        void OnDied()
        {
            flinch = 0f;
            if (combat) combat.Interrupt(0f);
            character.SetIncapacitated(true);
            if (driver) driver.PlayDeath();
            if (!driver || !driver.HasDeathAnimation) falling = StartCoroutine(FallOver());
            Died?.Invoke(lastAttacker);
        }

        /// <summary>Levanta com a vida cheia, de pé, pronto para lutar.</summary>
        public void Revive()
        {
            if (falling != null) StopCoroutine(falling);
            falling = null;
            flinch = 0f;
            if (model) model.SetLocalPositionAndRotation(modelRestPosition, modelRest);
            health.Revive();
            character.SetIncapacitated(false);
            if (driver) driver.PlayRevive();
            if (TryGetComponent(out Stamina stamina)) stamina.Refill();
        }

        /// <summary>Rolamento: nada acerta no meio dele (golpe, tiro, explosão).</summary>
        void Update()
        {
            float sinceUp = Time.time - character.DownUntil;
            bool shield = (character.DodgeInvulnerable || (quickRecovery && sinceUp >= 0f && sinceUp < getUpInvulnerable)) && !health.IsDead;
            if (shield == dodgeShield) return;
            if (shield && health.Invulnerable) return;   // já invulnerável por outro motivo (F1)
            dodgeShield = shield;
            health.Invulnerable = shield;
        }

        void LateUpdate()
        {
            if (!model || falling != null || health.IsDead || flinch <= 0.001f) return;
            // Tranco: tomba na hora e volta amortecido.
            flinch *= Mathf.Exp(-flinchRecovery * Time.deltaTime);
            model.localRotation = modelRest * Quaternion.AngleAxis(flinchDegrees * flinch, hitAxis);
        }

        /// <summary>Queda provisória: gira o visual em volta dos pés até deitar no chão.</summary>
        IEnumerator FallOver()
        {
            if (!model) yield break;
            Quaternion start = model.localRotation;
            Quaternion down = modelRest * Quaternion.AngleAxis(84f, hitAxis);
            for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
            {
                // Acelera como um tombo de verdade.
                float k = t / fallSeconds;
                k *= k;
                model.SetLocalPositionAndRotation(modelRestPosition + Vector3.up * 0.12f * k, Quaternion.Slerp(start, down, k));
                yield return null;
            }
            model.SetLocalPositionAndRotation(modelRestPosition + Vector3.up * 0.12f, down);
            falling = null;
        }
    }
}
