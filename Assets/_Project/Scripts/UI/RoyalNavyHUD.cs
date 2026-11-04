using System.Collections.Generic;
using Pirates.Combat;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// A Esquadra Real no HUD (G, passo 3, 03/11): o nome sobre cada navio da Coroa perto da câmera (azul-claro; "indo embora" quando
    /// desistiu) e avisos quando chega, quando um afunda e quando desiste.
    /// </summary>
    public class RoyalNavyHUD : MonoBehaviour
    {
        static readonly Color CrownColor = new(0.55f, 0.75f, 1f);
        static readonly Color AlarmColor = new(1f, 0.45f, 0.35f);
        static readonly Color CalmColor = new(0.75f, 0.9f, 0.7f);

        [SerializeField] float heightOffset = 11f;
        [SerializeField] float showWithin = 260f;

        WorldLabelPool labels;
        readonly Dictionary<GameObject, string> leaving = new();

        void OnEnable()
        {
            RoyalNavy.Arrived += OnArrived;
            RoyalNavy.ShipSunk += OnSunk;
            RoyalNavy.GaveUp += OnGaveUp;
            RoyalNavy.TrailLost += OnTrailLost;
            RoyalNavy.Spotted += OnSpotted;
        }

        void OnDisable()
        {
            RoyalNavy.Arrived -= OnArrived;
            RoyalNavy.ShipSunk -= OnSunk;
            RoyalNavy.GaveUp -= OnGaveUp;
            RoyalNavy.TrailLost -= OnTrailLost;
            RoyalNavy.Spotted -= OnSpotted;
            labels?.HideAll();
        }

        void LateUpdate()
        {
            var navy = RoyalNavy.Current;
            if (!navy) return;
            if (labels == null)
            {
                var hud = GameHud.Current;
                if (!hud || !hud.EnsureBuilt()) return;
                labels = new WorldLabelPool(hud.World);
            }
            if (!labels.Begin()) return;
            var cam = Camera.main;
            for (int i = 0; i < navy.Count; i++)
            {
                var ship = navy.ShipAt(i);
                if (!ship) continue;
                if (cam && (ship.transform.position - cam.transform.position).sqrMagnitude > showWithin * showWithin) continue;
                string label = navy.LabelOf(ship);
                if (navy.LeavingAt(i)) label = Leaving(ship, label);
                labels.Place(ship.transform.position + Vector3.up * heightOffset, label, CrownColor);
            }
            labels.End();
        }

        /// <summary>"Nome (indo embora)", montado uma vez por navio.</summary>
        string Leaving(GameObject ship, string label)
        {
            if (!leaving.TryGetValue(ship, out var t)) leaving[ship] = t = label + " (indo embora)";
            return t;
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        static void OnArrived(IReadOnlyList<string> ships)
        {
            string list = ships.Count == 1 ? ships[0] : string.Join(", ", ships);
            Feed($"<b>A Esquadra Real chegou!</b>\n<size=75%>{list} · procurando perto do crime; saia da vista para despistar</size>", AlarmColor);
        }

        static void OnSunk(GameObject ship, bool byPlayer)
        {
            if (byPlayer) Feed("<b>Navio da Coroa afundado</b>\n<size=75%>a procura da Coroa sobe</size>", AlarmColor);
        }

        static void OnTrailLost() =>
            Feed("<b>Você despistou a Esquadra Real</b>\n<size=75%>eles procuram onde você foi visto · a procura cai mais rápido</size>", CalmColor);

        static void OnSpotted() => Feed("<b>A Esquadra Real te avistou!</b>\n<size=75%>saia da vista dos navios da Coroa</size>", AlarmColor);

        static void OnGaveUp() => Feed("<b>A Esquadra Real desistiu</b>\n<size=75%>os navios da Coroa voltam para a base</size>", CalmColor);
    }
}
