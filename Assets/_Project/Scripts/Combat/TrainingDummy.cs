using System.Collections;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Boneco de treino (alvo de espada e pistola): balança ao levar dano, tomba ao zerar a vida e se levanta
    /// com a vida cheia depois de alguns segundos. O visual balança; o colisor fica parado.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour, Pirates.Core.IAssistTarget
    {
        [SerializeField] Transform visual;
        [Tooltip("Graus de balanço por ponto de dano.")]
        [SerializeField] float wobblePerDamage = 0.8f;
        [SerializeField] float wobbleDamping = 6f;
        [SerializeField] float wobbleFrequency = 9f;
        [SerializeField] float respawnDelay = 4f;

        Health health;
        Vector3 wobbleAxis;
        float wobbleAmount, wobbleTime;
        bool fallen;

        public void Configure(Transform visualRoot) => visual = visualRoot;

        void Awake() => health = GetComponent<Health>();

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
            Vector3 dir = info.direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            // Tomba para o lado em que foi empurrado.
            wobbleAxis = Vector3.Cross(Vector3.up, dir.normalized);
            wobbleAmount = Mathf.Min(35f, wobbleAmount + info.amount * wobblePerDamage);
            wobbleTime = 0f;
        }

        void OnDied() => StartCoroutine(FallAndRespawn());

        void Update()
        {
            if (fallen || !visual || wobbleAmount <= 0.01f) return;
            wobbleTime += Time.deltaTime;
            wobbleAmount *= Mathf.Exp(-wobbleDamping * Time.deltaTime);
            float angle = wobbleAmount * Mathf.Sin(wobbleTime * wobbleFrequency);
            visual.localRotation = Quaternion.AngleAxis(angle, transform.InverseTransformDirection(wobbleAxis));
        }

        IEnumerator FallAndRespawn()
        {
            fallen = true;
            Quaternion start = visual ? visual.localRotation : Quaternion.identity;
            Quaternion down = Quaternion.AngleAxis(85f, transform.InverseTransformDirection(wobbleAxis.sqrMagnitude > 0f ? wobbleAxis : transform.right));
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (visual) visual.localRotation = Quaternion.Slerp(start, down, t / 0.5f);
                yield return null;
            }
            yield return new WaitForSeconds(respawnDelay);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (visual) visual.localRotation = Quaternion.Slerp(down, Quaternion.identity, t / 0.6f);
                yield return null;
            }
            if (visual) visual.localRotation = Quaternion.identity;
            wobbleAmount = 0f;
            fallen = false;
            health.Revive();
        }
    }
}
