using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Qualquer coisa que responda "qual a altura da água aqui?".
    /// Navios, boias e destroços dependem só disto, nunca da implementação do oceano.
    /// </summary>
    public interface IOceanSampler
    {
        float SeaLevel { get; }
        float GetHeight(Vector3 worldPosition);
    }
}
