using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Procurado pela Coroa no HUD (G, passo 2, 03/11): plaquinha de madeira embaixo do relógio com "Procurado" e as estrelas (vermelhas
    /// as que valem, apagadas as outras) e, embaixo, quanto falta para cair uma e a multa do Almirante; some sem estrelas. Avisos quando
    /// sobe, desce, zera e no perdão. Só mexe no texto quando muda (os segundos, uma vez por segundo).
    /// </summary>
    public class WantedHUD : MonoBehaviour
    {
        static readonly Color WantedColor = new(1f, 0.45f, 0.35f);
        static readonly Color CalmColor = new(0.75f, 0.9f, 0.7f);

        GameHud hud;
        WantedLevel wanted;
        VisualElement card;
        readonly VisualElement[] stars = new VisualElement[5];
        Label detail;
        CachedText detailText;
        int shownStars = -1;

        void Update()
        {
            if (!wanted)
            {
                wanted = WantedLevel.Current;
                if (!wanted) return;
                wanted.StarsChanged += OnStarsChanged;
                wanted.Pardoned += OnPardoned;
            }
            if (card == null && !Build()) return;
            int n = wanted.Stars;
            Hud.Show(card, n > 0 && !Core.ScreenPanel.Open);
            if (n != shownStars)
            {
                shownStars = n;
                for (int i = 0; i < stars.Length; i++)
                {
                    Hud.Show(stars[i], i < wanted.MaxStars);
                    stars[i].EnableInClassList("hud-wanted__star--on", i < n);
                }
            }
            if (n <= 0) return;
            int seconds = Mathf.CeilToInt(wanted.SecondsToNextDrop);
            // Esquadra Real (passo 3): a caminho = quanto falta para chegar; no mar = quantos navios caçam.
            var navy = Combat.RoyalNavy.Current;
            int hunting = navy ? navy.Hunting : 0;
            int arrival = navy && hunting == 0 ? Mathf.CeilToInt(navy.NextArrival) : -1;
            // Despistar (04/11): te vendo, procurando (quanto falta para perder a pista) ou sem pista.
            int sight = hunting == 0 ? 0 : navy.SeesPlayer ? 1 : navy.IsTrailLost ? 2 : 3;
            int loseIn = sight == 3 ? Mathf.CeilToInt(navy.LoseIn) : 0;
            if (detailText.Stale(seconds, wanted.Fine, ((hunting * 1000L + arrival + 1) * 10 + sight) * 1000 + loseIn))
            {
                string ships = $"{hunting} {(hunting == 1 ? "navio" : "navios")}";
                string fleet = sight switch
                {
                    1 => $"Esquadra Real: {ships} te {(hunting == 1 ? "vê" : "veem")}! Saia da vista\n",
                    3 => $"Esquadra Real: {ships} procurando · despista em {loseIn / 60}:{loseIn % 60:00}\n",
                    2 => $"Esquadra Real perdeu sua pista (a procura cai mais rápido)\n",
                    _ => arrival >= 0 ? $"Esquadra Real chega em {arrival / 60}:{arrival % 60:00}\n" : "",
                };
                detailText.Text = $"{fleet}cai uma em {seconds / 60}:{seconds % 60:00}   ·   multa {wanted.Fine} (Almirante Teodoro)";
            }
            Hud.Set(detail, detailText.Text);
        }

        void OnDestroy()
        {
            if (!wanted) return;
            wanted.StarsChanged -= OnStarsChanged;
            wanted.Pardoned -= OnPardoned;
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Box("hud-card", "hud-card--wood", "hud-wanted");
            var row = Hud.Row("hud-wanted__row");
            row.Add(Hud.Text("Procurado", "hud-wanted__title"));
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = Hud.Box("hud-wanted__star");
                row.Add(stars[i]);
            }
            card.Add(row);
            detail = Hud.Text("", "hud-wanted__detail");
            card.Add(detail);
            card.style.display = DisplayStyle.None;
            // Embaixo da fama e do relógio, se já estão lá.
            int index = 0;
            for (int i = 0; i < hud.TopRight.childCount; i++)
                if (hud.TopRight[i].ClassListContains("hud-fame-chip") || hud.TopRight[i].ClassListContains("hud-clock")) index = i + 1;
            hud.TopRight.Insert(index, card);
            return true;
        }

        void OnDisable()
        {
            if (card != null) Hud.Show(card, false);
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, text, color);
        }

        // before = −1: o jogo carregou (sem aviso).
        void OnStarsChanged(int before, int now)
        {
            if (before < 0) return;
            if (now > before)
                Feed(now == 1
                    ? "<b>Procurado pela Coroa!</b>\n<size=75%>atacar mercantes tem preço: o Almirante Teodoro perdoa por uma multa</size>"
                    : $"<b>Procurado: {now} estrelas</b>\n<size=75%>a Coroa aumentou o preço da sua cabeça</size>", WantedColor);
            else if (now > 0)
                Feed($"<b>Procurado: {now} {(now == 1 ? "estrela" : "estrelas")}</b>\n<size=75%>a Coroa vai esquecendo</size>", CalmColor);
            else
                Feed("<b>A Coroa esqueceu você</b>\n<size=75%>ninguém procura mais o seu navio</size>", CalmColor);
        }

        static void OnPardoned(int gold) =>
            Feed($"<b>Perdoado pela Coroa</b>\n<size=75%>multa paga: {gold} de ouro</size>", CalmColor);
    }
}
