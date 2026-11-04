using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// Cursor do jogo no estilo pirata (04/11, pedido do Jhonata): a espada dourada do Kenney RPG UI no lugar da seta do Windows, com a
    /// ponta da lâmina como ponto do clique. No objeto do GameHUD (todas as cenas); volta a seta do sistema ao desligar.
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        [SerializeField] Texture2D cursor;
        [Tooltip("Ponto do clique, em pixels a partir do canto de cima à esquerda da imagem (a ponta da espada).")]
        [SerializeField] Vector2 hotspot = new(1f, 1f);

        public void Configure(Texture2D texture, Vector2 point)
        {
            cursor = texture;
            hotspot = point;
        }

        void OnEnable()
        {
            if (cursor) Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
        }

        void OnDisable() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
