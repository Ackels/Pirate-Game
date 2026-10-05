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

            if (staggerSeconds > 0f)
            {
                if (combat) combat.Interrupt(staggerSeconds);
                character.SetSpeedMultiplier(staggerSpeed, staggerSeconds * 0.6f);
            }
            // Quem não é interrompido (o jogador) não perde a animação do próprio golpe: o golpe sai igual.
            bool keepsAttacking = staggerSeconds <= 0f && combat && combat.IsAttacking;
            if (driver && driver.HasHitAnimation && !keepsAttacking) driver.PlayHit();
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
