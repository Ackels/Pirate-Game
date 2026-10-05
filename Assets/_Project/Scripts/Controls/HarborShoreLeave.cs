using Pirates.Base;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// "Atracar e desembarcar" como interação do navio (tecla F, cena 07): perto do cais e devagar, atraca (o porão
    /// descarrega no armazém ao chegar) e o capitão desce no píer. Já atracado (voltou a bordo), F só desce de novo.
    /// O porto fica por conta das casas da vila (HarborBuildingVisitor no personagem); zarpar para longe desatraca.
    /// É uma ação, não uma janela: nunca fica "aberta".
    /// </summary>
    [RequireComponent(typeof(HarborVisitor))]
    public class HarborShoreLeave : MonoBehaviour, IInteraction
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Ponto no píer onde o capitão desce (e perto dele, volta a bordo).")]
        [SerializeField] LandingPoint pier;

        HarborVisitor visitor;

        public LandingPoint Pier => pier;
        public bool IsOpen => false;
        public bool CanOpen => switcher && pier && !switcher.IsOnFoot && visitor.CanDock;

        public void Configure(PlayerModeSwitcher modeSwitcher, LandingPoint pierLanding)
        {
            switcher = modeSwitcher;
            pier = pierLanding;
        }

        void Awake() => visitor = GetComponent<HarborVisitor>();

        public bool Open()
        {
            if (!CanOpen || !visitor.Dock(unloadHold: true)) return false;
            switcher.Disembark(pier);
            return true;
        }

        public void Close() { }
    }
}
