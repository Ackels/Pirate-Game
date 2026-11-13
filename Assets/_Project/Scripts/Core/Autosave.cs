using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Salvar sozinho nos marcos (12/11): capítulo da História, chefe derrotado, coleção completa. Quem não conhece o Save pede aqui
    /// (<see cref="Request"/>); o GameSave ouve e grava quando der (fora da abordagem e com o navio inteiro). Quem não pode ser salvo no
    /// meio (a abordagem) põe um <see cref="AddBlocker">bloqueio</see>.
    /// </summary>
    public static class Autosave
    {
        public static event Action<string> Requested;
        static readonly List<Func<bool>> blockers = new();

        public static void Request(string reason) => Requested?.Invoke(reason);

        public static void AddBlocker(Func<bool> blocked)
        {
            if (blocked != null && !blockers.Contains(blocked)) blockers.Add(blocked);
        }

        public static void RemoveBlocker(Func<bool> blocked) => blockers.Remove(blocked);

        /// <summary>Alguém não quer ser salvo agora.</summary>
        public static bool Blocked
        {
            get
            {
                for (int i = 0; i < blockers.Count; i++)
                    if (blockers[i] != null && blockers[i]()) return true;
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Requested = null;
            blockers.Clear();
        }
    }
}
