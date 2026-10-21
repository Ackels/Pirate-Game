using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// HUD do saque em terra: mochila (a pé) ou porão (no navio) num cartão à direita (UI Toolkit), nomes sobre o baú, o X
    /// e os sacos, aviso do baú (trancado ou [F] abrir) e de cavar, e ganhos ("+3 Madeira", "mochila cheia", descarregado no
    /// porão) e a janela de escolha baú ↔ mochila (com o mouse).
    /// </summary>
    public class LandLootHUD : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] LandLooter looter;
        [SerializeField] BackpackUnloader unloader;
        [SerializeField] Inventory hold;
        [SerializeField] float labelRange = 60f;
        [Tooltip("Segundos da faixa do prêmio raro ao abrir o baú.")]
        [SerializeField] float prizeBannerSeconds = 4f;

        CachedText promptText;
        readonly Dictionary<UnityEngine.Object, string> worldNames = new();

        GameHud hud;
        CargoCard cargo;
        HudPrompt prompt;
        WorldLabelPool names;
        LootWindow window;
        HudBanner prizeBanner;
        float prizeBannerUntil;
        LandChest shownChest;
        // Prêmio já anunciado em cada baú (reabrir não anuncia de novo; baú sorteado de novo, sim).
        readonly Dictionary<LandChest, ItemDefinition> announced = new();

        public void Bind(PlayerModeSwitcher modeSwitcher, LandLooter landLooter, BackpackUnloader backpackUnloader, Inventory shipHold)
        {
            switcher = modeSwitcher;
            looter = landLooter;
            unloader = backpackUnloader;
            hold = shipHold;
        }

        PrisonerRescuer rescuer;

        void OnEnable()
        {
            if (looter)
            {
                looter.Transferred += OnTransferred;
                looter.BackpackFull += OnBackpackFull;
                looter.Dropped += OnDropped;
                // Prisioneiros (C2): o aviso de F e a fala de quem foi solto saem por aqui, como os baús.
                rescuer = looter.GetComponent<PrisonerRescuer>();
                if (rescuer) rescuer.Freed += OnPrisonerFreed;
            }
            if (unloader) unloader.Unloaded += OnUnloaded;
        }

        void OnPrisonerFreed(Pirates.LandCombat.Prisoner p, string line, string got)
        {
            var h = GameHud.Current;
            if (!h) return;
            // Nome em cima e a fala embaixo, menor (numa linha só a fala passava da tela).
            string extra = string.IsNullOrEmpty(got) ? "" : $"\n{got}";
            h.Feed(GameHud.FeedKind.Notice, $"<b>{p.DisplayName} está livre!</b>\n<size=70%><i>“{line}”</i>{extra}</size>", HudColors.Gold);
        }

        void OnDisable()
        {
            if (looter)
            {
                looter.Transferred -= OnTransferred;
                looter.BackpackFull -= OnBackpackFull;
                looter.Dropped -= OnDropped;
            }
            if (unloader) unloader.Unloaded -= OnUnloaded;
            if (rescuer) rescuer.Freed -= OnPrisonerFreed;
            if (cargo == null) return;
            cargo.Hide();
            prompt.Hide();
            names.HideAll();
            window.Update(false, null);
        }

        static void Feed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, text, color);
        }

        void OnTransferred(ItemStack stack, bool toBackpack)
        {
            string text = !toBackpack ? $"-{stack.amount} {stack.item.displayName} (de volta ao baú)"
                : stack.item.category == ItemCategory.Schematic ? $"Esquema obtido: {stack.item.displayName}!"
                : stack.item.equipSlot != EquipSlot.None ? $"{Rarity.NameOf(stack.item.rarity)}: {stack.item.displayName}!  <size=80%>(I para equipar)</size>"
                : $"+{stack.amount} {stack.item.displayName}";
            Feed(text, toBackpack ? stack.item.color : new Color(0.75f, 0.75f, 0.75f));
        }

        void OnBackpackFull() => Feed("Mochila cheia: o resto ficou no saco", new Color(1f, 0.55f, 0.4f));

        void OnDropped(ItemStack stack, float seconds) =>
            Feed($"Largou {stack.amount} {stack.item.displayName} no chão  <size=80%>(o saco some em {Clock(seconds)})</size>", new Color(0.85f, 0.8f, 0.7f));

        static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        // Prazo sobre os sacos largados: o texto só refaz quando o segundo muda.
        readonly Dictionary<LootSack, (int second, string text)> sackTimers = new();

        string SackLabel(LootSack sack)
        {
            int s = Mathf.CeilToInt(sack.SecondsLeft);
            if (sackTimers.TryGetValue(sack, out var t) && t.second == s) return t.text;
            if (sackTimers.Count > 32) sackTimers.Clear();
            string text = $"Saque largado\n<size=70%>some em {s / 60}:{s % 60:00}</size>";
            sackTimers[sack] = (s, text);
            return text;
        }

        void OnUnloaded(List<ItemStack> moved, bool leftovers)
        {
            if (moved.Count > 0)
            {
                var parts = new List<string>();
                foreach (var s in moved) parts.Add($"{s.amount} {s.item.displayName}");
                Feed($"Descarregado no porão: {string.Join(", ", parts)}", new Color(0.55f, 0.92f, 0.6f));
            }
            if (leftovers) Feed("Porão cheio: parte ficou na mochila", new Color(1f, 0.55f, 0.4f));
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            cargo = new CargoCard(hud.TopRight);
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            names = new WorldLabelPool(hud.World, "hud-world-label--small");
            prizeBanner = new HudBanner();
            hud.Banners.Add(prizeBanner);
            window = new LootWindow(hud.Windows, new LootWindow.Setup
            {
                sourceTitle = "No baú",
                targetTitle = "Na mochila",
                emptySource = "vazio",
                emptyTarget = "vazia",
                giveBack = "Deixar",
                giveBackHint = "devolve o item ao baú",
                contents = () => looter.Current ? looter.Current.Contents : null,
                target = () => looter.Backpack,
                take = looter.Take,
                discard = looter.Discard,
                takeAll = looter.TakeAll,
                close = looter.Close,
            });
            return true;
        }

        void Update()
        {
            if (!switcher || !looter) return;
            if (cargo == null && !Build()) return;
            bool open = switcher.IsOnFoot && looter.IsOpen;
            window.Update(open, open ? looter.Current.DisplayName : null);
            AnnouncePrize(open ? looter.Current : null);

            if (switcher.IsOnFoot)
            {
                cargo.Show("Mochila", looter.Backpack, "vazia: quebre caixotes e barris, saqueie baús");
                UpdateWorldLabels();
                if (looter.IsOpen)
                {
                    prompt.Hide();
                    hud.LowerPickups();
                }
                else UpdatePrompt();
            }
            else
            {
                if (hold) cargo.Show("Porão", hold, "vazio");
                else cargo.Hide();
                prompt.Hide();
                names.HideAll();
            }
        }

        /// <summary>
        /// Baú aberto com prêmio raro, épico ou lendário (esquema ou equipamento): faixa com o nome na cor da raridade (E1, 19/10). Uma vez
        /// por prêmio sorteado em cada baú.
        /// </summary>
        void AnnouncePrize(LandChest chest)
        {
            if (prizeBannerUntil > 0f && Time.time > prizeBannerUntil)
            {
                prizeBannerUntil = 0f;
                prizeBanner.Hide();
            }
            if (chest == shownChest) return;
            shownChest = chest;
            if (!chest) return;
            var prize = chest.BestPrize;
            if (!prize || prize.rarity < ItemRarity.Rare) return;
            if (announced.TryGetValue(chest, out var already) && already == prize) return;
            announced[chest] = prize;
            string kind = prize.category == ItemCategory.Schematic ? "esquema" : prize.equipSlot != EquipSlot.None ? Rarity.SlotName(prize.equipSlot).ToLowerInvariant() : "prêmio";
            prizeBanner.Show($"<color={Rarity.Hex(prize.rarity)}>{Rarity.NameOf(prize.rarity)}</color>: {prize.displayName}!",
                $"{kind} no baú");
            prizeBannerUntil = Time.time + prizeBannerSeconds;
        }

        void UpdatePrompt()
        {
            var chest = looter.Nearby;
            if (!chest && looter.NearbyDig)
            {
                if (promptText.Stale(looter.NearbyDig)) promptText.Text = $"Cavar: {looter.NearbyDig.DisplayName}";
                prompt.Show("F", promptText.Text);
                return;
            }
            if (!chest && rescuer && rescuer.Nearby)
            {
                var p = rescuer.Nearby;
                bool cageLocked = p.IsLocked;
                string why = cageLocked ? p.LockedReason : null;
                if (promptText.Stale(p, HudDraw.Flag(cageLocked), why == null ? 0 : why.GetHashCode()))
                    promptText.Text = cageLocked ? $"{p.DisplayName}: gaiola trancada  <size=80%>({why})</size>" : $"Soltar {p.DisplayName}";
                prompt.Show(cageLocked ? null : "F", promptText.Text);
                return;
            }
            if (!chest)
            {
                prompt.Hide();
                return;
            }
            bool locked = chest.IsLocked;
            string reason = locked ? chest.LockedReason : null;
            // O motivo do tranca pode ser montado na hora: compara pelo conteúdo, não pela referência.
            if (promptText.Stale(chest, HudDraw.Flag(locked), reason == null ? 0 : reason.GetHashCode()))
                promptText.Text = locked ? $"{chest.DisplayName}: trancado  <size=80%>({reason})</size>" : $"Abrir {chest.DisplayName}";
            prompt.Show(locked ? null : "F", promptText.Text);
        }

        void UpdateWorldLabels()
        {
            if (!names.Begin()) return;
            var cam = Camera.main;
            float range2 = labelRange * labelRange;
            for (int i = 0; i < LandChest.Active.Count; i++)
            {
                var chest = LandChest.Active[i];
                if (chest.IsEmpty) continue;
                Vector3 pos = chest.transform.position + Vector3.up * 1.4f;
                if ((pos - cam.transform.position).sqrMagnitude > range2) continue;
                bool locked = chest.IsLocked;
                if (locked && !worldNames.ContainsKey(chest)) worldNames[chest] = $"{chest.DisplayName}\n<size=70%>trancado</size>";
                names.Place(pos, locked ? worldNames[chest] : chest.DisplayName, locked ? HudColors.Muted : HudColors.Gold);
            }
            for (int i = 0; i < BuriedTreasure.Active.Count; i++)
            {
                var spot = BuriedTreasure.Active[i];
                if (!spot.CanDig) continue;
                Vector3 pos = spot.transform.position + Vector3.up * 1.2f;
                if ((pos - cam.transform.position).sqrMagnitude > range2) continue;
                if (!worldNames.TryGetValue(spot, out var text)) worldNames[spot] = text = $"X  {spot.DisplayName}";
                names.Place(pos, text, HudColors.Gold);
            }
            for (int i = 0; i < LootSack.Active.Count; i++)
            {
                var sack = LootSack.Active[i];
                Vector3 pos = sack.transform.position + Vector3.up * 0.9f;
                if ((pos - cam.transform.position).sqrMagnitude > range2) continue;
                if (sack.Expires) names.Place(pos, SackLabel(sack), sack.SecondsLeft < 20f ? HudColors.Warn : HudColors.Cream);
                else names.Place(pos, "Saque", HudColors.Cream);
            }
            names.End();
        }
    }
}
