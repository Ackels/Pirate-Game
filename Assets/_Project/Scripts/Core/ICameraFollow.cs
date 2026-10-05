using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Troca o alvo da câmera (navio ↔ personagem a pé) e a faixa de zoom adequada a ele,
    /// sem quem troca precisar conhecer a câmera.
    /// </summary>
    public interface ICameraFollow
    {
        void Follow(Transform target, float minDistance, float maxDistance, float startDistance);

        /// <summary>Pula direto para o alvo, sem a câmera "voar" até ele (ex.: o navio foi teleportado ao carregar o jogo).</summary>
        void SnapToTarget();

        /// <summary>Gira a câmera (suave) para olhar na direção <paramref name="yaw"/> (graus): ex.: para a terra ao desembarcar.</summary>
        void LookToward(float yaw);
    }
}
