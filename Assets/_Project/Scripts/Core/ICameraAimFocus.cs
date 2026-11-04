using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Pede à câmera para enquadrar um ponto de mira (ex.: impacto previsto dos canhões)
    /// sem que o combate precise conhecer a câmera.
    /// </summary>
    public interface ICameraAimFocus
    {
        void SetAimPoint(Vector3 worldPoint);
        void ClearAimPoint();

        /// <summary>
        /// Enquanto houver ponto de mira, a câmera gira devagar até olhar para este rumo (graus em volta do eixo Y; ex.: a proa do navio,
        /// para o través ficar de lado na tela). Girar a câmera na mão suspende por uns segundos. Null = não gira sozinha.
        /// </summary>
        void SetAimHeading(float? yaw);
    }
}
