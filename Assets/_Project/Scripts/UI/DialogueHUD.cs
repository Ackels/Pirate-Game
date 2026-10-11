using System.Collections.Generic;
using Pirates.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Conversa com os NPCs da vila (10/10): caixa de pergaminho embaixo da tela com a plaquinha de madeira do nome, a fala (F passa)
    /// e, na última página, Aceitar / Agora não (missão oferecida) ou Receber (missão cumprida), com o pagamento e a fama. Perto de um
    /// NPC, o aviso "[F] Conversar com ..."; sobre a cabeça dos NPCs, "!" (tem missão para dar) e "?" (pronta para entregar), e o nome
    /// de quem está perto. Só remonta a caixa quando a página muda (NpcTalker.Version).
    /// </summary>
    public class DialogueHUD : MonoBehaviour
    {
        static readonly Color NameColor = new(0.97f, 0.91f, 0.79f);
        const float IconRange = 70f, NameRange = 9f;

        [SerializeField] NpcTalker talker;

        GameHud hud;
        VisualElement root, footer;
        Label nameLabel, roleLabel, text, reward;
        HudPrompt prompt;
        WorldLabelPool names;
        // "!" (ícone de exclamação do Kenney) e "?" (letra grossa dourada) sobre a cabeça: a "!" da fonte pirata é fina demais.
        readonly List<Label> icons = new();
        int iconsUsed;
        int shownVersion = -1;
        MissionGiver promptFor;
        CachedText promptLine;

        public void Bind(NpcTalker target) => talker = target;

        void Update()
        {
            if (!talker) return;
            if (root == null && !Build()) return;
            bool open = talker.IsOpen && talker.isActiveAndEnabled;
            Hud.Show(root, open);
            if (open)
            {
                if (shownVersion != talker.Version) Refresh();
                prompt.Hide();
            }
            else
            {
                shownVersion = -1;
                UpdatePrompt();
            }
            UpdateWorld(open);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            root = Hud.Box("hud-dialogue");
            root.pickingMode = PickingMode.Ignore;
            var box = Hud.Box("hud-dialogue__box");
            box.pickingMode = PickingMode.Position;
            var plate = Hud.Box("hud-dialogue__plate");
            nameLabel = Hud.Text("", "hud-dialogue__name");
            roleLabel = Hud.Text("", "hud-dialogue__role");
            plate.Add(nameLabel);
            plate.Add(roleLabel);
            text = Hud.Text("", "hud-dialogue__text");
            reward = Hud.Text("", "hud-dialogue__reward");
            footer = Hud.Box("hud-dialogue__footer");
            box.Add(text);
            box.Add(reward);
            box.Add(footer);
            // A plaquinha do nome por cima da borda do pergaminho (último filho: desenha por cima).
            box.Add(plate);
            root.Add(box);
            root.style.display = DisplayStyle.None;
            hud.Windows.Add(root);
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            names = new WorldLabelPool(hud.World, "hud-world-label--small");
            return true;
        }

        /// <summary>Página nova: nome, fala, pagamento (na oferta e na entrega) e os botões ou o "F continuar".</summary>
        void Refresh()
        {
            shownVersion = talker.Version;
            var giver = talker.Current;
            Hud.Set(nameLabel, giver.DisplayName);
            Hud.Show(roleLabel, !string.IsNullOrEmpty(giver.Role));
            if (!string.IsNullOrEmpty(giver.Role)) Hud.Set(roleLabel, giver.Role);
            Hud.Set(text, talker.Text);
            footer.Clear();

            bool last = talker.OnLastPage;
            var choice = last ? talker.PendingChoice : NpcTalker.Choice.None;
            var mission = talker.Mission;
            var board = talker.Board;
            bool showReward = choice != NpcTalker.Choice.None && mission && board;
            Hud.Show(reward, showReward);
            if (showReward)
                Hud.Set(reward, $"<b>{mission.title}</b>   ·   paga {Win.Stacks(board.Rewards(mission))}   ·   +{mission.fameReward} de fama");

            switch (choice)
            {
                case NpcTalker.Choice.Offer:
                    bool room = board.CanAccept;
                    footer.Add(Win.Button(room ? "Aceitar  <size=75%>[F]</size>" : $"Máx. {board.MaxActive} missões ativas", talker.Accept, room));
                    footer.Add(Win.Button("Agora não  <size=75%>[Esc]</size>", talker.Decline, extraClass: "hud-button--small"));
                    break;
                case NpcTalker.Choice.TurnIn:
                    footer.Add(Win.Button("Receber o pagamento  <size=75%>[F]</size>", talker.TurnIn));
                    break;
                default:
                    var hint = Hud.Box("hud-dialogue__hint");
                    hint.Add(Hud.Key("F"));
                    hint.Add(Hud.Text(last ? "Fechar" : "Continuar", "hud-dialogue__hint-text"));
                    footer.Add(hint);
                    break;
            }
        }

        void UpdatePrompt()
        {
            var near = talker.isActiveAndEnabled && talker.CanOpen ? talker.Nearby : null;
            if (!near)
            {
                prompt.Hide();
                promptFor = null;
                return;
            }
            if (promptFor != near || promptLine.Text == null)
            {
                promptFor = near;
                promptLine.Text = $"Conversar com {near.DisplayName}";
            }
            prompt.Show("F", promptLine.Text);
        }

        /// <summary>"!"/"?" sobre quem tem missão (de longe) e o nome de quem está perto (some com a conversa aberta).</summary>
        void UpdateWorld(bool open)
        {
            if (!names.Begin()) return;
            var board = talker.Board;
            var cam = Camera.main;
            iconsUsed = 0;
            var all = MissionGiver.Active;
            for (int i = 0; i < all.Count; i++)
            {
                var giver = all[i];
                if (giver.IsTalking) continue;
                Vector3 d = giver.transform.position - cam.transform.position;
                float distance = d.magnitude;
                if (distance > IconRange) continue;
                var offer = giver.Status(board);
                if (offer != MissionGiver.Offer.None) PlaceIcon(cam, giver.LabelPosition + Vector3.up * 0.3f, offer == MissionGiver.Offer.ReadyToTurnIn);
                if (!open && talker.isActiveAndEnabled && giver.FlatDistance(talker.transform.position) <= NameRange)
                    names.Place(giver.LabelPosition, giver.DisplayName, NameColor);
            }
            for (int i = iconsUsed; i < icons.Count; i++) Hud.Show(icons[i], false);
            names.End();
        }

        void PlaceIcon(Camera cam, Vector3 world, bool turnIn)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            if (iconsUsed == icons.Count)
            {
                var made = Hud.Text("", "hud-quest-icon");
                made.pickingMode = PickingMode.Ignore;
                hud.World.Add(made);
                icons.Add(made);
            }
            var icon = icons[iconsUsed++];
            icon.EnableInClassList("hud-quest-icon--turnin", turnIn);
            Hud.Set(icon, turnIn ? "?" : "");
            Vector2 p = RuntimePanelUtils.ScreenToPanel(hud.World.panel, new Vector2(screen.x, Screen.height - screen.y));
            icon.style.left = p.x;
            icon.style.top = p.y;
            Hud.Show(icon, true);
        }
    }
}
