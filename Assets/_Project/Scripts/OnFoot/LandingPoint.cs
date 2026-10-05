using System.Collections.Generic;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Praia de desembarque (marcada por um bote). O navio perto dela pode desembarcar o jogador no ponto em terra;
    /// a pé, perto do bote, dá para voltar ao navio.
    /// </summary>
    public class LandingPoint : MonoBehaviour
    {
        static readonly List<LandingPoint> active = new();
        public static IReadOnlyList<LandingPoint> Active => active;

        [SerializeField] string displayName = "Praia";
        [Tooltip("Onde o personagem aparece em terra (o chão é procurado logo abaixo).")]
        [SerializeField] Transform landPosition;
        [Tooltip("Distância (m) do navio a este ponto para poder desembarcar.")]
        [SerializeField] float shipRange = 35f;
        [Tooltip("Distância (m) do personagem ao bote para voltar ao navio.")]
        [SerializeField] float boardingRange = 4f;

        public string DisplayName => displayName;
        public Transform LandPosition => landPosition ? landPosition : transform;
        public float ShipRange => shipRange;
        public float BoardingRange => boardingRange;

        public void Configure(string pointName, Transform land, float shipDistance, float boardDistance)
        {
            displayName = pointName;
            landPosition = land;
            shipRange = shipDistance;
            boardingRange = boardDistance;
        }

        public float FlatDistance(Vector3 position)
        {
            Vector3 d = position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        public float BoardingDistance(Vector3 position)
        {
            Vector3 d = position - LandPosition.position;
            d.y = 0f;
            return d.magnitude;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, shipRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(LandPosition.position, boardingRange);
        }
    }
}
