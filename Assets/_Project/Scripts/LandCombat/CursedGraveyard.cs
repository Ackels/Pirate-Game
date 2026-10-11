using System.Collections;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Cemitério amaldiçoado (pacote B, passo 3): os esqueletos (EnemyFootAI do EnemyCamp do mesmo objeto) ficam enterrados (desligados,
    /// sem custo) de dia. De noite, quando o jogador a pé chega perto, saem das covas um a um (terra revolvida, gemido, "Zombie Stand Up" do
    /// Mixamo) e lutam como os piratas do acampamento. Ao amanhecer, os que estão de pé viram pó e os caídos somem; na noite seguinte
    /// levantam de novo, com a vida cheia. Amaldiçoados: caído pela primeira vez, levanta de novo da terra depois de reviveSeconds com
    /// parte da vida, a não ser que o golpe final seja de pólvora (tiro, bacamarte, bomba, barril: DamageInfo.ignites). Nada é salvo.
    /// </summary>
    [RequireComponent(typeof(EnemyCamp))]
    public class CursedGraveyard : MonoBehaviour
    {
        /// <summary>Uma animação de sair da cova: onde começa (deitado, s do clipe) e quanto tempo até ficar de pé (s de jogo).</summary>
        [System.Serializable]
        public struct RiseClip
        {
            public float startSeconds, seconds;
        }

        [Tooltip("O personagem do jogador (a pé).")]
        [SerializeField] Transform player;
        [Tooltip("Jogador a pé mais perto que isto (m) do centro, de noite: os esqueletos saem das covas.")]
        [SerializeField] float triggerRadius = 16f;
        [Tooltip("Noite (ITimeOfDay.Night) a partir da qual eles podem levantar, e abaixo da qual (amanhecendo) viram pó.")]
        [SerializeField, Range(0f, 1f)] float nightFrom = 0.6f, dawnBelow = 0.15f;
        [Tooltip("Segundos entre um esqueleto e o próximo saírem da cova (mínimo, máximo).")]
        [SerializeField] Vector2 riseStagger = new(0.25f, 0.7f);
        [Tooltip("Animações de sair da cova (Erguer0, Erguer1...: mesma ordem dos estados do Animator).")]
        [SerializeField] RiseClip[] riseClips = System.Array.Empty<RiseClip>();
        [Tooltip("Segundos que o esqueleto caído fica no chão ao amanhecer antes de sumir (os de pé viram pó na hora).")]
        [SerializeField] float crumbleDelay = 0.6f;
        [Tooltip("Caído pela primeira vez (sem pólvora no golpe final): levanta de novo depois destes segundos, com esta fração da vida.")]
        [SerializeField] float reviveSeconds = 2.5f;
        [SerializeField, Range(0f, 1f)] float reviveHealth = 0.7f;

        enum Phase { Day, Buried, Risen }

        const float CheckInterval = 0.25f;

        EnemyCamp camp;
        ITimeOfDay time;
        CombatEffects effects;
        Phase phase;
        float nextCheck;
        Coroutine rising;
        // Por esqueleto: já levantou de novo nesta noite; o último golpe tinha pólvora; levantar de novo pendente.
        bool[] revived = System.Array.Empty<bool>(), lastIgnited = System.Array.Empty<bool>(), pending = System.Array.Empty<bool>();
        System.Action<DamageInfo>[] onDamaged = System.Array.Empty<System.Action<DamageInfo>>();
        System.Action[] onDied = System.Array.Empty<System.Action>();

        /// <summary>Os esqueletos estão fora das covas (levantando, lutando ou caídos).</summary>
        public bool IsRisen => phase == Phase.Risen;
        public float TriggerRadius => triggerRadius;

        public void Configure(Transform playerCharacter, float radius, RiseClip[] clips)
        {
            player = playerCharacter;
            triggerRadius = radius;
            riseClips = clips ?? System.Array.Empty<RiseClip>();
        }

        void Awake()
        {
            camp = GetComponent<EnemyCamp>();
            // A fama e as missões só quando todos caíram de vez (não na primeira queda de cada um).
            camp.HoldClearedEvent = true;
            int n = camp.Members.Count;
            revived = new bool[n];
            lastIgnited = new bool[n];
            pending = new bool[n];
            onDamaged = new System.Action<DamageInfo>[n];
            onDied = new System.Action[n];
            for (int i = 0; i < n; i++)
            {
                int index = i;
                onDamaged[i] = info => lastIgnited[index] = info.ignites;
                onDied[i] = () => OnMemberDied(index);
            }
        }

        void OnEnable()
        {
            for (int i = 0; i < onDied.Length; i++)
            {
                // GetComponent: o Awake dos esqueletos (que guarda o Health) pode ainda não ter rodado.
                var h = camp.Members[i] ? camp.Members[i].GetComponent<Health>() : null;
                if (!h) continue;
                h.Damaged += onDamaged[i];
                h.Died += onDied[i];
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < onDied.Length; i++)
            {
                var h = camp.Members[i] ? camp.Members[i].GetComponent<Health>() : null;
                if (!h) continue;
                h.Damaged -= onDamaged[i];
                h.Died -= onDied[i];
            }
        }

        void OnMemberDied(int index)
        {
            if (phase == Phase.Risen && !revived[index] && !lastIgnited[index])
            {
                pending[index] = true;
                StartCoroutine(ReviveLater(index));
                return;
            }
            CheckCleared();
        }

        /// <summary>Caído pela primeira vez: a terra o devolve (levanta deitado de onde caiu, com parte da vida).</summary>
        IEnumerator ReviveLater(int index)
        {
            yield return new WaitForSeconds(reviveSeconds);
            pending[index] = false;
            var m = camp.Members[index];
            if (phase != Phase.Risen || !m || !m.gameObject.activeInHierarchy || !m.IsDead)
            {
                CheckCleared();
                yield break;
            }
            revived[index] = true;
            if (m.TryGetComponent(out FootHealthReactions reactions)) reactions.Revive();
            m.Health.SetCurrent(m.Health.Max * reviveHealth);
            int variant = riseClips.Length > 0 ? Random.Range(0, riseClips.Length) : 0;
            var clip = riseClips.Length > 0 ? riseClips[variant] : new RiseClip { seconds = 1.5f };
            m.Rise(variant, clip.startSeconds, clip.seconds);
            if (!effects) Services.TryGet(out effects);
            if (effects) effects.Unearth(m.transform.position, true);
        }

        /// <summary>Todos caídos de vez (nenhum para levantar de novo): acampamento derrotado.</summary>
        void CheckCleared()
        {
            if (!camp.IsCleared) return;
            foreach (bool p in pending) if (p) return;
            camp.PublishCleared();
        }

        void Start() => Bury();

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;
            if (time == null && !Services.TryGet(out time)) return;
            float night = time.Night;
            switch (phase)
            {
                case Phase.Day:
                    if (night >= nightFrom) phase = Phase.Buried;
                    break;
                case Phase.Buried:
                    if (night < dawnBelow) phase = Phase.Day;
                    else if (night >= nightFrom && PlayerNear()) rising = StartCoroutine(Rise());
                    break;
                case Phase.Risen:
                    if (night < dawnBelow) StartCoroutine(Crumble());
                    break;
            }
        }

        bool PlayerNear()
        {
            if (!player || !player.gameObject.activeInHierarchy) return false;
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            return d.magnitude <= triggerRadius;
        }

        /// <summary>Esqueletos debaixo da terra: desligados (sem IA, animação, colisor nem custo).</summary>
        void Bury()
        {
            if (rising != null) StopCoroutine(rising);
            rising = null;
            foreach (var m in camp.Members) if (m) m.gameObject.SetActive(false);
            phase = time != null && time.Night >= nightFrom ? Phase.Buried : Phase.Day;
        }

        /// <summary>Um por um, mais perto do jogador primeiro: liga, volta à cova com a vida cheia e sai dela.</summary>
        IEnumerator Rise()
        {
            phase = Phase.Risen;
            if (!effects) Services.TryGet(out effects);
            camp.WakeUp();
            for (int i = 0; i < revived.Length; i++) revived[i] = lastIgnited[i] = pending[i] = false;
            var order = new System.Collections.Generic.List<EnemyFootAI>(camp.Members);
            order.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
            for (int i = 0; i < order.Count; i++)
            {
                var m = order[i];
                if (!m) continue;
                if (i > 0) yield return new WaitForSeconds(Random.Range(riseStagger.x, riseStagger.y));
                m.gameObject.SetActive(true);
                m.ResetToPost();
                int variant = riseClips.Length > 0 ? Random.Range(0, riseClips.Length) : 0;
                var clip = riseClips.Length > 0 ? riseClips[variant] : new RiseClip { seconds = 1.5f };
                m.Rise(variant, clip.startSeconds, clip.seconds);
                if (effects) effects.Unearth(m.transform.position, true);
            }
            rising = null;
        }

        float Distance(EnemyFootAI m) => m && player ? (m.transform.position - player.position).sqrMagnitude : 0f;

        /// <summary>Amanhecer: os de pé viram pó (poeira de osso), os caídos somem um pouco depois; todos voltam para a cova.</summary>
        IEnumerator Crumble()
        {
            phase = Phase.Day;
            if (rising != null) StopCoroutine(rising);
            rising = null;
            if (!effects) Services.TryGet(out effects);
            foreach (var m in camp.Members)
            {
                if (!m || !m.gameObject.activeSelf || m.IsDead) continue;
                if (effects) effects.Unearth(m.transform.position, false);
                m.gameObject.SetActive(false);
            }
            yield return new WaitForSeconds(crumbleDelay);
            foreach (var m in camp.Members)
            {
                if (!m || !m.gameObject.activeSelf) continue;
                if (effects) effects.Unearth(m.transform.position, false);
                m.gameObject.SetActive(false);
            }
        }

        /// <summary>Testes e sandbox: levanta agora (se for noite) ou devolve todos à cova.</summary>
        public void RiseNow()
        {
            if (phase != Phase.Risen) rising = StartCoroutine(Rise());
        }

        public void BuryNow() => Bury();
    }
}
