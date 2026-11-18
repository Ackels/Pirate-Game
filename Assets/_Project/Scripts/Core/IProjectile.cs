using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Projétil que voa de verdade (18/11: bola de fogo do mago esqueleto), no lugar do tiro instantâneo da arma de fogo. Quem atira
    /// (CharacterCombat, no OnFoot) só conhece esta interface; o projétil mora no Combat.
    /// </summary>
    public interface IProjectile
    {
        /// <param name="source">Quem atirou (não leva o próprio tiro; os aliados dele também não).</param>
        /// <param name="velocity">Direção × velocidade (m/s).</param>
        /// <param name="damage">Dano no centro do estouro.</param>
        /// <param name="range">Até onde voa antes de estourar sozinho (m).</param>
        void Launch(GameObject source, Vector3 velocity, float damage, float range);
    }
}
