using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Pesca com vara (D2, passo 1, 27/10): a pé, com a vara de pesca (item comprado no mercado, guardado em qualquer lugar) e de frente
    /// para a água funda a poucos metros (beira da praia, píer, cais), F lança ("Fishing Cast" do Mixamo): a boia voa até a água e boia
    /// com as ondas; às vezes um peixe só belisca (a boia treme) e, depois de alguns segundos, morde de verdade (a boia afunda, "!");
    /// F (ou Espaço) na hora fisga e começa a <b>briga com o peixe</b> (ajuste de 27/10, pedido do Jhonata: "que nem é no Stardew Valley"):
    /// uma barra verde sobe enquanto Espaço (ou o botão esquerdo) fica apertado e cai quando solta; o peixe sobe e desce sozinho na faixa
    /// de água, e com ele dentro da barra o medidor ao lado enche (fora, esvazia). Cheio = o capitão puxa, o peixe sai da água pendurado
    /// na linha, aparece sobre a cabeça dele e vai para a mochila; vazio = o peixe escapa. Quanto mais raro, menor a barra, mais o peixe
    /// foge e mais rápido o medidor esvazia. F cedo demais (beliscada) ou tarde demais = o peixe leva a isca. Esc recolhe (na briga,
    /// solta o peixe); andar, levar golpe ou cair também. Animações de pesca do Human Crafting Animations (Kevin Iglesias).
    /// Peixe sorteado por peso, com mais peixes de praia no raso e mais dos grandes no fundo; quanto mais raro, menor a janela para fisgar.
    /// Fica no capitão (só liga em terra). Não pesca com piratas atrás dele nem com a mochila cheia; perde para qualquer outra
    /// interação do F que esteja disponível ali (baú, bote, alavanca).
    /// </summary>
    public class FishingRod : MonoBehaviour, IPagedInteraction
    {
        public enum Phase { Idle, Casting, Waiting, Bite, Fight, Reeling, Showing }

        /// <summary>Um peixe que pode morder: item, modelo (desligado, mostrado ao pescar), peso no sorteio e quanto o raso/o fundo mudam isso.</summary>
        [Serializable]
        public class Catch
        {
            public ItemDefinition item;
            public GameObject model;
            [Min(0f)] public float weight = 1f;
            [Tooltip("Peso × isto com a boia no raso (água até Shallow Depth).")]
            [Min(0f)] public float shallow = 1f;
            [Tooltip("Peso × isto com a boia no fundo (água a partir de Deep Depth).")]
            [Min(0f)] public float deep = 1f;
        }

        [SerializeField] ItemDefinition rodItem;
        [SerializeField] Catch[] catches = Array.Empty<Catch>();

        [Header("Visual (montado pelo gerador)")]
        [SerializeField] Transform rod;
        [SerializeField] Transform tip;
        [SerializeField] Transform bobber;
        [SerializeField] LineRenderer line;
        [Tooltip("Onde o peixe aparece pendurado (filho do capitão, acima da cabeça).")]
        [SerializeField] Transform showPoint;

        [Header("Onde lança")]
        [SerializeField] float castMin = 3.5f, castMax = 7.5f;
        [Tooltip("Água mínima sob a boia (m): menos que isso é areia molhada.")]
        [SerializeField] float minDepth = 0.6f;
        [SerializeField] float shallowDepth = 1.4f, deepDepth = 2.6f;
        [Tooltip("Pirata em alerta a menos disto (m) não deixa pescar.")]
        [SerializeField] float enemyRange = 22f;
        [Tooltip("Espaço livre mínimo na mochila (o peixe maior ocupa 1).")]
        [SerializeField] float minFreeSpace = 1f;

        [Header("Tempos (s)")]
        [Tooltip("Do F até a linha sair (\"FishingThrow01\": a vara chicoteia para a frente aos ~0,5 s).")]
        [SerializeField] float releaseAt = 0.5f;
        [SerializeField] float flightSeconds = 0.55f;
        [SerializeField] Vector2 waitRange = new(5f, 12f);
        [Tooltip("Janela para fisgar por raridade (comum, incomum, raro, épico, lendário).")]
        [SerializeField] float[] hookWindow = { 0.95f, 0.8f, 0.65f, 0.5f, 0.45f };
        [Tooltip("Puxar (\"FishingPullOut01\": o puxão para cima vai até ~0,8 s): o peixe (ou a boia) voa da água até a ponta da vara.")]
        [SerializeField] float reelSeconds = 0.85f;
        [SerializeField] float showSeconds = 1.6f;
        [Tooltip("A vara continua na mão um instante depois de o peixe chegar (as mãos ainda estão descendo).")]
        [SerializeField] float rodLinger = 0.55f;

        [Header("Briga com o peixe (como no Stardew Valley); 0–1 = altura da faixa de água")]
        [Tooltip("Tamanho da barra verde por raridade (comum, incomum, raro, épico, lendário).")]
        [SerializeField] float[] barSize = { 0.32f, 0.27f, 0.23f, 0.19f, 0.17f };
        [Tooltip("Quanto o peixe foge (0 = parado, 1 = muito) por raridade.")]
        [SerializeField] float[] fishAgility = { 0.3f, 0.48f, 0.66f, 0.84f, 1f };
        [Tooltip("Quanto o medidor esvazia por segundo com o peixe fora da barra, por raridade.")]
        [SerializeField] float[] progressLoss = { 0.16f, 0.19f, 0.23f, 0.27f, 0.3f };
        [Tooltip("Quanto o medidor enche por segundo com o peixe dentro da barra.")]
        [SerializeField] float progressGain = 0.26f;
        [SerializeField] float startProgress = 0.3f;
        [Tooltip("Começo da briga sem esvaziar (para o jogador achar o peixe).")]
        [SerializeField] float fightGrace = 0.6f;
        [Tooltip("Barra: aceleração para cima segurando, gravidade soltando, velocidade máxima e o quique no fundo.")]
        [SerializeField] float barLift = 3.4f, barGravity = 2.8f, barMaxSpeed = 1.5f, barBounce = 0.35f;

        Inventory backpack;
        Health health;
        FootCharacter footCharacter;
        CharacterCombat combat;
        CharacterAnimatorDriver animatorDriver;
        Animator animator;
        Transform rightHand, leftHand, chest;
        IOceanSampler ocean;
        IInteraction[] others = Array.Empty<IInteraction>();
        readonly List<Renderer> hiddenWeapon = new();
        readonly Vector3[] linePoints = new Vector3[12];

        float phaseUntil, nextScan, biteUntil, nextNibble, nibbleUntil, castAt;
        bool landed;
        Vector3 target, castFrom, facing, showFrom;
        float targetDepth;
        Catch hooked;
        bool reelingFish;
        float fightStart, fishVelocity, fishTarget, nextFishTarget, barVelocity, nextFightSplash, landedAt;
        bool? holdOverride;

        public Phase State { get; private set; }
        /// <summary>Tem um lugar de água à frente para lançar (atualizado a cada 0,2 s).</summary>
        public bool SpotAhead { get; private set; }
        /// <summary>O jogador tem a vara (em qualquer lugar: mochila, porão ou armazém).</summary>
        public bool HasRod => rodItem && PlayerStorage.Count(rodItem) > 0;
        /// <summary>Por que não dá para pescar aqui agora (vazio = dá ou não há água à frente).</summary>
        public string BlockReason { get; private set; } = "";
        /// <summary>O peixe que está na linha (mordendo, vindo ou mostrado).</summary>
        public ItemDefinition Hooked => hooked?.item;
        public Vector3 BobberPosition => bobber ? bobber.position : transform.position;
        /// <summary>Segundos que faltam para fisgar (só na mordida).</summary>
        public float BiteLeft => State == Phase.Bite ? Mathf.Max(0f, biteUntil - Time.time) : 0f;
        public ItemDefinition RodItem => rodItem;

        /// <summary>Briga: onde o peixe está (0 embaixo, 1 em cima da faixa de água).</summary>
        public float FightFish { get; private set; }
        /// <summary>Briga: base da barra verde (0–1) e o tamanho dela.</summary>
        public float FightBar { get; private set; }
        public float FightBarSize { get; private set; }
        /// <summary>Briga: medidor (cheio = pescou, vazio = escapou).</summary>
        public float FightProgress { get; private set; }
        /// <summary>Briga: o peixe está dentro da barra agora.</summary>
        public bool FightInside { get; private set; }
        /// <summary>Briga: o jogador está segurando (a barra sobe).</summary>
        public bool FightHolding { get; private set; }

        public bool IsOpen => State != Phase.Idle;
        public bool CanOpen => State == Phase.Idle && SpotAhead && HasRod && BlockReason.Length == 0 && !OtherInteraction();

        /// <summary>Linha lançada (a boia saiu).</summary>
        public static event Action<FishingRod> CastOut;
        /// <summary>Peixe mordeu de verdade: F agora.</summary>
        public static event Action<FishingRod> Bitten;
        /// <summary>Fisgou: começa a briga.</summary>
        public static event Action<FishingRod> Hooking;
        /// <summary>Ganhou a briga: o peixe vem vindo.</summary>
        public static event Action<FishingRod> Landing;
        /// <summary>Pescou (já na mochila).</summary>
        public static event Action<FishingRod, ItemDefinition> Caught;
        /// <summary>Não veio nada: o motivo (para o aviso).</summary>
        public static event Action<FishingRod, string> Missed;

        public void Configure(ItemDefinition rodDefinition, Catch[] table, Transform rodVisual, Transform rodTip, Transform bobberVisual,
            LineRenderer fishingLine, Transform show)
        {
            rodItem = rodDefinition;
            catches = table;
            rod = rodVisual;
            tip = rodTip;
            bobber = bobberVisual;
            line = fishingLine;
            showPoint = show;
        }

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            health = GetComponent<Health>();
            footCharacter = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            animatorDriver = GetComponentInChildren<CharacterAnimatorDriver>(true);
            animator = GetComponentInChildren<Animator>(true);
            if (animator && animator.isHuman)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (!chest) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            var all = GetComponents<IInteraction>();
            var list = new List<IInteraction>();
            foreach (var i in all) if (!ReferenceEquals(i, this)) list.Add(i);
            others = list.ToArray();
            HideGear();
        }

        void OnEnable()
        {
            if (health) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health) health.Damaged -= OnDamaged;
            if (State != Phase.Idle && animatorDriver) animatorDriver.SetFishing(false);
            State = Phase.Idle;
            hooked = null;
            HideGear();
            ShowWeapon();
        }

        bool OtherInteraction()
        {
            for (int i = 0; i < others.Length; i++)
                if (others[i] != null && others[i].CanOpen) return true;
            return false;
        }

        void OnDamaged(DamageInfo info)
        {
            if (State is Phase.Casting or Phase.Waiting or Phase.Bite) Abort("levou um golpe: a linha soltou");
            else if (State == Phase.Fight) Abort("levou um golpe: o peixe escapou", reel: true);
        }

        void Update()
        {
            if (ocean == null) Services.TryGet(out ocean);
            if (State == Phase.Idle)
            {
                if (Time.time >= nextScan)
                {
                    nextScan = Time.time + 0.2f;
                    Scan();
                }
                return;
            }

            // Pescando: parado, virado para a água, sem atacar.
            if (footCharacter)
            {
                footCharacter.SetSpeedMultiplier(0f, 0.25f);
                footCharacter.FaceDuring(facing, 0.25f);
            }
            if (combat) combat.Occupy(0.25f);
            if (State is Phase.Casting or Phase.Waiting or Phase.Bite)
            {
                bool moving = GameInput.Instance.Move.ReadValue<Vector2>().sqrMagnitude > 0.16f;
                if (moving || (footCharacter && (footCharacter.IsDown || footCharacter.IsDodging)) || (health && health.IsDead))
                {
                    Abort(moving ? "" : "a linha soltou");
                    return;
                }
            }
            else if (State == Phase.Fight && ((footCharacter && (footCharacter.IsDown || footCharacter.IsDodging)) || (health && health.IsDead)))
            {
                Abort("o peixe escapou", reel: true);
                return;
            }

            switch (State)
            {
                case Phase.Casting:
                    if (Time.time >= phaseUntil) BeginWaiting();
                    break;
                case Phase.Waiting:
                    if (!landed && Flight >= 1f)
                    {
                        landed = true;
                        Plop(1f);
                    }
                    if (Time.time >= nextNibble)
                    {
                        nextNibble = Time.time + UnityEngine.Random.Range(1.6f, 3.2f);
                        if (Time.time + 0.8f < phaseUntil && UnityEngine.Random.value < 0.55f)
                        {
                            nibbleUntil = Time.time + 0.14f;
                            Plop(0.35f);
                        }
                    }
                    if (Time.time >= phaseUntil) BeginBite();
                    break;
                case Phase.Bite:
                    // Espaço também fisga (é a tecla da briga que vem a seguir).
                    if (GameInput.Instance.Jump.WasPressedThisFrame()) Hook();
                    else if (Time.time > biteUntil) Abort("o peixe levou a isca", reel: true);
                    break;
                case Phase.Fight:
                    UpdateFight(Time.deltaTime);
                    break;
                case Phase.Reeling:
                    if (Time.time >= phaseUntil)
                    {
                        if (reelingFish && hooked != null) Land();
                        else End();
                    }
                    break;
                case Phase.Showing:
                    if (rod && rod.gameObject.activeSelf && Time.time >= landedAt + rodLinger)
                    {
                        rod.gameObject.SetActive(false);
                        ShowWeapon();
                    }
                    if (Time.time >= phaseUntil) End();
                    break;
            }
        }

        void LateUpdate()
        {
            if (State == Phase.Idle) return;
            PlaceRod();
            PlaceBobber();
            DrawLine();
            PlaceFish();
        }

        /// <summary>Procura água funda à frente (do mais longe para o mais perto) e diz por que não dá para pescar.</summary>
        void Scan()
        {
            SpotAhead = false;
            BlockReason = "";
            if (ocean == null || !footCharacter || !footCharacter.IsGrounded || footCharacter.InDeepWater) return;
            Vector3 me = transform.position;
            // Os pés na água funda ou longe dela: só na beira (o raso da praia vale).
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return;
            fwd.Normalize();
            for (float d = castMax; d >= castMin - 0.01f; d -= 0.5f)
            {
                if (!WaterAt(me + fwd * d, out Vector3 point, out float depth)) continue;
                if (Physics.Linecast(me + Vector3.up * 1.6f, point + Vector3.up * 0.3f, out _, ~0, QueryTriggerInteraction.Ignore)) continue;
                SpotAhead = true;
                target = point;
                targetDepth = depth;
                break;
            }
            if (!SpotAhead) return;
            if (!HasRod) BlockReason = "sem vara de pesca (compre no mercado)";
            else if (backpack && backpack.FreeSpace < minFreeSpace) BlockReason = "mochila cheia";
            else if (EnemiesNear()) BlockReason = "piratas por perto";
        }

        /// <summary>Água com pelo menos <see cref="minDepth"/> sob o ponto, sem nada por cima (píer, pedra).</summary>
        bool WaterAt(Vector3 at, out Vector3 point, out float depth)
        {
            float water = ocean.GetHeight(at);
            float ground = water - 20f;
            if (Physics.Raycast(new Vector3(at.x, water + 8f, at.z), Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) { point = default; depth = 0f; return false; }
                ground = hit.point.y;
            }
            depth = water - ground;
            point = new Vector3(at.x, water, at.z);
            return depth >= minDepth;
        }

        bool EnemiesNear()
        {
            var list = EnemyFootAI.Active;
            float r2 = enemyRange * enemyRange;
            Vector3 me = transform.position;
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (!ai || !ai.IsAlerted || ai.IsDead) continue;
                if ((ai.transform.position - me).sqrMagnitude < r2) return true;
            }
            return false;
        }

        /// <summary>F: lança (ou, pelo teclado, a tecla do <see cref="PlayerInteractInput"/>).</summary>
        public bool Open()
        {
            Scan();
            if (!CanOpen) return false;
            StartCast();
            return true;
        }

        void StartCast()
        {
            State = Phase.Casting;
            facing = target - transform.position;
            facing.y = 0f;
            facing.Normalize();
            transform.rotation = Quaternion.LookRotation(facing);
            phaseUntil = Time.time + releaseAt;
            hooked = null;
            reelingFish = false;
            landed = false;
            HideWeaponInHand();
            if (rod) rod.gameObject.SetActive(true);
            if (animatorDriver) animatorDriver.PlayFishCast();
        }

        /// <summary>Com a linha na água, F: fisga se o peixe está mordendo; antes disso, puxa cedo demais e recolhe vazia. Na briga não faz nada.</summary>
        public void Advance()
        {
            if (State == Phase.Bite) Hook();
            else if (State == Phase.Waiting) Abort("puxou cedo demais: o peixe fugiu", reel: true);
        }

        /// <summary>Esc: recolhe a linha (na briga, solta o peixe).</summary>
        public void Close()
        {
            if (State is Phase.Casting or Phase.Waiting or Phase.Bite) Abort("", reel: State != Phase.Casting);
            else if (State == Phase.Fight) Abort("soltou a linha: o peixe escapou", reel: true);
        }

        void BeginWaiting()
        {
            State = Phase.Waiting;
            castFrom = tip ? tip.position : transform.position + Vector3.up * 2f;
            castAt = Time.time;
            phaseUntil = Time.time + UnityEngine.Random.Range(waitRange.x, waitRange.y);
            nextNibble = Time.time + flightSeconds + UnityEngine.Random.Range(1f, 2f);
            if (bobber) bobber.gameObject.SetActive(true);
            if (line) line.enabled = true;
            CastOut?.Invoke(this);
        }

        void BeginBite()
        {
            hooked = Pick();
            if (hooked == null)
            {
                phaseUntil = Time.time + 2f;
                return;
            }
            State = Phase.Bite;
            int r = Mathf.Clamp((int)hooked.item.rarity, 0, hookWindow.Length - 1);
            biteUntil = Time.time + hookWindow[r];
            Plop(1.2f);
            Bitten?.Invoke(this);
        }

        /// <summary>Sorteio pelo peso de cada peixe, mudado pela fundura da água onde a boia caiu.</summary>
        Catch Pick()
        {
            float deepness = Mathf.InverseLerp(shallowDepth, deepDepth, targetDepth);
            float total = 0f;
            foreach (var c in catches)
                if (c != null && c.item) total += Weight(c, deepness);
            if (total <= 0f) return null;
            float roll = UnityEngine.Random.value * total;
            foreach (var c in catches)
            {
                if (c == null || !c.item) continue;
                roll -= Weight(c, deepness);
                if (roll <= 0f) return c;
            }
            return null;
        }

        static float Weight(Catch c, float deepness) =>
            c.weight * (deepness <= 0f ? c.shallow : deepness >= 1f ? c.deep : Mathf.Lerp(c.shallow, c.deep, deepness));

        /// <summary>Fisgou: começa a briga, com a barra centrada no peixe.</summary>
        void Hook()
        {
            State = Phase.Fight;
            int r = Rarity;
            FightBarSize = barSize[Mathf.Min(r, barSize.Length - 1)];
            FightFish = UnityEngine.Random.Range(0.25f, 0.45f);
            fishTarget = FightFish;
            fishVelocity = 0f;
            nextFishTarget = Time.time + 0.5f;
            FightBar = Mathf.Clamp(FightFish - FightBarSize * 0.5f, 0f, 1f - FightBarSize);
            barVelocity = 0f;
            FightProgress = startProgress;
            FightInside = true;
            fightStart = Time.time;
            nextFightSplash = Time.time + 0.3f;
            Plop(1.4f);
            if (animatorDriver) animatorDriver.PlayFishFight();
            Hooking?.Invoke(this);
        }

        int Rarity => hooked?.item ? Mathf.Max(0, (int)hooked.item.rarity) : 0;

        /// <summary>
        /// Um quadro da briga: a barra sobe segurando Espaço/botão esquerdo e cai soltando (quica no fundo); o peixe corre atrás de alvos
        /// sorteados (às vezes um pulo longo), mais longe e mais rápido quanto mais raro; o medidor enche com o peixe dentro da barra.
        /// </summary>
        void UpdateFight(float dt)
        {
            var input = GameInput.Instance;
            FightHolding = holdOverride ?? (input.Jump.IsPressed() || input.Attack.IsPressed());
            float max = 1f - FightBarSize;
            barVelocity = Mathf.Clamp(barVelocity + (FightHolding ? barLift : -barGravity) * dt, -barMaxSpeed, barMaxSpeed);
            float bar = FightBar + barVelocity * dt;
            if (bar <= 0f)
            {
                bar = 0f;
                if (barVelocity < 0f) barVelocity = -barVelocity * barBounce;
                if (barVelocity < 0.05f) barVelocity = 0f;
            }
            else if (bar >= max)
            {
                bar = max;
                if (barVelocity > 0f) barVelocity = 0f;
            }
            FightBar = bar;

            int r = Rarity;
            float agility = fishAgility[Mathf.Min(r, fishAgility.Length - 1)];
            if (Time.time >= nextFishTarget)
            {
                nextFishTarget = Time.time + UnityEngine.Random.Range(0.5f, 1.4f) / (0.6f + agility);
                fishTarget = UnityEngine.Random.value < 0.15f + 0.3f * agility
                    ? UnityEngine.Random.Range(0.04f, 0.96f)
                    : Mathf.Clamp(FightFish + UnityEngine.Random.Range(-0.25f, 0.25f) * (0.5f + agility), 0.04f, 0.96f);
            }
            FightFish = Mathf.SmoothDamp(FightFish, fishTarget, ref fishVelocity, Mathf.Lerp(0.7f, 0.22f, agility), 0.6f + 1.4f * agility, dt);

            FightInside = FightFish >= FightBar && FightFish <= FightBar + FightBarSize;
            if (FightInside) FightProgress += progressGain * dt;
            else if (Time.time - fightStart > fightGrace) FightProgress -= progressLoss[Mathf.Min(r, progressLoss.Length - 1)] * dt;
            FightProgress = Mathf.Clamp01(FightProgress);

            if (Time.time >= nextFightSplash)
            {
                nextFightSplash = Time.time + UnityEngine.Random.Range(0.35f, 0.8f);
                Plop(FightInside ? 0.5f : 0.8f);
            }
            if (FightProgress >= 1f) Reel();
            else if (FightProgress <= 0f) Abort("o peixe escapou", reel: true);
        }

        /// <summary>Ganhou a briga: puxa com o peixe na linha.</summary>
        void Reel()
        {
            State = Phase.Reeling;
            reelingFish = true;
            phaseUntil = Time.time + reelSeconds;
            castFrom = bobber ? bobber.position : target;
            Plop(1.4f);
            if (hooked?.model)
            {
                hooked.model.SetActive(true);
                hooked.model.transform.position = castFrom;
            }
            if (animatorDriver) animatorDriver.PlayFishReel();
            Landing?.Invoke(this);
        }

        /// <summary>Peixe chegou: vai para a mochila e fica um instante sobre a cabeça do capitão.</summary>
        void Land()
        {
            State = Phase.Showing;
            phaseUntil = Time.time + showSeconds;
            landedAt = Time.time;
            if (bobber) bobber.gameObject.SetActive(false);
            if (line) line.enabled = false;
            showFrom = hooked.model ? hooked.model.transform.position : transform.position;
            var item = hooked.item;
            int added = backpack ? backpack.Add(item, 1) : 0;
            if (added > 0) Caught?.Invoke(this, item);
            else Missed?.Invoke(this, "mochila cheia: o peixe escorregou de volta para a água");
        }

        /// <summary>Não veio nada: com <paramref name="reel"/>, recolhe a linha vazia (animação); senão só guarda.</summary>
        void Abort(string reason, bool reel = false)
        {
            if (!string.IsNullOrEmpty(reason)) Missed?.Invoke(this, reason);
            hooked = null;
            reelingFish = false;
            if (reel && bobber && bobber.gameObject.activeSelf)
            {
                State = Phase.Reeling;
                phaseUntil = Time.time + reelSeconds;
                castFrom = bobber.position;
                if (animatorDriver) animatorDriver.PlayFishReel();
                return;
            }
            if (animatorDriver) animatorDriver.SetFishing(false);
            End();
        }

        void End()
        {
            State = Phase.Idle;
            if (hooked?.model) hooked.model.SetActive(false);
            hooked = null;
            reelingFish = false;
            holdOverride = null;
            HideGear();
            ShowWeapon();
            nextScan = 0f;
        }

        void HideGear()
        {
            if (rod) rod.gameObject.SetActive(false);
            if (bobber) bobber.gameObject.SetActive(false);
            if (line) line.enabled = false;
            foreach (var c in catches)
                if (c?.model) c.model.SetActive(false);
        }

        void Plop(float size)
        {
            if (Services.TryGet(out CombatEffects effects)) effects.Plop(bobber && bobber.gameObject.activeSelf ? bobber.position : target, size);
        }

        // ---------- Visual: vara nas mãos, boia, linha, peixe ----------

        /// <summary>A vara vai entre as duas mãos, apontando para longe do peito e para cima (o clipe é de duas mãos; a esquerda é o gancho).</summary>
        static readonly Vector3 RodPivot = new(0f, 0.1f, -0.25f);

        void PlaceRod()
        {
            if (!rod || !rod.gameObject.activeSelf || !rightHand || !leftHand || !chest) return;
            // Mãos juntas (esperando, brigando): no meio delas; separadas (começo do lançamento, fim do puxão): na direita.
            float apart = Vector3.Distance(rightHand.position, leftHand.position);
            Vector3 mid = Vector3.Lerp((rightHand.position + leftHand.position) * 0.5f, rightHand.position, Mathf.InverseLerp(0.3f, 0.55f, apart));
            // Direção = de um ponto atrás e abaixo das mãos (no espaço do capitão) até elas: segurando na frente da barriga, ~35° para cima e
            // para a frente; mãos atrás da cabeça (preparo do lançamento), para cima e para trás; mãos no alto (puxão), bem em pé. Pelo peito
            // apontava para baixo: as mãos do capitão (cabeçudo) ficam ~0,35 m abaixo dele e a ponta entrava na areia.
            Vector3 pivot = transform.TransformPoint(RodPivot);
            Vector3 out_ = mid - pivot;
            if (out_.sqrMagnitude < 1e-4f) out_ = transform.forward;
            Vector3 dir = out_.normalized;
            rod.SetPositionAndRotation(mid - dir * 0.22f, Quaternion.LookRotation(dir, -transform.right));
        }

        /// <summary>0 → 1 do voo da boia até a água (1 = já boiando).</summary>
        float Flight => State == Phase.Waiting ? Mathf.Clamp01((Time.time - castAt) / flightSeconds) : 1f;

        void PlaceBobber()
        {
            if (!bobber || !bobber.gameObject.activeSelf) return;
            if (State == Phase.Fight)
            {
                // Puxada pelo peixe: anda de um lado para o outro e afunda (mais com o peixe fora da barra).
                Vector3 p = target;
                p.y = ocean != null ? ocean.GetHeight(target) : target.y;
                Vector3 side = Vector3.Cross(Vector3.up, facing);
                float t = Time.time;
                p += side * ((FightFish - 0.5f) * 0.9f + Mathf.Sin(t * 7.3f) * 0.06f) + facing * (Mathf.Sin(t * 3.1f) * 0.12f);
                p.y += -0.16f - (FightInside ? 0f : 0.1f) + Mathf.Sin(t * 13f) * 0.04f;
                bobber.position = p;
            }
            else if (State is Phase.Waiting or Phase.Bite)
            {
                Vector3 p = target;
                p.y = ocean != null ? ocean.GetHeight(target) : target.y;
                float flight = Flight;
                if (flight < 1f)
                {
                    // Voa em arco da ponta da vara até a água.
                    bobber.position = Vector3.Lerp(castFrom, p, flight) + Vector3.up * (Mathf.Sin(flight * Mathf.PI) * 2.2f);
                    return;
                }
                float dip = State == Phase.Bite ? 0.28f : Time.time < nibbleUntil ? 0.1f : 0f;
                p.y += 0.03f + Mathf.Sin(Time.time * 2.3f) * 0.025f - dip;
                bobber.position = p;
            }
            else if (State == Phase.Reeling)
            {
                float k = 1f - Mathf.Clamp01((phaseUntil - Time.time) / reelSeconds);
                Vector3 to = tip ? tip.position : transform.position + Vector3.up * 2f;
                Vector3 pos = Vector3.Lerp(castFrom, to, k * k);
                if (!reelingFish && ocean != null) pos.y = Mathf.Max(pos.y, ocean.GetHeight(pos));
                bobber.position = pos;
            }
        }

        void DrawLine()
        {
            if (!line || !line.enabled || !tip || !bobber) return;
            Vector3 a = tip.position, b = bobber.position;
            float sag = State == Phase.Waiting && Flight >= 1f ? Mathf.Min(0.6f, Vector3.Distance(a, b) * 0.08f) : 0.02f;
            int n = linePoints.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                linePoints[i] = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            line.positionCount = n;
            line.SetPositions(linePoints);
        }

        void PlaceFish()
        {
            var fish = hooked?.model;
            if (!fish || !fish.activeSelf) return;
            if (State == Phase.Reeling && bobber)
            {
                // Pendurado sob a boia, se debatendo.
                fish.transform.SetPositionAndRotation(bobber.position + Vector3.down * 0.25f,
                    Quaternion.Euler(Mathf.Sin(Time.time * 18f) * 25f, Time.time * 90f, 90f + Mathf.Sin(Time.time * 11f) * 20f));
            }
            else if (State == Phase.Showing && showPoint)
            {
                float k = 1f - Mathf.Clamp01((phaseUntil - Time.time) / showSeconds);
                float rise = Mathf.Clamp01(k * 4f);
                Vector3 pos = Vector3.Lerp(showFrom, showPoint.position, 1f - (1f - rise) * (1f - rise));
                fish.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Time.time * 140f, 0f));
            }
        }

        // ---------- Arma escondida enquanto pesca ----------

        void HideWeaponInHand()
        {
            if (!combat || combat.IsSwapping || hiddenWeapon.Count > 0) return;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual) return;
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                {
                    r.enabled = false;
                    hiddenWeapon.Add(r);
                }
        }

        void ShowWeapon()
        {
            foreach (var r in hiddenWeapon) if (r) r.enabled = true;
            hiddenWeapon.Clear();
        }

        // ---------- Testes ----------

        /// <summary>Teste: o peixe morde agora (na espera).</summary>
        public void BiteNow()
        {
            if (State == Phase.Waiting) phaseUntil = Time.time;
        }

        /// <summary>Teste: fisga agora (na mordida).</summary>
        public void HookNow()
        {
            if (State == Phase.Bite) Hook();
        }

        /// <summary>Teste: segura (true), solta (false) ou volta ao teclado (null) na briga.</summary>
        public void SetHoldOverride(bool? hold) => holdOverride = hold;

        /// <summary>Teste: lança para um ponto de água escolhido, sem conferir a frente.</summary>
        public bool CastAt(Vector3 point)
        {
            if (State != Phase.Idle || ocean == null || !WaterAt(point, out Vector3 water, out float depth)) return false;
            target = water;
            targetDepth = depth;
            SpotAhead = true;
            BlockReason = "";
            StartCast();
            return true;
        }
    }
}
