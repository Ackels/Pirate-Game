using System;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Missions;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Tutorial
{
    /// <summary>
    /// Tutorial do jogo novo (11/10, pedido do Jhonata), guiado pelo Velho Bento (na história, quem acha o capitão na praia e
    /// ensina o básico). Uma etapa por vez: o que fazer (com as teclas), uma fala do Bento e para onde ir (o HUD aponta). Cada
    /// etapa termina sozinha quando o jogador faz a coisa (desceu do navio, entrou numa casa, golpeou um boneco...), ouvindo os
    /// eventos que os sistemas já têm. Parte 1 = o porto e o treino a pé, e zarpar; o mar e as ilhas vêm depois.
    /// O save guarda a etapa (<see cref="ITutorial"/>); save de antes do tutorial = sem tutorial.
    /// </summary>
    public class TutorialGuide : MonoBehaviour, ITutorial
    {
        public enum Goal
        {
            Disembark, MeetGuide, VisitCargo, VisitShipyard, VisitConstruction, VisitTavern, VisitMarket, TalkToVillager,
            Slash, Shoot, Kick, Roll, Guard, Board, SetSail,
        }

        [Serializable]
        public class Stage
        {
            public Goal goal;
            public string title;
            [Tooltip("O que o Velho Bento diz (vazio = sem fala).")]
            [TextArea(2, 4)] public string guide;
            [Tooltip("O que fazer, com as teclas em <b>negrito</b>.")]
            [TextArea(1, 3)] public string objective;
            [Tooltip("Quantas vezes (golpes no boneco, por exemplo).")]
            public int count = 1;
        }

        public static TutorialGuide Current { get; private set; }

        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] HarborShoreLeave shoreLeave;
        [Tooltip("Quem guia (o Velho Bento): a primeira conversa é o roteiro abaixo.")]
        [SerializeField] MissionGiver guide;
        [Tooltip("Campo de treino do porto (bonecos).")]
        [SerializeField] Transform trainingYard;
        [Tooltip("Segundos com a guarda erguida para a etapa da guarda.")]
        [SerializeField] float guardSeconds = 1f;
        [Tooltip("A primeira conversa com o guia.")]
        [SerializeField, TextArea(2, 4)] string[] meetLines =
        {
            "Ora, ora! O náufrago acordou. Fui eu que te tirei da areia, sabia? Velho Bento, contramestre aposentado, ao seu dispor.",
            "O almirante Teodoro te deu aquela chalupa remendada e este cais. Não é muito, mas é um começo: Porto Alento agora é a sua base.",
            "Vou te mostrar a vila. Siga a marca amarela e não se perca, que eu não vou te buscar no mar de novo.",
        };
        [SerializeField] Stage[] stages =
        {
            new() { goal = Goal.Disembark, title = "Bem-vindo a Porto Alento",
                guide = "",
                objective = "O navio está atracado no seu cais. Aperte <b>F</b> para descer no píer." },
            new() { goal = Goal.MeetGuide, title = "O Velho Bento",
                guide = "Ei, você aí! Venha cá, capitão!",
                objective = "Vá até o <b>Velho Bento</b>, no começo do píer, e aperte <b>F</b> para conversar (<b>F</b> passa a fala)." },
            new() { goal = Goal.VisitTavern, title = "A Taverna",
                guide = "Na taverna você contrata marujos e pega contratos no quadro: até três de cada vez. O pagamento cai no armazém.",
                objective = "Entre na <b>Taverna</b> (<b>F</b> na porta)." },
            new() { goal = Goal.VisitConstruction, title = "O Mestre de obras",
                guide = "O mestre de obras levanta as construções da base: alojamento, forja, paiol, fortaleza. Cada uma ajuda de um jeito.",
                objective = "Entre no <b>Mestre de obras</b> (<b>F</b> na porta)." },
            new() { goal = Goal.TalkToVillager, title = "Os moradores",
                guide = "Quem tem <b>!</b> na cabeça tem serviço para você, com história e tudo. O <b>?</b> quer dizer que ele espera a sua volta.",
                objective = "Converse com o morador marcado (<b>F</b> perto dele)." },
            new() { goal = Goal.VisitMarket, title = "O Mercado",
                guide = "Na banca da praça se compra e vende: rações, cartuchos, munição dos canhões. Vender demais derruba o preço.",
                objective = "Vá até a <b>banca do mercado</b>, na praça, e aperte <b>F</b>." },
            new() { goal = Goal.VisitCargo, title = "O Armazém",
                guide = "Aqui fica tudo o que você junta. Atracou no cais, o porão descarrega sozinho no armazém. A Dona Mercês cuida dele.",
                objective = "Chegue na porta do <b>Armazém</b> e aperte <b>F</b> para entrar (<b>Esc</b> sai)." },
            new() { goal = Goal.VisitShipyard, title = "O Estaleiro",
                guide = "No estaleiro consertam o casco e fazem melhorias no navio. Com um esquema e ouro, até navio novo.",
                objective = "Entre no <b>Estaleiro</b> (<b>F</b> na porta)." },
            new() { goal = Goal.Slash, title = "Treino: o cutelo", count = 3,
                guide = "Agora, briga. Os bonecos de treino ficam ao lado do estaleiro. Mostre como se segura um cutelo.",
                objective = "Golpeie um boneco com o <b>botão esquerdo</b> do mouse (mira no cursor)." },
            new() { goal = Goal.Shoot, title = "Treino: a pistola",
                guide = "Pistola é para quem não quer chegar perto. Cada tiro gasta um cartucho da mochila.",
                objective = "Aperte <b>2</b> para a pistola, segure o <b>botão direito</b> para mirar e <b>clique</b> para atirar." },
            new() { goal = Goal.Kick, title = "Treino: o chute",
                guide = "Pirata no chão não bate em ninguém.",
                objective = "Aperte <b>V</b> para chutar: derruba quem está na sua frente." },
            new() { goal = Goal.Roll, title = "Treino: rolar",
                guide = "Quando o golpe vier, saia da frente. Mas cuidado com o fôlego.",
                objective = "Aperte <b>Ctrl</b> para rolar (gasta o <b>fôlego</b>, a barra verde)." },
            new() { goal = Goal.Guard, title = "Treino: a guarda",
                guide = "Com o cutelo, guarda alta! Erguida bem na hora do golpe, ela <b>apara</b> e o pirata fica tonto.",
                objective = "Aperte <b>1</b> para o cutelo e segure o <b>botão direito</b> para erguer a guarda." },
            new() { goal = Goal.Board, title = "De volta ao navio",
                guide = "Bom de briga! Agora, ao mar. Na vila não se ganha a vida, capitão.",
                objective = "Volte ao píer e aperte <b>F</b> para embarcar." },
            new() { goal = Goal.SetSail, title = "Zarpar",
                guide = "Velas ao vento! E volte inteiro, que o armazém não se enche sozinho.",
                objective = "<b>W</b> solta as velas (mais uma vez, mais rápido) e <b>A / D</b> mexem o leme. Saia do porto." },
        };

        int step = -1, progress;
        float guardHeld;
        CharacterCombat combat;
        FootCharacter character;
        HarborBuildingVisitor buildings;
        MissionBoard board;

        public int Step => step;
        public bool Running => step >= 0 && step < stages.Length;
        public int StageCount => stages.Length;
        public Stage CurrentStage => Running ? stages[step] : null;
        public int Progress => progress;
        public string GuideName => guide ? guide.DisplayName : "Velho Bento";
        /// <summary>Muda a cada etapa ou contagem (o HUD só remonta aí).</summary>
        public int Version { get; private set; }

        /// <summary>A etapa pede o capitão a pé, e ele está no navio (o HUD avisa para descer).</summary>
        public bool WaitingOnFoot => Running && NeedsFoot(stages[step].goal) && switcher && !switcher.IsOnFoot;

        public void Configure(PlayerModeSwitcher modeSwitcher, HarborShoreLeave shore, MissionGiver guideNpc, Transform yard)
        {
            switcher = modeSwitcher;
            shoreLeave = shore;
            guide = guideNpc;
            trainingYard = yard;
        }

        void Awake()
        {
            Current = this;
            if (switcher && switcher.Character)
            {
                character = switcher.Character;
                combat = character.GetComponent<CharacterCombat>();
                buildings = character.GetComponent<HarborBuildingVisitor>();
            }
        }

        void OnEnable()
        {
            Services.Register<ITutorial>(this);
            if (combat)
            {
                combat.Hit += OnHit;
                combat.Fired += OnFired;
                combat.Kicked += OnKicked;
            }
            if (character) character.Dodged += OnDodged;
            if (buildings) buildings.Entered += OnEntered;
            if (guide) guide.ScriptedTalkFinished += OnGuideTalked;
            MissionGiver.TalkStarted += OnTalkStarted;
        }

        void OnDisable()
        {
            Services.Unregister<ITutorial>(this);
            if (combat)
            {
                combat.Hit -= OnHit;
                combat.Fired -= OnFired;
                combat.Kicked -= OnKicked;
            }
            if (character) character.Dodged -= OnDodged;
            if (buildings) buildings.Entered -= OnEntered;
            if (guide) guide.ScriptedTalkFinished -= OnGuideTalked;
            MissionGiver.TalkStarted -= OnTalkStarted;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---------- Começar / salvar ----------

        public void Begin(bool enabled) => Enter(enabled ? 0 : -1);

        public void Restore(int saved) => Enter(saved >= 0 && saved < stages.Length ? saved : -1);

        /// <summary>Pula a etapa atual (sandbox F1).</summary>
        public void Skip() => Complete();

        /// <summary>Encerra o tutorial (sandbox F1).</summary>
        public void Stop() => Enter(-1);

        void Enter(int index)
        {
            step = index;
            progress = 0;
            guardHeld = 0f;
            if (guide) guide.ScriptedLines = Running && stages[step].goal == Goal.MeetGuide ? meetLines : null;
            Version++;
        }

        void Complete()
        {
            if (!Running) return;
            int done = step;
            bool last = done == stages.Length - 1;
            EventBus.Publish(new TutorialStepEvent { step = done, title = stages[done].title, finished = last });
            Enter(last ? -1 : done + 1);
        }

        void Count(Goal goal)
        {
            if (!Running || stages[step].goal != goal) return;
            progress++;
            Version++;
            if (progress >= Mathf.Max(1, stages[step].count)) Complete();
        }

        // ---------- Condições ----------

        void Update()
        {
            if (!Running || !switcher) return;
            switch (stages[step].goal)
            {
                case Goal.Disembark when switcher.IsOnFoot: Complete(); break;
                case Goal.Board when !switcher.IsOnFoot: Complete(); break;
                case Goal.SetSail when !switcher.IsOnFoot && switcher.Ship && !InPort(switcher.Ship.transform.position): Complete(); break;
                case Goal.Guard when combat && combat.IsBlocking:
                    guardHeld += Time.deltaTime;
                    if (guardHeld >= guardSeconds) Complete();
                    break;
            }
        }

        void OnHit(WeaponDefinition weapon, Vector3 _)
        {
            if (weapon && weapon.kind == WeaponKind.Melee) Count(Goal.Slash);
        }

        void OnFired(WeaponDefinition weapon, Vector3 _, Vector3 __) => Count(Goal.Shoot);
        void OnKicked() => Count(Goal.Kick);
        void OnDodged() => Count(Goal.Roll);
        void OnGuideTalked(MissionGiver _) => Count(Goal.MeetGuide);

        void OnTalkStarted(MissionGiver who)
        {
            if (who != guide) Count(Goal.TalkToVillager);
        }

        void OnEntered(HarborBuilding building)
        {
            var goal = building.Service switch
            {
                HarborService.Cargo => Goal.VisitCargo,
                HarborService.Shipyard => Goal.VisitShipyard,
                HarborService.Construction => Goal.VisitConstruction,
                HarborService.Missions => Goal.VisitTavern,
                HarborService.Market => Goal.VisitMarket,
                _ => (Goal)(-1),
            };
            Count(goal);
        }

        static bool InPort(Vector3 position)
        {
            for (int i = 0; i < HarborDock.Active.Count; i++)
                if (HarborDock.Active[i].Zone && HarborDock.Active[i].Zone.Contains(position)) return true;
            return false;
        }

        static bool NeedsFoot(Goal goal) => goal is not (Goal.Disembark or Goal.SetSail);

        // ---------- Para onde ir (o HUD aponta) ----------

        /// <summary>Lugar da etapa atual (nome para o marcador). false = sem lugar (a etapa é "faça isto aqui mesmo").</summary>
        public bool TryGetTarget(out Vector3 position, out string label)
        {
            position = default;
            label = null;
            if (!Running) return false;
            if (WaitingOnFoot && stages[step].goal != Goal.Board) return Pier(out position, out label);
            switch (stages[step].goal)
            {
                case Goal.Disembark: return false;
                case Goal.Board: return Pier(out position, out label);
                case Goal.MeetGuide:
                    if (!guide) return false;
                    position = guide.LabelPosition + Vector3.up * 0.6f;
                    label = guide.DisplayName;
                    return true;
                case Goal.VisitCargo: return Building(HarborService.Cargo, out position, out label);
                case Goal.VisitShipyard: return Building(HarborService.Shipyard, out position, out label);
                case Goal.VisitConstruction: return Building(HarborService.Construction, out position, out label);
                case Goal.VisitTavern: return Building(HarborService.Missions, out position, out label);
                case Goal.VisitMarket: return Building(HarborService.Market, out position, out label);
                case Goal.TalkToVillager: return Villager(out position, out label);
                case Goal.Slash:
                case Goal.Shoot:
                    if (!trainingYard) return false;
                    position = trainingYard.position + Vector3.up * 3f;
                    label = "Campo de treino";
                    return true;
                default: return false;
            }
        }

        bool Pier(out Vector3 position, out string label)
        {
            position = default;
            label = "Píer";
            if (!shoreLeave || !shoreLeave.Pier) return false;
            position = shoreLeave.Pier.transform.position + Vector3.up * 3f;
            return true;
        }

        static bool Building(HarborService service, out Vector3 position, out string label)
        {
            position = default;
            label = null;
            for (int i = 0; i < HarborBuilding.Active.Count; i++)
            {
                var b = HarborBuilding.Active[i];
                if (b.Service != service) continue;
                position = b.transform.position + Vector3.up * 3.5f;
                label = b.DisplayName;
                return true;
            }
            return false;
        }

        /// <summary>O morador com "!" mais perto (sem nenhum com "!", o mais perto), menos o guia.</summary>
        bool Villager(out Vector3 position, out string label)
        {
            position = default;
            label = null;
            if (!board) Services.TryGet(out board);
            Vector3 me = character ? character.transform.position : transform.position;
            MissionGiver best = null;
            float bestScore = float.MaxValue;
            var all = MissionGiver.Active;
            for (int i = 0; i < all.Count; i++)
            {
                var g = all[i];
                if (g == guide) continue;
                float score = g.FlatDistance(me) + (g.Status(board) == MissionGiver.Offer.Available ? 0f : 1000f);
                if (score >= bestScore) continue;
                best = g;
                bestScore = score;
            }
            if (!best) return false;
            position = best.LabelPosition + Vector3.up * 0.6f;
            label = best.DisplayName;
            return true;
        }
    }
}
