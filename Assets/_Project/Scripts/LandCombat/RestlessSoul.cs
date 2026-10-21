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
    /// </summary>
    public class RestlessSoul : CryptMonster
    {
        enum Phase { Hidden, Phased, Materializing, Solid, WindUp, Recover, Stunned, Fading, Dead }

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
        Vector3 destination;
        bool struck, swung, flashed;

        public override bool IsGhost => true;
        /// <summary>Sólida (leva dano).</summary>
        public bool IsSolid => phase is Phase.Solid or Phase.WindUp or Phase.Recover or Phase.Stunned;
        public string PhaseName => phase.ToString();
        public override float ImpactIn => phase == Phase.WindUp && !struck ? Mathf.Max(0f, phaseUntil - Time.time) : -1f;
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
            if (body) body.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.3f + post.x) * 0.12f);
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
                    if (to.magnitude <= attackRange) Enter(Phase.WindUp, windUpSeconds);
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
                    float k = (now - phaseStart) / windUpSeconds;
                    SetWarn(Mathf.Clamp01(k * 1.6f) * (left <= flashLead ? flashBoost : 1f));
                    if (left <= flashLead && !flashed)
                    {
                        flashed = true;
                        if (wisps) wisps.Emit(12);
                    }
                    if (left <= swingLead && !swung)
                    {
                        swung = true;
                        Play("Golpe", 0.05f);
                    }
                    // Avança na cabeçada (os últimos ~0,35 s).
                    if (left <= 0.35f)
                    {
                        Vector3 goal = target.transform.position + Vector3.up * hover;
                        Vector3 flat = goal - transform.position;
                        flat.y = 0f;
                        if (flat.magnitude > 0.9f) MoveTo(transform.position + flat.normalized * 0.5f, 4f, dt);
                    }
                    if (now < phaseUntil || struck) break;
                    struck = true;
                    Raise(Cue.Strike);
                    Vector3 d = target.transform.position - transform.position;
                    d.y = 0f;
                    var guard = d.magnitude <= attackRange + 0.5f
                        ? Strike(damage, TargetChest - d.normalized * 0.3f) : CharacterCombat.GuardResult.None;
                    if (guard == CharacterCombat.GuardResult.Parried)
                    {
                        Play("Levar", 0.05f);
                        if (Encounter) Encounter.EndAttack(this);
                        Enter(Phase.Stunned, stunSeconds);
                    }
                    else Enter(Phase.Recover, recoverSeconds);
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
                var c = Color.Lerp(baseColors[i], warnColor, Mathf.Min(1f, warn));
                if (warn > 1f) c *= warn;
                c.a = baseColors[i].a * alpha;
                materials[i].SetColor(BaseColorId, c);
            }
        }
    }
}
