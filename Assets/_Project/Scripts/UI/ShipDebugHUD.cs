using Pirates.Combat;
using Pirates.Core;
using Pirates.Progression;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Painel do navio (UI Toolkit, canto de cima à esquerda): casco, tripulação (beliches ocupados), velas, velocidade e leme, vento
    /// (seta com a proa para cima, força e quanto ele ajuda ou segura o navio naquele rumo), e cada bateria de canhões (pronta, mirando, recarregando), munição (1/2/3) e carpinteiro (R). Baixas na tripulação viram
    /// um aviso (somadas por ~1 s). Ajuda dos controles com H; faixa "Seu navio afundou" quando o casco zera.
    /// Some em terra (o FootHUD desliga este componente).
    /// </summary>
    public class ShipDebugHUD : MonoBehaviour
    {
        [SerializeField] ShipController ship;
        [SerializeField] string hudTitle = "Navegação — Protótipo 01";

        GameHud hud;
        VisualElement card, help;
        HudBanner sunk;
        HudBar hullBar, crewBar;
        Label hullText, crewText, sailText, speedText, carpenterText, windText;
        VisualElement windRow, windArrow;
        CachedText windLine;
        float shownWindAngle = float.NaN;
        AmmoChip[] ammoChips;
        int shownAmmo = -1;
        bool[] ammoAnnounced;
        VisualElement[] sailPips;
        VisualElement rudderKnob;
        CannonBattery[] batteries;
        BatteryRow[] rows;
        Health health;
        ShipCrew crew;
        ShipAmmo ammo;
        ShipCarpenter carpenter;
        CachedText hull, crewCount, speed, carpenterLine;
        int pendingLost;
        float lostFlushAt;
        bool hullHooked;
        int shownSail = -1;
        float shownRudder = float.NaN;
        bool helpShown;

        /// <summary>Ficha de uma munição: tecla, nome e cargas (∞ na comum).</summary>
        class AmmoChip
        {
            public VisualElement root;
            public Label count;
            public CachedText countText;
        }

        class BatteryRow
        {
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
            if (card == null) return;
            Hud.Show(card, visible);
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

            card = Hud.Card(paper: false);
            card.AddToClassList("hud-status");
            if (!health) ship.TryGetComponent(out health);
            if (health)
            {
                var row = Hud.Row();
                row.Add(Hud.Text("Casco", "hud-status__name"));
                hullBar = new HudBar("hud-bar--grow");
                row.Add(hullBar);
                hullText = Hud.Text("", "hud-status__value");
                row.Add(hullText);
                card.Add(row);
            }

            if (ship.TryGetComponent(out crew))
            {
                var row = Hud.Row();
                row.Add(Hud.Text("Tripulação", "hud-status__name"));
                crewBar = new HudBar("hud-bar--grow");
                crewBar.SetColor(HudColors.Cream);   // casco avariado já fica amarelo
                row.Add(crewBar);
                crewText = Hud.Text("", "hud-status__value");
                row.Add(crewText);
                card.Add(row);
                crew.Lost += OnCrewLost;
            }

            var sails = Hud.Row();
            sails.Add(Hud.Text("Velas", "hud-status__name"));
            var pips = Hud.Box("hud-pips");
            sailPips = new VisualElement[Mathf.Max(1, ship.Stats.sailLevels.Length - 1)];
            for (int i = 0; i < sailPips.Length; i++)
            {
                sailPips[i] = Hud.Box("hud-pip");
                pips.Add(sailPips[i]);
            }
            sails.Add(pips);
            sailText = Hud.Text("", "hud-status__value", "hud-status__value--wide");
            sails.Add(sailText);
            card.Add(sails);

            var helm = Hud.Row();
            helm.Add(Hud.Text("Leme", "hud-status__name"));
            var track = Hud.Box("hud-rudder");
            track.Add(Hud.Box("hud-rudder__center"));
            rudderKnob = Hud.Box("hud-rudder__knob");
            track.Add(rudderKnob);
            helm.Add(track);
            speedText = Hud.Text("", "hud-status__value", "hud-status__value--wide");
            helm.Add(speedText);
            card.Add(helm);

            windRow = Hud.Row();
            windRow.Add(Hud.Text("Vento", "hud-status__name"));
            var dial = Hud.Box("hud-wind__dial");
            windArrow = Hud.Box("hud-wind__arrow");
            windArrow.generateVisualContent += DrawWindArrow;
            dial.Add(windArrow);
            windRow.Add(dial);
            windText = Hud.Text("", "hud-status__value", "hud-status__value--wide");
            windRow.Add(windText);
            card.Add(windRow);

            batteries = ship.GetComponentsInChildren<CannonBattery>();
            rows = new BatteryRow[batteries.Length];
            for (int i = 0; i < batteries.Length; i++)
            {
                bool port = batteries[i].Side == BroadsideSide.Port;
                var row = Hud.Row("hud-battery");
                row.Add(Hud.Key(port ? "Q" : "E"));
                row.Add(Hud.Text(port ? "Bombordo" : "Estibordo", "hud-status__name"));
                var r = new BatteryRow { state = Hud.Text("", "hud-battery__state"), reload = new HudBar("hud-bar--thin") };
                r.reload.SetColor(HudColors.Gold);
                row.Add(r.state);
                row.Add(r.reload);
                card.Add(row);
                rows[i] = r;
            }
            if (ship.TryGetComponent(out ammo) && ammo.Slots.Count > 0)
            {
                var box = Hud.Box("hud-ammo");
                var title = Hud.Row();
                title.Add(Hud.Text("Munição", "hud-status__name"));
                title.Add(Hud.Text(ammo.Slots.Count > 1 ? "troque com 1 · 2 · 3" : "troque com 1 · 2", "hud-ammo__hint"));
                box.Add(title);
                var chips = Hud.Box("hud-ammo__chips");
                ammoChips = new AmmoChip[ammo.Slots.Count + 1];
                ammoAnnounced = new bool[ammo.Slots.Count + 1];
                for (int i = 0; i < ammoChips.Length; i++)
                {
                    var chip = new AmmoChip { root = Hud.Box("hud-ammo__chip") };
                    var key = Hud.Key((i + 1).ToString());
                    key.AddToClassList("hud-ammo__key");
                    chip.root.Add(key);
                    chip.root.Add(Hud.Text(i == 0 ? "Comum" : ammo.Slots[i - 1].ammo ? ammo.Slots[i - 1].ammo.displayName : "?", "hud-ammo__name"));
                    chip.count = Hud.Text("", "hud-ammo__count");
                    chip.root.Add(chip.count);
                    chips.Add(chip.root);
                    ammoChips[i] = chip;
                    // Já tinha carga ao montar o painel (jogo carregado): não anuncia de novo.
                    ammoAnnounced[i] = i == 0 || ammo.Count(i) > 0;
                }
                box.Add(chips);
                card.Add(box);
                shownAmmo = ammo.Selected;
                ammo.OutOfAmmo += OnOutOfAmmo;
            }
            else ammo = null;
            if (ship.TryGetComponent(out carpenter) && carpenter.Available)
            {
                var row = Hud.Row("hud-battery");
                row.Add(Hud.Key("R"));
                row.Add(Hud.Text("Carpinteiro", "hud-status__name", "hud-status__name--wide"));
                carpenterText = Hud.Text("", "hud-battery__state");
                row.Add(carpenterText);
                card.Add(row);
                carpenter.Finished += OnRepairFinished;
                carpenter.Resting += OnCarpenterResting;
            }
            else carpenter = null;
            card.Add(Hud.Text("<b>H</b>  controles", "hud-hint"));
            hud.TopLeft.Add(card);

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

        /// <summary>Seta do vento (para onde ele sopra, com a proa para cima), força e o efeito nesse rumo com as velas cheias.</summary>
        void RefreshWind()
        {
            bool has = Services.TryGet(out IWind wind);
            Hud.Show(windRow, has);
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
                windLine.Text = percent == 0 ? $"{force}, {side}" : $"{force}, {side} {(percent > 0 ? "+" : "−")}{Mathf.Abs(percent)}%";
            }
            Hud.Set(windText, windLine.Text);
        }

        /// <summary>Seta apontando para cima (o elemento gira); desenhada uma vez, na cor do texto.</summary>
        static void DrawWindArrow(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            float w = rect.width, h = rect.height;
            var paint = context.painter2D;
            paint.fillColor = HudColors.Cream;
            paint.BeginPath();
            paint.MoveTo(new Vector2(w * 0.5f, h * 0.06f));
            paint.LineTo(new Vector2(w * 0.86f, h * 0.48f));
            paint.LineTo(new Vector2(w * 0.61f, h * 0.48f));
            paint.LineTo(new Vector2(w * 0.61f, h * 0.94f));
            paint.LineTo(new Vector2(w * 0.39f, h * 0.94f));
            paint.LineTo(new Vector2(w * 0.39f, h * 0.48f));
            paint.LineTo(new Vector2(w * 0.14f, h * 0.48f));
            paint.ClosePath();
            paint.Fill();
        }

        void Update()
        {
            if (!ship || !ship.Stats) return;
            if (card == null && !Build()) return;

            if (health)
            {
                hullBar.SetDanger(health.Normalized, greenWhenFull: true);
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

            if (ship.SailIndex != shownSail)
            {
                shownSail = ship.SailIndex;
                for (int i = 0; i < sailPips.Length; i++) sailPips[i].EnableInClassList("hud-pip--on", i < shownSail);
                Hud.Set(sailText, ship.SailLabel);
            }
            if (speed.Stale(HudDraw.Tenths(ship.SpeedKnots))) speed.Text = $"{ship.SpeedKnots:0.0} nós";
            Hud.Set(speedText, speed.Text);
            float rudder = Mathf.Round(ship.Rudder * 50f) / 50f;
            if (rudder != shownRudder)
            {
                shownRudder = rudder;
                rudderKnob.style.left = Length.Percent((rudder + 1f) * 50f);
            }

            RefreshWind();
            for (int i = 0; i < batteries.Length; i++) RefreshBattery(batteries[i], rows[i]);
            if (ammo) RefreshAmmo();
            if (carpenter) RefreshCarpenter();

            if (hud.HelpVisible != helpShown)
            {
                helpShown = hud.HelpVisible;
                Hud.Show(help, helpShown);
            }
        }

        void RefreshAmmo()
        {
            for (int i = 0; i < ammoChips.Length; i++)
            {
                var chip = ammoChips[i];
                int count = ammo.Count(i);
                if (chip.countText.Stale(count))
                {
                    chip.countText.Text = count < 0 ? "sem limite" : count == 1 ? "1 carga" : $"{count} cargas";
                    Hud.Set(chip.count, chip.countText.Text);
                }
                chip.root.EnableInClassList("hud-ammo__chip--on", ammo.Selected == i);
                chip.root.EnableInClassList("hud-ammo__chip--empty", count == 0);
                // Primeira carga daquela munição a bordo: avisa a tecla (troca de munição era difícil de descobrir).
                if (count > 0 && !ammoAnnounced[i])
                {
                    ammoAnnounced[i] = true;
                    var slot = ammo.Slots[i - 1];
                    if (slot.ammo) hud.Feed(GameHud.FeedKind.Notice, $"Nova munição a bordo: aperte {i + 1} para carregar {slot.ammo.displayName.ToLowerInvariant()}", HudColors.Gold);
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
            if (!carpenterLine.Stale((long)carpenter.State, HudDraw.Flag(carpenter.IsOn), rest)) return;
            carpenterLine.Text = !carpenter.IsOn ? rest > 0 ? $"<color=#8f8a80>descansando {rest} s</color>" : "pronto (R)" : carpenter.State switch
            {
                ShipCarpenter.Status.Working => "<color=#9be37f>consertando o casco</color>",
                ShipCarpenter.Status.InCombat => "<color=#ffd36b>parado: em combate</color>",
                ShipCarpenter.Status.NoWood => "<color=#ff8a70>sem madeira no porão</color>",
                ShipCarpenter.Status.NoCrew => $"<color=#ff8a70>precisa de {crew.Settings.carpenterMinCrew} tripulantes</color>",
                _ => "desligado",
            };
            Hud.Set(carpenterText, carpenterLine.Text);
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

        /// <summary>Navio novo (outro casco, outros canhões): desmonta o painel; o próximo Update monta de novo.</summary>
        void OnHullChanged()
        {
            if (card == null) return;
            if (crew) crew.Lost -= OnCrewLost;
            if (ammo) ammo.OutOfAmmo -= OnOutOfAmmo;
            if (carpenter)
            {
                carpenter.Finished -= OnRepairFinished;
                carpenter.Resting -= OnCarpenterResting;
            }
            carpenterLine = default;
            shownAmmo = -1;
            card.RemoveFromHierarchy();
            help.RemoveFromHierarchy();
            sunk.RemoveFromHierarchy();
            card = null;
            hull = default;
            crewCount = default;
            speed = default;
            shownSail = -1;
            shownRudder = float.NaN;
            helpShown = false;
        }

        void OnDestroy()
        {
            if (crew) crew.Lost -= OnCrewLost;
            if (ammo) ammo.OutOfAmmo -= OnOutOfAmmo;
            if (carpenter)
            {
                carpenter.Finished -= OnRepairFinished;
                carpenter.Resting -= OnCarpenterResting;
            }
            if (hullHooked && ship) ship.HullChanged -= OnHullChanged;
        }

        static void RefreshBattery(CannonBattery battery, BatteryRow row)
        {
            long detail = battery.State switch
            {
                BatteryState.Ready => 0,
                BatteryState.Aiming => HudDraw.Units(battery.AimRange) * 2 + HudDraw.Flag(battery.AssistLocked),
                _ => 0,
            };
            if (row.text.Stale((long)battery.State, detail))
            {
                row.text.Text = battery.State switch
                {
                    BatteryState.Ready => "<color=#9be37f>pronto</color>",
                    BatteryState.Aiming => $"mirando {battery.AimRange:0} m" + (battery.AssistLocked ? "  <color=#ffd36b>travado</color>" : ""),
                    _ => "recarregando",
                };
                Hud.Set(row.state, row.text.Text);
            }
            bool reloading = battery.State is not (BatteryState.Ready or BatteryState.Aiming);
            Hud.Show(row.reload, reloading);
            if (reloading && battery.Stats)
                row.reload.Set(1f - battery.ReloadRemaining / Mathf.Max(0.01f, battery.Stats.reloadTime));
        }

        string HelpText()
        {
            bool guns = ship.GetComponentsInChildren<CannonBattery>().Length > 0;
            return "<b>W / S</b>   subir / baixar velas\n<b>A / D</b>   leme\n<b>Botão direito ou do meio + mouse</b>   girar e inclinar câmera\n<b>Scroll</b>   zoom" +
                   (guns ? "\n<b>Q / E</b>   segurar mira, soltar dispara (bombordo / estibordo)" : "") +
                   (ship.TryGetComponent(out ShipAmmo a) && a.Slots.Count > 0 ? "\n<b>1 / 2 / 3</b>   munição: comum / corrente (rasga velas) / metralha (de perto)" : "") +
                   (ship.TryGetComponent(out ShipCarpenter c) && c.Available ? "\n<b>R</b>   carpinteiro: conserta no mar, fora de combate, gastando madeira" : "") +
                   (CaptainFame.Current ? "\n<b>C</b>   fama do capitão: patente, bônus e o que falta para subir" : "") +
                   (guns ? "\n<b>F5</b>   reiniciar a cena" : "");
        }
    }
}
