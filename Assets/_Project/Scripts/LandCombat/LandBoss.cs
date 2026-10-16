using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Chefe a pé (pacote B, passo 4, 10/10: o Capitão Barba-Negra, de espadão, nas ruínas da Caveira). Fica junto do
    /// <see cref="EnemyFootAI"/>, que pergunta aqui qual golpe dar (<see cref="Choose"/>): cortes em sequência de perto, giro em volta
    /// quando o capitão está atrás dele ou bate sem parar, chute quando o capitão segura a guarda, salto de longe; o giro, o salto e o
    /// chute são imparáveis (passam pela guarda) e o chefe brilha em vermelho no preparo. Nada o derruba nem o interrompe; aparar abre
    /// a guarda dele. Com metade da vida: fase 2 (<see cref="Phase"/>), invencível enquanto se enfurece, solta uma onda de choque que
    /// derruba o capitão por perto e volta mais forte, mais rápido e com a sequência de três cortes. Se o capitão foge, volta ao posto
    /// com a vida cheia e na fase 1. A barra no topo da tela (BossBarHUD) aparece enquanto ele luta.
    /// </summary>
    [RequireComponent(typeof(EnemyFootAI))]
    public class LandBoss : MonoBehaviour
    {
        /// <summary>Um golpe do chefe: o AttackVariant da arma e quando ele vale.</summary>
        [Serializable]
        public class Move
        {
            public string name;
            [Tooltip("Golpe da arma (AttackVariant).")]
            public int variant;
            [Tooltip("Distância do capitão (m) em que este golpe pode sair.")]
            public float minRange, maxRange = 3f;
            [Tooltip("Peso no sorteio (entre os que valem).")]
            public float weight = 1f;
            [Tooltip("A partir de que fase.")]
            public int phase = 1;
            [Tooltip("Segundos até poder repetir este golpe.")]
            public float cooldown;
            [Tooltip("Segundos parado depois do último acerto (a brecha para o capitão).")]
            public float recovery = 0.8f;
            [Tooltip("Pode emendar outro corte depois dele (combo).")]
            public bool combo;
            [Tooltip("Resposta: capitão atrás dele ou batendo sem parar.")]
            public bool breaker;
            [Tooltip("Resposta: capitão segurando a guarda.")]
            public bool guardBreaker;
            [NonSerialized] public float readyAt;
        }

        public static readonly List<LandBoss> Active = new();
        /// <summary>Entrou na fase 2 (aviso no HUD, som).</summary>
        public static event Action<LandBoss> Enraged;
        /// <summary>Caiu (aviso no HUD).</summary>
        public static event Action<LandBoss> Defeated;

        [SerializeField] string bossName = "Capitão Barba-Negra";
        [SerializeField] string bossTitle = "o Carniceiro das Ruínas";

        [Header("Golpes")]
        [SerializeField] Move[] moves = Array.Empty<Move>();
        [Tooltip("Cortes que podem emendar num combo (AttackVariant).")]
        [SerializeField] int[] comboVariants = Array.Empty<int>();
        [Tooltip("Golpes levados em pouco tempo que fazem ele responder com o giro.")]
        [SerializeField] int hitsToBreak = 3;
        [SerializeField] float hitsWindow = 2.5f;
        [Tooltip("Ângulo (graus a partir da frente dele) a partir do qual o capitão está 'atrás'.")]
        [SerializeField] float behindAngle = 110f;
        [Tooltip("Segundos em que ele lembra que o capitão esteve atrás dele (rolou para as costas na brecha: quando fica pronto, já virou).")]
        [SerializeField] float behindMemory = 1.5f;

        [Header("Fase 2")]
        [SerializeField, Range(0.1f, 0.9f)] float phaseTwoAt = 0.5f;
        [Tooltip("Segundos parado e invencível se enfurecendo (Power Up).")]
        [SerializeField] float powerUpSeconds = 2.6f;
        [Tooltip("Segundos do começo do Power Up até a onda de choque.")]
        [SerializeField] float shockAt = 1.25f;
        [SerializeField] float shockRadius = 5.5f, shockDamage = 15f, shockPush = 9f, shockKnockdownWithin = 3.5f;
        [SerializeField] float damageMultiplier = 1.2f, speedMultiplier = 1.12f;
        [Tooltip("Pausa entre golpes, combo e golpes emendados: fase 1 e fase 2.")]
        [SerializeField] Vector2 pauseOne = new(0.5f, 1.1f), pauseTwo = new(0.25f, 0.6f);
        [SerializeField, Range(0f, 1f)] float comboOne = 0.45f, comboTwo = 0.7f;
        [SerializeField, Min(1)] int maxComboOne = 2, maxComboTwo = 3;
        [Tooltip("Fração da brecha (recovery) depois de cada golpe na fase 2.")]
        [SerializeField, Range(0.2f, 1f)] float recoveryTwo = 0.65f;

        [Header("Guarda-costas")]
        [Tooltip("Fica no posto, encarando, enquanto houver outro do acampamento de pé: os guarda-costas lutam primeiro (B.4, passo 2).")]
        [SerializeField] bool waitForGuards;
        [Tooltip("Capitão mais perto que isto (m) faz ele entrar na luta mesmo com guarda-costas de pé (e golpe nele também).")]
        [SerializeField] float engageRadius = 5f;

        [Header("Visual")]
        [Tooltip("Corpo (os olhos brilham pela emissão do material).")]
        [SerializeField] Renderer[] body = Array.Empty<Renderer>();
        [Tooltip("Lâmina do espadão: troca para o material brilhante no aviso e na fase 2.")]
        [SerializeField] Renderer[] blade = Array.Empty<Renderer>();
        [SerializeField] Material bladeGlow;
        [Tooltip("Brasas vermelhas em volta dele na fase 2 (e a rajada da onda de choque).")]
        [SerializeField] ParticleSystem aura;
        [SerializeField] Color eyeColor = new(1f, 0.12f, 0.05f);
        [SerializeField] float eyeCalm = 1.2f, eyeFury = 3f, eyeFlare = 6f;

        EnemyFootAI ai;
        Health health;
        FootCharacter character;
        CharacterCombat combat;
        FootHealthReactions reactions;
        CharacterAnimatorDriver driver;
        readonly Queue<float> recentHits = new();
        float powerUpStart = float.NegativeInfinity, flareUntil = float.NegativeInfinity, behindAt = float.NegativeInfinity;
        bool shocked = true, shielded, released;
        int lastCombo = -1;
        float walk, run;
        Material[] bladeNormal = Array.Empty<Material>();
        readonly List<Material> eyeMaterials = new();
        float shownEye = -1f;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public string DisplayName => bossName;
        public string Title => bossTitle;
        public Health Health => health;
        public int Phase { get; private set; } = 1;
        public bool IsPoweringUp => Time.time < powerUpStart + powerUpSeconds;
        /// <summary>Lutando (viu o capitão e está atrás dele, vivo): a barra do chefe aparece.</summary>
        public bool IsEngaged => ai && ai.IsAlerted && !health.IsDead;
        /// <summary>Fração da pausa/brecha na fase atual.</summary>
        public float RecoveryScale => Phase >= 2 ? recoveryTwo : 1f;
        public IReadOnlyList<Move> Moves => moves;
        /// <summary>Esperando no posto enquanto os guarda-costas lutam (a IA não sai nem ouve o alarme).</summary>
        public bool HoldingBack => waitForGuards && !released && GuardsStanding();

        public void SetWaitForGuards(bool wait, float radius)
        {
            waitForGuards = wait;
            engageRadius = radius;
        }

        /// <summary>Entra na luta (guarda-costas caídos, capitão colado ou golpe nele): o grito de guerra e a barra do chefe.</summary>
        public void Release()
        {
            if (released || !waitForGuards) return;
            released = true;
            ai.HearAlarm();
        }

        bool GuardsStanding()
        {
            var camp = ai.Camp;
            if (!camp) return false;
            var members = camp.Members;
            for (int i = 0; i < members.Count; i++)
                if (members[i] && members[i] != ai && !members[i].IsDead) return true;
            return false;
        }

        public void Configure(string name, string title, Move[] bossMoves, int[] combos, Renderer[] bodyRenderers, Renderer[] bladeRenderers,
            Material glow, ParticleSystem auraSystem)
        {
            bossName = name;
            bossTitle = title;
            moves = bossMoves ?? Array.Empty<Move>();
            comboVariants = combos ?? Array.Empty<int>();
            body = bodyRenderers ?? Array.Empty<Renderer>();
            blade = bladeRenderers ?? Array.Empty<Renderer>();
            bladeGlow = glow;
            aura = auraSystem;
        }

        void Awake()
        {
            ai = GetComponent<EnemyFootAI>();
            health = GetComponent<Health>();
            character = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            reactions = GetComponent<FootHealthReactions>();
            driver = GetComponentInChildren<CharacterAnimatorDriver>();
            walk = character.WalkSpeed;
            run = character.RunSpeed;
            bladeNormal = new Material[blade.Length];
            for (int i = 0; i < blade.Length; i++) bladeNormal[i] = blade[i] ? blade[i].sharedMaterial : null;
            // Cópia do material do corpo (só deste personagem): os olhos brilham mais ou menos sem mexer no asset.
            foreach (var r in body)
                if (r)
                {
                    var mats = r.materials;
                    foreach (var m in mats)
                        if (m.HasProperty(EmissionColor))
                        {
                            m.EnableKeyword("_EMISSION");
                            eyeMaterials.Add(m);
                        }
                }
        }

        void OnEnable()
        {
            Active.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            combat.Attacked += OnAttacked;
            ApplyPhase();
        }

        void OnDisable()
        {
            Active.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            combat.Attacked -= OnAttacked;
        }

        void OnDestroy()
        {
            foreach (var m in eyeMaterials) if (m) Destroy(m);
        }

        /// <summary>
        /// Golpe para agora, ou null (anda/espera). Respostas primeiro: capitão atrás ou batendo sem parar = giro; segurando a guarda de
        /// perto = chute; longe = salto. Senão, sorteia entre os cortes que alcançam.
        /// </summary>
        public Move Choose(float distance, Transform target, bool targetBlocking)
        {
            if (moves.Length == 0) return null;
            while (recentHits.Count > 0 && Time.time - recentHits.Peek() > hitsWindow) recentHits.Dequeue();
            bool behind = Time.time - behindAt < behindMemory;
            bool pressed = recentHits.Count >= hitsToBreak;

            Move pick = null;
            if (behind || pressed) pick = Best(distance, m => m.breaker);
            if (pick == null && targetBlocking && UnityEngine.Random.value < 0.75f) pick = Best(distance, m => m.guardBreaker);
            if (pick != null)
            {
                recentHits.Clear();
                behindAt = float.NegativeInfinity;
                return pick;
            }
            float total = 0f;
            foreach (var m in moves) if (Valid(m, distance) && !m.breaker && !m.guardBreaker) total += m.weight;
            if (total <= 0f) return null;
            float roll = UnityEngine.Random.value * total;
            foreach (var m in moves)
            {
                if (!Valid(m, distance) || m.breaker || m.guardBreaker) continue;
                roll -= m.weight;
                if (roll <= 0f) return m;
            }
            return null;
        }

        Move Best(float distance, Func<Move, bool> kind)
        {
            foreach (var m in moves) if (kind(m) && Valid(m, distance)) return m;
            return null;
        }

        bool Valid(Move m, float distance) =>
            Phase >= m.phase && Time.time >= m.readyAt && distance >= m.minRange && distance <= m.maxRange;

        /// <summary>Sandbox (F1): pula para a fase 2 agora (lutando).</summary>
        public void EnrageNow()
        {
            if (Phase != 1 || health.IsDead) return;
            health.SetCurrent(health.Max * phaseTwoAt);
            BeginPowerUp();
        }

        /// <summary>O golpe saiu: começa a espera dele.</summary>
        public void Used(Move m) => m.readyAt = Time.time + m.cooldown;

        /// <summary>Próximo corte do combo (diferente do anterior).</summary>
        public int ComboVariant()
        {
            if (comboVariants.Length == 0) return 0;
            int pick = comboVariants[UnityEngine.Random.Range(0, comboVariants.Length)];
            if (pick == lastCombo && comboVariants.Length > 1) pick = comboVariants[(Array.IndexOf(comboVariants, pick) + 1) % comboVariants.Length];
            lastCombo = pick;
            return pick;
        }

        void OnDamaged(DamageInfo info)
        {
            Release();
            if (health.IsDead) return;
            recentHits.Enqueue(Time.time);
            if (Phase == 1 && health.Normalized <= phaseTwoAt) BeginPowerUp();
        }

        void BeginPowerUp()
        {
            // Um golpe grande não pula a fase 2: a vida fica na marca.
            if (health.Normalized < phaseTwoAt) health.SetCurrent(health.Max * phaseTwoAt);
            Phase = 2;
            powerUpStart = Time.time;
            shocked = false;
            shielded = true;
            health.Invulnerable = true;
            combat.Interrupt(powerUpSeconds);
            if (driver) driver.PlayPowerUp();
            ai.Roar();
            ApplyPhase();
            if (aura) aura.Emit(60);
            Enraged?.Invoke(this);
        }

        void OnDied()
        {
            if (aura) aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetBladeGlow(false);
            Defeated?.Invoke(this);
        }

        /// <summary>Golpe imparável começando: brilho vermelho na lâmina e nos olhos até o impacto.</summary>
        void OnAttacked(WeaponDefinition weapon, int variant)
        {
            var strike = weapon ? weapon.StrikeFor(variant) : null;
            if (strike == null || !strike.unblockable) return;
            flareUntil = Time.time + weapon.HitDelayFor(variant);
            if (aura) aura.Emit(12);
        }

        void Update()
        {
            // Capitão perto e nas costas dele: guarda o momento (o giro responde mesmo depois de ele virar).
            var target = ai.Target;
            if (target && ai.IsAlerted)
            {
                Vector3 to = target.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 4.5f * 4.5f && to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > behindAngle) behindAt = Time.time;
            }
            if (waitForGuards && !health.IsDead)
            {
                bool standing = GuardsStanding();
                if (!released && (!standing || (target && target.gameObject.activeInHierarchy
                        && (target.position - transform.position).sqrMagnitude < engageRadius * engageRadius)))
                    Release();
                // O capitão fugiu com os guarda-costas de pé (ou eles voltaram): espera de novo.
                else if (released && standing && !ai.IsAlerted) released = false;
            }
            // Fugiu dele (voltou ao posto) ou levantou de novo: vida cheia e fase 1.
            if (!health.IsDead && !ai.IsAlerted && !IsPoweringUp && (Phase != 1 || health.Current < health.Max)) ResetFight();

            if (!shocked && Time.time >= powerUpStart + shockAt)
            {
                shocked = true;
                Shockwave();
            }
            if (shielded && !IsPoweringUp)
            {
                shielded = false;
                health.Invulnerable = false;
            }

            bool flaring = Time.time < flareUntil || IsPoweringUp;
            SetBladeGlow(flaring || Phase >= 2);
            float eye = health.IsDead ? 0f : flaring ? eyeFlare : Phase >= 2 ? eyeFury : eyeCalm;
            if (!Mathf.Approximately(eye, shownEye))
            {
                shownEye = eye;
                foreach (var m in eyeMaterials) m.SetColor(EmissionColor, eyeColor * eye);
            }
        }

        /// <summary>Onda de choque do Power Up: empurra e derruba o capitão por perto.</summary>
        void Shockwave()
        {
            if (aura) aura.Emit(80);
            if (Services.TryGet(out CombatEffects effects)) effects.Unearth(transform.position, false);
            var target = ai.Target;
            if (!target || !target.gameObject.activeInHierarchy) return;
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > shockRadius) return;
            Vector3 away = distance > 0.01f ? to / distance : transform.forward;
            float t = distance / shockRadius;
            if (target.TryGetComponent(out IDamageable damageable))
                damageable.TakeDamage(new DamageInfo
                {
                    amount = shockDamage, point = target.position + Vector3.up, direction = away, source = gameObject,
                    knockdown = distance <= shockKnockdownWithin ? Knockdown.Blast : Knockdown.None,
                });
            if (target.TryGetComponent(out IPushable pushable)) pushable.Push(away * shockPush * (1f - 0.5f * t));
        }

        void ResetFight()
        {
            Phase = 1;
            powerUpStart = float.NegativeInfinity;
            shocked = true;
            if (shielded) health.Invulnerable = false;
            shielded = false;
            if (!health.IsDead) health.SetCurrent(health.Max);
            recentHits.Clear();
            foreach (var m in moves) m.readyAt = 0f;
            ApplyPhase();
        }

        /// <summary>Números da fase: dano, velocidade, pausas e combos; brasas da aura.</summary>
        void ApplyPhase()
        {
            bool two = Phase >= 2;
            combat.DamageMultiplier = two ? damageMultiplier : 1f;
            character.ConfigureSpeeds(walk * (two ? speedMultiplier : 1f), run * (two ? speedMultiplier : 1f));
            ai.SetTempo(two ? pauseTwo : pauseOne, two ? comboTwo : comboOne, two ? maxComboTwo : maxComboOne);
            if (reactions) reactions.SetUnstoppable(true);
            if (aura)
            {
                if (two && !aura.isPlaying) aura.Play(true);
                if (!two && aura.isPlaying) aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void SetBladeGlow(bool on)
        {
            if (!bladeGlow) return;
            for (int i = 0; i < blade.Length; i++)
            {
                if (!blade[i]) continue;
                var want = on ? bladeGlow : bladeNormal[i];
                if (blade[i].sharedMaterial != want) blade[i].sharedMaterial = want;
            }
        }
    }
}
