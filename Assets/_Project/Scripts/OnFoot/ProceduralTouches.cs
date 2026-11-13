using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Toques feitos por código depois da animação (07/11, revisão das animações, passo 7 de 7):
    /// <list type="bullet">
    /// <item><b>Inclinar nas curvas:</b> correndo em curva o corpo inteiro inclina para dentro dela (em volta do chão entre os pés), como
    /// quem corre de verdade; parado ou em linha reta, nada.</item>
    /// <item><b>Pés no chão:</b> a animação é feita em chão plano; no morro o pé de baixo ficava no ar e o de cima entrava no chão. Um raio
    /// embaixo de cada pé acha o chão; o quadril desce o que o pé mais baixo precisa e cada perna estica ou dobra (IK de duas juntas, no
    /// plano em que o joelho já dobra) até o pé encostar; o pé deita um pouco na inclinação.</item>
    /// <item><b>Papagaio:</b> a peça solta no ombro do capitão balança como mola com o movimento do ombro.</item>
    /// </list>
    /// Posto pelo <see cref="SupportHandIK"/> em todo humanoide. Roda depois do PostureLimiter (−20) e antes da arma guiada pelas mãos (50).
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class ProceduralTouches : MonoBehaviour
    {
        [Header("Inclinar nas curvas")]
        [SerializeField] bool lean = true;
        [Tooltip("Graus de inclinação por (graus por segundo de curva × metros por segundo), dividido por 100.")]
        [SerializeField] float leanPerTurn = 1.8f;
        [SerializeField] float maxLean = 12f;
        [SerializeField] float leanSmooth = 0.12f;

        [Header("Pés no chão")]
        [SerializeField] bool feet = true;
        [Tooltip("O quadril desce no máximo isto (metros × escala) para o pé de baixo alcançar o chão.")]
        [SerializeField] float maxHipDrop = 0.3f;
        [Tooltip("Diferença de chão (metros × escala) que o pé corrige no máximo.")]
        [SerializeField] float maxFootShift = 0.35f;
        [Tooltip("O pé deita na inclinação do chão no máximo isto (graus).")]
        [SerializeField] float maxFootTilt = 25f;
        [SerializeField] float hipSmooth = 0.08f;

        [Header("Papagaio")]
        [SerializeField] float parrotStiffness = 90f;
        [SerializeField] float parrotDamping = 9f;
        [Tooltip("Graus de balanço por m/s² de aceleração do ombro.")]
        [SerializeField] float parrotSway = 1.2f;
        [SerializeField] float parrotMax = 25f;

        /// <summary>Teste: inclinação de agora (graus), quanto o quadril desceu (m), quanto cada pé foi corrigido (m).</summary>
        public float LastLean => leanAngle;
        public float LastHipDrop { get; private set; }
        public float LastLeftShift { get; private set; }
        public float LastRightShift { get; private set; }
        public Vector2 LastParrot => parrotAngle;

        /// <summary>Teste: liga/desliga cada toque em Play.</summary>
        public void SetEnabled(bool leanOn, bool feetOn) { lean = leanOn; feet = feetOn; }

        Animator animator;
        FootCharacter character;
        CharacterController controller;
        Transform root, hips;
        Transform lThigh, lKnee, lFoot, rThigh, rKnee, rFoot;
        float lastYaw, leanAngle, leanVel, hipDrop, hipVel;
        bool hasYaw;
        Transform parrot, parrotShoulder;
        Quaternion parrotBase;
        Vector3 lastShoulderPos, lastShoulderVel;
        Vector2 parrotAngle, parrotVel;
        bool hasShoulder;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        void Awake()
        {
            animator = GetComponent<Animator>();
            character = GetComponentInParent<FootCharacter>();
            controller = GetComponentInParent<CharacterController>();
            root = character ? character.transform : transform;
            if (!animator || !animator.isHuman) { enabled = false; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            lKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            // O papagaio do capitão: peça solta "Papagaio" no ombro.
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Papagaio") { parrot = t; parrotShoulder = t.parent; parrotBase = t.localRotation; break; }
        }

        void OnDisable() { hasYaw = false; hasShoulder = false; leanAngle = 0f; hipDrop = 0f; }

        bool Busy => character && (character.IsDown || character.IsDodging);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float scale = root.lossyScale.y;
            // Pés da animação antes de qualquer toque (o alvo dos pés no chão).
            Vector3 animL = lFoot ? lFoot.position : Vector3.zero, animR = rFoot ? rFoot.position : Vector3.zero;
            Quaternion animLRot = lFoot ? lFoot.rotation : Quaternion.identity, animRRot = rFoot ? rFoot.rotation : Quaternion.identity;
            bool grounded = controller ? controller.isGrounded || controller.velocity.y > -1f : true;

            if (lean && hips) Lean(dt, scale, grounded);
            if (feet && hips && lThigh && lKnee && lFoot && rThigh && rKnee && rFoot) Feet(dt, scale, grounded, animL, animR, animLRot, animRRot);
            if (parrot) Parrot(dt);
        }

        // ---------- Inclinar nas curvas ----------
        void Lean(float dt, float scale, bool grounded)
        {
            float yaw = root.eulerAngles.y;
            float yawRate = hasYaw ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
            lastYaw = yaw; hasYaw = true;
            Vector3 v = controller ? controller.velocity : Vector3.zero;
            v.y = 0f;
            float speed = v.magnitude / Mathf.Max(0.01f, scale);
            float target = (grounded && !Busy) ? Mathf.Clamp(-yawRate * speed * leanPerTurn * 0.01f, -maxLean, maxLean) : 0f;
            leanAngle = Mathf.SmoothDamp(leanAngle, target, ref leanVel, leanSmooth, Mathf.Infinity, dt);
            if (Mathf.Abs(leanAngle) < 0.05f) return;
            // Em volta do chão embaixo do quadril (os pés ficam onde estavam; o corpo inclina para dentro da curva).
            Vector3 pivot = new(hips.position.x, root.position.y, hips.position.z);
            Quaternion q = Quaternion.AngleAxis(leanAngle, root.forward);
            hips.SetPositionAndRotation(pivot + q * (hips.position - pivot), q * hips.rotation);
        }

        // ---------- Pés no chão ----------
        void Feet(float dt, float scale, bool grounded, Vector3 animL, Vector3 animR, Quaternion animLRot, Quaternion animRRot)
        {
            float dl = 0f, dr = 0f;
            Vector3 nl = Vector3.up, nr = Vector3.up;
            bool use = grounded && !Busy;
            if (use)
            {
                use &= Ground(animL, scale, out dl, out nl);
                use &= Ground(animR, scale, out dr, out nr);
            }
            float limit = maxFootShift * scale;
            dl = Mathf.Clamp(dl, -limit, limit);
            dr = Mathf.Clamp(dr, -limit, limit);
            float drop = use ? Mathf.Clamp(Mathf.Min(0f, Mathf.Min(dl, dr)), -maxHipDrop * scale, 0f) : 0f;
            hipDrop = Mathf.SmoothDamp(hipDrop, drop, ref hipVel, hipSmooth, Mathf.Infinity, dt);
            LastHipDrop = hipDrop;
            if (!use && Mathf.Abs(hipDrop) < 0.002f) { LastLeftShift = LastRightShift = 0f; return; }
            hips.position += Vector3.up * hipDrop;
            if (!use) return;
            LastLeftShift = dl; LastRightShift = dr;
            Leg(lThigh, lKnee, lFoot, animL + Vector3.up * dl, animLRot, nl);
            Leg(rThigh, rKnee, rFoot, animR + Vector3.up * dr, animRRot, nr);
        }

        /// <summary>Quanto o chão embaixo do pé está acima (+) ou abaixo (−) do chão plano do personagem.</summary>
        bool Ground(Vector3 foot, float scale, out float delta, out Vector3 normal)
        {
            delta = 0f; normal = Vector3.up;
            Vector3 from = new(foot.x, root.position.y + 0.6f * scale, foot.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 1.2f * scale, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(root)) continue;
                if (h.collider is CharacterController) continue;
                if (h.normal.y < 0.5f) continue;
                if (h.distance < best) { best = h.distance; delta = h.point.y - root.position.y; normal = h.normal; }
            }
            return !float.IsPositiveInfinity(best);
        }

        void Leg(Transform thigh, Transform knee, Transform foot, Vector3 target, Quaternion footRot, Vector3 normal)
        {
            Vector3 a = thigh.position, b = knee.position, c = foot.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            float d = Mathf.Clamp((target - a).magnitude, Mathf.Abs(l1 - l2) + 1e-4f, (l1 + l2) * 0.999f);
            // Joelho: ângulo que dá a distância pedida, no plano em que ele já dobra.
            Vector3 ba = a - b, bc = c - b;
            Vector3 axis = Vector3.Cross(ba, bc);
            if (axis.sqrMagnitude < 1e-10f) axis = root.right;
            axis.Normalize();
            float now = Vector3.Angle(ba, bc);
            float want = Mathf.Acos(Mathf.Clamp((l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2), -1f, 1f)) * Mathf.Rad2Deg;
            knee.rotation = Quaternion.AngleAxis(want - now, axis) * knee.rotation;
            // Coxa: aponta o pé para o alvo.
            thigh.rotation = Quaternion.FromToRotation(foot.position - a, target - a) * thigh.rotation;
            // Pé: o giro da animação, deitado na inclinação do chão (limitado).
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
            float ta = Quaternion.Angle(Quaternion.identity, tilt);
            if (ta > maxFootTilt) tilt = Quaternion.Slerp(Quaternion.identity, tilt, maxFootTilt / ta);
            foot.rotation = tilt * footRot;
        }

        // ---------- Papagaio ----------
        void Parrot(float dt)
        {
            Vector3 pos = parrotShoulder.position;
            Vector3 vel = hasShoulder ? (pos - lastShoulderPos) / dt : Vector3.zero;
            Vector3 acc = hasShoulder ? (vel - lastShoulderVel) / dt : Vector3.zero;
            lastShoulderPos = pos; lastShoulderVel = vel; hasShoulder = true;
            // Aceleração no espaço do personagem: para a frente → o papagaio tomba para trás; de lado → para o outro lado.
            Vector3 local = root.InverseTransformDirection(acc);
            // O sobe e desce da corrida também balança (para a frente quando o ombro desce de repente).
            Vector2 push = new(-local.z * parrotSway + local.y * parrotSway * 0.4f, local.x * parrotSway);
            push = Vector2.ClampMagnitude(push, parrotMax * 2f);
            // Mola: o ângulo procura o "empurrão" (graus) com a rigidez e o amortecimento.
            Vector2 force = (push - parrotAngle) * parrotStiffness - parrotVel * parrotDamping;
            parrotVel += force * dt;
            parrotAngle += parrotVel * dt;
            parrotAngle = Vector2.ClampMagnitude(parrotAngle, parrotMax);
            // Gira em volta dos eixos do personagem (frente/trás em volta do lado, de lado em volta da frente), a partir da pose de base.
            Quaternion world = parrotShoulder.rotation * parrotBase;
            Quaternion sway = Quaternion.AngleAxis(parrotAngle.x, root.right) * Quaternion.AngleAxis(parrotAngle.y, root.forward);
            parrot.rotation = sway * world;
        }
    }
}
