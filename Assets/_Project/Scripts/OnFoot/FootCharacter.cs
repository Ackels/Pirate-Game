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
    public class FootCharacter : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float runSpeed = 6f;
        [Tooltip("m/s² para acelerar/frear no chão.")]
        [SerializeField] float acceleration = 14f;
        [Tooltip("Controle no ar (fração da aceleração).")]
        [SerializeField, Range(0f, 1f)] float airControl = 0.35f;
        [SerializeField] float turnSharpness = 12f;
        [Tooltip("Atacando: o freio fica estas vezes mais forte (o golpe não sai deslizando).")]
        [SerializeField] float attackBrake = 3f;

        [Header("Mira (pistola)")]
        [Tooltip("Graus que o tronco gira em relação às pernas. Andando de lado, as pernas seguem o movimento até aqui.")]
        [SerializeField] float maxAimTwist = 60f;
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

        CharacterController controller;
        IFootInput input;
        IOceanSampler ocean;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        Vector3 lastSafePosition;
        FootCommand? commandOverride;
        CharacterCombat combat;
        CharacterAimRig aimRig;
        bool backpedal;
        Vector3 faceDirection;
        float faceUntil, speedMultiplier = 1f, speedMultiplierUntil;

        public float PlanarSpeed => planarVelocity.magnitude;
        public Vector3 PlanarVelocity => planarVelocity;
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

        public event Action Jumped;
        /// <summary>Caiu no mar fundo e voltou para o último chão seguro.</summary>
        public event Action RescuedFromWater;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<IFootInput>();
            combat = GetComponent<CharacterCombat>();
            aimRig = GetComponentInChildren<CharacterAimRig>(true);
            lastSafePosition = transform.position;
        }

        /// <summary>Velocidades de andar e correr (piratas inimigos correm um pouco menos que o jogador).</summary>
        public void ConfigureSpeeds(float walk, float run)
        {
            walkSpeed = walk;
            runSpeed = run;
        }

        /// <summary>Comando fixo no lugar do input (testes automáticos, cutscenes). null volta ao input normal.</summary>
        public void SetCommandOverride(FootCommand? command) => commandOverride = command;

        /// <summary>Liga/desliga o "fora de ação" (morto/levantando): para no lugar e o colisor some.</summary>
        public void SetIncapacitated(bool value)
        {
            Incapacitated = value;
            planarVelocity = Vector3.zero;
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

        /// <summary>Coloca o personagem num ponto (desembarque, reaparecer): procura o chão logo abaixo.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // Desliga o colisor antes: senão o raio pode bater no próprio personagem.
            controller.enabled = false;
            if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point + Vector3.up * 0.05f;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            controller.enabled = !Incapacitated;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            lastSafePosition = position;
        }

        void Update()
        {
            if (Incapacitated) return;
            float dt = Time.deltaTime;
            var command = commandOverride ?? (input != null ? input.GetCommand() : default);
            if (combat) combat.Tick(command);
            bool slowed = Time.time < speedMultiplierUntil;
            float speedScale = slowed ? speedMultiplier : 1f;
            // Mirando a pistola: anda devagar e não corre.
            bool aimHeld = combat && combat.IsAimHeld;
            if (aimHeld) speedScale *= combat.Current.aimMoveSpeed;

            // Direção pedida no plano (o input do jogador já converteu da câmera).
            Vector3 wish = new(command.move.x, 0f, command.move.y);
            float targetSpeed = wish.sqrMagnitude > 0.0001f ? (command.run && !aimHeld ? runSpeed : walkSpeed) * Mathf.Clamp01(wish.magnitude) * speedScale : 0f;
            Vector3 targetVelocity = wish.sqrMagnitude > 0.0001f ? wish.normalized * targetSpeed : Vector3.zero;
            float accel = acceleration * (IsGrounded ? 1f : airControl) * (slowed ? attackBrake : 1f);
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, accel * dt);

            if (aimHeld && combat.HasAimPoint) FaceAim(dt, combat.AimTarget, aimRig ? aimRig.ArmYawOffset : 0f);
            else if (command.faceAimPoint && command.hasAim && Time.time >= faceUntil) FaceAim(dt, command.aimPoint, 0f);
            else
            {
                WalkingBackward = backpedal = false;
                // Atacando: olha para a mira; senão, para onde anda.
                bool facing = Time.time < faceUntil;
                if (facing || planarVelocity.sqrMagnitude > 0.01f)
                {
                    var look = Quaternion.LookRotation(facing ? faceDirection : planarVelocity.normalized, Vector3.up);
                    // Atacando gira mais rápido: o golpe sai virado para o cursor.
                    float sharpness = facing ? turnSharpness * 2f : turnSharpness;
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-sharpness * dt));
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
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
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
        void FaceAim(float dt, Vector3 point, float armYawOffset)
        {
            Vector3 toAim = point - transform.position;
            toAim.y = 0f;
            if (toAim.sqrMagnitude < 0.25f) return;   // ponto em cima do personagem: mantém o giro
            float neutral = Yaw(toAim) - armYawOffset;
            float bodyYaw = neutral;
            float speed = planarVelocity.magnitude;
            bool moving = speed > 0.3f;
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
                bodyYaw = neutral + Mathf.Clamp(Mathf.DeltaAngle(neutral, legs), -maxAimTwist, maxAimTwist) * follow;
            }
            WalkingBackward = moving && backpedal;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, bodyYaw, 0f), 1f - Mathf.Exp(-aimTurnSharpness * dt));
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

            if (depth > drownDepth)
            {
                Teleport(lastSafePosition, transform.rotation);
                RescuedFromWater?.Invoke();
                return;
            }
            if (InDeepWater)
            {
                // Desfaz o passo para o fundo (mantém a queda vertical) e para de empurrar para lá.
                controller.enabled = false;
                transform.position = new Vector3(before.x, feet.y, before.z);
                controller.enabled = true;
                planarVelocity = Vector3.zero;
                return;
            }
            if (IsGrounded && depth < maxWadeDepth * 0.5f) lastSafePosition = feet;
        }
    }
}
