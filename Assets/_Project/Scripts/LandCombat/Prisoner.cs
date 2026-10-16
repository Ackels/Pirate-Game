using System.Collections;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Prisioneiro numa gaiola de um acampamento (C2, 16/10; pacote C, exploração). A gaiola fica trancada enquanto o acampamento tiver
    /// pirata de pé (o mesmo tranca do baú: <see cref="EnemyCamp.IsCleared"/>); limpo, F solta (quem paga a recompensa é o
    /// PrisonerRescuer do capitão: tripulantes, itens e fama). Ele acena quando o capitão chega perto, comemora ao sair e some numa
    /// nuvem de poeira ("foi para o seu navio"). Acampamento repovoado = outro prisioneiro na gaiola.
    /// </summary>
    public class Prisoner : MonoBehaviour
    {
        static readonly List<Prisoner> active = new();
        public static IReadOnlyList<Prisoner> Active => active;

        [SerializeField] string displayName = "Prisioneiro";
        [SerializeField] EnemyCamp camp;
        [SerializeField] Animator animator;
        [Tooltip("O corpo (some depois de solto).")]
        [SerializeField] GameObject body;
        [Tooltip("Porta da gaiola (gira no eixo Y ao abrir).")]
        [SerializeField] Transform door;
        [SerializeField, Min(0)] int crew = 1;
        [SerializeField] ItemStack[] reward = System.Array.Empty<ItemStack>();
        [SerializeField, Min(0)] int fame = 3;
        [SerializeField, TextArea] string[] thanks = { "Obrigado, capitão! Achei que ia apodrecer aqui." };
        [SerializeField] float doorOpenDegrees = -110f, vanishSeconds = 2.4f;

        bool freed;
        float nextWave;
        Quaternion doorClosed;

        public string DisplayName => displayName;
        public bool Freed => freed;
        public bool IsLocked => !freed && camp && !camp.IsCleared;
        public string LockedReason => camp ? camp.LockedReason : "";
        public bool CanFree => !freed && !IsLocked;
        public int Crew => crew;
        public IReadOnlyList<ItemStack> Reward => reward;
        public int Fame => fame;

        public void Configure(string name, EnemyCamp ownerCamp, Animator model, GameObject bodyRoot, Transform cageDoor, int crewJoining,
            ItemStack[] items, int fameReward, string[] thankLines)
        {
            displayName = name;
            camp = ownerCamp;
            animator = model;
            body = bodyRoot;
            door = cageDoor;
            crew = crewJoining;
            reward = items;
            fame = fameReward;
            thanks = thankLines;
        }

        void Awake()
        {
            if (door) doorClosed = door.localRotation;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Update()
        {
            // Acampamento repovoado (missão aceita, F1, a História): a gaiola tem gente de novo.
            if (freed && camp && !camp.IsCleared) ResetCage();
        }

        /// <summary>O capitão está perto (o PrisonerRescuer avisa): vira para ele e acena de vez em quando.</summary>
        public void Notice(Vector3 captain)
        {
            if (freed || !body || !body.activeSelf) return;
            Vector3 to = captain - body.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, Quaternion.LookRotation(to), 180f * Time.deltaTime);
            if (Time.time < nextWave) return;
            nextWave = Time.time + Random.Range(4f, 7f);
            Gesture("Wave");
        }

        /// <summary>Abre a gaiola; devolve a fala de agradecimento.</summary>
        public string Free()
        {
            if (!CanFree) return null;
            freed = true;
            StopAllCoroutines();
            StartCoroutine(Leave());
            Gesture("Yes");
            return thanks.Length > 0 ? thanks[Random.Range(0, thanks.Length)] : "";
        }

        IEnumerator Leave()
        {
            Quaternion open = doorClosed * Quaternion.Euler(0f, doorOpenDegrees, 0f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (door) door.localRotation = Quaternion.Slerp(doorClosed, open, Mathf.SmoothStep(0f, 1f, t / 0.6f));
                yield return null;
            }
            if (door) door.localRotation = open;
            yield return new WaitForSeconds(vanishSeconds);
            if (body)
            {
                if (Services.TryGet(out CombatEffects effects)) effects.Unearth(body.transform.position, false);
                body.SetActive(false);
            }
        }

        void ResetCage()
        {
            StopAllCoroutines();
            freed = false;
            if (door) door.localRotation = doorClosed;
            if (body) body.SetActive(true);
        }

        void Gesture(string state)
        {
            if (animator && animator.isActiveAndEnabled && animator.HasState(0, Animator.StringToHash(state)))
                animator.CrossFadeInFixedTime(state, 0.2f);
        }
    }
}
