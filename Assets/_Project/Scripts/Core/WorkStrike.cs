using System;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Golpe de um trabalhador do cenário (martelada do carpinteiro no estaleiro, 13/11): quem anima avisa, o som ouve (sem um conhecer o
    /// outro). <c>heavy</c> = golpe mais forte (no casco) que a martelada de acabamento; <c>material</c> = no que bate (pedra dos pedreiros do
    /// mestre de obras, metal da bigorna da forja, 13/11).
    /// </summary>
    public enum WorkMaterial { Wood, Stone, Metal }

    public static class WorkStrike
    {
        public static event Action<Vector3, bool, WorkMaterial> Struck;

        public static void Raise(Vector3 point, bool heavy, WorkMaterial material = WorkMaterial.Wood) => Struck?.Invoke(point, heavy, material);

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Struck = null;
    }
}
