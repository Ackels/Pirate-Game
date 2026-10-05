using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>Traduz o input do jogador em ShipCommand. A IA inimiga (M2) implementa a mesma interface.</summary>
    public class PlayerShipInput : MonoBehaviour, IShipInput
    {
        public ShipCommand GetCommand()
        {
            // Desligado quando o jogador está a pé: o ShipController continua pedindo comando.
            if (!enabled) return default;
            var input = GameInput.Instance;
            return new ShipCommand
            {
                steer = input.Steer.ReadValue<float>(),
                sailDelta = (input.SailUp.WasPressedThisFrame() ? 1 : 0)
                          - (input.SailDown.WasPressedThisFrame() ? 1 : 0),
            };
        }
    }
}
