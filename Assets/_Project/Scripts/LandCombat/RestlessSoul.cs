using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Alma penada das catacumbas (C5, passo 2, 20/10; o fantasma-caveira do Ultimate Monsters do Quaternius, verde e meio transparente).
    /// Transparente (quase invisível) ela é invencível e atravessa paredes; escolhe um lugar livre perto do capitão (no NavMesh, à vista
    /// dele), se materializa (aviso: fica sólida e brilha, <see cref="materializeSeconds"/>) e só então leva dano. Sólida, vai até ele e dá a
    /// cabeçada (preparo de <see cref="windUpSeconds"/>: fica vermelha e guincha; a animação do golpe só começa nos últimos
    /// <see cref="swingLead"/> s; passa pela guarda como golpe de lâmina). Levou golpe: tranco e some de novo (o
    /// jogador acerta um golpe por aparição, ou mais aparando). Aparada: fica tonta, sólida, por <see cref="stunSeconds"/>.
    /// Golpes novos (18/11, pedido do Jhonata), sorteados com a cabeçada: <b>garra</b> (o soco do kit: preparo mais curto, dano menor) e
    /// <b>atravessar</b> (fica azul no preparo, vira transparente e passa voando por dentro do capitão: tira fôlego, um pouco de vida e o
    /// deixa lento; a guarda não segura, o rolamento escapa; golpe no preparo cancela).
    /// </summary>
    public class RestlessSoul : CryptMonster
    {
        enum Phase { Hidden, Phased, Materializing, Solid, WindUp, Pass, Recover, Stunned, Fading, Dead }
        enum Move { Headbutt, Claw, Pass }

        [SerializeField] Transform body;
        [SerializeField] Renderer[] renderers = System.Array.Empty<Renderer>();
        [SerializeField] Collider hitBox;
        [SerializeField] ParticleSystem wisps;
        [Header("Combate")]
        [SerializeField] float damage = 14f;
        [SerializeField] float attackRange = 1.7f, approachSpeed = 3.4f, phasedSpeed = 7f;
        [Tooltip("Preparo da cabeçada (s; era 0,55: não dava para ler) e, dentro dele, quando a animação do golpe começa (antes do impacto).")]
        [SerializeField] float windUpSeconds = 0.85f, swingLead = 0.55f;
        [SerializeField] float materializeSeconds = 0.7f, solidSeconds = 2.4f, recoverSeconds = 0.9f;
        [SerializeField] float stunSeconds = 1.4f, staggerSeconds = 0.35f, fadeSeconds = 0.4f;
        [Tooltip("Tempo rondando transparente entre uma aparição e outra (s).")]
        [SerializeField] Vector2 phasedSeconds = new(0.8f, 1.6f);
        [Tooltip("A que distância do capitão ela aparece.")]
        [SerializeField] Vector2 appearDistance = new(2.4f, 3.6f);
        [Header("Bote (18/11, o soco parado no lugar não agradava)")]
        [Tooltip("No preparo da garra e da cabeçada: quanto sobe (m) e inclina para trás (graus); no golpe: quanto mergulha para a frente (graus), quantos metros avança e a que velocidade.")]
        [SerializeField] float rearRise = 0.45f, rearLean = 28f, strikeLean = 40f, lungeDistance = 1.4f, lungeSpeed = 10f;
        [Header("Garra (18/11)")]
        [Tooltip("Dano, preparo e quando a animação do soco começa (antes do impacto).")]
        [SerializeField] float clawDamage = 10f, clawWindUp = 0.65f, clawLead = 0.35f;
        [Header("Atravessar (18/11)")]
        [Tooltip("Dano (passa pela guarda), fôlego que tira, lentidão (velocidade e segundos), preparo, velocidade e até onde passa depois do capitão.")]
        [SerializeField] float passDamage = 4f, passStamina = 35f, passSlow = 0.5f, passSlowSeconds = 1.5f;
        [SerializeField] float passWindUp = 0.9f, passSpeed = 9f, passOvershoot = 3f, passReach = 0.8f;
        [SerializeField] Color passColor = new(0.5f, 1.1f, 2.4f, 1f);
        [Tooltip("Peso no sorteio: cabeçada, garra, atravessar.")]
        [SerializeField] Vector3 moveWeights = new(0.45f, 0.35f, 0.2f);
        [Header("Visual")]
        [SerializeField] float hover = 0.35f;
        [SerializeField, Range(0f, 1f)] float solidAlpha = 0.85f, phasedAlpha = 0.1f;
        [Tooltip("Cor da alma preparando o golpe (o aviso) e quanto ela clareia na hora de erguer a guarda.")]
        [SerializeField] Color warnColor = new(2.2f, 0.75f, 0.35f, 1f);
        [SerializeField] float flashBoost = 1.5f, flashLead = 0.45f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Material[] materials = System.Array.Empty<Material>();
        Color[] baseColors = System.Array.Empty<Color>();
        Phase phase = Phase.Hidden;
        float phaseStart, phaseUntil, alpha = -1f, warn;
        Vector3 destination, passTo;
        bool struck, swung, flashed;
        Move move;
        float windUpNow, leanNow, riseNow, leanGoal, riseGoal, lungeLeft;

        public override bool IsGhost => true;
        /// <summary>Sólida (leva dano).</summary>
        public bool IsSolid => phase is Phase.Solid or Phase.WindUp or Phase.Recover or Phase.Stunned;
        public string PhaseName => phase.ToString();
        public string MoveName => move.ToString();
        /// <summary>Atravessar: a guarda não segura (aviso azul: role).</summary>
        public override bool WarnUnblockable => move == Move.Pass && phase is Phase.WindUp or Phase.Pass;
        public override float ImpactIn => phase switch
        {
            Phase.WindUp when !struck => Mathf.Max(0f, phaseUntil - Time.time)
                + (move == Move.Pass && TargetAlive ? Mathf.Max(0f, FlatTo(target.transform.position) - passReach) / passSpeed : 0f),
            Phase.Pass when !struck && TargetAlive => Mathf.Max(0f, FlatTo(target.transform.position) - passReach) / passSpeed,
            _ => -1f,
        };

        float FlatTo(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
        protected override bool IsPhased => phase is Phase.Phased or Phase.Materializing or Phase.Fading;

        /// <summary>A Lâmina de São Telmo pegou a alma transparente: leva o golpe, solta o ectoplasma e foge para outro lado.</summary>
        protected override void OnHolyHit(DamageInfo info)
        {
            base.OnHolyHit(info);
            if (health.IsDead) return;
            Play("Levar", 0.05f);
            if (wisps) wisps.Emit(14);
            if (Encounter) Encounter.EndAttack(this);
            destination = PickSpot();
            Enter(Phase.Phased, Random.Range(phasedSeconds.x, phasedSeconds.y));
        }

        public void Configure(Transform model, Renderer[] visible, Collider box, ParticleSystem particles)
        {
            body = model;
            renderers = visible;
            hitBox = box;
            wisps = particles;
        }

        protected override void Awake()
        {
            base.Awake();
            // Cópia do material por alma: cada uma tem a sua transparência (o GPU Resident Drawer não aceita MaterialPropertyBlock).
            materials = new Material[renderers.Length];
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
                baseColors[i] = materials[i].GetColor(BaseColorId);
            }
            Hide();
        }

        void OnDestroy()
        {
            foreach (var m in materials)
                if (m) Destroy(m);
        }

        // ---------- Ciclo ----------

        protected override void OnWake()
        {
            // Sai do lugar dela já transparente e aparece um pouco depois (as três não chegam juntas).
            SetVisible(true);
            SetSolid(false);
            SetAlpha(0f);
            destination = PickSpot();
            Enter(Phase.Phased, Random.Range(0.3f, 1.4f));
        }

        protected override void OnSleep()
        {
            Hide();
            transform.SetPositionAndRotation(post, postRotation);
        }

        protected override void OnReset()
        {
            Hide();
            Play("Flutuar", 0f);
        }

        protected override void OnDeath()
        {
            SetSolid(false);
            Enter(Phase.Dead, 0f);
            Play("Morrer", 0.08f);
            if (wisps) wisps.Emit(30);
        }

        protected override void OnDamaged(DamageInfo info)
        {
            base.OnDamaged(info);
            if (health.IsDead || !IsSolid) return;
            // Levou golpe: tranco para trás e some (foge para aparecer de outro lado).
            Play("Levar", 0.05f);
            Vector3 away = transform.position - (info.source ? info.source.transform.position : transform.position - transform.forward);
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.position += away.normalized * 0.6f;
            if (Encounter) Encounter.EndAttack(this);
            Enter(Phase.Stunned, staggerSeconds);
        }

        void Hide()
        {
            phase = Phase.Hidden;
            SetSolid(false);
            SetAlpha(0f);
            SetVisible(false);
        }

        void Enter(Phase next, float seconds)
        {
            phase = next;
            phaseStart = Time.time;
            phaseUntil = Time.time + seconds;
            struck = swung = flashed = false;
            if (next != Phase.WindUp) SetWarn(0f);
            if (next is not (Phase.WindUp or Phase.Recover)) leanGoal = riseGoal = 0f;
            if (next == Phase.Recover) riseGoal = 0f;
            switch (next)
            {
                case Phase.Materializing:
                    Raise(Cue.Appear);
                    if (wisps) wisps.Emit(16);
                    Play("Flutuar");
                    break;
                case Phase.Fading:
                    SetSolid(false);
                    if (Encounter) Encounter.EndAttack(this);
                    Raise(Cue.Vanish);
                    if (wisps) wisps.Emit(10);
                    Play("Voar");
                    break;
                case Phase.WindUp:
                    // O aviso: fica vermelha e guincha; o golpe (animação e avanço) só sai no fim.
                    Raise(Cue.Attack);
                    break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || phase == Phase.Hidden) return;
            if (body)
            {
                // O bote: armar (sobe e inclina para trás) e mergulhar (inclina para a frente), suavizado.
                leanNow = Mathf.MoveTowards(leanNow, leanGoal, (leanGoal > leanNow ? 420f : 160f) * dt);
                riseNow = Mathf.MoveTowards(riseNow, riseGoal, 2.5f * dt);
                body.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.3f + post.x) * 0.12f + riseNow);
                body.localRotation = Quaternion.Euler(leanNow, 0f, 0f);
            }
            if (phase == Phase.Dead)
            {
                float k = Mathf.Clamp01((Time.time - phaseStart) / 1.4f);
                SetAlpha(Mathf.Lerp(solidAlpha, 0f, k));
                if (k >= 1f) SetVisible(false);
                return;
            }
            if (!IsAwake || !TargetAlive)
            {
                if (phase != Phase.Fading && phase != Phase.Phased) Enter(Phase.Fading, fadeSeconds);
            }
            float now = Time.time;
            switch (phase)
            {
                case Phase.Phased:
                    SetAlpha(Mathf.MoveTowards(alpha, phasedAlpha, dt / fadeSeconds));
                    MoveTo(destination, phasedSpeed, dt);
                    if (TargetAlive) Face(target.transform.position, dt);
                    if (now < phaseUntil || (transform.position - destination).sqrMagnitude > 0.04f || !IsAwake || !TargetAlive) break;
                    if (Encounter && !Encounter.TryBeginAttack(this))
                    {
                        // Sem a vez: ronda para outro lugar.
                        destination = PickSpot();
                        Enter(Phase.Phased, Random.Range(phasedSeconds.x, phasedSeconds.y));
                        break;
                    }
                    Enter(Phase.Materializing, materializeSeconds);
                    break;
                case Phase.Materializing:
                    SetAlpha(Mathf.Lerp(phasedAlpha, solidAlpha, Mathf.Clamp01((now - phaseStart) / materializeSeconds)));
                    if (TargetAlive) Face(target.transform.position, dt);
                    if (now < phaseUntil) break;
                    SetAlpha(solidAlpha);
                    SetSolid(true);
                    Enter(Phase.Solid, solidSeconds);
                    break;
                case Phase.Solid:
                {
                    if (!TargetAlive) break;
                    Vector3 to = target.transform.position - transform.position;
                    to.y = 0f;
                    Face(target.transform.position, dt);
                    if (to.magnitude <= attackRange) BeginWindUp();
                    else
                    {
                        Vector3 goal = target.transform.position + Vector3.up * hover;
                        MoveTo(Vector3.MoveTowards(transform.position, goal, to.magnitude - attackRange * 0.8f), approachSpeed, dt);
                        if (now >= phaseUntil) Enter(Phase.Fading, fadeSeconds);
                    }
                    break;
                }
                case Phase.WindUp:
                {
                    if (!TargetAlive) break;
                    Face(target.transform.position, dt);
                    float left = phaseUntil - now;
                    float k = (now - phaseStart) / windUpNow;
                    SetWarn(Mathf.Clamp01(k * 1.6f) * (left <= flashLead ? flashBoost : 1f));
                    if (move == Move.Pass)
                    {
                        // Recua um pouco e treme antes de atravessar.
                        Vector3 back = transform.position - target.transform.position;
                        back.y = 0f;
                        if (back.sqrMagnitude > 0.0001f && back.magnitude < 2.6f) MoveTo(transform.position + back.normalized, 1.5f, dt);
                        if (now >= phaseUntil) BeginPass();
                        break;
                    }
                    if (left <= flashLead && !flashed)
                    {
                        flashed = true;
                        if (wisps) wisps.Emit(12);
                    }
                    float lead = move == Move.Claw ? clawLead : swingLead;
                    if (!swung)
                    {
                        // Armando: sobe e inclina para trás, cada vez mais.
                        float arm = Mathf.Clamp01((now - phaseStart) / Mathf.Max(0.05f, windUpNow - lead));
                        leanGoal = -rearLean * arm;
                        riseGoal = rearRise * arm;
                    }
                    if (left <= lead && !swung)
                    {
                        swung = true;
                        Play(move == Move.Claw ? "Garra" : "Golpe", 0.05f);
                        // O bote: mergulha para a frente e avança até colar no capitão.
                        leanGoal = strikeLean;
                        riseGoal = 0f;
                        lungeLeft = lungeDistance;
                        if (wisps) wisps.Emit(10);
                    }
                    if (swung && lungeLeft > 0f)
                    {
                        Vector3 flat = target.transform.position - transform.position;
                        flat.y = 0f;
                        float step = Mathf.Min(lungeLeft, lungeSpeed * dt, Mathf.Max(0f, flat.magnitude - 0.7f));
                        if (step > 0f && flat.sqrMagnitude > 0.0001f) transform.position += flat.normalized * step;
                        lungeLeft = step > 0f ? lungeLeft - step : 0f;
                        if (wisps && Random.value < 0.5f) wisps.Emit(1);
                    }
                    if (now < phaseUntil || struck) break;
                    struck = true;
                    Raise(Cue.Strike);
                    Vector3 d = target.transform.position - transform.position;
                    d.y = 0f;
                    var guard = d.magnitude <= attackRange + 0.5f
                        ? Strike(move == Move.Claw ? clawDamage : damage, TargetChest - d.normalized * 0.3f) : CharacterCombat.GuardResult.None;
                    if (guard == CharacterCombat.GuardResult.Parried)
                    {
                        Play("Levar", 0.05f);
                        if (Encounter) Encounter.EndAttack(this);
                        Enter(Phase.Stunned, stunSeconds);
                    }
                    else Enter(Phase.Recover, recoverSeconds);
                    // Volta a ficar em pé devagar depois do bote.
                    leanGoal = 0f;
                    break;
                }
                case Phase.Pass:
                {
                    // Voa reto, transparente, por dentro do capitão e sai do outro lado.
                    SetAlpha(Mathf.MoveTowards(alpha, 0.35f, dt / 0.2f));
                    MoveTo(passTo, passSpeed, dt);
                    Face(passTo, dt * 3f);
                    if (!struck && TargetAlive && FlatTo(target.transform.position) <= passReach)
                    {
                        struck = true;
                        PassThrough();
                    }
                    if ((transform.position - passTo).sqrMagnitude < 0.04f || now >= phaseUntil) Enter(Phase.Fading, fadeSeconds);
                    break;
                }
                case Phase.Recover:
                case Phase.Stunned:
                    if (now >= phaseUntil) Enter(Phase.Fading, fadeSeconds);
                    break;
                case Phase.Fading:
                    SetAlpha(Mathf.MoveTowards(alpha, phasedAlpha, dt / fadeSeconds));
                    if (now < phaseUntil) break;
                    if (!IsAwake)
                    {
                        Hide();
                        transform.SetPositionAndRotation(post, postRotation);
                        break;
                    }
                    destination = PickSpot();
                    Enter(Phase.Phased, Random.Range(phasedSeconds.x, phasedSeconds.y));
                    break;
            }
        }

        // ---------- Golpes ----------

        void BeginWindUp()
        {
            float h = Mathf.Max(0f, moveWeights.x), c = Mathf.Max(0f, moveWeights.y), q = Mathf.Max(0f, moveWeights.z);
            float r = Random.value * Mathf.Max(0.0001f, h + c + q);
            move = r < h ? Move.Headbutt : r < h + c ? Move.Claw : Move.Pass;
            windUpNow = move switch { Move.Claw => clawWindUp, Move.Pass => passWindUp, _ => windUpSeconds };
            Enter(Phase.WindUp, windUpNow);
        }

        void BeginPass()
        {
            Vector3 c = target.transform.position;
            Vector3 dir = c - transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            passTo = c + dir * passOvershoot + Vector3.up * hover;
            SetSolid(false);
            Raise(Cue.Vanish);
            if (wisps) wisps.Emit(14);
            Play("Voar", 0.05f);
            Enter(Phase.Pass, 1.6f);
        }

        /// <summary>Passou por dentro: o frio tira fôlego e deixa lento; rolando (invencível) escapa.</summary>
        void PassThrough()
        {
            Raise(Cue.Strike);
            if (wisps) wisps.Emit(20);
            if (target.DodgeInvulnerable) return;
            Strike(passDamage, TargetChest, unblockable: true);
            if (target.TryGetComponent<Stamina>(out var stamina)) stamina.Spend(passStamina);
            target.SetSpeedMultiplier(passSlow, passSlowSeconds);
        }

        // ---------- Ajuda ----------

        /// <summary>Lugar livre perto do capitão: no NavMesh, à vista dele (sem parede no meio), a 2,4–3,6 m.</summary>
        Vector3 PickSpot()
        {
            if (!TargetAlive) return transform.position;
            Vector3 c = target.transform.position;
            for (int i = 0; i < 14; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(appearDistance.x, appearDistance.y);
                if (!NavMesh.SamplePosition(p, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                if (NavMesh.Raycast(c, hit.position, out _, NavMesh.AllAreas)) continue;
                return hit.position + Vector3.up * hover;
            }
            return c + (transform.position - c).normalized * appearDistance.y + Vector3.up * hover;
        }

        void MoveTo(Vector3 goal, float speed, float dt) => transform.position = Vector3.MoveTowards(transform.position, goal, speed * dt);

        void Face(Vector3 point, float dt)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * dt);
        }

        void SetSolid(bool solid)
        {
            if (hitBox) hitBox.enabled = solid;
            if (health) health.Invulnerable = !solid;
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers)
                if (r) r.enabled = visible;
            if (wisps)
            {
                var emission = wisps.emission;
                emission.enabled = visible;
            }
            if (animator) animator.enabled = visible;
        }

        void SetAlpha(float a)
        {
            if (Mathf.Abs(a - alpha) < 0.004f) return;
            alpha = a;
            ApplyColor();
        }

        /// <summary>0 = a cor de sempre, 1 = a do aviso; acima de 1 = o clarão da hora de erguer a guarda.</summary>
        void SetWarn(float w)
        {
            if (Mathf.Abs(w - warn) < 0.01f) return;
            warn = w;
            ApplyColor();
        }

        void ApplyColor()
        {
            for (int i = 0; i < materials.Length; i++)
            {
                var c = Color.Lerp(baseColors[i], move == Move.Pass ? passColor : warnColor, Mathf.Min(1f, warn));
                if (warn > 1f) c *= warn;
                c.a = baseColors[i].a * alpha;
                materials[i].SetColor(BaseColorId, c);
            }
        }
    }
}
