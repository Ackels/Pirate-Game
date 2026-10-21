using Pirates.Controls;
using Pirates.LandCombat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// A ruína das alavancas no HUD (C4, 19/10): "[F] Puxar a alavanca", o portão trancado ("puxe as 3 alavancas: 1 de 3"), o nome sobre
    /// as alavancas que faltam (de perto), faixa a cada alavanca e quando o portão abre. Passagens (C5, 20/10): "[F] Descer às
    /// catacumbas", "alçapão fechado", a tela preta da ida (<see cref="PassageWalker.Fade"/>) e a faixa de chegada.
    /// </summary>
    public class RuinHUD : MonoBehaviour
    {
        const float LabelRange = 22f, GateRange = 5.5f;

        [SerializeField] LeverPuller puller;
        [SerializeField] PassageWalker walker;
        [SerializeField] RuinPuzzle[] puzzles = System.Array.Empty<RuinPuzzle>();
        [SerializeField] float bannerSeconds = 4.5f;

        GameHud hud;
        HudPrompt prompt;
        HudBanner banner;
        WorldLabelPool names;
        VisualElement blackout;
        float bannerUntil, shownFade = -1f;
        CachedText line;

        public void Bind(LeverPuller leverPuller, PassageWalker passageWalker, RuinPuzzle[] all)
        {
            puller = leverPuller;
            walker = passageWalker;
            puzzles = all;
        }

        void OnEnable()
        {
            foreach (var p in puzzles)
                if (p)
                {
                    p.LeverPulled += OnLeverPulled;
                    p.Opened += OnOpened;
                }
            if (walker) walker.Arrived += OnArrived;
        }

        void OnDisable()
        {
            foreach (var p in puzzles)
                if (p)
                {
                    p.LeverPulled -= OnLeverPulled;
                    p.Opened -= OnOpened;
                }
            if (walker) walker.Arrived -= OnArrived;
            if (prompt == null) return;
            blackout.style.display = DisplayStyle.None;
            shownFade = 0f;
            prompt.Hide();
            banner.Hide();
            names.HideAll();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            banner = new HudBanner();
            hud.Banners.Add(banner);
            names = new WorldLabelPool(hud.World, "hud-world-label--small");
            // Tela preta da passagem: por cima de tudo (até das faixas), sem pegar o mouse.
            blackout = new VisualElement { pickingMode = PickingMode.Ignore };
            blackout.AddToClassList("hud-blackout");
            blackout.style.display = DisplayStyle.None;
            hud.Menu.Add(blackout);
            return true;
        }

        void Update()
        {
            if (!puller) return;
            if (prompt == null && !Build()) return;
            if (bannerUntil > 0f && Time.time > bannerUntil)
            {
                bannerUntil = 0f;
                banner.Hide();
            }
            UpdateBlackout();
            if (!puller.isActiveAndEnabled)
            {
                prompt.Hide();
                names.HideAll();
                return;
            }
            UpdatePrompt();
            UpdateLabels();
        }

        void UpdateBlackout()
        {
            float fade = walker && walker.isActiveAndEnabled ? walker.Fade : 0f;
            if (Mathf.Abs(fade - shownFade) < 0.004f) return;
            shownFade = fade;
            blackout.style.display = fade > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            blackout.style.opacity = fade;
        }

        void UpdatePrompt()
        {
            if (walker && walker.isActiveAndEnabled && !walker.IsTraveling && walker.Nearby)
            {
                var passage = walker.Nearby;
                if (!passage.IsLocked) prompt.Show("F", passage.ActionText);
                else
                {
                    if (line.Stale(passage, passage.LockProgress)) line.Text = $"Alçapão fechado  <size=80%>({passage.LockedReason})</size>";
                    prompt.Show(null, line.Text);
                }
                return;
            }
            if (walker && walker.IsTraveling)
            {
                prompt.Hide();
                return;
            }
            var lever = puller.Nearby;
            if (lever)
            {
                var p = lever.Puzzle;
                if (!lever.IsPulled) prompt.Show("F", "Puxar a alavanca");
                else
                {
                    if (line.Stale(lever, p ? p.Pulled : 0)) line.Text = p ? $"Alavanca puxada  <size=80%>({p.Pulled} de {p.LeverCount})</size>" : "Alavanca puxada";
                    prompt.Show(null, line.Text);
                }
                return;
            }
            Vector3 me = puller.transform.position;
            foreach (var p in puzzles)
            {
                if (!p || p.IsOpen || !p.Gate) continue;
                Vector3 d = p.Gate.position - me;
                d.y = 0f;
                if (d.sqrMagnitude > GateRange * GateRange) continue;
                if (line.Stale(p, p.Pulled)) line.Text = $"Portão de grade trancado  <size=80%>({p.LockedReason})</size>";
                prompt.Show(null, line.Text);
                return;
            }
            prompt.Hide();
        }

        void UpdateLabels()
        {
            if (!names.Begin()) return;
            var cam = Camera.main;
            var list = RuinLever.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i];
                if (l.IsPulled) continue;
                Vector3 pos = l.transform.position + Vector3.up * 1.6f;
                if ((pos - cam.transform.position).sqrMagnitude > LabelRange * LabelRange * 4f) continue;
                if ((l.transform.position - puller.transform.position).sqrMagnitude > LabelRange * LabelRange) continue;
                names.Place(pos, l.DisplayName, HudColors.Gold);
            }
            names.End();
        }

        void OnLeverPulled(RuinPuzzle p, int pulled, int total)
        {
            if (banner == null || pulled >= total) return;
            banner.Show($"Alavanca {pulled} de {total}", "a armadilha da ala para e um braseiro acende");
            bannerUntil = Time.time + bannerSeconds;
        }

        void OnArrived(RuinPassage p)
        {
            if (banner == null || string.IsNullOrEmpty(p.ArrivalTitle)) return;
            banner.Show(p.ArrivalTitle, p.ArrivalSubtitle);
            bannerUntil = Time.time + bannerSeconds;
        }

        void OnOpened(RuinPuzzle p)
        {
            if (banner == null) return;
            // Título curto: com o nome da ruína passava da faixa.
            banner.Show("O portão se abriu!", $"{p.DisplayName}: o santuário está livre");
            bannerUntil = Time.time + bannerSeconds + 1f;
        }
    }
}
