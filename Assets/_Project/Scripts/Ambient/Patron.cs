using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Freguês sentado (13/11, interiores: a taverna). Toca as animações de gente sentada do Mixamo (o Jhonata baixou várias: parado de
    /// três jeitos, conversando de dois, bebendo, comemorando) e troca de uma para outra de tempos em tempos, cada um no seu ritmo, para
    /// a taverna não ficar com todo mundo igual. Olha para o capitão quando ele passa perto. Some com a câmera longe (o Animator para).
    /// Os personagens do kit são pequenos (quadril a ~0,38 m em pé): a altura do banco e o lugar do corpo vêm do gerador, medidos na pose.
    /// </summary>
    public class Patron : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [Tooltip("Estados do Animator que este freguês toca (sorteados).")]
        [SerializeField] string[] states = { "Sentado_Parado1" };
        [Tooltip("Segundos até trocar de animação (mín./máx.).")]
        [SerializeField] Vector2 switchSeconds = new(14f, 32f);
        [SerializeField] float blendSeconds = 0.8f;
        [Tooltip("Olha para o capitão a esta distância (m).")]
        [SerializeField] float lookRadius = 4f;

        int current = -1;
        float nextSwitch;

        public void Configure(Animator patronAnimator, string[] patronStates)
        {
            animator = patronAnimator;
            states = patronStates;
        }

        void Start()
        {
            if (!animator || states == null || states.Length == 0) { enabled = false; return; }
            var look = animator.GetComponent<Core.HeadLook>();
            if (!look) look = animator.gameObject.AddComponent<Core.HeadLook>();
            look.SetAutoRadius(lookRadius);
            animator.speed = Random.Range(0.9f, 1.1f);
            current = Random.Range(0, states.Length);
            animator.Play(states[current], 0, Random.value);
            nextSwitch = Time.time + Random.Range(switchSeconds.x, switchSeconds.y);
        }

        void Update()
        {
            if (Time.time < nextSwitch || states.Length < 2) return;
            nextSwitch = Time.time + Random.Range(switchSeconds.x, switchSeconds.y);
            int pick = Random.Range(0, states.Length - 1);
            if (pick >= current) pick++;
            current = pick;
            animator.CrossFadeInFixedTime(states[current], blendSeconds);
        }
    }
}
