using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Controls
{
    /// <summary>
    /// Input do jogador a pé: WASD/analógico move, Shift corre, Espaço pula, botão esquerdo ataca na direção do
    /// cursor (ponto do chão sob o mouse), botão direito (segurar) mira a arma de fogo, 1/2/3 trocam de arma.
    /// No gamepad não há cursor: ataca e mira para a frente. Com uma janela aberta (baú, casa do porto), os cliques são
    /// da janela: não ataca, não mira, não pula nem troca de arma (andar continua, e se afastar fecha a janela).
    /// </summary>
    public class PlayerFootInput : MonoBehaviour, IFootInput
    {
        [Tooltip("Distância máxima (m) do raio do cursor até o chão.")]
        [SerializeField] float aimRayDistance = 200f;

        // RaycastAll criava um array novo a cada quadro; 64 sobra para um raio de 200 m (a ordem não importa: pega o mais perto).
        static readonly RaycastHit[] aimHits = new RaycastHit[64];

        IInteraction[] interactions;
        BombThrower bombs;

        void Awake()
        {
            interactions = GetComponents<IInteraction>();
            bombs = GetComponent<BombThrower>();
        }

        /// <summary>Janela do porto, baú ou painel aberto: o mouse é da janela (bomba e comida também esperam).</summary>
        public bool IsWindowOpen => WindowOpen;

        /// <summary>Ponto do chão sob o cursor (bomba de arremesso).</summary>
        public bool TryGetCursorPoint(out Vector3 point) => TryCursorAim(out point);

        bool WindowOpen
        {
            get
            {
                // Painel da fama (tecla C) também usa o mouse.
                if (ScreenPanel.Open) return true;
                if (interactions == null) return false;
                foreach (var i in interactions) if (i.IsOpen) return true;
                return false;
            }
        }

        public FootCommand GetCommand()
        {
            if (!enabled) return default;
            var input = GameInput.Instance;
            var command = new FootCommand
            {
                move = CameraRelative(Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f)),
                run = input.Run.IsPressed(),
                jump = input.Jump.WasPressedThisFrame(),
                dodge = input.Dodge.WasPressedThisFrame(),
                kick = input.Kick.WasPressedThisFrame(),
                attack = input.Attack.WasPressedThisFrame(),
                aim = input.Aim.IsPressed(),
                selectWeapon = input.Weapon1.WasPressedThisFrame() ? 1 : input.Weapon2.WasPressedThisFrame() ? 2
                    : input.Weapon3.WasPressedThisFrame() ? 3 : 0,
                nextWeapon = input.NextWeapon.WasPressedThisFrame(),
            };
            command.hasAim = TryCursorAim(out command.aimPoint);
            if (WindowOpen)
            {
                command.attack = command.aim = command.jump = command.dodge = command.kick = command.nextWeapon = false;
                command.selectWeapon = 0;
            }
            // Mirando a bomba (segurando G): o mouse é da mira, não ataca nem mira a pistola.
            if (bombs && bombs.IsAiming) command.attack = command.aim = false;
            return command;
        }

        /// <summary>WASD/analógico são relativos à câmera; o personagem recebe a direção no mundo (x, z).</summary>
        static Vector2 CameraRelative(Vector2 move)
        {
            var cam = Camera.main;
            Vector3 forward = cam ? cam.transform.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 world = right * move.x + forward * move.y;
            return new Vector2(world.x, world.z);
        }

        /// <summary>Ponto do chão sob o cursor; sem chão, o plano na altura dos pés do personagem.</summary>
        bool TryCursorAim(out Vector3 point)
        {
            point = default;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || !cam) return false;
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            // O acerto mais próximo que não seja o próprio personagem (o raio sai da câmera e pode passar por ele).
            float nearest = float.MaxValue;
            int count = Physics.RaycastNonAlloc(ray, aimHits, aimRayDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = aimHits[i];
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                point = hit.point;
            }
            if (nearest < float.MaxValue) return true;
            var plane = new Plane(Vector3.up, transform.position);
            if (!plane.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }
    }
}
