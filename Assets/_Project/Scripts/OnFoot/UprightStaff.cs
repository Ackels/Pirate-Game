using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Cajado em pé na mão (18/11, mago esqueleto): o modelo gira em volta da pegada até o eixo dele (o +Y local) apontar para cima, um
    /// pouco inclinado para a frente do personagem, qualquer que seja a pose da mão. Sem isso, parado (pistola baixa) a caveira do cajado
    /// arrastava no chão. Mexe só no visual: a "Boca" da mira continua presa à mão (mirando, a mão já deixa o cajado em pé).
    /// </summary>
    [DefaultExecutionOrder(80)]
    public class UprightStaff : MonoBehaviour
    {
        [Tooltip("Quanto o alto do cajado inclina para a frente do personagem (0 = reto para cima).")]
        [SerializeField, Range(0f, 0.6f)] float forwardLean = 0.18f;

        Transform character;

        void Awake()
        {
            var animator = GetComponentInParent<Animator>();
            character = animator ? animator.transform : transform.root;
        }

        void LateUpdate()
        {
            Vector3 forward = character ? character.forward : Vector3.forward;
            Vector3 up = (Vector3.up + forward * forwardLean).normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        }
    }
}
