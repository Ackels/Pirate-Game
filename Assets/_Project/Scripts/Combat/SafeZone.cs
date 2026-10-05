using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Área (círculo no mar) em que a IA inimiga não persegue nem ataca, e da qual ela se afasta.
    /// Usada em volta do porto-base: dá para fazer melhorias com calma.
    /// </summary>
    public class SafeZone : MonoBehaviour
    {
        static readonly List<SafeZone> active = new();
        public static IReadOnlyList<SafeZone> Active => active;

        [SerializeField, Min(1f)] float radius = 110f;

        public float Radius => radius;

        public void Configure(float zoneRadius) => radius = zoneRadius;

        public bool Contains(Vector3 position, float margin = 0f)
        {
            Vector3 d = position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= (radius + margin) * (radius + margin);
        }

        /// <summary>Zona que contém o ponto (null se nenhuma). <paramref name="margin"/> aumenta o raio.</summary>
        public static SafeZone At(Vector3 position, float margin = 0f)
        {
            foreach (var zone in active)
                if (zone.Contains(position, margin)) return zone;
            return null;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
