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
    /// Duelista (adaga): rodeia, entra para estocar e sai, ergue a guarda, apara e rola para longe dos golpes que vê (Duel).
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
        [Tooltip("Inimigo com nome (alvo de missão): ao cair publica EnemyDefeatedEvent; missão aceita com este id o levanta de novo.")]
        [SerializeField] string bountyId;

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
        [Tooltip("Segundos de provocação (grito de guerra) ao perceber o jogador, parado; 0 = não provoca.")]
        [SerializeField] float tauntSeconds;
        [Tooltip("Golpes emendados no máximo depois do primeiro (cada um com a chance do combo).")]
        [SerializeField, Min(1)] int maxCombo = 1;
        [Tooltip("Investida: com o jogador até esta distância (m, além do alcance do golpe), o golpe sai avançando. 0 = não avança.")]
        [SerializeField] float lungeRange;
        [Tooltip("Velocidade máxima (m/s) do avanço: durante o preparo de cada golpe vai atrás do jogador (pega quem rolou cedo demais).")]
        [SerializeField] float lungeSpeed = 7f;

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

        [Header("Duelista (adaga)")]
        [Tooltip("Luta como duelista: rodeia o jogador, entra para golpear e sai, ergue a guarda, apara e rola para longe do golpe.")]
        [SerializeField] bool duelist;
        [Tooltip("Golpe do jogador à frente, até reactRange: chance de aparar (ergue a guarda logo antes do impacto) e de rolar para longe.")]
        [SerializeField, Range(0f, 1f)] float parryChance = 0.3f, dodgeChance = 0.3f;
        [SerializeField] float reactRange = 3.2f;
        [Tooltip("Segundos entre um rolamento e outro (não rola de todo golpe).")]
        [SerializeField] float duelDodgeCooldown = 2.5f;
        [Tooltip("Esperando a vez: distância (m) que mantém, rodeando.")]
        [SerializeField] float duelRange = 3.2f;
        [Tooltip("Esperando a vez: chance de segurar a guarda erguida, e por quantos segundos (mínimo, máximo).")]
        [SerializeField, Range(0f, 1f)] float guardWhileWaiting = 0.45f;
        [SerializeField] Vector2 guardSeconds = new(0.8f, 1.6f);
        [Tooltip("Pressionada (o jogador golpeou há pouco e está perto): chance de erguer a guarda e segurar (o golpe vira arranhão; o " +
                 "chute e o tiro passam), testada a cada meio segundo. Ao baixar, contra-ataca.")]
        [SerializeField, Range(0f, 1f)] float pressGuardChance = 0.6f;
        [Tooltip("Jogador aparado ou caído: segundos em que ataca sem esperar a vez.")]
        [SerializeField] float counterSeconds = 1.2f;

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
        int comboCount;
        float lungeUntil = float.NegativeInfinity, currentLungeSpeed;
        LandBoss boss;
        Vector3 lastKnown;
        NavMeshPath path;
        Vector3[] corners = System.Array.Empty<Vector3>();
        int corner;
        float nextRepath;
        Vector3 pathGoal;
        readonly RaycastHit[] sightHits = new RaycastHit[16];
        float aimStart = float.NegativeInfinity, aimDuration;
        Vector3 trackedAim;
        bool taunted;
        CharacterAnimatorDriver driver;
        // Duelista: reação aos golpes do jogador, guarda, rolamento e o lado para onde rodeia.
        CharacterCombat targetCombat;
        FootCharacter targetCharacter;
        float guardFrom = float.NegativeInfinity, guardUntil = float.NegativeInfinity, dodgeAt = float.PositiveInfinity;
        float nextDuelDodge, counterUntil = float.NegativeInfinity, nextCircleFlip, nextGuardRoll, lastTargetAttack = float.NegativeInfinity;
        bool riposte;
        float circleSign = 1f;
        Vector3 dodgeDirection;
        bool waiting;
        NavMeshHit navHit;

        /// <summary>Luta como duelista (rodeia, entra e sai, guarda, apara, rola). Ver o cabeçalho "Duelista".</summary>
        public bool IsDuelist => duelist;
        /// <summary>Duelista: está com a guarda erguida agora (testes).</summary>
        public bool DuelGuarding => duelist && Time.time >= guardFrom && Time.time < guardUntil;

        /// <summary>Começou a provocar (som).</summary>
        public event System.Action Taunted;
        /// <summary>Saiu da cova (esqueleto amaldiçoado): gemido.</summary>
        public event System.Action Risen;
        float riseUntil = float.NegativeInfinity;
        bool riseInvulnerable;

        public State Current => state;
        /// <summary>Quem a IA persegue (o capitão).</summary>
        public Transform Target => target;
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

        /// <summary>Alvo de missão "derrote o X" (vazio = pirata comum).</summary>
        public string BountyId => bountyId;
        public void ConfigureBounty(string id) => bountyId = id;

        /// <summary>Luta corpo a corpo de outro jeito (brutamontes: alcance maior, pausa maior entre golpes, combo mais raro).</summary>
        public void ConfigureMelee(float range, float wait, Vector2 pause, float combo)
        {
            attackRange = range;
            waitRange = wait;
            attackPause = pause;
            comboChance = combo;
        }

        /// <summary>Investida (o golpe sai avançando com o jogador até <paramref name="range"/> m) e até quantos golpes emenda depois do primeiro.</summary>
        public void ConfigureLunge(float range, float speed, int combo)
        {
            lungeRange = range;
            lungeSpeed = speed;
            maxCombo = Mathf.Max(1, combo);
        }

        /// <summary>Provoca (grito de guerra, parado por <paramref name="seconds"/>) ao perceber o jogador, uma vez por perseguição.</summary>
        public void ConfigureTaunt(float seconds) => tauntSeconds = seconds;

        /// <summary>Pausa entre golpes, chance de combo e golpes emendados (o chefe muda na fase 2).</summary>
        public void SetTempo(Vector2 pause, float combo, int combos)
        {
            attackPause = pause;
            comboChance = combo;
            maxCombo = Mathf.Max(1, combos);
        }

        /// <summary>Urro sem a animação de provocar (o chefe se enfurecendo): só o som.</summary>
        public void Roar() => Taunted?.Invoke();

        /// <summary>Duelista (adaga): chance de aparar e de rolar ao ver o golpe do jogador, e distância que mantém rodeando.</summary>
        public void ConfigureDuelist(float parry, float dodge, float keepAt, float guardChance, float pressedGuard)
        {
            duelist = true;
            pressGuardChance = pressedGuard;
            parryChance = parry;
            dodgeChance = dodge;
            duelRange = keepAt;
            guardWhileWaiting = guardChance;
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
            boss = GetComponent<LandBoss>();
            if (target)
            {
                targetHealth = target.GetComponent<Health>();
                targetCombat = target.GetComponent<CharacterCombat>();
                targetCharacter = target.GetComponent<FootCharacter>();
            }
            post = transform.position;
            postRotation = transform.rotation;
            path = new NavMeshPath();
        }

        void OnEnable()
        {
            Active.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            combat.Attacked += OnOwnAttacked;
            if (!string.IsNullOrEmpty(bountyId)) EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
            if ((duelist || lungeRange > 0f) && targetCharacter) targetCharacter.Dodged += OnTargetDodged;
            if (duelist && targetCombat)
            {
                targetCombat.Attacked += OnTargetAttacked;
                targetCombat.Kicked += OnTargetKicked;
                targetCombat.Parried += OnTargetParried;
                combat.Blocked += OnOwnBlocked;
            }
        }

        void OnDisable()
        {
            Active.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (targetCombat)
            {
                targetCombat.Attacked -= OnTargetAttacked;
                targetCombat.Kicked -= OnTargetKicked;
                targetCombat.Parried -= OnTargetParried;
            }
            combat.Blocked -= OnOwnBlocked;
            combat.Attacked -= OnOwnAttacked;
            if (!string.IsNullOrEmpty(bountyId)) EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);
            if (targetCharacter) targetCharacter.Dodged -= OnTargetDodged;
            if (camp) camp.ReleaseAttackSlot(this);
        }

        /// <summary>Chamado pelo FootCharacter uma vez por frame: aqui a IA pensa.</summary>
        public FootCommand GetCommand()
        {
            var command = new FootCommand();
            if (!enabled || health.IsDead) return command;
            // Saindo da cova: parado (e sem levar golpe, ver EndRiseInvulnerable) até a animação terminar.
            if (Time.time < riseUntil) return command;
            // Chefe se enfurecendo (fase 2): parado, encarando.
            if (boss && boss.IsPoweringUp)
            {
                if (TargetAvailable) LookAt(ref command, target.position);
                return command;
            }
            Pursue();
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

        /// <summary>
        /// Esqueleto amaldiçoado (CursedGraveyard): sai da cova na animação <paramref name="variant"/> (começando deitado em
        /// <paramref name="startSeconds"/> do clipe), parado por <paramref name="seconds"/>, e já parte para cima do jogador (sem grito).
        /// </summary>
        public void Rise(int variant, float startSeconds, float seconds)
        {
            if (health.IsDead) return;
            if (!driver) driver = GetComponentInChildren<CharacterAnimatorDriver>();
            if (driver) driver.PlayRise(variant, startSeconds);
            riseUntil = Time.time + seconds;
            // Saindo da terra não leva golpe: senão, quem levanta de novo era morto ainda deitado (a segunda vida não contava).
            riseInvulnerable = true;
            health.Invulnerable = true;
            // Por tempo (não pelo GetCommand): com o acampamento dormindo longe da câmera a IA não pensa e ficava invencível.
            StartCoroutine(EndRiseInvulnerable(seconds));
            taunted = true;
            if (TargetAvailable)
            {
                lastKnown = target.position;
                lastSeen = Time.time;
                AlertedAt = Time.time;
                BeginChase();
            }
            Risen?.Invoke();
        }

        System.Collections.IEnumerator EndRiseInvulnerable(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!riseInvulnerable) yield break;
            riseInvulnerable = false;
            health.Invulnerable = false;
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
            guardFrom = guardUntil = counterUntil = lastTargetAttack = riseUntil = float.NegativeInfinity;
            if (riseInvulnerable)
            {
                riseInvulnerable = false;
                health.Invulnerable = false;
            }
            riposte = false;
            dodgeAt = float.PositiveInfinity;
            waiting = false;
            taunted = false;
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
            if (!string.IsNullOrEmpty(bountyId)) EventBus.Publish(new EnemyDefeatedEvent { targetId = bountyId });
        }

        /// <summary>Missão nova contra este inimigo: se já tinha caído, volta para o posto (como o acampamento se repovoa).</summary>
        void OnMissionStarted(MissionStartedEvent evt)
        {
            if (evt.targetId == bountyId && IsDead) ResetToPost();
        }

        void BeginAlert()
        {
            state = State.Alert;
            AlertedAt = Time.time;
            stateUntil = Time.time + Random.Range(reactionTime.x, reactionTime.y);
            if (tauntSeconds > 0f && !taunted)
            {
                // Grito de guerra: fica parado encarando enquanto ele dura (o acampamento já foi avisado pelo "!").
                taunted = true;
                stateUntil += tauntSeconds;
                if (!driver) driver = GetComponentInChildren<CharacterAnimatorDriver>();
                if (driver) driver.PlayTaunt();
                Taunted?.Invoke();
            }
        }

        void BeginChase()
        {
            state = State.Chase;
            lastSeen = Time.time;
            nextAttack = Time.time + Random.Range(0.1f, 0.5f);
            dodgeAt = float.PositiveInfinity;
            waiting = false;
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
                taunted = false;
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
            if (boss)
            {
                BossFight(ref command, distance, myTurn);
                return;
            }
            if (duelist)
            {
                Duel(ref command, distance, myTurn);
                return;
            }

            // Jogador acabou de rolar (investida): ataca quando o rolamento perde a invencibilidade, sem esperar a pausa.
            bool chaseRoll = Time.time < counterUntil && targetCharacter && !targetCharacter.DodgeInvulnerable;
            if (myTurn && (distance <= attackRange || (CanLunge(distance) && (Time.time >= nextAttack || chaseRoll))))
            {
                command.faceAimPoint = true;
                TryAttack(ref command, chaseRoll, distance);
                if (!command.attack && distance > attackRange) Move(ref command, Steer(lastKnown), false);
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

        // ---------- Duelista ----------

        /// <summary>
        /// Duelista: longe, corre até perto; esperando (a vez do acampamento ou a próxima estocada), mantém duelRange rodeando o
        /// jogador, às vezes com a guarda erguida; na vez, entra e estoca (muitas vezes duas), e volta a rodear andando de ré. Golpe do
        /// jogador visto de frente: às vezes apara (guarda logo antes do impacto), às vezes rola para longe (OnTargetAttacked). Jogador
        /// aparado ou caído: ataca na hora.
        /// </summary>
        void Duel(ref FootCommand command, float distance, bool myTurn)
        {
            Vector3 to = Flat(lastKnown - transform.position);
            Vector3 toward = distance > 0.01f ? to / distance : transform.forward;
            command.hasAim = true;
            command.aimPoint = lastKnown;
            if (distance > duelRange + 3f)
            {
                waiting = false;
                Move(ref command, Steer(lastKnown), distance > runBeyond);
                return;
            }
            command.faceAimPoint = true;

            if (Time.time >= dodgeAt)
            {
                // Rolamento marcado pela reação (o FootCharacter rola na direção do move).
                dodgeAt = float.PositiveInfinity;
                command.dodge = true;
                command.move = new Vector2(dodgeDirection.x, dodgeDirection.z);
                guardUntil = float.NegativeInfinity;
                return;
            }
            if (Time.time >= nextCircleFlip)
            {
                circleSign = Random.value < 0.5f ? -1f : 1f;
                nextCircleFlip = Time.time + Random.Range(1.5f, 3.5f);
            }
            Vector3 around = Vector3.Cross(Vector3.up, toward) * circleSign;
            // Pressionada: o jogador golpeou há pouco, de perto. Ergue a guarda (e contra-ataca ao baixar).
            if (!DuelGuarding && Time.time >= nextGuardRoll && Time.time - lastTargetAttack < 1.2f && distance < reactRange
                && !combat.IsAttacking && !comboPending && combat.ParryReady)
            {
                nextGuardRoll = Time.time + 0.5f;
                if (Random.value < pressGuardChance)
                {
                    guardFrom = Time.time;
                    guardUntil = guardFrom + Random.Range(guardSeconds.x, guardSeconds.y);
                    riposte = true;
                }
            }
            if (DuelGuarding)
            {
                // Guarda erguida (adaga na mão + mirar): anda devagar de lado, de frente para o jogador.
                command.aim = true;
                Move(ref command, around * 0.6f, false);
                return;
            }
            if (riposte && Time.time >= guardUntil)
            {
                // Baixou a guarda: contra-ataque na hora (sem esperar a vez).
                riposte = false;
                counterUntil = Mathf.Max(counterUntil, Time.time + 0.6f);
            }

            bool counter = Time.time < counterUntil || (targetCharacter && targetCharacter.IsDown);
            bool striking = combat.IsAttacking || comboPending;
            if (striking || ((myTurn || counter) && (counter || Time.time >= nextAttack)))
            {
                waiting = false;
                // Jogador rolando (sem levar golpe): corre atrás e estoca quando ele terminar.
                bool rolling = targetCharacter && targetCharacter.DodgeInvulnerable;
                if (!rolling && (distance <= attackRange || CanLunge(distance))) TryAttack(ref command, counter, distance);
                if (!command.attack && distance > attackRange) Move(ref command, Steer(lastKnown), distance > attackRange + 1.5f);
                return;
            }

            // Esperando: mantém a distância rodeando; ao começar a esperar, às vezes ergue a guarda por um tempo.
            if (!waiting)
            {
                waiting = true;
                if (Random.value < guardWhileWaiting)
                {
                    guardFrom = Time.time + Random.Range(0.3f, 0.8f);
                    guardUntil = guardFrom + Random.Range(guardSeconds.x, guardSeconds.y);
                }
            }
            float off = distance - duelRange;
            Vector3 radial = off < -0.4f ? -toward : off > 0.4f ? toward * 0.7f : Vector3.zero;
            Move(ref command, radial + around * 0.6f, false);
        }

        /// <summary>O jogador começou um golpe (ou tiro sem mirar): de frente e perto, o duelista apara ou rola, às vezes.</summary>
        void OnTargetAttacked(WeaponDefinition weapon, int variant)
        {
            if (!ReadyToReact(weapon && weapon.kind == WeaponKind.Melee ? reactRange : 12f, out Vector3 away)) return;
            lastTargetAttack = Time.time;
            if (DuelGuarding) return;
            float roll = Random.value;
            if (weapon && weapon.kind == WeaponKind.Melee && roll < parryChance && combat.ParryReady && !combat.IsAttacking)
            {
                // Ergue a guarda um pouco antes do impacto (dentro da janela de aparar) e segura um instante.
                guardFrom = Time.time + Mathf.Max(0f, weapon.HitDelayFor(variant) - 0.18f);
                guardUntil = guardFrom + 0.5f;
                return;
            }
            if (roll < parryChance + dodgeChance) PlanDodge(away);
        }

        /// <summary>Chute do jogador (passa pela guarda): perto, rola às vezes.</summary>
        void OnTargetKicked()
        {
            if (ReadyToReact(2.6f, out Vector3 away) && Random.value < dodgeChance * 1.5f) PlanDodge(away);
        }

        /// <summary>
        /// Segurou um golpe na guarda erguida: baixa e responde na hora. A estocada (~0,3 s) chega antes do próximo golpe de
        /// quem só clica (o cutelo espera 0,6 s); quem erguer a guarda a tempo apara a resposta.
        /// </summary>
        void OnOwnBlocked(Vector3 point, bool parried)
        {
            if (parried || !DuelGuarding) return;
            riposte = false;
            guardUntil = Time.time;
            counterUntil = Mathf.Max(counterUntil, Time.time + 0.6f);
        }

        /// <summary>O jogador rolou perto: o duelista (e quem tem investida) vai atrás e ataca quando o rolamento acaba (rolar sem parar gasta o fôlego dele).</summary>
        void OnTargetDodged()
        {
            if (state != State.Chase || health.IsDead || !TargetAvailable || Flat(target.position - transform.position).magnitude > 8f) return;
            counterUntil = Mathf.Max(counterUntil, Time.time + targetCharacter.DodgeSeconds + 0.9f);
            guardUntil = float.NegativeInfinity;
            riposte = false;
        }

        // ---------- Chefe ----------

        /// <summary>
        /// Chefe (LandBoss): o golpe é escolhido por ele (cortes, giro, chute, salto), sem esperar a vez do acampamento. Longe, vem
        /// andando (corre de muito longe); perto, rodeia devagar encarando até o próximo golpe. Depois de cada golpe fica parado a brecha
        /// dele (recovery). Capitão rolou perto: ataca quando o rolamento acaba, se o próximo golpe já estava quase pronto.
        /// </summary>
        void BossFight(ref FootCommand command, float distance, bool myTurn)
        {
            command.hasAim = true;
            command.aimPoint = lastKnown;
            command.faceAimPoint = distance < 14f;
            if (combat.IsAttacking) return;
            var weapon = combat.Current;
            if (!weapon) return;
            if (comboPending)
            {
                if (Time.time >= comboAt && combat.ReadyToAttack)
                {
                    combat.QueueVariant(boss.ComboVariant());
                    TryAttack(ref command, true, distance);
                }
                return;
            }
            bool chaseRoll = Time.time < counterUntil && targetCharacter && !targetCharacter.DodgeInvulnerable && Time.time >= nextAttack - 0.5f;
            if ((Time.time >= nextAttack || chaseRoll) && TargetAvailable && combat.ReadyToAttack)
            {
                var move = boss.Choose(distance, target, targetCombat && targetCombat.IsBlocking);
                if (move != null)
                {
                    combat.QueueVariant(move.variant);
                    TryAttack(ref command, true, distance);
                    if (command.attack)
                    {
                        boss.Used(move);
                        if (!move.combo) comboPending = false;
                        counterUntil = float.NegativeInfinity;
                        nextAttack = Time.time + weapon.LastHitFor(move.variant) + move.recovery * boss.RecoveryScale
                                     + Random.Range(attackPause.x, attackPause.y) + (comboPending ? weapon.cooldown : 0f);
                        return;
                    }
                }
            }
            if (distance > attackRange)
            {
                Move(ref command, Steer(lastKnown), distance > runBeyond);
                return;
            }
            // Perto, esperando o próximo golpe: rodeia devagar.
            if (Time.time >= nextCircleFlip)
            {
                circleSign = Random.value < 0.5f ? -1f : 1f;
                nextCircleFlip = Time.time + Random.Range(1.5f, 3f);
            }
            Vector3 toward = distance > 0.01f ? Flat(lastKnown - transform.position) / distance : transform.forward;
            Move(ref command, Vector3.Cross(Vector3.up, toward) * circleSign * 0.35f, false);
        }

        bool CanLunge(float distance) => lungeRange > 0f && distance > attackRange && distance <= lungeRange && !combat.IsAttacking && !comboPending;

        /// <summary>O golpe começou: com investida, avança atrás do jogador até o impacto (Pursue).</summary>
        void OnOwnAttacked(WeaponDefinition weapon, int variant)
        {
            if (lungeRange <= 0f || !weapon || weapon.kind != WeaponKind.Melee) return;
            // Sequência (chefe): avança até o último acerto. Golpe com investida própria (salto) vai mais rápido.
            lungeUntil = Time.time + weapon.LastHitFor(variant);
            var strike = weapon.StrikeFor(variant);
            currentLungeSpeed = strike != null && strike.lungeSpeed > 0f ? strike.lungeSpeed : lungeSpeed;
        }

        /// <summary>
        /// Preparo do golpe com investida: vai atrás do jogador (para onde ele está agora) para chegar no alcance a tempo do impacto. Rolar
        /// cedo não basta: só o rolamento na hora do impacto (invencível) ou sair de lado escapa. Golpe interrompido (aparado, levou golpe) para.
        /// </summary>
        void Pursue()
        {
            if (Time.time >= lungeUntil) return;
            if (!combat.IsAttacking || !TargetAvailable || state != State.Chase)
            {
                lungeUntil = float.NegativeInfinity;
                return;
            }
            Vector3 to = Flat(target.position - transform.position);
            float gap = to.magnitude - attackRange * 0.6f;
            if (gap <= 0f) return;
            float left = Mathf.Max(0.05f, lungeUntil - Time.time);
            character.Lunge(to / to.magnitude * Mathf.Min(currentLungeSpeed > 0f ? currentLungeSpeed : lungeSpeed, gap / left * 1.2f));
        }

        /// <summary>O duelista aparou o jogador: contra-ataca na hora.</summary>
        void OnTargetParried()
        {
            if (state == State.Chase && Flat(target.position - transform.position).magnitude < reactRange + 1f)
                counterUntil = Time.time + counterSeconds;
        }

        bool ReadyToReact(float range, out Vector3 away)
        {
            away = Vector3.zero;
            if (state != State.Chase || health.IsDead || character.IsDown || !TargetAvailable) return false;
            Vector3 from = Flat(target.position - transform.position);
            if (from.magnitude > range) return false;
            // Só o que vem de frente para ele (o jogador virado para o duelista).
            if (Vector3.Angle(target.forward, -from) > 70f) return false;
            away = from.sqrMagnitude > 0.0001f ? -from.normalized : -transform.forward;
            return true;
        }

        /// <summary>Rola para trás e para um lado (o que tiver chão no NavMesh), depois de um tempo de reação.</summary>
        void PlanDodge(Vector3 away)
        {
            if (Time.time < nextDuelDodge || combat.IsAttacking) return;
            float side = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < 3; i++)
            {
                float angle = i == 0 ? side * Random.Range(35f, 65f) : i == 1 ? -side * Random.Range(35f, 65f) : 0f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                if (NavMesh.Raycast(transform.position, transform.position + dir * 4.5f, out navHit, NavMesh.AllAreas)) continue;
                dodgeDirection = dir;
                dodgeAt = Time.time + Random.Range(0.06f, 0.14f);
                nextDuelDodge = Time.time + duelDodgeCooldown;
                guardUntil = float.NegativeInfinity;
                return;
            }
        }

        void TryAttack(ref FootCommand command, bool force = false, float distance = 0f)
        {
            var weapon = combat.Current;
            if (!weapon || combat.IsSwapping) return;
            // Duelista pressionada ou contra-atacando: só a estocada rápida (a lenta seria interrompida pelo golpe do jogador).
            if (duelist && (force || Time.time - lastTargetAttack < 1.2f)) combat.QueueVariant(0);
            if (comboPending && Time.time >= comboAt)
            {
                command.attack = true;
                comboPending = false;
                // Duelista: pode emendar mais um (até maxCombo).
                if (++comboCount < maxCombo && Random.value < comboChance)
                {
                    comboPending = true;
                    comboAt = Time.time + Mathf.Max(0.1f, weapon.cooldown - 0.2f);
                    nextAttack += weapon.cooldown;
                }
                return;
            }
            if ((!force && Time.time < nextAttack) || combat.IsAttacking) return;
            if (!force && camp && !camp.TryStrike()) return;
            command.attack = true;
            comboCount = 0;
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
