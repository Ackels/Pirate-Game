using Pirates.CameraSystem;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// O "ouvido" do jogo (o único AudioListener): fica no alvo da câmera (navio ou personagem), virado como a câmera.
    /// Com o ouvido na câmera, que fica a até 75 m do navio, os sons do próprio navio e do personagem ficariam baixos e
    /// os lados (esquerda/direita) mudariam com o zoom.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    [DefaultExecutionOrder(110)]
    public class ListenerAnchor : MonoBehaviour
    {
        /// <summary>O ouvido da cena (null se não há): os sons de ambiente só tocam perto dele.</summary>
        public static ListenerAnchor Current { get; private set; }

        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }

        [SerializeField] ElevatedCameraRig rig;
        [Tooltip("Altura (m) acima do pivô do alvo (a cabeça do personagem, o convés do navio).")]
        [SerializeField] float height = 1.6f;

        public void Configure(ElevatedCameraRig cameraRig) => rig = cameraRig;

        void LateUpdate()
        {
            if (!rig) return;
            var target = rig.Target;
            var cam = rig.transform;
            Vector3 forward = cam.forward;
            forward.y = 0f;
            var rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            transform.SetPositionAndRotation(target ? target.position + Vector3.up * height : cam.position, rotation);
        }
    }
}
