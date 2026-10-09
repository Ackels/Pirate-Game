using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Controls
{
    /// <summary>
    /// Ações de input definidas em código (sem asset .inputactions) para o protótipo.
    /// Teclado + gamepad. No M3 viram mapas separados por estado (Navegar / A pé / UI).
    /// </summary>
    public sealed class GameInput
    {
        static GameInput instance;
        public static GameInput Instance => instance ??= new GameInput();

        readonly InputActionMap sailing;

        public InputAction Steer { get; }
        public InputAction SailUp { get; }
        public InputAction SailDown { get; }
        public InputAction FirePort { get; }
        public InputAction FireStarboard { get; }
        public InputAction CameraOrbit { get; }
        public InputAction CameraPitch { get; }
        public InputAction CameraZoomScroll { get; }
        public InputAction CameraZoomStick { get; }
        public InputAction Interact { get; }
        public InputAction Move { get; }
        public InputAction Run { get; }
        public InputAction Jump { get; }
        /// <summary>A pé: rolamento (esquiva).</summary>
        public InputAction Dodge { get; }
        /// <summary>A pé: chute (derruba o pirata da frente).</summary>
        public InputAction Kick { get; }
        public InputAction Attack { get; }
        public InputAction Aim { get; }
        public InputAction Weapon1 { get; }
        public InputAction Weapon2 { get; }
        /// <summary>No navio, 1/2/3 escolhem a munição (a pé, 1/2 trocam a arma; quem está no comando lê).</summary>
        public InputAction Weapon3 { get; }
        /// <summary>No navio: liga/desliga o carpinteiro (conserto no mar).</summary>
        public InputAction Repair { get; }
        public InputAction NextWeapon { get; }
        public InputAction Cancel { get; }
        /// <summary>Abre/fecha o painel da fama do capitão (M10), no navio e a pé.</summary>
        public InputAction Fame { get; }

        public InputAction CameraOrbitKeys { get; }

        /// <summary>
        /// Controles de câmera a pé: o botão direito mira a pistola, então a câmera gira com o botão do meio
        /// (clique do scroll) + mouse ou com Q/E (no navio Q/E disparam os canhões). No navio, botão direito ou do meio.
        /// Quem troca é o PlayerModeSwitcher.
        /// </summary>
        public bool FootCameraControls { get; set; }

        /// <summary>Desligado = nenhum comando de jogo (menu inicial por cima do jogo parado).</summary>
        public bool GameplayEnabled
        {
            get => sailing.enabled;
            set
            {
                if (value) sailing.Enable();
                else sailing.Disable();
            }
        }

        GameInput()
        {
            sailing = new InputActionMap("Sailing");

            Steer = sailing.AddAction("Steer", InputActionType.Value, expectedControlLayout: "Axis");
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            Steer.AddBinding("<Gamepad>/leftStick/x");

            SailUp = sailing.AddAction("SailUp", InputActionType.Button);
            SailUp.AddBinding("<Keyboard>/w");
            SailUp.AddBinding("<Keyboard>/upArrow");
            SailUp.AddBinding("<Gamepad>/rightShoulder");

            SailDown = sailing.AddAction("SailDown", InputActionType.Button);
            SailDown.AddBinding("<Keyboard>/s");
            SailDown.AddBinding("<Keyboard>/downArrow");
            SailDown.AddBinding("<Gamepad>/leftShoulder");

            FirePort = sailing.AddAction("FirePort", InputActionType.Button);
            FirePort.AddBinding("<Keyboard>/q");
            FirePort.AddBinding("<Gamepad>/leftTrigger");

            FireStarboard = sailing.AddAction("FireStarboard", InputActionType.Button);
            FireStarboard.AddBinding("<Keyboard>/e");
            FireStarboard.AddBinding("<Gamepad>/rightTrigger");

            // Órbita no teclado é pelo botão direito do mouse (lido direto no ElevatedCameraRig).
            CameraOrbit = sailing.AddAction("CameraOrbit", InputActionType.Value, expectedControlLayout: "Axis");
            CameraOrbit.AddBinding("<Gamepad>/rightStick/x");

            // A pé: Q/E giram a câmera (o ElevatedCameraRig só lê com FootCameraControls ligado).
            CameraOrbitKeys = sailing.AddAction("CameraOrbitKeys", InputActionType.Value, expectedControlLayout: "Axis");
            CameraOrbitKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");

            // Inclinação no teclado/mouse é botão direito + mouse vertical (lido direto no ElevatedCameraRig).
            CameraPitch = sailing.AddAction("CameraPitch", InputActionType.Value, expectedControlLayout: "Axis");
            CameraPitch.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/dpad/down").With("Positive", "<Gamepad>/dpad/up");

            CameraZoomScroll = sailing.AddAction("CameraZoomScroll", InputActionType.Value,
                binding: "<Mouse>/scroll/y", expectedControlLayout: "Axis");
            CameraZoomStick = sailing.AddAction("CameraZoomStick", InputActionType.Value,
                binding: "<Gamepad>/rightStick/y", expectedControlLayout: "Axis");

            // A pé (M4). As mesmas teclas do navio (WASD): quem está no comando lê, o outro fica desligado.
            Move = sailing.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Run = sailing.AddAction("Run", InputActionType.Button);
            Run.AddBinding("<Keyboard>/leftShift");
            Run.AddBinding("<Gamepad>/leftStickPress");

            Jump = sailing.AddAction("Jump", InputActionType.Button);
            Jump.AddBinding("<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Dodge = sailing.AddAction("Dodge", InputActionType.Button);
            Dodge.AddBinding("<Keyboard>/leftCtrl");
            Dodge.AddBinding("<Gamepad>/rightShoulder");

            Kick = sailing.AddAction("Kick", InputActionType.Button);
            Kick.AddBinding("<Keyboard>/v");
            Kick.AddBinding("<Gamepad>/leftShoulder");

            // Combate a pé (M4.3): botão esquerdo ataca na direção do cursor; 1/2 trocam espada/pistola.
            Attack = sailing.AddAction("Attack", InputActionType.Button);
            Attack.AddBinding("<Mouse>/leftButton");
            Attack.AddBinding("<Gamepad>/rightTrigger");
            // Segurando: pistola em pose de mira, seguindo o cursor (o esquerdo atira na hora).
            Aim = sailing.AddAction("Aim", InputActionType.Button);
            Aim.AddBinding("<Mouse>/rightButton");
            Aim.AddBinding("<Gamepad>/leftTrigger");
            Weapon1 = sailing.AddAction("Weapon1", InputActionType.Button, binding: "<Keyboard>/1");
            Weapon2 = sailing.AddAction("Weapon2", InputActionType.Button, binding: "<Keyboard>/2");
            Weapon3 = sailing.AddAction("Weapon3", InputActionType.Button, binding: "<Keyboard>/3");
            Repair = sailing.AddAction("Repair", InputActionType.Button, binding: "<Keyboard>/r");
            Repair.AddBinding("<Gamepad>/dpad/left");
            NextWeapon = sailing.AddAction("NextWeapon", InputActionType.Button, binding: "<Gamepad>/buttonNorth");

            Interact = sailing.AddAction("Interact", InputActionType.Button);
            Interact.AddBinding("<Keyboard>/f");
            Interact.AddBinding("<Gamepad>/buttonWest");

            Cancel = sailing.AddAction("Cancel", InputActionType.Button);
            Cancel.AddBinding("<Keyboard>/escape");
            Cancel.AddBinding("<Gamepad>/buttonEast");

            Fame = sailing.AddAction("Fame", InputActionType.Button, binding: "<Keyboard>/c");
            Fame.AddBinding("<Gamepad>/select");

            sailing.Enable();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance?.sailing.Dispose();
            instance = null;
        }
    }
}
