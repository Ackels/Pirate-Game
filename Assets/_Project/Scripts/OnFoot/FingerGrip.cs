using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Dedos que fecham na arma (07/11, Jhonata: "todas as mãos estão ficando bugadas... você não consegue animar o dedo?").
    /// Os personagens do kit do Quaternius têm 3 ossos em cada dedo (Index/Middle/Pinky 1–3) e 2 no polegar, mas eles não entram no avatar
    /// humanoide: nenhuma animação mexe neles e as mãos ficavam sempre abertas, na pose de fábrica, em volta do cabo. Aqui cada dedo dobra
    /// para o lado da palma (o lado do polegar), junta por junta, o quanto a mão precisa: fechada quando segura a arma, meio relaxada
    /// quando está livre. As poses dos dedos são calculadas uma vez, na pose de repouso; por quadro só mistura as duas.
    /// Personagens sem esses ossos (KayKit: mão inteiriça) ficam como estão. Posto pelo <see cref="SupportHandIK"/> no Awake.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class FingerGrip : MonoBehaviour
    {
        [Tooltip("Dobra de cada junta dos dedos com a mão fechada (graus: base, meio, ponta).")]
        [SerializeField] Vector3 gripBend = new(65f, 80f, 55f);
        [Tooltip("Dobra do polegar com a mão fechada (graus: base, ponta).")]
        [SerializeField] Vector2 thumbBend = new(35f, 40f);
        [Tooltip("Quanto a mão fica fechada sem arma (0 = aberta como no modelo, 1 = fechada).")]
        [SerializeField] float relaxed = 0.25f;
        [SerializeField] float blendSeconds = 0.12f;

        [Tooltip("Arma de lâmina na mão: o cabo passa pelo centro do punho fechado (o lugar da mão é ajustado uma vez por arma).")]
        [SerializeField] bool fitHandle = true;
        readonly System.Collections.Generic.HashSet<Transform> fitted = new();

        /// <summary>Teste: quanto o lugar da arma na mão foi movido para o cabo passar no punho (metros, a última arma).</summary>
        public float LastFit { get; private set; }

        /// <summary>Centro do punho fechado da mão direita, no mundo.</summary>
        public bool TryGetRightFist(out Vector3 point)
        {
            point = RightFist;
            return right.valid;
        }
        public Vector3 RightFist => right.valid ? right.hand.TransformPoint(right.fistLocal) : Vector3.zero;

        /// <summary>Centro do punho fechado da mão esquerda, no mundo (para o cabo das armas de duas mãos passar por ele).</summary>
        /// <summary>Mão esquerda: direção dos dedos, normal da palma (espaço do osso da mão) e comprimento até os nós.</summary>
        public bool TryGetLeftPalm(out Vector3 fingerLocal, out Vector3 palmLocal, out float palmLength)
        {
            if (left.valid && left.palmLength > 0f) { fingerLocal = left.fingerLocal; palmLocal = left.palmLocal; palmLength = left.palmLength; return true; }
            // Sem dedos na esquerda (o gancho do capitão): a da direita espelhada (os ossos das mãos do kit são espelhados com sinal −1 nos três eixos).
            fingerLocal = -right.fingerLocal; palmLocal = -right.palmLocal; palmLength = right.palmLength;
            return right.valid && palmLength > 0f;
        }

        public bool TryGetLeftFist(out Vector3 point)
        {
            point = left.valid ? left.hand.TransformPoint(left.fistLocal) : Vector3.zero;
            return left.valid;
        }

        /// <summary>
        /// O lugar da arma na mão foi medido nas animações pelo osso da mão, mas o cabo ficava na frente do pulso, fora dos dedos: move o
        /// lugar (só de lado em relação ao cabo, o tanto que falta) para o eixo do cabo passar pelo centro do punho. Uma vez por lugar.
        /// </summary>
        void FitHandle()
        {
            var weapon = combat.Current;
            // Lâminas e, desde 12/11, armas de fogo de duas mãos (Jhonata: "os dedos do capitão ainda estão estranhos segurando o bacamarte":
            // o punho fechava no ar acima da coronha). A pistola fica de fora: o cabo dela é em ângulo com o cano (o eixo comprido).
            bool rifle = weapon && weapon.kind != WeaponKind.Melee && weapon.twoHanded;
            if (!weapon || (weapon.kind != WeaponKind.Melee && !rifle) || !right.valid) return;
            var slot = combat.HandSlot(combat.CurrentIndex);
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!slot || !visual || fitted.Contains(slot)) return;
            fitted.Add(slot);
            // Eixo comprido das malhas da arma (lâmina e cabo), no mundo, passando pela origem do lugar (onde fica a pegada).
            var box = new Bounds(); bool first = true;
            foreach (var f in visual.GetComponentsInChildren<MeshFilter>())
            {
                if (!f.sharedMesh) continue;
                var mb = f.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = slot.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; } else box.Encapsulate(p);
                }
            }
            if (first) return;
            var size = box.size;
            int a = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            Vector3 axisLocal = Vector3.zero; axisLocal[a] = 1f;
            Vector3 axis = slot.TransformDirection(axisLocal).normalized;
            Vector3 fist = right.hand.TransformPoint(right.fistLocal);
            Vector3 v = fist - slot.position;
            // De lado (o eixo do cabo passa pelo punho) e ao longo do cabo até alongMax (o ponto da pegada medido ficava ~19 cm além da mão
            // no cutelo dos piratas: a mão segurava além da ponta do cabo).
            // No rifle só de lado: andar ao longo do cano mudaria onde ele aponta e a mão de apoio.
            float limit = rifle ? 0f : alongMax;
            float along = Mathf.Clamp(Vector3.Dot(v, axis), -limit, limit);
            Vector3 shift = v - axis * (Vector3.Dot(v, axis) - along);
            slot.position += shift * fitAmount;
            LastFit = shift.magnitude * fitAmount;
        }

        [Tooltip("Quanto do caminho até o centro do punho o lugar da arma anda (1 = todo).")]
        [SerializeField] float fitAmount = 1f;
        [Tooltip("O lugar da arma anda no máximo isto (metros) ao longo do cabo.")]
        [SerializeField] float alongMax = 0.3f;

        /// <summary>Teste: troca as dobras em Play (recalcula as poses).</summary>
        public void SetBend(Vector3 grip, Vector2 thumb) { gripBend = grip; thumbBend = thumb; Build(); }

        /// <summary>Teste: quanto cada mão está fechada agora (0–1).</summary>
        public float RightAmount => right.amount;
        public float LeftAmount => left.amount;

        class Hand
        {
            public Transform hand;
            public Transform[] bones = new Transform[0];
            public Quaternion[] open, closed;
            public float amount = -1f;
            public bool valid;
            public Vector3 fistLocal;   // centro do punho fechado, no espaço do osso da mão
            public Vector3 fingerLocal, palmLocal;   // direção dos dedos e normal da palma (espaço da mão, pose aberta)
            public float palmLength;                 // do pulso aos nós dos dedos
        }

        readonly Hand right = new(), left = new();
        CharacterCombat combat;
        SupportHandIK support;
        Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();
            combat = GetComponentInParent<CharacterCombat>();
            support = GetComponent<SupportHandIK>();
            if (!animator || !animator.isHuman) return;
            right.hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            left.hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Build();
        }

        void Build()
        {
            Setup(right, ".R");
            Setup(left, ".L");
        }

        /// <summary>Acha os ossos dos dedos (filhos da mão com o nome do kit) e calcula a pose aberta (a de repouso) e a fechada.</summary>
        void Setup(Hand h, string suffix)
        {
            h.valid = false;
            if (!h.hand) return;
            Transform index1 = Find(h.hand, "Index1" + suffix), middle1 = Find(h.hand, "Middle1" + suffix),
                      pinky1 = Find(h.hand, "Pinky1" + suffix), thumb1 = Find(h.hand, "Thumb1" + suffix);
            if (!index1 || !middle1 || !pinky1 || !thumb1) return;
            var chains = new[] { Chain(index1, 3), Chain(middle1, 3), Chain(pinky1, 3) };
            var thumb = Chain(thumb1, 2);
            foreach (var c in chains) if (c.Length < 3) return;
            if (thumb.Length < 2) return;
            // A pose aberta é a de agora só na primeira vez (repouso do modelo: o Animator não mexe nesses ossos).
            int count = 3 * 3 + 2;
            var all = new Transform[count];
            for (int f = 0; f < 3; f++) for (int j = 0; j < 3; j++) all[f * 3 + j] = chains[f][j];
            all[9] = thumb[0]; all[10] = thumb[1];
            if (h.open == null || h.open.Length != count)
            {
                h.open = new Quaternion[count];
                for (int i = 0; i < count; i++) h.open[i] = all[i].localRotation;
            }
            // Mede na pose aberta.
            var now = new Quaternion[count];
            for (int i = 0; i < count; i++) { now[i] = all[i].localRotation; all[i].localRotation = h.open[i]; }
            // Palma: perpendicular ao plano dedos × nós dos dedos, do lado do polegar.
            Vector3 fingerDir = ((Index2(chains[0]) - chains[0][0].position) + (Index2(chains[2]) - chains[2][0].position)).normalized;
            Vector3 knuckles = (chains[2][0].position - chains[0][0].position).normalized;
            Vector3 palm = Vector3.Cross(fingerDir, knuckles).normalized;
            Vector3 thumbTip = thumb[1].childCount > 0 ? thumb[1].GetChild(0).position : thumb[1].position;
            if (Vector3.Dot(palm, thumbTip - chains[0][0].position) < 0f) palm = -palm;
            h.fingerLocal = Quaternion.Inverse(h.hand.rotation) * fingerDir;
            // Para a pegada por baixo (CarryIK): no kit os dedos nascem do mesmo ponto e os nós não dão direção; usa a segunda junta.
            Vector3 spread = Index2(chains[2]) - Index2(chains[0]);
            Vector3 palmOut = Vector3.Cross(fingerDir, spread.sqrMagnitude > 1e-10f ? spread : knuckles);
            if (palmOut.sqrMagnitude < 1e-12f) palmOut = palm;
            palmOut.Normalize();
            if (Vector3.Dot(palmOut, thumbTip - chains[0][0].position) < 0f) palmOut = -palmOut;
            palmOut = Vector3.ProjectOnPlane(palmOut, fingerDir).normalized;
            h.palmLocal = Quaternion.Inverse(h.hand.rotation) * palmOut;
            h.palmLength = (chains[1][0].position - h.hand.position).magnitude;
            h.closed = new Quaternion[count];
            for (int f = 0; f < 3; f++)
                for (int j = 0; j < 3; j++)
                {
                    var b = chains[f][j];
                    Vector3 dir = SegmentDir(b);
                    // Gira para o lado da palma em volta do eixo dedo × palma (nos ossos já dobrados acima na cadeia: o dedo enrola).
                    Vector3 axis = Vector3.Cross(dir, palm).normalized;
                    b.rotation = Quaternion.AngleAxis(gripBend[j], axis) * b.rotation;
                    h.closed[f * 3 + j] = b.localRotation;
                }
            for (int j = 0; j < 2; j++)
            {
                var b = thumb[j];
                Vector3 dir = SegmentDir(b);
                // O polegar fecha por cima dos dedos: na direção da palma e um pouco para o indicador.
                Vector3 toward = (palm + (chains[0][0].position - b.position).normalized * 0.5f).normalized;
                Vector3 axis = Vector3.Cross(dir, toward).normalized;
                b.rotation = Quaternion.AngleAxis(thumbBend[j], axis) * b.rotation;
                h.closed[9 + j] = b.localRotation;
            }
            // Centro do punho fechado: média das juntas dos três dedos dobrados (o anel por onde o cabo passa).
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < 9; i++) sum += all[i].position;
            h.fistLocal = h.hand.InverseTransformPoint(sum / 9f);
            for (int i = 0; i < count; i++) all[i].localRotation = now[i];
            h.bones = all;
            h.valid = true;
            h.amount = -1f;
        }

        static Vector3 Index2(Transform[] chain) => chain[1].position;

        static Vector3 SegmentDir(Transform b)
        {
            Vector3 tip = b.childCount > 0 ? b.GetChild(0).position : b.position + b.up * 0.01f;
            Vector3 d = tip - b.position;
            return d.sqrMagnitude > 1e-10f ? d.normalized : b.up;
        }

        static Transform[] Chain(Transform first, int length)
        {
            var list = new System.Collections.Generic.List<Transform>();
            var t = first;
            while (t && list.Count < length)
            {
                list.Add(t);
                Transform next = null;
                foreach (Transform c in t) if (!c.name.EndsWith("_end")) { next = c; break; }
                t = next;
            }
            return list.ToArray();
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform c in root)
            {
                if (c.name == name) return c;
                var r = Find(c, name);
                if (r) return r;
            }
            return null;
        }

        void LateUpdate()
        {
            if (!right.valid && !left.valid) return;
            bool rightHolds = false, leftHolds = false;
            if (combat && combat.Current)
            {
                var visual = combat.WeaponVisual(combat.CurrentIndex);
                var slot = combat.HandSlot(combat.CurrentIndex);
                rightHolds = visual && visual.gameObject.activeInHierarchy && slot && visual.IsChildOf(slot) && !combat.IsSwapping;
                // Duas mãos: a esquerda segura também (cano, cabo).
                leftHolds = rightHolds && combat.Current.supportOffset != 0f && support && support.enabled;
            }
            if (rightHolds && fitHandle) FitHandle();
            Blend(right, rightHolds ? 1f : relaxed);
            Blend(left, leftHolds ? 1f : relaxed);
        }

        void Blend(Hand h, float target)
        {
            if (!h.valid) return;
            float step = Time.deltaTime / Mathf.Max(0.01f, blendSeconds);
            float next = h.amount < 0f ? target : Mathf.MoveTowards(h.amount, target, step);
            if (Mathf.Abs(next - h.amount) < 0.0005f) return;
            h.amount = next;
            for (int i = 0; i < h.bones.Length; i++)
                h.bones[i].localRotation = Quaternion.Slerp(h.open[i], h.closed[i], next);
        }
    }
}
