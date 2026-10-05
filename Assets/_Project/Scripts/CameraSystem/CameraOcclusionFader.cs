using UnityEngine;

namespace Pirates.CameraSystem
{
    /// <summary>
    /// Fica na câmera (com o ElevatedCameraRig): a cada frame, o que for CameraOccluder e estiver entre a câmera e o alvo
    /// (pés e cabeça) ou em volta da câmera fica translúcido aos poucos; saiu da frente, volta ao normal. O teste é pela
    /// caixa (bounds) de cada um, sem física: copas sem colisor e fumaça também contam.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ElevatedCameraRig))]
    public class CameraOcclusionFader : MonoBehaviour
    {
        [Tooltip("Opacidade do que tapa o alvo (0 = some, 1 = normal).")]
        [SerializeField, Range(0f, 1f)] float fadedAlpha = 0.3f;
        [Tooltip("Velocidade da transição (opacidade por segundo).")]
        [SerializeField] float fadeSpeed = 4f;
        [Tooltip("Só olha o que está até esta distância (m) da câmera.")]
        [SerializeField] float range = 90f;
        [Tooltip("Pontos do alvo que têm de ficar à vista (acima do pivô): pés e cabeça.")]
        [SerializeField] float[] targetHeights = { 0.4f, 1.6f };
        [Tooltip("Folga (m) perto do alvo: o que só encosta nele (o chão debaixo da árvore) não conta.")]
        [SerializeField] float targetMargin = 0.4f;
        [Tooltip("Folga (m) em volta de cada objeto: o personagem tem largura, não é uma linha.")]
        [SerializeField] float padding = 0.45f;

        ElevatedCameraRig rig;

        void Awake() => rig = GetComponent<ElevatedCameraRig>();

        void LateUpdate()
        {
            var target = rig.Target;
            Vector3 eye = transform.position;
            float step = fadeSpeed * Time.deltaTime;
            float range2 = range * range;
            var occluders = CameraOccluder.Active;
            for (int k = occluders.Count - 1; k >= 0; k--)
            {
                var occluder = occluders[k];
                bool hide = false;
                if (target)
                {
                    var b = occluder.Bounds;
                    b.Expand(padding * 2f);
                    if ((b.center - eye).sqrMagnitude <= range2 + b.extents.sqrMagnitude)
                        hide = b.Contains(eye) || Blocks(b, eye, target.position);
                }
                occluder.FadeTowards(hide ? fadedAlpha : 1f, step);
            }
        }

        bool Blocks(Bounds b, Vector3 eye, Vector3 feet)
        {
            foreach (float h in targetHeights)
            {
                Vector3 to = feet + Vector3.up * h - eye;
                float length = to.magnitude;
                if (length < 0.01f) continue;
                if (b.IntersectRay(new Ray(eye, to / length), out float d) && d < length - targetMargin) return true;
            }
            return false;
        }
    }
}
