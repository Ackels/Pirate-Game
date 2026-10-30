using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Missions
{
    /// <summary>
    /// No personagem a pé: perto de um <see cref="MissionGiver"/>, F conversa. A conversa são páginas de fala do NPC (F passa); na
    /// última, conforme o caso: oferece a missão (Aceitar = F, "Agora não" = Esc ou o botão), entrega a missão cumprida (Receber o
    /// pagamento = F, vai para o armazém como no quadro) ou só fecha. Missão aceita e não cumprida = uma fala de espera; entregue e
    /// descansando = uma fala de "volte depois"; patente baixa = o NPC diz que ainda não confia. Quem desenha é o DialogueHUD (UI).
    /// Afastar-se fecha; o jogo não pausa (o mouse fica com a caixa: ScreenPanel).
    /// </summary>
    public class NpcTalker : MonoBehaviour, IPagedInteraction
    {
        public enum Choice { None, Offer, TurnIn }

        [Tooltip("Folga (m) além do alcance do NPC antes de a conversa fechar sozinha.")]
        [SerializeField] float closeMargin = 1.5f;

        readonly List<string> pages = new();
        MissionBoard board;
        bool panelOwned, scripted;

        public MissionGiver Nearby { get; private set; }
        public MissionGiver Current { get; private set; }
        public int Page { get; private set; }
        public int PageCount => pages.Count;
        public string Text => Page < pages.Count ? pages[Page] : "";
        public bool OnLastPage => Page >= pages.Count - 1;
        /// <summary>O que a última página pede (aceitar a missão, receber o pagamento) ou nada.</summary>
        public Choice PendingChoice { get; private set; }
        /// <summary>A missão de que se fala (oferecida, em andamento ou entregue).</summary>
        public MissionDefinition Mission { get; private set; }
        public MissionBoard Board => board;
        /// <summary>Muda a cada página ou escolha (a caixa só se refaz aí).</summary>
        public int Version { get; private set; }

        public bool IsOpen => Current;
        public bool CanOpen => Nearby && board;

        void Update()
        {
            if (!board) Services.TryGet(out board);
            Nearby = FindNearest();
            if (Current && Current.FlatDistance(transform.position) > Current.TalkRange + closeMargin) Close();
        }

        // Embarcar desliga o personagem: a conversa acaba.
        void OnDisable() => Close();

        public bool Open()
        {
            if (!CanOpen) return false;
            Current = Nearby;
            Build();
            Current.BeginTalk(transform.position);
            // A câmera olha do capitão para o NPC.
            Vector3 to = Current.transform.position - transform.position;
            if (Services.TryGet(out ICameraFollow cam) && to.sqrMagnitude > 0.01f) cam.LookToward(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            if (!ScreenPanel.Open)
            {
                ScreenPanel.Open = true;
                panelOwned = true;
            }
            Version++;
            return true;
        }

        public void Close()
        {
            if (!Current) return;
            // Roteiro lido até a última fala: o tutorial anda (fechar antes deixa para a próxima conversa).
            if (scripted && OnLastPage) Current.FinishScriptedTalk();
            scripted = false;
            Current.EndTalk();
            Current = null;
            pages.Clear();
            if (panelOwned) ScreenPanel.Open = false;
            panelOwned = false;
            Version++;
        }

        /// <summary>F: próxima fala; na última, aceita/recebe (se for o caso) ou fecha.</summary>
        public void Advance()
        {
            if (!Current) return;
            if (!OnLastPage)
            {
                Page++;
                Version++;
                return;
            }
            switch (PendingChoice)
            {
                case Choice.Offer when board.CanAccept: Accept(); break;
                case Choice.Offer: break;   // sem vaga: o botão mostra o limite; Esc fecha
                case Choice.TurnIn: TurnIn(); break;
                default: Close(); break;
            }
        }

        /// <summary>Aceita a missão oferecida (até o limite de missões ativas).</summary>
        public void Accept()
        {
            if (!Current || PendingChoice != Choice.Offer || !board.Accept(Mission)) return;
            Current.LastDeclined = null;   // a recusada volta à fila
            Current.Gesture("Yes");
            Say(Line(Mission.acceptLine, "Combinado, capitão. Volte aqui quando terminar."));
        }

        /// <summary>"Agora não": a missão continua com ele.</summary>
        public void Decline()
        {
            if (!Current || PendingChoice != Choice.Offer) return;
            Current.Gesture("No");
            Current.LastDeclined = Mission;
            Say(Line(Mission.declineLine, "Fica para outra hora, então."));
        }

        /// <summary>Entrega a missão cumprida: o pagamento vai para o armazém (como no quadro) e a conversa fecha.</summary>
        public void TurnIn()
        {
            if (!Current || PendingChoice != Choice.TurnIn) return;
            var a = board.ActiveFor(Mission);
            if (a == null || !board.TurnIn(a, board.PayTo)) return;
            Current.Gesture("Yes");
            Close();
        }

        /// <summary>Monta as falas: entregar o que está pronto, esperar o que está em andamento, oferecer o que dá, ou conversa à toa.</summary>
        void Build()
        {
            pages.Clear();
            Page = 0;
            PendingChoice = Choice.None;
            Mission = null;
            scripted = Current.HasScriptedTalk;
            if (scripted)
            {
                AddPages(Current.ScriptedLines, "...");
                return;
            }
            var list = Current.Missions;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                var a = m ? board.ActiveFor(m) : null;
                if (a == null || !board.IsComplete(a)) continue;
                Mission = m;
                AddPages(m.doneLines, "Feito! Aqui está o combinado.");
                PendingChoice = Choice.TurnIn;
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m || board.ActiveFor(m) == null) continue;
                Mission = m;
                pages.Add(Line(m.waitingLine, "Ainda esperando, capitão. Volte quando estiver feito."));
                return;
            }
            // Oferece a primeira que dá; a recusada da última vez só se não houver outra.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (!m || !board.IsOfferable(m) || (pass == 0 && m == Current.LastDeclined)) continue;
                    Mission = m;
                    AddPages(m.offerLines, m.description);
                    PendingChoice = Choice.Offer;
                    return;
                }
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m || board.IsUnlocked(m) || (!m.repeatable && board.IsFinished(m))) continue;
                Mission = m;
                pages.Add($"Tenho um serviço, mas não é para qualquer um. Volte quando for {board.RankTitle(m.requiredRank)}.");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m || (board.CooldownLeft(m) <= 0f && !board.IsFinished(m))) continue;
                Mission = m;
                pages.Add(Line(m.restingLine, "Por enquanto, nada. Volte daqui a uns dias."));
                return;
            }
            var chat = Current.SmallTalk;
            pages.Add(chat.Count > 0 ? chat[Random.Range(0, chat.Count)] : "Bons ventos, capitão.");
        }

        void Say(string line)
        {
            pages.Clear();
            pages.Add(line);
            Page = 0;
            PendingChoice = Choice.None;
            Version++;
        }

        void AddPages(string[] lines, string fallback)
        {
            if (lines != null)
                foreach (var line in lines)
                    if (!string.IsNullOrWhiteSpace(line)) pages.Add(line);
            if (pages.Count == 0) pages.Add(string.IsNullOrWhiteSpace(fallback) ? "..." : fallback);
        }

        static string Line(string line, string fallback) => string.IsNullOrWhiteSpace(line) ? fallback : line;

        MissionGiver FindNearest()
        {
            MissionGiver best = null;
            float bestDistance = float.MaxValue;
            var all = MissionGiver.Active;
            for (int i = 0; i < all.Count; i++)
            {
                float d = all[i].FlatDistance(transform.position);
                if (d > all[i].TalkRange || d >= bestDistance) continue;
                best = all[i];
                bestDistance = d;
            }
            return best;
        }
    }
}
