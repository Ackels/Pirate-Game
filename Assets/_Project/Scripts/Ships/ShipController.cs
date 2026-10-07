using System;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Ships
{
    /// <summary>
    /// Navegação arcade com base física: velas em níveis definem a velocidade-alvo,
    /// leme com inércia, quilha segura a derrapagem e o casco inclina nas curvas.
    /// Não sabe quem pilota: lê um IShipInput do mesmo GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Buoyancy))]
    public class ShipController : MonoBehaviour
    {
        const float MetersPerSecondToKnots = 1.943844f;

        [SerializeField] ShipStats stats;

        Rigidbody body;
        Buoyancy buoyancy;
        IShipInput input;
        int sailIndex;
        float steerInput;

        public event Action<int> SailChanged;
        /// <summary>
        /// O casco mudou (navio do jogador trocou de classe: modelo, canhões, colisor). Quem guardou peças ou medidas do navio
        /// (canhões no HUD e no controle, medidas dos efeitos) busca de novo.
        /// </summary>
        public event Action HullChanged;

        public ShipStats Stats => stats;
        public int SailIndex => sailIndex;
        public string SailLabel => stats.sailLevels[sailIndex].label;
        public float Throttle => stats.sailLevels[sailIndex].throttle;
        public float Rudder { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SpeedKnots => Mathf.Abs(ForwardSpeed) * MetersPerSecondToKnots;

        public void Configure(ShipStats shipStats) => stats = shipStats;

        /// <summary>Velocidade máxima × isto (velas rasgadas por bala de corrente: menor que 1). Não mexe no ShipStats.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// Vento na vela (M11): velocidade máxima × isto, pelo ângulo entre a proa e o vento (<see cref="IWind"/>); 1 sem vento na cena ou
        /// com as velas recolhidas/de ré. Vale igual para o jogador e a IA.
        /// </summary>
        public float WindFactor { get; private set; } = 1f;

        public void NotifyHullChanged() => HullChanged?.Invoke();

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            buoyancy = GetComponent<Buoyancy>();
            input = GetComponent<IShipInput>();

            if (!stats)
            {
                Debug.LogError("[ShipController] ShipStats não atribuído.", this);
                enabled = false;
                return;
            }
            body.centerOfMass = stats.centerOfMass;
            sailIndex = Mathf.Clamp(stats.startingSailIndex, 0, stats.sailLevels.Length - 1);
        }

        void Update()
        {
            if (input == null) return;
            ShipCommand command = input.GetCommand();
            steerInput = Mathf.Clamp(command.steer, -1f, 1f);
            if (command.sailDelta != 0) SetSail(sailIndex + command.sailDelta);
        }

        /// <summary>Nível de vela com as velas recolhidas (navio parado).</summary>
        public int FurledSailIndex
        {
            get
            {
                var levels = stats.sailLevels;
                for (int i = 0; i < levels.Length; i++)
                    if (Mathf.Approximately(levels[i].throttle, 0f)) return i;
                return 0;
            }
        }

        /// <summary>Põe o navio em outro lugar, parado e com as velas recolhidas (sandbox, carregar o jogo).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!body) body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            ForwardSpeed = 0f;
            Rudder = 0f;
            SetSail(FurledSailIndex);
        }

        public void SetSail(int index)
        {
            index = Mathf.Clamp(index, 0, stats.sailLevels.Length - 1);
            if (index == sailIndex) return;
            sailIndex = index;
            SailChanged?.Invoke(sailIndex);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Rudder = Mathf.MoveTowards(Rudder, steerInput, dt / Mathf.Max(0.01f, stats.rudderResponseTime));

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 velocity = body.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, forward);
            WindFactor = Throttle > 0f && Services.TryGet(out IWind wind) ? wind.SailFactor(forward) : 1f;

            // Fora da água (ex.: saltando uma onda) velas e leme não têm efeito.
            float grip = Mathf.Clamp01(buoyancy.SubmergedRatio * 2f);
            if (grip <= 0.01f) return;

            // Propulsão: converge para a velocidade-alvo das velas.
            float speedError = Throttle * stats.maxSpeed * SpeedMultiplier * WindFactor - ForwardSpeed;
            float accel = speedError > 0f
                ? Mathf.Min(speedError * stats.speedResponse, stats.acceleration)
                : Mathf.Max(speedError * stats.speedResponse, -stats.deceleration);
            body.AddForce(forward * (accel * grip), ForceMode.Acceleration);

            // Quilha: remove a velocidade lateral.
            float lateralSpeed = Vector3.Dot(velocity, right);
            body.AddForce(-right * (lateralSpeed * stats.lateralGrip * grip), ForceMode.Acceleration);

            // Leme: gira para o lado do leme, mais eficiente em velocidade de cruzeiro.
            float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / stats.maxSpeed);
            float steerage = stats.steerageBySpeed.Evaluate(speed01);
            float direction = ForwardSpeed < -0.1f ? -1f : 1f;
            float targetYawRate = Rudder * stats.turnRate * Mathf.Deg2Rad * steerage * direction;
            float yawError = targetYawRate - body.angularVelocity.y;
            body.AddTorque(Vector3.up * (yawError * stats.turnResponse * grip), ForceMode.Acceleration);

            // Inclinação para fora da curva.
            body.AddTorque(forward * (Rudder * speed01 * stats.heelStrength * grip), ForceMode.Acceleration);
        }
    }
}
