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
    /// HUD do desembarque (UI Toolkit): no navio, nome dos desembarques e o aviso "[F] Desembarcar"; a pé, painel com o
    /// lugar, a vida, as armas e os bandos por perto (ajuda dos controles com H), clarão vermelho ao levar golpe, "!" sobre
    /// os piratas que te viram, faixa de derrota e avisos (voltar ao navio, água funda). Em terra esconde os HUDs só de navio.
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
        CachedText where, life, weapons, banner1, landingPrompt, foodLine, bombLine;
        FoodEater food;
        PistolAmmo pistolAmmo;
        BombThrower bombs;
        CachedText[] campLines = System.Array.Empty<CachedText>();
        readonly System.Collections.Generic.Dictionary<LandingPoint, string> landingLabels = new();

        GameHud hud;
        VisualElement card, help, campBox;
        Label whereText, lifeText, weaponText, foodText, bombText;
        VisualElement staminaRow;
        HudBar lifeBar, staminaBar;
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
            if (combat)
            {
                combat.Blocked += OnBlocked;
                combat.GuardBroken += OnGuardBroken;
            }
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
            if (combat)
            {
                combat.Blocked -= OnBlocked;
                combat.GuardBroken -= OnGuardBroken;
            }
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

        void OnAte(float healed) => FoodFeed($"+{healed:0} vida  <size=80%>comeu uma ração</size>", HudColors.Good);
        void OnFoodRefused(string reason) => FoodFeed($"Não deu para comer: {reason}", HudColors.Muted);
        void OnFoodPacked(int n) => FoodFeed($"Levou {n} {(n == 1 ? "ração" : "rações")} na mochila  <size=80%>R para comer</size>", HudColors.Cream);

        // Só o aparar avisa (cada golpe na guarda avisando viraria enxurrada).
        void OnBlocked(Vector3 point, bool parried)
        {
            if (parried) FoodFeed("<b>Aparou!</b>  Ele ficou atordoado: solte a guarda e ataque", HudColors.Gold);
        }

        void OnGuardBroken() => FoodFeed("<b>Guarda quebrada!</b>  Sem fôlego: recue ou role", HudColors.Bad);

        void OnBombRefused(string reason) => FoodFeed($"Não deu para lançar: {reason}", HudColors.Muted);
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
            var lifeRow = Hud.Row();
            lifeRow.Add(Hud.Text("Vida", "hud-status__name"));
            lifeBar = new HudBar("hud-bar--grow");
            lifeRow.Add(lifeBar);
            lifeText = Hud.Text("", "hud-status__value");
            lifeRow.Add(lifeText);
            card.Add(lifeRow);
            // Fôlego (pacote A, passo 6): barra fina embaixo da vida; rolar e segurar a guarda gastam.
            staminaRow = Hud.Row();
            staminaRow.Add(Hud.Text("Fôlego", "hud-status__name"));
            staminaBar = new HudBar("hud-bar--grow");
            staminaBar.AddToClassList("hud-bar--thin");
            staminaRow.Add(staminaBar);
            staminaRow.style.display = DisplayStyle.None;
            card.Add(staminaRow);
            foodText = Hud.Text("", "hud-weapons");
            foodText.style.display = DisplayStyle.None;
            card.Add(foodText);
            bombText = Hud.Text("", "hud-weapons");
            bombText.style.display = DisplayStyle.None;
            card.Add(bombText);
            weaponText = Hud.Text("", "hud-weapons");
            card.Add(weaponText);
            campBox = Hud.Box("hud-camps");
            card.Add(campBox);
            card.Add(Hud.Text("<b>H</b>  controles", "hud-hint"));
            card.style.display = DisplayStyle.None;
            hud.TopLeft.Add(card);

            help = Hud.Card(paper: true, "A pé");
            help.AddToClassList("hud-help");
            help.Add(Hud.Text(
                "<b>WASD</b>   andar     <b>Shift</b>   correr     <b>Espaço</b>   pular     <b>Ctrl</b>   rolar (esquiva, gasta fôlego)     <b>V</b>   chutar (derruba)\n" +
                "<b>Botão esquerdo</b>   atacar (mira no cursor)\n" +
                "<b>Botão direito (segurar)</b>   pistola/bacamarte: mirar (laser), o esquerdo atira na hora · cutelo: <b>guarda</b> (gasta fôlego; golpe de frente vira arranhão; erguida na hora do golpe, <b>apara</b>, devolve fôlego e o pirata fica atordoado)\n" +
                "<b>G (segurar)</b>   mirar a bomba (arco e área da explosão): soltar lança, botão direito cancela\n" +
                "<b>1 / 2 / 3</b>   cutelo / pistola / bacamarte (comprado no mercado)\n" +
                "<b>Q / E</b>  ou  <b>botão do meio + mouse</b>   girar câmera     <b>Scroll</b>   zoom\n" +
                "<b>F</b>   entrar nas casas do porto · perto do bote, voltar ao navio\n" +
                "<b>R</b>   comer uma ração (cura; o capitão leva 5 do porão ao desembarcar)" +
                (CaptainFame.Current ? "\n<b>C</b>   fama do capitão" : ""), "hud-help__text"));
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
            if (health)
            {
                lifeBar.SetDanger(health.Normalized, greenWhenFull: true);
                if (life.Stale(HudDraw.Units(health.Current), HudDraw.Units(health.Max), HudDraw.Flag(health.Invulnerable)))
                    life.Text = $"{health.Current:0} / {health.Max:0}{(health.Invulnerable ? "  <color=#9be37f>invencível</color>" : "")}";
                Hud.Set(lifeText, life.Text);
            }
            if (stamina)
            {
                Hud.Show(staminaRow, true);
                staminaBar.Set(stamina.Normalized);
                // Faltou fôlego (rolar sem ter): pisca vermelho por 0,6 s; abaixo do custo do rolamento, amarela.
                bool blink = Time.time - stamina.LastDenied < 0.6f && (int)((Time.time - stamina.LastDenied) * 10f) % 2 == 0;
                staminaBar.SetColor(blink ? HudColors.Bad : stamina.CanDodge ? HudColors.Good : HudColors.Warn);
            }
            if (food && food.Food)
            {
                int n = food.Count;
                if (foodLine.Stale(n)) foodLine.Text = n > 0
                    ? $"<b>R</b>  comer ração  <size=90%>({n} na mochila, +{food.HealAmount:0} vida)</size>"
                    : "<color=#c8b89a><b>R</b>  sem ração na mochila</color>";
                Hud.Set(foodText, foodLine.Text);
                Hud.Show(foodText, true);
            }
            if (bombs && bombs.Bomb)
            {
                int n = bombs.Count;
                if (bombLine.Stale(n)) bombLine.Text = n > 0
                    ? $"<b>G</b>  segure e solte: bomba  <size=90%>({n} na mochila)</size>"
                    : "<color=#c8b89a><b>G</b>  sem bomba na mochila</color>";
                Hud.Set(bombText, bombLine.Text);
                Hud.Show(bombText, true);
            }
            Hud.Show(weaponText, combat);
            if (combat) Hud.Set(weaponText, WeaponLine());
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

        string WeaponLine()
        {
            var current = combat.Current;
            bool firearm = current && current.kind == WeaponKind.Firearm;
            long state = !firearm ? (combat.IsBlocking ? 5 : 0) : combat.IsReloading ? 1 + HudDraw.Tenths(combat.ReloadRemaining) * 4 : combat.IsAiming ? 2 : 3;
            if (firearm && combat.Ammo != null) state = state * 1000 + Mathf.Min(999, combat.AmmoFor(current));
            // Armas liberadas entram na conta (o bacamarte aparece ao ser comprado).
            long available = 0;
            for (int i = 0; i < combat.Weapons.Count && i < 16; i++)
            {
                if (combat.IsAvailable(i)) available |= 1L << i;
                if (combat.IsEmpty(i)) available |= 1L << (i + 16);
            }
            if (!weapons.Stale(current, combat.CurrentIndex * 1000L + combat.Weapons.Count + available * 100_000L, state)) return weapons.Text;
            return weapons.Text = BuildWeaponLine();
        }

        string BuildWeaponLine()
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < combat.Weapons.Count; i++)
            {
                if (!combat.IsAvailable(i)) continue;
                string name = $"<b>{i + 1}</b> {combat.Weapons[i].displayName}";
                // Arma de fogo guardada sem bala: a recarga só anda com ela na mão.
                if (i != combat.CurrentIndex && combat.IsEmpty(i)) name += " <size=80%><color=#ff9a66>(vazia)</color></size>";
                parts.Add(i == combat.CurrentIndex ? $"<color=#ffd36b>{name}</color>" : $"<color=#b9a98c>{name}</color>");
            }
            var current = combat.Current;
            bool firearm = current && current.kind == WeaponKind.Firearm;
            int left = firearm ? combat.AmmoFor(current) : 0;
            string ammoName = firearm && pistolAmmo ? AmmoName(pistolAmmo.AmmoItem(current)) : "cartuchos";
            string state = !firearm ? (combat.IsBlocking ? "\n<color=#9be37f>em guarda</color>" : "")
                : left <= 0 ? $"\n<color=#ff8a70>sem {ammoName}</color>  <size=85%>(mercado ou Forja)</size>"
                : combat.IsReloading ? $"\n<color=#ff9a66>recarregando {combat.ReloadRemaining:0.0} s</color>"
                : combat.IsAiming ? "\n<color=#9be37f>mirando</color>" : "\n<color=#9be37f>carregada</color>";
            if (firearm && combat.Ammo != null && left > 0) state += $"  <size=85%>· {left} {ammoName}</size>";
            return string.Join("     ", parts) + state;
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
                if (ai.IsDead || !ai.IsAlerted || Time.time - ai.AlertedAt > alertMarkSeconds) continue;
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
