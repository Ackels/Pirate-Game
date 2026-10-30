using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.OnFoot;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// HUD do desembarque (UI Toolkit): no navio, nome dos desembarques e o aviso "[F] Desembarcar"; a pé, o lugar e os bandos por perto
    /// (canto de cima, ajuda dos controles com H), clarão vermelho ao levar golpe, "!" sobre os piratas que te viram, faixa de derrota e
    /// avisos (voltar ao navio, água funda). Em terra esconde os HUDs só de navio.
    /// HUD de RPG (21/10, pedido do Jhonata com o Free Fire de exemplo): embaixo à esquerda o medalhão com o retrato do capitão, o nome, a
    /// patente, a barra de vida e a de fôlego; embaixo à direita o cartão da arma na mão (ícone grande, munição, estado) e a fileira de
    /// encaixes com as armas (1/2/3) e os itens de uso (R ração, G bomba) com a quantidade.
    /// </summary>
    public class FootHUD : MonoBehaviour
    {
        /// <summary>Distância (m) até onde um acampamento aparece no painel.</summary>
        const float CampListRange = 180f;

        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] ShipLandingInteraction landing;
        [Tooltip("HUDs que só valem no navio (status, porão, porto): somem em terra.")]
        [SerializeField] Behaviour[] shipOnlyHuds = System.Array.Empty<Behaviour>();
        [SerializeField] float labelRange = 300f;
        [Tooltip("Segundos que o \"!\" fica sobre o pirata depois que ele te vê.")]
        [SerializeField] float alertMarkSeconds = 2.5f;

        [Tooltip("Segundos da dica de câmera no primeiro desembarque.")]
        [SerializeField] float cameraHintSeconds = 7f;

        // Dica de câmera: só no primeiro desembarque da sessão.
        static bool cameraHintShown;
        float rescuedUntil, respawnedUntil, cameraHintUntil;
        CharacterCombat combat;
        Health health;
        PlayerFootRespawn respawn;
        HarborBuildingVisitor buildingVisitor;
        CachedText where, life, banner1, landingPrompt, playerLine, rankLine, currentAmmoLine, currentStateLine;
        FoodEater food;
        PistolAmmo pistolAmmo;
        BombThrower bombs;
        SmokeBomb smoke;
        CachedText[] campLines = System.Array.Empty<CachedText>();
        readonly System.Collections.Generic.Dictionary<LandingPoint, string> landingLabels = new();

        GameHud hud;
        VisualElement card, help, campBox;
        Label whereText, lifeText;
        HudBar lifeBar, staminaBar;
        // Capitão (embaixo à esquerda) e armas/itens (embaixo à direita).
        VisualElement player, medal, loadout, slotRow, currentIcon;
        Label nameText, rankText, medalLetter, currentAmmo, currentName, currentState;
        HudBar reloadBar;
        Texture2D portrait;
        WeaponDefinition shownWeapon;
        long slotSignature = -1;
        readonly System.Collections.Generic.List<Slot> slots = new();

        /// <summary>Um encaixe da fileira: arma (índice) ou item de uso (ração, bomba).</summary>
        sealed class Slot
        {
            public VisualElement root, icon;
            public Label key, count;
            public int weapon = -1;
            public bool food, bomb, smoke, on, empty, none;
            public int lastCount = int.MinValue;
        }
        Stamina stamina;
        Label[] campLabels = System.Array.Empty<Label>();
        HudPrompt prompt, cameraHint;
        HudBanner banner;
        WorldLabelPool landingNames, alertMarks;
        bool helpShown;

        public void Bind(PlayerModeSwitcher modeSwitcher, ShipLandingInteraction shipLanding, params Behaviour[] shipOnly)
        {
            switcher = modeSwitcher;
            landing = shipLanding;
            shipOnlyHuds = shipOnly;
        }

        void Awake()
        {
            if (!switcher) return;
            respawn = switcher.GetComponent<PlayerFootRespawn>();
            if (switcher.Character) health = switcher.Character.GetComponent<Health>();
            if (switcher.Character) food = switcher.Character.GetComponent<FoodEater>();
            if (switcher.Character) pistolAmmo = switcher.Character.GetComponent<PistolAmmo>();
            if (switcher.Character) bombs = switcher.Character.GetComponent<BombThrower>();
            if (switcher.Character) smoke = switcher.Character.GetComponent<SmokeBomb>();
            if (switcher.Character) combat = switcher.Character.GetComponent<CharacterCombat>();
            if (switcher.Character) stamina = switcher.Character.GetComponent<Stamina>();
        }

        void OnEnable()
        {
            if (!switcher) return;
            switcher.ModeChanged += OnModeChanged;
            if (switcher.Character) switcher.Character.RescuedFromWater += OnRescued;
            if (health) health.Damaged += OnHurt;
            if (respawn) respawn.Respawned += OnRespawned;
            if (food)
            {
                food.Ate += OnAte;
                food.Refused += OnFoodRefused;
                food.Packed += OnFoodPacked;
            }
            if (pistolAmmo)
            {
                pistolAmmo.Empty += OnNoCartridges;
                pistolAmmo.Packed += OnCartridgesPacked;
            }
            if (bombs)
            {
                bombs.Refused += OnBombRefused;
                bombs.Packed += OnBombsPacked;
            }
            if (smoke)
            {
                smoke.Used += OnSmokeUsed;
                smoke.Refused += OnSmokeRefused;
                smoke.Packed += OnSmokePacked;
            }
            if (combat)
            {
                combat.Blocked += OnBlocked;
                combat.GuardBroken += OnGuardBroken;
            }
            CharacterCombat.Struck += OnStruck;
        }

        void OnDisable()
        {
            if (!switcher) return;
            switcher.ModeChanged -= OnModeChanged;
            if (switcher.Character) switcher.Character.RescuedFromWater -= OnRescued;
            if (health) health.Damaged -= OnHurt;
            if (respawn) respawn.Respawned -= OnRespawned;
            if (food)
            {
                food.Ate -= OnAte;
                food.Refused -= OnFoodRefused;
                food.Packed -= OnFoodPacked;
            }
            if (pistolAmmo)
            {
                pistolAmmo.Empty -= OnNoCartridges;
                pistolAmmo.Packed -= OnCartridgesPacked;
            }
            if (bombs)
            {
                bombs.Refused -= OnBombRefused;
                bombs.Packed -= OnBombsPacked;
            }
            if (smoke)
            {
                smoke.Used -= OnSmokeUsed;
                smoke.Refused -= OnSmokeRefused;
                smoke.Packed -= OnSmokePacked;
            }
            if (combat)
            {
                combat.Blocked -= OnBlocked;
                combat.GuardBroken -= OnGuardBroken;
            }
            CharacterCombat.Struck -= OnStruck;
            HideAll();
        }

        void OnModeChanged(bool onFoot)
        {
            foreach (var hud in shipOnlyHuds) if (hud) hud.enabled = !onFoot;
            if (onFoot && !cameraHintShown)
            {
                cameraHintShown = true;
                cameraHintUntil = Time.time + cameraHintSeconds;
            }
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cameraHintShown = false;

        void OnRescued() => rescuedUntil = Time.time + 3f;
        void OnHurt(DamageInfo info) { if (hud) hud.Flash(); }
        void OnRespawned() => respawnedUntil = Time.time + 3.5f;

        void OnAte(float healed)
        {
            var meal = food ? food.LastEaten : null;
            string what = !meal || meal == food.Food ? "uma ração" : meal.displayName.ToLowerInvariant();
            FoodFeed($"+{healed:0} vida  <size=80%>comeu {what}</size>", HudColors.Good);
        }
        void OnFoodRefused(string reason) => FoodFeed($"Não deu para comer: {reason}", HudColors.Muted);
        void OnFoodPacked(int n) => FoodFeed($"Levou {n} {(n == 1 ? "ração" : "rações")} na mochila  <size=80%>R para comer</size>", HudColors.Cream);

        // Só o aparar avisa (cada golpe na guarda avisando viraria enxurrada).
        void OnBlocked(Vector3 point, bool parried)
        {
            if (parried) FoodFeed("<b>Aparou!</b>  Ele ficou atordoado: solte a guarda e ataque", HudColors.Gold);
        }

        // Adaga: golpe pelas costas avisa (no máximo um aviso a cada 2 s, o combo inteiro pode pegar de costas).
        float backstabFeedUntil;
        void OnStruck(CharacterCombat.StrikeReport report)
        {
            if (report.attacker != combat || (report.flags & CharacterCombat.StrikeFlags.Backstab) == 0 || Time.time < backstabFeedUntil) return;
            backstabFeedUntil = Time.time + 2f;
            FoodFeed($"<b>Pelas costas!</b>  {report.damage:0} de dano", HudColors.Gold);
        }

        void OnGuardBroken() => FoodFeed("<b>Guarda quebrada!</b>  Sem fôlego: recue ou role", HudColors.Bad);

        void OnBombRefused(string reason) => FoodFeed($"Não deu para lançar: {reason}", HudColors.Muted);
        void OnSmokeUsed(Vector3 at) => FoodFeed("<b>Fumaça!</b>  <size=80%>dentro dela ninguém te vê; quem ficou preso nela não enxerga</size>", HudColors.Cream);
        void OnSmokeRefused(string reason) => FoodFeed($"Sem fumaça: {reason}", HudColors.Muted);
        void OnSmokePacked(int n) => FoodFeed($"Levou {n} {(n == 1 ? "bomba de fumaça" : "bombas de fumaça")} na mochila  <size=80%>B para estourar</size>", HudColors.Cream);
        void OnBombsPacked(int n) => FoodFeed($"Levou {n} {(n == 1 ? "bomba" : "bombas")} na mochila  <size=80%>G para lançar</size>", HudColors.Cream);

        void OnNoCartridges(Pirates.Economy.ItemDefinition item) =>
            FoodFeed($"<b>Sem {AmmoName(item)}!</b>  <size=80%>compre no mercado ou faça na Forja (o cutelo é o 1)</size>", HudColors.Bad);
        void OnCartridgesPacked(Pirates.Economy.ItemDefinition item, int n) =>
            FoodFeed($"Levou {n} {(item ? item.displayName.ToLowerInvariant() : "cartuchos")} na mochila", HudColors.Cream);

        static string AmmoName(Pirates.Economy.ItemDefinition item) => item ? item.displayName.ToLowerInvariant() : "munição";

        static void FoodFeed(string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(GameHud.FeedKind.Pickup, text, color);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;

            card = Hud.Card(paper: false);
            card.AddToClassList("hud-status");
            whereText = Hud.Text("", "hud-card__title");
            card.Add(whereText);
            campBox = Hud.Box("hud-camps");
            card.Add(campBox);
            card.Add(Hud.Text("<b>H</b>  controles", "hud-hint"));
            card.style.display = DisplayStyle.None;
            hud.TopLeft.Add(card);

            // Capitão: medalhão com o retrato (o mesmo do painel da fama), nome e patente, vida e fôlego.
            player = Hud.Box("hud-player");
            medal = Hud.Box("hud-player__medal");
            medalLetter = Hud.Text("", "hud-player__letter");
            medal.Add(medalLetter);
            var plate = Hud.Box("hud-card", "hud-card--wood", "hud-player__plate");
            var head = Hud.Box("hud-player__head");
            nameText = Hud.Text("", "hud-player__name");
            rankText = Hud.Text("", "hud-player__rank");
            head.Add(nameText);
            head.Add(rankText);
            plate.Add(head);
            lifeBar = new HudBar("hud-bar--life");
            lifeBar.SetColor(LifeColor);
            lifeText = Hud.Text("", "hud-bar__value");
            lifeBar.Add(lifeText);
            plate.Add(lifeBar);
            // Fôlego (pacote A, passo 6): rolar e segurar a guarda gastam.
            staminaBar = new HudBar("hud-bar--stamina");
            staminaBar.style.display = DisplayStyle.None;
            plate.Add(staminaBar);
            player.Add(plate);
            player.Add(medal);
            player.style.display = DisplayStyle.None;
            hud.BottomLeft.Add(player);
            if (TryGetComponent(out FameHUD fame)) portrait = fame.Portrait;
            if (portrait) medal.style.backgroundImage = new StyleBackground(portrait);

            // Armas e itens: o cartão da arma na mão em cima, os encaixes embaixo.
            loadout = Hud.Box("hud-loadout__box");
            var currentCard = Hud.Box("hud-card", "hud-card--wood", "hud-loadout__current");
            var info = Hud.Box("hud-loadout__info");
            currentAmmo = Hud.Text("", "hud-loadout__ammo");
            currentName = Hud.Text("", "hud-loadout__name");
            currentState = Hud.Text("", "hud-loadout__state");
            info.Add(currentAmmo);
            info.Add(currentName);
            info.Add(currentState);
            currentIcon = Hud.Box("hud-loadout__icon");
            currentCard.Add(info);
            currentCard.Add(currentIcon);
            reloadBar = new HudBar("hud-bar--reload");
            reloadBar.SetColor(HudColors.Warn);
            reloadBar.style.display = DisplayStyle.None;
            currentCard.Add(reloadBar);
            slotRow = Hud.Box("hud-loadout__slots");
            loadout.Add(currentCard);
            loadout.Add(slotRow);
            loadout.style.display = DisplayStyle.None;
            hud.Loadout.Add(loadout);

            help = Hud.Card(paper: true, "A pé");
            help.AddToClassList("hud-help");
            help.Add(Hud.Text(
                "<b>WASD</b>   andar     <b>Shift</b>   correr     <b>Espaço</b>   pular     <b>Ctrl</b>   rolar (esquiva, gasta fôlego)     <b>V</b>   chutar (derruba)\n" +
                "<b>Botão esquerdo</b>   atacar (mira no cursor)\n" +
                "<b>Botão direito (segurar)</b>   pistola/bacamarte: mirar (anel onde a bala para), o esquerdo atira na hora · cutelo: <b>guarda</b> (gasta fôlego; golpe de frente vira arranhão; erguida na hora do golpe, <b>apara</b>, devolve fôlego e o pirata fica atordoado)\n" +
                "<b>G (segurar)</b>   mirar a bomba (arco e área da explosão): soltar lança, botão direito cancela     <b>B</b>   bomba de fumaça (no pé)\n" +
                "<b>T (segurar)</b>   roda de comando dos marujos: o mouse escolhe a ordem, soltar dá (ir até lá / atacar: aponte e clique)\n" +
                "<b>1 / 2 / 3</b>   cutelo / pistola / bacamarte (comprado no mercado)\n" +
                "<b>Q / E</b>  ou  <b>botão do meio + mouse</b>   girar câmera     <b>Scroll</b>   zoom\n" +
                "<b>F</b>   entrar nas casas do porto · perto do bote, voltar ao navio\n" +
                "<b>R</b>   comer uma ração (cura; o capitão leva 5 do porão ao desembarcar)" +
                "\n<b>I</b>   inventário: trocar a lâmina     <b>J</b>   diário: missões e História" +
                (CaptainFame.Current ? "     <b>C</b>   fama do capitão" : "") +
                (FishJournal.Current ? "     <b>P</b>   diário do pescador" : ""), "hud-help__text"));
            help.style.display = DisplayStyle.None;
            hud.TopLeft.Add(help);

            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            cameraHint = new HudPrompt();
            cameraHint.AddToClassList("hud-prompt--hint");
            hud.Prompts.Add(cameraHint);
            banner = new HudBanner();
            hud.Banners.Add(banner);
            landingNames = new WorldLabelPool(hud.World);
            alertMarks = new WorldLabelPool(hud.World, "hud-alert");
            return true;
        }

        void HideAll()
        {
            if (card == null) return;
            Hud.Show(card, false);
            Hud.Show(player, false);
            Hud.Show(loadout, false);
            Hud.Show(help, false);
            helpShown = false;
            prompt.Hide();
            cameraHint.Hide();
            banner.Hide();
            landingNames.HideAll();
            alertMarks.HideAll();
        }

        void Update()
        {
            if (!switcher) return;
            if (card == null && !Build()) return;
            if (switcher.IsOnFoot) UpdateOnFoot();
            else UpdateOnShip();
        }

        void UpdateOnShip()
        {
            cameraHint.Hide();
            Hud.Show(card, false);
            Hud.Show(player, false);
            Hud.Show(loadout, false);
            Hud.Show(help, false);
            helpShown = false;
            banner.Hide();
            alertMarks.HideAll();
            UpdateLandingLabels();
            if (!landing || !landing.Nearby)
            {
                prompt.Hide();
                return;
            }
            bool tooFast = landing.TooFast;
            if (landingPrompt.Stale(landing.Nearby, HudDraw.Flag(tooFast), tooFast ? HudDraw.Tenths(landing.SpeedKnots) : 0))
                landingPrompt.Text = tooFast
                    ? $"Recolha as velas para desembarcar  <size=80%>({landing.SpeedKnots:0.0} / {landing.MaxSpeedKnots:0} nós)</size>"
                    : $"Desembarcar: {landing.Nearby.DisplayName}";
            prompt.Show(tooFast ? null : "F", landingPrompt.Text);
        }

        void UpdateOnFoot()
        {
            landingNames.HideAll();
            var character = switcher.Character;
            if (!combat) combat = character.GetComponent<CharacterCombat>();
            UpdateAlertMarks();

            // Casa do porto aberta: a janela dela ocupa o meio da tela; o painel daqui sairia por cima.
            if (!buildingVisitor) character.TryGetComponent(out buildingVisitor);
            bool inside = buildingVisitor && buildingVisitor.IsOpen;
            Hud.Show(card, !inside);
            // Painel aberto (fama, equipamento) ou conversa: o capitão e as armas saem da frente.
            bool panel = inside || ScreenPanel.Open;
            Hud.Show(player, !panel);
            Hud.Show(loadout, !panel);
            bool wantHelp = hud.HelpVisible && !inside;
            if (wantHelp != helpShown)
            {
                helpShown = wantHelp;
                Hud.Show(help, wantHelp);
            }
            if (inside)
            {
                prompt.Hide();
                banner.Hide();
                return;
            }

            if (where.Stale(switcher.Landing)) where.Text = switcher.Landing ? switcher.Landing.DisplayName : "Em terra";
            Hud.Set(whereText, where.Text);
            UpdatePlayer();
            if (combat) UpdateLoadout();
            UpdateCamps(character.transform.position);

            if (respawn && respawn.IsDown)
            {
                if (banner1.Stale(HudDraw.Tenths(respawn.RespawnIn))) banner1.Text = $"acordando na praia em {respawn.RespawnIn:0.0} s";
                banner.Show("Você foi derrotado", banner1.Text);
            }
            else if (Time.time < respawnedUntil) banner.Show("Você acordou na praia", "com a vida cheia");
            else if (Time.time < rescuedUntil) banner.Show("Você caiu no mar", "e voltou para a praia");
            else banner.Hide();

            if (Time.time < cameraHintUntil)
                cameraHint.Show(null, "<b>Botão do meio</b> + mouse: girar e inclinar a câmera   ·   <b>Q / E</b>: girar   ·   <b>H</b>: controles");
            else cameraHint.Hide();

            if (switcher.CanEmbark) prompt.Show("F", "Voltar ao navio");
            else if (character.InDeepWater) prompt.Show(null, "Água funda: não dá para seguir");
            else prompt.Hide();
        }

        static readonly Color LifeColor = new(0.8f, 0.17f, 0.14f), LifeLow = new(1f, 0.32f, 0.22f);

        void UpdatePlayer()
        {
            var fame = CaptainFame.Current;
            string name = fame ? fame.CaptainName : "Capitão";
            int rank = fame ? fame.Rank : -1;
            if (playerLine.Stale(name)) playerLine.Text = name;
            Hud.Set(nameText, playerLine.Text);
            if (rankLine.Stale(rank)) rankLine.Text = fame ? fame.RankTitle(rank) : "";
            Hud.Set(rankText, rankLine.Text);
            if (!portrait && medalLetter.text.Length == 0 && name.Length > 0) Hud.Set(medalLetter, name.Substring(0, 1).ToUpperInvariant());
            if (health)
            {
                lifeBar.Set(health.Normalized);
                // Pouca vida: a barra pisca mais clara.
                bool low = health.Normalized < 0.3f && !health.IsDead;
                lifeBar.SetColor(low && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f ? LifeLow : LifeColor);
                if (life.Stale(HudDraw.Units(health.Current), HudDraw.Units(health.Max), HudDraw.Flag(health.Invulnerable)))
                    life.Text = health.Invulnerable ? $"{health.Current:0} / {health.Max:0}  <color=#ffe08a>invencível</color>" : $"{health.Current:0} / {health.Max:0}";
                Hud.Set(lifeText, life.Text);
            }
            if (stamina)
            {
                Hud.Show(staminaBar, true);
                staminaBar.Set(stamina.Normalized);
                // Faltou fôlego (rolar sem ter): pisca vermelho por 0,6 s; abaixo do custo do rolamento, amarela.
                bool blink = Time.time - stamina.LastDenied < 0.6f && (int)((Time.time - stamina.LastDenied) * 10f) % 2 == 0;
                staminaBar.SetColor(blink ? HudColors.Bad : stamina.CanDodge ? HudColors.Good : HudColors.Warn);
            }
        }

        /// <summary>Cartão da arma na mão e encaixes: refaz os encaixes só quando muda o que há (arma comprada, lâmina trocada).</summary>
        void UpdateLoadout()
        {
            long signature = combat.WeaponsVersion * 1_000_000L + combat.Weapons.Count * 1000L + (food && food.Food ? 1 : 0) + (bombs && bombs.Bomb ? 2 : 0)
                + (smoke && smoke.Item ? 500 : 0);
            for (int i = 0; i < combat.Weapons.Count && i < 16; i++) if (combat.IsAvailable(i)) signature += 4L << i;
            if (signature != slotSignature)
            {
                slotSignature = signature;
                BuildSlots();
            }
            foreach (var slot in slots)
            {
                int count;
                bool on = false, empty = false;
                if (slot.weapon >= 0)
                {
                    var weapon = combat.Weapons[slot.weapon];
                    on = slot.weapon == combat.CurrentIndex;
                    empty = combat.IsEmpty(slot.weapon) && !on;
                    count = weapon && weapon.kind == WeaponKind.Firearm && combat.Ammo != null ? Mathf.Min(999, combat.AmmoFor(weapon)) : -1;
                }
                else count = slot.food ? food.Count : slot.smoke ? smoke.Count : bombs.Count;
                if (on != slot.on)
                {
                    slot.on = on;
                    slot.root.EnableInClassList("hud-slot--on", on);
                }
                if (empty != slot.empty)
                {
                    slot.empty = empty;
                    slot.root.EnableInClassList("hud-slot--empty", empty);
                }
                bool none = count == 0;
                if (none != slot.none)
                {
                    slot.none = none;
                    slot.root.EnableInClassList("hud-slot--none", none);
                }
                if (count != slot.lastCount)
                {
                    slot.lastCount = count;
                    Hud.Set(slot.count, count < 0 ? "" : count.ToString());
                }
            }
            UpdateCurrent();
        }

        void BuildSlots()
        {
            slotRow.Clear();
            slots.Clear();
            for (int i = 0; i < combat.Weapons.Count; i++)
            {
                if (!combat.IsAvailable(i) || !combat.Weapons[i]) continue;
                var slot = MakeSlot((i + 1).ToString(), combat.Weapons[i].icon, combat.Weapons[i].displayName);
                slot.weapon = i;
            }
            if (food && food.Food) MakeSlot("R", food.Food.icon, food.Food.displayName).food = true;
            if (bombs && bombs.Bomb) MakeSlot("G", bombs.Bomb.icon, bombs.Bomb.displayName).bomb = true;
            if (smoke && smoke.Item) MakeSlot("B", smoke.Item.icon, smoke.Item.displayName).smoke = true;
        }

        Slot MakeSlot(string key, Texture2D icon, string name)
        {
            var slot = new Slot
            {
                root = Hud.Box("hud-slot"),
                icon = Hud.Box("hud-slot__icon"),
                key = Hud.Text(key, "hud-slot__key"),
                count = Hud.Text("", "hud-slot__count"),
            };
            if (icon) slot.icon.style.backgroundImage = new StyleBackground(icon);
            else slot.icon.Add(Hud.Text(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1), "hud-slot__letter"));
            slot.root.Add(slot.icon);
            slot.root.Add(slot.key);
            slot.root.Add(slot.count);
            slotRow.Add(slot.root);
            slots.Add(slot);
            return slot;
        }

        /// <summary>A arma na mão: ícone grande, nome, munição (arma de fogo: na arma / na mochila) e o estado.</summary>
        void UpdateCurrent()
        {
            var weapon = combat.Current;
            if (weapon && weapon != shownWeapon)
            {
                shownWeapon = weapon;
                currentIcon.style.backgroundImage = weapon.icon ? new StyleBackground(weapon.icon) : new StyleBackground();
                Hud.Set(currentName, weapon.displayName);
            }
            weapon = shownWeapon;
            if (!weapon) return;
            bool firearm = weapon.kind == WeaponKind.Firearm;
            bool inHand = combat.Current == weapon;
            int pack = firearm && combat.Ammo != null ? Mathf.Min(999, combat.AmmoFor(weapon)) : -1;
            int loaded = inHand ? combat.ShotsLeft : 0;
            if (currentAmmoLine.Stale(weapon, firearm ? loaded * 1000L + pack : -1))
                currentAmmoLine.Text = !firearm ? "" : pack >= 0 ? $"{loaded}<size=60%> / {pack}</size>" : loaded.ToString();
            Hud.Set(currentAmmo, currentAmmoLine.Text);
            Hud.Show(currentAmmo, firearm);

            int state;
            string ammoName = firearm && pistolAmmo ? AmmoName(pistolAmmo.AmmoItem(weapon)) : "cartuchos";
            if (!inHand) state = 9;
            else if (!firearm) state = combat.RiposteOpen ? 1 : combat.IsBlocking ? 2 : Time.time < combat.GuardLockedUntil ? 3 : 0;
            else state = pack == 0 && loaded <= 0 ? 4 : combat.IsReloading ? 5 : combat.IsAiming ? 6 : 7;
            if (currentStateLine.Stale(state, state == 5 ? HudDraw.Tenths(combat.ReloadRemaining) : 0))
                currentStateLine.Text = state switch
                {
                    1 => "<color=#ffd36b>contra-ataque!</color>",
                    2 => "<color=#9be37f>em guarda</color>",
                    3 => "<color=#ff8a70>guarda quebrada</color>",
                    4 => $"<color=#ff8a70>sem {ammoName}</color>",
                    5 => $"<color=#ffb066>recarregando {combat.ReloadRemaining:0.0} s</color>",
                    6 => "<color=#9be37f>mirando</color>",
                    7 => "<color=#9be37f>carregada</color>",
                    9 => "<color=#c8b89a>trocando...</color>",
                    _ => "<color=#c8b89a>pronta</color>",
                };
            Hud.Set(currentState, currentStateLine.Text);
            bool reloading = state == 5 && weapon.reloadTime > 0f;
            Hud.Show(reloadBar, reloading);
            if (reloading) reloadBar.Set(1f - combat.ReloadRemaining / weapon.reloadTime);
        }

        void UpdateCamps(Vector3 me)
        {
            if (campLines.Length != EnemyCamp.Active.Count)
            {
                campLines = new CachedText[EnemyCamp.Active.Count];
                foreach (var l in campLabels) l.RemoveFromHierarchy();
                campLabels = new Label[EnemyCamp.Active.Count];
                for (int i = 0; i < campLabels.Length; i++)
                {
                    campLabels[i] = Hud.Text("", "hud-camp");
                    campBox.Add(campLabels[i]);
                }
            }
            for (int i = 0; i < EnemyCamp.Active.Count; i++)
            {
                var camp = EnemyCamp.Active[i];
                // Só os grupos por perto (o mundo tem vários: Caveira, postos, Ilha Oeste, forte da Norte).
                bool near = (camp.Center - me).sqrMagnitude <= CampListRange * CampListRange;
                Hud.Show(campLabels[i], near);
                if (!near) continue;
                if (campLines[i].Stale(camp, camp.Alive * 1000L + camp.Total, HudDraw.Flag(camp.IsCleared) * 2 + HudDraw.Flag(camp.InCombat)))
                {
                    string status = camp.IsCleared ? "<color=#9be37f>derrotado!</color>"
                        : $"{camp.Alive} / {camp.Total} piratas{(camp.InCombat ? "  <color=#ff8a70>em combate</color>" : "")}";
                    campLines[i].Text = $"<color=#ffd36b>{camp.DisplayName}</color>   {status}";
                }
                Hud.Set(campLabels[i], campLines[i].Text);
            }
        }

        /// <summary>"!" vermelho sobre o pirata logo depois que ele te vê.</summary>
        void UpdateAlertMarks()
        {
            if (!alertMarks.Begin()) return;
            for (int i = 0; i < EnemyFootAI.Active.Count; i++)
            {
                var ai = EnemyFootAI.Active[i];
                // Golpeando: o aviso de golpe ("!"/"!!", CryptWarningHUD) fica no lugar deste.
                if (ai.IsDead || !ai.IsAlerted || Time.time - ai.AlertedAt > alertMarkSeconds || ai.ImpactIn >= 0f) continue;
                alertMarks.Place(ai.transform.position + Vector3.up * 2.4f, "", Color.white);
            }
            alertMarks.End();
        }

        void UpdateLandingLabels()
        {
            if (!landingNames.Begin()) return;
            var cam = Camera.main;
            float range2 = labelRange * labelRange;
            for (int i = 0; i < LandingPoint.Active.Count; i++)
            {
                var point = LandingPoint.Active[i];
                // Píer do porto: só se desce atracando (o rótulo é o do porto).
                if (point.ShipRange <= 0f) continue;
                Vector3 pos = point.transform.position + Vector3.up * 6f;
                if ((pos - cam.transform.position).sqrMagnitude > range2) continue;
                if (!landingLabels.TryGetValue(point, out var text))
                    landingLabels[point] = text = $"{point.DisplayName}\n<size=70%>desembarque</size>";
                landingNames.Place(pos, text, HudColors.Cream);
            }
            landingNames.End();
        }
    }
}
