using System;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Pirates.Controls
{
    /// <summary>
    /// Roda de comando dos marujos em terra (30/10, pedido do Jhonata: "segurar algum botão que aparece uma roda de controle"): a pé, com
    /// marujos ao lado, <b>segurar T</b> abre a roda em volta do cursor (o jogo fica em câmera lenta) e o mouse escolhe a fatia; <b>soltar T</b>
    /// dá a ordem aos dois. "Ir até lá" e "Atacar alvo" pedem para apontar depois: um anel no chão segue o cursor (no pirata mais perto dele,
    /// no ataque) e o clique esquerdo confirma; botão direito, Esc ou T de novo cancelam. Fica no capitão, ao lado das outras ações a pé.
    /// </summary>
    [RequireComponent(typeof(FootCharacter))]
    public class CrewCommander : MonoBehaviour
    {
        public enum Phase { Closed, Wheel, Pointing }

        /// <summary>As fatias, em sentido horário a partir de cima.</summary>
        public static readonly CrewOrder[] Slices =
            { CrewOrder.Follow, CrewOrder.Hold, CrewOrder.MoveTo, CrewOrder.Attack, CrewOrder.Guard, CrewOrder.Passive };

        [SerializeField] CrewEscort escort;
        [Tooltip("Anel no chão enquanto aponta (criado no gerador).")]
        [SerializeField] LineRenderer marker;
        [Tooltip("Velocidade do jogo com a roda aberta ou apontando.")]
        [SerializeField, Range(0.05f, 1f)] float slowMotion = 0.3f;
        [Tooltip("Do centro da roda até onde o mouse não escolhe nada (fração da altura da tela).")]
        [SerializeField] float deadZone = 0.04f;
        [Tooltip("Raio da roda na tela (fração da altura): o centro não chega mais perto da borda que isto.")]
        [SerializeField] float wheelRadius = 0.2f;
        [Tooltip("Atacar alvo: pirata a até isto (m) do cursor.")]
        [SerializeField] float pickRadius = 3f;
        [SerializeField] Color pointColor = new(1f, 0.78f, 0.25f, 0.95f), attackColor = new(1f, 0.22f, 0.12f, 0.95f),
            invalidColor = new(0.8f, 0.8f, 0.8f, 0.5f);

        const int RingPoints = 32;
        static readonly Vector3[] ring = new Vector3[RingPoints];

        PlayerFootInput input;
        BombThrower bombs;
        Pirates.Combat.Health health;
        float slowedFrom = -1f;
        int closedFrame = -1;

        public Phase State { get; private set; }
        /// <summary>Fatia sob o mouse (−1 = no meio: soltar não dá ordem).</summary>
        public int Hover { get; private set; } = -1;
        /// <summary>Centro da roda na tela (pixels, y para cima).</summary>
        public Vector2 Center { get; private set; }
        /// <summary>A ordem sendo apontada (Ir até lá / Atacar alvo).</summary>
        public CrewOrder Pending { get; private set; }
        /// <summary>A última ordem dada (a roda mostra).</summary>
        public CrewOrder Current { get; private set; }
        /// <summary>Apontando: o lugar e o pirata sob o cursor (testes e HUD).</summary>
        public Vector3 PointTarget { get; private set; }
        public EnemyFootAI EnemyTarget { get; private set; }
        public bool PointValid { get; private set; }
        /// <summary>Roda aberta ou apontando: o mouse é dela (sem atacar nem mirar; o Esc também).</summary>
        public bool Busy => State != Phase.Closed;
        /// <summary>Busy ou fechou neste quadro: o clique que confirmou (ou o direito que cancelou) não vira golpe nem mira.</summary>
        public bool OwnsMouse => Busy || Time.frameCount == closedFrame;
        /// <summary>Raio da roda e da zona morta, em pixels da tela.</summary>
        public float RadiusPixels => wheelRadius * Screen.height;
        public float DeadPixels => deadZone * Screen.height;

        /// <summary>Ordem dada: qual e a quantos marujos.</summary>
        public event Action<CrewOrder, int> Ordered;
        /// <summary>Não deu: o motivo (HUD).</summary>
        public event Action<string> Refused;

        public void Configure(CrewEscort crewEscort, LineRenderer pointMarker)
        {
            escort = crewEscort;
            marker = pointMarker;
        }

        void Awake()
        {
            health = GetComponent<Pirates.Combat.Health>();
            input = GetComponent<PlayerFootInput>();
            bombs = GetComponent<BombThrower>();
            if (marker) marker.enabled = false;
        }

        void OnDisable()
        {
            Close();
            Current = CrewOrder.Follow;
        }

        void Update()
        {
            var keys = GameInput.Instance;
            bool window = input && input.IsWindowOpen;
            if (MatesUp() == 0 || (health && health.IsDead) || window)
            {
                if (Busy) Close();
                else if (keys.CrewWheel.WasPressedThisFrame() && !window)
                    Refused?.Invoke(escort && escort.Count > 0 ? "Os marujos estão caídos" : "Nenhum marujo desceu com você");
                return;
            }
            switch (State)
            {
                case Phase.Closed:
                    if (keys.CrewWheel.WasPressedThisFrame() && !(bombs && bombs.IsAiming)) OpenWheel(MousePosition());
                    break;
                case Phase.Wheel:
                    if (pinned) break;
                    Hover = SliceAt(MousePosition());
                    if (!keys.CrewWheel.IsPressed()) Choose(Hover);
                    break;
                case Phase.Pointing:
                    if (keys.CrewWheel.WasPressedThisFrame() || keys.Aim.WasPressedThisFrame() || keys.Cancel.WasPressedThisFrame())
                    {
                        Close();
                        break;
                    }
                    if (input && input.TryGetCursorPoint(out Vector3 point)) Aim(point);
                    if (keys.Attack.WasPressedThisFrame()) Confirm();
                    break;
            }
        }

        static Vector2 MousePosition() => Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;

        int MatesUp()
        {
            if (!escort) return 0;
            int n = 0;
            var list = escort.Crewmates;
            for (int i = 0; i < list.Count; i++)
                if (list[i] && list[i].isActiveAndEnabled && !list[i].IsDown) n++;
            return n;
        }

        // ---------- Roda ----------

        /// <summary>Abre a roda com o centro em <paramref name="screen"/> (puxado para dentro da tela). Público para os testes.</summary>
        public void OpenWheel(Vector2 screen)
        {
            float r = RadiusPixels;
            Center = new Vector2(Mathf.Clamp(screen.x, r, Mathf.Max(r, Screen.width - r)), Mathf.Clamp(screen.y, r, Mathf.Max(r, Screen.height - r)));
            Hover = -1;
            State = Phase.Wheel;
            SlowDown();
        }

        bool pinned;

        /// <summary>Testes (foto): abre a roda com a fatia <paramref name="slice"/> acesa, sem ler o T nem o mouse, até o <see cref="Close"/>.</summary>
        public void PinWheel(Vector2 screen, int slice)
        {
            OpenWheel(screen);
            pinned = true;
            Hover = slice;
        }

        /// <summary>Fatia no ponto da tela (−1 = na zona morta do meio).</summary>
        public int SliceAt(Vector2 screen)
        {
            Vector2 d = screen - Center;
            if (d.magnitude < DeadPixels) return -1;
            // 0° em cima, crescendo no sentido horário (y da tela para cima).
            float angle = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            float step = 360f / Slices.Length;
            return Mathf.RoundToInt(angle / step) % Slices.Length;
        }

        /// <summary>Soltou T na fatia <paramref name="slice"/> (−1 = fecha sem ordem). Público para os testes.</summary>
        public void Choose(int slice)
        {
            if (slice < 0 || slice >= Slices.Length)
            {
                Close();
                return;
            }
            var order = Slices[slice];
            if (order is CrewOrder.MoveTo or CrewOrder.Attack)
            {
                Pending = order;
                State = Phase.Pointing;
                PointValid = false;
                EnemyTarget = null;
                return;
            }
            Close();
            Give(order, default, null);
        }

        // ---------- Apontar ----------

        /// <summary>O cursor no chão em <paramref name="point"/>: acha o lugar ou o pirata e põe o anel. Público para os testes.</summary>
        public void Aim(Vector3 point)
        {
            PointTarget = point;
            if (Pending == CrewOrder.Attack)
            {
                EnemyTarget = EnemyNear(point);
                PointValid = EnemyTarget;
                if (EnemyTarget) DrawRing(EnemyTarget.transform.position, 0.85f, attackColor);
                else DrawRing(point, 0.6f, invalidColor);
                return;
            }
            PointValid = NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
            // Cursor na copa de uma árvore ou no alto de uma pedra: tenta o chão embaixo, na altura do capitão.
            if (!PointValid)
                PointValid = NavMesh.SamplePosition(new Vector3(point.x, transform.position.y, point.z), out hit, 3f, NavMesh.AllAreas);
            if (PointValid) PointTarget = hit.position;
            DrawRing(PointTarget, 1.1f, PointValid ? pointColor : invalidColor);
        }

        /// <summary>Clique esquerdo apontando. Público para os testes.</summary>
        public void Confirm()
        {
            if (State != Phase.Pointing) return;
            if (!PointValid)
            {
                Refused?.Invoke(Pending == CrewOrder.Attack ? "Aponte para um pirata" : "Não dá para chegar ali");
                return;
            }
            var order = Pending;
            Vector3 point = PointTarget;
            var enemy = EnemyTarget;
            Close();
            Give(order, point, enemy);
        }

        EnemyFootAI EnemyNear(Vector3 point)
        {
            EnemyFootAI best = null;
            float bestD = pickRadius * pickRadius;
            var list = EnemyFootAI.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (!ai || ai.IsDead || !ai.isActiveAndEnabled) continue;
                Vector3 d = ai.transform.position - point;
                d.y = 0f;
                if (d.sqrMagnitude < bestD)
                {
                    bestD = d.sqrMagnitude;
                    best = ai;
                }
            }
            return best;
        }

        void DrawRing(Vector3 center, float radius, Color color)
        {
            if (!marker) return;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                ring[i] = center + new Vector3(Mathf.Cos(a) * radius, 0.1f, Mathf.Sin(a) * radius);
            }
            marker.loop = true;
            marker.positionCount = RingPoints;
            marker.SetPositions(ring);
            marker.startColor = marker.endColor = color;
            marker.enabled = true;
        }

        // ---------- Ordem ----------

        /// <summary>Dá a ordem aos marujos em terra (de pé ou caídos: o caído obedece quando levantar).</summary>
        public void Give(CrewOrder order, Vector3 point, EnemyFootAI enemy)
        {
            if (!escort) return;
            int n = 0;
            var list = escort.Crewmates;
            for (int i = 0; i < list.Count; i++)
            {
                var mate = list[i];
                if (!mate || !mate.isActiveAndEnabled) continue;
                mate.Command(order, point, enemy);
                n++;
            }
            if (n == 0) return;
            if (order != CrewOrder.Attack) Current = order;
            Ordered?.Invoke(order, n);
        }

        public void Close()
        {
            if (Busy) closedFrame = Time.frameCount;
            State = Phase.Closed;
            pinned = false;
            Hover = -1;
            EnemyTarget = null;
            if (marker) marker.enabled = false;
            SpeedUp();
        }

        void SlowDown()
        {
            if (slowedFrom >= 0f || Time.timeScale <= slowMotion) return;
            slowedFrom = Time.timeScale;
            Time.timeScale = slowMotion;
        }

        /// <summary>Volta a velocidade de antes, se ninguém mexeu nela no meio (pausa, parada de impacto).</summary>
        void SpeedUp()
        {
            if (slowedFrom < 0f) return;
            if (Mathf.Approximately(Time.timeScale, slowMotion)) Time.timeScale = slowedFrom;
            slowedFrom = -1f;
        }
    }
}
