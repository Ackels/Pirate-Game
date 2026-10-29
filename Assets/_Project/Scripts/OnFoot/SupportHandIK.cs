using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Mão de apoio nas armas de duas mãos (27/10, pedido do Jhonata: "ajustar armas... para ficar melhor encaixado na mão", com o IK Helper
    /// Tool do Kevin Iglesias de referência). Os clipes do Mixamo/Explosive são de outros personagens e outras armas: no bacamarte e no
    /// mosquete a mão esquerda ficava ~12 cm fora da arma, e no espadão do Barba-Negra a ~45 cm do cabo. A cada quadro, a mão esquerda vai
    /// (IK de posição do humanoide) a um ponto no eixo comprido da arma na mão: a partir de onde está a mão direita, andando
    /// <see cref="WeaponDefinition.supportOffset"/> × o comprimento da arma para a ponta mais longe dela (boca do cano, lâmina).
    /// O giro do pulso continua o da animação, menos nas armas em que a mão escorrega no cabo (<see cref="WeaponDefinition.supportSlides"/>, o
    /// machado do brutamontes): ali o pulso gira o mínimo para segurar o cabo como a mão direita, espelhada (<see cref="gripMirror"/>; cada
    /// animação põe o punho de um jeito, e a direita é a que segura a arma de verdade). Desliga (0,15 s) trocando de arma, recarregando, nas ações de corpo inteiro (rolar,
    /// chutar, arremessar, alavanca, pesca), caído, morto ou com a arma escondida (comendo). Fica no objeto do Animator; precisa do
    /// "IK Pass" nas camadas do tronco e do corpo inteiro (o gerador liga).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SupportHandIK : MonoBehaviour
    {
        [SerializeField] float blendSeconds = 0.15f;

        [Tooltip("Espelho da pegada: a direção do cabo (para a ponta) no espaço do alvo de IK da mão direita, com estes sinais, é a direção do " +
                 "cabo dentro do punho esquerdo.")]
        // (−1, −1, 1): testado com os 5 espelhos no brutamontes, é o único em que os dedos fecham em volta do cabo (27/10).
        [SerializeField] Vector3 gripMirror = new(-1f, -1f, 1f);

        [Tooltip("Espelho do vão pulso → cabo da mão direita para a esquerda (no espaço do alvo de IK de cada mão).")]
        // 40% do vão do alvo (o alvo de IK fica uns centímetros fora do osso e o vão inteiro dobrava o cotovelo a ~55°, "braço de
        // T-rex"): assim o pulso esquerdo fica a ~11 cm do cabo, como o direito (~10), e o cotovelo da guarda a ~88° (27/10).
        [SerializeField] Vector3 palmMirror = new(-0.4f, -0.4f, 0.4f);

        /// <summary>Direção do cabo (para a ponta) no espaço do alvo de IK da mão direita (medida a cada quadro, para testes).</summary>
        public Vector3 RightGrip { get; private set; }

        /// <summary>Direção mão esquerda → direita da animação no espaço do alvo de IK da mão esquerda (para testes).</summary>
        public Vector3 AnimatedGrip { get; private set; }

        Animator animator;
        CharacterCombat combat;
        FootCharacter character;
        CharacterAnimatorDriver driver;
        Transform rightHand;
        float weight;

        // Eixo da arma em uso, medido uma vez por arma (espaço local do visual).
        Transform measured;
        Renderer firstRenderer;
        Vector3 axisA, axisB;

        void Awake()
        {
            animator = GetComponent<Animator>();
            combat = GetComponentInParent<CharacterCombat>();
            character = GetComponentInParent<FootCharacter>();
            driver = GetComponent<CharacterAnimatorDriver>();
            if (animator.isHuman) rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        void Update()
        {
            bool on = Wanted(out _);
            float step = blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, step);
        }

        bool Wanted(out Transform visual)
        {
            visual = null;
            if (!combat || !rightHand) return false;
            var weapon = combat.Current;
            if (!weapon || weapon.supportOffset == 0f || combat.IsSwapping || combat.IsReloading) return false;
            if (character && (character.IsDown || character.IsDodging)) return false;
            // Ações de corpo inteiro (rolar, chutar, arremessar, alavanca, pesca, cair) soltam a mão; os golpes em movimento do espadão
            // (giro, salto, sequência do Barba-Negra) usam a mesma etiqueta e continuam com as duas mãos.
            if (driver && driver.InFullBodyMove && !combat.IsAttacking) return false;
            visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !visual.gameObject.activeInHierarchy) return false;
            // Guardada (nas costas, na bainha) não puxa a mão.
            var hand = combat.HandSlot(combat.CurrentIndex);
            if (hand && !visual.IsChildOf(hand)) return false;
            if (visual != measured) Measure(visual);
            return firstRenderer && firstRenderer.enabled;
        }

        /// <summary>Eixo mais comprido da caixa das malhas da arma, no espaço do visual.</summary>
        void Measure(Transform visual)
        {
            measured = visual;
            firstRenderer = visual.GetComponentInChildren<Renderer>();
            var filters = visual.GetComponentsInChildren<MeshFilter>();
            bool first = true;
            var box = new Bounds();
            foreach (var f in filters)
            {
                if (!f.sharedMesh) continue;
                var mb = f.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = visual.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                    else box.Encapsulate(p);
                }
            }
            var size = box.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var half = Vector3.zero;
            half[axis] = box.extents[axis];
            axisA = box.center - half;
            axisB = box.center + half;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (weight <= 0.001f || !Wanted(out var visual))
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                return;
            }
            Vector3 a = visual.TransformPoint(axisA), b = visual.TransformPoint(axisB);
            Vector3 ab = b - a;
            float length = ab.magnitude;
            if (length < 0.05f) return;
            Vector3 dir = ab / length;
            // A mão direita (já posta pela animação desta camada) marca o ponto de partida; a ponta mais longe dela é a "frente" da arma.
            float s = Vector3.Dot(rightHand.position - a, dir);
            if (s > length * 0.5f)
            {
                (a, b) = (b, a);
                dir = -dir;
                s = length - s;
            }
            var weapon = combat.Current;
            float at = Mathf.Clamp(s + weapon.supportOffset * length, 0f, length);
            // Os ossos (e a arma presa à mão) ainda estão na pose do quadro anterior, e o alvo de IK da mão direita desta camada já está
            // na pose nova: leva o ponto junto com o quanto esse alvo andou e girou desde o quadro anterior (o alvo não é o osso: tem um
            // deslocamento fixo dele, que assim se cancela; somar a diferença alvo − osso tirava a mão ~10 cm do lugar).
            Vector3 goal = animator.GetIKPosition(AvatarIKGoal.RightHand);
            Quaternion goalRotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
            bool follow = hasPreviousGoal && Time.frameCount - previousGoalFrame == 1 && (goal - previousGoal).sqrMagnitude < 0.25f;
            if (weapon.supportSlides)
            {
                // Escorrega no cabo: o ponto mais perto de onde a animação põe a mão esquerda (levado para a pose do quadro anterior, a da
                // arma), entre o supportOffset e o vão mínimo até a mão direita.
                Vector3 animated = animator.GetIKPosition(AvatarIKGoal.LeftHand);
                if (follow) animated = previousGoal + previousGoalRotation * (Quaternion.Inverse(goalRotation) * (animated - goal));
                float near = s + Mathf.Sign(weapon.supportOffset) * weapon.supportSlideGap * length;
                float lo = Mathf.Min(at, near), hi = Mathf.Max(at, near);
                at = Mathf.Clamp(Vector3.Dot(animated - a, dir), Mathf.Max(0f, lo), Mathf.Min(length, hi));
            }
            Vector3 support = a + dir * at;
            // O alvo de IK é o pulso, e o cabo passa pela palma, uns centímetros dele: na mão direita esse vão é o do alvo até o eixo da
            // arma (no espaço do alvo); a esquerda usa o mesmo vão espelhado (com o pulso no eixo, o cabo atravessava o antebraço).
            Vector3 palm = Vector3.zero;
            if (weapon.supportSlides)
            {
                Vector3 wrist = follow ? previousGoal : goal;
                Quaternion wristRotation = follow ? previousGoalRotation : goalRotation;
                Vector3 onAxis = a + dir * Vector3.Dot(wrist - a, dir);
                palm = Quaternion.Inverse(wristRotation) * (onAxis - wrist);
            }
            if (follow)
            {
                support = goal + goalRotation * (Quaternion.Inverse(previousGoalRotation) * (support - previousGoal));
                dir = goalRotation * (Quaternion.Inverse(previousGoalRotation) * dir);
            }
            Quaternion leftRotation = animator.GetIKRotation(AvatarIKGoal.LeftHand);
            Vector3 leftGoal = animator.GetIKPosition(AvatarIKGoal.LeftHand);
            if ((goal - leftGoal).sqrMagnitude > 0.0001f)
                AnimatedGrip = Quaternion.Inverse(leftRotation) * (goal - leftGoal).normalized;
            RightGrip = Quaternion.Inverse(goalRotation) * dir;
            if (weapon.supportSlides)
            {
                // O cabo passa pelo punho fechado como na mão direita (espelhada): gira o pulso da animação o mínimo para isso.
                Vector3 grip = Vector3.Scale(RightGrip, gripMirror);
                Quaternion held = Quaternion.FromToRotation(leftRotation * grip, dir) * leftRotation;
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, held);
                support -= held * Vector3.Scale(palm, palmMirror);
            }
            else animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            pendingGoal = goal;
            pendingGoalRotation = goalRotation;
            pendingFrame = Time.frameCount;
            LastTarget = support;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, support);
        }

        Vector3 previousGoal, pendingGoal;
        Quaternion previousGoalRotation, pendingGoalRotation;
        int previousGoalFrame = -10, pendingFrame = -10;
        bool hasPreviousGoal;

        /// <summary>Teste: onde a mão de apoio foi mandada no último quadro.</summary>
        public Vector3 LastTarget { get; private set; }

        void LateUpdate()
        {
            // O alvo deste quadro vira o "anterior" do próximo (os ossos ficam nesta pose até lá).
            previousGoal = pendingGoal;
            previousGoalRotation = pendingGoalRotation;
            hasPreviousGoal = pendingFrame == Time.frameCount;
            previousGoalFrame = Time.frameCount;
        }
    }
}
