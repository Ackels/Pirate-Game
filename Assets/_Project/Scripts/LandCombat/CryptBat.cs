using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Morcego das catacumbas (C5, passo 2, 20/10; o morcego do Animated Monsters do Quaternius, com olhos vermelhos acesos). Dormindo, o
    /// bando ronda alto no meio da sala. Acordado, ronda o capitão a 2,8–4,2 m e 2,4–3,2 m de altura; de tempos em tempos um deles (a vez do
    /// <see cref="CryptEncounter"/>) guincha, para no ar (<see cref="windUpSeconds"/>) e ataca; depois sobe de volta. Lá em cima o cutelo não
    /// alcança (só tiro); no ataque, sim. Levou golpe: sobe tonto. Morto, cai.
    /// Três ataques sorteados (18/11, pedido do Jhonata: "criarmos novos golpes"): <b>mergulho</b> (o de antes: mordida no peito, passa pela
    /// guarda), <b>rasante</b> (desce de lado, passa pelo peito do capitão de raspão e sai do outro lado: dá para cortar na passagem) e
    /// <b>agarrar</b> (mergulha, gruda no peito e morde algumas vezes com o capitão andando devagar; golpe, chute ou rolamento derrubam;
    /// só um morcego agarrado por vez).
    /// </summary>
    public class CryptBat : CryptMonster
    {
        enum Phase { Roost, Circle, WindUp, Dive, Swoop, Latched, Climb, Stunned, Dead }
        enum Move { Dive, Swoop, Latch }

        [SerializeField] Collider hitBox;
        [Header("Combate")]
        [SerializeField] float damage = 6f;
        [SerializeField] Vector2 circleRadius = new(2.8f, 4.2f), circleHeight = new(2.4f, 3.2f);
        [Tooltip("windUpSeconds = tempo tremendo no ar antes de atacar (o aviso; era 0,35 s).")]
        [SerializeField] float circleSpeed = 4.5f, diveSpeed = 9f, climbSpeed = 6f, windUpSeconds = 0.6f, climbSeconds = 0.9f, reach = 0.9f;
        [Tooltip("Aparado: fica tonto à altura do peito do capitão (dá para bater) por estes segundos (22/10, revisão: o último morcego mordia sem parar e a guarda não resolvia).")]
        [SerializeField] float stunSeconds = 1.5f, stunHeight = 1.1f, stunFallSpeed = 6f;
        [Tooltip("Espera entre um ataque e outro (s).")]
        [SerializeField] Vector2 attackGap = new(1.4f, 3.2f);
        [Header("Rasante (18/11)")]
        [Tooltip("Dano do raspão, velocidade da passada, até onde passa depois do capitão (m) e a distância do peito que conta como raspão.")]
        [SerializeField] float swoopDamage = 5f, swoopSpeed = 8f, swoopOvershoot = 3.2f, swoopReach = 1.0f;
        [Header("Agarrar (18/11)")]
        [Tooltip("Dano ao grudar, de cada mordida, tempo até a primeira (dá para reagir), intervalo entre elas e quanto o capitão anda agarrado.")]
        [SerializeField] float latchDamage = 3f, biteDamage = 4f, firstBite = 1f, biteEvery = 0.75f, latchedSlow = 0.55f;
        [SerializeField] int bites = 3;
        [Tooltip("Onde gruda: à frente do capitão e a altura (m). Mais perto ele some dentro da cabeça grande do capitão.")]
        [SerializeField] Vector2 latchOffset = new(0.75f, 0.95f);
        [Tooltip("Peso no sorteio: mergulho, rasante, agarrar.")]
        [SerializeField] Vector3 moveWeights = new(0.4f, 0.35f, 0.25f);
        [Header("Dormindo")]
        [SerializeField] float roostRadius = 2.5f, roostHeight = 3.4f;

        Phase phase = Phase.Roost;
        float phaseStart, phaseUntil, nextAttack, angle, radius, height, spin = 1f, deadFloor;
        Vector3 velocity;
        Move move;
        Vector3 swoopVia, swoopTo;
        bool swoopHit, swoopOut;
        int bitesLeft;
        float nextBite;
        static CryptBat latched;

        public override bool IsGhost => false;
        public string PhaseName => phase.ToString();
        public string MoveName => move.ToString();
        public override float WarningHeight => 0.9f;
        /// <summary>Agarrado, a mordida passa pela guarda: aviso azul (bata nele, chute ou role).</summary>
        public override bool WarnUnblockable => phase == Phase.Latched;
        /// <summary>Até a mordida: o resto do tremor + a distância do ataque na velocidade dele.</summary>
        public override float ImpactIn => phase switch
        {
            Phase.WindUp when TargetAlive => Mathf.Max(0f, phaseUntil - Time.time)
                + (move == Move.Swoop ? Mathf.Max(0f, DiveDistance - swoopReach) / swoopSpeed : DiveDistance / diveSpeed),
            Phase.Dive when TargetAlive => Mathf.Max(0f, DiveDistance - reach) / diveSpeed,
            Phase.Swoop when TargetAlive && !swoopHit => SwoopTime(),
            Phase.Latched when TargetAlive => Mathf.Max(0f, nextBite - Time.time),
            _ => -1f,
        };

        float DiveDistance => (TargetChest - transform.position).magnitude;

        /// <summary>Segundos até o ponto da passada que encosta no peito (o rasante não mira no peito, passa por ele).</summary>
        float SwoopTime() => swoopOut ? 0f : Mathf.Max(0f, (swoopVia - transform.position).magnitude - swoopReach * 0.6f) / swoopSpeed;

        public void Configure(Collider box) => hitBox = box;

        protected override void Awake()
        {
            base.Awake();
            angle = Random.Range(0f, Mathf.PI * 2f);
            spin = Random.value < 0.5f ? -1f : 1f;
            radius = Random.Range(0.5f, 1f) * roostRadius;
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }

        protected override void OnWake()
        {
            Vector3 to = transform.position - target.transform.position;
            angle = Mathf.Atan2(to.z, to.x);
            radius = Random.Range(circleRadius.x, circleRadius.y);
            height = Random.Range(circleHeight.x, circleHeight.y);
            nextAttack = Time.time + Random.Range(0.6f, attackGap.y);
            Enter(Phase.Circle, 0f);
        }

        protected override void OnSleep()
        {
            Release();
            Enter(Phase.Roost, 0f);
        }

        protected override void OnReset()
        {
            gameObject.SetActive(true);
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            if (hitBox) hitBox.enabled = true;
            if (animator) animator.enabled = true;
            Play("Voar", 0f);
            Enter(Phase.Roost, 0f);
        }

        protected override void OnDeath()
        {
            Release();
            if (hitBox) hitBox.enabled = false;
            Play("Morrer", 0.05f);
            // Chão embaixo (o colisor dele já está desligado).
            deadFloor = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y + 0.15f : post.y - roostHeight;
            velocity = Vector3.zero;
            Enter(Phase.Dead, 0f);
        }

        protected override void OnDamaged(DamageInfo info)
        {
            base.OnDamaged(info);
            if (health.IsDead) return;
            Play("Levar", 0.05f);
            Vector3 away = transform.position - (info.source ? info.source.transform.position : transform.position - transform.forward);
            away.y = 0f;
            velocity = (away.sqrMagnitude > 0.0001f ? away.normalized * 3f : Vector3.zero) + Vector3.up * 3f;
            if (phase == Phase.Latched) ShakenOff(false);
            else if (phase is Phase.WindUp or Phase.Dive or Phase.Swoop) Climb();
        }

        void Enter(Phase next, float seconds)
        {
            phase = next;
            phaseStart = Time.time;
            phaseUntil = Time.time + seconds;
        }

        /// <summary>Aparado: tonto e baixo, a hora de bater (como a alma aparada); depois sobe e espera a próxima vez.</summary>
        void Stun()
        {
            Release();
            if (Encounter) Encounter.EndAttack(this);
            nextAttack = Time.time + Random.Range(attackGap.x, attackGap.y);
            velocity = Vector3.zero;
            Play("Levar", 0.05f);
            Enter(Phase.Stunned, stunSeconds);
        }

        /// <summary>Derrubado do peito (golpe, chute ou rolamento): fica tonto embaixo, como o aparado.</summary>
        void ShakenOff(bool knocked)
        {
            Vector3 away = transform.position - target.transform.position;
            away.y = 0f;
            Stun();
            if (knocked) velocity = (away.sqrMagnitude > 0.0001f ? away.normalized * 4f : -transform.forward * 4f) + Vector3.up * 1.5f;
        }

        void Release()
        {
            if (latched == this) latched = null;
            IgnoreBody(false);
            if (targetCombat) targetCombat.Kicked -= OnTargetKicked;
        }

        void OnTargetKicked()
        {
            if (phase == Phase.Latched) ShakenOff(true);
        }

        Move PickMove()
        {
            float d = Mathf.Max(0f, moveWeights.x), s = Mathf.Max(0f, moveWeights.y), l = latched ? 0f : Mathf.Max(0f, moveWeights.z);
            float r = Random.value * Mathf.Max(0.0001f, d + s + l);
            return r < d ? Move.Dive : r < d + s ? Move.Swoop : Move.Latch;
        }

        /// <summary>Passada do rasante: desce do lado em que está, passa rente ao peito e sai do outro lado do capitão, subindo.</summary>
        void PlanSwoop()
        {
            Vector3 chest = TargetChest;
            Vector3 side = transform.position - chest;
            side.y = 0f;
            if (side.sqrMagnitude < 0.01f) side = transform.forward;
            side.Normalize();
            // Desce até rente ao peito, um pouco de lado (raspão, não pancada de frente), e sai em linha reta do outro lado, subindo.
            Vector3 beside = Vector3.Cross(Vector3.up, side) * (Random.value < 0.5f ? -0.35f : 0.35f);
            swoopVia = chest + beside;
            Vector3 across = swoopVia - transform.position;
            across.y = 0f;
            across = across.sqrMagnitude > 0.0001f ? across.normalized : -side;
            swoopTo = swoopVia + across * swoopOvershoot + Vector3.up * 0.9f;
            swoopHit = swoopOut = false;
        }

        /// <summary>Grudou no peito: morde algumas vezes (a primeira só depois de um intervalo: dá tempo do aviso).</summary>
        void Latch()
        {
            latched = this;
            bitesLeft = Mathf.Max(1, bites);
            nextBite = Time.time + firstBite;
            velocity = Vector3.zero;
            if (targetCombat) targetCombat.Kicked += OnTargetKicked;
            IgnoreBody(true);
            Enter(Phase.Latched, 99f);
        }

        /// <summary>Grudado no peito, o colisor dele fica dentro do capitão: sem isso, o CharacterController tropeçava nele.</summary>
        void IgnoreBody(bool ignore)
        {
            if (!hitBox || !target) return;
            var body = target.GetComponent<CharacterController>();
            if (body) Physics.IgnoreCollision(hitBox, body, ignore);
        }

        void Climb()
        {
            Release();
            if (Encounter) Encounter.EndAttack(this);
            nextAttack = Time.time + Random.Range(attackGap.x, attackGap.y);
            Enter(Phase.Climb, climbSeconds);
            Play("Voar");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float now = Time.time;
            if (phase == Phase.Dead)
            {
                velocity += Vector3.down * 12f * dt;
                var p = transform.position + velocity * dt;
                if (p.y <= deadFloor)
                {
                    p.y = deadFloor;
                    velocity = Vector3.zero;
                }
                transform.position = p;
                return;
            }
            // Empurrão de levar golpe, freando.
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.position += velocity * dt;
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, 9f * dt);
            }
            if (phase != Phase.Roost && (!IsAwake || !TargetAlive))
            {
                Release();
                Enter(Phase.Roost, 0f);
                if (Encounter) Encounter.EndAttack(this);
            }
            switch (phase)
            {
                case Phase.Roost:
                    angle += spin * 0.9f * dt;
                    // O lugar dele (post) já fica no alto da sala.
                    FlyTo(post + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius), 3f, dt);
                    break;
                case Phase.Circle:
                {
                    angle += spin * circleSpeed / radius * dt;
                    Vector3 c = target.transform.position;
                    float bob = Mathf.Sin(now * 3.1f + angle) * 0.25f;
                    FlyTo(c + new Vector3(Mathf.Cos(angle) * radius, height + bob, Mathf.Sin(angle) * radius), 7f, dt);
                    if (now >= nextAttack)
                    {
                        if (Encounter && !Encounter.TryBeginAttack(this)) nextAttack = now + Random.Range(0.5f, 1.2f);
                        else
                        {
                            move = PickMove();
                            // Reserva a vez de agarrar já no aviso (dois não grudam juntos).
                            if (move == Move.Latch) latched = this;
                            Raise(Cue.Attack);
                            Play("Atacar", 0.05f);
                            Enter(Phase.WindUp, windUpSeconds);
                        }
                    }
                    break;
                }
                case Phase.WindUp:
                    Face(TargetChest, dt);
                    // Treme no ar antes de atacar (o aviso).
                    transform.position += Random.insideUnitSphere * (0.6f * dt);
                    if (now >= phaseUntil)
                    {
                        if (move == Move.Swoop)
                        {
                            PlanSwoop();
                            Play("Voar");
                            Enter(Phase.Swoop, 2f);
                        }
                        else Enter(Phase.Dive, 1.2f);
                    }
                    break;
                case Phase.Dive:
                {
                    Vector3 goal = TargetChest;
                    Face(goal, dt);
                    transform.position = Vector3.MoveTowards(transform.position, goal, diveSpeed * dt);
                    if ((transform.position - goal).sqrMagnitude <= reach * reach)
                    {
                        Raise(Cue.Strike);
                        var guard = Strike(move == Move.Latch ? latchDamage : damage, goal);
                        if (guard == CharacterCombat.GuardResult.Parried) Stun();
                        else if (move == Move.Latch && guard == CharacterCombat.GuardResult.None && !target.IsDodging) Latch();
                        else Climb();
                    }
                    else if (now >= phaseUntil) Climb();
                    break;
                }
                case Phase.Swoop:
                {
                    // Desce até rente ao peito e passa reto, subindo do outro lado.
                    Vector3 before = transform.position;
                    transform.position = Vector3.MoveTowards(before, swoopOut ? swoopTo : swoopVia, swoopSpeed * dt);
                    if (!swoopOut && (transform.position - swoopVia).sqrMagnitude < 0.01f) swoopOut = true;
                    Vector3 moved = transform.position - before;
                    moved.y = 0f;
                    if (moved.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(moved);
                    if (!swoopHit && (transform.position - TargetChest).sqrMagnitude <= swoopReach * swoopReach)
                    {
                        swoopHit = true;
                        Raise(Cue.Strike);
                        if (Strike(swoopDamage, TargetChest) == CharacterCombat.GuardResult.Parried)
                        {
                            Stun();
                            break;
                        }
                    }
                    if ((transform.position - swoopTo).sqrMagnitude < 0.04f || now >= phaseUntil) Climb();
                    break;
                }
                case Phase.Latched:
                {
                    // Grudado no peito, de frente para ele; rolar ou cair derrubam.
                    if (target.IsDodging || target.IsDown)
                    {
                        ShakenOff(true);
                        break;
                    }
                    Transform t = target.transform;
                    transform.SetPositionAndRotation(t.position + t.forward * latchOffset.x + Vector3.up * latchOffset.y, Quaternion.LookRotation(-t.forward));
                    target.SetSpeedMultiplier(latchedSlow, 0.2f);
                    if (now >= nextBite)
                    {
                        Raise(Cue.Strike);
                        Play("Morder", 0.05f);
                        Strike(biteDamage, TargetChest, unblockable: true);
                        if (--bitesLeft <= 0) Climb();
                        else nextBite = now + biteEvery;
                    }
                    break;
                }
                case Phase.Climb:
                {
                    Vector3 c = target.transform.position;
                    FlyTo(c + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius), climbSpeed, dt);
                    if (now >= phaseUntil) Enter(Phase.Circle, 0f);
                    break;
                }
                case Phase.Stunned:
                {
                    // Cai na vertical até o peito do capitão e fica balançando; o resto do tempo é dele para bater.
                    Vector3 p = transform.position;
                    p.y = Mathf.MoveTowards(p.y, target.transform.position.y + stunHeight, stunFallSpeed * dt);
                    transform.position = p;
                    if (now >= phaseUntil) Climb();
                    break;
                }
            }
        }

        void FlyTo(Vector3 goal, float speed, float dt)
        {
            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(before, goal, speed * dt);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            if (moved.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moved), 540f * dt);
        }

        void Face(Vector3 point, float dt)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 720f * dt);
        }
    }
}
