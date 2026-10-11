using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Marcador sobre um lugar do mundo (estrela, nome e distância) na camada World do HUD; fora da tela (ou atrás da câmera), preso
    /// na borda com uma seta apontando para ele. Usado pelas missões e pelo tutorial. Só mexe no texto quando muda.
    /// </summary>
    sealed class WorldPointer
    {
        readonly VisualElement root, arrow;
        readonly Label text;
        CachedText line;
        float angle = float.NaN;

        public WorldPointer(GameHud hud, string extraClass = null)
        {
            root = Hud.Box("hud-marker");
            if (extraClass != null) root.AddToClassList(extraClass);
            root.Add(Hud.Box("hud-marker__star"));
            text = Hud.Text("", "hud-marker__text");
            root.Add(text);
            arrow = Hud.Box("hud-marker__arrow");
            root.Add(arrow);
            hud.World.Add(root);
        }

        public void Hide() => Hud.Show(root, false);

        /// <summary>Põe o marcador sobre <paramref name="world"/> com "nome  N m".</summary>
        public void Place(GameHud hud, Camera cam, Vector3 world, string name, int meters)
        {
            const float margin = 80f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0f;
            if (behind) sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, -sp.z);
            bool inside = !behind && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
            float newAngle = float.NaN;
            if (!inside)
            {
                // Empurra para a borda na direção do destino (a partir do centro da tela).
                Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 dir = new Vector2(sp.x, sp.y) - center;
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                float scale = Mathf.Min((Screen.width * 0.5f - margin) / Mathf.Max(1f, Mathf.Abs(dir.x)),
                                        (Screen.height * 0.5f - margin) / Mathf.Max(1f, Mathf.Abs(dir.y)));
                // Atrás da câmera: sempre na borda (a projeção invertida pode cair no meio da tela).
                Vector2 edge = center + dir * (behind ? scale : Mathf.Min(1f, scale));
                sp = new Vector3(edge.x, edge.y, 1f);
                // A seta do pacote aponta para cima; a tela do painel tem y para baixo (girar positivo = horário).
                newAngle = Mathf.Round(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
            }

            if (line.Stale(name, meters)) line.Text = $"{name}  <size=80%>{meters} m</size>";
            Hud.Set(text, line.Text);
            Vector2 p = RuntimePanelUtils.ScreenToPanel(hud.World.panel, new Vector2(sp.x, Screen.height - sp.y));
            // Preso na borda: o texto inteiro fica na tela (a largura é a do quadro anterior).
            Vector2 half = root.layout.size * 0.5f;
            Vector2 area = hud.World.layout.size;
            if (!inside && half.x > 0f && area.x > 0f)
            {
                p.x = Mathf.Clamp(p.x, half.x + 10f, area.x - half.x - 10f);
                p.y = Mathf.Clamp(p.y, half.y + 34f, area.y - half.y - 10f);
            }
            root.style.left = p.x;
            root.style.top = p.y;
            Hud.Show(root, true);
            bool showArrow = !float.IsNaN(newAngle);
            Hud.Show(arrow, showArrow);
            if (showArrow && newAngle != angle)
            {
                angle = newAngle;
                arrow.style.rotate = new Rotate(newAngle);
            }
        }
    }
}
