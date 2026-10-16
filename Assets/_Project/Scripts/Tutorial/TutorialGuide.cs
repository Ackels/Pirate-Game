using System;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Tutorial
{
    /// <summary>
    /// Tutorial do jogo novo (11/10, pedido do Jhonata), guiado pelo Velho Bento (na história, quem acha o capitão na praia e
    /// ensina o básico). Uma etapa por vez: o que fazer (com as teclas), uma fala do Bento e para onde ir (o HUD aponta). Cada
    /// etapa termina sozinha quando o jogador faz a coisa (desceu do navio, entrou numa casa, golpeou um boneco...), ouvindo os
    /// eventos que os sistemas já têm. Parte 1 = o porto e o treino a pé, e zarpar; parte 2 = o mar (canhões, afundar a chalupa
    /// pirata, saquear os destroços, atracar); parte 3 = as ilhas (desembarcar na praia norte da Caveira, derrotar o posto do naufrágio,
    /// saque a pé, a fama, voltar ao navio). A praia sul ficava a ~185 m do galeão Tormenta, que via o navio e atacava.
    /// O save guarda a etapa (<see cref="ITutorial"/>); save de antes do tutorial = sem tutorial.
    /// </summary>
    public class TutorialGuide : MonoBehaviour, ITutorial
    {
        public enum Goal
        {
            Disembark, MeetGuide, VisitCargo, VisitShipyard, VisitConstruction, VisitTavern, VisitMarket, TalkToVillager,
            Slash, Shoot, Kick, Roll, Guard, Board, SetSail,
            FireCannons, SinkShip, Salvage, Dock,
            LandOnIsland, ClearOutpost, LandLoot, OpenFame,
        }

        [Serializable]
        public class Stage
        {
            [Tooltip("Parte do tutorial (1 = o porto, 2 = o mar): nome em Part Names.")]
            public int part = 1;
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
        [Tooltip("Praia onde a parte das ilhas desembarca (a praia norte da Caveira: a sul fica perto do galeão Tormenta).")]
        [SerializeField] LandingPoint islandLanding;
        [Tooltip("Posto pequeno que a parte das ilhas manda derrotar (o do naufrágio: 2 de cutelo e 1 de pistola).")]
        [SerializeField] EnemyCamp outpost;
        [Tooltip("Caixotes e barris até esta distância (m) do posto contam para a etapa do saque a pé.")]
        [SerializeField] float outpostLootRadius = 25f;
        [Tooltip("Segundos com a guarda erguida para a etapa da guarda.")]
        [SerializeField] float guardSeconds = 1f;
        [Tooltip("Etapa do saque sem destroços com carga no mar por este tempo (sumiram, ou o navio afundou longe) = cumprida.")]
        [SerializeField] float noWreckSeconds = 30f;
        [Tooltip("Na etapa de afundar, a chalupa pirata mais perto vem atrás do navio daqui (m): ela nasce perto da patrulha do " +
                 "brigue Santa Clara, forte demais para quem está começando.")]
        [SerializeField] float callPirateRange = 900f;
        [Tooltip("Nome de cada parte (a 1ª é a parte 1).")]
        [SerializeField] string[] partNames = { "O porto", "O mar", "As ilhas" };
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
            new() { part = 2, goal = Goal.FireCannons, title = "Os canhões",
                guide = "Canhão só atira pelo costado: bombordo é a esquerda, estibordo a direita. Segure, e a mira vai para longe; solte, e fogo!",
                objective = "Segure <b>Q</b> (bombordo) ou <b>E</b> (estibordo) para mirar e <b>solte</b> para disparar. Pode ser na água, é treino." },
            new() { part = 2, goal = Goal.SinkShip, title = "Fogo nos piratas!",
                guide = "Lá vem uma chalupa pirata atrás de você! Fique de lado para ela e mande bala. Casco feio? Volte ao porto: lá ninguém te segue.",
                objective = "Afunde a <b>chalupa pirata</b> marcada. <b>W / S</b> soltam e recolhem as velas, <b>Q / E</b> disparam." },
            new() { part = 2, goal = Goal.Salvage, title = "O saque",
                guide = "Navio no fundo, destroços na água: o que couber no porão é seu. Ouro, madeira, pólvora... às vezes até um baú!",
                objective = "Pare ao lado dos <b>destroços</b> (<b>S</b> recolhe as velas), aperte <b>F</b> e pegue os itens." },
            new() { part = 2, goal = Goal.Dock, title = "De volta ao porto",
                guide = "Porão cheio, rumo a casa! Atracou no cais, a carga vai sozinha para o armazém e o jogo fica salvo.",
                objective = "Volte a <b>Porto Alento</b>, chegue devagar no seu cais e aperte <b>F</b> para atracar." },
            new() { part = 3, goal = Goal.LandOnIsland, title = "Terra à vista",
                guide = "As ilhas guardam o melhor saque, e os piores piratas. Vamos começar pela Ilha da Caveira, pela praia do norte.",
                objective = "Navegue até a <b>praia norte da Caveira</b> e, perto da areia, aperte <b>F</b> para desembarcar." },
            new() { part = 3, goal = Goal.ClearOutpost, title = "Os guardas do naufrágio",
                guide = "Três capangas do Lázaro guardam um naufrágio a leste. O de pistola avisa com uma linha vermelha: saia da frente! Vida baixa? <b>R</b> come uma ração.",
                objective = "Siga a trilha e derrote os <b>guardas do posto do naufrágio</b>." },
            new() { part = 3, goal = Goal.LandLoot, title = "Saque a pé",
                guide = "Caixote e barril de pirata sempre têm alguma coisa. Quebre e passe por cima do saco: vai tudo para a mochila.",
                objective = "Quebre um <b>caixote</b> ou <b>barril</b> do posto com o cutelo e passe por cima do saco." },
            new() { part = 3, goal = Goal.OpenFame, title = "A sua fama",
                guide = "Cada feito espalha o seu nome: navio afundado, posto limpo, contrato entregue. Com fama você sobe de patente e libera contratos, mercadorias e bônus.",
                objective = "Aperte <b>C</b> para ver a fama do capitão (<b>C</b> ou <b>Esc</b> fecha)." },
            new() { part = 3, goal = Goal.Board, title = "De volta a bordo",
                guide = "Aquele acampamento grande no meio da ilha tem onze piratas e um brutamontes: volte com tripulação e melhorias. O contrato dele está na taverna.",
                objective = "Volte ao navio e aperte <b>F</b> para embarcar: a mochila descarrega no porão." },
        };

        int step = -1, progress;
        float guardHeld, noWreckFor;
        CharacterCombat combat;
        FootCharacter character;
        HarborBuildingVisitor buildings;
        MissionBoard board;
        CannonBattery[] batteries = Array.Empty<CannonBattery>();
        WreckSalvager salvager;
        HarborVisitor visitor;
        EnemyShipAI calledPirate;
        LandLooter looter;
        NpcTalker talker;

        public int Step => step;
        public bool Running => step >= 0 && step < stages.Length;
        public int StageCount => stages.Length;
        public Stage CurrentStage => Running ? stages[step] : null;
        public int Progress => progress;
        public string GuideName => guide ? guide.DisplayName : "Velho Bento";
        /// <summary>Muda a cada etapa ou contagem (o HUD só remonta aí).</summary>
        public int Version { get; private set; }

        public int Parts
        {
            get
            {
                int max = 0;
                for (int i = 0; i < stages.Length; i++) max = Mathf.Max(max, stages[i].part);
                return max;
            }
        }

        public int FirstStepOf(int part)
        {
            for (int i = 0; i < stages.Length; i++)
                if (stages[i].part == part) return i;
            return -1;
        }

        /// <summary>Parte da etapa (0 = fora do tutorial).</summary>
        public int PartOf(int index) => index >= 0 && index < stages.Length ? stages[index].part : 0;

        public string PartName(int part) => part >= 1 && part <= partNames.Length ? partNames[part - 1] : $"Parte {part}";

        /// <summary>Etapa atual dentro da parte (1 em diante) e quantas a parte tem.</summary>
        public (int index, int count) PartProgress
        {
            get
            {
                if (!Running) return (0, 0);
                int part = stages[step].part, index = 0, count = 0;
                for (int i = 0; i < stages.Length; i++)
                {
                    if (stages[i].part != part) continue;
                    count++;
                    if (i <= step) index++;
                }
                return (index, count);
            }
        }

        /// <summary>A etapa pede o capitão a pé, e ele está no navio (o HUD avisa para descer).</summary>
        public bool WaitingOnFoot => Running && NeedsFoot(stages[step].goal) && switcher && !switcher.IsOnFoot;

        /// <summary>Onde descer quando a etapa pede o capitão a pé e ele está no navio (o HUD avisa).</summary>
        public string DisembarkHint => OnIsland
            ? "Primeiro desça do navio: perto da <b>praia norte da Caveira</b>, aperte <b>F</b>."
            : "Primeiro volte à terra: no cais do porto, aperte <b>F</b> para descer do navio.";

        /// <summary>A etapa é no mar, e o capitão está a pé (o HUD avisa para embarcar).</summary>
        public bool WaitingOnShip => Running && NeedsShip(stages[step].goal) && switcher && switcher.IsOnFoot;

        public void Configure(PlayerModeSwitcher modeSwitcher, HarborShoreLeave shore, MissionGiver guideNpc, Transform yard,
            LandingPoint landing, EnemyCamp islandOutpost)
        {
            switcher = modeSwitcher;
            shoreLeave = shore;
            guide = guideNpc;
            trainingYard = yard;
            islandLanding = landing;
            outpost = islandOutpost;
        }

        void Awake()
        {
            Current = this;
            if (switcher && switcher.Character)
            {
                character = switcher.Character;
                combat = character.GetComponent<CharacterCombat>();
                buildings = character.GetComponent<HarborBuildingVisitor>();
                looter = character.GetComponent<LandLooter>();
                talker = character.GetComponent<NpcTalker>();
            }
            if (switcher && switcher.Ship)
            {
                // Os canhões de todos os cascos (os do casco que não está em uso ficam desligados e não disparam).
                batteries = switcher.Ship.GetComponentsInChildren<CannonBattery>(true);
                salvager = switcher.Ship.GetComponent<WreckSalvager>();
                visitor = switcher.Ship.GetComponent<HarborVisitor>();
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
            if (looter) looter.Transferred += OnLandLooted;
            if (guide) guide.ScriptedTalkFinished += OnGuideTalked;
            MissionGiver.TalkStarted += OnTalkStarted;
            for (int i = 0; i < batteries.Length; i++) batteries[i].Fired += OnCannonFired;
            if (salvager) salvager.Transferred += OnSalvaged;
            if (visitor) visitor.Docked += OnDocked;
            EventBus.Subscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Subscribe<CampClearedEvent>(OnCampCleared);
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
            if (looter) looter.Transferred -= OnLandLooted;
            if (guide) guide.ScriptedTalkFinished -= OnGuideTalked;
            MissionGiver.TalkStarted -= OnTalkStarted;
            for (int i = 0; i < batteries.Length; i++) batteries[i].Fired -= OnCannonFired;
            if (salvager) salvager.Transferred -= OnSalvaged;
            if (visitor) visitor.Docked -= OnDocked;
            EventBus.Unsubscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Unsubscribe<CampClearedEvent>(OnCampCleared);
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
            guardHeld = noWreckFor = 0f;
            if (guide) guide.ScriptedLines = Running && stages[step].goal == Goal.MeetGuide ? meetLines : null;
            // Posto já limpo nesta sessão (o jogador passou antes por lá): os guardas voltam para a etapa ter o que fazer.
            if (Running && stages[step].goal == Goal.ClearOutpost && outpost && outpost.IsCleared) outpost.ResetAll();
            Version++;
        }

        void Complete()
        {
            if (!Running) return;
            int done = step;
            bool last = done == stages.Length - 1;
            int part = stages[done].part;
            EventBus.Publish(new TutorialStepEvent
            {
                step = done, title = stages[done].title, finished = last,
                partFinished = last || stages[done + 1].part != part, part = PartName(part),
            });
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
                case Goal.LandOnIsland when switcher.IsOnFoot && !AtPier: Complete(); break;
                // O painel da fama usa o ScreenPanel; a conversa com um NPC também (essa não conta).
                case Goal.OpenFame when ScreenPanel.Open && !(talker && talker.IsOpen): Complete(); break;
                case Goal.SetSail when !switcher.IsOnFoot && switcher.Ship && !InPort(switcher.Ship.transform.position): Complete(); break;
                case Goal.Guard when combat && combat.IsBlocking:
                    guardHeld += Time.deltaTime;
                    if (guardHeld >= guardSeconds) Complete();
                    break;
                case Goal.SinkShip when NearestPirate(out var pirate) && pirate != calledPirate:
                    pirate.Hunt(callPirateRange);
                    calledPirate = pirate;
                    break;
                case Goal.Salvage:
                    // Os destroços aparecem quando o navio termina de afundar (alguns segundos) e somem com o tempo.
                    noWreckFor = NearestWreck(out _) ? 0f : noWreckFor + Time.deltaTime;
                    if (noWreckFor >= noWreckSeconds) Complete();
                    break;
            }
        }

        void OnCannonFired() => Count(Goal.FireCannons);
        void OnShipSunk(ShipSunkEvent _) => Count(Goal.SinkShip);
        void OnDocked(HarborDock _) => Count(Goal.Dock);

        void OnCampCleared(CampClearedEvent e)
        {
            if (outpost && e.campId == outpost.CampId) Count(Goal.ClearOutpost);
        }

        void OnLandLooted(ItemStack _, bool toBackpack)
        {
            if (toBackpack) Count(Goal.LandLoot);
        }

        /// <summary>O capitão está a pé no cais do porto (não numa ilha).</summary>
        bool AtPier => shoreLeave && shoreLeave.Pier && switcher.Landing == shoreLeave.Pier;

        void OnSalvaged(ItemStack _, bool toHold)
        {
            if (toHold) Count(Goal.Salvage);
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

        static bool NeedsShip(Goal goal) =>
            goal is Goal.SetSail or Goal.FireCannons or Goal.SinkShip or Goal.Salvage or Goal.Dock or Goal.LandOnIsland;
        static bool NeedsFoot(Goal goal) => goal is not (Goal.Disembark or Goal.OpenFame) && !NeedsShip(goal);

        /// <summary>A etapa é numa ilha: no navio, o capitão desce na praia, não no cais.</summary>
        bool OnIsland => Running && stages[step].part >= 3;

        // ---------- Para onde ir (o HUD aponta) ----------

        /// <summary>Lugar da etapa atual (nome para o marcador). false = sem lugar (a etapa é "faça isto aqui mesmo").</summary>
        public bool TryGetTarget(out Vector3 position, out string label)
        {
            position = default;
            label = null;
            if (!Running) return false;
            if (WaitingOnFoot && stages[step].goal != Goal.Board)
                return OnIsland ? Landing(islandLanding, IslandBeachName, out position, out label) : Pier(out position, out label);
            // A pé fora do porto, a etapa de desembarcar já se cumpre: aqui o capitão está no cais.
            if (WaitingOnShip) return Pier(out position, out label);
            switch (stages[step].goal)
            {
                case Goal.Disembark: return false;
                case Goal.Board:
                    // Numa ilha, o navio ficou ancorado na praia onde o capitão desceu.
                    if (switcher.IsOnFoot && switcher.Landing && !AtPier) return Landing(switcher.Landing, "Seu navio", out position, out label);
                    return Pier(out position, out label);
                case Goal.LandOnIsland:
                    return Landing(islandLanding, IslandBeachName, out position, out label);
                case Goal.ClearOutpost:
                    if (!outpost) return false;
                    position = outpost.Center + Vector3.up * 4f;
                    label = outpost.DisplayName;
                    return true;
                case Goal.LandLoot: return OutpostLoot(out position, out label);
                case Goal.SinkShip:
                    if (!NearestPirate(out var pirate)) return false;
                    position = pirate.transform.position + Vector3.up * 9f;
                    label = "Chalupa pirata";
                    return true;
                case Goal.Salvage:
                    if (!NearestWreck(out var wreck)) return false;
                    position = wreck.transform.position + Vector3.up * 3f;
                    label = "Destroços";
                    return true;
                case Goal.Dock:
                    if (HarborDock.Active.Count == 0) return false;
                    position = HarborDock.Active[0].Berth.position + Vector3.up * 4f;
                    label = "Seu cais";
                    return true;
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

        Vector3 ShipPosition => switcher && switcher.Ship ? switcher.Ship.transform.position : transform.position;

        /// <summary>Navio pirata comum mais perto (sem os chefes, os caçadores e os navios com nome de missão, fortes demais no começo).</summary>
        bool NearestPirate(out EnemyShipAI best)
        {
            best = null;
            Vector3 me = ShipPosition;
            float bestDistance = float.MaxValue;
            var all = EnemyShipAI.Active;
            for (int i = 0; i < all.Count; i++)
            {
                var ai = all[i];
                if (ai.IsDead || !string.IsNullOrEmpty(ai.TargetId) || ai.TryGetComponent(out ShipBoss _)) continue;
                float d = (ai.transform.position - me).sqrMagnitude;
                if (d >= bestDistance) continue;
                best = ai;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>Destroços com carga mais perto do navio.</summary>
        bool NearestWreck(out ShipWreck best)
        {
            best = null;
            Vector3 me = ShipPosition;
            float bestDistance = float.MaxValue;
            var all = ShipWreck.Active;
            for (int i = 0; i < all.Count; i++)
            {
                var w = all[i];
                if (w.IsEmpty) continue;
                float d = (w.transform.position - me).sqrMagnitude;
                if (d >= bestDistance) continue;
                best = w;
                bestDistance = d;
            }
            return best;
        }

        const string IslandBeachName = "Praia norte da Caveira";

        static bool Landing(LandingPoint point, string name, out Vector3 position, out string label)
        {
            position = default;
            label = name;
            if (!point) return false;
            position = point.LandPosition.position + Vector3.up * 3f;
            return true;
        }

        /// <summary>Saco de saque no chão perto do posto (primeiro) ou o caixote/barril inteiro mais perto do posto.</summary>
        bool OutpostLoot(out Vector3 position, out string label)
        {
            position = default;
            label = null;
            if (!outpost) return false;
            Vector3 center = outpost.Center;
            float limit = outpostLootRadius * outpostLootRadius, best = limit;
            for (int i = 0; i < LootSack.Active.Count; i++)
            {
                float d = (LootSack.Active[i].transform.position - center).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                position = LootSack.Active[i].transform.position + Vector3.up * 1.5f;
                label = "Saco de saque";
            }
            if (label != null) return true;
            best = limit;
            for (int i = 0; i < BreakableLoot.Active.Count; i++)
            {
                var b = BreakableLoot.Active[i];
                if (b.IsBroken) continue;
                float d = (b.transform.position - center).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                position = b.transform.position + Vector3.up * 1.8f;
                label = b.name.StartsWith("Barril") ? "Barril" : "Caixote";
            }
            return label != null;
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
