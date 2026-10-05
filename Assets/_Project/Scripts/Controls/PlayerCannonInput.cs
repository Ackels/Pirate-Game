using Pirates.Combat;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Controls
{
    /// <summary>
    /// Segurar mira, soltar dispara. Uma ação por bordo.
    /// Durante a mira (e um instante depois do disparo) a câmera enquadra o impacto previsto. Navio que troca de casco
    /// (ShipController.HullChanged): pega os canhões do casco novo.
    /// </summary>
    public class PlayerCannonInput : MonoBehaviour
    {
        [SerializeField] CannonBattery port;
        [SerializeField] CannonBattery starboard;
        [Tooltip("Segundos que a câmera continua no alvo depois do disparo, para ver onde as balas caem.")]
        [SerializeField] float holdFramingAfterFire = 1.5f;

        ICameraAimFocus cameraFocus;
        ShipController ship;
        Vector3 lastImpact;
        float holdUntil = -1f;

        public void Configure(CannonBattery portBattery, CannonBattery starboardBattery)
        {
            port = portBattery;
            starboard = starboardBattery;
        }

        void OnEnable()
        {
            // Desligado (capitão em terra), não ouve a troca de casco: confere ao voltar se os canhões ainda são os ligados.
            if (port && !port.gameObject.activeInHierarchy) FindBatteries();
            if (port) port.Fired += OnPortFired;
            if (starboard) starboard.Fired += OnStarboardFired;
            if (ship || TryGetComponent(out ship)) ship.HullChanged += OnHullChanged;
        }

        void OnDisable()
        {
            if (port) port.Fired -= OnPortFired;
            if (starboard) starboard.Fired -= OnStarboardFired;
            if (ship) ship.HullChanged -= OnHullChanged;
        }

        void OnHullChanged()
        {
            if (port) port.Fired -= OnPortFired;
            if (starboard) starboard.Fired -= OnStarboardFired;
            FindBatteries();
            if (port) port.Fired += OnPortFired;
            if (starboard) starboard.Fired += OnStarboardFired;
        }

        /// <summary>Os canhões ligados agora (os do casco antigo ficam desligados junto com ele).</summary>
        void FindBatteries()
        {
            port = starboard = null;
            foreach (var battery in GetComponentsInChildren<CannonBattery>())
                if (battery.Side == BroadsideSide.Port) port = battery;
                else starboard = battery;
        }

        void OnPortFired() => HoldFraming(port);
        void OnStarboardFired() => HoldFraming(starboard);

        void HoldFraming(CannonBattery battery)
        {
            lastImpact = battery.PredictedImpact;
            holdUntil = Time.time + holdFramingAfterFire;
        }

        void Update()
        {
            var input = GameInput.Instance;
            Handle(input.FirePort, port);
            Handle(input.FireStarboard, starboard);
        }

        // LateUpdate: roda depois de CannonBattery.Update ter calculado o impacto previsto.
        void LateUpdate()
        {
            if (cameraFocus == null && !Services.TryGet(out cameraFocus)) return;
            CannonBattery aiming = IsAiming(port) ? port : IsAiming(starboard) ? starboard : null;
            if (aiming) cameraFocus.SetAimPoint(aiming.PredictedImpact);
            else if (Time.time < holdUntil) cameraFocus.SetAimPoint(lastImpact);
            else cameraFocus.ClearAimPoint();
        }

        static bool IsAiming(CannonBattery battery) => battery && battery.State == BatteryState.Aiming;

        static void Handle(InputAction action, CannonBattery battery)
        {
            if (!battery) return;
            if (action.WasPressedThisFrame()) battery.BeginAim();
            if (action.WasReleasedThisFrame()) battery.ReleaseAndFire();
        }
    }
}
