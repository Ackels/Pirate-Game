using Pirates.Progression;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Teclas extras no navio (M9): 1 / 2 / 3 escolhem a munição (comum, corrente, metralha; gamepad: botão norte troca) e
    /// R liga/desliga o carpinteiro. Desliga em terra junto com os outros controles do navio (PlayerModeSwitcher).
    /// </summary>
    public class PlayerShipExtrasInput : MonoBehaviour
    {
        ShipAmmo ammo;
        ShipCarpenter carpenter;

        void Awake()
        {
            TryGetComponent(out ammo);
            TryGetComponent(out carpenter);
        }

        void Update()
        {
            var input = GameInput.Instance;
            if (ammo)
            {
                if (input.Weapon1.WasPressedThisFrame()) ammo.Select(0);
                if (input.Weapon2.WasPressedThisFrame()) ammo.Select(1);
                if (input.Weapon3.WasPressedThisFrame()) ammo.Select(2);
                if (input.NextWeapon.WasPressedThisFrame()) ammo.Cycle();
            }
            if (carpenter && input.Repair.WasPressedThisFrame()) carpenter.Toggle();
        }
    }
}
