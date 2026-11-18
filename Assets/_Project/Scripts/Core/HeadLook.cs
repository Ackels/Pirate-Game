using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Olhar (12/11, revisão das animações do chat "Animador", passo 4; Jhonata: animação "travada, estranha"): ninguém virava a cabeça
    /// para o capitão. Depois da animação, o pescoço e a cabeça giram para um ponto (inimigo em alerta: quem ele persegue) ou, com
    /// <see cref="autoRadius"/>, para o capitão quando ele chega perto (NPCs, moradores). Limitado para os lados e para cima/baixo,
    /// suavizado, entra e sai aos poucos. Vale para humanoides e para os rigs genéricos do kit (ossos "Head" e "Neck" pelo nome).
    /// Só trabalha com o Animator animando (parado, a pose não volta a cada quadro e a cabeça ficaria torta).
    /// </summary>
    [DefaultExecutionOrder(30)]
    public class HeadLook : MonoBehaviour
    {
        /// <summary>A cabeça do capitão (posta pelo CharacterAnimatorDriver dele).</summary>
        public static Transform Player { get; set; }

        [Tooltip("Olha para o capitão quando ele chega a esta distância (m); 0 = só quando alguém mandar (SetTarget).")]
        [SerializeField] float autoRadius;
        [SerializeField] float maxYaw = 70f;
        [SerializeField] float maxPitch = 35f;
        [Tooltip("Parte do giro que fica no pescoço (o resto na cabeça).")]
        [SerializeField, Range(0f, 1f)] float neckShare = 0.4f;
        [Tooltip("Segundos para começar/parar de olhar.")]
        [SerializeField] float blendSeconds = 0.35f;
        [Tooltip("Rapidez com que o olhar segue o alvo (1/s).")]
        [SerializeField] float followRate = 7f;

        Transform head, neck;
        Animator animator;
        Vector3 headForwardLocal, neckForwardLocal;
        Vector3 localDir = Vector3.forward;
        Vector3 target;
        bool hasTarget, ready;
        float weight;

        public float Weight => weight;
        public void SetAutoRadius(float meters) => autoRadius = Mathf.Max(0f, meters);
        public void SetTarget(Vector3 worldPoint) { target = worldPoint; hasTarget = true; }
        public void ClearTarget() => hasTarget = false;

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            }
            if (!head) head = Find(transform, "Head");
            if (!neck) neck = Find(transform, "Neck");
            if (!head) { enabled = false; return; }
            // Para onde o rosto aponta, no espaço do osso. Em humanoide, pela pose T do avatar (nela o personagem olha para a frente):
            // pela pose do quadro em que o componente nasce errava (18/11, Jhonata: esqueletos do KayKit "com a cabeça virada para cima o
            // tempo todo"; nasciam saindo da cova, deitados, e a "frente" do rosto ficava apontando para cima). Sem avatar, a pose de agora.
            if (!TPoseForward(head, out headForwardLocal)) headForwardLocal = Quaternion.Inverse(head.rotation) * transform.forward;
            if (neck && !TPoseForward(neck, out neckForwardLocal)) neckForwardLocal = Quaternion.Inverse(neck.rotation) * transform.forward;
            ready = true;
        }

        /// <summary>A frente do personagem no espaço do osso, pela pose T guardada no avatar humanoide (giros locais da raiz até o osso).</summary>
        bool TPoseForward(Transform bone, out Vector3 local)
        {
            local = Vector3.forward;
            if (!animator || !animator.isHuman || !animator.avatar) return false;
            var skeleton = animator.avatar.humanDescription.skeleton;
            if (skeleton == null || skeleton.Length == 0) return false;
            Quaternion rel = Quaternion.identity;
            for (var t = bone; t && t != animator.transform; t = t.parent)
            {
                int i = System.Array.FindIndex(skeleton, s => s.name == t.name);
                if (i < 0) return false;
                rel = skeleton[i].rotation * rel;
            }
            local = Quaternion.Inverse(rel) * Vector3.forward;
            return true;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        void LateUpdate()
        {
            if (!ready || (animator && !animator.isActiveAndEnabled)) return;
            Vector3 point = target;
            bool want = hasTarget;
            if (!want && autoRadius > 0f && Player && Player != head)
            {
                Vector3 d = Player.position - head.position;
                if (d.sqrMagnitude < autoRadius * autoRadius) { point = Player.position; want = true; }
            }
            float dt = Time.deltaTime;
            weight = Mathf.MoveTowards(weight, want ? 1f : 0f, dt / Mathf.Max(0.01f, blendSeconds));
            if (want)
            {
                Vector3 local = transform.InverseTransformDirection(point - head.position);
                if (local.sqrMagnitude > 1e-4f)
                {
                    local.Normalize();
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                    float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                    Vector3 clamped = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                    localDir = Vector3.Slerp(localDir, clamped, 1f - Mathf.Exp(-dt * followRate));
                }
            }
            if (weight <= 0.001f) { localDir = Vector3.forward; return; }
            Vector3 aim = transform.TransformDirection(localDir);
            if (neck)
            {
                Vector3 cur = neck.rotation * neckForwardLocal;
                neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, aim), neckShare * weight) * neck.rotation;
            }
            Vector3 headCur = head.rotation * headForwardLocal;
            head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(headCur, aim), weight) * head.rotation;
        }
    }
}
