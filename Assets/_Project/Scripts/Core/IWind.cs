using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Vento (M11, clima): o sistema de vento (Ambient/WindSystem) registra no <see cref="Services"/>. Navios (velocidade pela vela),
    /// fumaça, espuma do mar, HUD e sandbox leem por aqui, sem conhecer o clima.
    /// </summary>
    public interface IWind
    {
        /// <summary>Para onde o vento sopra (horizontal, normalizado).</summary>
        Vector3 Direction { get; }
        /// <summary>Direção em graus (0 = soprando para +Z, 90 = para +X).</summary>
        float Degrees { get; }
        /// <summary>0 = calmaria, 1 = vento forte.</summary>
        float Strength { get; }
        /// <summary>
        /// Velocidade de um navio com a proa em <paramref name="forward"/> × isto: vento pelo través ou por trás empurra (&gt; 1), de
        /// bolina e contra segura (&lt; 1). Com pouco vento a diferença é menor.
        /// </summary>
        float SailFactor(Vector3 forward);
        /// <summary>Sandbox fixou direção ou força (o resto continua mudando sozinho).</summary>
        bool Overridden { get; }
        /// <summary>Sandbox: fixa a direção (graus) e/ou a força; null devolve aquela parte ao vento que muda sozinho.</summary>
        void SetOverride(float? degrees, float? strength);
    }
}
