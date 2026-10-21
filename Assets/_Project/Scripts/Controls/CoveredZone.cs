using Pirates.Core;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Volume de um lugar debaixo da terra (C5, 20/10: as catacumbas da Ermida): com o capitão a pé dentro da caixa, avisa o
    /// <see cref="CoveredArea"/> (luz, céu, mar, chuva, vento e som mudam). Pela posição, não pela passagem: vale para morrer lá embaixo,
    /// carregar o jogo e o F1.
    /// </summary>
    public class CoveredZone : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("A caixa do lugar no mundo.")]
        [SerializeField] Bounds area;

        public Bounds Area => area;

        public void Configure(PlayerModeSwitcher modeSwitcher, Bounds worldArea)
        {
            switcher = modeSwitcher;
            area = worldArea;
        }

        void Update()
        {
            var character = switcher ? switcher.Character : null;
            bool inside = switcher && switcher.IsOnFoot && character && area.Contains(character.transform.position);
            CoveredArea.Set(this, inside);
        }

        void OnDisable() => CoveredArea.Set(this, false);
    }
}
