using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Alavanca da ruína (C4, 19/10): F perto (<c>LeverPuller</c>) puxa; o capitão para na frente dela (<see cref="StandPoint"/>), a mão
    /// pega o cabo e, quando puxa (<see cref="Pull"/> com atraso), o cabo deita na direção dele e o braseiro dela acende na frente do
    /// portão. Fica puxada até a ruína voltar (<see cref="RuinPuzzle.ResetAll"/>).
    /// </summary>
    public class RuinLever : MonoBehaviour
    {
        static readonly List<RuinLever> active = new();
        public static IReadOnlyList<RuinLever> Active => active;

        [SerializeField] string displayName = "Alavanca";
        [SerializeField] Transform handle;
        [Tooltip("Inclinação do cabo (graus no X local, para a frente +Z): parado, quando a mão pega e depois de puxar.")]
        [SerializeField] float fromAngle = 30f, grabAngle = 30f, toAngle = 75f, pullSeconds = 0.42f;
        [Tooltip("Do F até a mão chegar no cabo (s): se o cabo parado está em pé, ele inclina para quem puxa nesse fim.")]
        [SerializeField] float reachSeconds = 0.3f;
        [Tooltip("Onde o capitão para: distância do cabo para a frente e para a esquerda dele (a mão direita fica no cabo).")]
        [SerializeField] float standDistance = 0.6f, standSideways = 0.27f;
        [Tooltip("Dá para puxar dos dois lados (frente e trás): o cabo deita para o lado de quem puxa.")]
        [SerializeField] bool twoSided;
        [Tooltip("O braseiro que acende (fogo e luz), ligado ao puxar.")]
        [SerializeField] GameObject brazierFire;

        float pulledAt = -1f, pressedAt;
        bool moving;
        float side = 1f;

        public string DisplayName => displayName;
        public bool IsPulled { get; private set; }
        public RuinPuzzle Puzzle { get; private set; }

        /// <summary>Alguém puxou esta alavanca.</summary>
        public static event Action<RuinLever> Pulled;

        public void Configure(string name, Transform leverHandle, GameObject fire, bool bothSides = false)
        {
            displayName = name;
            handle = leverHandle;
            brazierFire = fire;
            twoSided = bothSides;
            // Dos dois lados o cabo fica em pé parado (inclinado para um lado, quem vem do outro o veria deitado para longe).
            fromAngle = bothSides ? 0f : 30f;
        }

        /// <summary>Lado de quem está em <paramref name="from"/>: +1 na frente (+Z), −1 atrás (só com <see cref="twoSided"/>).</summary>
        float SideOf(Vector3 from) => twoSided && Vector3.Dot(from - transform.position, transform.forward) < 0f ? -1f : 1f;

        /// <summary>Onde o capitão para para puxar (de frente para o cabo, com a mão direita nele), vindo de <paramref name="from"/>.</summary>
        public void StandPoint(Vector3 from, out Vector3 position, out Quaternion rotation)
        {
            float s = SideOf(from);
            Vector3 pivot = handle ? handle.position : transform.position;
            Vector3 toward = transform.forward * s;
            rotation = Quaternion.LookRotation(-toward);
            Vector3 right = rotation * Vector3.right;
            position = pivot + toward * standDistance - right * standSideways;
            position.y = from.y;
        }

        internal void Bind(RuinPuzzle puzzle) => Puzzle = puzzle;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Start() => Apply(fromAngle);

        /// <summary>Puxa: o cabo começa a deitar depois de <paramref name="delay"/> s (a mão da animação chegando), para o lado de quem está
        /// em <paramref name="from"/>. A contagem da ruína e o som saem nessa hora.</summary>
        public bool Pull(Vector3 from, float delay = 0f)
        {
            if (IsPulled) return false;
            IsPulled = true;
            side = SideOf(from);
            pressedAt = Time.time;
            pulledAt = Time.time + Mathf.Max(0f, delay);
            moving = false;
            return true;
        }

        public bool Pull() => Pull(transform.position + transform.forward);

        /// <summary>Volta para cima e apaga o braseiro (a ruína voltou ao começo).</summary>
        public void ResetLever()
        {
            IsPulled = false;
            pulledAt = -1f;
            moving = false;
            side = 1f;
            Apply(fromAngle);
            if (brazierFire) brazierFire.SetActive(false);
        }

        void Update()
        {
            if (pulledAt < 0f) return;
            if (Time.time < pulledAt)
            {
                // A mão pegou o cabo em pé: ele inclina para quem puxa (até o ângulo da pegada).
                float g = Mathf.Clamp01((Time.time - pressedAt - reachSeconds) / reachSeconds);
                Apply(Mathf.Lerp(fromAngle, grabAngle, g * g * (3f - 2f * g)));
                return;
            }
            if (!moving)
            {
                moving = true;
                Pulled?.Invoke(this);
            }
            float k = Mathf.Clamp01((Time.time - pulledAt) / pullSeconds);
            Apply(Mathf.Lerp(grabAngle, toAngle, k * k * (3f - 2f * k)));
            if (k >= 1f)
            {
                pulledAt = -1f;
                moving = false;
                if (brazierFire && !brazierFire.activeSelf) brazierFire.SetActive(true);
            }
        }

        void Apply(float angle)
        {
            if (handle) handle.localRotation = Quaternion.Euler(angle * side, 0f, 0f);
        }
    }
}
