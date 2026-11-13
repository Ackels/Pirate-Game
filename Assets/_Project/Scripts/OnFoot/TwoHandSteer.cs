using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Arma de duas mãos guiada pelas duas mãos da animação (07/11, revisão das animações, passo 5; Jhonata sobre o brutamontes: "a mão
    /// fica, o braço vai, aí ela dá uma esticada", "não é algo fluido, o braço e a mão mexendo junto").
    /// Os golpes de duas mãos são de humanos de braço comprido; no tubarão do kit (braço curto, peito largo) as mãos da animação não ficam
    /// num cabo reto como no dono do clipe. Prender a mão esquerda num ponto do cabo (IK do <see cref="SupportHandIK"/>) obrigava o braço a
    /// uma pose que a animação não tem: ele ficava duro, esticava e travava. Aqui é o contrário: nenhuma mão é puxada, os braços fazem
    /// exatamente o golpe, e a arma gira na mão direita (em volta do ponto do cabo que ela segura) até o cabo apontar para a mão
    /// esquerda. Só nas armas com <see cref="WeaponDefinition.steerByHands"/>. Posto pelo <see cref="SupportHandIK"/> no Awake.
    /// Roda depois do PostureLimiter (−20) e do assentar da arma do CharacterCombat (0): usa a pose final dos ossos do quadro.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class TwoHandSteer : MonoBehaviour
    {
        [SerializeField] float blendSeconds = 0.15f;
        [Tooltip("A arma gira na mão no máximo isto (graus) em relação à pegada normal.")]
        [SerializeField] float maxDegrees = 80f;
        [Tooltip("Com as mãos mais perto que isto (metros × escala) a direção entre elas não vale: a arma fica na pegada normal.")]
        [SerializeField] float minGap = 0.06f;
        [Tooltip("Espelho do vão pulso → cabo da mão direita (no osso dela) para a palma esquerda (no osso dela).")]
        [SerializeField] Vector3 palmMirror = new(-0.4f, -0.4f, 0.4f);
        [Tooltip("A palma esquerda fica isto (metros × escala) à frente do pulso, na linha do antebraço.")]
        [SerializeField] float palmReach = 0.07f;
        Transform leftElbow;

        /// <summary>Teste: troca a palma esquerda em Play.</summary>
        public void SetPalm(float reach, Vector3 mirror) { palmReach = reach; palmMirror = mirror; }

        /// <summary>Teste: quanto a arma girou na mão no último quadro (graus).</summary>
        public float LastAngle { get; private set; }
        /// <summary>Teste: distância da palma esquerda (ou do pulso, sem a palma) ao eixo do cabo no último quadro (metros).</summary>
        public float LastLeftGap { get; private set; }
        /// <summary>Teste: quanto a mão esquerda passa da ponta do cabo no último quadro (metros; negativo = no cabo).</summary>
        public float LastBeyondEnd { get; private set; }
        /// <summary>Teste: peso da guia agora (0–1).</summary>
        public float Weight => weight;

        SupportHandIK ik;
        Animator animator;
        Transform rightHand, leftHand, root;
        Transform visual, slot;
        Vector3 localA, localB;
        float side, weight, handSide, sideSince;
        bool slides;
        [Tooltip("Por quanto tempo (s) depois de pegar a arma o lado do cabo da mão esquerda ainda pode trocar.")]
        [SerializeField] float sideLockSeconds = 0.4f;

        /// <summary>Teste: lado do cabo da mão esquerda (+1/−1 em relação ao eixo medido).</summary>
        public float LastSide { get; private set; }

        void Start()
        {
            // Espelho do esqueleto entre as mãos, na pose de repouso (simétrica): cada eixo do osso da mão esquerda é o da direita
            // refletido no plano do meio do corpo, com estes sinais.
            if (!rightHand || !leftHand) return;
            Vector3 n = root.right;
            for (int i = 0; i < 3; i++)
            {
                Vector3 e = Vector3.zero; e[i] = 1f;
                Vector3 r = Reflect(rightHand.rotation * e, n);
                handMirror[i] = Vector3.Dot(r, leftHand.rotation * e) >= 0f ? 1f : -1f;
            }
            mirrorKnown = handMirror.x * handMirror.y * handMirror.z < 0f;
        }

        void Awake()
        {
            ik = GetComponent<SupportHandIK>();
            fingers = GetComponent<FingerGrip>();
            animator = GetComponent<Animator>();
            if (animator && animator.isHuman)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            var character = GetComponentInParent<FootCharacter>();
            root = character ? character.transform : transform;
        }

        void LateUpdate()
        {
            if (!ik || !rightHand || !leftHand) return;
            if (weight > 0.001f) SpreadKnees(weight);
            bool want = ik.TryGetSteer(out var v, out var a, out var b, out float s, out bool sl);
            if (want && v != visual)
            {
                if (visual && visual.parent == slot) visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual = v;
                slot = v.parent;
                weight = 0f;
                handSide = 0f;
            }
            if (want) { localA = a; localB = b; side = s; slides = sl; }
            if (!visual) return;
            // Saiu da mão (guardada, trocada): o CharacterCombat cuida do lugar dela.
            if (visual.parent != slot) { visual = null; weight = 0f; LastAngle = 0f; return; }
            weight = Mathf.MoveTowards(weight, want ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, blendSeconds));
            // A pegada normal na mão é a pose zero no lugar da mão; a guia parte dela todo quadro (nada se acumula).
            visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            LastAngle = 0f;
            LastSlide = 0f;
            if (weight <= 0.001f)
            {
                if (!want) visual = null;
                return;
            }
            // Mão direita girada no antebraço (antes de tudo: o machado, filho dela, vai junto). A esquerda continua espelhada da pose
            // da animação (rightAnimRotation) e o cabo passa por ela do mesmo lado de antes (offsetSign).
            rightAnimRotation = rightHand.rotation;
            float offsetSign = 1f;
            if (slides && rightTwist != 0f && rightElbow)
            {
                Vector3 forearm = (rightHand.position - rightElbow.position).normalized;
                rightHand.rotation = Quaternion.AngleAxis(rightTwist * weight, forearm) * rightHand.rotation;
                if (flipLeftOffset) offsetSign = Mathf.Cos(rightTwist * weight * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            }
            Vector3 pa = visual.TransformPoint(localA), pb = visual.TransformPoint(localB);
            Vector3 axis = pb - pa;
            float length = axis.magnitude;
            if (length < 0.05f) return;
            Vector3 dir = axis / length;
            // Eixo fixo de A para B (o cabo escorrega nas mãos: a mão direita pode passar do meio dele).
            // Ponto de giro: o centro do punho direito fechado (FingerGrip), senão o pulso.
            bool fists = useFist && fingers && fingers.TryGetRightFist(out var rightFist);
            Vector3 pivot = fists ? fingers.RightFist : rightHand.position;
            float at = Vector3.Dot(pivot - pa, dir);
            // Ponto do cabo na mão direita e o lado do cabo onde vai a esquerda.
            Vector3 grip = pa + dir * at;
            // O cabo passa pela palma esquerda, não pelo pulso: o vão pulso → cabo da mão direita (no osso dela), espelhado e menor no
            // osso da esquerda (o SupportHandIK fecha o punho esquerdo no cabo). Medido em 07/11 no brutamontes com a mão presa por IK
            // (a pegada aprovada em 27/10): direita (−0,05; −0,09; 0), esquerda (0,01; 0,04; 0,01) → espelho ≈ (−0,4; −0,4; 0,4).
            Vector3 rightPalm = Quaternion.Inverse(rightHand.rotation) * (grip - rightHand.position);
            Vector3 leftPoint = leftHand.position + leftHand.rotation * Vector3.Scale(rightPalm, palmMirror);
            // Os dedos do kit continuam a linha do antebraço: a palma fica palmReach à frente do pulso nessa linha (07/11: só com o vão
            // espelhado ela ficava no pulso e a mão segurava a bola da ponta do cabo com os dedos para fora dele).
            if (slides && leftElbow && palmReach > 0f) leftPoint += (leftHand.position - leftElbow.position).normalized * (palmReach * root.lossyScale.y);
            // Mão esquerda espelho da direita (abaixo): o cabo passa por ela com o mesmo vão que passa pela direita.
            if (slides && mirrorLeftHand && mirrorKnown) leftPoint = leftHand.position + offsetSign * (mirrorPlane == 1 ? -1f : 1f) * (grip - rightHand.position);
            Vector3 toLeft = leftPoint - grip;
            float gap = toLeft.magnitude, scale = root.lossyScale.y;
            // Lado do cabo da mão esquerda: começa no da arma (supportOffset) e troca só quando a mão passa bem para o outro lado (com a
            // pegada fixa, no Barba-Negra a mão da animação ficava do lado oposto e a arma virava 80° o tempo todo).
            // Só nos primeiros instantes com a arma na mão (sideLockSeconds): depois o lado fica (no meio do golpe a mão esquerda às vezes
            // passa para o lado da cabeça do machado, e trocar o lado virava a arma de ponta-cabeça).
            // side (supportOffset) conta a partir da mão direita para a ponta mais longe dela.
            if (handSide == 0f) { handSide = (side == 0f ? 1f : side) * (at > length * 0.5f ? -1f : 1f); sideSince = Time.time; }
            if (Time.time - sideSince < sideLockSeconds && gap > 0.001f && Vector3.Dot(toLeft / gap, dir * handSide) < -0.3f) handSide = -handSide;
            LastSide = handSide;
            Vector3 toward = dir * handSide;
            float w = weight * Mathf.Clamp01((gap - minGap * scale) / (0.05f * scale));
            if (w <= 0.001f) return;
            Quaternion turn = Quaternion.FromToRotation(toward, toLeft / gap);
            float angle = Quaternion.Angle(Quaternion.identity, turn);
            if (angle > maxDegrees) turn = Quaternion.Slerp(Quaternion.identity, turn, maxDegrees / angle);
            turn = Quaternion.Slerp(Quaternion.identity, turn, w);
            Vector3 newDir = turn * toward;
            if (slides && turnRightHand)
            {
                // A mão direita gira junto com o machado (em volta do pulso; o machado vai junto, é filho dela): a pegada dela fica a
                // original. Girando só o machado dentro da mão, ela ficava de ponta-cabeça no cabo (Jhonata, 07/11: "tá invertida, virada
                // para ele"). Depois um acerto pequeno do machado em volta do ponto da mão para o cabo passar de novo pela mão esquerda.
                Vector3 wrist = rightHand.position;
                rightHand.rotation = turn * rightHand.rotation;
                grip = wrist + turn * (grip - wrist);
                Vector3 toPalm = leftPoint - grip;
                if (toPalm.sqrMagnitude > 1e-6f)
                {
                    Quaternion fix = Quaternion.FromToRotation(newDir, toPalm.normalized);
                    float fa = Quaternion.Angle(Quaternion.identity, fix);
                    if (fa > 25f) fix = Quaternion.Slerp(Quaternion.identity, fix, 25f / fa);
                    visual.SetPositionAndRotation(grip + fix * (visual.position - grip), fix * visual.rotation);
                    newDir = fix * newDir;
                }
            }
            else visual.SetPositionAndRotation(grip + turn * (visual.position - grip), turn * visual.rotation);
            LastAngle = Quaternion.Angle(Quaternion.identity, turn);
            Vector3 fromGrip = leftPoint - grip;
            float end = handSide > 0f ? length - at : at;
            if (slides && mirrorLeftHand && mirrorKnown) MirrorLeftHand(newDir, grip, w);
            // Com os dedos fechando (FingerGrip), o cabo passa pelo centro do punho esquerdo já virado (acerto pequeno em volta da mão direita).
            if (slides && useFist && fingers && fingers.TryGetLeftFist(out var fist))
            {
                Vector3 toFist = fist - grip;
                if (toFist.sqrMagnitude > 1e-6f)
                {
                    Quaternion fix = Quaternion.FromToRotation(newDir, toFist.normalized);
                    float fa = Quaternion.Angle(Quaternion.identity, fix);
                    if (fa > 30f) fix = Quaternion.Slerp(Quaternion.identity, fix, 30f / fa);
                    fix = Quaternion.Slerp(Quaternion.identity, fix, w);
                    visual.SetPositionAndRotation(grip + fix * (visual.position - grip), fix * visual.rotation);
                    newDir = fix * newDir;
                }
                fromGrip = fist - grip;
            }
            if (slides) SlideHandle(newDir, Vector3.Dot(fromGrip, newDir), end, length, scale, w);
            // (07/11, Jhonata: a mão direita "tá de ponta-cabeça, virada para ele"): na pegada medida nos golpes a palma dela fica virada para
            // o corpo e o cabo passa pelas costas da mão. Vira 180° no eixo do antebraço (a palma passa a encarar o cabo) e o machado volta para
            // onde estava (a mão esquerda não muda). Testados nas fotos de perto: girar no eixo pulso → cabo não muda o lado da palma, e girar
            // o machado junto com a mão tirava o cabo da mão esquerda.
            if (slides && flipRightHand && rightElbow && !fists)
            {
                Vector3 forearm = rightHand.position - rightElbow.position;
                if (forearm.sqrMagnitude > 1e-6f)
                {
                    Vector3 vp = visual.position;
                    Quaternion vr = visual.rotation;
                    rightHand.rotation = Quaternion.AngleAxis(flipDegrees * w, forearm.normalized) * rightHand.rotation;
                    visual.SetPositionAndRotation(vp, vr);
                }
            }
            LastLeftGap = (fromGrip - newDir * Vector3.Dot(fromGrip, newDir)).magnitude;
            // Quanto a mão esquerda passa da ponta do cabo (negativo = está no cabo).
            LastBeyondEnd = Vector3.Dot(fromGrip, newDir) - end - LastSlide;
        }

        // ---------- Cabo que escorrega nas mãos (07/11, Jhonata: "essa mão dele segurando o machado acho muito estranha") ----------
        // A distância entre as mãos muda o golpe todo (no brutamontes de 0,25 a 0,65 m), e com o machado fixo na mão direita a esquerda ora
        // segurava a bola da ponta, ora o ar além dela. Agora o cabo escorrega: a mão esquerda fica sempre endMargin acima da ponta, e a
        // direita sobe ou desce no cabo (sem passar de handleFraction do comprimento, onde começam as lâminas). Só nas armas com supportSlides.

        [Header("Cabo que escorrega")]
        [Tooltip("Parte do comprimento da arma, a partir da ponta do cabo, que dá para segurar (no machado duplo do kit, até as lâminas).")]
        [SerializeField] float handleFraction = 0.46f;
        [Tooltip("A palma esquerda fica a isto (metros × escala) da ponta do cabo.")]
        [SerializeField] float endMargin = 0.09f;

        /// <summary>Teste: quanto o cabo escorregou no último quadro (metros; positivo = a ponta se afastou da mão direita).</summary>
        public float LastSlide { get; private set; }

        void SlideHandle(Vector3 handleDir, float leftAlong, float end, float length, float scale, float w)
        {
            float wanted = Mathf.Min(leftAlong + endMargin * scale, handleFraction * length);
            float slide = (wanted - end) * w;
            visual.position += handleDir * slide;
            LastSlide = slide;
        }

        // ---------- Joelhos afastados (07/11, Jhonata: "a calça dele tá entrando dentro da perna") ----------
        // Os golpes de duas mãos são de humanos de pernas finas: na preparação os joelhos se fecham (no brutamontes, de 0,30 para 0,14 m de
        // lado, sem a escala) e as pernas largas da calça do tubarão se atravessam. Enquanto a arma é guiada pelas mãos, cada coxa gira para
        // fora o que falta para os joelhos ficarem a kneeGap × a largura do quadril, e a canela gira de volta para o pé não sair do lugar.

        [Header("Joelhos afastados")]
        [Tooltip("Distância mínima de lado entre os joelhos, em larguras do quadril (0 = desliga).")]
        [SerializeField] float kneeGap = 0.8f;
        [Tooltip("Cada coxa gira para fora no máximo isto (graus).")]
        [SerializeField] float kneeMaxDegrees = 15f;
        Transform leftThigh, rightThigh, leftKnee, rightKnee, leftFoot, rightFoot;

        /// <summary>Teste: quanto os joelhos foram afastados no último quadro (metros, os dois somados).</summary>
        public float LastKneeSpread { get; private set; }

        void SpreadKnees(float w)
        {
            LastKneeSpread = 0f;
            if (kneeGap <= 0f || !leftThigh || !rightThigh || !leftKnee || !rightKnee || !leftFoot || !rightFoot) return;
            Vector3 side = root.right;
            float hip = Mathf.Abs(Vector3.Dot(leftThigh.position - rightThigh.position, side));
            float gap = Vector3.Dot(rightKnee.position - leftKnee.position, side);
            float need = (kneeGap * hip - gap) * w;
            if (need <= 0.001f) return;
            Spread(leftThigh, leftKnee, leftFoot, -side * (need * 0.5f));
            Spread(rightThigh, rightKnee, rightFoot, side * (need * 0.5f));
            LastKneeSpread = need;
        }

        void Spread(Transform thigh, Transform knee, Transform foot, Vector3 push)
        {
            Vector3 hip = thigh.position, footPos = foot.position;
            Quaternion footRot = foot.rotation;
            Vector3 from = knee.position - hip;
            Quaternion turn = Quaternion.FromToRotation(from, from + push);
            float a = Quaternion.Angle(Quaternion.identity, turn);
            if (a > kneeMaxDegrees) turn = Quaternion.Slerp(Quaternion.identity, turn, kneeMaxDegrees / a);
            thigh.rotation = turn * thigh.rotation;
            // O pé volta para onde estava (a canela gira para dentro) e com o mesmo giro.
            Vector3 k = knee.position;
            knee.rotation = Quaternion.FromToRotation(foot.position - k, footPos - k) * knee.rotation;
            foot.rotation = footRot;
        }

        // ---------- Mão esquerda espelhada (07/11, Jhonata: "a mão dele tá como se estivesse pegando o machado pelo pulso... tem que girar a
        // mão para os dedos encaixarem no cabo") ----------
        // A mão direita segura direito. Numa pegada de duas mãos a esquerda é a imagem dela no espelho: mesmo lado do cabo para o pulso,
        // palma do lado oposto, dedos fechando em volta. O espelho é o plano que contém o cabo e o vão pulso direito → cabo; o punho
        // esquerdo vira essa imagem (com os sinais do esqueleto medidos em repouso) e o cabo passa por ele com o mesmo vão da direita.

        [Header("Mão esquerda espelhada")]
        [SerializeField] bool mirrorLeftHand = true;
        Vector3 handMirror = Vector3.one;
        bool mirrorKnown;

        /// <summary>Teste: sinais do espelho entre os ossos das mãos (medidos em repouso).</summary>
        public Vector3 HandMirror => handMirror;
        /// <summary>Teste: liga/desliga a mão espelhada em Play.</summary>
        public void SetMirrorHand(bool on) => mirrorLeftHand = on;
        /// <summary>Teste: giro extra da mão esquerda no eixo do antebraço (graus).</summary>
        public void SetPalmTwist(float degrees) => palmTwist = degrees;
        /// <summary>Teste: plano do espelho (0 = contém o cabo e o vão do pulso, 1 = perpendicular ao vão, 2 = perpendicular ao cabo).</summary>
        public void SetMirrorPlane(int plane) => mirrorPlane = plane;
        [Tooltip("Plano do espelho entre as mãos: 0 = contém o cabo e o vão pulso → cabo; 1 = perpendicular a esse vão; 2 = perpendicular ao cabo.")]
        // 2 (07/11): testados os 3 nas fotos de perto; com 0 a mão segurava com as costas (Jhonata: "a mão tá ao contrário"), com 1 o punho
        // ficava embaixo da ponta do cabo, com 2 os dedos fecham por cima da madeira parado e nos golpes.
        [SerializeField] int mirrorPlane = 2;
        [Tooltip("Nas armas que escorregam, a mão direita gira junto com a arma (a pegada dela fica a original).")]
        [SerializeField] bool turnRightHand = false;
        [Tooltip("Nas armas que escorregam, a mão direita vira 180° em volta do vão pulso → cabo (o machado fica onde está): o polegar passa a " +
                 "apontar para as lâminas, como o da esquerda.")]
        [SerializeField] bool flipRightHand = true;
        [Tooltip("Com os dedos que fecham (FingerGrip), o cabo passa pelo centro do punho esquerdo.")]
        [SerializeField] bool useFist = true;
        FingerGrip fingers;
        /// <summary>Teste.</summary>
        public void SetUseFist(bool on) => useFist = on;
        [Tooltip("Quanto a mão direita vira no antebraço (graus).")]
        [SerializeField] float flipDegrees = 180f;
        /// <summary>Teste: quanto a mão direita vira no antebraço.</summary>
        public void SetFlipDegrees(float degrees) => flipDegrees = degrees;
        [Tooltip("Nas armas que escorregam, a mão direita gira isto (graus) no eixo do antebraço antes de tudo (o machado vai junto).")]
        [SerializeField] float rightTwist = 0f;
        Quaternion rightAnimRotation = Quaternion.identity;
        Transform rightElbow;
        /// <summary>Teste: giro da mão direita no antebraço.</summary>
        public void SetRightTwist(float degrees) => rightTwist = degrees;
        [Tooltip("Com a mão direita girada, o cabo passa pela esquerda do outro lado também.")]
        [SerializeField] bool flipLeftOffset = false;
        /// <summary>Teste.</summary>
        public void SetFlipLeftOffset(bool on) => flipLeftOffset = on;
        /// <summary>Teste: liga/desliga a mão direita virada.</summary>
        public void SetFlipRightHand(bool on) => flipRightHand = on;
        /// <summary>Teste: liga/desliga o giro da mão direita junto com a arma.</summary>
        public void SetTurnRightHand(bool on) => turnRightHand = on;
        [Tooltip("Giro extra da mão esquerda no eixo do antebraço depois do espelho (graus).")]
        [SerializeField] float palmTwist = 0f;

        static Vector3 Reflect(Vector3 v, Vector3 n) => v - 2f * Vector3.Dot(v, n) * n;

        void MirrorLeftHand(Vector3 handleDir, Vector3 grip, float w)
        {
            Vector3 onAxis = grip + handleDir * Vector3.Dot(rightHand.position - grip, handleDir);
            Vector3 offset = rightHand.position - onAxis;
            Vector3 n = mirrorPlane == 1 ? offset : mirrorPlane == 2 ? handleDir : Vector3.Cross(handleDir, offset);
            if (n.sqrMagnitude < 1e-8f) return;
            n.Normalize();
            Quaternion r = rightAnimRotation;
            Vector3 forward = Reflect(r * Vector3.forward, n) * handMirror.z;
            Vector3 up = Reflect(r * Vector3.up, n) * handMirror.y;
            Quaternion target = Quaternion.LookRotation(forward, up);
            // Giro extra no eixo do antebraço (teste: 180° vira a palma para o cabo).
            if (palmTwist != 0f && leftElbow) target = Quaternion.AngleAxis(palmTwist, (leftHand.position - leftElbow.position).normalized) * target;
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, target, w);
        }
    }
}
