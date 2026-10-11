using System;
using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Menu inicial do mundo (UI Toolkit, em tela cheia por cima do jogo parado): o título "A Coroa de Prata" sobre o último
    /// quadro do prólogo, com Continuar (cinza sem jogo salvo; com o resumo do save), Novo jogo (com save, pede confirmação;
    /// depois o nome do capitão, M10, e o prólogo), Prólogo (rever), Opções (gráficos, áudio, controles: <see cref="GameOptions"/>),
    /// Créditos e Sair. Enquanto está na tela o tempo para, os comandos de jogo ficam desligados e toca a música do menu
    /// (<see cref="TitleScreen"/>). Depois mostra "Jogo salvo" a cada salvamento (ao atracar e ao sair do porto), no canto de baixo.
    /// </summary>
    public class SaveMenuHUD : MonoBehaviour
    {
        enum View { Main, ConfirmNew, Name, Options, Credits, Prologue }
        enum Tab { Graphics, Audio, Controls }

        [SerializeField] GameSave save;
        [SerializeField] PrologueHUD prologue;
        [SerializeField] string title = "A Coroa de Prata";
        [SerializeField] string subtitle = "";
        [Tooltip("Fundo do menu (o último quadro do prólogo).")]
        [SerializeField] Texture2D background;
        [Tooltip("Texto da tela de créditos (rich text). Edite o arquivo para mudar.")]
        [SerializeField] TextAsset credits;
        [SerializeField] float toastSeconds = 2.5f;

        bool paused, peeked;
        bool withTutorial = true;
        View view;
        Tab tab;
        bool dirty;
        TextField nameField;
        SaveData preview;
        string previewError;
        string previewText;   // resumo pronto (montado uma vez ao ler o save)
        string toast;
        bool toastIsError;
        float toastUntil;

        Label toastLabel;
        VisualElement menu, backdrop, panel, content;
        View shownView = (View)(-1);

        static readonly (FullScreenMode mode, string name)[] ScreenModes =
        {
            (FullScreenMode.FullScreenWindow, "Tela cheia"),
            (FullScreenMode.ExclusiveFullScreen, "Tela cheia exclusiva"),
            (FullScreenMode.Windowed, "Janela"),
        };
        static readonly int[] FpsLimits = { 30, 60, 120, 144, 0 };
        static readonly string[] QualityNames = { "Baixa", "Média", "Alta" };
        static readonly string[] QualityNotes =
        {
            "Sem anti-serrilhado, 80% da resolução, sombras mais curtas. Para quando o jogo engasga.",
            "Sem anti-serrilhado e sombras um pouco mais curtas.",
            "Como o jogo foi feito: anti-serrilhado 2× e sombras completas.",
        };

        public void Bind(GameSave gameSave, string menuSubtitle)
        {
            save = gameSave;
            subtitle = menuSubtitle;
        }

        /// <summary>Fundo, créditos e prólogo (gerador).</summary>
        public void Configure(Texture2D menuBackground, TextAsset creditsText, PrologueHUD prologueHud)
        {
            background = menuBackground;
            credits = creditsText;
            prologue = prologueHud;
        }

        /// <summary>Tela mostrada agora (testes): Main, ConfirmNew, Name, Options, Credits ou Prologue.</summary>
        public string CurrentView => view.ToString();

        void OnEnable()
        {
            if (!save) return;
            save.Saved += OnSaved;
            save.Failed += OnFailed;
        }

        void OnDisable()
        {
            if (save)
            {
                save.Saved -= OnSaved;
                save.Failed -= OnFailed;
            }
            if (paused) SetPaused(false);
            if (menu != null) Hud.Show(menu, false);
        }

        void Update()
        {
            bool showMenu = save && save.WaitingForChoice;
            if (showMenu != paused) SetPaused(showMenu);
            UpdateMenu();
            UpdateToast();
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            GameInput.Instance.GameplayEnabled = !value;
            TitleScreen.Showing = value;
            view = View.Main;
            peeked = false;
        }

        void OnSaved(SaveData _) => Show("Jogo salvo", false);

        void OnFailed(string reason) => Show(reason, true);

        void Show(string message, bool error)
        {
            toast = message;
            toastIsError = error;
            toastUntil = Time.unscaledTime + (error ? toastSeconds * 2f : toastSeconds);
            // Erro ao continuar: o menu relê o save (pode ter mudado).
            peeked = false;
            dirty = true;
        }

        // ---------- Menu ----------

        void UpdateMenu()
        {
            if (!paused)
            {
                if (menu != null) Hud.Show(menu, false);
                return;
            }
            if (menu == null && !BuildMenu()) return;
            TitleScreen.Showing = true;
            Hud.Show(menu, view != View.Prologue);
            var hud = GameHud.Current;
            if (hud) hud.TypingText = view == View.Name;
            if (view == View.Prologue) return;

            // Fundo respirando devagar (zoom de ida e volta).
            float s = 1.06f + 0.035f * Mathf.Sin(Time.unscaledTime * 0.08f);
            backdrop.style.scale = new Scale(new Vector3(s, s, 1f));

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && view != View.Main)
            {
                if (view == View.Options) GameOptions.Save();
                view = View.Main;
            }

            if (view == shownView && peeked && !dirty) return;
            shownView = view;
            dirty = false;
            content.Clear();
            nameField = null;
            panel.EnableInClassList("hud-title__panel--side", view == View.Main);
            panel.EnableInClassList("hud-title__panel--wide", view is View.Options or View.Credits);
            switch (view)
            {
                case View.ConfirmNew: BuildConfirm(); break;
                case View.Name: BuildName(); break;
                case View.Options: BuildOptions(); break;
                case View.Credits: BuildCredits(); break;
                default: BuildChoices(); break;
            }
        }

        bool BuildMenu()
        {
            var hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            menu = Hud.Box("hud-title");
            menu.pickingMode = PickingMode.Position;
            backdrop = Hud.Box("hud-title__bg");
            if (background) backdrop.style.backgroundImage = new StyleBackground(background);
            menu.Add(backdrop);
            menu.Add(Hud.Box("hud-title__shade"));
            var heading = Hud.Box("hud-title__heading");
            heading.Add(Hud.Text(title, "hud-title__name"));
            if (!string.IsNullOrEmpty(subtitle)) heading.Add(Hud.Text(subtitle, "hud-title__subtitle"));
            menu.Add(heading);
            panel = Hud.Box("hud-window", "hud-title__panel");
            panel.pickingMode = PickingMode.Position;
            content = Hud.Box("hud-menu__content");
            panel.Add(content);
            menu.Add(panel);
            menu.Add(Hud.Text("O jogo salva sozinho ao atracar no porto e ao sair dele.", "hud-title__note"));
            hud.Menu.Add(menu);
            return true;
        }

        void Go(View next)
        {
            view = next;
            dirty = true;
        }

        void BuildChoices()
        {
            if (!peeked)
            {
                peeked = true;
                save.TryPeek(out preview, out previewError);
                previewText = preview != null ? Describe(preview)
                    : save.HasSave ? $"<color=#a8321f>Não deu para ler o jogo salvo: {previewError}.</color>"
                    : "<i>Nenhum jogo salvo ainda.</i>";
            }
            content.Add(Win.Button("Continuar", () => save.Continue(), preview != null, "hud-button--big"));
            content.Add(Hud.Text(previewText, "hud-title__summary"));
            content.Add(Win.Button("Novo jogo", () => Go(save.HasSave ? View.ConfirmNew : View.Name), true, "hud-button--big"));
            content.Add(Win.Button("Prólogo", () => PlayPrologue(SavedCaptain(), null), prologue && prologue.FrameCount > 0, "hud-button--big"));
            content.Add(Win.Button("Opções", () =>
            {
                tab = Tab.Graphics;
                Go(View.Options);
            }, true, "hud-button--big"));
            content.Add(Win.Button("Créditos", () => Go(View.Credits), true, "hud-button--big"));
            content.Add(Win.Button("Sair", Quit, true, "hud-button--big"));
        }

        void BuildConfirm()
        {
            content.Add(Hud.Text("Começar um jogo novo?", "hud-menu__question"));
            content.Add(Hud.Text("O jogo salvo vai ser apagado: armazém, upgrades, construções e missões voltam ao começo. " +
                                 "(Fica uma cópia em save_anterior.json, caso mude de ideia.)", "hud-menu__summary"));
            var row = Hud.Row("hud-menu__buttons");
            row.Add(Win.Button("Apagar e começar", () => Go(View.Name), true, "hud-button--big"));
            row.Add(Win.Button("Voltar", () => Go(View.Main), true, "hud-button--big"));
            content.Add(row);
        }

        /// <summary>Nome do capitão (M10): aparece no painel da fama (tecla C) e no prólogo. Enter também zarpa.</summary>
        void BuildName()
        {
            content.Add(Hud.Text("Como se chama o capitão?", "hud-menu__question"));
            content.Add(Hud.Text("O nome aparece na história e no painel da fama (tecla C), e cresce com as suas façanhas.", "hud-menu__summary"));
            var fame = CaptainFame.Current;
            nameField = new TextField { maxLength = CaptainFame.MaxNameLength, value = fame ? fame.CaptainName : "Capitão Barbarossa" };
            nameField.AddToClassList("hud-menu__name");
            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) BeginNewGame();
            }, TrickleDown.TrickleDown);
            content.Add(nameField);
            // Tutorial do Velho Bento (11/10): ligado por padrão; quem já conhece o jogo desliga aqui. O botão só troca o texto
            // (remontar a tela apagaria o nome digitado).
            Button tutorial = null;
            tutorial = Win.Button(TutorialLabel(), () =>
            {
                withTutorial = !withTutorial;
                tutorial.text = TutorialLabel();
            }, true, "hud-menu__tutorial");
            content.Add(tutorial);
            var row = Hud.Row("hud-menu__buttons");
            row.Add(Win.Button("Zarpar!", BeginNewGame, true, "hud-button--big"));
            row.Add(Win.Button("Voltar", () => Go(View.Main), true, "hud-button--big"));
            content.Add(row);
            // Já com o cursor no campo (o texto vem selecionado: digitar troca o nome inteiro).
            nameField.schedule.Execute(() =>
            {
                nameField?.Focus();
                nameField?.SelectAll();
            });
        }

        string TutorialLabel() => withTutorial ? "Tutorial: <b>sim</b> (o Velho Bento mostra o porto e o básico)" : "Tutorial: <b>não</b>";

        /// <summary>Jogo novo com ou sem o tutorial (o botão da tela do nome; testes).</summary>
        public bool WithTutorial
        {
            get => withTutorial;
            set => withTutorial = value;
        }

        void BeginNewGame() => BeginNewGame(nameField != null ? nameField.value : null);

        /// <summary>Zarpar: o prólogo com este nome e, no fim (ou ao pular), o jogo novo.</summary>
        public void BeginNewGame(string captainName)
        {
            var hud = GameHud.Current;
            if (hud) hud.TypingText = false;
            PlayPrologue(captainName, () => StartNewGame(captainName));
        }

        /// <summary>Teste pela CLI: começa um jogo novo com este nome sem o prólogo, como se tivesse pulado.</summary>
        public void StartNewGame(string captainName)
        {
            view = View.Main;
            var hud = GameHud.Current;
            if (hud) hud.TypingText = false;
            save.NewGame(captainName, withTutorial);
        }

        /// <summary>Mostra o prólogo; sem <paramref name="then"/>, volta ao menu.</summary>
        public void PlayPrologue(string captainName, Action then)
        {
            if (!prologue)
            {
                then?.Invoke();
                return;
            }
            Go(View.Prologue);
            prologue.Play(captainName, () =>
            {
                Go(View.Main);
                then?.Invoke();
            });
        }

        string SavedCaptain()
        {
            if (preview != null && preview.hasFame && !string.IsNullOrWhiteSpace(preview.captainName)) return preview.captainName;
            var fame = CaptainFame.Current;
            return fame ? fame.CaptainName : null;
        }

        static void Quit()
        {
            GameOptions.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Opções ----------

        void BuildOptions()
        {
            content.Add(Hud.Text("Opções", "hud-menu__question"));
            var tabs = Hud.Row("hud-tabs", "hud-options__tabs");
            AddTab(tabs, "Gráficos", Tab.Graphics);
            AddTab(tabs, "Áudio", Tab.Audio);
            AddTab(tabs, "Controles", Tab.Controls);
            content.Add(tabs);
            var body = Hud.Box("hud-options__body");
            content.Add(body);
            switch (tab)
            {
                case Tab.Audio: BuildAudio(body); break;
                case Tab.Controls: BuildControls(body); break;
                default: BuildGraphics(body); break;
            }
            var row = Hud.Row("hud-menu__buttons");
            row.Add(Win.Button("Padrão", ResetTab, true, "hud-button--big"));
            row.Add(Win.Button("Voltar", () =>
            {
                GameOptions.Save();
                Go(View.Main);
            }, true, "hud-button--big"));
            content.Add(row);
        }

        void AddTab(VisualElement row, string text, Tab which)
        {
            var b = Win.Button(text, () =>
            {
                tab = which;
                dirty = true;
            }, true, "hud-tab");
            if (tab == which) b.AddToClassList("hud-tab--on");
            row.Add(b);
        }

        /// <summary>Linha "Nome   [&lt;]  valor  [&gt;]" com uma nota opcional embaixo.</summary>
        void Stepper(VisualElement parent, string label, string value, Action prev, Action next, bool enabled = true, string note = null)
        {
            var row = Hud.Row("hud-options__row");
            row.Add(Hud.Text(label, "hud-options__label"));
            row.Add(Arrow("<", prev, enabled));
            row.Add(Hud.Text(value, "hud-options__value"));
            row.Add(Arrow(">", next, enabled));
            parent.Add(row);
            if (!string.IsNullOrEmpty(note)) parent.Add(Hud.Text(note, "hud-options__note"));
        }

        Button Arrow(string text, Action change, bool enabled)
        {
            var b = Win.Button(text, () => Changed(change), enabled && change != null, "hud-button--small");
            b.AddToClassList("hud-options__arrow");
            return b;
        }

        void Changed(Action change)
        {
            change?.Invoke();
            GameOptions.Notify();
            dirty = true;
        }

        void BuildGraphics(VisualElement body)
        {
            var o = GameOptions.Current;
            var mode = o.screenMode == GameOptions.DefaultScreenMode ? Screen.fullScreenMode : (FullScreenMode)o.screenMode;
            int m = Array.FindIndex(ScreenModes, x => x.mode == mode);
            if (m < 0) m = mode == FullScreenMode.MaximizedWindow ? 2 : 0;
            bool editor = Application.isEditor;
            Stepper(body, "Modo de tela", ScreenModes[m].name,
                () => o.screenMode = (int)ScreenModes[(m + ScreenModes.Length - 1) % ScreenModes.Length].mode,
                () => o.screenMode = (int)ScreenModes[(m + 1) % ScreenModes.Length].mode, !editor);

            var sizes = Resolutions();
            Vector2Int size = o.width > 0 ? new Vector2Int(o.width, o.height)
                : mode == FullScreenMode.Windowed ? new Vector2Int(Screen.width, Screen.height)
                : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            int r = sizes.FindIndex(x => x == size);
            if (r < 0) r = sizes.Count - 1;
            Stepper(body, "Resolução", $"{size.x} × {size.y}",
                r > 0 ? () => SetSize(o, sizes[r - 1]) : null,
                r < sizes.Count - 1 ? () => SetSize(o, sizes[r + 1]) : null, !editor,
                editor ? "Modo de tela e resolução só mudam no jogo compilado (no editor vale a janela Game)." : null);

            Stepper(body, "VSync", o.vSync ? "Ligado" : "Desligado", () => o.vSync = !o.vSync, () => o.vSync = !o.vSync, true,
                o.vSync ? "Trava na taxa da tela (60 Hz no notebook): sem cortes na imagem e o notebook não esquenta à toa." : null);
            int f = Array.IndexOf(FpsLimits, o.fpsLimit);
            if (f < 0) f = FpsLimits.Length - 1;
            Stepper(body, "Limite de FPS", o.vSync ? "pelo VSync" : FpsLimits[f] == 0 ? "Sem limite" : FpsLimits[f].ToString(),
                f > 0 ? () => o.fpsLimit = FpsLimits[f - 1] : null,
                f < FpsLimits.Length - 1 ? () => o.fpsLimit = FpsLimits[f + 1] : null, !o.vSync);

            int q = (int)o.quality;
            Stepper(body, "Qualidade", QualityNames[q],
                q > 0 ? () => o.quality = (GameOptions.Quality)(q - 1) : null,
                q < 2 ? () => o.quality = (GameOptions.Quality)(q + 1) : null, true, QualityNotes[q]);
        }

        static void SetSize(GameOptions o, Vector2Int size)
        {
            o.width = size.x;
            o.height = size.y;
        }

        static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var res in Screen.resolutions)
            {
                var v = new Vector2Int(res.width, res.height);
                if (v.x >= 800 && !list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        void BuildAudio(VisualElement body)
        {
            var o = GameOptions.Current;
            Volume(body, "Volume geral", () => o.master, v => o.master = v);
            Volume(body, "Música", () => o.music, v => o.music = v);
            Volume(body, "Efeitos (canhões, golpes, tiros)", () => o.effects, v => o.effects = v);
            Volume(body, "Ambiente (mar, vento, chuva)", () => o.ambient, v => o.ambient = v);
            Volume(body, "Interface", () => o.interfaceVolume, v => o.interfaceVolume = v);
        }

        void Volume(VisualElement body, string label, Func<float> get, Action<float> set)
        {
            var o = GameOptions.Current;
            int step = Mathf.RoundToInt(get() * 10f);
            Stepper(body, label, $"{step * 10}%",
                step > 0 ? () => { set((step - 1) / 10f); o.hasAudio = true; } : null,
                step < 10 ? () => { set((step + 1) / 10f); o.hasAudio = true; } : null);
        }

        void BuildControls(VisualElement body)
        {
            var o = GameOptions.Current;
            int s = Mathf.RoundToInt(o.cameraSensitivity * 4f);
            Stepper(body, "Sensibilidade da câmera (mouse)", $"{o.cameraSensitivity:0.00}×",
                s > 1 ? () => o.cameraSensitivity = (s - 1) / 4f : null,
                s < 10 ? () => o.cameraSensitivity = (s + 1) / 4f : null);
            Stepper(body, "Inverter câmera (vertical)", o.invertCamera ? "Sim" : "Não",
                () => o.invertCamera = !o.invertCamera, () => o.invertCamera = !o.invertCamera);

            var keys = Hud.Row("hud-options__keys");
            var ship = Hud.Box("hud-section", "hud-options__keybox");
            ship.Add(Hud.Text("No navio", "hud-options__keytitle"));
            ship.Add(Hud.Text(
                "<b>W / S</b>  velas · <b>A / D</b>  leme\n<b>Q / E</b>  segurar mira, soltar dispara\n<b>1 / 2 / 3</b>  munição\n" +
                "<b>R</b>  carpinteiro · <b>F</b>  atracar, saquear\n<b>Botão direito/meio + mouse</b>  câmera\n<b>Scroll</b>  zoom",
                "hud-options__keys-text"));
            var foot = Hud.Box("hud-section", "hud-options__keybox");
            foot.Add(Hud.Text("A pé", "hud-options__keytitle"));
            foot.Add(Hud.Text(
                "<b>WASD</b>  andar · <b>Shift</b>  correr · <b>Espaço</b>  pular\n<b>Botão esquerdo</b>  atacar · <b>direito</b>  mirar/guarda\n" +
                "<b>Ctrl</b>  rolar · <b>V</b>  chutar · <b>G</b>  bomba\n<b>1 / 2 / 3</b>  armas · <b>R</b>  comer · <b>F</b>  interagir\n" +
                "<b>Q / E</b> ou <b>botão do meio</b>  câmera",
                "hud-options__keys-text"));
            keys.Add(ship);
            keys.Add(foot);
            body.Add(keys);
            body.Add(Hud.Text("<b>H</b> mostra os controles durante o jogo · <b>C</b> fama do capitão · <b>Esc</b> fecha janelas.", "hud-options__note"));
        }

        void ResetTab()
        {
            var o = GameOptions.Current;
            var d = new GameOptions();
            switch (tab)
            {
                case Tab.Audio:
                    o.master = d.master;
                    o.music = d.music;
                    o.effects = d.effects;
                    o.ambient = d.ambient;
                    o.interfaceVolume = d.interfaceVolume;
                    o.hasAudio = true;
                    break;
                case Tab.Controls:
                    o.cameraSensitivity = d.cameraSensitivity;
                    o.invertCamera = d.invertCamera;
                    break;
                default:
                    o.screenMode = d.screenMode;
                    o.width = d.width;
                    o.height = d.height;
                    o.vSync = d.vSync;
                    o.fpsLimit = d.fpsLimit;
                    o.quality = d.quality;
                    break;
            }
            Changed(null);
        }

        // ---------- Créditos ----------

        void BuildCredits()
        {
            content.Add(Hud.Text("Créditos", "hud-menu__question"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("hud-credits");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.Add(Hud.Text(credits ? credits.text : title, "hud-credits__text"));
            content.Add(scroll);
            var row = Hud.Row("hud-menu__buttons");
            row.Add(Win.Button("Voltar", () => Go(View.Main), true, "hud-button--big"));
            content.Add(row);
        }

        /// <summary>"Salvo em 01/10 às 14:32 no Porto da Base", o capitão, o que há no armazém, upgrades, construções e missões.</summary>
        string Describe(SaveData d)
        {
            var lines = new List<string>();
            string when = DateTime.TryParse(d.savedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date)
                ? $"{date:dd/MM} às {date:HH:mm}"
                : "?";
            if (d.hasFame && !string.IsNullOrWhiteSpace(d.captainName)) lines.Add($"<b>{d.captainName}</b> · fama {d.fame}");
            lines.Add($"Salvo em {when}{(string.IsNullOrEmpty(d.harborName) ? "" : $" no {d.harborName}")}.");

            var stock = new List<string>();
            foreach (var c in d.warehouse)
            {
                var item = save.FindItem(c.id);
                if (!item || item.category != ItemCategory.Resource) continue;
                stock.Add($"{item.displayName} {c.amount}");
                if (stock.Count == 4) break;
            }
            lines.Add(stock.Count > 0 ? $"Armazém: {string.Join(" · ", stock)}" : "Armazém vazio");

            int upgradeLevels = 0, buildingLevels = 0;
            foreach (var u in d.upgrades) upgradeLevels += u.level;
            foreach (var b in d.buildings) buildingLevels += b.level;
            lines.Add($"Upgrades: {upgradeLevels} · Construções: {buildingLevels} · Missões ativas: {d.activeMissions.Length}");
            return string.Join("\n", lines);
        }

        // ---------- Aviso ----------

        /// <summary>No canto de baixo, à direita: livre na janela do porto e nos HUDs do navio e de terra.</summary>
        void UpdateToast()
        {
            bool visible = Time.unscaledTime < toastUntil;
            if (toastLabel == null)
            {
                if (!visible) return;
                var hud = GameHud.Current;
                if (!hud || !hud.EnsureBuilt()) return;
                toastLabel = Hud.Text("", "hud-toast");
                hud.BottomRight.Add(toastLabel);
            }
            Hud.Show(toastLabel, visible);
            if (!visible) return;
            Hud.Set(toastLabel, toast);
            toastLabel.EnableInClassList("hud-toast--error", toastIsError);
            toastLabel.style.opacity = Mathf.Clamp01((toastUntil - Time.unscaledTime) * 2f);
        }
    }
}
