using System;
using System.Collections;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Armas do personagem a pé: troca (1/2) com animação de guardar/sacar, combo de golpes, ataque na direção da mira
    /// com tempo de golpe (hitDelay) casado com a animação, espada (área em arco à frente) e pistola (raio com
    /// dispersão, recarga). Pistola com mira segurada (botão direito): braço erguido seguindo a mira (CharacterAimRig),
    /// tiro na hora e mais preciso. Cada arma tem um lugar na mão e outro guardado (bainha/cinto) e muda de um para o
    /// outro no momento certo da animação. O dano vai por IDamageable, nunca em quem é do mesmo time (team).
    /// Serve ao jogador e aos piratas da IA (o comando vem do FootCharacter). A animação é avisada pelos eventos.
    /// </summary>
    [RequireComponent(typeof(FootCharacter))]
    public class CharacterCombat : MonoBehaviour
    {
        [Tooltip("Time: golpes e tiros não acertam personagens do mesmo time (0 = jogador, 1 = piratas inimigos).")]
        [SerializeField] int team;
        [SerializeField] WeaponDefinition[] weapons = Array.Empty<WeaponDefinition>();
        [Tooltip("Visual de cada arma (mesma ordem).")]
        [SerializeField] Transform[] weaponVisuals = Array.Empty<Transform>();
        [Tooltip("Lugar de cada arma na mão (filho do osso da mão).")]
        [SerializeField] Transform[] handSlots = Array.Empty<Transform>();
        [Tooltip("Lugar de cada arma guardada (bainha, cinto: filho do quadril).")]
        [SerializeField] Transform[] holsterSlots = Array.Empty<Transform>();
        [Tooltip("De onde sai o tiro (boca da pistola). Sem ele, sai do peito.")]
        [SerializeField] Transform muzzle;
        [Tooltip("Boca de cada arma (mesma ordem; vazio = a de cima). O bacamarte tem a sua.")]
        [SerializeField] Transform[] muzzles = Array.Empty<Transform>();
        [Tooltip("Altura (m) do peito, de onde parte o golpe e o tiro sem boca definida.")]
        [SerializeField] float chestHeight = 1.3f;
        [Tooltip("Linha do tiro (rastro rápido). Opcional.")]
        [SerializeField] LineRenderer tracer;
        [Tooltip("Bacamarte: segundos do rastro de cada chumbo (um risco que voa da boca até onde o chumbo parou e some).")]
        [SerializeField] float pelletTracerSeconds = 0.25f;
        [Tooltip("Clique feito até este tempo (s) antes de poder atacar fica guardado: o combo encadeia sem perder clique.")]
        [SerializeField] float attackBuffer = 0.35f;
        [Tooltip("Pistola: alvo (IDamageable) a até esta distância (m) do cursor recebe a mira no corpo.")]
        [SerializeField] float aimSnapRadius = 1.5f;
        [Tooltip("Pistola: segundos depois do tiro até a animação de recarga começar (o coice aparece antes).")]
        [SerializeField] float reloadAnimDelay = 0.3f;
        [Tooltip("Segundos para a arma assentar na mão ou na bainha ao trocar de lugar (sem estalo de posição).")]
        [SerializeField] float attachBlendTime = 0.15f;

        [Header("Chute (V)")]
        [Tooltip("Dano do chute (pouco: o que vale é derrubar), alcance (m) do peito, arco (°) à frente.")]
        [SerializeField] float kickDamage = 8f, kickRange = 1.8f, kickAngle = 100f;
        [Tooltip("Segundos até o pé acertar (no clipe, o pé fica esticado de 0,6 a 0,8 s; tocado 1,4× mais rápido), parado no chute e espera até o próximo.")]
        [SerializeField] float kickImpact = 0.45f, kickBusy = 0.9f, kickCooldown = 3f;
        [Tooltip("Empurrão (m/s) em quem leva o chute.")]
        [SerializeField] float kickPush = 3.5f;

        [Header("Defesa (segurar mirar com o cutelo)")]
        [Tooltip("Arco (°) à frente que a guarda cobre: golpe de lado ou de trás passa.")]
        [SerializeField] float blockAngle = 150f;
        [Tooltip("Fração do dano que passa pela guarda (arranhão).")]
        [SerializeField, Range(0f, 1f)] float blockedDamage = 0.2f;
        [Tooltip("Empurrão (m/s) de quem defende, para trás, a cada golpe na guarda.")]
        [SerializeField] float blockPush = 2.5f;
        [Tooltip("Velocidade andando com a guarda erguida (não corre).")]
        [SerializeField, Range(0.1f, 1f)] float blockMoveSpeed = 0.5f;
        [Tooltip("Aparar: o golpe chega até estes segundos depois de erguer a guarda = nada de dano e quem atacou fica atordoado.")]
        [SerializeField] float parryWindow = 0.3f;
        [Tooltip("Segundos com a guarda baixada antes de erguer valer de novo para aparar (sem isso, apertar sem parar aparava tudo).")]
        [SerializeField] float parryRearm = 0.5f;
        [Tooltip("Quem foi aparado: segundos sem atacar (anda devagar) e empurrão (m/s) para trás.")]
        [SerializeField] float parryStun = 1.4f, parryPush = 4f;

        readonly HashSet<IDamageable> hitThisSwing = new();
        FootCharacter character;
        float nextKickTime;
        float busyUntil, nextAttackTime, tracerUntil, aimPoseUntil, lastAttackTime = float.NegativeInfinity;
        int lastVariant = -1;
        float bufferedUntil = float.NegativeInfinity, lastShotTime = float.NegativeInfinity;
        bool swapping;
        Coroutine pendingStrike;
        // Por arma: recarregar a pistola não impede usar a espada.
        int[] shotsLeft = Array.Empty<int>();
        float[] reloadUntil = Array.Empty<float>();
        // Recarga só anda com a arma na mão (05/10, pedido do Jhonata: trocar bacamarte ↔ pistola recarregava de graça): guardada no
        // meio da recarga, fica aqui o que faltava e a recarga para; sacada de novo, continua de onde parou.
        float[] reloadPaused = Array.Empty<float>();
        // Arma trocando de lugar: sai da pose antiga (no espaço do lugar novo) e assenta em attachBlendTime.
        float[] attachProgress = Array.Empty<float>();
        Vector3[] attachFromPosition = Array.Empty<Vector3>();
        Quaternion[] attachFromRotation = Array.Empty<Quaternion>();
        Vector3[] visualScale = Array.Empty<Vector3>();

        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        /// <summary>Boca da arma <paramref name="index"/> (a geral, se ela não tem uma própria).</summary>
        public Transform MuzzleOf(int index) => index >= 0 && index < muzzles.Length && muzzles[index] ? muzzles[index] : muzzle;
        /// <summary>Boca da arma na mão.</summary>
        public Transform CurrentMuzzle => MuzzleOf(CurrentIndex);
        /// <summary>A arma pode ser sacada (o bacamarte só depois de comprado).</summary>
        public bool IsAvailable(int index) => index >= 0 && index < weapons.Length && (index >= locked.Length || !locked[index]);
        /// <summary>Visual da arma <paramref name="index"/> (mesma ordem de <see cref="Weapons"/>), para efeitos.</summary>
        public Transform WeaponVisual(int index) => index >= 0 && index < weaponVisuals.Length ? weaponVisuals[index] : null;
        public int Team => team;
        /// <summary>Arma na mão (-1 no meio de uma troca, entre guardar uma e sacar a outra).</summary>
        public int CurrentIndex { get; private set; } = -1;
        public WeaponDefinition Current => CurrentIndex >= 0 && CurrentIndex < weapons.Length ? weapons[CurrentIndex] : null;
        public bool IsSwapping => swapping;
        public bool IsAttacking => Time.time < busyUntil;
        /// <summary>Um ataque agora sairia na hora (sem ficar guardado esperando a arma): a IA do chefe só escolhe o golpe assim.</summary>
        public bool ReadyToAttack => !swapping && !IsAttacking && !IsBlocking && Time.time >= nextAttackTime;
        public bool IsReloading => Current && Time.time < reloadUntil[CurrentIndex];
        public float ReloadRemaining => Current ? Mathf.Max(0f, reloadUntil[CurrentIndex] - Time.time) : 0f;
        public int ShotsLeft => Current ? shotsLeft[CurrentIndex] : 0;
        /// <summary>A arma de fogo <paramref name="index"/> está descarregada (guardada no meio da recarga, ou recarregando na mão).</summary>
        public bool IsEmpty(int index) => index >= 0 && index < weapons.Length && weapons[index] && weapons[index].shotsPerReload > 0 && shotsLeft[index] <= 0;
        /// <summary>Pistola: pose de mira segurando mirar, logo depois do tiro e durante a recarga.</summary>
        public bool IsAimingPose => Current && Current.kind == WeaponKind.Firearm && (IsAimHeld || Time.time < aimPoseUntil || IsReloading);
        /// <summary>Segurando mirar com a pistola na mão (fora de troca): vira para a mira e não corre.</summary>
        public bool IsAimHeld { get; private set; }
        /// <summary>Mirando de fato: braço erguido seguindo a mira (na recarga a mão está ocupada).</summary>
        public bool IsAiming => IsAimHeld && !IsReloading;
        /// <summary>Ponto 3D para onde a arma mira agora: corpo do alvo perto do cursor ou altura do peito sobre o chão.</summary>
        public Vector3 AimTarget { get; private set; }
        /// <summary>A mira travou num alvo (o tiro vai no corpo dele): a mira na tela fica vermelha.</summary>
        public bool AimOnTarget { get; private set; }
        /// <summary>A mira vem de um cursor (mouse). Sem ele (gamepad), mira para a frente.</summary>
        public bool HasAimPoint { get; private set; }
        /// <summary>Recarga na animação: começa um pouco depois do tiro, para o coice aparecer.</summary>
        public bool ReloadAnimating => IsReloading && Time.time - lastShotTime >= reloadAnimDelay;

        /// <summary>Ataque com animação começou (arma, golpe do combo). Tiro mirando não passa aqui: o braço já está erguido.</summary>
        public event Action<WeaponDefinition, int> Attacked;
        /// <summary>A bala saiu (arma, boca, fim do rastro): coice e efeitos.</summary>
        public event Action<WeaponDefinition, Vector3, Vector3> Fired;
        /// <summary>Acertou algo: ponto do acerto.</summary>
        public event Action<WeaponDefinition, Vector3> Hit;
        /// <summary>Chumbo do leque além do principal (bacamarte): boca, onde parou. Só efeitos (o Fired já veio uma vez).</summary>
        public event Action<WeaponDefinition, Vector3, Vector3> PelletLanded;
        /// <summary>A pose de arma mudou (arma que está sendo sacada ou que já está na mão; null = mãos vazias na troca).</summary>
        public event Action<WeaponDefinition> WeaponChanged;
        /// <summary>Começou a chutar (animação).</summary>
        public event Action Kicked;
        /// <summary>O chute acertou alguém ou algo (ponto): som e brilho.</summary>
        public event Action<Vector3> KickLanded;
        /// <summary>Começou a guardar / sacar esta arma (animações).</summary>
        public event Action<WeaponDefinition> Holstering;
        public event Action<WeaponDefinition> Drawing;
        /// <summary>Um golpe pegou na guarda (ponto; true = aparado): som, faíscas, reação.</summary>
        public event Action<Vector3, bool> Blocked;
        /// <summary>O golpe deste personagem foi aparado: fica atordoado (animação).</summary>
        public event Action Parried;

        /// <summary>Resultado de um golpe contra a guarda.</summary>
        public enum GuardResult { None, Blocked, Parried, Broken }

        /// <summary>A guarda quebrou (fôlego zerado com ela erguida): som e aviso.</summary>
        public event Action GuardBroken;
        /// <summary>Guarda quebrada: não ergue de novo até este instante.</summary>
        public float GuardLockedUntil { get; private set; } = float.NegativeInfinity;
        Stamina stamina;

        /// <summary>Guarda erguida (segurando mirar com o cutelo na mão): golpe de frente vira arranhão; na hora certa, apara.</summary>
        public bool IsBlocking { get; private set; }
        /// <summary>Erguer agora ainda apara (a guarda ficou baixada tempo bastante).</summary>
        public bool ParryReady => Time.time - blockEnd >= parryRearm;
        public float BlockMoveSpeed => blockMoveSpeed;
        float blockStart = float.NegativeInfinity, blockEnd = float.NegativeInfinity;
        bool parryArmed;

        public void Configure(WeaponDefinition[] definitions, Transform[] visuals, Transform[] hands, Transform[] holsters,
            Transform gunMuzzle, LineRenderer shotTracer)
        {
            weapons = definitions;
            weaponVisuals = visuals;
            handSlots = hands;
            holsterSlots = holsters;
            muzzle = gunMuzzle;
            tracer = shotTracer;
        }

        /// <summary>Boca de cada arma (mesma ordem das armas; null = a geral).</summary>
        public void SetMuzzles(Transform[] perWeapon) => muzzles = perWeapon ?? Array.Empty<Transform>();

        public void SetTeam(int value) => team = value;

        bool[] locked = Array.Empty<bool>();
        int queuedVariant = -1;

        /// <summary>O próximo golpe será este (a IA escolhe o golpe rápido quando precisa). Vale para um golpe só.</summary>
        public void QueueVariant(int variant) => queuedVariant = variant;

        /// <summary>
        /// Libera ou tranca uma arma (o bacamarte só depois de comprado: Controls/GearWeapons). Trancada não saca; se estava na mão,
        /// volta para a primeira.
        /// </summary>
        public void SetAvailable(int index, bool available)
        {
            if (index < 0 || index >= weapons.Length) return;
            if (locked.Length != weapons.Length) Array.Resize(ref locked, weapons.Length);
            if (locked[index] == !available) return;
            locked[index] = !available;
            if (!available && CurrentIndex == index && isActiveAndEnabled) Equip(0);
        }

        IAmmoSupply ammo;
        float nextDryFire;

        /// <summary>Munição das armas de fogo (null = infinita, como os piratas da IA).</summary>
        public IAmmoSupply Ammo => ammo;
        public void SetAmmo(IAmmoSupply supply) => ammo = supply;
        /// <summary>Tiros que há para esta arma (int.MaxValue sem munição limitada).</summary>
        public int AmmoFor(WeaponDefinition weapon) => ammo == null || !weapon ? int.MaxValue : ammo.Count(weapon);

        /// <summary>Raio do "ímã" da mira no corpo do alvo perto do ponto mirado. 0 = atira no ponto (IA: dá para desviar).</summary>
        public void SetAimSnap(float radius) => aimSnapRadius = Mathf.Max(0f, radius);

        /// <summary>
        /// Atordoado (levou um golpe): cancela o golpe ou tiro que ia sair e só ataca de novo depois de
        /// <paramref name="seconds"/>. Quem bate primeiro interrompe o outro.
        /// </summary>
        public void Interrupt(float seconds)
        {
            if (pendingStrike != null) StopCoroutine(pendingStrike);
            pendingStrike = null;
            busyUntil = Time.time;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + seconds);
            bufferedUntil = float.NegativeInfinity;
            // Solta a mira (o próximo comando a segura de novo, se ainda for o caso; morto, não volta).
            IsAimHeld = false;
        }

        void Awake()
        {
            character = GetComponent<FootCharacter>();
            stamina = GetComponent<Stamina>();
            shotsLeft = new int[weapons.Length];
            reloadUntil = new float[weapons.Length];
            reloadPaused = new float[weapons.Length];
            attachProgress = new float[weapons.Length];
            attachFromPosition = new Vector3[weapons.Length];
            attachFromRotation = new Quaternion[weapons.Length];
            visualScale = new Vector3[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                shotsLeft[i] = weapons[i] ? weapons[i].shotsPerReload : 0;
                if (i < weaponVisuals.Length && weaponVisuals[i]) visualScale[i] = weaponVisuals[i].localScale;
                Place(i, holsterSlots, false);
            }
            // Começa com a primeira arma já na mão (sem animação).
            if (weapons.Length > 0) TakeInHand(0, false);
        }

        /// <summary>Chamado pelo FootCharacter a cada frame com o comando do jogador.</summary>
        public void Tick(in FootCommand command)
        {
            if (command.selectWeapon > 0) Equip(command.selectWeapon - 1);
            else if (command.nextWeapon && weapons.Length > 0)
            {
                // Próxima arma liberada (pula as trancadas).
                int from = CurrentIndex < 0 ? 0 : CurrentIndex;
                for (int step = 1; step < weapons.Length; step++)
                {
                    int next = (from + step) % weapons.Length;
                    if (!IsAvailable(next)) continue;
                    Equip(next);
                    break;
                }
            }

            // Recarga terminou: munição cheia de novo (vale mesmo com outra arma na mão).
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i] && weapons[i].shotsPerReload > 0 && shotsLeft[i] <= 0 && Time.time >= reloadUntil[i])
                    shotsLeft[i] = weapons[i].shotsPerReload;
            var current = Current;
            IsAimHeld = command.aim && current && current.kind == WeaponKind.Firearm && !swapping;
            HasAimPoint = command.hasAim;
            // Guarda: mirar com o cutelo na mão (não no meio de um golpe, chute ou troca; caído/rolando o comando vem vazio).
            bool guard = command.aim && current && current.kind == WeaponKind.Melee && !swapping && !IsAttacking
                && Time.time >= GuardLockedUntil && (!stamina || stamina.Current > 0f);
            if (guard != IsBlocking)
            {
                if (guard)
                {
                    blockStart = Time.time;
                    parryArmed = ParryReady;
                }
                else blockEnd = Time.time;
                IsBlocking = guard;
            }
            // Guarda erguida gasta fôlego (e não deixa recuperar); zerou = quebrou.
            if (IsBlocking && stamina)
            {
                stamina.Spend(stamina.GuardPerSecond * Time.deltaTime);
                if (stamina.Current <= 0f) BreakGuard();
            }
            if (current && current.kind == WeaponKind.Firearm)
            {
                bool onTarget = false;
                AimTarget = command.hasAim ? ShotTarget(command.aimPoint, out onTarget) : transform.position + transform.forward * 10f + Vector3.up * chestHeight;
                AimOnTarget = onTarget;
            }

            if (command.kick && CanKick) Kick(command);

            // Clique guardado: tenta de novo enquanto não vence (o comando atual traz a mira mais nova).
            if (command.attack) bufferedUntil = Time.time + attackBuffer;
            if (Time.time <= bufferedUntil && TryAttack(command)) bufferedUntil = float.NegativeInfinity;

            if (tracer && tracer.enabled && Time.time > tracerUntil) tracer.enabled = false;
            UpdatePelletTracers();
        }

        // ---------- Rastros do chumbo (bacamarte) ----------

        // Cópias do rastro do tiro, criadas no primeiro tiro de leque e reaproveitadas.
        readonly List<LineRenderer> pelletTracers = new();
        readonly List<(Vector3 from, Vector3 to)> pelletPaths = new();
        float pelletTracerStart = float.NegativeInfinity;
        int pelletTracersShown;

        void StartPelletTracer(int i, Vector3 from, Vector3 to)
        {
            if (!tracer) return;
            while (pelletTracers.Count <= i)
            {
                var copy = Instantiate(tracer, tracer.transform.parent);
                copy.name = "RastroDoChumbo";
                copy.widthMultiplier = 0.075f;   // mais grosso que o da pistola: a câmera do jogo fica a ~13 m
                copy.positionCount = 2;
                pelletTracers.Add(copy);
                pelletPaths.Add(default);
            }
            pelletPaths[i] = (from, to);
            pelletTracers[i].enabled = true;
            pelletTracersShown = Mathf.Max(pelletTracersShown, i + 1);
        }

        /// <summary>
        /// Cada chumbo é um risco curto que corre da boca até onde parou (a cabeça chega lá em 60% do tempo) e encurta até sumir: dá
        /// para ver o leque abrindo, e os que bateram em algo param antes.
        /// </summary>
        void UpdatePelletTracers()
        {
            if (pelletTracersShown == 0) return;
            float k = pelletTracerSeconds > 0f ? (Time.time - pelletTracerStart) / pelletTracerSeconds : 1f;
            if (k >= 1f)
            {
                for (int i = 0; i < pelletTracersShown; i++) pelletTracers[i].enabled = false;
                pelletTracersShown = 0;
                return;
            }
            float head = Mathf.Clamp01(k / 0.55f), tail = Mathf.Clamp01((k - 0.3f) / 0.7f);
            float alpha = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
            var front = new Color(1f, 0.95f, 0.7f, 0.95f * alpha);
            var back = new Color(1f, 0.8f, 0.45f, 0.25f * alpha);
            for (int i = 0; i < pelletTracersShown; i++)
            {
                var line = pelletTracers[i];
                if (!line.enabled) continue;
                var (from, to) = pelletPaths[i];
                line.SetPosition(0, Vector3.Lerp(from, to, tail));
                line.SetPosition(1, Vector3.Lerp(from, to, head));
                line.startColor = back;
                line.endColor = front;
            }
        }

        /// <summary>Troca para a arma <paramref name="index"/>: guarda a atual e saca a nova, com as animações.</summary>
        public void Equip(int index)
        {
            if (weapons.Length == 0 || swapping || IsAttacking) return;
            if (index < 0 || index >= weapons.Length || !IsAvailable(index)) return;
            if (index == CurrentIndex) return;
            StartCoroutine(Swap(index));
        }

        IEnumerator Swap(int to)
        {
            swapping = true;
            var from = Current;
            int fromIndex = CurrentIndex;
            PauseReload(fromIndex);
            if (from)
            {
                if (from.animatedDraw)
                {
                    Holstering?.Invoke(from);
                    yield return new WaitForSeconds(from.holsterReleaseTime);
                    PutAway(fromIndex);
                    WeaponChanged?.Invoke(null);
                    yield return new WaitForSeconds(Mathf.Max(0f, from.holsterDuration - from.holsterReleaseTime));
                }
                else PutAway(fromIndex);
            }

            CurrentIndex = -1;
            var next = weapons[to];
            if (next.animatedDraw)
            {
                // A pose da arma (WeaponType) muda antes do saque, para a animação certa tocar.
                WeaponChanged?.Invoke(next);
                Drawing?.Invoke(next);
                yield return new WaitForSeconds(next.drawGrabTime);
                TakeInHand(to);
                yield return new WaitForSeconds(Mathf.Max(0f, next.drawDuration - next.drawGrabTime));
            }
            else TakeInHand(to);
            swapping = false;
        }

        void TakeInHand(int index, bool smooth = true)
        {
            ResumeReload(index);
            Place(index, handSlots, smooth);
            CurrentIndex = index;
            lastVariant = -1;
            WeaponChanged?.Invoke(weapons[index]);
        }

        void PutAway(int index) => Place(index, holsterSlots, true);

        /// <summary>Guardou a arma no meio da recarga: guarda o que faltava e a recarga para (não completa no cinto/nas costas).</summary>
        void PauseReload(int index)
        {
            if (index < 0 || index >= weapons.Length || shotsLeft[index] > 0 || Time.time >= reloadUntil[index]) return;
            reloadPaused[index] = reloadUntil[index] - Time.time;
            reloadUntil[index] = float.PositiveInfinity;
        }

        /// <summary>Sacou de novo: a recarga continua de onde parou.</summary>
        void ResumeReload(int index)
        {
            if (index < 0 || index >= weapons.Length || reloadPaused[index] <= 0f) return;
            reloadUntil[index] = Time.time + reloadPaused[index];
            reloadPaused[index] = 0f;
        }

        /// <summary>Muda a arma de lugar (mão ↔ bainha/cinto). Suave: parte de onde estava e assenta no lugar novo.</summary>
        void Place(int index, Transform[] slots, bool smooth)
        {
            if (index < 0 || index >= weaponVisuals.Length || !weaponVisuals[index]) return;
            var slot = index < slots.Length ? slots[index] : null;
            if (!slot) return;
            var visual = weaponVisuals[index];
            visual.SetParent(slot, true);
            visual.localScale = visualScale[index];
            if (smooth && attachBlendTime > 0f)
            {
                attachFromPosition[index] = visual.localPosition;
                attachFromRotation[index] = visual.localRotation;
                attachProgress[index] = 0f;
            }
            else
            {
                visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                attachProgress[index] = 1f;
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < attachProgress.Length; i++)
            {
                if (attachProgress[i] >= 1f || !weaponVisuals[i]) continue;
                attachProgress[i] = Mathf.Min(1f, attachProgress[i] + Time.deltaTime / attachBlendTime);
                float t = Mathf.SmoothStep(0f, 1f, attachProgress[i]);
                weaponVisuals[i].SetLocalPositionAndRotation(Vector3.Lerp(attachFromPosition[i], Vector3.zero, t),
                    Quaternion.Slerp(attachFromRotation[i], Quaternion.identity, t));
            }
        }

        public bool CanKick => !swapping && !IsAttacking && Time.time >= nextKickTime;

        /// <summary>Chute para a mira (sem mira, para a frente): pouco dano, empurra e derruba (cambaleia e cai sentado).</summary>
        void Kick(in FootCommand command)
        {
            Vector3 direction = transform.forward;
            if (command.hasAim)
            {
                Vector3 toAim = command.aimPoint - transform.position;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.04f) direction = toAim.normalized;
            }
            busyUntil = Time.time + kickBusy;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + kickBusy);
            nextKickTime = Time.time + kickCooldown;
            bufferedUntil = float.NegativeInfinity;
            character.FaceDuring(direction, kickImpact + 0.2f);
            character.SetSpeedMultiplier(0.15f, kickBusy);
            Kicked?.Invoke();
            pendingStrike = StartCoroutine(KickStrike(direction));
        }

        IEnumerator KickStrike(Vector3 direction)
        {
            yield return new WaitForSeconds(kickImpact);
            pendingStrike = null;
            hitThisSwing.Clear();
            Vector3 chest = transform.position + Vector3.up * chestHeight * 0.6f;
            int count = Physics.OverlapSphereNonAlloc(chest, kickRange, snapHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = snapHits[i];
                if (col.transform.IsChildOf(transform) || IsAlly(col)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !hitThisSwing.Add(target)) continue;
                Vector3 closest = col.bounds.ClosestPoint(chest);
                Vector3 to = closest - chest;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(direction, to) > kickAngle * 0.5f) continue;
                target.TakeDamage(new DamageInfo { amount = kickDamage, point = closest, direction = direction, source = gameObject, knockdown = Knockdown.Stumble });
                var pushable = col.GetComponentInParent<IPushable>();
                if (pushable != null) pushable.Push(direction * kickPush);
                KickLanded?.Invoke(closest);
            }
            hitThisSwing.Clear();
        }

        bool TryAttack(in FootCommand command)
        {
            var weapon = Current;
            // Guarda erguida não ataca: o clique fica guardado e sai ao soltar (contra-ataque depois de aparar).
            if (!weapon || swapping || IsAttacking || IsBlocking || Time.time < nextAttackTime || IsReloading) return false;
            if (weapon.kind == WeaponKind.Firearm && shotsLeft[CurrentIndex] <= 0) return false;
            if (weapon.kind == WeaponKind.Firearm && ammo != null && ammo.Count(weapon) <= 0)
            {
                // Clique seco (um aviso por clique, não a cada quadro do botão segurado).
                if (Time.time >= nextDryFire) ammo.OnEmpty(weapon);
                nextDryFire = Time.time + 0.4f;
                return false;
            }

            Vector3 direction = transform.forward;
            if (command.hasAim)
            {
                Vector3 toAim = command.aimPoint - transform.position;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.04f) direction = toAim.normalized;
            }

            // Combo: clicar de novo logo depois continua a sequência de golpes.
            int variant = Time.time - lastAttackTime <= weapon.comboWindow ? (lastVariant + 1) % weapon.attackVariants : 0;
            if (queuedVariant >= 0 && queuedVariant < weapon.attackVariants) variant = queuedVariant;
            else if (weapon.randomVariants && weapon.attackVariants > 1)
            {
                // Sorteado, diferente do anterior (a IA mistura golpe rápido e lento).
                variant = UnityEngine.Random.Range(0, weapon.attackVariants - 1);
                if (variant >= lastVariant && lastVariant >= 0) variant++;
            }
            lastVariant = variant;
            queuedVariant = -1;
            lastAttackTime = Time.time;

            // Braço já erguido (mirando ou logo depois de um tiro): a bala sai na hora, sem animação de tiro.
            bool armRaised = weapon.kind == WeaponKind.Firearm && (IsAiming || Time.time < aimPoseUntil);
            float delay = armRaised ? weapon.aimedShotDelay : weapon.HitDelayFor(variant);
            // Sequência (golpe com acertos a mais): ocupado até o último acerto.
            float last = armRaised ? delay : weapon.LastHitFor(variant);
            busyUntil = Time.time + last + 0.1f;
            nextAttackTime = Time.time + weapon.cooldown + (last - delay);
            if (!IsAimHeld)
            {
                character.FaceDuring(direction, delay + 0.15f);
                character.SetSpeedMultiplier(weapon.moveSpeedWhileAttacking, last + 0.15f);
            }
            if (weapon.kind == WeaponKind.Firearm) aimPoseUntil = Time.time + delay + weapon.aimPoseSeconds;
            if (!armRaised) Attacked?.Invoke(weapon, variant);

            Vector3 aim = command.hasAim ? command.aimPoint : transform.position + direction * weapon.range;
            pendingStrike = StartCoroutine(Strike(weapon, CurrentIndex, variant, direction, aim, delay, armRaised ? weapon.aimedSpread : 1f));
            return true;
        }

        IEnumerator Strike(WeaponDefinition weapon, int index, int variant, Vector3 direction, Vector3 aimPoint, float delay, float spread)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (weapon.kind != WeaponKind.Melee)
            {
                pendingStrike = null;
                Shoot(weapon, index, direction, aimPoint, spread);
                yield break;
            }
            var strike = weapon.StrikeFor(variant);
            var followUps = strike?.followUps;
            if (followUps == null || followUps.Length == 0)
            {
                pendingStrike = null;
                Swing(weapon, direction, strike);
                yield break;
            }
            // Sequência: cada acerto vai para onde o personagem está virado agora (ele avança e gira entre um e outro).
            if (!Swing(weapon, direction, strike)) yield break;
            float done = 0f;
            foreach (var t in followUps)
            {
                if (t > done) yield return new WaitForSeconds(t - done);
                done = t;
                if (!Swing(weapon, transform.forward, strike)) yield break;
            }
            pendingStrike = null;
        }

        /// <summary>Dano dos golpes × isto (fase 2 do chefe). Não mexe no asset da arma.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>
        /// Espada: tudo que é IDamageable num arco à frente, até o alcance. Cada alvo leva um golpe só. false = aparado (o resto da
        /// sequência se perde).
        /// </summary>
        bool Swing(WeaponDefinition weapon, Vector3 direction, StrikeOverride strike = null)
        {
            hitThisSwing.Clear();
            float range = strike != null && strike.range > 0f ? strike.range : weapon.range;
            float angle = strike != null && strike.angle > 0f ? strike.angle : weapon.angle;
            float damage = (strike != null && strike.damage > 0f ? strike.damage : weapon.damage) * DamageMultiplier;
            var knockdown = strike != null && strike.customKnockdown ? strike.knockdown : weapon.knockdown;
            bool unblockable = strike != null && strike.unblockable;
            Vector3 chest = transform.position + Vector3.up * chestHeight;
            foreach (var col in Physics.OverlapSphere(chest, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform) || IsAlly(col)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !hitThisSwing.Add(target)) continue;
                Vector3 closest = col.ClosestPoint(chest);
                Vector3 to = closest - chest;
                to.y = 0f;
                if (angle < 360f && to.sqrMagnitude > 0.0001f && Vector3.Angle(direction, to) > angle * 0.5f) continue;
                // Guarda do outro (bala, chute e explosão não passam por aqui: a guarda só segura lâmina). Imparável passa direto.
                var defender = col.GetComponentInParent<CharacterCombat>();
                var guard = defender && defender != this && !unblockable ? defender.Defend(transform.position, closest, weapon.guardCost) : GuardResult.None;
                if (guard == GuardResult.Parried)
                {
                    GotParried(defender.transform.position);
                    return false;
                }
                bool blocked = guard == GuardResult.Blocked;
                // Guarda quebrada pelo golpe: passa parte do dano, com a reação normal de levar golpe.
                float scale = blocked ? defender.blockedDamage : guard == GuardResult.Broken ? defender.GuardBreakDamage : 1f;
                target.TakeDamage(new DamageInfo
                {
                    amount = damage * scale, point = closest, direction = direction, source = gameObject,
                    blocked = blocked,
                    // Golpe pesado (machado do brutamontes) derruba; na guarda, não.
                    knockdown = blocked ? Knockdown.None : knockdown,
                });
                if (!blocked) Hit?.Invoke(weapon, closest);
            }
            return true;
        }

        /// <summary>
        /// Golpe de lâmina vindo de <paramref name="attacker"/>: com a guarda erguida e o golpe de frente, segura (arranhão, empurra para
        /// trás) ou, logo depois de erguer, apara (nada passa). Chamado por quem golpeia, antes do dano.
        /// </summary>
        public GuardResult Defend(Vector3 attacker, Vector3 point, float guardCost = 1f)
        {
            if (!IsBlocking) return GuardResult.None;
            Vector3 to = attacker - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, to) > blockAngle * 0.5f) return GuardResult.None;
            bool parry = parryArmed && Time.time - blockStart <= parryWindow;
            if (!parry && to.sqrMagnitude > 0.0001f) character.Push(-to.normalized * blockPush);
            // Faísca onde as lâminas se encontram: à frente de quem defende, na altura da guarda (a borda do colisor caía em cima do chapéu).
            Vector3 front = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            Blocked?.Invoke(transform.position + front * 0.55f + Vector3.up * chestHeight * 0.75f, parry);
            if (stamina)
            {
                // Aparar devolve fôlego; segurar o golpe gasta (zerou = a guarda quebra e parte do golpe passa).
                if (parry) stamina.Refund(stamina.ParryRefund);
                else
                {
                    stamina.Spend(stamina.BlockHitCost * guardCost);
                    if (stamina.Current <= 0f)
                    {
                        BreakGuard();
                        return GuardResult.Broken;
                    }
                }
            }
            return parry ? GuardResult.Parried : GuardResult.Blocked;
        }

        /// <summary>Sem fôlego com a guarda erguida: ela cai e só volta depois de guardBreakSeconds (sem atordoar: viraria morte certa).</summary>
        void BreakGuard()
        {
            IsBlocking = false;
            blockEnd = Time.time;
            GuardLockedUntil = Time.time + (stamina ? stamina.GuardBreakSeconds : 1f);
            GuardBroken?.Invoke();
        }

        float GuardBreakDamage => stamina ? stamina.GuardBreakDamage : 0.5f;

        /// <summary>O golpe foi aparado: o resto do golpe se perde, fica sem atacar nem chutar, lento, e é empurrado para trás.</summary>
        void GotParried(Vector3 from)
        {
            Interrupt(parryStun);
            nextKickTime = Mathf.Max(nextKickTime, Time.time + parryStun);
            character.SetSpeedMultiplier(0.25f, parryStun);
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) character.Push(away.normalized * parryPush);
            Parried?.Invoke();
        }

        /// <summary>Pistola: raio da boca até o alvo, com dispersão (menor mirando); a 1ª coisa atingida leva o dano.</summary>
        void Shoot(WeaponDefinition weapon, int index, Vector3 direction, Vector3 aimPoint, float spread)
        {
            shotsLeft[index]--;
            lastShotTime = Time.time;
            ammo?.Take(weapon);
            // A recarga conta a partir do fim do coice, junto com a animação.
            if (shotsLeft[index] <= 0 && weapon.shotsPerReload > 0) reloadUntil[index] = Time.time + reloadAnimDelay + weapon.reloadTime;

            var gunMuzzle = MuzzleOf(index);
            Vector3 origin = gunMuzzle ? gunMuzzle.position : transform.position + Vector3.up * chestHeight + direction * 0.5f;
            Vector3 target = ShotTarget(aimPoint, out _);
            Vector3 aimDir = (target - origin).sqrMagnitude > 0.01f ? (target - origin).normalized : direction;
            float cone = weapon.angle * spread;
            Vector3 shotDir = Quaternion.Euler(UnityEngine.Random.Range(-1f, 1f) * cone * 0.25f,
                                               UnityEngine.Random.Range(-1f, 1f) * cone * 0.5f, 0f) * aimDir;

            // Bacamarte: o primeiro chumbo vai na mira (com a dispersão de sempre), os outros abrem em leque em volta dele.
            // O dano de todos os chumbos que pegam no mesmo alvo vira um golpe só (uma reação, um número).
            pelletDamage.Clear();
            Vector3 end = Pellet(weapon, origin, shotDir);
            int pellets = Mathf.Max(1, weapon.pellets);
            if (pellets > 1) StartPelletTracer(0, origin, end);
            Vector3 right = Vector3.Cross(Vector3.up, shotDir).normalized, up = Vector3.Cross(shotDir, right);
            float half = weapon.pelletSpread * 0.5f;
            for (int p = 1; p < pellets; p++)
            {
                // Espalhados num disco (mais largos que altos: o leque é do cano para os lados).
                Vector2 disc = UnityEngine.Random.insideUnitCircle;
                Vector3 dir = Quaternion.AngleAxis(disc.x * half, up) * Quaternion.AngleAxis(disc.y * half * 0.5f, right) * shotDir;
                Vector3 landed = Pellet(weapon, origin, dir);
                StartPelletTracer(p, origin, landed);
                PelletLanded?.Invoke(weapon, origin, landed);
            }
            if (pellets > 1) pelletTracerStart = Time.time;
            foreach (var pair in pelletDamage)
            {
                var (amount, point, dir) = pair.Value;
                pair.Key.TakeDamage(new DamageInfo { amount = amount, point = point, direction = dir, source = gameObject, ignites = true });
                Hit?.Invoke(weapon, point);
            }
            pelletDamage.Clear();

            if (tracer && pellets == 1)
            {
                tracer.positionCount = 2;
                tracer.SetPosition(0, origin);
                tracer.SetPosition(1, end);
                tracer.enabled = true;
                tracerUntil = Time.time + 0.06f;
            }
            Fired?.Invoke(weapon, origin, end);
        }

        readonly Dictionary<IDamageable, (float amount, Vector3 point, Vector3 direction)> pelletDamage = new();

        /// <summary>Um projétil: a 1ª coisa no caminho para; se leva dano, soma em pelletDamage. Devolve onde parou.</summary>
        Vector3 Pellet(WeaponDefinition weapon, Vector3 origin, Vector3 dir)
        {
            Vector3 end = origin + dir * weapon.range;
            foreach (var hit in SortedHits(origin, dir, weapon.range))
            {
                if (hit.transform.IsChildOf(transform) || IsAlly(hit.collider)) continue;
                end = hit.point;
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    pelletDamage[damageable] = pelletDamage.TryGetValue(damageable, out var sum)
                        ? (sum.amount + weapon.damage, sum.point, sum.direction)
                        : (weapon.damage, hit.point, dir);
                break;
            }
            return end;
        }

        /// <summary>
        /// Onde o tiro mira: no corpo do alvo mais perto do cursor (até aimSnapRadius) ou, sem alvo, na altura do peito
        /// sobre o ponto do chão. O cursor marca o chão; ir reto na altura da boca passava por cima de quem estava mais baixo.
        /// </summary>
        Vector3 ShotTarget(Vector3 aimPoint, out bool onTarget)
        {
            onTarget = false;
            if (aimSnapRadius <= 0f) return aimPoint + Vector3.up * chestHeight;
            Collider best = null;
            float bestDistance = aimSnapRadius;
            // Roda todo quadro com a pistola na mão: buffer fixo em vez do array novo do OverlapSphere.
            int count = Physics.OverlapSphereNonAlloc(aimPoint, aimSnapRadius, snapHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = snapHits[i];
                if (col.transform.IsChildOf(transform) || IsAlly(col) || col.GetComponentInParent<IDamageable>() == null) continue;
                Vector3 d = col.bounds.center - aimPoint;
                d.y = 0f;
                if (d.magnitude >= bestDistance) continue;
                best = col;
                bestDistance = d.magnitude;
            }
            onTarget = best;
            return best ? best.bounds.center : aimPoint + Vector3.up * chestHeight;
        }

        static readonly Collider[] snapHits = new Collider[32];

        /// <summary>Personagem do mesmo time (não leva dano nem segura bala).</summary>
        bool IsAlly(Collider col)
        {
            var other = col.GetComponentInParent<CharacterCombat>();
            return other && other != this && other.team == team;
        }

        static RaycastHit[] SortedHits(Vector3 origin, Vector3 direction, float range)
        {
            var hits = Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits;
        }

        void OnEnable()
        {
            if (CurrentIndex < 0 && weapons.Length > 0 && shotsLeft.Length == weapons.Length) TakeInHand(0, false);
        }

        void OnDisable()
        {
            // Desembarcou/embarcou no meio de uma troca: cancela a troca (a arma fica onde estava).
            StopAllCoroutines();
            pendingStrike = null;
            for (int i = 0; i < pelletTracersShown; i++) pelletTracers[i].enabled = false;
            pelletTracersShown = 0;
            swapping = false;
            IsAimHeld = false;
            IsBlocking = false;
        }
    }
}
