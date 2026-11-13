using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// O capitão está numa zona coberta (debaixo da terra: as catacumbas da Ermida, C5, 20/10; serve para cavernas depois). Quem
    /// cuida do lugar avisa (<see cref="Set"/>); o resto ouve <see cref="Changed"/>: a luz do dia some e o fundo fica preto, sem mar,
    /// chuva, vento, raio e com outro som. Interiores de casa (13/11) dizem como é a luz e o som deles (<see cref="SetLook"/>).
    /// </summary>
    public static class CoveredArea
    {
        static readonly List<object> inside = new();
        static readonly Dictionary<object, CoveredLook> looks = new();

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

        /// <summary>A zona diz como é por dentro (interior de casa; sem isso, o jeito das catacumbas).</summary>
        public static void SetLook(object zone, CoveredLook look) => looks[zone] = look;

        /// <summary>
        /// Interior de casa que mostra o lado de fora com a hora e o clima de verdade (13/11): luz do sol/lua, céu, névoa, chuva em volta
        /// da sala e clarão do raio seguem o tempo lá fora.
        /// </summary>
        public static bool OutsideVisible(out CoveredLook look) => TryGetLook(out look) && look.outsideWeather;

        /// <summary>Luz e som do lugar coberto em que o capitão está; falso = o das catacumbas (luz fria, ar de caverna, sem música).</summary>
        public static bool TryGetLook(out CoveredLook look)
        {
            for (int i = inside.Count - 1; i >= 0; i--)
                if (looks.TryGetValue(inside[i], out look)) return true;
            look = default;
            return false;
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            inside.Clear();
            looks.Clear();
            Changed = null;
        }
    }

    /// <summary>Luz, som e música de um lugar coberto (interior de uma casa do porto, 13/11).</summary>
    [Serializable]
    public struct CoveredLook
    {
        [Tooltip("Luz de cima (o sol vira esta luz lá dentro).")]
        public Color light;
        public float intensity;
        [Tooltip("Inclinação e giro da luz de cima.")]
        public Vector2 angles;
        [Range(0f, 1f)] public float shadow;
        public Color ambient;
        [Tooltip("Toca a música do lugar (taverna: a do menu, Adventure Inn).")]
        public bool music;
        [Tooltip("Sem música própria, toca a do porto (o armazém, 13/11).")]
        public bool portMusic;
        [Tooltip("Toca o ar das catacumbas.")]
        public bool air;
        [Tooltip("Chuva de fora ouvida no telhado (0 = nada, 1 = como lá fora).")]
        [Range(0f, 1f)] public float rainOnRoof;
        [Tooltip("Cor do fundo da câmera (o que fica além do cenário do lugar; o das catacumbas é preto).")]
        public Color background;
        [Tooltip("O lado de fora segue a hora e o clima de verdade (dia, noite, chuva, tempestade); a luz de dentro só se mistura (13/11).")]
        public bool outsideWeather;
        [Tooltip("Com o lado de fora visível: quanto a luz e o ambiente do lugar entram na luz de fora (0 = só a de fora).")]
        [Range(0f, 1f)] public float indoorMix;
        [HideInInspector] public Vector3 center;
        [HideInInspector] public float radius;
        [HideInInspector] public Vector3 extents;
    }
}
