using System;
using Pirates.Core;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Personagem a pé (jogador ou pirata da IA) com CharacterController (movimento por script, sem root motion): anda e
    /// corre na direção pedida, gira para onde anda, pula e cai com gravidade. Quem decide é o IFootInput do mesmo
    /// objeto (PlayerFootInput ou a IA de terra): os dois "apertam os mesmos botões" (FootCommand).
    /// Mirando a pistola (ou olhando para um ponto), vira para a mira: as pernas seguem o movimento (até maxAimTwist)
    /// e o tronco gira o resto (CharacterAimRig); de costas, anda de ré.
    /// Não entra em água mais funda que a cintura; se cair no mar, volta para o último chão seguro.
    /// O visual (e o Animator) fica num filho.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootCharacter : MonoBehaviour, IPushable
    {
        [Header("Movimento")]
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float runSpeed = 6f;
        [Tooltip("m/s² para acelerar/frear no chão.")]
        [SerializeField] float acceleration = 14f;
        [Tooltip("Controle no ar (fração da aceleração).")]
        [SerializeField, Range(0f, 1f)] float airControl = 0.35f;
        [SerializeField] float turnSharpness = 12f;
        [Tooltip("Giro máximo (°/s; 0 = sem limite). Os grandes (brutamontes, chefe) viram mais devagar: davam meia-volta em 0,16 s.")]
        [SerializeField] float maxTurnSpeed;
        [Tooltip("Atacando: o freio fica estas vezes mais forte (o golpe não sai deslizando).")]
        [SerializeField] float attackBrake = 3f;

        [Header("Mira (pistola)")]
        [Tooltip("Graus que o tronco gira em relação às pernas. Andando de lado, as pernas seguem o movimento até aqui.")]
        [SerializeField] float maxAimTwist = 60f;
        [Tooltip("Guarda erguida: o mesmo, menor (a lâmina fica virada para o cursor; o golpe de frente é o que vem de lá).")]
        [SerializeField] float guardTwist = 30f;
        [SerializeField] float aimTurnSharpness = 14f;

        [Header("Pulo e gravidade")]
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Segundos depois de sair do chão em que ainda dá para pular (perdoa o tempo do botão).")]
        [SerializeField] float coyoteTime = 0.12f;

        [Header("Água")]
        [Tooltip("Profundidade máxima (m, da água até os pés) em que o personagem entra no mar.")]
        [SerializeField] float maxWadeDepth = 0.9f;
        [Tooltip("Abaixo desta profundidade (caiu de um barranco no mar) volta ao último chão seguro.")]
        [SerializeField] float drownDepth = 2.5f;

        [Header("Rolamento (esquiva)")]
        [Tooltip("Metros do rolamento e quanto tempo leva.")]
        [SerializeField] float dodgeDistance = 4.5f, dodgeSeconds = 0.8f;
        [Tooltip("Segundos de espera depois de um rolamento (não dá para rolar sem parar).")]
        [SerializeField] float dodgeCooldown = 0.4f;
        [Tooltip("Trecho do rolamento (fração 0–1) em que nada acerta o personagem.")]
        [SerializeField] Vector2 dodgeInvulnerable = new(0.05f, 0.6f);
        [Tooltip("Rolar apertado até estes segundos antes de poder fica guardado (sai assim que der).")]
        [SerializeField] float dodgeBuffer = 0.2f;
        [Tooltip("Clique de ataque no meio do rolamento: a partir desta fração o rolamento termina e o golpe sai.")]
        [SerializeField, Range(0.5f, 1f)] float rollAttackFrom = 0.8f;

        [Tooltip("Derrubado: depois destes segundos no chão, o rolamento levanta rolando (jogador; a IA fica até levantar).")]
        [SerializeField] bool rollOutOfKnockdown;
        [SerializeField] float rollOutAfter = 0.8f;

        [Header("Empurrão (explosão)")]
        [Tooltip("m/s² com que o empurrão some (o personagem desliza e recupera o controle).")]
        [SerializeField] float pushDrag = 10f;
        [Tooltip("Fração do empurrão que pega (brutamontes e chefe são pesados: recuam bem menos).")]
        [SerializeField, Range(0f, 1f)] float pushScale = 1f;

        CharacterController controller;
        IFootInput input;
        IOceanSampler ocean;
        Vector3 planarVelocity, pushVelocity, lungeVelocity, stepVelocity, animatedExtra;
        float stepUntil, dodgeBufferedUntil = float.NegativeInfinity;
        float verticalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        Vector3 lastSafePosition;
        FootCommand? commandOverride;
        CharacterCombat combat;
        Stamina stamina;
        CharacterAimRig aimRig;
        bool backpedal;
        Vector3 faceDirection;
        float faceUntil, speedMultiplier = 1f, speedMultiplierUntil;
        float dodgeStart = float.NegativeInfinity, dodgeReadyAt, downUntil = float.NegativeInfinity, downSince = float.NegativeInfinity;
        Vector3 dodgeDirection;

        public float PlanarSpeed => planarVelocity.magnitude;
        public Vector3 PlanarVelocity => planarVelocity;
        /// <summary>
        /// Velocidade que a animação das pernas mostra: o andar mais a investida da IA (antes a investida não entrava e o pirata deslizava
        /// parado na pose do golpe). O passo curto do golpe do capitão não conta (golpe com o tronco só, andando, fica torto).
        /// </summary>
        public Vector3 AnimatedVelocity => planarVelocity + animatedExtra;
        /// <summary>
        /// Andando de lado ou de ré virado para um ponto (guarda, mira, IA encarando): o corpo fica de frente para o ponto e as pernas usam
        /// as animações de andar de lado/de ré (estado "Lateral" do Animator). Antes o corpo virava até 60° para onde andava.
        /// </summary>
        public bool IsStrafing { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public bool IsGrounded { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        /// <summary>Pés na água acima do limite (empurrado de volta para a praia).</summary>
        public bool InDeepWater { get; private set; }
        /// <summary>Mirando e andando para trás das pernas: a animação de andar toca ao contrário.</summary>
        public bool WalkingBackward { get; private set; }
        /// <summary>Fora de ação (morto): não obedece comandos e não colide (o colisor desliga).</summary>
        public bool Incapacitated { get; private set; }
        /// <summary>Rolando (esquiva): anda sozinho na direção do rolamento, sem atacar nem mirar.</summary>
        public bool IsDodging => Time.time - dodgeStart < dodgeSeconds;
        /// <summary>No meio do rolamento: golpes, tiros e explosões não acertam (quem cuida é o FootHealthReactions, pela Health).</summary>
        public bool DodgeInvulnerable
        {
            get
            {
                float f = (Time.time - dodgeStart) / dodgeSeconds;
                return f >= dodgeInvulnerable.x && f < dodgeInvulnerable.y;
            }
        }
        public float DodgeSeconds => dodgeSeconds;
        /// <summary>Derrubado (chute, explosão): no chão até levantar, sem obedecer comandos (o empurrão e a gravidade continuam).</summary>
        public bool IsDown => Time.time < downUntil;
        /// <summary>Quando levantou (ou levanta) da última queda.</summary>
        public float DownUntil => downUntil;

        public event Action Jumped;
        /// <summary>Começou a rolar (animação, som, poeira).</summary>
        public event Action Dodged;
        /// <summary>Foi derrubado (som e poeira da queda).</summary>
        public event Action KnockedDown;
        /// <summary>Caiu no mar fundo e voltou para o último chão seguro.</summary>
        public event Action RescuedFromWater;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<IFootInput>();
            combat = GetComponent<CharacterCombat>();
            stamina = GetComponent<Stamina>();
            aimRig = GetComponentInChildren<CharacterAimRig>(true);
            lastSafePosition = transform.position;
        }

        /// <summary>Fração do empurrão que pega (os pesados recuam menos).</summary>
        public void ConfigurePushScale(float scale) => pushScale = Mathf.Clamp01(scale);

        /// <summary>Giro máximo em °/s (0 = sem limite): os grandes viram mais devagar.</summary>
        public void ConfigureTurnRate(float degreesPerSecond) => maxTurnSpeed = Mathf.Max(0f, degreesPerSecond);

        /// <summary>Velocidades de andar e correr (piratas inimigos correm um pouco menos que o jogador).</summary>
        public void ConfigureSpeeds(float walk, float run)
        {
            walkSpeed = walk;
            runSpeed = run;
        }

        /// <summary>Comando fixo no lugar do input (testes automáticos, cutscenes). null volta ao input normal.</summary>
        public void SetCommandOverride(FootCommand? command) => commandOverride = command;

        /// <summary>Derrubado, o rolamento levanta rolando depois de <see cref="rollOutAfter"/> (o capitão do jogador).</summary>
        public void AllowRollOutOfKnockdown(bool value) => rollOutOfKnockdown = value;

        /// <summary>Liga/desliga o "fora de ação" (morto/levantando): para no lugar e o colisor some.</summary>
        public void SetIncapacitated(bool value)
        {
            Incapacitated = value;
            dodgeStart = float.NegativeInfinity;
            // Morto caído continua "no chão" (a animação fica deitada); só reviver zera.
            if (!value) downUntil = float.NegativeInfinity;
            planarVelocity = pushVelocity = Vector3.zero;
            verticalVelocity = 0f;
            WalkingBackward = backpedal = false;
            if (controller) controller.enabled = !value;
        }

        /// <summary>Vira para <paramref name="direction"/> por alguns segundos, mesmo andando para outro lado (ataque mirado).</summary>
        public void FaceDuring(Vector3 direction, float seconds)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            faceDirection = direction.normalized;
            faceUntil = Time.time + seconds;
        }

        /// <summary>Anda mais devagar por alguns segundos (ex.: durante um golpe, ou atordoado por um golpe).</summary>
        public void SetSpeedMultiplier(float multiplier, float seconds)
        {
            speedMultiplier = Mathf.Clamp01(multiplier);
            speedMultiplierUntil = Time.time + seconds;
        }

        /// <summary>Explosão: empurra para fora (desliza no chão e some aos poucos) e, com <c>velocity.y</c>, tira do chão.</summary>
        public void Push(Vector3 velocity)
        {
            if (Incapacitated) return;
            velocity *= pushScale;
            pushVelocity += new Vector3(velocity.x, 0f, velocity.z);
            if (velocity.y > 0f)
            {
                verticalVelocity = Mathf.Max(verticalVelocity, velocity.y);
                lastGroundedTime = float.NegativeInfinity;
            }
        }

        /// <summary>Investida da IA: soma esta velocidade ao movimento só neste quadro (quem chama repete a cada quadro do avanço).</summary>
        public void Lunge(Vector3 velocity) => lungeVelocity = new Vector3(velocity.x, 0f, velocity.z);

        /// <summary>Passo do golpe do capitão até o inimigo: soma esta velocidade ao movimento por <paramref name="seconds"/>.</summary>
        public void StepFor(Vector3 velocity, float seconds)
        {
            stepVelocity = new Vector3(velocity.x, 0f, velocity.z);
            stepUntil = Time.time + seconds;
        }

        /// <summary>Coloca o personagem num ponto (desembarque, reaparecer): procura o chão logo abaixo.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // Ponto inválido (infinito ou NaN, como o de um NavMesh.SamplePosition que falhou): ligar o CharacterController lá derrubava
            // o Unity inteiro (crash do PhysX em 16/10, num script de teste). Ignora e avisa.
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
            {
                Debug.LogWarning($"[Pirates] Teleport de {name} para um ponto inválido ({position}): ignorado.");
                return;
            }
            // Desliga o colisor antes: senão o raio pode bater no próprio personagem.
            controller.enabled = false;
            if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point + Vector3.up * 0.05f;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            controller.enabled = !Incapacitated;
            planarVelocity = pushVelocity = Vector3.zero;
            verticalVelocity = 0f;
            lastSafePosition = position;
        }

        void Update()
        {
            if (Incapacitated) return;
            float dt = Time.deltaTime;
            var command = commandOverride ?? (input != null ? input.GetCommand() : default);
            // Rolar guardado um instante: apertado no fim do golpe, da espera ou da queda, sai assim que puder.
            if (command.dodge) dodgeBufferedUntil = Time.time + dodgeBuffer;
            bool wantsDodge = Time.time <= dodgeBufferedUntil;
            if (IsDown && rollOutOfKnockdown && wantsDodge && Time.time - downSince >= rollOutAfter && IsGrounded && (!stamina || stamina.CanDodge))
            {
                // Levanta rolando (revisão de 09/10: caído no meio da luta, o acampamento matava o capitão no chão).
                downUntil = Time.time;
                dodgeReadyAt = 0f;
            }
            if (IsDown || IsDodging) lungeVelocity = Vector3.zero;
            if (IsDown || IsDodging)
            {
                IsStrafing = false;
                animatedExtra = Vector3.zero;
            }
            if (IsDown)
            {
                if (combat) combat.Tick(default);
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, acceleration * dt);
                MoveBody(dt);
                return;
            }
            // Rolar gasta fôlego (sem fôlego: não rola e a barra pisca).
            if (wantsDodge && CanDodge)
            {
                dodgeBufferedUntil = float.NegativeInfinity;
                if (!stamina || stamina.TrySpend(stamina.DodgeCost)) BeginDodge(command);
            }
            if (IsDodging)
            {
                // Clique no meio do rolamento fica guardado; perto do fim, o rolamento termina e o golpe sai (rolar e emendar o golpe).
                if (command.attack && combat) combat.BufferAttack(dodgeStart + dodgeSeconds + 0.2f);
                if (combat && combat.HasBufferedAttack && (Time.time - dodgeStart) / dodgeSeconds >= rollAttackFrom) dodgeStart = Time.time - dodgeSeconds;
            }
            if (IsDodging)
            {
                // Rolando: só o rolamento (sem atacar, mirar, trocar de arma nem pular).
                if (combat) combat.Tick(default);
                Dodge(dt);
                return;
            }
            if (combat) combat.Tick(command);
            bool slowed = Time.time < speedMultiplierUntil;
            float speedScale = slowed ? speedMultiplier : 1f;
            // Mirando a pistola: anda devagar e não corre.
            bool aimHeld = combat && combat.IsAimHeld;
            if (aimHeld) speedScale *= combat.Current.aimMoveSpeed;
            // Guarda erguida (cutelo): anda devagar, não corre e fica de frente para o cursor.
            bool guard = combat && combat.IsBlocking;
            if (guard) speedScale *= combat.BlockMoveSpeed;

            // Direção pedida no plano (o input do jogador já converteu da câmera).
            Vector3 wish = new(command.move.x, 0f, command.move.y);
            float targetSpeed = wish.sqrMagnitude > 0.0001f ? (command.run && !aimHeld && !guard ? runSpeed : walkSpeed) * Mathf.Clamp01(wish.magnitude) * speedScale : 0f;
            Vector3 targetVelocity = wish.sqrMagnitude > 0.0001f ? wish.normalized * targetSpeed : Vector3.zero;
            float accel = acceleration * (IsGrounded ? 1f : airControl) * (slowed ? attackBrake : 1f);
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, accel * dt);

            if (aimHeld && combat.HasAimPoint) FaceAim(dt, combat.AimTarget, aimRig ? aimRig.ArmYawOffset : 0f);
            else if (guard && command.hasAim) FaceAim(dt, command.aimPoint, 0f, guardTwist);
            else if (command.faceAimPoint && command.hasAim && Time.time >= faceUntil) FaceAim(dt, command.aimPoint, 0f);
            else
            {
                WalkingBackward = backpedal = false;
                IsStrafing = false;
                // Atacando: olha para a mira; senão, para onde anda.
                bool facing = Time.time < faceUntil;
                if (facing || planarVelocity.sqrMagnitude > 0.01f)
                {
                    var look = Quaternion.LookRotation(facing ? faceDirection : planarVelocity.normalized, Vector3.up);
                    // Atacando gira mais rápido: o golpe sai virado para o cursor.
                    float sharpness = facing ? turnSharpness * 2f : turnSharpness;
                    Turn(look, sharpness, dt);
                }
            }

            // Pulo (com "coyote time") e gravidade.
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (command.jump && Time.time - lastGroundedTime <= coyoteTime && verticalVelocity <= 0f)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                lastGroundedTime = float.NegativeInfinity;
                Jumped?.Invoke();
            }
            verticalVelocity += gravity * dt;

            Vector3 before = transform.position;
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, pushDrag * dt);
            Vector3 step = Time.time < stepUntil ? stepVelocity : Vector3.zero;
            controller.Move((planarVelocity + pushVelocity + lungeVelocity + step + Vector3.up * verticalVelocity) * dt);
            animatedExtra = lungeVelocity;
            lungeVelocity = Vector3.zero;
            IsGrounded = controller.isGrounded;
            if (IsGrounded) lastGroundedTime = Time.time;

            KeepOutOfDeepWater(before);
        }

        /// <summary>Derruba por <paramref name="seconds"/> (quem chama cuida da animação); cancela o rolamento.</summary>
        public void KnockDown(float seconds)
        {
            if (Incapacitated || seconds <= 0f) return;
            downUntil = Time.time + seconds;
            downSince = Time.time;
            dodgeStart = float.NegativeInfinity;
            faceUntil = 0f;
            WalkingBackward = backpedal = false;
            KnockedDown?.Invoke();
        }

        bool CanDodge => IsGrounded && !IsDodging && Time.time >= dodgeReadyAt && !InDeepWater && !(combat && combat.ActionLocked);

        /// <summary>
        /// Rola para onde está andando; parado, para longe da mira (revisão de 21/10: rolava para a frente, para dentro do inimigo). Vira de
        /// uma vez para o lado do rolamento (antes girava no começo e o rolamento de ré saía de costas). O golpe ou tiro que ia sair é cancelado.
        /// </summary>
        void BeginDodge(in FootCommand command)
        {
            var wish = new Vector3(command.move.x, 0f, command.move.y);
            Vector3 away = transform.position - command.aimPoint;
            away.y = 0f;
            dodgeDirection = wish.sqrMagnitude > 0.01f ? wish : command.hasAim && away.sqrMagnitude > 0.25f ? away : transform.forward;
            dodgeDirection.y = 0f;
            dodgeDirection.Normalize();
            transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up);
            stepUntil = 0f;
            dodgeStart = Time.time;
            dodgeReadyAt = Time.time + dodgeSeconds + dodgeCooldown;
            faceUntil = 0f;
            WalkingBackward = backpedal = false;
            if (combat) combat.Interrupt(0.1f);
            Dodged?.Invoke();
        }

        /// <summary>Um quadro do rolamento: o clipe anda quase sempre no mesmo ritmo; freia no último quinto (sai já andando, sem tranco).</summary>
        void Dodge(float dt)
        {
            float f = (Time.time - dodgeStart) / dodgeSeconds;
            float speed = dodgeDistance / dodgeSeconds * (f < 0.8f ? 1.05f : Mathf.Lerp(1.05f, 0.6f, (f - 0.8f) / 0.2f));
            planarVelocity = dodgeDirection * speed;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dodgeDirection, Vector3.up), 1f - Mathf.Exp(-turnSharpness * 2f * dt));
            MoveBody(dt);
        }

        /// <summary>Move com a velocidade atual, o empurrão e a gravidade (rolamento, no chão), sem ler comando.</summary>
        void MoveBody(float dt)
        {
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            Vector3 before = transform.position;
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, pushDrag * dt);
            controller.Move((planarVelocity + pushVelocity + Vector3.up * verticalVelocity) * dt);
            IsGrounded = controller.isGrounded;
            if (IsGrounded) lastGroundedTime = Time.time;
            KeepOutOfDeepWater(before);
        }

        /// <summary>
        /// Vira para um ponto andando para outro lado. "Neutro" é o giro que deixa o braço da pose de mira apontado para
        /// o alvo (<paramref name="armYawOffset"/>: o braço do clipe aponta de lado; 0 = olhar de frente). Andando, as
        /// pernas seguem o movimento, para a frente ou de ré, até maxAimTwist do neutro; o CharacterAimRig gira o tronco
        /// o que faltar.
        /// </summary>
        void FaceAim(float dt, Vector3 point, float armYawOffset, float twist = -1f)
        {
            if (twist < 0f) twist = maxAimTwist;
            Vector3 toAim = point - transform.position;
            toAim.y = 0f;
            if (toAim.sqrMagnitude < 0.25f) return;   // ponto em cima do personagem: mantém o giro
            float neutral = Yaw(toAim) - armYawOffset;
            float bodyYaw = neutral;
            float speed = planarVelocity.magnitude;
            bool moving = speed > 0.3f;
            // De lado ou de ré (com folga entre 25° e 40° para não ficar trocando): o corpo fica no neutro e as pernas andam de lado.
            float sideways = moving ? Mathf.Abs(Mathf.DeltaAngle(neutral, Yaw(planarVelocity))) : 0f;
            IsStrafing = moving && sideways > (IsStrafing ? 25f : 40f);
            if (IsStrafing)
            {
                WalkingBackward = backpedal = false;
                Turn(Quaternion.Euler(0f, bodyYaw, 0f), aimTurnSharpness, dt);
                return;
            }
            if (moving)
            {
                float moveYaw = Yaw(planarVelocity);
                // Folga entre 70° e 110°: andando de lado não fica trocando entre frente e ré. Só decide com
                // velocidade de verdade: freando/invertendo, a direção do movimento oscila perto do zero.
                float off = Mathf.Abs(Mathf.DeltaAngle(neutral, moveYaw));
                if (speed > 0.8f)
                {
                    if (off > 110f) backpedal = true;
                    else if (off < 70f) backpedal = false;
                }
                float legs = backpedal ? moveYaw + 180f : moveYaw;
                // Devagar, as pernas ficam no neutro; andando, seguem o movimento (sem pulo no giro).
                float follow = Mathf.InverseLerp(0.3f, 1.2f, speed);
                bodyYaw = neutral + Mathf.Clamp(Mathf.DeltaAngle(neutral, legs), -twist, twist) * follow;
            }
            WalkingBackward = moving && backpedal;
            Turn(Quaternion.Euler(0f, bodyYaw, 0f), aimTurnSharpness, dt);
        }

        /// <summary>Gira para <paramref name="look"/> (amortecido), sem passar de <see cref="maxTurnSpeed"/>.</summary>
        void Turn(Quaternion look, float sharpness, float dt)
        {
            var next = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-sharpness * dt));
            if (maxTurnSpeed > 0f) next = Quaternion.RotateTowards(transform.rotation, next, maxTurnSpeed * dt);
            transform.rotation = next;
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        void KeepOutOfDeepWater(Vector3 before)
        {
            if (ocean == null && !Services.TryGet(out ocean)) return;
            Vector3 feet = transform.position;
            // Nível médio do mar, não a onda do momento: com a onda, o limite "piscava" a cada vale e o
            // personagem ia entrando no mar aos pouquinhos.
            float depth = ocean.SeaLevel - feet.y;
            InDeepWater = depth > maxWadeDepth;

            // De pé no fundo além do limite (andando não se chega lá: o passo que afunda é desfeito no limite, que passa só uns 3 cm) =
            // caiu do píer ou de uma pedra no mar. Antes ficava preso: todo passo era desfeito, inclusive o de volta (Jhonata, 09/10).
            // Volta ao último lugar seguro.
            if (depth > drownDepth || (IsGrounded && depth > maxWadeDepth + 0.15f))
            {
                Teleport(lastSafePosition, transform.rotation);
                RescuedFromWater?.Invoke();
                return;
            }
            // Na água funda, só o passo que afunda é desfeito: andar no plano ou para o raso continua valendo.
            if (InDeepWater && feet.y < before.y - 0.001f)
            {
                // Desfaz o passo para o fundo e para de empurrar para lá. No chão volta inteiro (com a altura: mantendo a nova, o
                // personagem descia uns centímetros a cada quadro empurrando a beira); no ar mantém a queda.
                controller.enabled = false;
                transform.position = new Vector3(before.x, IsGrounded ? before.y : feet.y, before.z);
                controller.enabled = true;
                planarVelocity = pushVelocity = Vector3.zero;
                return;
            }
            if (IsGrounded && depth < maxWadeDepth * 0.5f) lastSafePosition = feet;
        }
    }
}
