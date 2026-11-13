using System.Collections.Generic;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Banqueta do balcão da taverna onde o capitão senta para tomar uma caneca (13/11, pedido do Jhonata: "pedir uma bebida pro barman, só
    /// para ficar escutando a música ambiente"). <see cref="Sit"/> põe o capitão sentado na banqueta com a caneca na frente: o Animator dele
    /// troca para o dos fregueses (as mesmas animações de sentar, trocando a cada 14–32 s), a câmera vira para o balcão e a HUD de baixo some
    /// (<see cref="ScreenPanel"/>). F, Esc ou andar levantam (<see cref="StandUp"/>): tudo volta como estava, inclusive os parâmetros do Animator.
    /// </summary>
    public class BarSeat : MonoBehaviour
    {
        [Tooltip("Onde fica a raiz do capitão sentado (um pouco atrás e abaixo do meio do assento).")]
        [SerializeField] Transform body;
        [Tooltip("Onde ele fica de pé ao levantar.")]
        [SerializeField] Transform standPoint;
        [Tooltip("A caneca no balcão (aparece com ele sentado).")]
        [SerializeField] GameObject mug;
        [SerializeField] RuntimeAnimatorController seatedController;
        [SerializeField] string[] states = { "Sentado_Parado1", "Sentado_Conversa2", "Sentado_Parado2", "Sentado_Comemora" };
        [SerializeField] Vector2 switchSeconds = new(14f, 32f);
        [SerializeField] float blendSeconds = 0.8f;

        static readonly List<BarSeat> active = new();
        public static IReadOnlyList<BarSeat> Active => active;
        /// <summary>A banqueta em que o capitão está sentado agora (null = nenhuma).</summary>
        public static BarSeat Seated { get; private set; }

        public bool IsSeated => Seated == this;

        PlayerModeSwitcher switcher;
        GameObject captain;
        Animator animator;
        RuntimeAnimatorController savedController;
        bool savedRootMotion;
        readonly List<(AnimatorControllerParameter p, float f, int i, bool b)> savedParams = new();
        readonly List<Behaviour> disabled = new();
        readonly List<Renderer> hidden = new();
        int sitFrame;
        float nextSwitch;
        int current = -1;

        public void Configure(Transform seatBody, Transform stand, GameObject mugProp, RuntimeAnimatorController controller)
        {
            body = seatBody;
            standPoint = stand;
            mug = mugProp;
            seatedController = controller;
            if (mug) mug.SetActive(false);
        }

        void OnEnable() => active.Add(this);

        void OnDisable()
        {
            active.Remove(this);
            if (IsSeated) StandUp();
        }

        /// <summary>Senta o capitão (a pé) aqui. Falso = não deu (navio, já sentado, sem animações).</summary>
        public bool Sit()
        {
            if (Seated || !body || !seatedController) return false;
            switcher = FindAnyObjectByType<PlayerModeSwitcher>();
            if (!switcher || !switcher.IsOnFoot || !switcher.Character) return false;
            captain = switcher.Character.gameObject;
            var foot = captain.GetComponent<FootCharacter>();
            animator = foot ? foot.GetComponentInChildren<Animator>() : null;
            if (!animator) return false;

            // Quem mexe no personagem ou no Animator dele para enquanto ele está sentado.
            disabled.Clear();
            Disable(foot);
            Disable(captain.GetComponent<CharacterController>());
            Disable(captain.GetComponent<PlayerInteractInput>());
            Disable(captain.GetComponent<PlayerFootInput>());
            foreach (var b in animator.GetComponents<Behaviour>())
                if (b is CharacterAnimatorDriver or SupportHandIK or PostureLimiter or ProceduralTouches or TwoHandSteer) Disable(b);

            // Nada na mão (arma, ferramenta): esconde e devolve ao levantar.
            hidden.Clear();
            Hide(animator.GetBoneTransform(HumanBodyBones.RightHand));
            Hide(animator.GetBoneTransform(HumanBodyBones.LeftHand));
            // As armas guardadas (nas costas, no cinto) também: o espadão nas costas atravessava a banqueta.
            if (captain.TryGetComponent(out CharacterCombat combat))
                for (int i = 0; i < combat.Weapons.Count; i++) Hide(combat.WeaponVisual(i));

            savedController = animator.runtimeAnimatorController;
            savedRootMotion = animator.applyRootMotion;
            savedParams.Clear();
            foreach (var p in animator.parameters)
                savedParams.Add((p, p.type == AnimatorControllerParameterType.Float ? animator.GetFloat(p.nameHash) : 0f,
                    p.type == AnimatorControllerParameterType.Int ? animator.GetInteger(p.nameHash) : 0,
                    p.type == AnimatorControllerParameterType.Bool && animator.GetBool(p.nameHash)));

            captain.transform.SetPositionAndRotation(body.position, body.rotation);
            animator.runtimeAnimatorController = seatedController;
            animator.applyRootMotion = false;
            current = Random.Range(0, states.Length);
            animator.Play(states[current], 0, Random.value);
            nextSwitch = Time.time + Random.Range(switchSeconds.x, switchSeconds.y);

            if (mug) mug.SetActive(true);
            ScreenPanel.Open = true;
            if (Services.TryGet(out ICameraFollow cam)) cam.LookToward(body.eulerAngles.y);
            Seated = this;
            sitFrame = Time.frameCount;
            return true;
        }

        public void StandUp()
        {
            if (!IsSeated) return;
            Seated = null;
            if (animator)
            {
                animator.runtimeAnimatorController = savedController;
                animator.applyRootMotion = savedRootMotion;
                foreach (var (p, f, i, b) in savedParams)
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float: animator.SetFloat(p.nameHash, f); break;
                        case AnimatorControllerParameterType.Int: animator.SetInteger(p.nameHash, i); break;
                        case AnimatorControllerParameterType.Bool: animator.SetBool(p.nameHash, b); break;
                    }
            }
            foreach (var r in hidden) if (r) r.enabled = true;
            hidden.Clear();
            foreach (var b in disabled) if (b) b.enabled = true;
            disabled.Clear();
            if (disabledController)
            {
                disabledController.enabled = true;
                disabledController = null;
            }
            if (mug) mug.SetActive(false);
            ScreenPanel.Open = false;
            if (captain && standPoint && captain.TryGetComponent(out FootCharacter foot))
                foot.Teleport(standPoint.position, standPoint.rotation);
        }

        void Update()
        {
            if (!IsSeated) return;
            if (!captain || !captain.activeInHierarchy || !switcher || !switcher.IsOnFoot)
            {
                StandUp();
                return;
            }
            captain.transform.SetPositionAndRotation(body.position, body.rotation);

            var input = GameInput.Instance;
            if (Time.frameCount > sitFrame + 1 && input != null &&
                (input.Interact.WasPressedThisFrame() || input.Cancel.WasPressedThisFrame() || input.Move.ReadValue<Vector2>().sqrMagnitude > 0.25f))
            {
                StandUp();
                return;
            }

            if (Time.time >= nextSwitch && states.Length > 1)
            {
                nextSwitch = Time.time + Random.Range(switchSeconds.x, switchSeconds.y);
                int next = Random.Range(0, states.Length - 1);
                if (next >= current) next++;
                current = next;
                animator.CrossFadeInFixedTime(states[current], blendSeconds);
            }
        }

        void Disable(Behaviour b)
        {
            if (!b || !b.enabled) return;
            b.enabled = false;
            disabled.Add(b);
        }

        void Disable(CharacterController c)
        {
            if (c && c.enabled)
            {
                c.enabled = false;
                disabledController = c;
            }
        }

        CharacterController disabledController;

        void Hide(Transform bone)
        {
            if (!bone) return;
            foreach (var r in bone.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                {
                    r.enabled = false;
                    hidden.Add(r);
                }
        }
    }
}
