using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Acampamento de piratas em terra: quando um vê o jogador, avisa os outros (cada um reage com um pequeno atraso);
    /// só alguns atacam de cada vez (vez de atacar), os outros cercam esperando, e os golpes saem espaçados; longe demais do acampamento, desistem
    /// e voltam para o posto. Tranca o baú do tesouro até o último pirata cair (ILockSource). ResetAll levanta todos
    /// nos postos (sandbox).
    /// </summary>
    public class EnemyCamp : MonoBehaviour, ILockSource
    {
        public static readonly List<EnemyCamp> Active = new();

        [SerializeField] string displayName = "Acampamento pirata";
        [Tooltip("Id para as missões (\"derrote o acampamento X\"): mesmo texto do targetId da missão.")]
        [SerializeField] string campId = "acampamento";
        [Tooltip("Fama do capitão ao derrotar o último pirata (M10).")]
        [SerializeField, Min(0)] int fame = 15;
        [Tooltip("Longe disto (m) do centro, o pirata desiste de perseguir e volta para o posto.")]
        [SerializeField] float leashRadius = 32f;
        [Tooltip("Levou tiro ou golpe: por estes segundos (renova a cada acerto) o acampamento todo persegue mais longe e sabe onde " +
                 "o jogador está (sem isso, dava para matar todos de fora do raio: corriam até a borda e voltavam).")]
        [SerializeField, Min(0f)] float provokedSeconds = 15f;
        [Tooltip("Até onde perseguem (m do centro) enquanto provocados.")]
        [SerializeField, Min(0f)] float provokedLeash = 85f;
        [Tooltip("Quantos atacam ao mesmo tempo; os outros esperam em volta.")]
        [SerializeField, Min(1)] int maxAttackers = 2;
        [Tooltip("Segundos mínimos entre golpes de piratas diferentes: não batem todos no mesmo instante.")]
        [SerializeField] float strikeSpacing = 0.45f;
        [SerializeField] EnemyFootAI[] members = System.Array.Empty<EnemyFootAI>();
        [Tooltip("Barris de pólvora do acampamento: inteiros de novo quando ele é repovoado.")]
        [SerializeField] PowderKeg[] kegs = System.Array.Empty<PowderKeg>();

        [Header("Longe do jogador")]
        [Tooltip("Mais longe que isto (m) da câmera e fora de combate, os piratas dormem: ficam parados na pose, sem IA, " +
                 "animação nem passos (economia de CPU). Um golpe ou ResetAll acordam na hora.")]
        [SerializeField] float sleepDistance = 180f;
        [Tooltip("Acordam mais perto que isto (folga para não ficar liga-desliga na divisa).")]
        [SerializeField] float wakeDistance = 160f;

        const float SleepCheckInterval = 0.5f;

        readonly List<EnemyFootAI> attackers = new();
        readonly List<Behaviour> sleeping = new();
        float nextStrike, nextSleepCheck;

        /// <summary>Piratas parados por estarem longe da câmera (ver <see cref="sleepDistance"/>).</summary>
        public bool IsAsleep { get; private set; }

        public string DisplayName => displayName;
        public string CampId => campId;
        public Vector3 Center => transform.position;
        public float LeashRadius => leashRadius;
        /// <summary>Alguém do acampamento levou tiro ou golpe há pouco.</summary>
        public bool IsProvoked => Time.time < provokedUntil;
        /// <summary>Raio de perseguição agora (maior enquanto provocado).</summary>
        public float EffectiveLeash => IsProvoked ? Mathf.Max(leashRadius, provokedLeash) : leashRadius;
        float provokedUntil = float.NegativeInfinity;
        public IReadOnlyList<EnemyFootAI> Members => members;
        public int Total => members.Length;

        public int Alive
        {
            get
            {
                int alive = 0;
                foreach (var m in members) if (m && !m.IsDead) alive++;
                return alive;
            }
        }

        public bool IsCleared => Total > 0 && Alive == 0;
        public bool IsLocked => !IsCleared;
        public string LockedReason
        {
            get
            {
                // O HUD pergunta a cada quadro perto do baú: refaz o texto só quando muda quantos estão vivos.
                int alive = Alive;
                if (alive != lockedReasonFor || lockedReason == null)
                {
                    lockedReasonFor = alive;
                    lockedReason = alive == 1 ? "Derrote o último pirata do acampamento" : $"Derrote os {alive} piratas do acampamento";
                }
                return lockedReason;
            }
        }

        string lockedReason;
        int lockedReasonFor = -1;
        /// <summary>Algum pirata está perseguindo ou lutando.</summary>
        public bool InCombat
        {
            get
            {
                foreach (var m in members) if (m && !m.IsDead && m.IsAlerted) return true;
                return false;
            }
        }

        /// <summary>Fama que derrotar o acampamento dá (M10): acampamento grande mais, posto de guarda menos.</summary>
        public void SetFame(int value) => fame = Mathf.Max(0, value);

        public void Configure(string name, string id, EnemyFootAI[] enemies, float leash, int attackersAtOnce)
        {
            displayName = name;
            campId = id;
            members = enemies;
            leashRadius = leash;
            maxAttackers = Mathf.Max(1, attackersAtOnce);
        }

        public void SetKegs(PowderKeg[] powderKegs) => kegs = powderKegs ?? System.Array.Empty<PowderKeg>();

        void OnEnable()
        {
            Active.Add(this);
            // GetComponent: o Awake dos piratas pode ainda não ter rodado.
            foreach (var m in members)
                if (m && m.TryGetComponent(out Health h))
                {
                    h.Died += OnMemberDied;
                    h.Damaged += OnMemberDamaged;
                }
            EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
            // Acampamentos conferem a distância em momentos diferentes.
            nextSleepCheck = Time.unscaledTime + Random.value * SleepCheckInterval;
        }

        void OnDisable()
        {
            Active.Remove(this);
            foreach (var m in members)
                if (m && m.TryGetComponent(out Health h))
                {
                    h.Died -= OnMemberDied;
                    h.Damaged -= OnMemberDamaged;
                }
            EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void Update()
        {
            if (Time.unscaledTime < nextSleepCheck) return;
            nextSleepCheck = Time.unscaledTime + SleepCheckInterval;
            var cam = Camera.main;
            if (!cam) return;
            Vector3 d = cam.transform.position - Center;
            d.y = 0f;
            float distance = d.magnitude;
            if (IsAsleep) { if (distance < wakeDistance) WakeUp(); }
            else if (distance > sleepDistance && !InCombat) Sleep();
        }

        /// <summary>
        /// Para os piratas onde estão: desliga tudo neles (IA, personagem, animação, mira, som) menos a vida e o
        /// EnemyFootAI (continuam na lista de piratas e recebendo golpe). O Animator desligado guarda a pose.
        /// </summary>
        void Sleep()
        {
            IsAsleep = true;
            foreach (var m in members)
            {
                if (!m) continue;
                foreach (var b in m.GetComponentsInChildren<Behaviour>())
                {
                    if (!b.enabled || b is EnemyFootAI || b is Health) continue;
                    if (b is Animator animator) animator.keepAnimatorStateOnDisable = true;
                    b.enabled = false;
                    sleeping.Add(b);
                }
            }
        }

        /// <summary>Volta tudo ao normal (a câmera chegou perto, alguém levou golpe ou o acampamento vai ser reiniciado).</summary>
        public void WakeUp()
        {
            if (!IsAsleep) return;
            IsAsleep = false;
            foreach (var b in sleeping) if (b) b.enabled = true;
            sleeping.Clear();
            nextSleepCheck = Time.unscaledTime + SleepCheckInterval;
        }

        // Golpe num pirata dormindo (canhão de longe, sandbox): acorda antes do Died, para a reação de morte receber o evento,
        // e fica acordado uns segundos para a queda terminar (senão congelaria no meio).
        void OnMemberDamaged(DamageInfo _)
        {
            WakeUp();
            nextSleepCheck = Time.unscaledTime + 3f;
        }

        void OnMemberDied()
        {
            if (IsCleared) EventBus.Publish(new CampClearedEvent { campId = campId, fame = fame });
        }

        /// <summary>Missão nova contra este acampamento: se já estava derrotado, os piratas voltam.</summary>
        void OnMissionStarted(MissionStartedEvent evt)
        {
            if (evt.targetId == campId && IsCleared) ResetAll();
        }

        /// <summary>Um pirata levou tiro ou golpe: o acampamento todo vem e persegue mais longe por um tempo.</summary>
        public void Provoke(EnemyFootAI by)
        {
            provokedUntil = Time.time + provokedSeconds;
            RaiseAlarm(by);
        }

        /// <summary>Um pirata viu o jogador: os outros acordam.</summary>
        public void RaiseAlarm(EnemyFootAI by)
        {
            foreach (var m in members)
                if (m && m != by) m.HearAlarm();
        }

        /// <summary>Pede a vez de atacar. true = pode atacar agora (já tinha a vez ou havia vaga).</summary>
        public bool RequestAttackSlot(EnemyFootAI ai)
        {
            attackers.RemoveAll(a => !a || a.IsDead || !a.IsAlerted);
            if (attackers.Contains(ai)) return true;
            if (attackers.Count >= maxAttackers) return false;
            attackers.Add(ai);
            return true;
        }

        public void ReleaseAttackSlot(EnemyFootAI ai) => attackers.Remove(ai);

        /// <summary>Pede para começar um golpe agora. false = outro pirata acabou de golpear (espere um pouco).</summary>
        public bool TryStrike()
        {
            if (Time.time < nextStrike) return false;
            nextStrike = Time.time + strikeSpacing;
            return true;
        }

        /// <summary>Levanta todos nos postos, com a vida cheia (sandbox).</summary>
        public void ResetAll()
        {
            WakeUp();
            attackers.Clear();
            provokedUntil = float.NegativeInfinity;
            foreach (var m in members) if (m) m.ResetToPost();
            foreach (var keg in kegs) if (keg) keg.Restore();
        }
    }
}
