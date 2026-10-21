using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// O capitão está numa zona coberta (debaixo da terra: as catacumbas da Ermida, C5, 20/10; serve para cavernas depois). Quem
    /// cuida do lugar avisa (<see cref="Set"/>); o resto ouve <see cref="Changed"/>: a luz do dia some e o fundo fica preto, sem mar,
    /// chuva, vento, raio e com outro som.
    /// </summary>
    public static class CoveredArea
    {
        static readonly List<object> inside = new();

        public static bool Inside => inside.Count > 0;
        public static event Action<bool> Changed;

        /// <summary>Uma zona diz se o capitão está dentro dela (várias zonas: coberto se alguma diz que sim).</summary>
        public static void Set(object zone, bool isInside)
        {
            bool before = Inside;
            if (isInside) { if (!inside.Contains(zone)) inside.Add(zone); }
            else inside.Remove(zone);
            if (Inside != before) Changed?.Invoke(Inside);
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            inside.Clear();
            Changed = null;
        }
    }
}
