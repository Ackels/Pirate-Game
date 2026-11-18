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
    // Antes do PostureLimiter (−20): a direção da lâmina no punho é medida na pose que saiu do IK, antes de ele endireitar o tronco
    // nos golpes (medida depois, ela variava a cada quadro e o desvio da cabeça e o do chão erravam a conta).
    [DefaultExecutionOrder(-30)]
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

        [Header("Alcance da mão de apoio")]
        [Tooltip("A mão de apoio só vai a pontos do cabo a até esta fração do comprimento do braço esquerdo (ombro → pulso). Mais longe ela " +
                 "escorrega no cabo para perto da mão direita; se nem assim alcança, solta aos poucos (em vez de o IK esticar o braço).")]
        [SerializeField] float supportReach = 0.92f;
        [Tooltip("Vão mínimo (metros × escala) entre as duas mãos quando a de apoio escorrega para perto da direita.")]
        [SerializeField] float supportMinGap = 0.1f;
        [Tooltip("Quando nem escorregando o ponto cabe, o braço vai até esta fração do comprimento (quase esticado) na direção dele.")]
        [SerializeField] float fullReach = 0.98f;

        [Header("Guarda alta")]
        [Tooltip("Onde a mão direita fica na guarda das armas de uma mão (metros no espaço do personagem, a partir da altura da cabeça: " +
                 "x para a direita, y para cima da cabeça, z para a frente; × a escala do personagem).")]
        [SerializeField] Vector3 guardHand = new(0.16f, -0.18f, 0.38f);
        [Tooltip("Direção da lâmina na guarda (espaço do personagem): apontada para a frente (o inimigo), um pouco para cima e para a direita " +
                 "(para a esquerda ela cruzava o papagaio do ombro).")]
        [SerializeField] Vector3 guardBlade = new(0.25f, 0.5f, 0.85f);
        [SerializeField] float guardBlendSeconds = 0.18f;

        [Header("Lâmina fora da cabeça")]
        [Tooltip("Volume da cabeça (com chapéu) por avatar: centro e raios de um elipsoide nos eixos do personagem, a partir do osso da cabeça " +
                 "(metros na escala 1; medido nas malhas em 07/11, sem o que fica mais de 8 cm abaixo do osso: a barba do capitão é do osso da cabeça e " +
                 "descia até o peito). Avatar fora da lista: sem desvio.")]
        [SerializeField] HeadVolume[] headVolumes =
        {
            new("Characters_Captain_Barbarossa_Humano", new Vector3(0f, 0.27f, -0.07f), new Vector3(0.39f, 0.35f, 0.52f)),
            new("Characters_Henry_Humano", new Vector3(-0.07f, 0.18f, -0.02f), new Vector3(0.43f, 0.26f, 0.37f)),
            new("Characters_Anne_Humano", new Vector3(0f, 0.21f, -0.09f), new Vector3(0.36f, 0.29f, 0.39f)),
            new("Characters_Sharky_Humano", new Vector3(0.01f, 0.17f, 0.07f), new Vector3(0.26f, 0.25f, 0.31f)),
            new("Characters_Mako_Humano", new Vector3(0f, 0.15f, 0.10f), new Vector3(0.25f, 0.22f, 0.37f)),
            new("Characters_Skeleton_Humano", new Vector3(0f, 0.14f, 0.01f), new Vector3(0.25f, 0.22f, 0.26f)),
        };
        [Tooltip("O braço gira no ombro no máximo isto (graus) para a lâmina não atravessar a cabeça.")]
        [SerializeField] float clearMaxDegrees = 70f;
        [Tooltip("Velocidade (graus por segundo) com que o desvio entra e sai (com 1200 a mão do Barba-Negra girava até 77° num quadro).")]
        [SerializeField] float clearSpeed = 600f;
        [Tooltip("Folga: a lâmina fica a pelo menos isto (em raios do elipsoide) do centro da cabeça (a conta parte do pulso e a lâmina da mão).")]
        [SerializeField] float clearMargin = 1.15f;

        [Header("Lâmina acima do chão")]
        [Tooltip("Nas armas de duas mãos a lâmina não aponta mais para baixo que isto (componente vertical da direção pulso → ponta).")]
        [SerializeField] float bladeFloorY = -0.35f;
        [Tooltip("E a ponta fica pelo menos isto (metros × escala) acima dos pés.")]
        [SerializeField] float bladeTipClearance = 0.08f;
        [Tooltip("O pulso gira no máximo isto (graus) para erguer a lâmina.")]
        [SerializeField] float bladeFloorMaxDegrees = 75f;

        [System.Serializable]
        public struct HeadVolume
        {
            public string avatar;
            public Vector3 center, radii;
            public HeadVolume(string avatar, Vector3 center, Vector3 radii) { this.avatar = avatar; this.center = center; this.radii = radii; }
        }

        /// <summary>Teste: quanto o pulso está girado agora para a lâmina não apontar para o chão (graus).</summary>
        public float FloorLift => floorAngle;

        /// <summary>Teste: quanto o braço está girado agora para a lâmina passar fora da cabeça (graus).</summary>
        public float HeadClearance => clearAngle;

        /// <summary>Teste: distância (em raios) da lâmina da animação até a cabeça no último cálculo (9 = não calculou).</summary>
        public float HeadDistance { get; private set; } = 9f;

        /// <summary>Direção do cabo (para a ponta) no espaço do alvo de IK da mão direita (medida a cada quadro, para testes).</summary>
        public Vector3 RightGrip { get; private set; }

        /// <summary>Direção mão esquerda → direita da animação no espaço do alvo de IK da mão esquerda (para testes).</summary>
        public Vector3 AnimatedGrip { get; private set; }

        Animator animator;
        CharacterCombat combat;
        FootCharacter character;
        CharacterAnimatorDriver driver;
        Transform rightHand;
        GreatswordSwing swingCtl;
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
            if (!animator.isHuman || !animator.avatar) return;
            if (!GetComponent<FingerGrip>()) gameObject.AddComponent<FingerGrip>(); if (!GetComponent<ProceduralTouches>()) gameObject.AddComponent<ProceduralTouches>(); if (!GetComponent<TwoHandSteer>()) gameObject.AddComponent<TwoHandSteer>(); swingCtl = GetComponent<GreatswordSwing>(); if (!swingCtl) swingCtl = gameObject.AddComponent<GreatswordSwing>();
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (leftShoulder && leftElbow && leftHand)
                leftArmLength = (leftElbow.position - leftShoulder.position).magnitude + (leftHand.position - leftElbow.position).magnitude;
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var root = character ? character.transform : transform;
            if (headBone) headToBody = Quaternion.Inverse(headBone.rotation) * root.rotation;
            foreach (var v in headVolumes)
                if (v.avatar == animator.avatar.name) { headVolume = v; hasHeadVolume = headBone && shoulder; }
        }

        [Tooltip("Estados do tronco em que a pose já põe a mão esquerda na arma: sem o IK da mão de apoio (12/11: na corrida com o bacamarte o " +
                 "IK puxava o corpo para alcançar a mão e a arma balançava −25…+17° a cada passada; a pose do Kevin Iglesias segura o cano a 1 cm).")]
        // "Espadao_Correndo" (12/11, Jhonata: "o espadão quando eu fico correndo com ele na mão fica piscando"): na pose de corrida o IK da mão
        // esquerda do humanoide puxava o corpo e a arma saltava ~80° num quadro, duas vezes por passada (sem o IK, nada), e na pose parada o mesmo IK dava um tranco de ~40° no antebraço ao começar a andar. Aqui fica o IK só do braço.
        [SerializeField] string[] posedStates = { "BacamarteParado", "BacamarteCorrendo", "Espadao_Pronto", "Espadao_Correndo" };
        readonly System.Collections.Generic.HashSet<int> posedHashes = new();
        int posedLayer = -2;
        [Tooltip("Nesses estados a mão esquerda vai ao cano por um IK só do braço (ombro e cotovelo), depois da animação: o corpo e a arma " +
                 "não se mexem. Sinal do espelho da pegada da mão direita para a esquerda, e quanto do vão pulso → cano dela a esquerda usa.")]
        [SerializeField] float posedGripSign = -1f;
        [SerializeField] float posedPalm = 0.6f;
        float posedWeight;
        Transform armUpper, armLower, armHand;

        // Bacamarte e mosquete parados/correndo (12/11, Jhonata: "ele segurando as mãos meio embaixo dela", com uma foto de referência): a
        // pose do Kevin Iglesias levava a arma atravessada na horizontal na altura do peito, coronha alta atrás do ombro direito. Agora a
        // arma vai na diagonal na frente do corpo (coronha ao lado do quadril direito, cano subindo para a esquerda e para a frente), a mão
        // direita baixa (perto da cintura) e a esquerda POR BAIXO do cano, com a palma para cima. Os dois braços por IK de duas juntas, depois
        // da animação (o corpo não é puxado). Medidas no espaço do personagem, em metros na escala 1, a mão a partir do peito (respira junto).
        [Header("Armas de fogo de duas mãos paradas/correndo")]
        [SerializeField] Vector3 carryDirection = new(-0.88f, 0.36f, 0.3f);
        [SerializeField] Vector3 carryHandFromChest = new(0.17f, -0.24f, 0.2f);
        [Tooltip("Giro da arma no próprio eixo (graus): 0 = o alto da arma o mais para cima possível.")]
        [SerializeField] float carryRoll;
        [Tooltip("Vão da palma esquerda até o eixo do cano (m na escala 1).")]
        [SerializeField] float carryUnderGap = 0.05f;
        FingerGrip fingers;
        Transform armUpperR, armLowerR;

        static readonly int DrawBackHash = Animator.StringToHash("SacarCostas");

        bool InPosedState()
        {
            // Volta do golpe do espadão feito à mão: a mão esquerda volta ao cabo pelo IK só do braço, aos poucos (17/11).
            if (swingCtl && swingCtl.Recovering) return true;
            // Só com o Animator pronto: desligado (o capitão começa no navio) o GetLayerIndex dá −1 e a pose nunca mais ligava.
            if (posedLayer == -2 && animator.isActiveAndEnabled && animator.isInitialized)
            {
                posedLayer = animator.GetLayerIndex("Tronco");
                if (posedLayer >= 0) foreach (var n in posedStates) { int h = Animator.StringToHash(n); if (animator.HasState(posedLayer, h)) posedHashes.Add(h); }
            }
            if (posedLayer < 0 || posedHashes.Count == 0) return false;
            // Quem guia a arma pelas mãos (brutamontes, Barba-Negra, Lázaro) fica como foi aprovado.
            if (combat && combat.Current && combat.Current.steerByHands) return false;
            int current = animator.GetCurrentAnimatorStateInfo(posedLayer).shortNameHash;
            // Saindo do saque das costas a pose nova entrava com a arma ainda em cima do ombro e o cano descia cruzando o rosto do capitão:
            // espera a transição do saque terminar (a pose de segurar da animação passa por fora da cabeça).
            if (current == DrawBackHash) return false;
            var info = animator.IsInTransition(posedLayer) ? animator.GetNextAnimatorStateInfo(posedLayer) : animator.GetCurrentAnimatorStateInfo(posedLayer);
            return posedHashes.Contains(info.shortNameHash);
        }

        void Update()
        {
            bool wanted = Wanted(out _), posed = wanted && InPosedState();
            bool on = wanted && !posed;
            posedWeight = Mathf.MoveTowards(posedWeight, posed ? 1f : 0f, blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f);
            float step = blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, step);
            guardWeight = Mathf.MoveTowards(guardWeight, GuardWanted() ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, guardBlendSeconds));
        }

        // ---------- Guarda alta (06/11, revisão das animações do chat "Animador", passo 4) ----------
        // A guarda do espadão do Mixamo ("Blocking") ergue a lâmina em diagonal na frente do rosto: de frente fica bonita, mas a câmera do jogo
        // é alta (13 m, ~41°) e o chapéu enorme tapava a lâmina em 3 de 4 ângulos (a guarda parecia a pose de pronto). Na guarda, com arma de
        // uma mão, a mão direita sobe (IK do humanoide, o cotovelo dobra sozinho) para a frente do peito e a lâmina aponta para o inimigo (guarda
        // de esgrima), saindo da frente da aba do chapéu: vista da câmera do jogo aparece em 3–4 de 4 ângulos (antes 1). Testadas 6 posições
        // (atravessada para a esquerda batia no papagaio). Vale para o capitão e para quem usa a guarda (duelista). Medido pela lâmina de cada
        // personagem (direção mão → ponta no espaço do alvo de IK, guardada fora da guarda).

        float guardWeight;
        Transform guardVisual;
        Vector3 guardTipA, guardTipB;
        Vector3 bladeInGoal;
        bool bladeKnown;
        Quaternion lastRightGoal;
        int lastRightGoalFrame = -10;

        bool GuardWanted()
        {
            if (!combat || !rightHand || !driver || !driver.InGuardPose) return false;
            var weapon = combat.Current;
            if (!weapon || weapon.kind != WeaponKind.Melee || weapon.supportOffset != 0f) return false;
            if (character && (character.IsDown || character.IsDodging)) return false;
            return bladeKnown;
        }

        /// <summary>Ponta da lâmina na mão (o lado mais longe da mão do eixo comprido das malhas), medida uma vez por arma.</summary>
        bool BladeTip(out Vector3 tip)
        {
            tip = default;
            if (!combat) return false;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !visual.gameObject.activeInHierarchy) return false;
            if (visual != guardVisual)
            {
                guardVisual = visual;
                bladeKnown = false;
                var box = new Bounds();
                bool first = true;
                foreach (var f in visual.GetComponentsInChildren<MeshFilter>())
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
                guardTipA = box.center; guardTipB = box.center;
                guardTipA[axis] = box.min[axis];
                guardTipB[axis] = box.max[axis];
            }
            Vector3 a = visual.TransformPoint(guardTipA), b = visual.TransformPoint(guardTipB);
            tip = (a - rightHand.position).sqrMagnitude > (b - rightHand.position).sqrMagnitude ? a : b;
            return true;
        }

        /// <summary>Fora da guarda: guarda a direção da lâmina no espaço do alvo de IK da mão direita (o desta mesma pose).</summary>
        void RememberBlade()
        {
            if (guardWeight > 0f || clearAngle > 0.01f || floorAngle > 0.01f || lastRightGoalFrame != Time.frameCount || !rightHand) return;
            var hand = combat ? combat.HandSlot(combat.CurrentIndex) : null;
            var visual = combat ? combat.WeaponVisual(combat.CurrentIndex) : null;
            if (!visual || (hand && !visual.IsChildOf(hand))) return;
            if (!BladeTip(out var tip)) return;
            Vector3 d = tip - rightHand.position;
            if (d.sqrMagnitude < 0.0025f) return;
            bladeInGoal = Quaternion.Inverse(lastRightGoal) * d.normalized;
            bladeKnown = true;
        }

        void GuardIK()
        {
            if (guardWeight <= 0f || !bladeKnown)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }
            var root = character ? character.transform : transform;
            float scale = root.lossyScale.y;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            float headY = head ? head.position.y : root.position.y + scale;
            Vector3 position = root.position + root.right * (guardHand.x * scale) + root.forward * (guardHand.z * scale);
            position.y = headY + guardHand.y * scale;
            Quaternion goal = animator.GetIKRotation(AvatarIKGoal.RightHand);
            Vector3 wanted = root.TransformDirection(guardBlade.normalized);
            Quaternion rotation = Quaternion.FromToRotation(goal * bladeInGoal, wanted) * goal;
            float w = guardWeight * guardWeight * (3f - 2f * guardWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
            animator.SetIKPosition(AvatarIKGoal.RightHand, position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rotation);
        }

        bool Wanted(out Transform visual)
        {
            visual = null;
            if (!combat || !rightHand) return false;
            // O golpe do espadão feito à mão manda nos dois braços (17/11).
            if (swingCtl && swingCtl.Active) return false;
            var weapon = combat.Current;
            if (!weapon || weapon.supportOffset == 0f || combat.IsSwapping || combat.IsReloading) return false;
            if (character && (character.IsDown || character.IsDodging)) return false;
            // Ações de corpo inteiro (rolar, chutar, arremessar, alavanca, pesca, cair) soltam a mão; os golpes em movimento do espadão
            // (giro, salto, sequência do Barba-Negra) usam a mesma etiqueta e continuam com as duas mãos.
            if (driver && driver.InFullBodyMove && !combat.IsAttacking) return false;
            if (driver && driver.InWeaponChange) return false;
            visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !visual.gameObject.activeInHierarchy) return false;
            // Guardada (nas costas, na bainha) não puxa a mão.
            var hand = combat.HandSlot(combat.CurrentIndex);
            if (hand && !visual.IsChildOf(hand)) return false;
            if (visual != measured) Measure(visual);
            return firstRenderer && firstRenderer.enabled;
        }

        /// <summary>
        /// Para o <see cref="TwoHandSteer"/>: a arma na mão é guiada pelas duas mãos? Devolve o visual, o eixo dele (espaço local) e o lado do
        /// cabo em que fica a mão esquerda (+1 = para a ponta mais longe da mão direita, −1 = para o outro lado).
        /// </summary>
        public bool TryGetSteer(out Transform visual, out Vector3 localA, out Vector3 localB, out float side, out bool slides)
        {
            localA = axisA; localB = axisB; side = 0f; slides = false;
            if (!Wanted(out visual) || !combat.Current.steerByHands) return false;
            localA = axisA; localB = axisB;
            side = Mathf.Sign(combat.Current.supportOffset);
            slides = combat.Current.supportSlides;
            return true;
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

        // ---------- Lâmina fora da cabeça (07/11, revisão das animações, passo 5) ----------
        // Os personagens do kit são cabeçudos (o capitão ainda tem o chapéu de três bicos): nos golpes de duas mãos (todos os 11 cortes do
        // espadão da Explosive passam por ela) a lâmina atravessava a cabeça e o chapéu. Se a lâmina desta pose (alvo de IK da mão direita + a
        // direção dela no punho) entra no elipsoide da cabeça, o braço inteiro gira no ombro (posição e giro do alvo de IK juntos: o pulso fica
        // como está) o mínimo para ela passar por fora, para o lado em que já estava saindo; entra e sai a clearSpeed. A mão de apoio usa esse
        // alvo novo, então as duas mãos continuam no cabo.

        HeadVolume headVolume;

        /// <summary>Volume da cabeça (com o chapéu) agora, no mundo: centro, eixos e raios.</summary>
        public bool TryGetHeadVolume(out Vector3 center, out Quaternion axes, out Vector3 radii)
        {
            center = default; axes = Quaternion.identity; radii = default;
            if (!hasHeadVolume || !headBone) return false;
            var root = character ? character.transform : transform;
            float scale = root.lossyScale.y;
            axes = headBone.rotation * headToBody;
            center = headBone.position + axes * (headVolume.center * scale);
            radii = headVolume.radii * scale;
            return true;
        }
        bool hasHeadVolume;
        Transform headBone, shoulder;
        Quaternion headToBody = Quaternion.identity;
        float clearAngle;
        Vector3 clearAxis = Vector3.up;
        int clearFrame = -10;

        bool HeadClearWanted()
        {
            if (swingCtl && swingCtl.Active) return false;
            // Só o capitão (07/11): no brutamontes e no Barba-Negra o braço direito girado no ombro levava o cabo para onde a mão esquerda não
            // alcança, e as mãos se separavam até 0,8 m na preparação do golpe ("a mão/braço dele fica todo contorcido"). No capitão o cabo é
            // curto perto da mão e a diferença é pequena (0,38 → 0,48 m no pior quadro).
            if (!hasHeadVolume || !combat || !combat.MeleeAssist || !rightHand || guardWeight > 0f || !bladeKnown) return false;
            var weapon = combat.Current;
            // Só as armas de duas mãos (espadões, tipo 4, e o machado do brutamontes, 5). Cutelo, sabres, adagas e os machados de uma mão do
            // capitão (3) ficam como estavam: neles a lâmina só raspava a cabeça em 1–2 quadros da machadada de cima, e o IK da mão punha o
            // punho ~90° fora da animação na entrada do desvio (tranco de 98° num quadro).
            if (!weapon || weapon.kind != WeaponKind.Melee || weapon.animatorWeaponType < 4) return false;
            if (character && (character.IsDown || character.IsDodging)) return false;
            // Chute, arremesso, alavanca etc. (ações de corpo inteiro que não são golpe): no chute o desvio crescia até o máximo (69°) e o
            // braço enfiava o espadão no corpo, piscando (07/11, Jhonata: "quando chuto com a espada grande ela começa a piscar").
            if (combat.IsKicking || (driver && driver.InFullBodyMove && !combat.IsAttacking)) return false;
            // Nas poses de segurar (corrida do espadão) a lâmina não passa pela cabeça nem pelo chão: o desvio ligava por 2–3 quadros a cada
            // passada e o IK da mão direita entrava e saía de uma vez (salto de ~50° na arma).
            if (InPosedState()) return false;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !visual.gameObject.activeInHierarchy) return false;
            var hand = combat.HandSlot(combat.CurrentIndex);
            return !hand || visual.IsChildOf(hand);
        }

        /// <summary>Menor distância do segmento ao centro do elipsoide, em raios (menos de 1 = entra na cabeça).</summary>
        static float EllipsoidDistance(Vector3 a, Vector3 b, Vector3 center, Quaternion axes, Vector3 radii)
        {
            Quaternion inv = Quaternion.Inverse(axes);
            Vector3 pa = inv * (a - center), pb = inv * (b - center);
            pa = new Vector3(pa.x / radii.x, pa.y / radii.y, pa.z / radii.z);
            pb = new Vector3(pb.x / radii.x, pb.y / radii.y, pb.z / radii.z);
            Vector3 d = pb - pa;
            float t = Mathf.Clamp01(-Vector3.Dot(pa, d) / Mathf.Max(1e-6f, d.sqrMagnitude));
            return (pa + d * t).magnitude;
        }

        void HeadClearIK()
        {
            // O IK roda uma vez por camada com "IK Pass" (tronco e corpo inteiro): a conta só na primeira do quadro.
            if (Time.frameCount == clearFrame) return;
            clearFrame = Time.frameCount;
            float target = 0f;
            HeadDistance = 9f;
            if (HeadClearWanted() && BladeTip(out var tip))
            {
                float bladeLength = (tip - rightHand.position).magnitude;
                var root = character ? character.transform : transform;
                float scale = root.lossyScale.y;
                Quaternion axes = headBone.rotation * headToBody;
                Vector3 center = headBone.position + axes * (headVolume.center * scale);
                Vector3 radii = headVolume.radii * scale;
                Vector3 goal = animator.GetIKPosition(AvatarIKGoal.RightHand);
                Vector3 dir = animator.GetIKRotation(AvatarIKGoal.RightHand) * bladeInGoal;
                Vector3 end = goal + dir * bladeLength;
                HeadDistance = EllipsoidDistance(goal, end, center, axes, radii);
                if (HeadDistance < clearMargin)
                {
                    Vector3 pivot = shoulder.position;
                    // Ponto da lâmina mais perto do centro e para onde ele tem de sair (para longe do centro).
                    Vector3 seg = end - goal;
                    float t = Mathf.Clamp01(Vector3.Dot(center - goal, seg) / Mathf.Max(1e-6f, seg.sqrMagnitude));
                    Vector3 closest = goal + seg * t;
                    Vector3 away = Vector3.ProjectOnPlane(closest - center, closest - pivot);
                    if (away.sqrMagnitude < 1e-6f) away = Vector3.ProjectOnPlane(root.right, closest - pivot);
                    Vector3 axis = Vector3.Cross(closest - pivot, away);
                    if (axis.sqrMagnitude > 1e-8f)
                    {
                        axis.Normalize();
                        // O eixo fica o do começo do desvio até ele voltar a zero (deixando seguir o lado de cada quadro, ele dava meia-volta
                        // em 3 quadros na entrada do golpe do espadão e a mão girava 130° num quadro).
                        if (clearAngle < 2f) clearAxis = axis;
                        // Menor giro que tira a lâmina (de 5 em 5°, depois em metades); sem saída até o máximo, o que mais a afasta
                        // (pular direto para o máximo dava trancos na finalização de cima).
                        float best = HeadDistance, bestAngle = 0f, found = -1f;
                        for (float ang = 5f; ang <= clearMaxDegrees + 0.01f; ang += 5f)
                        {
                            var r = Quaternion.AngleAxis(ang, clearAxis);
                            float dd = EllipsoidDistance(pivot + r * (goal - pivot), pivot + r * (end - pivot), center, axes, radii);
                            if (dd >= clearMargin) { found = ang; break; }
                            if (dd > best) { best = dd; bestAngle = ang; }
                        }
                        if (found > 0f)
                        {
                            float lo = found - 5f, hi = found;
                            for (int i = 0; i < 4; i++)
                            {
                                float mid = (lo + hi) * 0.5f;
                                var r = Quaternion.AngleAxis(mid, clearAxis);
                                if (EllipsoidDistance(pivot + r * (goal - pivot), pivot + r * (end - pivot), center, axes, radii) >= clearMargin) hi = mid;
                                else lo = mid;
                            }
                            target = hi;
                        }
                        else target = bestAngle;
                    }
                }
            }
            clearAngle = Mathf.MoveTowards(clearAngle, target, clearSpeed * Time.deltaTime);
        }

        // ---------- Lâmina acima do chão (07/11, Jhonata: "nos golpes do espadão parece que ele está batendo para baixo") ----------
        // Os golpes de duas mãos da Explosive terminam com as mãos baixas e a lâmina para a frente e para baixo; no capitão (pernas curtas, mãos
        // a ~0,4 m do chão) ela apontava 50–90° para o chão e a ponta entrava nele no fim de todo golpe. Trocar a pegada não resolve (o que a
        // ergue no golpe deixa a guarda torta). Nas armas de duas mãos o alvo de IK da mão direita gira no pulso só o que falta para a lâmina
        // não apontar mais que bladeFloorY para baixo nem a ponta passar do chão; a mão de apoio vai junto (ela segue a arma). Depois do desvio
        // da cabeça (que gira o braço no ombro), na mesma direção no chão que a lâmina já tinha.

        float floorAngle;
        Vector3 floorAxis = Vector3.right;
        int floorFrame = -10;

        void BladeFloorIK()
        {
            if (Time.frameCount != floorFrame)
            {
                floorFrame = Time.frameCount;
                float target = 0f;
                // Só no capitão: os golpes do Mixamo do Barba-Negra e do brutamontes não apontam para o chão, e erguer a lâmina deles levava ela
                // para dentro da cabeça (Barba-Negra, golpe 7: 1 → 9 quadros).
                if (HeadClearWanted() && BladeTip(out var tip))
                {
                    var root = character ? character.transform : transform;
                    float length = (tip - rightHand.position).magnitude;
                    Vector3 goal = animator.GetIKPosition(AvatarIKGoal.RightHand);
                    Vector3 dir = animator.GetIKRotation(AvatarIKGoal.RightHand) * bladeInGoal;
                    float minY = bladeFloorY;
                    if (length > 0.05f)
                        minY = Mathf.Max(minY, Mathf.Clamp((root.position.y + bladeTipClearance * root.lossyScale.y - goal.y) / length, -1f, 0.6f));
                    if (dir.y < minY)
                    {
                        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                        if (flat.sqrMagnitude < 1e-4f) flat = root.forward;
                        flat.Normalize();
                        Vector3 wanted = flat * Mathf.Sqrt(1f - minY * minY) + Vector3.up * minY;
                        Vector3 axis = Vector3.Cross(dir, wanted);
                        if (axis.sqrMagnitude > 1e-8f)
                        {
                            floorAxis = axis.normalized;
                            target = Mathf.Min(bladeFloorMaxDegrees, Vector3.Angle(dir, wanted));
                            // Erguer não pode levar a lâmina para dentro da cabeça (no Barba-Negra levava): fica no maior giro que passa por fora.
                            float scale = root.lossyScale.y;
                            Quaternion axes = headBone.rotation * headToBody;
                            Vector3 center = headBone.position + axes * (headVolume.center * scale);
                            Vector3 radii = headVolume.radii * scale;
                            Vector3 from = clearAngle > 0.01f ? shoulder.position + Quaternion.AngleAxis(clearAngle, clearAxis) * (goal - shoulder.position) : goal;
                            Vector3 Lifted(float deg) => from + Quaternion.AngleAxis(deg, floorAxis) * dir * length;
                            if (EllipsoidDistance(from, Lifted(target), center, axes, radii) < clearMargin)
                            {
                                float lo = 0f, hi = target;
                                for (int i = 0; i < 6; i++)
                                {
                                    float mid = (lo + hi) * 0.5f;
                                    if (EllipsoidDistance(from, Lifted(mid), center, axes, radii) < clearMargin) hi = mid;
                                    else lo = mid;
                                }
                                target = lo;
                            }
                        }
                    }
                }
                floorAngle = Mathf.MoveTowards(floorAngle, target, clearSpeed * Time.deltaTime);
            }
        }

        // O alvo da mão direita desta camada antes de qualquer desvio, e o que foi mandado (a mão de apoio segue o mandado: lendo o da
        // animação, ela ficava no cabo antigo e as mãos se separavam até 0,7 m na preparação do golpe do brutamontes).
        Vector3 animGoal, sentGoal;
        Quaternion animGoalRotation, sentGoalRotation;
        bool handOverridden;

        /// <summary>Desvio da cabeça (braço no ombro) e lâmina acima do chão (pulso), juntos no alvo de IK da mão direita.</summary>
        void ApplyHandOverrides()
        {
            if (guardWeight > 0f || !shoulder) return;
            bool clear = clearAngle > 0.01f, floor = floorAngle > 0.01f;
            Vector3 position = animGoal;
            Quaternion rotation = animGoalRotation;
            // Duas mãos: a direita não vai mais longe do ombro esquerdo do que a esquerda alcança (menos o vão até o ponto do cabo).
            float pull = 0f;
            if (weight > 0.001f && leftShoulder && leftArmLength > 0f && Wanted(out _) && !combat.Current.steerByHands)
            {
                float scale = (character ? character.transform : transform).lossyScale.y;
                float maxDistance = supportReach * leftArmLength - supportMinGap * scale;
                Vector3 fromShoulder = position - LeftShoulderNow();
                float distance = fromShoulder.magnitude;
                if (distance > maxDistance && maxDistance > 0.05f)
                {
                    pull = (distance - maxDistance) * weight;
                    position -= fromShoulder / distance * pull;
                }
            }
            LastPull = pull;
            if (!clear && !floor && pull <= 0f) return;
            if (clear)
            {
                Vector3 pivot = shoulder.position;
                var r = Quaternion.AngleAxis(clearAngle, clearAxis);
                position = pivot + r * (position - pivot);
                rotation = r * rotation;
            }
            if (floor) rotation = Quaternion.AngleAxis(floorAngle, floorAxis) * rotation;
            float w = Mathf.Max(Mathf.Max(Mathf.Clamp01(clearAngle / 4f), Mathf.Clamp01(floorAngle / 4f)), pull > 0f ? 1f : 0f);
            // O giro só com os desvios (puxar a mão para dentro mexe só na posição: o pulso fica o da animação).
            float wr = Mathf.Max(Mathf.Clamp01(clearAngle / 4f), Mathf.Clamp01(floorAngle / 4f));
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, wr);
            animator.SetIKPosition(AvatarIKGoal.RightHand, position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rotation);
            sentGoal = Vector3.Lerp(animGoal, position, w);
            sentGoalRotation = Quaternion.Slerp(animGoalRotation, rotation, wr);
            handOverridden = true;
        }

        void OnAnimatorIK(int layerIndex)
        {
            lastRightGoal = animator.GetIKRotation(AvatarIKGoal.RightHand);
            lastRightGoalFrame = Time.frameCount;
            animGoal = animator.GetIKPosition(AvatarIKGoal.RightHand);
            animGoalRotation = lastRightGoal;
            handOverridden = false;
            GuardIK();
            HeadClearIK();
            BladeFloorIK();
            ApplyHandOverrides();
            KickHold();
            if (weight > 0.001f && Wanted(out var steered) && combat.Current.steerByHands)
            {
                SteerWrist(steered);
                return;
            }
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
            Vector3 goal = handOverridden ? sentGoal : animator.GetIKPosition(AvatarIKGoal.RightHand);
            Quaternion goalRotation = handOverridden ? sentGoalRotation : animator.GetIKRotation(AvatarIKGoal.RightHand);
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
            float reachWeight = KeepInReach(ref support, dir, goal);
            if (weapon.supportSlides) animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight * reachWeight);
            pendingGoal = goal;
            pendingGoalRotation = goalRotation;
            pendingFrame = Time.frameCount;
            LastTarget = support;
            LastReach = reachWeight;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight * reachWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, support);
        }

        // ---------- Arma guiada pelas mãos (TwoHandSteer, 07/11) ----------
        // O braço esquerdo não é puxado (só a animação), mas o punho gira para fechar no cabo como o direito (gripMirror, o mesmo da mão
        // que escorrega); o TwoHandSteer aponta o cabo para a palma esquerda (Jhonata: "encaixa melhor a mão esquerda no cabo").

        void SteerWrist(Transform visual)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            Vector3 a = visual.TransformPoint(axisA), b = visual.TransformPoint(axisB);
            Vector3 ab = b - a;
            float length = ab.magnitude;
            if (length < 0.05f) { animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f); return; }
            Vector3 dir = ab / length;
            if (Vector3.Dot(rightHand.position - a, dir) > length * 0.5f) { a = b; dir = -dir; }
            Vector3 goal = animator.GetIKPosition(AvatarIKGoal.RightHand);
            Quaternion goalRotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
            bool follow = hasPreviousGoal && Time.frameCount - previousGoalFrame == 1 && (goal - previousGoal).sqrMagnitude < 0.25f;
            if (follow) dir = goalRotation * (Quaternion.Inverse(previousGoalRotation) * dir);
            pendingGoal = goal;
            pendingGoalRotation = goalRotation;
            pendingFrame = Time.frameCount;
            Quaternion leftRotation = animator.GetIKRotation(AvatarIKGoal.LeftHand);
            RightGrip = Quaternion.Inverse(goalRotation) * dir;
            Vector3 grip = Vector3.Scale(RightGrip, gripMirror);
            Quaternion held = Quaternion.FromToRotation(leftRotation * grip, dir) * leftRotation;
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, held);
        }

        // ---------- Arma parada no chute (07/11, Jhonata: "quando chuto com a espada grande ela começa a piscar e fica entrando no
        // personagem") ----------
        // O chute é de corpo inteiro e o braço dele levava o espadão para a frente do rosto. Com arma pesada na mão (machados e espadões),
        // durante o chute a mão direita fica onde estava na pose de antes (guardada em relação ao personagem): só as pernas chutam.

        Vector3 holdPos, holdElbow;
        Quaternion holdRot = Quaternion.identity;
        Transform rightElbow;
        bool holdKnown;
        float holdWeight;
        int holdFrame = -10;

        /// <summary>Teste: quanto a mão da arma está presa na pose de antes do chute (0–1).</summary>
        public float KickHoldWeight => holdWeight;

        void KickHold()
        {
            if (!combat || !rightHand) return;
            var weapon = combat.Current;
            var root = character ? character.transform : transform;
            bool heavy = weapon && weapon.kind == WeaponKind.Melee && weapon.animatorWeaponType >= 3 && !combat.IsSwapping;
            bool kicking = combat.IsKicking;
            Vector3 goal = animGoal;
            Quaternion rot = animGoalRotation;
            if (Time.frameCount != holdFrame)
            {
                holdFrame = Time.frameCount;
                if (heavy && !kicking && !combat.IsAttacking)
                {
                    // A pose que aparecia (com a lâmina já desviada da cabeça), não a da animação: presa a da animação, o espadão ficava
                    // em pé na frente do rosto durante o chute.
                    Vector3 shownPos = handOverridden ? sentGoal : goal;
                    Quaternion shownRot = handOverridden ? sentGoalRotation : rot;
                    holdPos = root.InverseTransformPoint(shownPos);
                    holdRot = Quaternion.Inverse(root.rotation) * shownRot;
                    if (!rightElbow) rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    // Os ossos ainda estão no quadro anterior: o cotovelo de antes serve de dica (sem ela o IK pulava entre duas soluções).
                    if (rightElbow) holdElbow = root.InverseTransformPoint(rightElbow.position);
                    holdKnown = true;
                }
                holdWeight = Mathf.MoveTowards(holdWeight, heavy && kicking && holdKnown ? 1f : 0f, Time.deltaTime / 0.12f);
            }
            // Tem prioridade sobre os desvios do golpe que ainda estão voltando a zero (chute logo depois de um golpe): sem isso os dois se
            // alternavam e a mão girava até 112° num quadro.
            if (holdWeight <= 0.001f) { animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f); return; }
            Vector3 pos = root.TransformPoint(holdPos);
            Quaternion r = root.rotation * holdRot;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, holdWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, holdWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, pos);
            animator.SetIKRotation(AvatarIKGoal.RightHand, r);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, holdWeight);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, root.TransformPoint(holdElbow));
            sentGoal = Vector3.Lerp(goal, pos, holdWeight);
            sentGoalRotation = Quaternion.Slerp(rot, r, holdWeight);
            handOverridden = true;
        }

        Vector3 previousGoal, pendingGoal;
        Quaternion previousGoalRotation, pendingGoalRotation;
        int previousGoalFrame = -10, pendingFrame = -10;
        bool hasPreviousGoal;

        // ---------- Alcance (07/11, revisão das animações, passo 5; Jhonata: "toda vez que o brutamontes vai fazer o golpe ele dá uma
        // esticadona que contorce todo o braço") ----------
        // O brutamontes é um tubarão do kit (braços curtos, cabeçona) com os golpes de duas mãos de humanos de braço comprido: o ponto do cabo
        // onde a mão esquerda devia ir ficava fora do alcance, e o IK do humanoide esticava o braço até 110% do comprimento com o cotovelo
        // travado em 180° (no golpe horizontal por ~0,6 s). Agora a mão direita não vai mais longe do ombro esquerdo do que ele alcança
        // (ApplyHandOverrides traz o alvo dela para dentro, o golpe fica um pouco mais curto), o ponto escorrega no cabo para o lado da mão
        // direita até caber, e só se nem assim cabe a mão solta aos poucos.

        Transform leftShoulder;
        float leftArmLength;
        Vector3 leftShoulderLocal;
        bool leftShoulderKnown;

        /// <summary>
        /// Ombro esquerdo agora: no IK os ossos ainda estão na pose do quadro anterior, e nos golpes que giram o corpo (o giro de 360° do
        /// Barba-Negra) a posição velha ficava para trás e a conta soltava a mão à toa; guardado em relação ao personagem, acompanha o giro.
        /// </summary>
        Vector3 LeftShoulderNow()
        {
            var root = character ? character.transform : transform;
            return leftShoulderKnown ? root.TransformPoint(leftShoulderLocal) : leftShoulder.position;
        }

        /// <summary>Teste: quanto a mão direita foi trazida para dentro no último quadro para a esquerda alcançar o cabo (metros).</summary>
        public float LastPull { get; private set; }

        /// <summary>Teste: quanto do IK da mão de apoio ficou valendo no último quadro pelo alcance (1 = tudo).</summary>
        public float LastReach { get; private set; } = 1f;

        float KeepInReach(ref Vector3 support, Vector3 axis, Vector3 rightGoal)
        {
            if (!leftShoulder || leftArmLength <= 0f) return 1f;
            float scale = (character ? character.transform : transform).lossyScale.y;
            float reach = supportReach * leftArmLength;
            Vector3 shoulderPos = LeftShoulderNow();
            Vector3 toShoulder = support - shoulderPos;
            if (toShoulder.magnitude <= reach) return 1f;
            // Escorrega no eixo para o lado da mão direita: u de 0 (onde está) até onde fica o vão mínimo dela.
            Vector3 toward = Vector3.Dot(rightGoal - support, axis) >= 0f ? axis : -axis;
            float maxU = Mathf.Max(0f, Vector3.Dot(rightGoal - support, toward) - supportMinGap * scale);
            // |toShoulder + toward·u| = reach  →  u² + 2bu + c = 0
            float b = Vector3.Dot(toShoulder, toward), c = toShoulder.sqrMagnitude - reach * reach;
            float disc = b * b - c;
            if (disc >= 0f)
            {
                float u = -b - Mathf.Sqrt(disc);
                if (u < 0f) u = -b + Mathf.Sqrt(disc);
                if (u >= 0f && u <= maxU) { support += toward * u; return 1f; }
            }
            // Nem colado na mão direita cabe: vai até lá e, se ainda passa, o braço vai até quase esticado na direção do ponto (a mão fica o
            // mais perto do cabo que dá). Antes a mão soltava conforme passava do alcance: no espadão do capitão o ponto fica no limite e a
            // mão (o gancho) pegava e soltava o cabo no ritmo da respiração da pose parada, a cada ~2 s (Jhonata, 07/11).
            support += toward * maxU;
            Vector3 fromShoulder = support - shoulderPos;
            float most = fullReach * leftArmLength;
            if (fromShoulder.magnitude > most) support = shoulderPos + fromShoulder.normalized * most;
            return 1f;
        }

        /// <summary>Teste: onde a mão de apoio foi mandada no último quadro.</summary>
        public Vector3 LastTarget { get; private set; }

        /// <summary>Teste: distância do pulso esquerdo ao ponto do cano no último quadro com a pose de rifle (metros).</summary>
        public float PosedGap { get; private set; } = -1f;

        /// <summary>
        /// Mão esquerda no cano nas poses de rifle (12/11, Jhonata: "essa mão dele ficou estranha"): a pose do Kevin Iglesias deixava a mão
        /// esquerda do capitão erguida na frente do peito, longe do cano, e o IK do humanoide (que alcança a mão puxando o corpo todo) fazia a
        /// arma balançar na corrida. Aqui, depois da animação, só o braço esquerdo gira (IK de duas juntas no plano do cotovelo animado) até o
        /// pulso chegar ao ponto do cano: a arma, presa à mão direita, não se mexe.
        /// </summary>
        static void TwoBone(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 down)
        {
            Vector3 sh = upper.position, el = lower.position, wr = hand.position;
            float la = (el - sh).magnitude, lb = (wr - el).magnitude;
            Vector3 toT = target - sh;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(la - lb) + 0.001f, (la + lb) * 0.999f);
            Vector3 n = toT.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(el - sh, n);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(down, n);
            pole.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            Vector3 newEl = sh + n * (la * cosA) + pole * (la * Mathf.Sqrt(1f - cosA * cosA));
            upper.rotation = Quaternion.FromToRotation(el - sh, newEl - sh) * upper.rotation;
            Vector3 el2 = lower.position, wr2 = hand.position;
            lower.rotation = Quaternion.FromToRotation(wr2 - el2, sh + n * d - el2) * lower.rotation;
        }

        /// <summary>Arma de fogo de duas mãos parada/correndo: arma na diagonal e a mão esquerda por baixo do cano.</summary>
        bool CarryIK(Transform visual)
        {
            var weapon = combat.Current;
            if (!weapon || weapon.kind != WeaponKind.Firearm || !weapon.twoHanded) return false;
            if (!fingers) fingers = GetComponent<FingerGrip>();
            if (!fingers || !fingers.TryGetLeftPalm(out var fingerLocal, out var palmLocal, out var palmLength)) return false;
            if (!armUpperR)
            {
                armUpperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                armLowerR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!armUpperR || !armLowerR || !chest) return false;
            var root = character ? character.transform : transform;
            float scale = root.lossyScale.y, w = posedWeight;
            // Eixo da arma agora (coronha = ponta perto da mão direita) e o alto dela.
            Vector3 a = visual.TransformPoint(axisA), b = visual.TransformPoint(axisB);
            if ((rightHand.position - a).sqrMagnitude > (rightHand.position - b).sqrMagnitude) (a, b) = (b, a);
            float length = (b - a).magnitude;
            if (length < 0.05f) return false;
            Vector3 curDir = (b - a) / length;
            Vector3 curUp = Vector3.ProjectOnPlane(visual.up, curDir);
            if (curUp.sqrMagnitude < 1e-6f) return false;
            Vector3 dir = root.TransformDirection(carryDirection.normalized);
            Vector3 up = Vector3.ProjectOnPlane(root.up, dir).normalized;
            up = Quaternion.AngleAxis(carryRoll, dir) * up;
            Quaternion turn = Quaternion.LookRotation(dir, up) * Quaternion.Inverse(Quaternion.LookRotation(curDir, curUp));
            Quaternion handGoal = Quaternion.Slerp(rightHand.rotation, turn * rightHand.rotation, w);
            Vector3 wristGoal = Vector3.Lerp(rightHand.position, chest.position + root.TransformVector(carryHandFromChest), w);
            TwoBone(armUpperR, armLowerR, rightHand, wristGoal, -root.up);
            rightHand.rotation = handGoal;
            // Mão esquerda por baixo do cano, palma para cima, dedos para fora do corpo.
            a = visual.TransformPoint(axisA); b = visual.TransformPoint(axisB);
            if ((rightHand.position - a).sqrMagnitude > (rightHand.position - b).sqrMagnitude) (a, b) = (b, a);
            Vector3 gunDir = (b - a).normalized, gunUp = Vector3.ProjectOnPlane(visual.up, gunDir).normalized;
            float s = Vector3.Dot(rightHand.position - a, gunDir);
            Vector3 support = a + gunDir * Mathf.Clamp(s + weapon.supportOffset * length, 0f, length);
            Vector3 fingersOut = Vector3.Cross(gunDir, gunUp);
            if (Vector3.Dot(fingersOut, root.forward) < 0f) fingersOut = -fingersOut;
            Quaternion leftGoal = Quaternion.LookRotation(fingersOut, gunUp) * Quaternion.Inverse(Quaternion.LookRotation(fingerLocal, palmLocal));
            Vector3 leftWrist = support - gunUp * (carryUnderGap * scale) - fingersOut * (palmLength * 0.6f);
            leftWrist = Vector3.Lerp(armHand.position, leftWrist, w);
            TwoBone(armUpper, armLower, armHand, leftWrist, -root.up);
            armHand.rotation = Quaternion.Slerp(armHand.rotation, leftGoal, w);
            PosedGap = (armHand.position - leftWrist).magnitude;
            return true;
        }

        void PosedArmIK()
        {
            PosedGap = -1f;
            if (posedWeight <= 0.001f || !Wanted(out var visual)) return;
            if (!armUpper)
            {
                armUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                armLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                armHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (!armUpper || !armLower || !armHand) return;
            }
            if (CarryIK(visual)) return;
            Vector3 a = visual.TransformPoint(axisA), b = visual.TransformPoint(axisB);
            float length = (b - a).magnitude;
            if (length < 0.05f) return;
            Vector3 dir = (b - a) / length;
            float s = Vector3.Dot(rightHand.position - a, dir);
            if (s > length * 0.5f) { (a, b) = (b, a); dir = -dir; s = length - s; }
            Vector3 support = a + dir * Mathf.Clamp(s + combat.Current.supportOffset * length, 0f, length);
            // Pegada espelhada da direita: o eixo da arma e o vão pulso → eixo no espaço do osso da mão direita.
            Vector3 rightAxis = Quaternion.Inverse(rightHand.rotation) * dir;
            Vector3 onAxis = a + dir * s;
            Vector3 rightPalm = Quaternion.Inverse(rightHand.rotation) * (onAxis - rightHand.position);
            Quaternion handRot = Quaternion.FromToRotation(armHand.rotation * (rightAxis * posedGripSign), dir) * armHand.rotation;
            Vector3 wrist = support - handRot * (rightPalm * posedGripSign) * posedPalm;
            wrist = Vector3.Lerp(armHand.position, wrist, posedWeight);
            // Duas juntas: o cotovelo fica no plano em que a animação o pôs.
            Vector3 sh = armUpper.position, el = armLower.position, wr = armHand.position;
            float la = (el - sh).magnitude, lb = (wr - el).magnitude;
            Vector3 toT = wrist - sh;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(la - lb) + 0.001f, (la + lb) * 0.999f);
            Vector3 n = toT.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(el - sh, n);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(-transform.up, n);
            pole.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            Vector3 newEl = sh + n * (la * cosA) + pole * (la * Mathf.Sqrt(1f - cosA * cosA));
            armUpper.rotation = Quaternion.FromToRotation(el - sh, newEl - sh) * armUpper.rotation;
            Vector3 el2 = armLower.position, wr2 = armHand.position;
            armLower.rotation = Quaternion.FromToRotation(wr2 - el2, sh + n * d - el2) * armLower.rotation;
            armHand.rotation = Quaternion.Slerp(armHand.rotation, handRot, posedWeight);
            PosedGap = (armHand.position - wrist).magnitude;
        }

        void LateUpdate()
        {
            PosedArmIK();
            // O alvo deste quadro vira o "anterior" do próximo (os ossos ficam nesta pose até lá).
            previousGoal = pendingGoal;
            previousGoalRotation = pendingGoalRotation;
            hasPreviousGoal = pendingFrame == Time.frameCount;
            previousGoalFrame = Time.frameCount;
            if (leftShoulder)
            {
                leftShoulderLocal = (character ? character.transform : transform).InverseTransformPoint(leftShoulder.position);
                leftShoulderKnown = true;
            }
            RememberBlade();
        }
    }
}
