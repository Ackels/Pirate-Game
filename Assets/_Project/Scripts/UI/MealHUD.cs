using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Pratos da taverna no HUD (D2, 30/10): uma plaquinha embaixo do relógio com o que está valendo e quanto falta ("Atum grelhado ·
    /// recarga −10% · 7:32"); some sem prato. Avisos ao comer e quando o bônus acaba.
    /// </summary>
    public class MealHUD : MonoBehaviour
    {
        static readonly Color MealColor = new(1f, 0.78f, 0.45f);

        GameHud hud;
        VisualElement card;
        Label lines;
        CachedText text;
        float nextRefresh;

        void OnEnable()
        {
            Meals.Eaten += OnEaten;
            Meals.Expired += OnExpired;
        }

        void OnDisable()
        {
            Meals.Eaten -= OnEaten;
            Meals.Expired -= OnExpired;
            if (card != null) Hud.Show(card, false);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            card = Hud.Box("hud-card", "hud-card--wood", "hud-meals");
            card.Add(Hud.Text("Pratos e preparos", "hud-meals__title"));
            lines = Hud.Text("", "hud-meals__lines");
            card.Add(lines);
            card.style.display = DisplayStyle.None;
            hud.TopRight.Add(card);
            return true;
        }

        void Update()
        {
            var meals = Meals.Current;
            if (!meals) return;
            if (card == null && !Build()) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            Place();

            int shown = 0;
            long sig = 0;
            for (int e = 1; e <= (int)MealEffect.Poison; e++)
            {
                float left = meals.Remaining((MealEffect)e);
                if (left <= 0f) continue;
                shown++;
                sig = Win.Mix(Win.Mix(sig, e), Mathf.CeilToInt(left));
            }
            bool visible = shown > 0 && !ScreenPanel.Open;
            Hud.Show(card, visible);
            if (!visible || !text.Stale(sig)) return;
            var sb = new System.Text.StringBuilder();
            for (int e = 1; e <= (int)MealEffect.Poison; e++)
            {
                var effect = (MealEffect)e;
                float left = meals.Remaining(effect);
                if (left <= 0f) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<b>{meals.Dish(effect).displayName}</b> <size=85%>{Short(effect, meals.Dish(effect))}</size>  {Meals.Clock(left)}");
            }
            text.Text = sb.ToString();
            Hud.Set(lines, text.Text);
        }

        /// <summary>Logo embaixo do relógio (ou da fama), acima da mochila.</summary>
        void Place()
        {
            var parent = hud.TopRight;
            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent[i] != card && (parent[i].ClassListContains("hud-fame-chip") || parent[i].ClassListContains("hud-clock"))) index = i + 1;
            int at = parent.IndexOf(card);
            if (at < index) index--;
            if (at != index)
            {
                parent.Remove(card);
                parent.Insert(Mathf.Min(index, parent.childCount), card);
            }
        }

        static string Short(MealEffect effect, ItemDefinition dish) => effect switch
        {
            MealEffect.Stamina => $"+{dish.mealAmount:0} fôlego",
            MealEffect.Reload => $"recarga −{dish.mealAmount * 100f:0}%",
            MealEffect.HullRegen => "conserta o casco",
            MealEffect.Stealth => "furtivo",
            MealEffect.Damage => $"golpes +{dish.mealAmount * 100f:0}%",
            MealEffect.Poison => "lâmina envenenada",
            _ => "",
        };

        static void OnEaten(ItemDefinition dish)
        {
            var h = GameHud.Current;
            if (dish.mealEffect == MealEffect.Smoke) return;   // a fumaça avisa sozinha (FootHUD)
            if (h) h.Feed(GameHud.FeedKind.Notice, $"<b>{(Meals.IsTool(dish) ? "Usou" : "Comeu")}: {dish.displayName}</b>\n<size=75%>{Meals.Describe(dish)}</size>", MealColor);
        }

        static void OnExpired(ItemDefinition dish)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, $"Acabou o efeito: {dish.displayName}", new Color(0.85f, 0.8f, 0.7f));
        }
    }
}
