using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Fera amaldiçoada da Ilha Amaldiçoada (pacote H, passo 2, 06/11): o ogro de caveira e os demônios do Ultimate Monsters do Quaternius
    /// (rig genérico: não usa o Animator humanoide nem o <c>EnemyFootAI</c>). Anda no NavMesh atrás do capitão e escolhe um golpe por
    /// sorteio entre os que alcançam (<see cref="Move"/>): soco, pancada que derruba e, nos demônios, o salto que cai em cima (não para na
    /// guarda: o aviso fica azul, role). Os golpes passam pela guarda como os de um pirata e o aparo deixa a fera tonta. Pesada: golpe não
    /// interrompe. Amaldiçoada: caída pela primeira vez, levanta de novo com parte da vida, a não ser que o golpe final seja de pólvora ou
    /// da Lâmina de São Telmo (como os esqueletos do cemitério). Fica escondida (desligada) até alguém chamar <see cref="Emerge"/>: sai do
    /// chão e luta. Morta de vez, afunda na terra e some.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CursedBeast : CryptMonster
    {
        enum Phase { Hidden, Emerging, Chase, WindUp, Leap, Recover, Stunned, Down, Rising, Dead }

        [System.Serializable]
        public struct Move
        {
            [Tooltip("Estado do Animator (a velocidade dele já faz o impacto cair no fim do preparo).")]
            public string state;
            [Tooltip("Alcance (m); no salto, a distância máxima (e minRange a mínima).")]
            public float range, minRange;
            [Tooltip("Preparo até o impacto (s; o aviso).")]
            public float windUp;
            public float damage, recover, cooldown, weight;
            public Knockdown knockdown;
            [Tooltip("Salto: voa até onde o capitão estava e cai em cima (área, não para na guarda).")]
            public bool leap;
        }

        [SerializeField] NavMeshAgent agent;
        [Tooltip("O modelo (sobe da terra ao sair e afunda ao morrer).")]
        [SerializeField] Transform body;
        [SerializeField] Collider hitBox;
        [SerializeField] float walkSpeed = 2f, runSpeed = 4.2f, turnSpeed = 220f;
        [SerializeField] Move[] moves = System.Array.Empty<Move>();
        [Header("Salto")]
        [SerializeField] float leapSeconds = 0.8f, leapHeight = 3.2f, leapRadius = 3f;
        [Header("Amaldiçoada")]
        [SerializeField] float emergeSeconds = 1.6f, emergeDepth = 3.2f;
        [Tooltip("Caída sem pólvora: levanta depois destes segundos, com esta fração da vida, em reviveSeconds (invencível).")]
        [SerializeField] float reviveDelay = 3f, reviveSeconds = 1.4f;
        [SerializeField, Range(0f, 1f)] float reviveHealth = 0.6f;
        [SerializeField] float stunSeconds = 1.6f, sinkDelay = 4f, sinkSeconds = 1.8f;
        [Header("Visual e som")]
        [SerializeField] float warningHeight = 3.6f, voicePitch = 0.5f, bodyHeight = 1.4f;

        // Uma fera atacando por vez (com duas, os golpes e os saltos saíam juntos: 60 de dano num salto duplo em cima do capitão).
        static CursedBeast striking;
        static float nextLeapAt;

        Phase phase = Phase.Hidden;
        float phaseStart, phaseUntil, nextPath;
        float[] readyAt = System.Array.Empty<float>();
        int current = -1;
        bool struck, revived, lastPowder;
        Vector3 leapFrom, leapTo;
        string loco = "";
        ICameraShake shake;
        CombatEffects effects;

        public override bool IsGhost => true;
        public override float WarningHeight => warningHeight;
        public override float VoicePitch => voicePitch;
        protected override float BodyHeight => bodyHeight;
        public override bool WarnUnblockable => current >= 0 && moves[current].leap;
        public override float ImpactIn => phase switch
        {
            Phase.WindUp => Mathf.Max(0f, phaseUntil - Time.time) + (current >= 0 && moves[current].leap ? leapSeconds : 0f),
            Phase.Leap => Mathf.Max(0f, phaseUntil - Time.time),
            _ => -1f,
        };
        public string PhaseName => phase.ToString();
        /// <summary>Fora de luta de vez: escondida ou morta (o cerco conta).</summary>
        public bool IsGone => phase is Phase.Hidden or Phase.Dead;
        /// <summary>Caída esperando levantar (ainda não acabou).</summary>
        public bool IsDownForNow => phase is Phase.Down or Phase.Rising;

        public void Configure(Transform model, Collider box, float walk, float run, Move[] attacks, float pitch, float warnHeight)
        {
            agent = GetComponent<NavMeshAgent>();
            body = model;
            hitBox = box;
            walkSpeed = walk;
            runSpeed = run;
            moves = attacks;
            voicePitch = pitch;
            warningHeight = warnHeight;
        }

        protected override void Awake()
        {
            base.Awake();
            if (!agent) agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            readyAt = new float[moves.Length];
        }

        // ---------- Ciclo ----------

        /// <summary>Sai da terra (aviso: terra voando, brilho), invencível, e depois persegue o capitão.</summary>
        public void Emerge(FootCharacter captain)
        {
            if (!captain) return;
            gameObject.SetActive(true);
            ResetToPost();
            revived = false;
            lastPowder = false;
            SetBody(-emergeDepth);
            health.Invulnerable = true;
            if (hitBox) hitBox.enabled = false;
            Unearth(true);
            Raise(Cue.Appear);
            Play("Duck", 0f);
            Enter(Phase.Emerging, emergeSeconds);
            Wake(captain);
        }

        /// <summary>Volta para a terra na hora (o cerco acabou ou foi largado): some com poeira.</summary>
        public void Vanish()
        {
            if (!gameObject.activeSelf) return;
            if (phase != Phase.Hidden) Unearth(false);
            Sleep();
            Hide();
        }

        protected override void OnWake()
        {
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = Time.time + Random.Range(0.3f, 1.2f);
        }

        protected override void OnSleep() { }

        protected override void OnReset()
        {
            SetBody(0f);
            if (agent.isActiveAndEnabled) agent.Warp(post);
            current = -1;
            loco = "";
            Play("Idle", 0f);
        }

        void Hide()
        {
            phase = Phase.Hidden;
            current = -1;
            gameObject.SetActive(false);
        }

        void Enter(Phase next, float seconds)
        {
            phase = next;
            phaseStart = Time.time;
            phaseUntil = Time.time + seconds;
        }

        // ---------- Morte e volta ----------

        protected override void OnDamaged(DamageInfo info)
        {
            base.OnDamaged(info);
            lastPowder = info.ignites || info.holy;
        }

        protected override bool DefersDeath() => !revived && !lastPowder;

        protected override void OnFalseDeath()
        {
            StopAgent();
            if (hitBox) hitBox.enabled = false;
            current = -1;
            Play("Death", 0.1f);
            Enter(Phase.Down, reviveDelay);
        }

        protected override void OnDeath()
        {
            StopAgent();
            if (hitBox) hitBox.enabled = false;
            current = -1;
            Play("Death", 0.1f);
            Enter(Phase.Dead, sinkDelay + sinkSeconds);
        }

        // ---------- Por quadro ----------

        void Update()
        {
            switch (phase)
            {
                case Phase.Emerging:
                    SetBody(-emergeDepth * (1f - Smooth(Progress)));
                    if (Time.time >= phaseUntil)
                    {
                        SetBody(0f);
                        health.Invulnerable = false;
                        if (hitBox) hitBox.enabled = true;
                        Raise(Cue.Attack);
                        Enter(Phase.Chase, 0f);
                    }
                    return;
                case Phase.Down:
                    if (Time.time >= phaseUntil)
                    {
                        // A terra devolve a fera: levanta onde caiu.
                        revived = true;
                        health.Revive();
                        health.SetCurrent(health.Max * reviveHealth);
                        health.Invulnerable = true;
                        Unearth(true);
                        Raise(Cue.Appear);
                        Play("Duck", 0.2f);
                        Enter(Phase.Rising, reviveSeconds);
                    }
                    return;
                case Phase.Rising:
                    if (Time.time >= phaseUntil)
                    {
                        health.Invulnerable = false;
                        if (hitBox) hitBox.enabled = true;
                        loco = "";
                        Enter(Phase.Chase, 0f);
                    }
                    return;
                case Phase.Dead:
                    float sinkFrom = phaseStart + sinkDelay;
                    if (Time.time >= sinkFrom) SetBody(-emergeDepth * Mathf.Clamp01((Time.time - sinkFrom) / sinkSeconds));
                    if (Time.time >= phaseUntil) Hide();
                    return;
                case Phase.Hidden:
                    return;
            }

            if (!IsAwake || !TargetAlive)
            {
                if (phase != Phase.Chase) Enter(Phase.Chase, 0f);
                StopAgent();
                Locomotion(0f);
                return;
            }

            switch (phase)
            {
                case Phase.Chase: Chase(); break;
                case Phase.WindUp: WindUp(); break;
                case Phase.Leap: Leap(); break;
                case Phase.Recover:
                case Phase.Stunned:
                    if (phase == Phase.Recover) Face(target.transform.position, 0.5f);
                    if (Time.time >= phaseUntil)
                    {
                        loco = "";
                        Enter(Phase.Chase, 0f);
                    }
                    break;
            }
        }

        float Progress => phaseUntil > phaseStart ? Mathf.Clamp01((Time.time - phaseStart) / (phaseUntil - phaseStart)) : 1f;
        static float Smooth(float t) => t * t * (3f - 2f * t);

        void Chase()
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            int pick = PickMove(d);
            if (pick >= 0)
            {
                StartMove(pick);
                return;
            }
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = d > 7f ? runSpeed : walkSpeed;
                agent.stoppingDistance = 1.6f;
                if (Time.time >= nextPath)
                {
                    nextPath = Time.time + 0.25f;
                    agent.SetDestination(target.transform.position);
                }
            }
            Face(target.transform.position, 1f);
            Locomotion(agent.isActiveAndEnabled ? agent.velocity.magnitude : 0f);
        }

        int PickMove(float distance)
        {
            if (striking && striking != this && striking.isActiveAndEnabled && striking.phase is Phase.WindUp or Phase.Leap) return -1;
            float total = 0f;
            for (int i = 0; i < moves.Length; i++)
                if (Usable(i, distance)) total += moves[i].weight;
            if (total <= 0f) return -1;
            float roll = Random.value * total;
            for (int i = 0; i < moves.Length; i++)
            {
                if (!Usable(i, distance)) continue;
                roll -= moves[i].weight;
                if (roll <= 0f) return i;
            }
            return -1;
        }

        bool Usable(int i, float distance)
        {
            var m = moves[i];
            if (Time.time < readyAt[i] || distance < m.minRange || distance > m.range) return false;
            if (m.leap) return Time.time >= nextLeapAt;
            // Golpe de perto: só de frente para o capitão.
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, to) < 50f;
        }

        void StartMove(int i)
        {
            current = i;
            struck = false;
            striking = this;
            if (moves[i].leap) nextLeapAt = Time.time + moves[i].windUp + leapSeconds + 3f;
            StopAgent();
            Play(moves[i].state, 0.1f);
            Raise(Cue.Attack);
            Enter(Phase.WindUp, moves[i].windUp);
        }

        void WindUp()
        {
            var m = moves[current];
            // Vira para o capitão até pouco antes do golpe (depois, quem sai do lugar escapa).
            if (Time.time < phaseUntil - 0.3f) Face(target.transform.position, 1f);
            if (Time.time < phaseUntil) return;
            if (m.leap)
            {
                leapFrom = transform.position;
                // Cai ao lado do capitão (do lado de onde veio), não em cima: o raio do golpe ainda o pega se ele ficar parado.
                Vector3 back = leapFrom - target.transform.position;
                back.y = 0f;
                leapTo = target.transform.position + (back.sqrMagnitude > 0.01f ? back.normalized : -transform.forward) * 1.6f;
                if (NavMesh.SamplePosition(leapTo, out var hit, 2f, NavMesh.AllAreas)) leapTo = hit.position;
                else leapTo = leapFrom;
                if (agent.enabled) agent.enabled = false;
                Play("Jump", 0.05f);
                Enter(Phase.Leap, leapSeconds);
                return;
            }
            Hit(m, transform.position + transform.forward * 1.2f);
            readyAt[current] = Time.time + m.cooldown;
            Enter(Phase.Recover, m.recover);
        }

        void Leap()
        {
            float t = Progress;
            var p = Vector3.Lerp(leapFrom, leapTo, t);
            p.y += Mathf.Sin(t * Mathf.PI) * leapHeight;
            transform.position = p;
            Face(leapTo, 1f);
            if (t < 1f) return;
            transform.position = leapTo;
            agent.enabled = true;
            agent.Warp(leapTo);
            var m = moves[current];
            Play("Jump_Land", 0.02f);
            Unearth(false);
            if (shake != null || Services.TryGet(out shake)) shake.Shake(0.35f, 0.4f);
            Vector3 flat = target.transform.position - leapTo;
            flat.y = 0f;
            if (flat.magnitude <= leapRadius)
                Strike(m.damage, target.transform.position + Vector3.up, m.knockdown, leapTo, unblockable: true);
            readyAt[current] = Time.time + m.cooldown;
            Enter(Phase.Recover, m.recover);
        }

        void Hit(Move m, Vector3 point)
        {
            if (struck) return;
            struck = true;
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > m.range + 0.6f || (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > 70f)) return;
            Raise(Cue.Strike);
            var guard = Strike(m.damage, target.transform.position + Vector3.up * 1.1f, m.knockdown);
            if (m.knockdown != Knockdown.None && (shake != null || Services.TryGet(out shake))) shake.Shake(0.25f, 0.3f);
            if (guard != CharacterCombat.GuardResult.Parried) return;
            // Aparada: tonta, aberta para o contra-ataque.
            Play("HitReact", 0.05f);
            readyAt[current] = Time.time + m.cooldown;
            Enter(Phase.Stunned, stunSeconds);
        }

        void StopAgent()
        {
            if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        void Face(Vector3 point, float factor)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * factor * Time.deltaTime);
        }

        void Locomotion(float speed)
        {
            string next = speed > 3f ? "Run" : speed > 0.3f ? "Walk" : "Idle";
            if (next == loco) return;
            loco = next;
            Play(next, 0.2f);
        }

        void SetBody(float y)
        {
            if (body) body.localPosition = new Vector3(0f, y, 0f);
        }

        void Unearth(bool rising)
        {
            if (!effects && !Services.TryGet(out effects)) return;
            effects.Unearth(transform.position, rising);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBeastStatics()
        {
            striking = null;
            nextLeapAt = 0f;
        }

        /// <summary>Testes: estado e próximo golpe.</summary>
        public string Describe() => $"{name} {phase} vida {health.Current:0}/{health.Max:0} golpe {(current >= 0 ? moves[current].state : "-")}";
    }
}
