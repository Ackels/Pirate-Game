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
    /// Mapa do tesouro na tela (C1, 16/10): cartão pequeno à direita, embaixo da História ("Mapa do tesouro" + "Pedaços: 2 de 3" ou o
    /// lugar do X), marcador com o X sobre o tesouro marcado (seta na borda fora da tela) e avisos quando o mapa se monta e quando o
    /// tesouro sai do chão. Só aparece com pelo menos um pedaço ou com o mapa montado; some com o menu, casas do porto, conversa e painéis.
    /// </summary>
    public class TreasureMapHUD : MonoBehaviour
    {
        [SerializeField] TreasureMaps maps;
        [SerializeField] PlayerModeSwitcher switcher;

        GameHud hud;
        VisualElement card;
        Label title, line;
        WorldPointer pointer;
        int shownVersion = -1;
        float nextPlace;
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
            if (card != null) Hud.Show(card, false);
            pointer?.Hide();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Card(paper: true);
            card.AddToClassList("hud-treasure-map");
            title = Hud.Text("Mapa do tesouro", "hud-treasure-map__title");
            line = Hud.Text("", "hud-treasure-map__line");
            card.Add(title);
            card.Add(line);
            card.style.display = DisplayStyle.None;
            hud.TopRight.Add(card);
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
            Feed($"<b>O mapa do tesouro está completo!</b>\n<size=75%>Um X marca o lugar: {spot.place}</size>", HudColors.Gold);

        void OnFound(TreasureMaps.Spot spot) => Feed($"<b>Tesouro do mapa desenterrado!</b>\n<size=75%>{spot.place}</size>", HudColors.Gold);

        void Update()
        {
            if (!maps) return;
            if (card == null && !Build()) return;
            bool busy = TitleScreen.Showing || ScreenPanel.Open || (buildings && buildings.IsOpen) || (talker && talker.IsOpen)
                        || (visitor && visitor.IsOpen);
            bool show = !busy && (maps.HasMap || maps.Pieces > 0);
            Hud.Show(card, show);
            if (!show)
            {
                pointer.Hide();
                return;
            }
            if (Time.unscaledTime >= nextPlace) Place();
            if (maps.Version != shownVersion) Refresh();
            UpdatePointer();
        }

        /// <summary>Embaixo da fama, do relógio e da História (montam em qualquer ordem).</summary>
        void Place()
        {
            nextPlace = Time.unscaledTime + 1f;
            var parent = hud.TopRight;
            int want = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent[i].ClassListContains("hud-fame-chip") || parent[i].ClassListContains("hud-clock") || parent[i].ClassListContains("hud-story"))
                    want = i + 1;
            int at = parent.IndexOf(card);
            if (at < want) want--;
            if (at != want) parent.Insert(want, card);
        }

        void Refresh()
        {
            shownVersion = maps.Version;
            Hud.Set(line, maps.HasMap
                ? $"X marcado: <b>{maps.ActiveSpot.place}</b>. Desembarque, ache o X e aperte <b>F</b> para cavar."
                : $"Pedaços: <b>{maps.Pieces} de {maps.PiecesPerMap}</b>. Caem de baús, navios grandes e chefes.");
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
