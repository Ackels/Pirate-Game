using System;
using System.Collections;
using Pirates.Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    /// <summary>
    /// Liga a vida à flutuação: com dano o casco embarca água e assenta mais fundo;
    /// ao zerar a vida, o objeto perde o controle, aderna e afunda. Serve para navios, balsas e barris.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Buoyancy), typeof(Rigidbody))]
    public class SinkOnDeath : MonoBehaviour
    {
        [Tooltip("Empuxo restante com vida quase zerada (1 = intacto). Abaixo de 0,5 afunda.")]
        [SerializeField, Range(0.5f, 1f)] float floodedBuoyancy = 0.7f;
        [SerializeField] float sinkDuration = 7f;
        [Tooltip("Aceleração angular (rad/s²) extra que aderna o casco enquanto afunda.")]
        [SerializeField] float capsizeStrength = 0.15f;
        [Tooltip("Fração do tempo de naufrágio em que um dos lados alaga por completo (afunda de bico ou tomba).")]
        [SerializeField, Range(0.1f, 1f)] float floodTimeFraction = 0.5f;
        [Tooltip("Arrasto horizontal da água no casco afundando (freia o navio que perdeu o controle).")]
        [SerializeField] float sunkHorizontalDrag = 0.6f;
        [Tooltip("Profundidade em que o objeto some da cena.")]
        [SerializeField] float removeBelowDepth = 12f;

        Health health;
        Buoyancy buoyancy;
        Rigidbody body;

        public bool IsSinking { get; private set; }

        /// <summary>Disparado quando o objeto terminou de afundar (M2.4 usa para soltar loot).</summary>
        public event Action<SinkOnDeath> Sunk;

        void Awake()
        {
            health = GetComponent<Health>();
            buoyancy = GetComponent<Buoyancy>();
            body = GetComponent<Rigidbody>();
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Healed += OnHealed;
            health.Restored += UpdateFlooding;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Healed -= OnHealed;
            health.Restored -= UpdateFlooding;
            health.Died -= OnDied;
        }

        void OnDamaged(Pirates.Core.DamageInfo _) => UpdateFlooding();

        // Casco consertado bombeia a água para fora e volta a flutuar mais alto.
        void OnHealed(float _) => UpdateFlooding();

        void UpdateFlooding()
        {
            if (IsSinking) return;
            buoyancy.BuoyancyScale = Mathf.Lerp(floodedBuoyancy, 1f, health.Normalized);
        }

        void OnDied()
        {
            if (IsSinking) return;
            IsSinking = true;

            // Navio sem tripulação: sem velas, sem leme, sem canhões. Sem o controlador nada segurava a
            // velocidade (o arrasto dos navios é 0), e o casco morto deslizava a ~15 nós; a água passa a frear.
            if (TryGetComponent(out ShipController ship)) ship.enabled = false;
            buoyancy.HorizontalDrag = Mathf.Max(buoyancy.HorizontalDrag, sunkHorizontalDrag);
            foreach (var battery in GetComponentsInChildren<CannonBattery>()) battery.enabled = false;

            StartCoroutine(Sink());
        }

        IEnumerator Sink()
        {
            float start = buoyancy.BuoyancyScale;
            // Lado que alaga primeiro: proa ou popa, puxando um pouco para um dos bordos.
            Vector3 floodSide = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.value < 0.5f ? 1f : -1f).normalized;
            // Torque que empurra o lado alagado para baixo (eixo perpendicular, no plano do casco).
            Vector3 localAxis = Vector3.Cross(Vector3.up, floodSide);

            for (float t = 0f; t < sinkDuration; t += Time.deltaTime)
            {
                float k = t / sinkDuration;
                buoyancy.SetFlooding(floodSide, k / floodTimeFraction);
                buoyancy.BuoyancyScale = Mathf.Lerp(start, 0.1f, k * k);
                body.AddTorque(transform.TransformDirection(localAxis) * (capsizeStrength * (1f - k)), ForceMode.Acceleration);
                if (transform.position.y < -removeBelowDepth) break;
                yield return null;
            }

            // Limite de tempo caso encalhe numa pedra e nunca chegue ao fundo.
            for (float t = 0f; t < 10f && transform.position.y > -removeBelowDepth; t += Time.deltaTime)
                yield return null;

            Sunk?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
