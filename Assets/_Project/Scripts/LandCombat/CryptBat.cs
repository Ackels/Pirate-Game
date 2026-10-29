using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Morcego das catacumbas (C5, passo 2, 20/10; o morcego do Animated Monsters do Quaternius, com olhos vermelhos acesos). Dormindo, o
    /// bando ronda alto no meio da sala. Acordado, ronda o capitão a 2,8–4,2 m e 2,4–3,2 m de altura; de tempos em tempos um deles (a vez do
    /// <see cref="CryptEncounter"/>) guincha, para no ar (<see cref="windUpSeconds"/>) e mergulha no peito dele (mordida fraca, passa pela
    /// guarda); depois sobe de volta. Lá em cima o cutelo não alcança (só tiro); no mergulho, sim. Levou golpe: sobe tonto. Morto, cai.
    /// </summary>
    public class CryptBat : CryptMonster
    {
        enum Phase { Roost, Circle, WindUp, Dive, Climb, Stunned, Dead }

        [SerializeField] Collider hitBox;
        [Header("Combate")]
        [SerializeField] float damage = 6f;
        [SerializeField] Vector2 circleRadius = new(2.8f, 4.2f), circleHeight = new(2.4f, 3.2f);
        [Tooltip("windUpSeconds = tempo tremendo no ar antes de mergulhar (o aviso; era 0,35 s).")]
        [SerializeField] float circleSpeed = 4.5f, diveSpeed = 9f, climbSpeed = 6f, windUpSeconds = 0.6f, climbSeconds = 0.9f, reach = 0.9f;
        [Tooltip("Aparado: fica tonto à altura do peito do capitão (dá para bater) por estes segundos (22/10, revisão: o último morcego mordia sem parar e a guarda não resolvia).")]
        [SerializeField] float stunSeconds = 1.5f, stunHeight = 1.1f, stunFallSpeed = 6f;
        [Tooltip("Espera entre um mergulho e outro (s).")]
        [SerializeField] Vector2 attackGap = new(1.4f, 3.2f);
        [Header("Dormindo")]
        [SerializeField] float roostRadius = 2.5f, roostHeight = 3.4f;

        Phase phase = Phase.Roost;
        float phaseStart, phaseUntil, nextAttack, angle, radius, height, spin = 1f, deadFloor;
        Vector3 velocity;

        public override bool IsGhost => false;
        public string PhaseName => phase.ToString();
        public override float WarningHeight => 0.9f;
        /// <summary>Até a mordida: o resto do tremor + a distância do mergulho na velocidade dele.</summary>
        public override float ImpactIn => phase switch
        {
            Phase.WindUp when TargetAlive => Mathf.Max(0f, phaseUntil - Time.time) + DiveDistance / diveSpeed,
            Phase.Dive when TargetAlive => Mathf.Max(0f, DiveDistance - reach) / diveSpeed,
            _ => -1f,
        };

        float DiveDistance => (TargetChest - transform.position).magnitude;

        public void Configure(Collider box) => hitBox = box;

        protected override void Awake()
        {
            base.Awake();
            angle = Random.Range(0f, Mathf.PI * 2f);
            spin = Random.value < 0.5f ? -1f : 1f;
            radius = Random.Range(0.5f, 1f) * roostRadius;
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

        protected override void OnSleep() => Enter(Phase.Roost, 0f);

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
            if (phase is Phase.WindUp or Phase.Dive) Climb();
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
            if (Encounter) Encounter.EndAttack(this);
            nextAttack = Time.time + Random.Range(attackGap.x, attackGap.y);
            velocity = Vector3.zero;
            Play("Levar", 0.05f);
            Enter(Phase.Stunned, stunSeconds);
        }

        void Climb()
        {
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
                            Raise(Cue.Attack);
                            Play("Atacar", 0.05f);
                            Enter(Phase.WindUp, windUpSeconds);
                        }
                    }
                    break;
                }
                case Phase.WindUp:
                    Face(TargetChest, dt);
                    // Treme no ar antes de mergulhar (o aviso).
                    transform.position += Random.insideUnitSphere * (0.6f * dt);
                    if (now >= phaseUntil) Enter(Phase.Dive, 1.2f);
                    break;
                case Phase.Dive:
                {
                    Vector3 goal = TargetChest;
                    Face(goal, dt);
                    transform.position = Vector3.MoveTowards(transform.position, goal, diveSpeed * dt);
                    if ((transform.position - goal).sqrMagnitude <= reach * reach)
                    {
                        Raise(Cue.Strike);
                        var guard = Strike(damage, goal);
                        if (guard == CharacterCombat.GuardResult.Parried) Stun();
                        else Climb();
                    }
                    else if (now >= phaseUntil) Climb();
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
