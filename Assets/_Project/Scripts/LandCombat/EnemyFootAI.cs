using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.LandCombat
{
    /// <summary>
    /// IA de um pirata a pé. "Aperta os botões" do mesmo FootCharacter/CharacterCombat do jogador (IFootInput), então
    /// anda, corre, gira e golpeia com as mesmas animações. Guarda o posto até ver o jogador (alcance + ângulo de visão
    /// + linha de visão; muito perto percebe mesmo de costas, e ao levar golpe), reage, avisa o acampamento e persegue
    /// pelo NavMesh. Com a vez de atacar (o acampamento limita quantos), chega perto e golpeia em intervalos, às vezes em
    /// combo; sem a vez, espera em volta encarando. Com pistola (atirador), mantém distância: segura a mira (a linha
    /// vermelha avisa) e atira depois de um tempo; a mira segue o jogador com atraso (correndo de lado dá para desviar);
    /// recarregando, recua; colado, recua mirando. Perde o jogador de vista por um tempo ou longe do acampamento: volta.
    /// </summary>
    [RequireComponent(typeof(FootCharacter), typeof(CharacterCombat), typeof(Health))]
    public class EnemyFootAI : MonoBehaviour, IFootInput
    {
        public enum State { Guard, Alert, Chase, Return, Dead }

        public static readonly List<EnemyFootAI> Active = new();
        /// <summary>Debug (sandbox): ninguém vê o jogador (só reagem se apanharem).</summary>
        public static bool Distracted { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Distracted = false;

        [SerializeField] Transform target;
        [SerializeField] EnemyCamp camp;

        [Header("Percepção")]
        [SerializeField] float sightRange = 16f;
        [Tooltip("Noite fechada (M11): o alcance da vista vira esta fração (o 'perto demais' e o tiro/golpe que provoca continuam iguais).")]
        [SerializeField, Range(0.2f, 1f)] float nightSight = 0.6f;
        [Tooltip("Abertura da visão (graus à frente).")]
        [SerializeField] float sightAngle = 150f;
        [Tooltip("Mais perto que isto (m) percebe o jogador mesmo de costas.")]
        [SerializeField] float hearRange = 4.5f;
        [Tooltip("Segundos entre perceber e partir para cima (mínimo, máximo).")]
        [SerializeField] Vector2 reactionTime = new(0.3f, 0.7f);
        [Tooltip("Segundos sem ver o jogador até desistir.")]
        [SerializeField] float loseTargetAfter = 6f;

        [Header("Luta")]
        [SerializeField] float attackRange = 1.9f;
        [Tooltip("Sem a vez de atacar, espera a esta distância (m), encarando.")]
        [SerializeField] float waitRange = 3.6f;
        [Tooltip("Segundos de folga entre um ataque e o próximo (mínimo, máximo), além do tempo do golpe.")]
        [SerializeField] Vector2 attackPause = new(0.6f, 1.4f);
        [SerializeField, Range(0f, 1f)] float comboChance = 0.35f;

        [Header("Pistola (atirador)")]
        [Tooltip("Distância (m) que o atirador tenta manter do jogador.")]
        [SerializeField] float preferredRange = 10f;
        [Tooltip("Mais perto que isto, recua mirando.")]
        [SerializeField] float tooClose = 5f;
        [Tooltip("Alcance (m) para começar a mirar.")]
        [SerializeField] float shootRange = 20f;
        [Tooltip("Segundos segurando a mira antes de atirar (mínimo, máximo): o tempo de o jogador sair da frente.")]
        [SerializeField] Vector2 aimTime = new(0.8f, 1.3f);
        [Tooltip("Rapidez com que a mira acompanha o jogador. Menor = mais atraso (mais fácil desviar correndo de lado).")]
        [SerializeField] float aimTracking = 3f;

        [Header("Movimento")]
        [Tooltip("Mais longe que isto do destino, corre.")]
        [SerializeField] float runBeyond = 4.5f;
        [SerializeField] float repathInterval = 0.3f;
        [Tooltip("Distância (m) em que se afasta dos outros piratas para não se amontoar.")]
        [SerializeField] float separation = 1.2f;

        FootCharacter character;
        CharacterCombat combat;
        Health health;
        FootHealthReactions reactions;
        Health targetHealth;
        Vector3 post;
        Quaternion postRotation;
        State state;
        float stateUntil, lastSeen = float.NegativeInfinity, nextAttack, comboAt;
        bool comboPending;
        Vector3 lastKnown;
        NavMeshPath path;
        Vector3[] corners = System.Array.Empty<Vector3>();
        int corner;
        float nextRepath;
        Vector3 pathGoal;
        readonly RaycastHit[] sightHits = new RaycastHit[16];
        float aimStart = float.NegativeInfinity, aimDuration;
        Vector3 trackedAim;

        public State Current => state;
        /// <summary>Viu o jogador e está reagindo, perseguindo ou lutando.</summary>
        public bool IsAlerted => state is State.Alert or State.Chase;
        public bool IsDead => health && health.IsDead;
        public Health Health => health;
        /// <summary>Momento em que percebeu o jogador (para o "!" sobre a cabeça).</summary>
        public float AlertedAt { get; private set; } = float.NegativeInfinity;
        /// <summary>Atirador (arma de fogo na mão): luta de longe.</summary>
        public bool IsGunner => combat && combat.Current && combat.Current.kind == WeaponKind.Firearm;

        public void Configure(Transform player, EnemyCamp enemyCamp)
        {
            target = player;
            camp = enemyCamp;
        }

        /// <summary>Distâncias de atirador de outra arma (o bacamarteiro chega bem mais perto que o de pistola e mira mais tempo).</summary>
        public void ConfigureRanged(float keepAt, float backOffInside, float startAimingAt, Vector2 aimSeconds, float tracking)
        {
            preferredRange = keepAt;
            tooClose = backOffInside;
            shootRange = startAimingAt;
            aimTime = aimSeconds;
            aimTracking = tracking;
        }

        void Awake()
        {
            character = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            health = GetComponent<Health>();
            reactions = GetComponent<FootHealthReactions>();
            if (target) targetHealth = target.GetComponent<Health>();
            post = transform.position;
            postRotation = transform.rotation;
            path = new NavMeshPath();
        }

        void OnEnable()
        {
            Active.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            Active.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (camp) camp.ReleaseAttackSlot(this);
        }

        /// <summary>Chamado pelo FootCharacter uma vez por frame: aqui a IA pensa.</summary>
        public FootCommand GetCommand()
        {
            var command = new FootCommand();
            if (!enabled || health.IsDead) return command;
            switch (state)
            {
                case State.Guard:
                    if (Notices()) BeginAlert();
                    else HoldPost(ref command);
                    break;
                case State.Alert:
                    LookAt(ref command, lastKnown);
                    if (Time.time >= stateUntil) BeginChase();
                    break;
                case State.Chase:
                    Chase(ref command);
                    break;
                case State.Return:
                    if (Notices()) BeginAlert();
                    else if (!HoldPost(ref command)) state = State.Guard;
                    break;
            }
            return command;
        }

        /// <summary>Outro pirata do acampamento deu o alarme.</summary>
        public void HearAlarm()
        {
            if (health.IsDead || IsAlerted || !TargetAvailable) return;
            lastKnown = target.position;
            BeginAlert();
        }

        /// <summary>Levanta no posto com a vida cheia (sandbox, acampamento resetado).</summary>
        public void ResetToPost()
        {
            if (reactions) reactions.Revive();
            character.Teleport(post, postRotation);
            state = State.Guard;
            corners = System.Array.Empty<Vector3>();
            lastSeen = AlertedAt = aimStart = float.NegativeInfinity;
            if (camp) camp.ReleaseAttackSlot(this);
        }

        bool TargetAvailable => target && target.gameObject.activeInHierarchy && (!targetHealth || !targetHealth.IsDead);

        void OnDamaged(DamageInfo info)
        {
            if (health.IsDead || !TargetAvailable) return;
            // Golpe de quem não estava vendo: sabe de onde veio e reage na hora; o acampamento todo vem junto e persegue mais longe.
            lastKnown = target.position;
            lastSeen = Time.time;
            if (!IsAlerted)
            {
                AlertedAt = Time.time;
                BeginChase();
            }
            if (camp) camp.Provoke(this);
        }

        void OnDied()
        {
            state = State.Dead;
            if (camp) camp.ReleaseAttackSlot(this);
        }

        void BeginAlert()
        {
            state = State.Alert;
            AlertedAt = Time.time;
            stateUntil = Time.time + Random.Range(reactionTime.x, reactionTime.y);
        }

        void BeginChase()
        {
            state = State.Chase;
            lastSeen = Time.time;
            nextAttack = Time.time + Random.Range(0.1f, 0.5f);
            if (camp) camp.RaiseAlarm(this);
        }

        bool Notices()
        {
            if (!CanSee()) return false;
            lastKnown = target.position;
            return true;
        }

        void Chase(ref FootCommand command)
        {
            if (CanSee())
            {
                lastSeen = Time.time;
                lastKnown = target.position;
            }
            // Provocado (levou tiro há pouco): sabe onde o jogador está mesmo sem ver (corre até ele; o atirador ainda precisa ver
            // para atirar) e não desiste por perder de vista.
            bool provoked = camp && camp.IsProvoked && TargetAvailable;
            if (provoked) lastKnown = target.position;
            bool tooFar = camp && Flat(target ? target.position - camp.Center : Vector3.zero).magnitude > camp.EffectiveLeash;
            if (!TargetAvailable || tooFar || (!provoked && Time.time - lastSeen > loseTargetAfter))
            {
                state = State.Return;
                if (camp) camp.ReleaseAttackSlot(this);
                return;
            }

            if (IsGunner)
            {
                Shoot(ref command);
                return;
            }

            float distance = Flat(lastKnown - transform.position).magnitude;
            command.hasAim = true;
            command.aimPoint = lastKnown;
            // Só disputa a vez de atacar quando já está perto.
            bool myTurn = distance > waitRange + 2f ? false : !camp || camp.RequestAttackSlot(this);
            if (distance > waitRange + 4f && camp) camp.ReleaseAttackSlot(this);

            if (myTurn && distance <= attackRange)
            {
                command.faceAimPoint = true;
                TryAttack(ref command);
            }
            else if (!myTurn && distance <= waitRange)
            {
                // Esperando a vez: encara; se colou demais, recua um pouco.
                command.faceAimPoint = true;
                if (distance < waitRange - 1f) Move(ref command, -Flat(lastKnown - transform.position).normalized, false);
            }
            else
            {
                Vector3 goal = lastKnown;
                Move(ref command, Steer(goal), distance > runBeyond);
                // Chegando para atacar: já encara (o golpe sai virado).
                command.faceAimPoint = myTurn && distance <= attackRange + 1.2f;
            }
        }

        /// <summary>
        /// Atirador: chega a um alcance de tiro com vista livre, segura a mira (linha vermelha) por aimTime e atira.
        /// A mira persegue o jogador com atraso; recarregando, volta à distância preferida.
        /// </summary>
        void Shoot(ref FootCommand command)
        {
            Vector3 to = Flat(lastKnown - transform.position);
            float distance = to.magnitude;
            bool sees = Time.time - lastSeen < 0.2f;
            trackedAim = Vector3.Lerp(trackedAim, lastKnown, 1f - Mathf.Exp(-aimTracking * Time.deltaTime));
            command.hasAim = true;
            command.aimPoint = trackedAim;

            if (!sees || distance > shootRange)
            {
                aimStart = float.NegativeInfinity;
                command.aimPoint = lastKnown;
                Move(ref command, Steer(lastKnown), distance > runBeyond);
                return;
            }

            command.faceAimPoint = true;
            Vector3 away = distance > 0.01f ? -to / distance : -transform.forward;
            if (combat.IsReloading)
            {
                // Recarregando (pistola girando): afasta até a distância preferida; longe demais, chega mais perto.
                aimStart = float.NegativeInfinity;
                if (distance < preferredRange) Move(ref command, away, false);
                else if (distance > preferredRange + 5f) Move(ref command, Steer(lastKnown), false);
                return;
            }

            command.aim = true;
            if (aimStart < 0f)
            {
                // Começa a mirar onde o jogador está agora; daí a mira o segue com atraso.
                aimStart = Time.time;
                aimDuration = Random.Range(aimTime.x, aimTime.y);
                trackedAim = lastKnown;
            }
            if (distance < tooClose) Move(ref command, away, false);
            if (Time.time - aimStart >= aimDuration && combat.IsAiming && (!camp || camp.TryStrike()))
            {
                command.attack = true;
                aimStart = float.NegativeInfinity;
            }
        }

        void TryAttack(ref FootCommand command)
        {
            var weapon = combat.Current;
            if (!weapon || combat.IsSwapping) return;
            if (comboPending && Time.time >= comboAt)
            {
                command.attack = true;
                comboPending = false;
                return;
            }
            if (Time.time < nextAttack || combat.IsAttacking) return;
            if (camp && !camp.TryStrike()) return;
            command.attack = true;
            // Às vezes emenda o 2º golpe (clicando de novo um pouco antes de o 1º terminar).
            comboPending = Random.value < comboChance;
            comboAt = Time.time + Mathf.Max(0.1f, weapon.cooldown - 0.2f);
            nextAttack = Time.time + weapon.cooldown * (comboPending ? 2f : 1f) + Random.Range(attackPause.x, attackPause.y);
        }

        /// <summary>Volta para o posto e fica de guarda virado para onde estava. false = já está no posto.</summary>
        bool HoldPost(ref FootCommand command)
        {
            Vector3 to = Flat(post - transform.position);
            if (to.magnitude > 0.6f)
            {
                Move(ref command, Steer(post), to.magnitude > runBeyond * 2f);
                return true;
            }
            LookAt(ref command, transform.position + postRotation * Vector3.forward * 5f);
            return false;
        }

        static void LookAt(ref FootCommand command, Vector3 point)
        {
            command.hasAim = true;
            command.aimPoint = point;
            command.faceAimPoint = true;
        }

        void Move(ref FootCommand command, Vector3 direction, bool run)
        {
            direction += Separation();
            direction = Vector3.ClampMagnitude(direction, 1f);
            command.move = new Vector2(direction.x, direction.z);
            command.run = run;
        }

        /// <summary>Direção para o próximo canto do caminho no NavMesh (sem NavMesh, vai reto).</summary>
        Vector3 Steer(Vector3 goal)
        {
            if (Time.time >= nextRepath || (goal - pathGoal).sqrMagnitude > 1f) Repath(goal);
            while (corner < corners.Length && Flat(corners[corner] - transform.position).magnitude < 0.5f) corner++;
            Vector3 next = corner < corners.Length ? corners[corner] : goal;
            Vector3 dir = Flat(next - transform.position);
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
        }

        void Repath(Vector3 goal)
        {
            nextRepath = Time.time + repathInterval;
            pathGoal = goal;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit from, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(goal, out NavMeshHit to, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                && path.status != NavMeshPathStatus.PathInvalid)
            {
                corners = path.corners;
                corner = corners.Length > 1 ? 1 : 0;
            }
            else
            {
                corners = System.Array.Empty<Vector3>();
                corner = 0;
            }
        }

        /// <summary>Empurrão para longe dos outros piratas vivos muito perto.</summary>
        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var other in Active)
            {
                if (other == this || other.IsDead) continue;
                Vector3 away = Flat(transform.position - other.transform.position);
                float d = away.magnitude;
                if (d < 0.001f || d > separation) continue;
                push += away / d * (1f - d / separation);
            }
            return push * 0.8f;
        }

        /// <summary>Vê o jogador: dentro do alcance, do ângulo de visão (ou bem perto) e sem nada no meio.</summary>
        bool CanSee()
        {
            if (Distracted || !TargetAvailable) return false;
            Vector3 to = target.position - transform.position;
            float distance = Flat(to).magnitude;
            float range = sightRange;
            if (Services.TryGet(out ITimeOfDay time)) range *= Mathf.Lerp(1f, nightSight, time.Night);
            if (distance > Mathf.Max(range, hearRange)) return false;
            if (distance > hearRange && Vector3.Angle(transform.forward, Flat(to)) > sightAngle * 0.5f) return false;

            // Linha de visão do olho ao peito: personagens (colisor do CharacterController) não tapam a vista.
            Vector3 eye = transform.position + Vector3.up * 1.6f, chest = target.position + Vector3.up * 1.2f;
            Vector3 ray = chest - eye;
            int count = Physics.RaycastNonAlloc(eye, ray.normalized, sightHits, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = sightHits[i].collider;
                if (col is CharacterController || col.transform.IsChildOf(transform) || col.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
