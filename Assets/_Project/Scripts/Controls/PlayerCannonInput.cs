using Pirates.Combat;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Controls
{
    /// <summary>
    /// Mira dos canhões (03/11, pedido do Jhonata: segurar Q/E e só atirar de lado "não estou gostando"): <b>Q/E arma</b> a mira daquele bordo
    /// e ela fica ligada (sem segurar) até apertar de novo, trocar de bordo ou Esc; o <b>cursor no mar</b> dá a distância e o ângulo (os
    /// canhões giram até <see cref="CannonStats.maxAimTraverse"/> para a frente e para trás do través; passou disso, a linha fica vermelha e
    /// a mira para na borda) e o <b>clique esquerdo dispara</b>. Depois do tiro a mira continua armada e volta quando a bateria recarrega.
    /// Armada, a câmera enquadra o bordo (um ponto fixo no través, longe), não o impacto: com o cursor mandando na mira, seguir o impacto
    /// fazia a mira correr sozinha. Janela, painel ou desembarque desarmam. Navio que troca de casco (ShipController.HullChanged): pega
    /// os canhões do casco novo. Com um bordo armado e os canhões de caça montados (upgrade), o cursor à frente do navio usa o da proa
    /// e atrás o da popa (<see cref="Pick"/>); o clique dispara o que está mirando (<see cref="Active"/>).
    /// </summary>
    public class PlayerCannonInput : MonoBehaviour
    {
        [SerializeField] CannonBattery port;
        [SerializeField] CannonBattery starboard;
        [Tooltip("Segundos que a câmera continua no alvo depois do disparo, para ver onde as balas caem.")]
        [SerializeField] float holdFramingAfterFire = 1.5f;

        [Tooltip("Armada, a câmera enquadra o navio e um ponto no través a esta fração do alcance máximo.")]
        [SerializeField, Range(0.2f, 1f)] float framingReach = 0.8f;
        [Tooltip("Cursor a menos disto (fração da tela) da borda: a câmera desliza para aquele lado.")]
        [SerializeField, Range(0f, 0.2f)] float edgeZone = 0.06f;
        [Tooltip("Metros por segundo que o enquadramento desliza com o cursor na borda.")]
        [SerializeField] float edgePanSpeed = 70f;

        // Deslize do enquadramento pela borda da tela, no referencial do través (z = para fora do bordo).
        Vector3 framePan;

        ICameraAimFocus cameraFocus;
        ShipController ship;
        IInteraction[] interactions;
        Vector3 lastImpact;
        float holdUntil = -1f;
        CannonBattery armed, active, bow, stern;
        int armedFrame = -1;

        /// <summary>Bordo com a mira armada (null = nenhum).</summary>
        public CannonBattery Armed => armed;
        /// <summary>O canhão que o cursor está usando agora: o bordo armado ou o de caça da proa/popa.</summary>
        public CannonBattery Active => active;
        /// <summary>O cursor está no mar (sem isso, a mira fica no último ponto).</summary>
        public bool CursorOnSea { get; private set; }
        /// <summary>Testes: o "cursor" fica neste ponto do mar (null = o mouse).</summary>
        public Vector3? CursorOverride { get; set; }
        /// <summary>Testes: posição do cursor na tela (0–1) para o deslize da borda (null = o mouse).</summary>
        public Vector2? EdgeOverride { get; set; }

        public void Configure(CannonBattery portBattery, CannonBattery starboardBattery)
        {
            port = portBattery;
            starboard = starboardBattery;
        }

        void OnEnable()
        {
            // Desligado (capitão em terra), não ouve a troca de casco: confere ao voltar quais canhões estão ligados
            // (e acha os de caça, que não vêm pelo Configure).
            FindBatteries();
            if (port) port.Fired += OnPortFired;
            if (starboard) starboard.Fired += OnStarboardFired;
            if (ship || TryGetComponent(out ship)) ship.HullChanged += OnHullChanged;
        }

        void OnDisable()
        {
            Disarm();
            if (cameraFocus != null)
            {
                cameraFocus.ClearAimPoint();
                cameraFocus.SetAimHeading(null);
            }
            if (port) port.Fired -= OnPortFired;
            if (starboard) starboard.Fired -= OnStarboardFired;
            if (ship) ship.HullChanged -= OnHullChanged;
        }

        void OnHullChanged()
        {
            Disarm();
            if (port) port.Fired -= OnPortFired;
            if (starboard) starboard.Fired -= OnStarboardFired;
            FindBatteries();
            if (port) port.Fired += OnPortFired;
            if (starboard) starboard.Fired += OnStarboardFired;
        }

        /// <summary>Os canhões ligados agora (os do casco antigo ficam desligados junto com ele).</summary>
        void FindBatteries()
        {
            port = starboard = bow = stern = null;
            foreach (var battery in GetComponentsInChildren<CannonBattery>())
                switch (battery.Side)
                {
                    case BroadsideSide.Port: port = battery; break;
                    case BroadsideSide.Starboard: starboard = battery; break;
                    case BroadsideSide.Bow: bow = battery; break;
                    case BroadsideSide.Stern: stern = battery; break;
                }
        }

        /// <summary>
        /// Qual canhão o cursor usa: à frente do navio (até o giro do canhão da proa) o da proa, atrás o da popa, se montados;
        /// senão o bordo armado. Um pouco de folga para o que já está mirando, para não ficar trocando na borda.
        /// </summary>
        CannonBattery Pick(Vector3 point)
        {
            Transform t = ship ? ship.transform : transform;
            Vector3 to = Vector3.ProjectOnPlane(point - t.position, Vector3.up);
            if (to.sqrMagnitude < 1f) return active ? active : armed;
            float fromBow = Vector3.Angle(Vector3.ProjectOnPlane(t.forward, Vector3.up), to);
            if (Covers(bow, fromBow)) return bow;
            if (Covers(stern, 180f - fromBow)) return stern;
            return armed;
        }

        bool Covers(CannonBattery chaser, float angle) =>
            chaser && chaser.Mounted && angle <= chaser.MaxAimTraverse + (chaser == active ? 3f : 0f);

        void SetActive(CannonBattery battery)
        {
            if (active == battery) return;
            if (active)
            {
                active.CancelAim();
                active.ClearManualAim();
            }
            active = battery;
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
            if (WindowOpen())
            {
                Disarm();
                return;
            }
            if (input.FirePort.WasPressedThisFrame()) Toggle(port);
            if (input.FireStarboard.WasPressedThisFrame()) Toggle(starboard);
            if (!armed) return;
            if (input.Cancel.WasPressedThisFrame())
            {
                Disarm();
                return;
            }
            // O ponto antes do BeginAim: a mira já nasce no cursor (e a assistência procura perto dele).
            Vector3 point = CursorOverride ?? default;
            CursorOnSea = CursorOverride.HasValue || TryCursorOnSea(out point);
            if (CursorOnSea) SetActive(Pick(point));
            else if (!active) SetActive(armed);
            if (CursorOnSea) active.SetManualAim(point);
            if (active.State == BatteryState.Ready) active.BeginAim();
            // O clique do quadro que armou não dispara (no controle o gatilho é o mesmo).
            if (input.Attack.WasPressedThisFrame() && Time.frameCount != armedFrame && active.State == BatteryState.Aiming)
                active.ReleaseAndFire();
        }

        /// <summary>Q/E: arma aquele bordo; o mesmo de novo desarma; o outro troca.</summary>
        public void Toggle(CannonBattery battery)
        {
            if (!battery) return;
            bool same = armed == battery;
            Disarm();
            if (same) return;
            armed = battery;
            armedFrame = Time.frameCount;
        }

        /// <summary>
        /// Cursor na borda da tela: o ponto enquadrado desliza para aquele lado (no plano do mar, pelos eixos da câmera), até o
        /// arco dos canhões (alcance máximo + uma folga). Volta ao través ao desarmar.
        /// </summary>
        void EdgePan(Vector3 beam, float maxRange)
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (!cam) return;
            float x, y;
            if (EdgeOverride.HasValue) (x, y) = (EdgeOverride.Value.x, EdgeOverride.Value.y);
            else
            {
                if (mouse == null || CursorOverride.HasValue) return;
                Vector2 m = PointerLock.Position(mouse.position.ReadValue());
                x = m.x / Mathf.Max(1, Screen.width);
                y = m.y / Mathf.Max(1, Screen.height);
            }
            if (x < 0f || x > 1f || y < 0f || y > 1f) return;   // mouse fora da janela
            float dx = x < edgeZone ? -1f : x > 1f - edgeZone ? 1f : 0f;
            float dy = y < edgeZone ? -1f : y > 1f - edgeZone ? 1f : 0f;
            if (dx == 0f && dy == 0f) return;
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            // O deslize fica guardado no referencial do través: virando o navio, o enquadramento gira junto.
            var toBeam = Quaternion.LookRotation(beam, Vector3.up);
            Vector3 pan = toBeam * framePan + (right * dx + forward * dy).normalized * edgePanSpeed * Time.unscaledDeltaTime;
            // O ponto enquadrado não passa do alcance + 20% nem cai para trás do través.
            Vector3 point = beam * maxRange * framingReach + pan;
            float along = Vector3.Dot(point, beam);
            if (along < maxRange * 0.3f) point += beam * (maxRange * 0.3f - along);
            point = Vector3.ClampMagnitude(point, maxRange * 1.2f);
            framePan = Quaternion.Inverse(toBeam) * (point - beam * maxRange * framingReach);
        }

        public void Disarm()
        {
            framePan = Vector3.zero;
            SetActive(null);
            if (!armed) return;
            armed.CancelAim();
            armed.ClearManualAim();
            armed = null;
            CursorOnSea = false;
        }

        /// <summary>Janela do navio (destroços, porto da 05) ou painel aberto: o mouse é deles.</summary>
        bool WindowOpen()
        {
            if (ScreenPanel.Open) return true;
            interactions ??= GetComponents<IInteraction>();
            for (int i = 0; i < interactions.Length; i++)
                if (interactions[i] is not INoWindowInteraction && interactions[i].IsOpen) return true;
            return false;
        }

        /// <summary>Ponto do mar sob o cursor (plano na altura da água, sem acertar navio nem ilha: a mira é no mar).</summary>
        static bool TryCursorOnSea(out Vector3 point)
        {
            point = default;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || !cam) return false;
            var ray = cam.ScreenPointToRay(PointerLock.Position(mouse.position.ReadValue()));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter) || enter > 2000f) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        // LateUpdate: roda depois de CannonBattery.Update ter calculado o impacto previsto.
        void LateUpdate()
        {
            if (cameraFocus == null && !Services.TryGet(out cameraFocus)) return;
            if (armed)
            {
                // Um ponto no través do bordo armado, que só se move quando o cursor encosta na borda da tela (como num jogo de
                // estratégia): no meio da tela o enquadramento não depende do cursor. Enquadrar o arco inteiro sempre (120 m a ±35°)
                // levava a câmera a ~200 m e os navios sumiam na névoa.
                Transform t = ship ? ship.transform : transform;
                Vector3 beam = Vector3.ProjectOnPlane(t.right * (int)armed.Side, Vector3.up).normalized;
                EdgePan(beam, armed.MaxRange);
                cameraFocus.SetAimPoint(t.position + beam * armed.MaxRange * framingReach
                    + Quaternion.LookRotation(beam, Vector3.up) * framePan);
                // A câmera gira devagar até a proa ficar para cima da tela: o través (navio e alvo) fica de lado, na largura da tela.
                Vector3 bow = Vector3.ProjectOnPlane(t.forward, Vector3.up);
                cameraFocus.SetAimHeading(bow.sqrMagnitude > 0.01f ? Mathf.Atan2(bow.x, bow.z) * Mathf.Rad2Deg : null);
            }
            else
            {
                cameraFocus.SetAimHeading(null);
                if (Time.time < holdUntil) cameraFocus.SetAimPoint(lastImpact);
                else cameraFocus.ClearAimPoint();
            }
        }
    }
}
