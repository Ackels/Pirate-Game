using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Pirates.LandCombat
{
    /// <summary>
    /// O Capelão Fantasma (C5, passo 3, 20/10): o chefe do salão das catacumbas, a alma do capelão da Frota de Prata, guardando o baú
    /// (é o <see cref="ILockSource"/> dele: o baú só abre com ele derrotado). Como as almas penadas, transparente é invencível e anda pelo
    /// salão; aparece perto do capitão e, sólido, faz uma de três coisas, todas avisadas pelo "!" / "!!" sobre ele:
    /// <list type="bullet">
    /// <item>cabeçada (amarelo = erga a guarda; aparada, fica tonto e aberto por <see cref="stunSeconds"/>);</item>
    /// <item>almas do chão: um círculo aparece debaixo do capitão e, no fim, as almas estouram dali (azul = a guarda não segura: saia do
    /// círculo ou role);</item>
    /// <item>fogo-fátuo: três bolas que perseguem devagar (a guarda segura e apara).</item>
    /// </list>
    /// Depois de cada golpe fica sólido um pouco (a hora de bater); leva até <see cref="hitsPerWindow"/> golpes e some. Com metade da vida,
    /// fica furioso (mais rápido) e chama as duas almas penadas do salão de tempos em tempos. Não cambaleia com golpe. A Lâmina de São
    /// Telmo, que está no baú dele, fere mesmo transparente (para a ruína recomeçada).
    /// </summary>
    public class ChaplainGhost : CryptMonster, ILockSource
    {
        enum Phase { Hidden, Rising, Phased, Materializing, Solid, WindUp, Lament, Volley, Recover, Stunned, Summon, Fury, Fading, Dead }

        static readonly List<ChaplainGhost> bosses = new();
        /// <summary>Os Capelães ligados (a barra do chefe percorre esta lista).</summary>
        public static IReadOnlyList<ChaplainGhost> Bosses => bosses;
        public static event Action<ChaplainGhost> Enraged, Defeated;

        [SerializeField] string displayName = "Capelão Fantasma";
        [SerializeField] string title = "guardião das catacumbas da Ermida";
        [SerializeField] Transform body;
        [SerializeField] Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] Collider hitBox;
        [SerializeField] ParticleSystem wisps;
        [SerializeField] Light glow;
        [Tooltip("As velas que giram em volta da cabeça.")]
        [SerializeField] Transform halo;
        [Tooltip("O círculo no chão do ataque das almas.")]
        [SerializeField] LineRenderer ring;
        [Tooltip("As bolas de fogo-fátuo (fora do Capelão, desligadas até ele lançar).")]
        [SerializeField] Transform[] orbs = Array.Empty<Transform>();
        [Tooltip("As almas penadas que ele chama na fúria (só acordam chamadas).")]
        [SerializeField] RestlessSoul[] souls = Array.Empty<RestlessSoul>();

        [Header("Cabeçada")]
        [SerializeField] float strikeDamage = 22f;
        [SerializeField] float strikeRange = 2.5f, approachSpeed = 3.2f, phasedSpeed = 6f;
        [SerializeField] float windUpSeconds = 1f, swingLead = 0.55f, flashLead = 0.45f;
        [SerializeField] float recoverSeconds = 1.5f, stunSeconds = 2.4f, solidSeconds = 3f;
        [Header("Almas do chão")]
        [SerializeField] float lamentDamage = 22f;
        [SerializeField] float lamentRadius = 2.6f, lamentSeconds = 1.4f, lamentRecover = 1.8f, lamentCooldown = 7f;
        [Header("Fogo-fátuo")]
        [SerializeField] float orbDamage = 10f;
        [SerializeField] float orbSpeed = 6.5f, orbTurn = 70f, orbLife = 4f, orbGap = 0.3f, orbReach = 0.6f;
        [SerializeField] float volleyWindUp = 0.85f, volleyRecover = 1.1f, volleyCooldown = 6f, volleyFrom = 4.5f;
        [Header("Fúria")]
        [SerializeField, Range(0f, 1f)] float furyAt = 0.5f;
        [SerializeField] float furySeconds = 2.2f, summonEvery = 20f, summonSeconds = 1.6f;
        [Tooltip("Tempos de preparo e espera na fúria × isto.")]
        [SerializeField] float furyTempo = 0.82f;
        [Header("Ritmo")]
        [SerializeField] Vector2 phasedSeconds = new(0.9f, 1.6f);
        [SerializeField] Vector2 appearDistance = new(3.5f, 5.5f);
        [SerializeField] int hitsPerWindow = 4;
        [SerializeField] float materializeSeconds = 0.6f, fadeSeconds = 0.45f, riseSeconds = 2.2f;
        [Header("Visual")]
        [SerializeField] float hover = 0.6f;
        [SerializeField, Range(0f, 1f)] float solidAlpha = 0.9f, phasedAlpha = 0.14f;
        [SerializeField] Color warnColor = new(2.2f, 0.75f, 0.35f, 1f), lamentColor = new(0.6f, 1.5f, 2.8f, 1f), furyColor = new(1.7f, 0.6f, 2.4f, 1f);
        [SerializeField] float flashBoost = 1.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Material[] materials = Array.Empty<Material>();
        Color[] baseColors = Array.Empty<Color>();
        Phase phase = Phase.Hidden;
        float phaseStart, phaseUntil, alpha = -1f, warn, nextLament, nextVolley, nextSummon, glowIntensity;
        Color warnTint;
        Vector3 destination, lamentCenter;
        bool struck, swung, flashed, enraged, soulsCalled;
        int hitsTaken;
        CombatEffects effects;
        Vector3[] orbVelocity = Array.Empty<Vector3>();
        float[] orbLaunch = Array.Empty<float>(), orbDeath = Array.Empty<float>();
        Vector3[] ringPoints = Array.Empty<Vector3>();

        public string DisplayName => displayName;
        public string Title => title;
        public bool IsEngaged => IsAwake && !IsDead;
        public bool IsEnraged => enraged;
        public string PhaseName => phase.ToString();
        public bool IsSolid => phase is Phase.Solid or Phase.WindUp or Phase.Lament or Phase.Volley or Phase.Recover or Phase.Stunned or Phase.Summon;
        public bool IsLocked => !IsDead;
        public string LockedReason => $"derrote o {displayName}";
        public override bool IsGhost => true;
        public override float VoicePitch => 0.62f;
        public override float WarningHeight => 3.4f;
        public override bool WarnUnblockable => phase == Phase.Lament;
        protected override bool IsPhased => phase is Phase.Phased or Phase.Materializing or Phase.Fading;
        protected override float BodyHeight => 1.6f;

        public override float ImpactIn
        {
            get
            {
                float now = Time.time;
                switch (phase)
                {
                    case Phase.WindUp when !struck:
                    case Phase.Lament when !struck:
                        return Mathf.Max(0f, phaseUntil - now);
                    case Phase.Volley when TargetAlive:
                        return Mathf.Max(0f, phaseUntil - now) + Mathf.Max(0f, (TargetChest - transform.position).magnitude - orbReach) / orbSpeed;
                }
                float soonest = -1f;
                for (int i = 0; i < orbs.Length; i++)
                {
                    if (!orbs[i] || !orbs[i].gameObject.activeSelf || !TargetAlive) continue;
                    float t = Mathf.Max(0f, (TargetChest - orbs[i].position).magnitude - orbReach) / orbSpeed;
                    if (soonest < 0f || t < soonest) soonest = t;
                }
                return soonest;
            }
        }

        public void Configure(Transform model, Renderer[] visible, Collider box, ParticleSystem particles, Light light, Transform candles,
            LineRenderer floorRing, Transform[] fireballs, RestlessSoul[] calledSouls)
        {
            body = model;
            renderers = visible;
            hitBox = box;
            wisps = particles;
            glow = light;
            halo = candles;
            ring = floorRing;
            orbs = fireballs;
            souls = calledSouls;
        }

        /// <summary>Testes: liga a fúria agora.</summary>
        public void EnrageNow()
        {
            if (IsAwake && !IsDead && !enraged) Enter(Phase.Fury, furySeconds);
        }

        protected override void Awake()
        {
            base.Awake();
            materials = new Material[renderers.Length];
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
                baseColors[i] = materials[i].GetColor(BaseColorId);
            }
            orbVelocity = new Vector3[orbs.Length];
            orbLaunch = new float[orbs.Length];
            orbDeath = new float[orbs.Length];
            ringPoints = ring ? new Vector3[ring.positionCount] : Array.Empty<Vector3>();
            if (glow) glowIntensity = glow.intensity;
            Hide();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            bosses.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            bosses.Remove(this);
        }

        void OnDestroy()
        {
            foreach (var m in materials)
                if (m) Destroy(m);
        }

        // ---------- Ciclo ----------

        protected override void OnWake()
        {
            if (!effects) Services.TryGet(out effects);
            hitsTaken = 0;
            nextLament = Time.time + 3f;
            nextVolley = Time.time + 2f;
            // Sobe do altar: aparece devagar, invencível, e geme (o aviso de que a luta começou).
            SetVisible(true);
            SetSolid(false);
            SetAlpha(0f);
            transform.SetPositionAndRotation(post, postRotation);
            Enter(Phase.Rising, riseSeconds);
        }

        protected override void OnSleep() => Rest();
        protected override void OnReset() => Rest();

        void Rest()
        {
            Hide();
            enraged = false;
            soulsCalled = false;
            ApplyColor();
            transform.SetPositionAndRotation(post, postRotation);
            Play("Flutuar", 0f);
        }

        protected override void OnDeath()
        {
            SetSolid(false);
            HideAttacks();
            Enter(Phase.Dead, 0f);
            Play("Morrer", 0.08f);
            if (wisps) wisps.Emit(60);
            // As almas que ele chamou vão junto.
            foreach (var s in souls)
                if (s && s.IsAwake && !s.IsDead) s.Sleep();
            Defeated?.Invoke(this);
        }

        protected override void OnDamaged(DamageInfo info)
        {
            base.OnDamaged(info);
            if (health.IsDead) return;
            hitsTaken++;
            // Não cambaleia: só clareia um instante. Na fúria, o golpe que passa da metade para tudo.
            if (!enraged && health.Normalized <= furyAt)
            {
                Enter(Phase.Fury, furySeconds);
                return;
            }
            if (phase is Phase.Solid or Phase.Recover && hitsTaken >= hitsPerWindow) Enter(Phase.Fading, fadeSeconds);
            else if (phase == Phase.Stunned && hitsTaken >= hitsPerWindow + 2) Enter(Phase.Fading, fadeSeconds);
            else if (phase is Phase.Solid or Phase.Recover or Phase.Stunned) Play("Levar", 0.05f);
        }

        protected override void OnHolyHit(DamageInfo info)
        {
            base.OnHolyHit(info);
            if (!health.IsDead && wisps) wisps.Emit(16);
        }

        void Hide()
        {
            phase = Phase.Hidden;
            SetSolid(false);
            SetAlpha(0f);
            SetVisible(false);
            HideAttacks();
        }

        void HideAttacks()
        {
            if (ring) ring.enabled = false;
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i]) orbs[i].gameObject.SetActive(false);
                orbLaunch[i] = 0f;
            }
        }

        void Enter(Phase next, float seconds)
        {
            float tempo = enraged && next is Phase.WindUp or Phase.Lament or Phase.Volley or Phase.Recover or Phase.Phased ? furyTempo : 1f;
            phase = next;
            phaseStart = Time.time;
            phaseUntil = Time.time + seconds * tempo;
            struck = swung = flashed = false;
            if (next is not (Phase.WindUp or Phase.Lament or Phase.Volley)) SetWarn(0f, warnColor);
            if (ring && next != Phase.Lament) ring.enabled = false;
            switch (next)
            {
                case Phase.Rising:
                    Raise(Cue.Appear);
                    if (wisps) wisps.Emit(30);
                    Play("Flutuar");
                    break;
                case Phase.Materializing:
                    if (wisps) wisps.Emit(16);
                    Play("Flutuar");
                    break;
                case Phase.Fading:
                    SetSolid(false);
                    Raise(Cue.Vanish);
                    if (wisps) wisps.Emit(10);
                    Play("Voar");
                    break;
                case Phase.WindUp:
                case Phase.Volley:
                    Raise(Cue.Attack);
                    break;
                case Phase.Lament:
                    Raise(Cue.Attack);
                    Play("Lancar", 0.1f);
                    lamentCenter = target.transform.position;
                    DrawRing(0f);
                    if (ring) ring.enabled = true;
                    break;
                case Phase.Summon:
                    soulsCalled = false;
                    Raise(Cue.Appear);
                    Play("Lancar", 0.1f);
                    break;
                case Phase.Fury:
                    // A fúria: invencível um instante, clarão roxo, urro; depois chama as almas.
                    enraged = true;
                    SetSolid(false);
                    HideAttacks();
                    SetAlpha(solidAlpha);
                    Raise(Cue.Burst);
                    Raise(Cue.Appear);
                    if (wisps) wisps.Emit(50);
                    if (effects) effects.Unearth(transform.position, true);
                    Play("Lancar", 0.1f);
                    ApplyColor();
                    Enraged?.Invoke(this);
                    break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || phase == Phase.Hidden) return;
            float now = Time.time;
            if (body) body.localPosition = Vector3.up * (Mathf.Sin(now * 1.7f) * 0.15f);
            if (halo) halo.Rotate(0f, 55f * dt, 0f, Space.Self);
            UpdateOrbs(dt, now);
            if (phase == Phase.Dead)
            {
                float k = Mathf.Clamp01((now - phaseStart) / 2.2f);
                SetAlpha(Mathf.Lerp(solidAlpha, 0f, k));
                if (k >= 1f) SetVisible(false);
                return;
            }
            if ((!IsAwake || !TargetAlive) && phase is not (Phase.Fading or Phase.Phased))
            {
                HideAttacks();
                Enter(Phase.Fading, fadeSeconds);
            }
            switch (phase)
            {
                case Phase.Rising:
                    SetAlpha(Mathf.Lerp(0f, solidAlpha, Mathf.Clamp01((now - phaseStart) / riseSeconds)));
                    if (TargetAlive) Face(target.transform.position, dt);
                    if (now >= phaseUntil) Enter(Phase.Fading, fadeSeconds);
                    break;
                case Phase.Phased:
                    SetAlpha(Mathf.MoveTowards(alpha, phasedAlpha, dt / fadeSeconds));
                    MoveTo(destination, phasedSpeed, dt);
                    if (TargetAlive) Face(target.transform.position, dt);
                    if (now < phaseUntil || (transform.position - destination).sqrMagnitude > 0.04f || !IsAwake || !TargetAlive) break;
                    Enter(Phase.Materializing, materializeSeconds);
                    break;
                case Phase.Materializing:
                    SetAlpha(Mathf.Lerp(phasedAlpha, solidAlpha, Mathf.Clamp01((now - phaseStart) / materializeSeconds)));
                    if (TargetAlive) Face(target.transform.position, dt);
                    if (now < phaseUntil) break;
                    SetAlpha(solidAlpha);
                    SetSolid(true);
                    hitsTaken = 0;
                    ChooseMove(now);
                    break;
                case Phase.Solid:
                {
                    if (!TargetAlive) break;
                    Vector3 to = target.transform.position - transform.position;
                    to.y = 0f;
                    Face(target.transform.position, dt);
                    if (to.magnitude <= strikeRange) Enter(Phase.WindUp, windUpSeconds);
                    else
                    {
                        Vector3 goal = target.transform.position + Vector3.up * hover;
                        MoveTo(Vector3.MoveTowards(transform.position, goal, to.magnitude - strikeRange * 0.8f), approachSpeed, dt);
                        if (now >= phaseUntil) Enter(Phase.Fading, fadeSeconds);
                    }
                    break;
                }
                case Phase.WindUp:
                    UpdateStrike(dt, now);
                    break;
                case Phase.Lament:
                {
                    float k = Mathf.Clamp01((now - phaseStart) / Mathf.Max(0.01f, phaseUntil - phaseStart));
                    if (TargetAlive) Face(target.transform.position, dt);
                    SetWarn(Mathf.Clamp01(k * 1.6f) * (phaseUntil - now <= flashLead ? flashBoost : 1f), lamentColor);
                    DrawRing(k);
                    if (now < phaseUntil || struck) break;
                    struck = true;
                    Burst();
                    Enter(Phase.Recover, lamentRecover);
                    break;
                }
                case Phase.Volley:
                {
                    if (TargetAlive) Face(target.transform.position, dt);
                    float k = Mathf.Clamp01((now - phaseStart) / Mathf.Max(0.01f, phaseUntil - phaseStart));
                    SetWarn(Mathf.Clamp01(k * 1.6f), warnColor);
                    if (now < phaseUntil) break;
                    Play("Lancar", 0.05f);
                    for (int i = 0; i < orbs.Length; i++) orbLaunch[i] = now + i * orbGap;
                    Enter(Phase.Recover, volleyRecover + orbs.Length * orbGap);
                    break;
                }
                case Phase.Summon:
                    if (TargetAlive) Face(target.transform.position, dt);
                    if (!soulsCalled && now >= phaseStart + (phaseUntil - phaseStart) * 0.6f) CallSouls();
                    if (now >= phaseUntil) Enter(Phase.Fading, fadeSeconds);
                    break;
                case Phase.Fury:
                    if (now < phaseUntil) break;
                    nextSummon = now;
                    SetSolid(true);
                    Enter(Phase.Summon, summonSeconds);
                    break;
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
            UpdateGlow();
        }

        /// <summary>Sólido de novo: chama as almas (fúria), joga o fogo-fátuo (de longe), as almas do chão ou vai dar a cabeçada.</summary>
        void ChooseMove(float now)
        {
            if (!TargetAlive)
            {
                Enter(Phase.Fading, fadeSeconds);
                return;
            }
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (enraged && now >= nextSummon && CountSoulsUp() < souls.Length)
            {
                Enter(Phase.Summon, summonSeconds);
                return;
            }
            if (d > volleyFrom && now >= nextVolley && orbs.Length > 0)
            {
                nextVolley = now + volleyCooldown;
                Enter(Phase.Volley, volleyWindUp);
                return;
            }
            if (now >= nextLament && Random.value < (enraged ? 0.45f : 0.35f))
            {
                nextLament = now + lamentCooldown * (enraged ? 0.7f : 1f);
                Enter(Phase.Lament, lamentSeconds);
                return;
            }
            Enter(Phase.Solid, solidSeconds);
        }

        void UpdateStrike(float dt, float now)
        {
            if (!TargetAlive) return;
            Face(target.transform.position, dt);
            float left = phaseUntil - now;
            float k = (now - phaseStart) / Mathf.Max(0.01f, phaseUntil - phaseStart);
            SetWarn(Mathf.Clamp01(k * 1.6f) * (left <= flashLead ? flashBoost : 1f), warnColor);
            if (left <= flashLead && !flashed)
            {
                flashed = true;
                if (wisps) wisps.Emit(16);
            }
            if (left <= swingLead && !swung)
            {
                swung = true;
                Play("Golpe", 0.05f);
            }
            // Avança na cabeçada (os últimos ~0,35 s).
            if (left <= 0.35f)
            {
                Vector3 flat = target.transform.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude > 1.3f) MoveTo(transform.position + flat.normalized * 0.6f, 5f, dt);
            }
            if (now < phaseUntil || struck) return;
            struck = true;
            Raise(Cue.Strike);
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            var guard = d.magnitude <= strikeRange + 0.6f
                ? Strike(strikeDamage * (enraged ? 1.15f : 1f), TargetChest - d.normalized * 0.3f) : CharacterCombat.GuardResult.None;
            if (guard == CharacterCombat.GuardResult.Parried)
            {
                Play("Levar", 0.05f);
                Enter(Phase.Stunned, stunSeconds);
            }
            else Enter(Phase.Recover, recoverSeconds);
        }

        /// <summary>As almas estouram do chão no círculo: quem está dentro leva (a guarda não segura; rolando, nada).</summary>
        void Burst()
        {
            Raise(Cue.Burst);
            if (effects) effects.Unearth(lamentCenter, true);
            if (ring) ring.enabled = false;
            if (wisps)
            {
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < 18; i++)
                {
                    float a = i / 18f * Mathf.PI * 2f;
                    emit.position = lamentCenter + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * lamentRadius * Random.Range(0.3f, 1f);
                    emit.velocity = Vector3.up * Random.Range(2f, 4f);
                    wisps.Emit(emit, 1);
                }
            }
            if (!TargetAlive) return;
            Vector3 flat = target.transform.position - lamentCenter;
            flat.y = 0f;
            if (flat.magnitude > lamentRadius + 0.25f) return;
            Strike(lamentDamage * (enraged ? 1.15f : 1f), TargetChest, Knockdown.Stumble, lamentCenter, unblockable: true);
        }

        void CallSouls()
        {
            soulsCalled = true;
            nextSummon = Time.time + summonEvery;
            foreach (var s in souls)
            {
                if (!s || (s.IsAwake && !s.IsDead)) continue;
                s.ResetToPost();
                s.Wake(target);
                if (effects) effects.Unearth(s.transform.position, true);
            }
        }

        int CountSoulsUp()
        {
            int n = 0;
            foreach (var s in souls)
                if (s && s.IsAwake && !s.IsDead) n++;
            return n;
        }

        // ---------- Fogo-fátuo ----------

        void UpdateOrbs(float dt, float now)
        {
            for (int i = 0; i < orbs.Length; i++)
            {
                var orb = orbs[i];
                if (!orb) continue;
                if (orbLaunch[i] > 0f && now >= orbLaunch[i])
                {
                    orbLaunch[i] = 0f;
                    if (phase == Phase.Dead || !TargetAlive) continue;
                    // Sai das mãos, um pouco para o lado de cada uma, e vai para o peito do capitão.
                    Vector3 side = transform.right * ((i % 2 == 0 ? 1f : -1f) * 0.7f);
                    orb.position = transform.position + Vector3.up * 1.9f + transform.forward * 0.9f + side;
                    orbVelocity[i] = (TargetChest - orb.position).normalized * orbSpeed + Vector3.up * 1.2f + side * 1.5f;
                    orbDeath[i] = now + orbLife;
                    orb.gameObject.SetActive(true);
                    Raise(Cue.Strike);
                }
                if (!orb.gameObject.activeSelf) continue;
                if (now >= orbDeath[i] || !TargetAlive)
                {
                    PopOrb(i);
                    continue;
                }
                Vector3 want = (TargetChest - orb.position).normalized * orbSpeed;
                orbVelocity[i] = Vector3.RotateTowards(orbVelocity[i], want, orbTurn * Mathf.Deg2Rad * dt, orbSpeed * dt);
                orbVelocity[i] = orbVelocity[i].normalized * orbSpeed;
                orb.position += orbVelocity[i] * dt;
                if ((TargetChest - orb.position).sqrMagnitude > orbReach * orbReach) continue;
                Strike(orbDamage, orb.position, Knockdown.None, orb.position - orbVelocity[i].normalized);
                PopOrb(i);
            }
        }

        void PopOrb(int i)
        {
            if (wisps)
            {
                var emit = new ParticleSystem.EmitParams { position = orbs[i].position, applyShapeToPosition = false };
                wisps.Emit(emit, 8);
            }
            orbs[i].gameObject.SetActive(false);
        }

        // ---------- Ajuda ----------

        /// <summary>Lugar livre no salão perto do capitão: no NavMesh, à vista dele, dentro da sala.</summary>
        Vector3 PickSpot()
        {
            if (!TargetAlive) return post;
            Vector3 c = target.transform.position;
            var room = Encounter ? Encounter.Area : new Bounds(post, Vector3.one * 30f);
            room.Expand(new Vector3(-2f, 0f, -2f));
            for (int i = 0; i < 16; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(appearDistance.x, appearDistance.y);
                if (!room.Contains(new Vector3(p.x, room.center.y, p.z))) continue;
                if (!NavMesh.SamplePosition(p, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                if (NavMesh.Raycast(c, hit.position, out _, NavMesh.AllAreas)) continue;
                return hit.position + Vector3.up * hover;
            }
            return post;
        }

        void DrawRing(float k)
        {
            if (!ring || ringPoints.Length == 0) return;
            // O círculo cheio desde o começo, mais grosso e claro até a hora de sair.
            Vector3 c = lamentCenter + Vector3.up * 0.06f;
            int n = ringPoints.Length;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)(n - 1) * Mathf.PI * 2f;
                ringPoints[i] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * lamentRadius;
            }
            ring.SetPositions(ringPoints);
            float w = Mathf.Lerp(0.06f, 0.2f, k);
            ring.startWidth = ring.endWidth = w;
            var col = Color.Lerp(new Color(0.35f, 0.7f, 1f, 0.6f), new Color(0.85f, 1f, 1f, 1f), k * k);
            ring.startColor = ring.endColor = col;
        }

        void MoveTo(Vector3 goal, float speed, float dt) => transform.position = Vector3.MoveTowards(transform.position, goal, speed * dt);

        void Face(Vector3 point, float dt)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 240f * dt);
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
            if (glow) glow.enabled = visible;
            if (halo) halo.gameObject.SetActive(visible);
        }

        void SetAlpha(float a)
        {
            if (Mathf.Abs(a - alpha) < 0.004f) return;
            alpha = a;
            ApplyColor();
        }

        void SetWarn(float w, Color tint)
        {
            if (Mathf.Abs(w - warn) < 0.01f && tint == warnTint) return;
            warn = w;
            warnTint = tint;
            ApplyColor();
        }

        void ApplyColor()
        {
            for (int i = 0; i < materials.Length; i++)
            {
                var start = enraged ? Color.Lerp(baseColors[i], furyColor, 0.55f) : baseColors[i];
                var c = Color.Lerp(start, warnTint, Mathf.Min(1f, warn));
                if (warn > 1f) c *= warn;
                c.a = baseColors[i].a * Mathf.Max(0f, alpha);
                materials[i].SetColor(BaseColorId, c);
            }
        }

        void UpdateGlow()
        {
            if (!glow) return;
            glow.intensity = glowIntensity * Mathf.Clamp01(alpha / solidAlpha + 0.25f) * (1f + warn * 0.5f) * (enraged ? 1.3f : 1f);
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBosses()
        {
            bosses.Clear();
            Enraged = null;
            Defeated = null;
        }
    }
}
