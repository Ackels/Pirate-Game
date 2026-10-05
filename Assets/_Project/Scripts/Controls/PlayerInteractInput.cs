using Pirates.Core;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// F abre/fecha a interação disponível no mesmo objeto: no navio (saque de destroços, porto, desembarcar) ou no
    /// personagem a pé (baú, voltar ao navio); Esc fecha. Com uma janela aberta, F fecha essa; senão abre a primeira
    /// que estiver ao alcance.
    /// </summary>
    public class PlayerInteractInput : MonoBehaviour
    {
        IInteraction[] interactions;

        void Awake() => interactions = GetComponents<IInteraction>();

        void Update()
        {
            var input = GameInput.Instance;
            bool interact = input.Interact.WasPressedThisFrame();
            if (!interact && !input.Cancel.WasPressedThisFrame()) return;

            foreach (var i in interactions)
            {
                if (!i.IsOpen) continue;
                i.Close();
                return;
            }
            if (!interact) return;
            foreach (var i in interactions)
                if (i.CanOpen && i.Open()) return;
        }
    }
}
