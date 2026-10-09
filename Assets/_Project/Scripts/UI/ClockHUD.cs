using Pirates.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Relógio do dia (M11): plaquinha no canto de cima à direita (embaixo da fama) com um disco de sol/lua, "Dia N · HH:MM" e a parte do
    /// dia (com o tempo do <see cref="IWeather"/> quando não é céu limpo: "tarde · chuva"). Lê o <see cref="ITimeOfDay"/> do
    /// <see cref="Services"/>; sem ciclo na cena, some. O texto só muda a cada 10 min de jogo.
    /// </summary>
    public class ClockHUD : MonoBehaviour
    {
        static readonly Color SunColor = new(1f, 0.83f, 0.35f), DuskColor = new(1f, 0.55f, 0.3f), MoonColor = new(0.75f, 0.83f, 1f);

        GameHud hud;
        VisualElement card, disc;
        Label time, part;
        CachedText timeText, partText;
        int shownBand = -1;

        void Update()
        {
            bool has = Services.TryGet(out ITimeOfDay clock);
            if (card == null && (!has || !Build())) return;
            Hud.Show(card, has);
            if (!has) return;

            int tenMinutes = Mathf.FloorToInt(clock.Hour * 6f);
            if (timeText.Stale(clock.Day, tenMinutes))
                timeText.Text = $"Dia {clock.Day}  ·  {tenMinutes / 6:00}:{tenMinutes % 6 * 10:00}";
            Hud.Set(time, timeText.Text);

            // Parte do dia pela claridade (o amanhecer e o anoitecer usam a mesma faixa do ciclo).
            float night = clock.Night;
            int band = night < 0.15f ? 0 : night > 0.85f ? 2 : 1;
            bool morning = clock.Hour < 12f;
            // Clima (M11): "tarde · chuva"; céu limpo não aparece.
            int sky = 0;
            string skyLabel = null;
            if (Services.TryGet(out IWeather weather))
            {
                sky = weather.Storm > 0.5f ? 3 : weather.Rain > 0.4f ? 2 : weather.Overcast > 0.35f ? 1 : 0;
                skyLabel = weather.Label;
            }
            if (partText.Stale(band, morning ? 1 : 0, sky))
            {
                string part = band == 0 ? (clock.Hour < 12f ? "manhã" : "tarde") : band == 2 ? "noite" : morning ? "amanhecer" : "anoitecer";
                partText.Text = sky == 0 ? part : $"{part} · {skyLabel}";
            }
            Hud.Set(part, partText.Text);
            if (band != shownBand)
            {
                shownBand = band;
                disc.style.backgroundColor = band == 0 ? SunColor : band == 2 ? MoonColor : DuskColor;
            }
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Box("hud-card", "hud-card--wood", "hud-clock");
            var row = Hud.Row("hud-clock__row");
            disc = Hud.Box("hud-clock__disc");
            row.Add(disc);
            time = Hud.Text("", "hud-clock__time");
            row.Add(time);
            part = Hud.Text("", "hud-clock__part");
            row.Add(part);
            card.Add(row);
            // Embaixo da plaquinha da fama, se ela já está lá (a fama se põe no topo quando monta).
            int index = 0;
            for (int i = 0; i < hud.TopRight.childCount; i++)
                if (hud.TopRight[i].ClassListContains("hud-fame-chip")) index = i + 1;
            hud.TopRight.Insert(index, card);
            return true;
        }

        void OnDisable()
        {
            if (card != null) Hud.Show(card, false);
        }
    }
}
