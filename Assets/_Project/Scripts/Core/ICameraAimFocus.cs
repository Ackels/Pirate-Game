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
    }
}
