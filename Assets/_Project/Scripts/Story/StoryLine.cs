using System;
using Pirates.Base;
using Pirates.Core;
using Pirates.Missions;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Story
{
    /// <summary>
    /// Trilha da História, "A Coroa de Prata" (16/10; HISTORIA.md, "O arco principal"): capítulos em ordem, cada um pedindo uma patente
    /// (a fama segura o ritmo; o meio do jogo continua aberto), com objetivos que usam o que o jogo já tem e contam pelos mesmos eventos
    /// das missões (navio afundado, acampamento/forte limpo, inimigo com nome derrotado) ou pelo estado (construção, tripulação).
    /// Separada dos contratos: o mesmo feito conta para os dois. Evento só conta com o capítulo aberto (patente alcançada); ao abrir, os
    /// alvos dele voltam ao lugar (o mesmo aviso de "missão aceita": acampamento repovoa, brigue volta, chefe levanta). O último capítulo
    /// ainda não existe no jogo (Ilha Amaldiçoada, pacote H): fica "em breve". Textos nos valores padrão: regere a cena para pegar texto
    /// novo, ou edite no Inspector.
    /// </summary>
    public class StoryLine : MonoBehaviour, IStory
    {
        public enum Goal
        {
            /// <summary>Afundar navios (id = o do navio com nome; vazio = qualquer um).</summary>
            SinkShips,
            /// <summary>Limpar um acampamento ou calar um forte (id do acampamento/forte).</summary>
            ClearCamp,
            /// <summary>Derrotar um inimigo com nome (chefe, monstro do mar).</summary>
            DefeatEnemy,
            /// <summary>Estado: construção da base (id = nome do asset) no nível <c>count</c>.</summary>
            Building,
            /// <summary>Estado: <c>count</c> tripulantes a bordo.</summary>
            Crew,
            /// <summary>
            /// Conversa de história com um NPC da vila (id = o nome dele): ele ganha "!" e as <c>lines</c> como conversa de roteiro
            /// (a mesma do tutorial); conta ao chegar na última fala.
            /// </summary>
            Talk,
        }

        [Serializable]
        public class Objective
        {
            public string text;
            public Goal goal;
            [Tooltip("Navio, acampamento, forte ou inimigo (o mesmo id das missões e do marcador no mapa); construção = nome do asset; " +
                     "conversa = nome do NPC.")]
            public string id;
            [Min(1)] public int count = 1;
            [Tooltip("Conversa: as falas do NPC ({nome} = o nome do capitão).")]
            [TextArea] public string[] lines = Array.Empty<string>();

            public bool IsState => goal is Goal.Building or Goal.Crew;

            [NonSerialized] string talkMarker;
            /// <summary>Id do marcador no mapa (conversa = o marcador do NPC), sem alocar a cada quadro.</summary>
            public string MarkerId => goal == Goal.Talk ? talkMarker ??= MissionGiver.MarkerIdFor(id) : id;
        }

        [Serializable]
        public class ChapterSpec
        {
            public string title;
            [Tooltip("Patente que abre o capítulo (0 = Grumete).")]
            public int requiredRank;
            [TextArea] public string intro;
            [Tooltip("Fala da faixa quando o capítulo acaba.")]
            [TextArea] public string outro;
            public Objective[] objectives = Array.Empty<Objective>();
            [Tooltip("Ainda não existe no jogo: aparece como \"em breve\" e não acaba (as conversas dele ainda acontecem).")]
            public bool comingSoon;
        }

        [SerializeField] ChapterSpec[] chapters = DefaultChapters();
        [SerializeField] BaseBuildings buildings;
        [SerializeField] ShipCrew crew;
        [Tooltip("Segundos entre as conferências dos objetivos de estado (construção, tripulação).")]
        [SerializeField] float stateCheckSeconds = 0.5f;

        int chapter;
        int[] progress = Array.Empty<int>();
        bool initialized, wasOpen;
        float nextCheck;

        public static StoryLine Current { get; private set; }

        /// <summary>Muda quando o capítulo, um objetivo ou a abertura do capítulo mudam (o HUD só remonta nisso).</summary>
        public int Version { get; private set; }
        public int Chapter => chapter;
        public int ChapterCount => chapters.Length;
        public int[] Progress => (int[])progress.Clone();
        public bool Finished => chapter >= chapters.Length;
        public ChapterSpec CurrentChapter => Finished ? null : chapters[chapter];
        public string ChapterTitle(int index) => index >= 0 && index < chapters.Length ? chapters[index].title : "";
        public ChapterSpec Spec(int index) => index >= 0 && index < chapters.Length ? chapters[index] : null;

        /// <summary>O capítulo atual abriu agora (a patente chegou, ou o anterior acabou com a patente já alcançada).</summary>
        public event Action<int> Opened;

        /// <summary>A patente do capítulo foi alcançada (os objetivos contam).</summary>
        public bool IsOpen => !Finished && Rank >= chapters[chapter].requiredRank;

        /// <summary>Patente atual (sem fama na cena: abre tudo).</summary>
        public static int Rank => Services.TryGet(out ICaptainRank rank) ? rank.Rank : int.MaxValue;

        public void Configure(BaseBuildings baseBuildings, ShipCrew shipCrew)
        {
            buildings = baseBuildings;
            crew = shipCrew;
        }

        void Awake()
        {
            Current = this;
            ResetProgress();
        }

        void OnEnable()
        {
            Services.Register<IStory>(this);
            EventBus.Subscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Subscribe<CampClearedEvent>(OnCampCleared);
            EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
            MissionGiver.TalkStarted += OnTalkStarted;
        }

        void OnDisable()
        {
            Services.Unregister<IStory>(this);
            EventBus.Unsubscribe<ShipSunkEvent>(OnShipSunk);
            EventBus.Unsubscribe<CampClearedEvent>(OnCampCleared);
            EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
            MissionGiver.TalkStarted -= OnTalkStarted;
            ReleaseTalk();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---------- Começar / salvar ----------

        public void Begin() => Restore(0, null);

        public void Restore(int savedChapter, int[] savedProgress)
        {
            ReleaseTalk();
            chapter = Mathf.Clamp(savedChapter, 0, chapters.Length);
            ResetProgress();
            if (savedProgress != null)
                for (int i = 0; i < progress.Length && i < savedProgress.Length; i++) progress[i] = Mathf.Max(0, savedProgress[i]);
            // A abertura do capítulo é conferida de novo no próximo quadro, sem repovoar (ao carregar, as ilhas já voltam cheias).
            initialized = false;
            Version++;
        }

        void ResetProgress()
        {
            int n = Finished ? 0 : chapters[chapter].objectives.Length;
            progress = new int[n];
        }

        // ---------- Objetivos ----------

        /// <summary>Quanto do objetivo <paramref name="i"/> do capítulo atual já foi feito (estado = o valor de agora).</summary>
        public int Amount(int i)
        {
            var c = CurrentChapter;
            if (c == null || i < 0 || i >= c.objectives.Length) return 0;
            var o = c.objectives[i];
            return o.goal switch
            {
                Goal.Building => BuildingLevel(o.id),
                Goal.Crew => crew ? crew.Count : 0,
                _ => i < progress.Length ? progress[i] : 0,
            };
        }

        public bool IsDone(int i)
        {
            var c = CurrentChapter;
            return c != null && i >= 0 && i < c.objectives.Length && Amount(i) >= c.objectives[i].count;
        }

        /// <summary>Primeiro objetivo que falta e tem lugar no mapa (o marcador do HUD aponta para ele; null = nenhum).</summary>
        public string NextTargetId
        {
            get
            {
                var c = CurrentChapter;
                if (c == null || !IsOpen) return null;
                for (int i = 0; i < c.objectives.Length; i++)
                    if (!c.objectives[i].IsState && !string.IsNullOrEmpty(c.objectives[i].id) && !IsDone(i)) return c.objectives[i].MarkerId;
                return null;
            }
        }

        int BuildingLevel(string assetName)
        {
            if (!buildings) return 0;
            var list = buildings.Available;
            for (int i = 0; i < list.Count; i++)
                if (list[i] && list[i].name == assetName) return buildings.Level(list[i]);
            return 0;
        }

        void Count(Goal goal, string id)
        {
            var c = CurrentChapter;
            if (c == null || !IsOpen) return;
            bool changed = false;
            for (int i = 0; i < c.objectives.Length; i++)
            {
                var o = c.objectives[i];
                if (o.goal != goal || IsDone(i)) continue;
                if (!string.IsNullOrEmpty(o.id) && o.id != id) continue;
                progress[i]++;
                changed = true;
            }
            if (!changed) return;
            Version++;
            CheckComplete();
        }

        void OnShipSunk(ShipSunkEvent e) => Count(Goal.SinkShips, e.targetId);
        void OnCampCleared(CampClearedEvent e) => Count(Goal.ClearCamp, e.campId);
        void OnEnemyDefeated(EnemyDefeatedEvent e) => Count(Goal.DefeatEnemy, e.targetId);

        // ---------- Andamento ----------

        int lastStateSum = -1;

        void Update()
        {
            if (Finished) return;
            bool open = IsOpen;
            if (!initialized)
            {
                initialized = true;
                wasOpen = open;
            }
            else if (open && !wasOpen)
            {
                wasOpen = true;
                Version++;
                BringTargetsBack();
                Opened?.Invoke(chapter);
            }
            else wasOpen = open;

            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + stateCheckSeconds;
            // Construção e tripulação não têm evento aqui: confere de tempos em tempos (e o HUD remonta só se o valor mudou).
            var c = chapters[chapter];
            int sum = 0;
            for (int i = 0; i < c.objectives.Length; i++)
                if (c.objectives[i].IsState) sum = sum * 31 + Amount(i);
            if (sum != lastStateSum)
            {
                lastStateSum = sum;
                Version++;
            }
            if (open) UpdateTalk();
            else ReleaseTalk();
            if (open) CheckComplete();
        }

        // ---------- Conversas de história ----------

        MissionGiver talkGiver, talking;
        string[] talkLines;
        int talkIndex = -1;

        /// <summary>
        /// Põe as falas da primeira conversa que falta no NPC dela (o "!" aparece). Se o NPC já tem outra conversa de roteiro (o Velho
        /// Bento no tutorial), espera ela acabar.
        /// </summary>
        void UpdateTalk()
        {
            var c = chapters[chapter];
            int index = -1;
            for (int i = 0; i < c.objectives.Length; i++)
                if (c.objectives[i].goal == Goal.Talk && !IsDone(i))
                {
                    index = i;
                    break;
                }
            if (index < 0)
            {
                ReleaseTalk();
                return;
            }
            var giver = MissionGiver.Find(c.objectives[index].id);
            if (giver != talkGiver || index != talkIndex)
            {
                ReleaseTalk();
                if (!giver) return;
                talkGiver = giver;
                talkIndex = index;
                talkLines = WithName(c.objectives[index].lines);
                giver.ScriptedTalkFinished += OnTalkFinished;
            }
            if (!talkGiver.HasScriptedTalk) talkGiver.ScriptedLines = talkLines;
        }

        void ReleaseTalk()
        {
            if (talkGiver)
            {
                talkGiver.ScriptedTalkFinished -= OnTalkFinished;
                if (talkGiver.ScriptedLines == talkLines) talkGiver.ScriptedLines = null;
            }
            talkGiver = talking = null;
            talkLines = null;
            talkIndex = -1;
        }

        // A conversa abriu com as nossas falas (não as do tutorial): ao chegar na última, conta.
        void OnTalkStarted(MissionGiver giver)
        {
            if (giver && giver == talkGiver && giver.ScriptedLines == talkLines) talking = giver;
        }

        void OnTalkFinished(MissionGiver giver)
        {
            if (giver != talking || talkIndex < 0 || talkIndex >= progress.Length) return;
            talking = null;
            progress[talkIndex]++;
            ReleaseTalk();
            Version++;
            CheckComplete();
        }

        static string[] WithName(string[] lines)
        {
            string name = CaptainFame.Current ? CaptainFame.Current.CaptainName : "capitão";
            var result = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++) result[i] = lines[i].Replace("{nome}", name);
            return result;
        }

        void CheckComplete()
        {
            var c = CurrentChapter;
            if (c == null || c.comingSoon || c.objectives.Length == 0) return;
            for (int i = 0; i < c.objectives.Length; i++)
                if (!IsDone(i)) return;
            Advance();
        }

        /// <summary>Capítulo cumprido: faixa (StoryChapterEvent) e o próximo (que abre no próximo quadro, se a patente deixar).</summary>
        void Advance()
        {
            var c = chapters[chapter];
            int done = chapter;
            ReleaseTalk();
            chapter++;
            ResetProgress();
            initialized = true;
            wasOpen = false;
            lastStateSum = -1;
            Version++;
            EventBus.Publish(new StoryChapterEvent { chapter = done, title = c.title, next = ChapterTitle(chapter) });
        }

        /// <summary>Capítulo abriu: o acampamento repovoa, o navio com nome volta ao mar, o chefe levanta (como ao aceitar uma missão).</summary>
        void BringTargetsBack()
        {
            var c = CurrentChapter;
            if (c == null) return;
            foreach (var o in c.objectives)
                if (!o.IsState && !string.IsNullOrEmpty(o.id))
                    EventBus.Publish(new MissionStartedEvent { missionId = "historia", targetId = o.id });
        }

        /// <summary>Sandbox (F1): dá o capítulo atual por cumprido.</summary>
        public void CompleteNow()
        {
            var c = CurrentChapter;
            if (c == null || c.comingSoon) return;
            Advance();
        }

        // ---------- Textos (HISTORIA.md; dá para editar no Inspector) ----------

        static Objective Obj(string text, Goal goal, string id = "", int count = 1) => new() { text = text, goal = goal, id = id, count = count };

        static Objective Talk(string text, string npc, params string[] lines) => new() { text = text, goal = Goal.Talk, id = npc, lines = lines };

        static ChapterSpec[] DefaultChapters() => new[]
        {
            new ChapterSpec
            {
                title = "Náufrago",
                requiredRank = 0,
                intro = "Porto Alento te deu uma chalupa remendada e um cais quebrado. Mostre que a vila apostou no capitão certo.",
                outro = "O porto começa a reviver. E os cobradores de Lázaro já ouviram falar de você.",
                objectives = new[]
                {
                    Talk("Fale com o Almirante Teodoro, no estaleiro", "Almirante Teodoro",
                        "Então você é o náufrago que o Bento pescou na praia. {nome}, não é? Ainda tem água salgada nas orelhas.",
                        "Sou o Almirante Teodoro, o último oficial da Coroa neste fim de mundo. Um almirante sem esquadra, como a molecada " +
                        "adora lembrar.",
                        "Porto Alento paga tributo a Lázaro Negro há cinco anos. A Âncora Negra leva o peixe, a madeira e o nosso orgulho.",
                        "Aquela chalupa no cais é velha, mas flutua. É sua, e o cais também, em troca de uma coisa: proteja esta vila.",
                        "Comece pelo básico: mande uns navios da Âncora Negra para o fundo, junte uma tripulação e dê casa aos " +
                        "trabalhadores. Um porto vive de gente."),
                    Obj("Afunde 2 navios da Âncora Negra", Goal.SinkShips, "", 2),
                    Obj("Tenha 4 tripulantes a bordo (sobreviventes ou contratados na taverna)", Goal.Crew, "", 4),
                    Obj("Construa o Alojamento com o Mestre de obras", Goal.Building, "Building_Alojamento", 1),
                },
            },
            new ChapterSpec
            {
                title = "O tributo",
                requiredRank = 1,
                intro = "Os cobradores de Lázaro acampam na Ilha da Caveira e vêm buscar o tributo da vila. Desta vez, quem paga são eles.",
                outro = "No baú dos cobradores, uma carta de Lázaro para o Barba-Negra: \"o mapa da Coroa está quase completo\". " +
                        "O almirante vai querer ver isso.",
                objectives = new[]
                {
                    Talk("Fale com Dona Mercês, no armazém", "Dona Mercês",
                        "Ah, o capitão de quem todo mundo fala! Vou direto ao ponto, que tempo é dinheiro: os cobradores de Lázaro chegaram.",
                        "Acamparam na Ilha da Caveira, a leste. Toda lua cheia eles vêm e levam metade do armazém. Metade! Do MEU armazém.",
                        "Se você limpar aquele acampamento, a vila passa um bom tempo sem pagar tributo. E eu fico te devendo um favor, " +
                        "coisa rara.",
                        "Dizem que o chefe deles guarda as cartas de Lázaro num baú. Achou alguma coisa escrita? Leve para o almirante."),
                    Obj("Limpe o acampamento pirata da Ilha da Caveira", Goal.ClearCamp, "acampamento_caveira"),
                },
            },
            new ChapterSpec
            {
                title = "Carta de corso",
                requiredRank = 2,
                intro = "A carta dos cobradores chegou às mãos do Almirante Teodoro. Ele quer falar com você, e a Âncora Negra ainda " +
                        "guarda o Forte São Brás e o brigue Santa Clara.",
                outro = "Lázaro pôs preço na sua cabeça. Os caçadores de recompensas já estão no mar.",
                objectives = new[]
                {
                    Talk("Receba a carta de corso do Almirante Teodoro", "Almirante Teodoro",
                        "{nome}! Li a carta que você trouxe do baú dos cobradores. Lázaro escreve para o Barba-Negra sobre o mapa da " +
                        "Coroa de Prata.",
                        "Então é verdade: ele quer a Coroa. Com aquela prata, ele compra todos os portos do Mar de Prata. E nenhum " +
                        "almirante vai poder detê-lo.",
                        "Navios eu não tenho. Mas ainda tenho tinta e o selo da Coroa. Tome: uma carta de corso. A partir de hoje você " +
                        "é Corsário e caça em nome do rei.",
                        "Primeira ordem: a Âncora Negra tomou o Forte São Brás, na Ilha Norte, e o brigue Santa Clara, que era nosso. " +
                        "Cale o forte e afunde o brigue.",
                        "E abra o olho: quando Lázaro souber disso, vai pôr preço na sua cabeça."),
                    Obj("Cale o Forte São Brás, na Ilha Norte", Goal.ClearCamp, "forte_norte"),
                    Obj("Afunde o brigue Santa Clara", Goal.SinkShips, "brigue_santa_clara"),
                },
            },
            new ChapterSpec
            {
                title = "Os tenentes de Lázaro",
                requiredRank = 3,
                intro = "Dois homens seguram o reino de Lázaro: o Barba-Negra, nas ruínas da Caveira, e o próprio Lázaro, no galeão Tormenta.",
                outro = "O Tormenta foi a pique, mas Lázaro fugiu num bote jurando vingança. Agora só falta o caminho das Águas Negras.",
                objectives = new[]
                {
                    Talk("Converse com o Velho Bento, no começo do píer", "Velho Bento",
                        "Capitão, vi o Santa Clara afundar da ponta do píer. Fazia anos que eu não ria daquele jeito.",
                        "Mas escute um velho: agora Lázaro vai mandar os tenentes. O pior é o Barba-Negra, o Carniceiro das Ruínas, lá " +
                        "na Caveira.",
                        "Eu naveguei com ele, antes de ele virar bicho. Ele deixa a Guarda Negra lutar primeiro e só então entra. " +
                        "Derrube os guardas antes.",
                        "E o próprio Lázaro caça no sudeste, no galeão Tormenta. Vença os dois e o reino dele racha ao meio.",
                        "Leve rações. E volte vivo, que eu já perdi gente demais para esse mar."),
                    Obj("Derrote o Capitão Barba-Negra nas ruínas da Caveira", Goal.DefeatEnemy, "chefe_barbanegra"),
                    Obj("Afunde o galeão Tormenta, no mar do sudeste", Goal.SinkShips, "galeao_tormenta"),
                },
            },
            new ChapterSpec
            {
                title = "As Águas Negras",
                requiredRank = 4,
                intro = "A oeste, onde o mar fica preto, dorme a Coroa de Prata. E o que a guarda tem braços demais.",
                outro = "Do naufrágio da Coroa sobe uma névoa estranha, rumo a uma ilha que não estava no mapa. Lázaro chegou antes.",
                objectives = new[]
                {
                    Talk("Fale com Iara, a pescadora, ao lado do cais dos botes", "Iara",
                        "Capitão... dizem que você afundou o Tormenta. E que Lázaro fugiu num bote, remando para o oeste.",
                        "Para o oeste só tem uma coisa: as Águas Negras. Foi lá que o meu marido sumiu, procurando a Coroa de Prata.",
                        "A Coroa dorme no casco da nau capitânia da Frota de Prata. E uma coisa com braços demais dorme em cima dela.",
                        "Se for mesmo até lá, mire nos braços que prendem o casco. E, se puder... traga notícias dele."),
                    Obj("Vença a lula gigante das Águas Negras", Goal.DefeatEnemy, "lula_gigante"),
                },
            },
            new ChapterSpec
            {
                title = "A Ilha Amaldiçoada",
                requiredRank = 4,
                intro = "Lázaro fez um trato com a maldição da prata. A ilha envolta em névoa espera por você.",
                outro = "",
                comingSoon = true,
                objectives = new[]
                {
                    Talk("Fale com Tobias, o coveiro", "Tobias",
                        "Você voltou das Águas Negras. Poucos voltam. E viu a névoa, não viu? Subindo do naufrágio, rumo ao noroeste.",
                        "Eu avisei: a prata da Frota é amaldiçoada. É por causa dela que os mortos do meu cemitério levantam.",
                        "Lázaro chegou antes de você. Fez um trato com a maldição, e agora a ilha na névoa é dele.",
                        "Ainda não há mapa que leve até lá. Mas quando houver, capitão... leve sal, pólvora e coragem."),
                },
            },
        };
    }
}
