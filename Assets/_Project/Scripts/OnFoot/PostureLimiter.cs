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

        [Header("Estabilizar a corrida")]
        [Tooltip("Tempo (s) do filtro da coluna e da cabeça andando/correndo: tira o tremor rápido da passada e deixa a inclinação lenta.")]
        [SerializeField] float stabilizeSeconds = 0.1f;
        [Tooltip("Quanto de cada osso fica filtrado (coluna, peito, peito de cima, pescoço, cabeça).")]
        [SerializeField] float[] stabilizeAmount = { 0.35f, 0.5f, 0.6f, 0.8f, 1f };
        [Tooltip("Velocidade (m/s, no tamanho de um personagem comum) em que começa e em que fica inteira.")]
        [SerializeField] Vector2 stabilizeSpeed = new(1.5f, 4f);
        [Tooltip("Estados do tronco com arma de duas mãos firme no peito: andando/correndo, a coluna e o peito ficam inteiros no filtro (o quadril não leva a arma junto a cada passada).")]
        [SerializeField] string[] steadyStates = { "BacamarteParado", "BacamarteCorrendo", "Espadao_Correndo" };
        [Tooltip("Tempo (s) do filtro da coluna nesses estados.")]
        [SerializeField] float steadySeconds = 0.35f;

        [Header("Lâmina no golpe")]
        [Tooltip("Estados do tronco em que a lâmina não pode apontar para o chão (girando só a mão). Vazio desde 07/11: o pulso reto (abaixo) faz isso sem quebrar o pulso.")]
        [SerializeField] string[] bladeLiftStates = { };
        [Tooltip("Quanto a lâmina pode apontar para baixo nesses estados (componente vertical da direção da mão até a ponta).")]
        [SerializeField] float bladeMinY = -0.2f;

        [Header("Pulso reto no golpe")]
        [Tooltip("Estados do tronco em que o pulso fica reto com o antebraço e o antebraço gira no próprio eixo para a lâmina ir para fora e para a frente (o corte do Kevin Iglesias dobra o pulso ~80° no nosso esqueleto).")]
        [SerializeField] string[] straightWristStates = { "Golpe4", "Adaga_Golpe0", "Adaga_Golpe1", "Adaga_Golpe2" };
        [Tooltip("Para onde a lâmina aponta nesses estados: para fora do peito (1), para a frente do personagem e para cima, somados.")]
        [SerializeField] Vector2 straightWristAim = new(0.6f, 0.2f);
        [Tooltip("Depois de girar o antebraço, o pulso pode dobrar até isto (graus) para chegar mais perto do alvo.")]
        [SerializeField] float straightWristBend = 35f;
        [Tooltip("Estados do tronco da pose de espada pronta (parado e correndo) com o pulso reto e a lâmina para a frente (18/11, Jhonata: os inimigos seguravam a espada com a lâmina para o lado e a mão virada para trás).")]
        [SerializeField] string[] readyWristStates = { "EspadaPronta", "CorrerComEspada" };
        [Tooltip("Para onde a lâmina aponta na pose pronta: para fora do peito (x), para a frente (y) e para cima (z), somados.")]
        [SerializeField] Vector3 readyWristAim = new(0.3f, 1f, 0.15f);

        [Header("Filtro de virada")]
        [Tooltip("Mão, antebraço, pé ou canela girando mais que isto (graus) de um quadro para o outro é tranco da mistura, não movimento.")]
        [SerializeField] float flipDegrees = 80f;
        [Tooltip("Nesse caso o osso vai para a pose nova no máximo isto por quadro (graus).")]
        [SerializeField] float flipCatchUp = 45f;

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
        FootCharacter character;
        CharacterAnimatorDriver driver;
        readonly Transform[] chain = new Transform[5];
        readonly Quaternion[] animated = new Quaternion[5], filtered = new Quaternion[5];
        float stabilize, steady;
        readonly HashSet<int> steadyHashes = new();
        bool filterReady;
        readonly Transform[] limbs = new Transform[12];
        readonly Quaternion[] lastLimb = new Quaternion[12];
        readonly int[] heldFrames = new int[12];
        bool limbsReady;
        CharacterCombat combat;
        Transform rightHand;
        readonly HashSet<int> bladeLiftHashes = new();
        float bladeLift;
        Transform measuredBlade;
        Transform rightFore;
        Quaternion handInFore = Quaternion.identity;
        readonly HashSet<int> straightWristHashes = new();
        readonly HashSet<int> readyWristHashes = new();
        float straightWrist;
        bool readyAim;
        Vector3 bladeEndA, bladeEndB;

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
            chain[0] = spine; chain[1] = chest; chain[2] = upperChest; chain[3] = neck; chain[4] = head;
            character = GetComponentInParent<FootCharacter>();
            driver = GetComponent<CharacterAnimatorDriver>();
            combat = GetComponentInParent<CharacterCombat>();
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (layer >= 0)
                foreach (var name in bladeLiftStates)
                {
                    int hash = Animator.StringToHash(name);
                    if (animator.HasState(layer, hash)) bladeLiftHashes.Add(hash);
                }
            if (layer >= 0)
                foreach (var name in steadyStates)
                {
                    int hash = Animator.StringToHash(name);
                    if (animator.HasState(layer, hash)) steadyHashes.Add(hash);
                }
            rightFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            // Pulso reto = a mão como estava em relação ao antebraço na pose de ligação (Awake).
            if (rightFore && rightHand) handInFore = Quaternion.Inverse(rightFore.rotation) * rightHand.rotation;
            if (layer >= 0)
                foreach (var name in straightWristStates)
                {
                    int hash = Animator.StringToHash(name);
                    if (animator.HasState(layer, hash)) straightWristHashes.Add(hash);
                }
            if (layer >= 0 && readyWristStates != null)
                foreach (var name in readyWristStates)
                {
                    int hash = Animator.StringToHash(name);
                    if (animator.HasState(layer, hash)) readyWristHashes.Add(hash);
                }
            limbs[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            limbs[1] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            limbs[2] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            limbs[3] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            limbs[4] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            limbs[5] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            limbs[6] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            limbs[7] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            limbs[8] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            limbs[9] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            limbs[10] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            limbs[11] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (twistRoot && neck && head)
            {
                neckRest = Quaternion.Inverse(twistRoot.rotation) * neck.rotation;
                headRest = Quaternion.Inverse(twistRoot.rotation) * head.rotation;
                twistAxis = (Quaternion.Inverse(twistRoot.rotation) * (head.position - neck.position)).normalized;
            }
        }

        /// <summary>Religado (acampamento acordando, capitão desembarcando): os filtros recomeçam da pose do clipe.</summary>
        void OnEnable()
        {
            limbsReady = false;
            filterReady = false;
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
            Stabilize();
            if (weight <= 0f)
            {
                LimitTwist();
                StraightWrist();
                LiftBlade();
                GuardFlips();
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
            StraightWrist();
            LiftBlade();
            GuardFlips();
        }

        /// <summary>
        /// Filtro de virada (06/11, revisão das animações, passo 3): na mistura de dois golpes com o pé (ou a mão) em poses muito diferentes o
        /// humanoide às vezes dá a volta pelo lado errado e o osso gira ~140° num quadro e volta no seguinte (medido no corte da cintura →
        /// revés do cutelo: pé esquerdo 138°, Guarda Negra 151°). Nenhum movimento de verdade chega perto (o chute mais rápido: ~74° por quadro
        /// no mundo; o chute ~64° e o rolamento ~79° no osso): acima de <see cref="flipDegrees"/> (80°; com 110 sobrava o pé girando 86° na
        /// emenda) o osso fica onde estava (o pico de um quadro some: no quadro seguinte o clipe já voltou);
        /// se a pose nova durar mais de 2 quadros, ele vai até ela no máximo <see cref="flipCatchUp"/> por quadro. Braços, antebraços, mãos,
        /// coxas, canelas e pés (o grito do brutamontes virava o braço 144°). Depois de pausa ou quadro muito longo, recomeça do clipe.
        /// </summary>
        void GuardFlips()
        {
            float dt = Time.deltaTime;
            bool reset = !limbsReady || dt <= 0f || dt > 0.1f;
            for (int i = 0; i < limbs.Length; i++)
            {
                var bone = limbs[i];
                if (!bone) continue;
                Quaternion now = bone.localRotation;
                if (!reset && Quaternion.Angle(lastLimb[i], now) > flipDegrees)
                {
                    now = heldFrames[i] < 2 ? lastLimb[i] : Quaternion.RotateTowards(lastLimb[i], now, flipCatchUp);
                    heldFrames[i]++;
                    bone.localRotation = now;
                }
                else heldFrames[i] = 0;
                lastLimb[i] = now;
            }
            limbsReady = true;
        }

        /// <summary>
        /// Corrida sem chacoalhar (06/11, pedido do Jhonata: "fica chacoalhando demais quando começa a correr"): com a passada acompanhando a
        /// velocidade, a disparada toca até 2× e a cabeça grande do kit balançava ~14 cm para a frente e para trás a ~6–7 vezes por segundo (o
        /// tremor cresce com o quadrado do ritmo). Andando/correndo, a coluna, o pescoço e a cabeça seguem a pose do clipe filtrada no espaço do
        /// personagem (um filtro de <see cref="stabilizeSeconds"/>; medido na disparada do capitão, giro da cabeça 5.400 → 1.900 °/s² ≈ o da
        /// corrida antiga em ritmo normal, posição 82 → 44; com 0,14 s melhora pouco e a cabeça atrasaria nas viradas). A inclinação da corrida
        /// e as viradas ficam.
        /// Fora disso (parado, golpe, rolar, guarda, cair, morto) o filtro só acompanha o clipe, para entrar sem pulo.
        /// </summary>
        void Stabilize()
        {
            Transform root = transform;
            Quaternion inv = Quaternion.Inverse(root.rotation);
            for (int i = 0; i < chain.Length; i++) if (chain[i]) animated[i] = inv * chain[i].rotation;
            float dt = Time.deltaTime;
            if (!filterReady || dt <= 0f)
            {
                for (int i = 0; i < chain.Length; i++) filtered[i] = animated[i];
                filterReady = true;
                return;
            }
            float target = 0f;
            if (character && !character.IsDown && character.IsGrounded && (!driver || !driver.InAction) && weight <= 0f)
            {
                float speed = character.AnimatedVelocity.magnitude / Mathf.Max(0.1f, root.lossyScale.y);
                target = Mathf.InverseLerp(stabilizeSpeed.x, stabilizeSpeed.y, speed);
            }
            stabilize = Mathf.MoveTowards(stabilize, target, dt / 0.2f);
            // Arma firme (12/11, bacamarte e mosquete): a pose do rifle prende os braços ao peito e não gira o peito contra o quadril como uma
            // corrida; o quadril girando a cada passada levava peito, braços e arma junto (arma subindo e descendo ~45°, braço esquerdo 122°).
            bool steadyState = false;
            if (layer >= 0 && steadyHashes.Count > 0)
                steadyState = steadyHashes.Contains(animator.GetCurrentAnimatorStateInfo(layer).shortNameHash)
                              || (animator.IsInTransition(layer) && steadyHashes.Contains(animator.GetNextAnimatorStateInfo(layer).shortNameHash));
            steady = Mathf.MoveTowards(steady, steadyState ? 1f : 0f, dt / 0.15f);
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.005f, Mathf.Lerp(stabilizeSeconds, steadySeconds, steady)));
            for (int i = 0; i < chain.Length; i++)
            {
                if (!chain[i]) continue;
                filtered[i] = Quaternion.Slerp(filtered[i], animated[i], k);
                if (stabilize <= 0f) continue;
                float baseAmount = i < stabilizeAmount.Length ? stabilizeAmount[i] : 1f;
                float amount = stabilize * Mathf.Lerp(baseAmount, 1f, i < 3 ? steady : 0f);
                // De cima para baixo na hierarquia: cada osso no absoluto, então o filtro do pai não soma no filho.
                chain[i].rotation = root.rotation * Quaternion.Slerp(animated[i], filtered[i], amount);
            }
        }

        /// <summary>
        /// Lâmina no corte do Kevin Iglesias (06/11, Jhonata: "parece que ele tá dando um soco com a espada virada para baixo"): a pegada das
        /// lâminas foi medida nos golpes do Mixamo e o pulso do "Attack1H01_R" gira de outro jeito; no impacto a lâmina apontava para o chão
        /// (−0,89 na vertical, medido no capitão). Nos <see cref="bladeLiftStates"/>, se a lâmina aponta mais para baixo que
        /// <see cref="bladeMinY"/>, a mão gira no pulso só o que falta para ela ficar no limite, na mesma direção no chão (o braço e o resto do
        /// golpe ficam iguais). Medido na lâmina de cada personagem: vale para capitão, piratas e Guarda Negra, cada um com o seu esqueleto.
        /// </summary>
        void LiftBlade()
        {
            bool on = false;
            if (layer >= 0 && bladeLiftHashes.Count > 0)
            {
                on = bladeLiftHashes.Contains(animator.GetCurrentAnimatorStateInfo(layer).shortNameHash)
                     || (animator.IsInTransition(layer) && bladeLiftHashes.Contains(animator.GetNextAnimatorStateInfo(layer).shortNameHash));
            }
            bladeLift = Mathf.MoveTowards(bladeLift, on ? 1f : 0f, Time.deltaTime / 0.08f);
            if (bladeLift <= 0f || straightWrist >= 1f || !combat || !rightHand) return;
            var weapon = combat.Current;
            if (!weapon || weapon.kind != WeaponKind.Melee) return;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !visual.gameObject.activeInHierarchy) return;
            if (visual != measuredBlade) MeasureBlade(visual);
            Vector3 a = visual.TransformPoint(bladeEndA), b = visual.TransformPoint(bladeEndB);
            Vector3 handPos = rightHand.position;
            Vector3 tip = (a - handPos).sqrMagnitude > (b - handPos).sqrMagnitude ? a : b;
            Vector3 dir = tip - handPos;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            if (dir.y >= bladeMinY) return;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
            flat.Normalize();
            Vector3 wanted = flat * Mathf.Sqrt(1f - bladeMinY * bladeMinY) + Vector3.up * bladeMinY;
            Quaternion fix = Quaternion.FromToRotation(dir, wanted);
            rightHand.rotation = Quaternion.Slerp(Quaternion.identity, fix, bladeLift) * rightHand.rotation;
        }

        bool InStates(HashSet<int> hashes)
        {
            if (layer < 0 || hashes.Count == 0) return false;
            return hashes.Contains(animator.GetCurrentAnimatorStateInfo(layer).shortNameHash)
                   || (animator.IsInTransition(layer) && hashes.Contains(animator.GetNextAnimatorStateInfo(layer).shortNameHash));
        }

        /// <summary>
        /// Pulso reto (07/11, Jhonata: "a mão dele tá toda esquisita" no 1º golpe): no nosso esqueleto o corte do Kevin Iglesias chega com o pulso
        /// dobrado ~80° para cima (mão em pé, lâmina pendurada, parecia um soco), e girar ainda mais a mão para erguer a lâmina deixava o pulso
        /// quebrado. Nos <see cref="straightWristStates"/> a mão volta a ficar reta com o antebraço (como na pose de ligação) e o antebraço gira no
        /// próprio eixo (cotovelo → pulso: a mão não sai do lugar) até a lâmina apontar o mais perto possível de "para fora do peito, um pouco para a
        /// frente e para cima" (<see cref="straightWristAim"/>); o que faltar, o pulso dobra até <see cref="straightWristBend"/>. O braço, o ombro e o arco da mão ficam os do clipe.
        /// </summary>
        void StraightWrist()
        {
            bool strike = InStates(straightWristHashes), ready = !strike && InStates(readyWristHashes);
            if (strike || ready) readyAim = ready;
            straightWrist = Mathf.MoveTowards(straightWrist, strike || ready ? 1f : 0f, Time.deltaTime / 0.1f);
            if (straightWrist <= 0f || !combat || !rightHand || !rightFore) return;
            var weapon = combat.Current;
            if (!weapon || weapon.kind != WeaponKind.Melee) return;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !visual.gameObject.activeInHierarchy) return;
            if (visual != measuredBlade) MeasureBlade(visual);
            Vector3 handPos = rightHand.position;
            Vector3 a = visual.TransformPoint(bladeEndA), b = visual.TransformPoint(bladeEndB);
            Vector3 tip = (a - handPos).sqrMagnitude > (b - handPos).sqrMagnitude ? a : b;
            Vector3 bladeInHand = Quaternion.Inverse(rightHand.rotation) * (tip - handPos);
            if (bladeInHand.sqrMagnitude < 1e-6f) return;

            Quaternion straight = rightFore.rotation * handInFore;
            Vector3 axis = handPos - rightFore.position;
            if (axis.sqrMagnitude < 1e-6f) return;
            axis.Normalize();
            Vector3 chestPos = (upperChest ? upperChest : chest ? chest : hips).position;
            Vector3 outward = Vector3.ProjectOnPlane(handPos - chestPos, transform.up);
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : transform.right;
            Vector3 target = readyAim
                ? (outward * readyWristAim.x + transform.forward * readyWristAim.y + transform.up * readyWristAim.z).normalized
                : (outward + transform.forward * straightWristAim.x + transform.up * straightWristAim.y).normalized;

            // Lâmina com o pulso reto, girando em volta do eixo do antebraço: o ângulo que mais a aproxima do alvo.
            Vector3 d = (straight * bladeInHand).normalized;
            Vector3 perp = d - Vector3.Dot(d, axis) * axis;
            float roll = Mathf.Atan2(Vector3.Dot(Vector3.Cross(axis, perp), target), Vector3.Dot(perp, target)) * Mathf.Rad2Deg;
            Quaternion spin = Quaternion.AngleAxis(roll, axis);

            // O que faltar, o pulso dobra (pouco: um pulso de verdade não passa muito disso).
            Quaternion wanted = spin * straight;
            Vector3 rolled = (wanted * bladeInHand).normalized;
            Quaternion bend = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(rolled, target), straightWristBend);
            wanted = bend * wanted;

            Quaternion animatedHand = rightHand.rotation;
            rightFore.rotation = Quaternion.Slerp(Quaternion.identity, spin, straightWrist) * rightFore.rotation;
            rightHand.rotation = Quaternion.Slerp(animatedHand, wanted, straightWrist);
        }

        /// <summary>As duas pontas do eixo comprido das malhas da arma, no espaço do visual (uma vez por arma).</summary>
        void MeasureBlade(Transform visual)
        {
            measuredBlade = visual;
            var bounds = new Bounds();
            bool any = false;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh) continue;
                var mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = visual.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            var size = bounds.size;
            int axis = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
            bladeEndA = bounds.center; bladeEndB = bounds.center;
            bladeEndA[axis] = bounds.min[axis];
            bladeEndB[axis] = bounds.max[axis];
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
