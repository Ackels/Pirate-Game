using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>Onde a câmera está olhando, no nível do mar (riscos de vento, chuva e raios nascem em volta dele).</summary>
    static class ViewFocus
    {
        /// <summary>Ponto no nível do mar à frente da câmera; <paramref name="reach"/> = distância da câmera até ele (12–70 m).</summary>
        public static Vector3 Of(Camera cam, out float reach)
        {
            var t = cam.transform;
            var focus = t.forward.y < -0.05f ? t.position + t.forward * (-t.position.y / t.forward.y) : t.position + t.forward * 30f;
            reach = Mathf.Clamp(Vector3.Distance(t.position, focus), 12f, 70f);
            return focus;
        }
    }
}
