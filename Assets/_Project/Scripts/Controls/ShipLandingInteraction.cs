using Pirates.Core;
using Pirates.OnFoot;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// "Desembarcar" como interação do navio (tecla F): perto de uma praia de desembarque e devagar.
    /// É uma ação, não uma janela: nunca fica "aberta".
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class ShipLandingInteraction : MonoBehaviour, IInteraction
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Velocidade máxima (nós) para desembarcar.")]
        [SerializeField] float maxSpeedKnots = 3f;

        ShipController ship;

        public LandingPoint Nearby { get; private set; }
        public bool TooFast => ship.SpeedKnots > maxSpeedKnots;
        public float SpeedKnots => ship.SpeedKnots;
        public float MaxSpeedKnots => maxSpeedKnots;

        public bool IsOpen => false;
        public bool CanOpen => switcher && !switcher.IsOnFoot && Nearby && !TooFast;

        public void Configure(PlayerModeSwitcher modeSwitcher) => switcher = modeSwitcher;

        void Awake() => ship = GetComponent<ShipController>();

        void Update()
        {
            Nearby = null;
            float best = float.MaxValue;
            for (int i = 0; i < LandingPoint.Active.Count; i++)
            {
                var point = LandingPoint.Active[i];
                float d = point.FlatDistance(transform.position);
                if (d > point.ShipRange || d > best) continue;
                Nearby = point;
                best = d;
            }
        }

        public bool Open()
        {
            if (!CanOpen) return false;
            switcher.Disembark(Nearby);
            return true;
        }

        public void Close() { }
    }
}
