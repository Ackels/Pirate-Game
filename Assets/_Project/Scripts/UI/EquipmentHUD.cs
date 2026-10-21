using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.OnFoot;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Tela de equipamento (E1; tecla I, no navio e a pé). Refeita em 20/10 a pedido do Jhonata, no jeito dos inventários de jogo
    /// (referências: Last Day on Earth, Minecraft): à direita o <b>capitão de corpo inteiro</b> (a vitrine ao vivo, <see cref="LoadoutMannequin"/>,
    /// gira arrastando o mouse) com os <b>encaixes em volta</b> (lâmina, pistola e bacamarte de um lado; traje, relíquia e mochila do outro)
    /// e os atributos embaixo; à esquerda a <b>grade de itens</b> com ícone e moldura na cor da raridade (equipamentos que ele tem em
    /// qualquer lugar; depois a mochila a pé ou o porão no navio) e o quadro do item escolhido, com números, comparação e Equipar/Tirar.
    /// Clique escolhe, clique duplo numa lâmina equipa. Não pausa; os cliques são dela (ScreenPanel); remonta só quando algo muda.
    /// </summary>
    public class EquipmentHUD : MonoBehaviour
    {
        [Tooltip("Armas do capitão (a lâmina de começo e as equipáveis).")]
        [SerializeField] GearWeapons gear;
        [Tooltip("Ícone do encaixe da mochila com a de lona (a de começo, que não é item).")]
        [SerializeField] Texture2D starterPackIcon;

        GameHud hud;
        HudWindow window;
        bool open;
        PlayerModeSwitcher switcher;
        CharacterCombat combat;
        Object selected;
        readonly List<ItemDefinition> owned = new();

        public void Bind(GearWeapons captainGear, Texture2D packIcon)
        {
            gear = captainGear;
            starterPackIcon = packIcon;
        }

        /// <summary>Tela aberta (testes pela CLI: Toggle sem teclado).</summary>
        public bool IsOpen => open;

        public void Toggle() => CaptainBook.Toggle(CaptainBook.Tab.Inventory);

        /// <summary>Escolhe um item ou arma no quadro de detalhes (testes).</summary>
        public void Select(Object key) => selected = key;

        void OnDisable() => SetOpen(false);

        void OnDestroy() => CaptainBook.Unregister(CaptainBook.Tab.Inventory);

        void Update()
        {
            if (window == null && !Build()) return;
            var input = GameInput.Instance;
            if (input.GameplayEnabled)
            {
                // Abas da tela do capitão: I abre/fecha esta, ou troca para ela se outra aba está aberta.
                if (input.Equipment.WasPressedThisFrame()) Toggle();
                else if (open && input.Cancel.WasPressedThisFrame()) SetOpen(false);
            }
            else if (open) SetOpen(false);   // menu inicial por cima

            if (!open) return;
            window.RefreshAll();
            hud.LowerPickups();
        }

        void SetOpen(bool value)
        {
            if (value && window == null) return;
            if (open == value) return;
            open = value;
            ScreenPanel.Open = value;
            if (value) CaptainBook.RefreshTabs(window);
            window?.Show(value);
            if (LoadoutMannequin.Current) LoadoutMannequin.Current.Show(value);
            if (hud != null && hud.Prompts != null) Hud.Show(hud.Prompts, !value);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            switcher = FindAnyObjectByType<PlayerModeSwitcher>(FindObjectsInactive.Include);
            if (gear) combat = gear.GetComponent<CharacterCombat>();
            window = new HudWindow(hud.Windows, "hud-window--equipment");
            window.SetTitle("Inventário do capitão", "I ou Esc fecha · clique escolhe, clique duplo equipa");
            CaptainBook.AddTabs(window, CaptainBook.Tab.Inventory);
            CaptainBook.Register(CaptainBook.Tab.Inventory, () => open, SetOpen);
            window.AddPane(window.Body, Signature, BuildBody);
            window.Footer.Add(Hud.Text("Lâminas e armas novas vêm dos baús: acampamentos, tesouros, ruínas e chefes. O facho do baú sai na cor do melhor prêmio.",
                "hud-window__hint"));
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[I]</b>", () => SetOpen(false)));
            window.Show(false);
            return true;
        }

        long Signature()
        {
            long hash = Win.Mix(PlayerLoadout.Version, PlayerStorage.Version);
            hash = Win.Mix(hash, selected);
            hash = Win.Mix(hash, OnFoot ? 1 : 2);
            hash = Win.Mix(hash, Win.Of(Bag));
            if (gear) hash = Win.Mix(hash, gear.CurrentBlade);
            return hash;
        }

        bool OnFoot => switcher && switcher.IsOnFoot;

        /// <summary>A mochila a pé; no navio, o porão.</summary>
        Inventory Bag => OnFoot || !switcher || !switcher.Ship ? (gear ? gear.GetComponent<Inventory>() : null) : switcher.Ship.GetComponent<Inventory>();

        // ---------- Montagem ----------

        void BuildBody(VisualElement body)
        {
            owned.Clear();
            PlayerStorage.CollectOwned(owned, item => item.category == ItemCategory.Gear || item.equipSlot != EquipSlot.None);
            owned.Sort((a, b) => b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : string.CompareOrdinal(a.displayName, b.displayName));

            var layout = Hud.Row("hud-equip");
            body.Add(layout);
            var left = Hud.Box("hud-equip__left");
            layout.Add(left);
            BuildItems(left);
            var doll = Hud.Box("hud-equip__doll");
            layout.Add(doll);
            BuildDoll(doll);
        }

        void BuildItems(VisualElement left)
        {
            var scroll = Win.Scroll(left);
            scroll.AddToClassList("hud-equip__items");
            Win.Text(scroll, "Equipamentos", "hud-window__subtitle-strong");
            var grid = Hud.Box("hud-equip__grid");
            scroll.Add(grid);
            var starter = gear ? gear.StarterBlade : null;
            if (starter) Cell(grid, starter, null, Icon(null, starter), ItemRarity.Common, 0, IsInUse(null, starter));
            foreach (var item in owned)
                Cell(grid, item, item, Icon(item, null), item.rarity, 0, IsInUse(item, null));

            var bag = Bag;
            Win.Text(scroll, OnFoot ? "Mochila" : "Porão", "hud-window__subtitle-strong");
            var bagGrid = Hud.Box("hud-equip__grid");
            scroll.Add(bagGrid);
            int shown = 0;
            if (bag)
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    var item = bag.Items[i];
                    if (!item || item.IsUnique || item.category == ItemCategory.Crew) continue;
                    Cell(bagGrid, item, item, Icon(item, null), item.rarity, bag.Count(item), false);
                    shown++;
                }
            if (shown == 0) Win.Text(scroll, OnFoot ? "mochila vazia" : "porão vazio", "hud-window__empty");

            var details = Win.Section(left, "hud-equip__details");
            BuildDetails(details);
        }

        /// <summary>Uma casinha da grade: ícone (ou a cor e a inicial), moldura da raridade, quantidade e "em uso".</summary>
        void Cell(VisualElement parent, Object key, ItemDefinition item, Texture2D icon, ItemRarity rarity, int count, bool inUse)
        {
            var cell = Hud.Box("hud-equip__cell");
            cell.pickingMode = PickingMode.Position;
            if (rarity >= ItemRarity.Uncommon) SetBorder(cell, Rarity.ColorOf(rarity));
            if (key == selected) cell.AddToClassList("hud-equip__cell--selected");
            if (inUse) cell.AddToClassList("hud-equip__cell--in-use");
            cell.Add(IconBox(icon, item));
            if (count > 1) cell.Add(Hud.Text(count >= 1000 ? $"{count / 1000f:0.#}k" : count.ToString(), "hud-equip__count"));
            if (inUse) cell.Add(Hud.Text("✓", "hud-equip__check"));
            cell.RegisterCallback<ClickEvent>(e =>
            {
                selected = key;
                if (e.clickCount >= 2) Equip(item, key as WeaponDefinition);
            });
            parent.Add(cell);
        }

        static VisualElement IconBox(Texture2D icon, ItemDefinition item)
        {
            var box = Hud.Box("hud-equip__icon");
            box.pickingMode = PickingMode.Ignore;
            if (icon) box.style.backgroundImage = new StyleBackground(icon);
            else if (item)
            {
                box.style.backgroundColor = item.color;
                box.AddToClassList("hud-equip__icon--swatch");
                var letter = Hud.Text(item.displayName.Substring(0, 1), "hud-equip__initial");
                letter.pickingMode = PickingMode.Ignore;
                box.Add(letter);
            }
            return box;
        }

        static void SetBorder(VisualElement e, Color c)
        {
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }

        // ---------- O capitão e os encaixes ----------

        void BuildDoll(VisualElement doll)
        {
            var fame = CaptainFame.Current;
            if (fame)
            {
                doll.Add(Hud.Text(fame.CaptainName, "hud-equip__name"));
                doll.Add(Hud.Text(fame.RankTitle(fame.Rank), "hud-equip__rank"));
            }
            var row = Hud.Row("hud-equip__doll-row");
            doll.Add(row);
            var leftSlots = Hud.Box("hud-equip__slots");
            row.Add(leftSlots);
            var portrait = Hud.Box("hud-equip__portrait");
            row.Add(portrait);
            var rightSlots = Hud.Box("hud-equip__slots");
            row.Add(rightSlots);

            var mannequin = LoadoutMannequin.Current;
            if (mannequin && mannequin.Texture)
            {
                portrait.style.backgroundImage = Background.FromRenderTexture((RenderTexture)mannequin.Texture);
                portrait.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if ((e.pressedButtons & 1) != 0) mannequin.Spin(-e.deltaPosition.x * 0.7f);
                });
                portrait.Add(Hud.Text("arraste para girar", "hud-equip__spin-hint"));
            }

            // Lâmina
            var bladeItem = PlayerLoadout.Active(EquipSlot.Blade);
            var blade = EquippedBlade();
            Slot(leftSlots, "Lâmina", bladeItem ? bladeItem : blade, bladeItem, Icon(bladeItem, blade), bladeItem ? bladeItem.rarity : ItemRarity.Common, true);
            // Armas de fogo do capitão (a pistola sempre; o bacamarte se comprado).
            for (int i = 0; combat && i < combat.Weapons.Count; i++)
            {
                var w = combat.Weapons[i];
                if (!w || w.kind != WeaponKind.Firearm) continue;
                var item = gear ? gear.ItemFor(i) : null;
                bool has = combat.IsAvailable(i);
                Slot(leftSlots, w.twoHanded ? "Arma pesada" : "Pistola", has ? (item ? item : w) : null, has ? item : null,
                    has ? Icon(item, w) : null, has && item ? item.rarity : ItemRarity.Common, has);
            }
            Slot(rightSlots, "Traje", null, null, null, ItemRarity.Common, false, "em breve");
            Slot(rightSlots, "Relíquia", null, null, null, ItemRarity.Common, false, "em breve");
            var pack = gear ? gear.GetComponent<BackpackGear>() : null;
            var packItem = pack ? pack.Current : null;
            Slot(rightSlots, "Mochila", packItem, packItem, packItem ? Icon(packItem, null) : starterPackIcon,
                packItem ? packItem.rarity : ItemRarity.Common, true, packItem ? null : "de lona");

            BuildStats(doll, blade);
        }

        void Slot(VisualElement parent, string label, Object key, ItemDefinition item, Texture2D icon, ItemRarity rarity, bool filled, string empty = null)
        {
            var box = Hud.Box("hud-equip__slot");
            var cell = Hud.Box("hud-equip__cell", "hud-equip__cell--slot");
            cell.pickingMode = PickingMode.Position;
            if (!filled) cell.AddToClassList("hud-equip__cell--empty");
            if (rarity >= ItemRarity.Uncommon) SetBorder(cell, Rarity.ColorOf(rarity));
            if (key && key == selected) cell.AddToClassList("hud-equip__cell--selected");
            if (filled && (icon || item)) cell.Add(IconBox(icon, item));
            else cell.Add(Hud.Text(empty ?? "—", "hud-equip__empty-text"));
            if (key) cell.RegisterCallback<ClickEvent>(_ => selected = key);
            box.Add(cell);
            box.Add(Hud.Text(label, "hud-equip__slot-label"));
            parent.Add(box);
        }

        void BuildStats(VisualElement doll, WeaponDefinition blade)
        {
            var row = Hud.Row("hud-equip__stats");
            doll.Add(row);
            var health = gear ? gear.GetComponent<Health>() : null;
            var stamina = gear ? gear.GetComponent<Stamina>() : null;
            var bag = gear ? gear.GetComponent<Inventory>() : null;
            Stat(row, "Vida", health ? $"{health.Max:0}" : "—");
            Stat(row, "Fôlego", stamina ? $"{stamina.Max:0}" : "—");
            if (blade)
            {
                Stat(row, "Golpe", $"{blade.damage:0}");
                Stat(row, "Golpes/s", $"{Rate(blade):0.0}");
                Stat(row, "Dano/s", $"{Dps(blade):0}");
            }
            Stat(row, "Mochila", bag ? $"{bag.Capacity:0}" : "—");
        }

        static void Stat(VisualElement row, string label, string value)
        {
            var box = Hud.Box("hud-equip__stat");
            box.Add(Hud.Text(value, "hud-equip__stat-value"));
            box.Add(Hud.Text(label, "hud-equip__stat-label"));
            row.Add(box);
        }

        // ---------- Detalhes ----------

        void BuildDetails(VisualElement box)
        {
            var item = selected as ItemDefinition;
            var weapon = item ? item.equipment as WeaponDefinition : selected as WeaponDefinition;
            if (!item && !weapon)
            {
                Win.Text(box, "Clique num item para ver os detalhes. Clique duplo numa lâmina para equipar.", "hud-window__empty");
                return;
            }
            var rarity = item ? item.rarity : ItemRarity.Common;
            string name = item ? item.displayName : weapon.displayName;
            var top = Win.Row(box);
            top.Add(IconBox(Icon(item, weapon), item));
            var head = Hud.Box("hud-grow", "hud-equip__details-head");
            top.Add(head);
            head.Add(Hud.Text($"<color={InkHex(rarity)}><b>{name}</b></color>", "hud-window__label", "hud-window__text"));
            string kind = item && item.equipSlot != EquipSlot.None ? Rarity.SlotName(item.equipSlot)
                : weapon ? (weapon.kind == WeaponKind.Melee ? "Lâmina" : "Arma de fogo")
                : item && item.category == ItemCategory.Gear ? "Equipamento"
                : item && item.category == ItemCategory.Schematic ? "Esquema" : "Recurso";
            head.Add(Hud.Text($"{Rarity.NameOf(rarity)} · {kind}", "hud-equip__rarity"));

            var current = EquippedBlade();
            if (weapon && weapon.kind == WeaponKind.Melee)
            {
                bool inUse = weapon == current;
                Win.Text(box, Stats(weapon) + (inUse || !current ? "" : $"   {Compare(weapon, current)}"), "hud-window__small", "hud-window__text");
                string about = item ? item.description : StarterText;
                if (!string.IsNullOrEmpty(about)) Win.Text(box, $"<i>{about}</i>", "hud-window__small", "hud-window__muted");
                var actions = Win.Row(box);
                Win.Spacer(actions);
                if (!inUse) actions.Add(Win.Button("Equipar", () => Equip(item, weapon), true, "hud-button--small"));
                else if (item) actions.Add(Win.Button("Tirar (volta ao cutelo)", () => Equip(null, null), true, "hud-button--small"));
                else actions.Add(Hud.Text("em uso", "hud-equip__in-use"));
                return;
            }
            if (weapon)
            {
                string shots = weapon.pellets > 1 ? $"{weapon.pellets} chumbos × {weapon.damage:0}" : $"dano <b>{weapon.damage:0}</b>";
                Win.Text(box, $"{shots} · alcance {weapon.range:0} m · recarga {weapon.reloadTime:0.0} s", "hud-window__small", "hud-window__text");
            }
            if (item && item.backpackCapacity > 0f)
                Win.Text(box, $"espaço da mochila: <b>{item.backpackCapacity:0}</b> (vale a maior que você tiver)", "hud-window__small", "hud-window__text");
            var bag = Bag;
            if (item && bag && bag.Count(item) > 0 && !item.IsUnique)
                Win.Text(box, $"<b>{bag.Count(item)}</b> {(OnFoot ? "na mochila" : "no porão")} · {HudDraw.SpaceText(item)}", "hud-window__small", "hud-window__text");
            string text = item ? item.description : null;
            if (!string.IsNullOrEmpty(text)) Win.Text(box, $"<i>{text}</i>", "hud-window__small", "hud-window__muted");
            var looter = OnFoot && gear ? gear.GetComponent<LandLooter>() : null;
            if (item && looter && looter.Backpack.Count(item) > 0) AddDrop(box, looter, item);
        }

        /// <summary>
        /// Largar da mochila (21/10, pedido do Jhonata): o item vai para um saco no chão, à frente do capitão, que some sozinho depois de
        /// alguns minutos (sem prazo, dava para estocar saque pelo mapa). Equipamento não se larga.
        /// </summary>
        void AddDrop(VisualElement box, LandLooter looter, ItemDefinition item)
        {
            int count = looter.Backpack.Count(item);
            string reason = looter.DropBlockReason(item);
            var row = Win.Row(box);
            row.Add(Hud.Text(reason ?? $"largado no chão, o saco some em {looter.DropLifetime / 60f:0.#} min", "hud-window__small", "hud-window__muted", "hud-grow"));
            if (reason != null) return;
            if (count > 1) row.Add(Win.Button("Largar 1", () => Drop(looter, item, 1), true, "hud-button--small"));
            row.Add(Win.Button(count > 1 ? $"Largar todos ({count})" : "Largar no chão", () => Drop(looter, item, count), true, "hud-button--small"));
        }

        static void Drop(LandLooter looter, ItemDefinition item, int amount)
        {
            if (looter.Drop(item, amount) > 0) return;
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Notice, looter.DropBlockReason(item) ?? "não deu para largar aqui", HudColors.Bad);
        }

        const string StarterText = "O cutelo de sempre: firme, sem luxo. Serve até achar coisa melhor.";

        bool IsInUse(ItemDefinition item, WeaponDefinition weapon)
        {
            var current = EquippedBlade();
            if (item && item.equipment is WeaponDefinition w) return w == current;
            if (weapon) return weapon == current;
            return item && (item == PlayerLoadout.Active(item.equipSlot) || (gear && gear.GetComponent<BackpackGear>() is { } pack && pack.Current == item)
                || (combat && IsUnlockedGun(item)));
        }

        bool IsUnlockedGun(ItemDefinition item)
        {
            for (int i = 0; i < combat.Weapons.Count; i++)
                if (gear.ItemFor(i) == item) return combat.IsAvailable(i);
            return false;
        }

        /// <summary>A lâmina em uso: a do item equipado (se o jogador ainda a tem) ou a de começo.</summary>
        WeaponDefinition EquippedBlade()
        {
            var item = PlayerLoadout.Active(EquipSlot.Blade);
            if (item && item.equipment is WeaponDefinition w) return w;
            return gear ? gear.StarterBlade : null;
        }

        static Texture2D Icon(ItemDefinition item, WeaponDefinition weapon)
        {
            if (item && item.icon) return item.icon;
            if (item && item.equipment is WeaponDefinition w && w.icon) return w.icon;
            return weapon ? weapon.icon : null;
        }

        void Equip(ItemDefinition item, WeaponDefinition weapon)
        {
            // Só lâminas se equipam aqui (a de começo = tirar a equipada).
            bool blade = item ? item.equipSlot == EquipSlot.Blade : weapon && weapon.kind == WeaponKind.Melee || weapon == null;
            if (!blade) return;
            if (item) PlayerLoadout.Equip(item);
            else PlayerLoadout.Unequip(EquipSlot.Blade);
            var h = GameHud.Current;
            string name = item ? item.displayName : gear && gear.StarterBlade ? gear.StarterBlade.displayName : "Cutelo";
            if (h) h.Feed(GameHud.FeedKind.Notice, $"Lâmina equipada: {name}", item ? Rarity.ColorOf(item.rarity) : HudColors.Cream);
        }

        // ---------- Números ----------

        /// <summary>Golpes por segundo batendo sem parar (o combo do cutelo tem a mesma espera nos dois golpes).</summary>
        static float Rate(WeaponDefinition w) => w.cooldown > 0.01f ? 1f / w.cooldown : 1f;

        static float Dps(WeaponDefinition w) => w.damage * Rate(w);

        static string Stats(WeaponDefinition w) =>
            $"dano <b>{w.damage:0}</b> · <b>{Rate(w):0.0}</b> golpes/s · <b>{Dps(w):0}</b> de dano/s · alcance {w.range:0.0} m";

        static string Compare(WeaponDefinition w, WeaponDefinition current)
        {
            float a = Dps(w), b = Dps(current);
            if (b <= 0f) return "";
            int pct = Mathf.RoundToInt((a / b - 1f) * 100f);
            if (pct == 0) return "<color=#6b4a2b>(mesmo dano/s)</color>";
            return pct > 0 ? $"<color=#2f7a24><b>+{pct}%</b> dano/s</color>" : $"<color=#a8321f><b>{pct}%</b> dano/s</color>";
        }

        /// <summary>Cor da raridade para texto no pergaminho (as do HUD são claras demais no papel).</summary>
        static string InkHex(ItemRarity rarity) => "#" + ColorUtility.ToHtmlStringRGB(rarity switch
        {
            ItemRarity.Uncommon => new Color(0.2f, 0.48f, 0.16f),
            ItemRarity.Rare => new Color(0.16f, 0.36f, 0.7f),
            ItemRarity.Epic => new Color(0.46f, 0.22f, 0.68f),
            ItemRarity.Legendary => new Color(0.72f, 0.46f, 0.04f),
            _ => new Color(0.35f, 0.25f, 0.16f),
        });
    }
}
