using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Alvo da mira assistida dos canhões que não é navio (a cabeça da lula gigante). Fica na lista enquanto ligado; quem o controla
    /// diz quando ele está à mostra (<see cref="Exposed"/>: a cabeça submersa não pode ser travada).
    /// </summary>
    public class AimTarget : MonoBehaviour
    {
        static readonly List<AimTarget> active = new();
        public static IReadOnlyList<AimTarget> Active => active;

        /// <summary>À mostra (pode ser mirado): a cabeça fora d'água.</summary>
        public bool Exposed { get; set; }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();
    }
}
