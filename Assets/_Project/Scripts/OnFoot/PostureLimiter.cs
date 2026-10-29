using System.Collections.Generic;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Postura nos golpes (23/10, pedido do Jhonata: "fica todo contorcido"): os golpes do Mixamo dobram o tronco e abaixam a cabeça para olhar o
    /// alvo no chão; no capitão, cabeçudo e de chapéu grande, a cabeça chegava a 90° (o chapéu virado para o chão) e o tronco a ~60°. Depois do
    /// Animator, enquanto o tronco está num estado de golpe, endireita a coluna e o pescoço com um limite suave: até <c>torsoStart</c>/<c>headStart</c>
    /// fica como o clipe, acima disso só <c>torsoKeep</c>/<c>headKeep</c> do que passa (medido no combo do machado:
    /// cabeça 91° → 31°, tronco 62° → 34°). Os braços vão junto com o peito (a lâmina sobe um pouco).
    /// Entra e sai com uma mistura curta, então rolar, cair e as outras ações ficam como estão. Fica no objeto do Animator.
    /// <para>Torção do pescoço (23/10, "bate e o rosto buga"): no corte do cutelo do Mixamo o corpo gira ~120° e a cabeça continua olhando o
    /// alvo, 150° torcida em relação ao peito; a barba do capitão é presa ao peito e ao pescoço, então girava junto e o rosto torcia. Sempre
    /// (não só nos golpes) o pescoço e a cabeça giram no máximo <c>neckTwist</c>/<c>headTwist</c> em relação ao peito: no giro a cabeça
    /// acompanha o corpo.</para>
    /// <para>Inimigos (23/10, revisão): piratas, esqueletos e Guarda Negra (corte do cutelo, até 158° de torção), duelista (118°), brutamontes
    /// (tronco 61°, cabeça 81°, 119°) e Barba-Negra (88°, com a barba do mesmo modelo do capitão) usam os mesmos clipes e ganharam o mesmo
    /// componente, com o tronco e a cabeça um pouco mais soltos (<see cref="ConfigureBend"/>).</para>
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(Animator))]
    public class PostureLimiter : MonoBehaviour
    {
        [Tooltip("Inclinação do tronco (quadril → cabeça, em graus da vertical) que o golpe pode ter sem correção.")]
        [SerializeField] float torsoStart = 18f;
        [Tooltip("Fração do que passa do limite que fica (0 = trava no limite, 1 = sem correção).")]
        [Range(0f, 1f)] [SerializeField] float torsoKeep = 0.25f;
        [Tooltip("Quanto a cabeça pode apontar para baixo (graus do 'para cima' do osso) sem correção.")]
        [SerializeField] float headStart = 20f;
        [Range(0f, 1f)] [SerializeField] float headKeep = 0.1f;
        [Tooltip("Giro máximo (graus) da cabeça em relação ao peito, em volta do pescoço.")]
        [SerializeField] float headTwist = 50f;
        [Tooltip("Giro máximo (graus) do pescoço em relação ao peito.")]
        [SerializeField] float neckTwist = 25f;
        [Tooltip("Segundos para a correção entrar e sair.")]
        [SerializeField] float blendSeconds = 0.15f;
        [SerializeField] string upperLayer = "Tronco";
        [Tooltip("Prefixos dos estados de golpe do tronco.")]
        [SerializeField] string[] attackStates = { "Golpe", "Machado_Golpe", "Espadao_Golpe", "Adaga_Golpe" };

        Animator animator;
        int layer = -1;
        readonly HashSet<int> attackHashes = new();
        Transform hips, spine, chest, upperChest, neck, head;
        float weight;
        /// <summary>Direção "para cima" da cabeça na pose parada, no espaço do osso (o osso do kit não aponta o eixo Y para cima).</summary>
        Vector3 headUpLocal = Vector3.up;
        /// <summary>Pescoço e cabeça em relação ao peito na pose de ligação, e o eixo do pescoço no espaço do peito.</summary>
        Quaternion neckRest, headRest;
        Vector3 twistAxis = Vector3.up;
        Transform twistRoot;

        void Awake()
        {
            animator = GetComponent<Animator>();
            layer = animator.GetLayerIndex(upperLayer);
            if (layer >= 0)
                foreach (var prefix in attackStates)
                    for (int i = 0; i < 16; i++)
                    {
                        int hash = Animator.StringToHash(prefix + i);
                        if (animator.HasState(layer, hash)) attackHashes.Add(hash);
                    }
            if (!animator.isHuman) return;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            // O "para cima" da cabeça é o do personagem na pose de agora (Awake: pose de ligação, de pé).
            if (head) headUpLocal = Quaternion.Inverse(head.rotation) * transform.up;
            twistRoot = upperChest ? upperChest : chest;
            if (twistRoot && neck && head)
            {
                neckRest = Quaternion.Inverse(twistRoot.rotation) * neck.rotation;
                headRest = Quaternion.Inverse(twistRoot.rotation) * head.rotation;
                twistAxis = (Quaternion.Inverse(twistRoot.rotation) * (head.position - neck.position)).normalized;
            }
        }

        /// <summary>Limites do tronco e da cabeça nos golpes (o gerador põe nos inimigos um pouco mais soltos que no capitão).</summary>
        public void ConfigureBend(float torsoStartDegrees, float torsoKeepFraction, float headStartDegrees, float headKeepFraction)
        {
            torsoStart = torsoStartDegrees;
            torsoKeep = torsoKeepFraction;
            headStart = headStartDegrees;
            headKeep = headKeepFraction;
        }

        bool Attacking()
        {
            if (layer < 0) return false;
            if (attackHashes.Contains(animator.GetCurrentAnimatorStateInfo(layer).shortNameHash)) return true;
            return animator.IsInTransition(layer) && attackHashes.Contains(animator.GetNextAnimatorStateInfo(layer).shortNameHash);
        }

        void LateUpdate()
        {
            if (!hips || !head) return;
            weight = Mathf.MoveTowards(weight, Attacking() ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, blendSeconds));
            if (weight <= 0f)
            {
                LimitTwist();
                return;
            }
            Vector3 up = transform.up;

            // Tronco: quadril → cabeça de volta para o alto, repartido pela coluna.
            Vector3 body = head.position - hips.position;
            float tilt = Vector3.Angle(body, up);
            if (tilt > torsoStart)
            {
                float fix = (tilt - torsoStart) * (1f - torsoKeep) * weight;
                Vector3 axis = Vector3.Cross(body, up);
                if (axis.sqrMagnitude > 1e-6f)
                {
                    axis.Normalize();
                    Bend(spine, axis, fix * 0.3f);
                    Bend(chest, axis, fix * 0.35f);
                    Bend(upperChest ? upperChest : chest, axis, fix * 0.35f);
                }
            }
            LimitTwist();

            // Cabeça: o "para cima" dela de volta para perto do alto, metade no pescoço.
            Vector3 headUp = head.rotation * headUpLocal;
            float pitch = Vector3.Angle(headUp, up);
            if (pitch > headStart)
            {
                float fix = (pitch - headStart) * (1f - headKeep) * weight;
                Vector3 axis = Vector3.Cross(headUp, up);
                if (axis.sqrMagnitude > 1e-6f)
                {
                    axis.Normalize();
                    if (neck) Bend(neck, axis, fix * 0.5f);
                    Bend(head, axis, neck ? fix * 0.5f : fix);
                }
            }
        }

        /// <summary>Pescoço e depois a cabeça: o giro em volta do eixo do pescoço, em relação ao peito, fica dentro do limite (o resto da rotação fica).</summary>
        void LimitTwist()
        {
            if (!twistRoot || !neck) return;
            ClampTwist(neck, neckRest, neckTwist);
            ClampTwist(head, headRest, headTwist);
        }

        void ClampTwist(Transform bone, Quaternion rest, float max)
        {
            // Rotação a mais em relação à pose de ligação, no espaço do peito, separada em balanço × giro em volta do eixo do pescoço.
            Quaternion delta = Quaternion.Inverse(twistRoot.rotation) * bone.rotation * Quaternion.Inverse(rest);
            Vector3 along = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), twistAxis);
            var twist = new Quaternion(along.x, along.y, along.z, delta.w);
            float norm = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (norm < 1e-5f) return;
            twist = new Quaternion(twist.x / norm, twist.y / norm, twist.z / norm, twist.w / norm);
            twist.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Vector3.Dot(axis, twistAxis) < 0f) angle = -angle;
            if (Mathf.Abs(angle) <= max) return;
            Quaternion swing = delta * Quaternion.Inverse(twist);
            bone.rotation = twistRoot.rotation * swing * Quaternion.AngleAxis(Mathf.Clamp(angle, -max, max), twistAxis) * rest;
        }

        static void Bend(Transform bone, Vector3 axis, float degrees)
        {
            if (!bone || Mathf.Abs(degrees) < 0.01f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
