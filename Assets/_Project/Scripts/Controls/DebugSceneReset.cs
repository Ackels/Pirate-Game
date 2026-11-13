using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pirates.Controls
{
    /// <summary>Protótipo: F5 recarrega a cena para repetir testes (alvos afundados voltam).</summary>
    public class DebugSceneReset : MonoBehaviour
    {
        // Só no editor e em build de desenvolvimento: no jogo compilado o F5 voltava ao menu e jogava fora o que foi feito desde o último save.
        void Awake()
        {
            if (!Debug.isDebugBuild) Destroy(this);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
