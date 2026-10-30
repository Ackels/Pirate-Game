using Pirates.Base;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// HUD do navio (UI Toolkit). Refeita em 21/10 a pedido do Jhonata no jeito da HUD a pé de RPG:
    /// <list type="bullet">
    /// <item><b>Navegação</b> (em cima à esquerda): rosa dos ventos com o navio de proa para cima e a seta do vento girando em volta, as
    /// velas em bolinhas, a velocidade em nós e quanto o vento ajuda ou segura nesse rumo.</item>
    /// <item><b>O navio</b> (embaixo à esquerda): medalhão com o timão, que gira com o leme, a classe do navio, o casco numa barra grande
    /// (pisca com pouco casco) e a tripulação numa barra fina.</item>
    /// <item><b>A artilharia</b> (embaixo à direita): os cartões Q bombordo / E estibordo (pronto, mirando, recarregando com barra) e os
    /// encaixes 1/2/3 da munição e R do carpinteiro (madeira no porão).</item>
    /// </list>
    /// Baixas na tripulação viram um aviso (somadas por ~1 s). Ajuda dos controles com H; faixa "Seu navio afundou" quando o casco zera.
    /// Some em terra (o FootHUD desliga este componente); os cantos de baixo saem com um painel aberto.
    /// </summary>
    public class ShipDebugHUD : MonoBehaviour
    {
        static readonly Color HullColor = new(0.86f, 0.22f, 0.16f), CrewColor = new(0.93f, 0.86f, 0.7f);
        static readonly Color Brass = new(0.84f, 0.65f, 0.28f), WindColor = new(0.97f, 0.91f, 0.79f);

        [SerializeField] ShipController ship;
        [SerializeField] string hudTitle = "Navegação — Protótipo 01";

        GameHud hud;
        VisualElement nav, plate, artillery, help;
        HudBanner sunk;
        HudBar hullBar, crewBar;
        Label hullText, crewText, sailText, speedText, carpenterText, windText, shipName, shipSub;
        VisualElement windArrow, wheel, crewRow;
        CachedText windLine;
        float shownWindAngle = float.NaN;
        AmmoSlot[] ammoSlots;
        AmmoSlot carpenterSlot;
        int shownAmmo = -1;
        bool[] ammoAnnounced;
        VisualElement[] sailPips;
        CannonBattery[] batteries;
        BatteryCard[] cards;
        Health health;
        ShipCrew crew;
        ShipAmmo ammo;
        ShipCarpenter carpenter;
        PlayerShipClass shipClass;
        Inventory hold;
        HarborVisitor visitor;
        CachedText hull, crewCount, speed, carpenterLine, classLine;
        int pendingLost;
        float lostFlushAt;
        bool hullHooked;
        int shownSail = -1;
        float shownRudder = float.NaN;
        bool helpShown, cornersShown = true;

        /// <summary>Encaixe da munição (ou do carpinteiro): ícone, tecla e a quantidade embaixo.</summary>
        class AmmoSlot
        {
            public VisualElement root;
            public Label count;
            public CachedText countText;
        }

        class BatteryCard
        {
            public VisualElement root;
            public Label state;
            public HudBar reload;
            public CachedText text;
        }

        public void Bind(ShipController target, string title)
        {
            ship = target;
            hudTitle = title;
        }

        void OnEnable() => SetVisible(true);
        void OnDisable() => SetVisible(false);

        void SetVisible(bool visible)
        {
            if (nav == null) return;
            Hud.Show(nav, visible);
            Hud.Show(plate, visible && cornersShown);
            Hud.Show(artillery, visible && cornersShown && batteries.Length > 0);
            if (!visible)
            {
                Hud.Show(help, false);
                helpShown = false;
                sunk.Hide();
            }
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            if (!hullHooked)
            {
                ship.HullChanged += OnHullChanged;
                hullHooked = true;
            }
            if (!health) ship.TryGetComponent(out health);
            ship.TryGetComponent(out crew);
            ship.TryGetComponent(out shipClass);
            ship.TryGetComponent(out hold);
            ship.TryGetComponent(out visitor);
            BuildNavigation();
            BuildPlate();
            BuildArtillery();

            help = Hud.Card(paper: true, hudTitle);
            help.AddToClassList("hud-help");
            help.Add(Hud.Text(HelpText(), "hud-help__text"));
            help.style.display = DisplayStyle.None;
            hud.TopLeft.Add(help);

            sunk = new HudBanner();
            hud.Banners.Add(sunk);
            SetVisible(isActiveAndEnabled);
            return true;
        }

        // ---------- Navegação (em cima à esquerda) ----------

        void BuildNavigation()
        {
            nav = Hud.Box("hud-card", "hud-card--wood", "hud-nav");
            var dial = Hud.Box("hud-nav__dial");
            dial.Add(new ShipIcon(ShipIcon.Kind.Compass, Brass));
            windArrow = new WindArrow(WindColor);
            dial.Add(windArrow);
            nav.Add(dial);

            var info = Hud.Box("hud-nav__info");
            speedText = Hud.Text("", "hud-nav__speed");
            info.Add(speedText);
            var sails = Hud.Row("hud-nav__sails");
            sails.Add(Hud.Text("Velas", "hud-nav__label"));
            var pips = Hud.Box("hud-pips");
            sailPips = new VisualElement[Mathf.Max(1, ship.Stats.sailLevels.Length - 1)];
            for (int i = 0; i < sailPips.Length; i++)
            {
                sailPips[i] = Hud.Box("hud-pip");
                pips.Add(sailPips[i]);
            }
            sails.Add(pips);
            sailText = Hud.Text("", "hud-nav__value");
            sails.Add(sailText);
            info.Add(sails);
            windText = Hud.Text("", "hud-nav__wind");
            info.Add(windText);
            info.Add(Hud.Text("<b>H</b>  controles", "hud-hint"));
            nav.Add(info);
            hud.TopLeft.Add(nav);
        }

        // ---------- O navio (embaixo à esquerda) ----------

        void BuildPlate()
        {
            plate = Hud.Box("hud-player", "hud-ship");
            var medal = Hud.Box("hud-player__medal", "hud-ship__medal");
            wheel = new ShipIcon(ShipIcon.Kind.Wheel, Brass);
            wheel.AddToClassList("hud-ship__wheel");
            medal.Add(wheel);
            var card = Hud.Box("hud-card", "hud-card--wood", "hud-player__plate");
            var head = Hud.Box("hud-player__head");
            shipName = Hud.Text("", "hud-player__name");
            shipSub = Hud.Text("", "hud-player__rank");
            head.Add(shipName);
            head.Add(shipSub);
            card.Add(head);
            hullBar = new HudBar("hud-bar--life");
            hullBar.SetColor(HullColor);
            hullText = Hud.Text("", "hud-bar__value");
            hullBar.Add(hullText);
            Hud.Show(hullBar, health);
            card.Add(hullBar);
            crewRow = Hud.Row("hud-ship__crew");
            crewRow.Add(Hud.Text("Tripulação", "hud-ship__crew-label"));
            crewBar = new HudBar("hud-ship__crew-bar");
            crewBar.SetColor(CrewColor);
            crewRow.Add(crewBar);
            crewText = Hud.Text("", "hud-ship__crew-value");
            crewRow.Add(crewText);
            Hud.Show(crewRow, crew);
            card.Add(crewRow);
            plate.Add(card);
            plate.Add(medal);
            hud.BottomLeft.Add(plate);
            if (crew) crew.Lost += OnCrewLost;
        }

        // ---------- A artilharia (embaixo à direita) ----------

        void BuildArtillery()
        {
            artillery = Hud.Box("hud-loadout__box", "hud-guns");
            batteries = ship.GetComponentsInChildren<CannonBattery>();
            // Bombordo à esquerda, estibordo à direita (como os lados do navio na tela).
            System.Array.Sort(batteries, (a, b) => (a.Side == BroadsideSide.Port ? 0 : 1).CompareTo(b.Side == BroadsideSide.Port ? 0 : 1));
            var row = Hud.Row("hud-guns__row");
            cards = new BatteryCard[batteries.Length];
            for (int i = 0; i < batteries.Length; i++)
            {
                bool port = batteries[i].Side == BroadsideSide.Port;
                var c = new BatteryCard { root = Hud.Box("hud-card", "hud-card--wood", "hud-gun") };
                var top = Hud.Row("hud-gun__top");
                top.Add(Hud.Key(port ? "Q" : "E"));
                top.Add(Hud.Text(port ? "Bombordo" : "Estibordo", "hud-gun__name"));
                c.root.Add(top);
                var icon = new ShipIcon(ShipIcon.Kind.Cannon, new Color(0.66f, 0.66f, 0.7f), mirrored: port);
                icon.AddToClassList("hud-gun__icon");
                c.root.Add(icon);
                c.state = Hud.Text("", "hud-gun__state");
                c.root.Add(c.state);
                c.reload = new HudBar("hud-bar--reload");
                c.reload.SetColor(HudColors.Warn);
                c.root.Add(c.reload);
                row.Add(c.root);
                cards[i] = c;
            }
            artillery.Add(row);

            var slots = Hud.Box("hud-loadout__slots");
            if (ship.TryGetComponent(out ammo) && ammo.Slots.Count > 0)
            {
                ammoSlots = new AmmoSlot[ammo.Slots.Count + 1];
                ammoAnnounced = new bool[ammo.Slots.Count + 1];
                for (int i = 0; i < ammoSlots.Length; i++)
                {
                    var item = i == 0 ? null : ammo.Slots[i - 1].item;
                    string name = i == 0 ? "Comum" : ammo.Slots[i - 1].ammo ? ammo.Slots[i - 1].ammo.displayName : "?";
                    ammoSlots[i] = Slot(slots, (i + 1).ToString(), item ? item.icon : null, i == 0, name);
                    // Já tinha carga ao montar o painel (jogo carregado): não anuncia de novo.
                    ammoAnnounced[i] = i == 0 || ammo.Count(i) > 0;
                }
                shownAmmo = ammo.Selected;
                ammo.OutOfAmmo += OnOutOfAmmo;
            }
            else ammo = null;
            if (ship.TryGetComponent(out carpenter) && carpenter.Available && crew)
            {
                var wood = crew.Settings ? crew.Settings.carpenterWood : null;
                carpenterSlot = Slot(slots, "R", wood ? wood.icon : null, false, "Carpinteiro");
                carpenter.Finished += OnRepairFinished;
                carpenter.Resting += OnCarpenterResting;
            }
            else carpenter = null;
            artillery.Add(slots);
            carpenterText = Hud.Text("", "hud-guns__note");
            Hud.Show(carpenterText, false);
            artillery.Add(carpenterText);
            hud.Loadout.Add(artillery);
        }

        static AmmoSlot Slot(VisualElement parent, string key, Texture2D icon, bool ball, string name)
        {
            var slot = new AmmoSlot { root = Hud.Box("hud-slot") };
            var box = Hud.Box("hud-slot__icon");
            if (icon) box.style.backgroundImage = new StyleBackground(icon);
            else if (ball)
            {
                var b = new ShipIcon(ShipIcon.Kind.Ball, new Color(0.52f, 0.52f, 0.56f));
                b.AddToClassList("hud-slot__drawn");
                box.Add(b);
            }
            else box.Add(Hud.Text(name.Substring(0, 1), "hud-slot__letter"));
            slot.root.Add(box);
            slot.root.Add(Hud.Text(key, "hud-slot__key"));
            slot.count = Hud.Text("", "hud-slot__count");
            slot.root.Add(slot.count);
            slot.root.tooltip = name;
            parent.Add(slot.root);
            return slot;
        }

        // ---------- Atualização ----------

        void Update()
        {
            if (!ship || !ship.Stats) return;
            if (nav == null && !Build()) return;

            // Painel aberto (inventário, diário, fama) ou o porto na frente: os cantos de baixo saem.
            bool corners = !ScreenPanel.Open && !(visitor && visitor.IsOpen);
            if (corners != cornersShown)
            {
                cornersShown = corners;
                SetVisible(isActiveAndEnabled);
            }

            UpdatePlate();
            UpdateNavigation();
            for (int i = 0; i < batteries.Length; i++) RefreshBattery(batteries[i], cards[i]);
            if (ammo) RefreshAmmo();
            if (carpenter) RefreshCarpenter();

            if (hud.HelpVisible != helpShown)
            {
                helpShown = hud.HelpVisible;
                Hud.Show(help, helpShown);
            }
        }

        void UpdatePlate()
        {
            var cls = shipClass ? shipClass.Current : null;
            string captain = CaptainFame.Current ? CaptainFame.Current.CaptainName : null;
            if (classLine.Stale(cls, captain == null ? 0 : captain.GetHashCode()))
            {
                classLine.Text = cls ? cls.displayName : "Seu navio";
                Hud.Set(shipName, classLine.Text);
                Hud.Set(shipSub, captain != null ? $"de {captain}" : "");
            }
            if (health)
            {
                hullBar.Set(health.Normalized);
                // Casco baixo: a barra pisca, como a vida a pé.
                bool low = health.Normalized < 0.3f && !health.IsDead;
                hullBar.style.opacity = low ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
                if (hull.Stale(HudDraw.Units(health.Current), HudDraw.Units(health.Max))) hull.Text = $"{health.Current:0} / {health.Max:0}";
                Hud.Set(hullText, hull.Text);
                if (health.IsDead) sunk.Show("Seu navio afundou!", "F5 para recomeçar");
                else sunk.Hide();
            }
            if (crew)
            {
                if (crewCount.Stale(crew.Count, crew.Berths))
                {
                    crewCount.Text = $"{crew.Count} / {crew.Berths}";
                    crewBar.Set(crew.Fraction);
                }
                Hud.Set(crewText, crewCount.Text);
                if (pendingLost > 0 && Time.time >= lostFlushAt)
                {
                    hud.Feed(GameHud.FeedKind.Pickup, pendingLost == 1 ? "Baixa a bordo: -1 tripulante" : $"Baixas a bordo: -{pendingLost} tripulantes", HudColors.Bad);
                    pendingLost = 0;
                }
            }
            // O timão gira com o leme (leme todo = um terço de volta; o raio vermelho mostra quanto).
            float rudder = Mathf.Round(ship.Rudder * 50f) / 50f;
            if (rudder != shownRudder)
            {
                shownRudder = rudder;
                wheel.style.rotate = new Rotate(new Angle(rudder * 120f, AngleUnit.Degree));
            }
        }

        void UpdateNavigation()
        {
            if (ship.SailIndex != shownSail)
            {
                shownSail = ship.SailIndex;
                for (int i = 0; i < sailPips.Length; i++) sailPips[i].EnableInClassList("hud-pip--on", i < shownSail);
                Hud.Set(sailText, ship.SailLabel);
            }
            if (speed.Stale(HudDraw.Tenths(ship.SpeedKnots))) speed.Text = $"{ship.SpeedKnots:0.0} <size=60%>nós</size>";
            Hud.Set(speedText, speed.Text);
            RefreshWind();
        }

        /// <summary>Seta do vento (para onde ele sopra, com a proa para cima), força e o efeito nesse rumo com as velas cheias.</summary>
        void RefreshWind()
        {
            bool has = Services.TryGet(out IWind wind);
            Hud.Show(windArrow, has);
            Hud.Show(windText, has);
            if (!has) return;
            var forward = Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up);
            float angle = Mathf.Round(Vector3.SignedAngle(forward, wind.Direction, Vector3.up) / 3f) * 3f;
            if (angle != shownWindAngle)
            {
                shownWindAngle = angle;
                windArrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }
            float off = Mathf.Abs(angle);
            int point = off < 35f ? 0 : off < 75f ? 1 : off < 110f ? 2 : off < 150f ? 3 : 4;
            int strength = wind.Strength < 0.4f ? 0 : wind.Strength < 0.7f ? 1 : 2;
            int percent = Mathf.RoundToInt((wind.SailFactor(forward) - 1f) * 100f);
            if (windLine.Stale(point, strength, percent))
            {
                string force = strength == 0 ? "fraco" : strength == 1 ? "moderado" : "forte";
                string side = point switch { 0 => "por trás", 1 => "a favor", 2 => "de lado", 3 => "meio contra", _ => "contra" };
                string effect = percent == 0 ? "" : percent > 0 ? $"  <color=#9be37f><b>+{percent}%</b></color>" : $"  <color=#ff8a70><b>−{-percent}%</b></color>";
                windLine.Text = $"Vento {force}, {side}{effect}";
            }
            Hud.Set(windText, windLine.Text);
        }

        void RefreshAmmo()
        {
            for (int i = 0; i < ammoSlots.Length; i++)
            {
                var slot = ammoSlots[i];
                int count = ammo.Count(i);
                if (slot.countText.Stale(count))
                {
                    slot.countText.Text = count < 0 ? "∞" : count.ToString();
                    Hud.Set(slot.count, count < 0 ? "" : slot.countText.Text);
                }
                slot.root.EnableInClassList("hud-slot--on", ammo.Selected == i);
                slot.root.EnableInClassList("hud-slot--none", count == 0);
                // Primeira carga daquela munição a bordo: avisa a tecla (troca de munição era difícil de descobrir).
                if (count > 0 && !ammoAnnounced[i])
                {
                    ammoAnnounced[i] = true;
                    var s = ammo.Slots[i - 1];
                    if (s.ammo) hud.Feed(GameHud.FeedKind.Notice, $"Nova munição a bordo: aperte {i + 1} para carregar {s.ammo.displayName.ToLowerInvariant()}", HudColors.Gold);
                }
            }
            if (ammo.Selected != shownAmmo)
            {
                shownAmmo = ammo.Selected;
                string name = shownAmmo == 0 ? "bala comum" : ammo.Current ? ammo.Current.displayName.ToLowerInvariant() : "?";
                hud.Feed(GameHud.FeedKind.Pickup, $"Canhões com {name}", HudColors.Gold);
            }
        }

        void RefreshCarpenter()
        {
            int rest = Mathf.CeilToInt(carpenter.RestSecondsLeft);
            var wood = crew.Settings ? crew.Settings.carpenterWood : null;
            int woodCount = hold && wood ? hold.Count(wood) : 0;
            if (!carpenterLine.Stale((long)carpenter.State * 2 + HudDraw.Flag(carpenter.IsOn), rest, woodCount)) return;
            Hud.Set(carpenterSlot.count, woodCount.ToString());
            carpenterSlot.root.EnableInClassList("hud-slot--on", carpenter.IsOn && carpenter.State == ShipCarpenter.Status.Working);
            carpenterSlot.root.EnableInClassList("hud-slot--empty", carpenter.IsOn && carpenter.State != ShipCarpenter.Status.Working);
            carpenterSlot.root.EnableInClassList("hud-slot--none", !carpenter.IsOn && rest > 0);
            // Pronto e desligado: a linha some (o encaixe R já diz tudo).
            carpenterLine.Text = !carpenter.IsOn ? rest > 0 ? $"<color=#bdb6a8>Carpinteiro descansando {rest} s</color>" : null : carpenter.State switch
            {
                ShipCarpenter.Status.Working => "<color=#9be37f>Carpinteiro consertando o casco</color>",
                ShipCarpenter.Status.InCombat => "<color=#ffd36b>Carpinteiro parado: em combate</color>",
                ShipCarpenter.Status.NoWood => "<color=#ff8a70>Carpinteiro: sem madeira no porão</color>",
                ShipCarpenter.Status.NoCrew => $"<color=#ff8a70>Carpinteiro: precisa de {crew.Settings.carpenterMinCrew} tripulantes</color>",
                _ => null,
            };
            Hud.Show(carpenterText, carpenterLine.Text != null);
            if (carpenterLine.Text != null) Hud.Set(carpenterText, carpenterLine.Text);
        }

        void OnOutOfAmmo(ShipAmmo.Slot slot)
        {
            if (hud && slot?.ammo) hud.Feed(GameHud.FeedKind.Pickup, $"Sem cargas de {slot.ammo.displayName.ToLowerInvariant()} no porão", HudColors.Bad);
        }

        void OnCarpenterResting(float seconds)
        {
            if (hud) hud.Feed(GameHud.FeedKind.Pickup, $"Carpinteiro descansando: faltam {Mathf.CeilToInt(seconds)} s", HudColors.Bad);
        }

        void OnRepairFinished()
        {
            if (hud) hud.Feed(GameHud.FeedKind.Pickup, "Carpinteiro: casco consertado", HudColors.Good);
        }

        // Uma bordada acerta várias balas quase juntas: soma as baixas e avisa uma vez só.
        void OnCrewLost(int dead)
        {
            if (pendingLost == 0) lostFlushAt = Time.time + 1f;
            pendingLost += dead;
        }

        /// <summary>Navio novo (outro casco, outros canhões): desmonta a HUD; o próximo Update monta de novo.</summary>
        void OnHullChanged()
        {
            if (nav == null) return;
            Unhook();
            carpenterLine = default;
            classLine = default;
            shownAmmo = -1;
            nav.RemoveFromHierarchy();
            plate.RemoveFromHierarchy();
            artillery.RemoveFromHierarchy();
            help.RemoveFromHierarchy();
            sunk.RemoveFromHierarchy();
            nav = null;
            hull = default;
            crewCount = default;
            speed = default;
            shownSail = -1;
            shownRudder = float.NaN;
            shownWindAngle = float.NaN;
            windLine = default;
            helpShown = false;
            cornersShown = true;
        }

        void Unhook()
        {
            if (crew) crew.Lost -= OnCrewLost;
            if (ammo) ammo.OutOfAmmo -= OnOutOfAmmo;
            if (carpenter)
            {
                carpenter.Finished -= OnRepairFinished;
                carpenter.Resting -= OnCarpenterResting;
            }
        }

        void OnDestroy()
        {
            Unhook();
            if (hullHooked && ship) ship.HullChanged -= OnHullChanged;
        }

        static void RefreshBattery(CannonBattery battery, BatteryCard card)
        {
            long detail = battery.State switch
            {
                BatteryState.Aiming => HudDraw.Units(battery.AimRange) * 2 + HudDraw.Flag(battery.AssistLocked),
                _ => 0,
            };
            if (card.text.Stale((long)battery.State, detail))
            {
                card.text.Text = battery.State switch
                {
                    BatteryState.Ready => "<color=#9be37f>pronto</color>",
                    BatteryState.Aiming => $"<color=#ffd36b>mirando {battery.AimRange:0} m</color>" + (battery.AssistLocked ? "  <b>travado</b>" : ""),
                    _ => "<color=#bdb6a8>recarregando</color>",
                };
                Hud.Set(card.state, card.text.Text);
                card.root.EnableInClassList("hud-gun--ready", battery.State == BatteryState.Ready);
                card.root.EnableInClassList("hud-gun--aiming", battery.State == BatteryState.Aiming);
            }
            bool reloading = battery.State is not (BatteryState.Ready or BatteryState.Aiming);
            Hud.Show(card.reload, reloading);
            if (reloading && battery.Stats)
                card.reload.Set(1f - battery.ReloadRemaining / Mathf.Max(0.01f, battery.Stats.reloadTime));
        }

        string HelpText()
        {
            bool guns = ship.GetComponentsInChildren<CannonBattery>().Length > 0;
            return "<b>W / S</b>   subir / baixar velas\n<b>A / D</b>   leme\n<b>Botão direito ou do meio + mouse</b>   girar e inclinar câmera\n<b>Scroll</b>   zoom" +
                   (guns ? "\n<b>Q / E</b>   segurar mira, soltar dispara (bombordo / estibordo)" : "") +
                   (ship.TryGetComponent(out ShipAmmo a) && a.Slots.Count > 0 ? "\n<b>1 / 2 / 3 / 4</b>   munição: comum / corrente (rasga velas) / metralha (de perto) / incendiária (põe fogo)" : "") +
                   (ship.TryGetComponent(out ShipCarpenter c) && c.Available ? "\n<b>R</b>   carpinteiro: conserta no mar, fora de combate, gastando madeira" : "") +
                   (CaptainFame.Current ? "\n<b>C</b>   fama do capitão: patente, bônus e o que falta para subir" : "") +
                   "\n<b>I</b>   inventário do capitão (lâminas achadas nos baús)     <b>J</b>   diário: missões e História" +
                   (FishJournal.Current ? "\n<b>P</b>   diário do pescador: peixes pescados, recordes e onde achar os outros" : "") +
                   (guns ? "\n<b>F5</b>   reiniciar a cena" : "");
        }
    }
}
