using System.Collections.Generic;
using Pirates.Combat;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// Mercantes no HUD (G, passo 1, 03/11): o nome sobre cada mercante perto da câmera (azul; "fugindo" quando foge) e avisos quando o
    /// jogador ataca um e quando um afunda pelas mãos dele.
    /// </summary>
    public class MerchantHUD : MonoBehaviour
    {
        static readonly Color MerchantColor = new(0.62f, 0.78f, 1f);
        static readonly Color PiracyColor = new(1f, 0.55f, 0.35f);

        [SerializeField] float heightOffset = 9f;
        [SerializeField] float showWithin = 220f;

        WorldLabelPool labels;
        readonly Dictionary<MerchantShip, string> fleeing = new();

        void OnEnable()
        {
            MerchantShip.AttackedByPlayer += OnAttacked;
            MerchantShip.Sunk += OnSunk;
        }

        void OnDisable()
        {
            MerchantShip.AttackedByPlayer -= OnAttacked;
            MerchantShip.Sunk -= OnSunk;
            labels?.HideAll();
        }

        void LateUpdate()
        {
            if (labels == null)
            {
                var hud = GameHud.Current;
                if (!hud || !hud.EnsureBuilt()) return;
                labels = new WorldLabelPool(hud.World);
            }
            if (!labels.Begin()) return;
            var cam = Camera.main;
            var list = MerchantShip.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m || m.IsDead) continue;
                if (cam && (m.transform.position - cam.transform.position).sqrMagnitude > showWithin * showWithin) continue;
                string text = m.Current == MerchantShip.Mode.Fleeing ? Fleeing(m) : m.DisplayName;
                labels.Place(m.transform.position + Vector3.up * heightOffset, text, MerchantColor);
            }
            labels.End();
        }

        /// <summary>"Nome (fugindo)", montado uma vez por navio.</summary>
        string Fleeing(MerchantShip m)
        {
            if (!fleeing.TryGetValue(m, out var t)) fleeing[m] = t = m.DisplayName + " (fugindo)";
            return t;
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        static void OnAttacked(MerchantShip m) =>
            Feed($"<b>Você atacou o {m.DisplayName}!</b>\n<size=75%>atacar mercantes é pirataria: a Coroa não vai gostar</size>", PiracyColor);

        static void OnSunk(MerchantShip m, bool byPlayer)
        {
            if (byPlayer) Feed($"<b>{m.DisplayName} afundado</b>\n<size=75%>a carga está nos destroços</size>", PiracyColor);
        }
    }
}
