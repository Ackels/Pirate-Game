using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Loot;
using Pirates.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Mapa do tesouro na tela (C1, 16/10): marcador com o X sobre o tesouro marcado (seta na borda fora da tela) e avisos quando o mapa
    /// se monta e quando o tesouro sai do chão. Os pedaços e o lugar do X ficam na aba Missões da tela do capitão (J; 21/10, o cartão à
    /// direita saiu da tela). O marcador some com o menu, casas do porto, conversa e painéis.
    /// </summary>
    public class TreasureMapHUD : MonoBehaviour
    {
        [SerializeField] TreasureMaps maps;
        [SerializeField] PlayerModeSwitcher switcher;

        GameHud hud;
        WorldPointer pointer;
        HarborBuildingVisitor buildings;
        HarborVisitor visitor;
        NpcTalker talker;

        public void Bind(TreasureMaps treasureMaps, PlayerModeSwitcher modeSwitcher)
        {
            maps = treasureMaps;
            switcher = modeSwitcher;
        }

        void OnEnable()
        {
            if (!maps) return;
            maps.Completed += OnCompleted;
            maps.Found += OnFound;
        }

        void OnDisable()
        {
            if (maps)
            {
                maps.Completed -= OnCompleted;
                maps.Found -= OnFound;
            }
            pointer?.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            pointer = new WorldPointer(hud, "hud-marker--map");
            pointer.Hide();
            if (switcher && switcher.Character)
            {
                buildings = switcher.Character.GetComponent<HarborBuildingVisitor>();
                talker = switcher.Character.GetComponent<NpcTalker>();
            }
            if (switcher && switcher.Ship) visitor = switcher.Ship.GetComponent<HarborVisitor>();
            return true;
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        void OnCompleted(TreasureMaps.Spot spot) =>
            Feed($"<b>O mapa do tesouro está completo!</b>\n<size=75%>Um X marca o lugar: {spot.place} · J abre o diário</size>", HudColors.Gold);

        void OnFound(TreasureMaps.Spot spot) => Feed($"<b>Tesouro do mapa desenterrado!</b>\n<size=75%>{spot.place}</size>", HudColors.Gold);

        void Update()
        {
            if (!maps) return;
            if (pointer == null && !Build()) return;
            bool busy = TitleScreen.Showing || ScreenPanel.Open || (buildings && buildings.IsOpen) || (talker && talker.IsOpen)
                        || (visitor && visitor.IsOpen);
            if (busy || !maps.HasMap)
            {
                pointer.Hide();
                return;
            }
            UpdatePointer();
        }

        void UpdatePointer()
        {
            var cam = Camera.main;
            string id = maps.ActiveId;
            var marker = string.IsNullOrEmpty(id) ? null : WorldMarker.Find(id);
            if (!cam || !marker || hud.World.panel == null)
            {
                pointer.Hide();
                return;
            }
            Vector3 me = switcher && switcher.IsOnFoot ? switcher.Character.transform.position
                : switcher && switcher.Ship ? switcher.Ship.transform.position : marker.transform.position;
            Vector3 to = marker.transform.position - me;
            to.y = 0f;
            pointer.Place(hud, cam, marker.LabelPosition, marker.Label, Mathf.RoundToInt(to.magnitude));
        }
    }
}
