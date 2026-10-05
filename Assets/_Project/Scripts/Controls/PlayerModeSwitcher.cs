using System;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Troca o jogador entre navegar e andar a pé. Ao desembarcar: o navio lança âncora (velas recolhidas e
    /// arrasto forte), o input do navio desliga, o personagem aparece em terra e a câmera passa a segui-lo, virada para onde
    /// ele olha ao descer (para a terra, não para o mar). Com uma
    /// SafeZone no navio, ela liga enquanto ele está ancorado: navios inimigos não o atacam com o jogador em terra.
    /// Ao voltar (F perto do bote: BoatBoarding), desfaz tudo. No M4+ isto vira o estado Sailing ↔ OnFoot do jogo.
    /// </summary>
    public class PlayerModeSwitcher : MonoBehaviour
    {
        [SerializeField] ShipController ship;
        [SerializeField] FootCharacter character;
        [Tooltip("Arrasto horizontal da água com a âncora lançada (o navio quase não deriva).")]
        [SerializeField] float anchorDrag = 2.5f;

        [Header("Câmera")]
        [SerializeField] Vector3 shipZoom = new(18f, 75f, 40f);
        [SerializeField] Vector3 footZoom = new(6f, 30f, 13f);

        Buoyancy shipBuoyancy;
        // Zona segura no navio ancorado: inimigos não o atacam enquanto o jogador está em terra.
        SafeZone anchorZone;
        float shipDrag;
        Behaviour[] shipControls = Array.Empty<Behaviour>();
        LandingPoint landing;
        Health characterHealth;
        // A mesma tecla F desembarca e embarca: sem esta trava, o F que desembarca embarcaria no mesmo frame.
        int lastSwitchFrame = -1;

        public bool IsOnFoot { get; private set; }
        public LandingPoint Landing => landing;
        public FootCharacter Character => character;
        public ShipController Ship => ship;
        /// <summary>A pé, vivo e perto do bote: F volta ao navio.</summary>
        public bool CanEmbark => IsOnFoot && landing && !(characterHealth && characterHealth.IsDead)
                                 && landing.BoardingDistance(character.transform.position) <= landing.BoardingRange;

        /// <summary>true = desembarcou, false = voltou ao navio.</summary>
        public event Action<bool> ModeChanged;

        public void Configure(ShipController playerShip, FootCharacter playerCharacter)
        {
            ship = playerShip;
            character = playerCharacter;
        }

        void Awake()
        {
            shipBuoyancy = ship.GetComponent<Buoyancy>();
            if (ship.TryGetComponent(out anchorZone)) anchorZone.enabled = false;
            // Tudo que o jogador controla no navio (velas/leme, canhões, F do navio) desliga em terra.
            shipControls = new Behaviour[]
            {
                ship.GetComponent<PlayerShipInput>(), ship.GetComponent<PlayerCannonInput>(), ship.GetComponent<PlayerInteractInput>(),
                ship.GetComponent<PlayerShipExtrasInput>(),
            };
            character.TryGetComponent(out characterHealth);
            character.gameObject.SetActive(false);
        }

        public void Disembark(LandingPoint point)
        {
            if (IsOnFoot || !point || Time.frameCount == lastSwitchFrame) return;
            lastSwitchFrame = Time.frameCount;
            landing = point;
            IsOnFoot = true;

            foreach (var c in shipControls) if (c) c.enabled = false;
            ship.SetSail(FurledSailIndex());
            if (anchorZone) anchorZone.enabled = true;
            if (shipBuoyancy)
            {
                shipDrag = shipBuoyancy.HorizontalDrag;
                shipBuoyancy.HorizontalDrag = Mathf.Max(shipDrag, anchorDrag);
            }

            var land = point.LandPosition;
            character.gameObject.SetActive(true);
            character.Teleport(land.position, land.rotation);
            Follow(character.transform, footZoom);
            if (Services.TryGet(out ICameraFollow cam)) cam.LookToward(land.eulerAngles.y);
            GameInput.Instance.FootCameraControls = true;
            ModeChanged?.Invoke(true);
        }

        public void Embark()
        {
            if (!IsOnFoot || Time.frameCount == lastSwitchFrame) return;
            lastSwitchFrame = Time.frameCount;
            IsOnFoot = false;
            character.gameObject.SetActive(false);

            foreach (var c in shipControls) if (c) c.enabled = true;
            if (shipBuoyancy) shipBuoyancy.HorizontalDrag = shipDrag;
            if (anchorZone) anchorZone.enabled = false;
            Follow(ship.transform, shipZoom);
            GameInput.Instance.FootCameraControls = false;
            ModeChanged?.Invoke(false);
        }

        static void Follow(Transform target, Vector3 zoom)
        {
            if (Services.TryGet(out ICameraFollow cam)) cam.Follow(target, zoom.x, zoom.y, zoom.z);
        }

        int FurledSailIndex()
        {
            var levels = ship.Stats.sailLevels;
            for (int i = 0; i < levels.Length; i++)
                if (Mathf.Approximately(levels[i].throttle, 0f)) return i;
            return 0;
        }
    }
}
