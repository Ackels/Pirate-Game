using System;
using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Menu inicial do mundo (UI Toolkit, por cima do jogo parado): Continuar, com o resumo do jogo salvo, ou Novo jogo
    /// (com save, pede confirmação; depois o nome do capitão, M10). Enquanto está na tela o tempo para e os comandos de jogo
    /// ficam desligados.
    /// Depois mostra "Jogo salvo" a cada salvamento (ao atracar e ao sair do porto), no canto de baixo.
    /// </summary>
    public class SaveMenuHUD : MonoBehaviour
    {
        [SerializeField] GameSave save;
        [SerializeField] string title = "Pirate RPG";
        [SerializeField] string subtitle = "";
        [SerializeField] float toastSeconds = 2.5f;

        bool paused, confirmNew, askName, peeked;
        TextField nameField;
        SaveData preview;
        string previewError;
        string previewText;   // resumo pronto (montado uma vez ao ler o save)
        string toast;
        bool toastIsError;
        float toastUntil;

        Label toastLabel;
        VisualElement menu, content;
        int shownView = -1;

        public void Bind(GameSave gameSave, string menuSubtitle)
        {
            save = gameSave;
            subtitle = menuSubtitle;
        }

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
            confirmNew = false;
            askName = false;
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
            shownView = -1;
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
            Hud.Show(menu, true);
            int view = askName ? 2 : confirmNew ? 1 : 0;
            var hud = GameHud.Current;
            if (hud) hud.TypingText = askName;
            if (view == shownView && peeked) return;
            shownView = view;
            content.Clear();
            nameField = null;
            if (askName) BuildName();
            else if (confirmNew) BuildConfirm();
            else BuildChoices();
        }

        bool BuildMenu()
        {
            var hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            menu = Hud.Box("hud-menu__backdrop");
            menu.pickingMode = PickingMode.Position;
            var panel = Hud.Box("hud-window", "hud-menu__panel");
            panel.pickingMode = PickingMode.Position;
            panel.Add(Hud.Text(title, "hud-menu__title"));
            if (!string.IsNullOrEmpty(subtitle)) panel.Add(Hud.Text(subtitle, "hud-menu__subtitle"));
            content = Hud.Box("hud-menu__content");
            panel.Add(content);
            panel.Add(Hud.Text("O jogo salva sozinho ao atracar no porto e ao sair dele.", "hud-menu__note"));
            menu.Add(panel);
            hud.Menu.Add(menu);
            return true;
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
            content.Add(Hud.Text(previewText, "hud-menu__summary"));
            content.Add(Win.Button("Novo jogo", () =>
            {
                if (save.HasSave) confirmNew = true;
                else askName = true;
            }, true, "hud-button--big"));
        }

        void BuildConfirm()
        {
            content.Add(Hud.Text("Começar um jogo novo?", "hud-menu__question"));
            content.Add(Hud.Text("O jogo salvo vai ser apagado: armazém, upgrades, construções e missões voltam ao começo. " +
                                 "(Fica uma cópia em save.json.bak, caso mude de ideia.)", "hud-menu__summary"));
            var row = Hud.Row("hud-menu__buttons");
            row.Add(Win.Button("Apagar e começar", () => askName = true, true, "hud-button--big"));
            row.Add(Win.Button("Voltar", () => confirmNew = false, true, "hud-button--big"));
            content.Add(row);
        }

        /// <summary>Nome do capitão (M10): aparece no painel da fama (tecla C). Enter também zarpa.</summary>
        void BuildName()
        {
            content.Add(Hud.Text("Como se chama o capitão?", "hud-menu__question"));
            content.Add(Hud.Text("O nome aparece no painel da fama (tecla C) e cresce com as suas façanhas.", "hud-menu__summary"));
            var fame = CaptainFame.Current;
            nameField = new TextField { maxLength = CaptainFame.MaxNameLength, value = fame ? fame.CaptainName : "Capitão Barbarossa" };
            nameField.AddToClassList("hud-menu__name");
            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) StartNewGame();
            }, TrickleDown.TrickleDown);
            content.Add(nameField);
            var row = Hud.Row("hud-menu__buttons");
            row.Add(Win.Button("Zarpar!", StartNewGame, true, "hud-button--big"));
            row.Add(Win.Button("Voltar", () =>
            {
                askName = false;
                confirmNew = false;
            }, true, "hud-button--big"));
            content.Add(row);
            // Já com o cursor no campo (o texto vem selecionado: digitar troca o nome inteiro).
            nameField.schedule.Execute(() =>
            {
                nameField?.Focus();
                nameField?.SelectAll();
            });
        }

        /// <summary>Teste pela CLI: começa um jogo novo com este nome, como se tivesse digitado e clicado em Zarpar.</summary>
        public void StartNewGame(string captainName)
        {
            askName = false;
            var hud = GameHud.Current;
            if (hud) hud.TypingText = false;
            save.NewGame(captainName);
        }

        void StartNewGame() => StartNewGame(nameField != null ? nameField.value : null);

        /// <summary>"Salvo em 01/10 às 14:32 no Porto da Base", o que há no armazém, upgrades, construções e missões.</summary>
        string Describe(SaveData d)
        {
            var lines = new List<string>();
            string when = DateTime.TryParse(d.savedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date)
                ? $"{date:dd/MM} às {date:HH:mm}"
                : "?";
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
            lines.Add($"Upgrades do navio: {upgradeLevels} nível(is) · Construções: {buildingLevels} nível(is) · " +
                      $"Missões ativas: {d.activeMissions.Length}");
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
