using Pirates.Core;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// "Voltar ao navio" como interação a pé (tecla F perto do bote), ao lado do baú e de outras interações em terra.
    /// É uma ação, não uma janela: nunca fica "aberta".
    /// </summary>
    public class BoatBoarding : MonoBehaviour, IInteraction
    {
        [SerializeField] PlayerModeSwitcher switcher;

        public bool IsOpen => false;
        public bool CanOpen => switcher && switcher.CanEmbark;

        public void Configure(PlayerModeSwitcher modeSwitcher) => switcher = modeSwitcher;

        public bool Open()
        {
            if (!CanOpen) return false;
            switcher.Embark();
            return true;
        }

        public void Close() { }
    }
}
