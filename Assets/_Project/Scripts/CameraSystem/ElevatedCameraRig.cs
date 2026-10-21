using Pirates.Controls;
using Pirates.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.CameraSystem
{
    /// <summary>
    /// Câmera elevada que segue o alvo com suavização e olha um pouco à frente do movimento.
    /// Zoom altera distância e inclinação juntos (perto = mais cinematográfica, longe = mais tática).
    /// Com um ponto de mira ativo, enquadra navio e alvo juntos (se afasta e desliza para o alvo).
    /// </summary>
    public class ElevatedCameraRig : MonoBehaviour, ICameraAimFocus, ICameraFollow, ICameraShake
    {
        [SerializeField] Transform target;

        [Header("Distância / inclinação")]
        [SerializeField] float minDistance = 18f;
        [SerializeField] float maxDistance = 75f;
        [SerializeField] float startDistance = 40f;
        [SerializeField] float minPitch = 32f;
        [SerializeField] float maxPitch = 62f;
        [SerializeField] float zoomStep = 5f;
        [SerializeField] float stickZoomSpeed = 30f;

        [Header("Órbita")]
        [SerializeField] float orbitSpeed = 90f;
        [Tooltip("Graus por segundo segurando Q/E (a pé).")]
        [SerializeField] float keyOrbitSpeed = 110f;
        [SerializeField] float mouseOrbitSensitivity = 0.25f;
        [Tooltip("Graus de inclinação por pixel de mouse (botão direito + mouse vertical).")]
        [SerializeField] float mousePitchSensitivity = 0.15f;
        [SerializeField] float stickPitchSpeed = 45f;
        [Tooltip("Limites absolutos da inclinação: baixo = quase no nível do mar, alto = visão de cima.")]
        [SerializeField] float pitchLimitMin = 12f;
        [SerializeField] float pitchLimitMax = 85f;

        [Header("Seguir")]
        [SerializeField] float followSharpness = 5f;
        [SerializeField] float rotationSharpness = 10f;
        [SerializeField] float zoomSharpness = 8f;
        [Tooltip("Segundos de velocidade do alvo usados para antecipar o enquadramento.")]
        [SerializeField] float lookAheadTime = 1.2f;

        [Header("Mira")]
        [Tooltip("0 = foco no navio, 1 = foco no ponto de mira.")]
        [SerializeField, Range(0f, 1f)] float aimFocusBlend = 0.5f;
        [Tooltip("Margem (m) em volta do navio e do alvo ao enquadrar a mira.")]
        [SerializeField] float aimFramingMargin = 20f;
        [SerializeField] float maxAimDistance = 130f;

        Rigidbody targetBody;
        bool hasAimPoint;
        Vector3 aimPoint;
        Vector3 focus;
        float distance, targetDistance;
        float yaw, targetYaw;
        // Ajuste do jogador somado à inclinação que o zoom já define.
        float pitchOffset, targetPitchOffset;

        /// <summary>O que a câmera segue (navio ou personagem a pé).</summary>
        public Transform Target => target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            targetBody = target ? target.GetComponentInParent<Rigidbody>() : null;
        }

        /// <summary>Segue outro alvo com outra faixa de zoom (ex.: personagem a pé fica mais perto que o navio).</summary>
        public void Follow(Transform newTarget, float min, float max, float start)
        {
            SetTarget(newTarget);
            minDistance = Mathf.Max(1f, min);
            maxDistance = Mathf.Max(minDistance, max);
            targetDistance = Mathf.Clamp(start, minDistance, maxDistance);
            targetPitchOffset = 0f;
            hasAimPoint = false;
        }

        public void SnapToTarget()
        {
            if (!target) return;
            focus = target.position;
            yaw = targetYaw = target.eulerAngles.y;
            distance = targetDistance;
            pitchOffset = targetPitchOffset;
            Apply();
        }

        public void LookToward(float newYaw) => targetYaw += Mathf.DeltaAngle(targetYaw, newYaw);

        public void SetAimPoint(Vector3 worldPoint)
        {
            hasAimPoint = true;
            aimPoint = worldPoint;
        }

        public void ClearAimPoint() => hasAimPoint = false;

        void OnEnable()
        {
            Services.Register<ICameraAimFocus>(this);
            Services.Register<ICameraFollow>(this);
            Services.Register<ICameraShake>(this);
        }

        float shakeStrength, shakeStart, shakeSeconds;

        /// <summary>Tremida (passo 5 da revisão das animações): ruído que some em <paramref name="seconds"/> de tempo real.</summary>
        public void Shake(float strength, float seconds)
        {
            float now = Time.unscaledTime;
            float left = ShakeLeft(now);
            if (strength < left) return;
            shakeStrength = strength;
            shakeStart = now;
            shakeSeconds = Mathf.Max(0.01f, seconds);
        }

        float ShakeLeft(float now)
        {
            if (shakeStrength <= 0f) return 0f;
            float k = 1f - (now - shakeStart) / shakeSeconds;
            return k <= 0f ? 0f : shakeStrength * k * k;
        }

        void OnDisable()
        {
            Services.Unregister<ICameraAimFocus>(this);
            Services.Unregister<ICameraFollow>(this);
            Services.Unregister<ICameraShake>(this);
        }

        void Start()
        {
            SetTarget(target);
            distance = targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            if (target)
            {
                focus = target.position;
                yaw = targetYaw = target.eulerAngles.y;
            }
            Apply();
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;
            ReadInput(dt);

            Vector3 desiredFocus = target.position;
            if (targetBody)
            {
                Vector3 v = targetBody.linearVelocity;
                desiredFocus += new Vector3(v.x, 0f, v.z) * lookAheadTime;
            }

            float desiredDistance = targetDistance;
            if (hasAimPoint)
            {
                Vector3 toAim = aimPoint - target.position;
                toAim.y = 0f;
                desiredFocus = target.position + toAim * aimFocusBlend;
                // Distância para caber navio e alvo: meia largura visível aproximada = distância * tan(meio FOV horizontal).
                float halfSpan = Mathf.Max(aimFocusBlend, 1f - aimFocusBlend) * toAim.magnitude + aimFramingMargin;
                float halfFovTan = Mathf.Tan(HorizontalHalfFov() * Mathf.Deg2Rad);
                desiredDistance = Mathf.Clamp(Mathf.Max(targetDistance, halfSpan / halfFovTan), minDistance, maxAimDistance);
            }

            focus = Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-followSharpness * dt));
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rotationSharpness * dt));
            pitchOffset = Mathf.Lerp(pitchOffset, targetPitchOffset, 1f - Mathf.Exp(-rotationSharpness * dt));
            distance = Mathf.Lerp(distance, desiredDistance, 1f - Mathf.Exp(-zoomSharpness * dt));
            Apply();
        }

        void ReadInput(float dt)
        {
            var input = GameInput.Instance;

            targetYaw += input.CameraOrbit.ReadValue<float>() * orbitSpeed * dt;
            // Botão do meio + mouse gira em todo lugar. A pé o botão direito é da mira (no navio ele também gira) e Q/E
            // giram a câmera (no navio são os canhões).
            if (input.FootCameraControls) targetYaw += input.CameraOrbitKeys.ReadValue<float>() * keyOrbitSpeed * dt;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.middleButton.isPressed || (!input.FootCameraControls && mouse.rightButton.isPressed)))
            {
                // Sensibilidade e inversão do menu Opções.
                var options = GameOptions.Current;
                Vector2 delta = mouse.delta.ReadValue() * options.cameraSensitivity;
                targetYaw += delta.x * mouseOrbitSensitivity;
                targetPitchOffset -= delta.y * mousePitchSensitivity * (options.invertCamera ? -1f : 1f);
            }
            targetPitchOffset -= input.CameraPitch.ReadValue<float>() * stickPitchSpeed * dt;

            float scroll = input.CameraZoomScroll.ReadValue<float>();
            if (Mathf.Abs(scroll) > 0.01f) targetDistance -= Mathf.Sign(scroll) * zoomStep;
            targetDistance -= input.CameraZoomStick.ReadValue<float>() * stickZoomSpeed * dt;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

            // Mantém o ajuste dentro dos limites absolutos para a inclinação do zoom atual.
            float basePitch = ZoomPitch(targetDistance);
            targetPitchOffset = Mathf.Clamp(targetPitchOffset, pitchLimitMin - basePitch, pitchLimitMax - basePitch);
        }

        float ZoomPitch(float atDistance) =>
            Mathf.Lerp(minPitch, maxPitch, Mathf.InverseLerp(minDistance, maxDistance, atDistance));

        float HorizontalHalfFov()
        {
            var cam = GetComponent<Camera>();
            if (!cam) return 30f;
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            return Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect) * Mathf.Rad2Deg;
        }

        void Apply()
        {
            float pitch = Mathf.Clamp(ZoomPitch(distance) + pitchOffset, pitchLimitMin, pitchLimitMax);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = focus - rotation * Vector3.forward * distance;
            float now = Time.unscaledTime;
            float amount = ShakeLeft(now);
            if (amount > 0f)
            {
                // Ruído rápido no plano da tela (não muda para onde a câmera olha: só sacode).
                float t = now * 38f;
                position += rotation * new Vector3(Mathf.PerlinNoise(t, 0.3f) * 2f - 1f, Mathf.PerlinNoise(0.7f, t) * 2f - 1f, 0f) * amount;
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
