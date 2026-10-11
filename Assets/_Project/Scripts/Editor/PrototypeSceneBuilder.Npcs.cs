using Pirates.Ambient;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// NPCs da vila que dão missões conversando (10/10, pedido do Jhonata: "pegar algumas missões com alguns NPCs", com fala na tela
    /// como no Palworld, em estilo pirata; as missões "com história" saem do quadro da taverna e a entrega é com o NPC). Cada um é um
    /// morador do kit (Anne/Henry recoloridos, Animator dos civis) com o <see cref="MissionGiver"/> e um WorldMarker (o HUD de missões
    /// aponta para ele quando a missão está pronta); o personagem ganha o <see cref="NpcTalker"/> (F conversa) e o HUD o
    /// <see cref="DialogueHUD"/>. As falas ficam na própria missão (Data/Missions), escritas aqui só na primeira vez (o que o Jhonata
    /// mudar no asset fica). Passo 1: Tobias, o coveiro (cemitério). Passo 2: Velho Bento (carrasco), Dona Mercês (duelista), Almirante
    /// Teodoro (guardião do forte), Zé Caolho e Rato Velho (os dois mapas de tesouro).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Um NPC: quem é, onde fica (local do porto) e para onde olha, a roupa, a missão (pelo id) e a conversa à toa.</summary>
        sealed class NpcSpec
        {
            public string name, role, missionId, model;
            public Vector2 spot;
            public float yaw;
            public int palette;
            public bool shovel;
            public string[] smallTalk;
            public System.Action<MissionDefinition> dialogue;
        }

        static NpcSpec[] VillageNpcs => new[]
        {
            new NpcSpec
            {
                // A oeste da praça, com a pá.
                name = "Tobias", role = "o coveiro", missionId = GraveyardMissionId, spot = new(-20f, 22f), yaw = 61f,
                model = KitCharacters.Henry, palette = 4, shovel = true, dialogue = GraveyardDialogue,
                smallTalk = new[]
                {
                    "Cova funda, boca fechada: é o que eu sempre digo.",
                    "Faz tempo que eu não piso na Caveira. E não piso mais.",
                    "Se um dia precisar de uma cova, capitão... tomara que não.",
                },
            },
            new NpcSpec
            {
                // No começo do píer, de frente para quem desembarca.
                name = "Velho Bento", role = "contramestre aposentado", missionId = BruteSkullId, spot = new(4f, 54f), yaw = 10f,
                model = KitCharacters.Henry, palette = 5, dialogue = BentoDialogue,
                smallTalk = new[]
                {
                    "Contramestre aposentado, capitão. Aposentado à força.",
                    "No meu tempo, pirata respeitava velho.",
                    "O mar não esquece quem o desafia.",
                },
            },
            new NpcSpec
            {
                // Ao lado do armazém.
                name = "Dona Mercês", role = "dona do armazém", missionId = DuelistHideoutId, spot = new(14f, 53f), yaw = 200f,
                model = KitCharacters.Anne, palette = 5, dialogue = MercesDialogue,
                smallTalk = new[]
                {
                    "Se for vender alguma coisa, vá ao mercado. Se for roubar, vá embora.",
                    "O armazém não descansa, capitão.",
                    "Tempo é dinheiro. E o dinheiro é meu.",
                },
            },
            new NpcSpec
            {
                // No estaleiro.
                name = "Almirante Teodoro", role = "do almirantado", missionId = BruteFortId, spot = new(34f, 45f), yaw = 220f,
                model = KitCharacters.Henry, palette = 6, dialogue = TeodoroDialogue,
                smallTalk = new[]
                {
                    "Ordem e disciplina, capitão. É o que falta nestes mares.",
                    "O almirantado observa tudo.",
                    "Os olhos no horizonte e os pés no cais.",
                },
            },
            new NpcSpec
            {
                // Na porta da taverna (de onde ele quase não sai).
                name = "Zé Caolho", role = "jogador de cartas", missionId = TreasureMissionId, spot = new(-10f, 51f), yaw = 160f,
                model = KitCharacters.Henry, palette = 7, dialogue = ZeDialogue,
                smallTalk = new[]
                {
                    "Perdi o olho numa aposta. Ganhei o tapa-olho em outra.",
                    "Na taverna eu sou rei. Fora dela, nem tanto.",
                    "Uma partida? Não? Melhor para o seu ouro.",
                },
            },
            new NpcSpec
            {
                // Num canto escondido, perto do Mestre de obras.
                name = "Rato Velho", role = "rato de cais", missionId = "mapa_contrabandista", spot = new(-28f, 44f), yaw = 90f,
                model = KitCharacters.Henry, palette = 8, dialogue = RatoDialogue,
                smallTalk = new[]
                {
                    "Rato velho não cai em ratoeira, capitão.",
                    "Eu vejo tudo, eu ouço tudo. E não digo nada. A não ser por ouro.",
                    "Psst... nada, nada. Só queria ver se você estava prestando atenção.",
                },
            },
        };

        /// <summary>Conversa no personagem e no HUD, e os NPCs com as missões deles (cena 07, depois do quadro de missões).</summary>
        static void CreateMissionGivers(MissionDefinition[] missions, FootCharacter character)
        {
            var talker = character.gameObject.AddComponent<NpcTalker>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            if (hud) hud.gameObject.AddComponent<DialogueHUD>().Bind(talker);
            if (!HasKitCharacter(KitCharacters.Henry) || !HasKitCharacter(KitCharacters.Anne)) return;

            var life = GameObject.Find("VidaDoPorto")?.transform;
            foreach (var npc in VillageNpcs)
            {
                var mission = System.Array.Find(missions, m => m && m.missionId == npc.missionId);
                if (mission && !mission.givenByNpc)
                {
                    mission.givenByNpc = true;
                    mission.giver = npc.name;
                    npc.dialogue(mission);
                    EditorUtility.SetDirty(mission);
                }
                MissionGiverNpc(life, npc, mission ? new[] { mission } : System.Array.Empty<MissionDefinition>());
            }
        }

        /// <summary>Um NPC parado no porto (como os moradores), que gesticula, com o MissionGiver e o WorldMarker.</summary>
        static MissionGiver MissionGiverNpc(Transform parent, NpcSpec npc, MissionDefinition[] given)
        {
            var ground = PortGround(npc.spot);
            var go = new GameObject("NPC_" + npc.name.Replace(' ', '_'));
            if (parent) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, npc.yaw, 0f));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 1.7f;
            var visual = KitCharacter(npc.model, go.transform, CivilianMaterial(npc.model, npc.palette));
            if (!visual.TryGetComponent(out Animator animator)) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = CivilAnimator(npc.model);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var idle = go.AddComponent<Townsperson>();
            idle.Configure(animator, null);
            var giver = go.AddComponent<MissionGiver>();
            giver.Configure(npc.name, npc.role, given, npc.smallTalk, animator, idle);
            // Marcador acima do "!"/"?" da cabeça.
            go.AddComponent<WorldMarker>().Configure(MissionGiver.MarkerIdFor(npc.name), $"{npc.name}, {npc.role}", 4.3f);
            if (npc.shovel)
            {
                // Pá fincada no chão ao lado (o coveiro): o cabo inclinado.
                var shovel = Model(Kit.Survival, "tool-shovel", go.transform, new Vector3(0.75f, 0.35f, 0.25f), 20f, SurvivalScale, "Pa");
                shovel.transform.localRotation *= Quaternion.Euler(0f, 0f, 75f);
            }
            return giver;
        }

        static void Lines(MissionDefinition m, string[] offer, string accept, string decline, string waiting, string[] done, string resting)
        {
            m.offerLines = offer;
            m.acceptLine = accept;
            m.declineLine = decline;
            m.waitingLine = waiting;
            m.doneLines = done;
            m.restingLine = resting;
        }

        static void GraveyardDialogue(MissionDefinition m) => Lines(m, new[]
            {
                "Psiu... você aí, capitão. Tem um minuto para um velho coveiro? Não? Pois vai ter.",
                "Trinta anos eu enterrei pirata naquele cemitério ao lado da trilha das ruínas, na Ilha da Caveira. Gente ruim, gente boa... a terra não escolhe.",
                "Mês passado fui abrir uma cova nova, já de noite, com a lanterna acesa. Aí a terra começou a mexer. Não era bicho, capitão. Era uma mão. Verde. Só osso.",
                "Um por um eles foram saindo das covas. Os mesmos que eu enterrei! Larguei a pá ali mesmo e corri para o bote. Nunca remei tão rápido na vida.",
                "Eles só saem de noite, e o sol desfaz tudo em pó. Mas escute bem: derrubado, o desgraçado levanta de novo. Só a pólvora acaba com eles de vez: tiro ou bomba.",
                "Se tiver coragem, vá lá depois que o sol se puser e devolva aqueles coitados para a terra. Eu pago o que tenho, e ainda dou uns cartuchos que guardei.",
            },
            "Que os santos te protejam, capitão. Vá depois que escurecer, e não dê as costas para cova nenhuma.",
            "Eu entendo. Eu também não voltaria lá... nem bêbado.",
            "Eles ainda estão de pé, não estão? Eu juro que escuto os gemidos daqui, à noite. Vá depois que o sol se puser.",
            new[]
            {
                "Você voltou! E inteiro! Então é verdade... o cemitério está em paz?",
                "Obrigado, capitão. Hoje eu durmo de lanterna apagada. Tome o combinado, e os cartuchos, como prometi.",
            },
            "Por enquanto está tudo quieto por lá. Mas maldição de pirata não acaba fácil... volte daqui a uns dias, que eu aviso se a terra mexer de novo.");

        static void BentoDialogue(MissionDefinition m) => Lines(m, new[]
            {
                "Ei, capitão! Sente aqui um minuto com o velho Bento. Já fui contramestre, sabe? Hoje só conto as minhas cicatrizes.",
                "Mês passado eu e mais três rapazes desembarcamos na Caveira atrás de uns barris de rum. Rum! Três homens morreram por causa de rum.",
                "No pátio daquele acampamento tem um homem-tubarão do tamanho de uma porta, com um machado de duas lâminas. Ele partiu o Joaquim ao meio com um golpe só.",
                "Mas eu reparei numa coisa enquanto fugia: antes de descer, ele levanta o machado e fica parado um instante. Quem erguer a guarda na hora certa deixa ele tonto. Ou role para longe.",
                "Derrube aquele carrasco, por mim e pelos rapazes. O que eu juntei é seu, e ainda tenho uns ferros guardados.",
            },
            "Esse é o espírito! Vou acender uma vela por você... e outra pelo rum que ficou lá.",
            "Tem razão de ter medo. Eu também teria, se ainda tivesse juízo.",
            "E então? O carrasco ainda está de pé? Lembre: quando ele levantar o machado, guarda ou rolamento.",
            new[]
            {
                "Você derrubou o carrasco? Pelas barbas do meu avô!",
                "Hoje eu bebo em nome do Joaquim e dos outros. Tome, capitão: o ouro e o ferro, como prometi.",
            },
            "Dizem que já tem outro brutamontes no lugar dele. Esses tubarões não acabam nunca... volte daqui a um tempo.");

        static void MercesDialogue(MissionDefinition m) => Lines(m, new[]
            {
                "Capitão! Ainda bem que apareceu. Estou perdendo dinheiro todo santo dia.",
                "Os contrabandistas da Ilha Oeste roubam a minha carga antes de ela chegar ao porto. Antes eram uns bêbados. Agora contrataram uma duelista.",
                "Uma moça de adaga, rápida como um peixe-espada. Mandei dois guardas ao esconderijo; os dois voltaram costurados.",
                "Dizem que ela apara qualquer golpe e rola para longe. Mas o meu sobrinho jura que um chute bem dado derruba a guarda dela.",
                "Tire essa duelista do caminho e eu pago bem. Ainda dou três bombas que confisquei de um contrabandista.",
            },
            "Ótimo. Traga de volta a minha paz... e, se puder, a minha carga.",
            "Hum. Então volte quando tiver mais coragem, ou menos amor à vida.",
            "A duelista ainda está lá? Cada dia é mais uma carga perdida, capitão.",
            new[]
            {
                "A duelista caiu? Finalmente! Os contrabandistas vão pensar duas vezes antes de mexer comigo.",
                "Aqui está, capitão: o ouro e as bombas, como combinado. Negócio é negócio.",
            },
            "Por enquanto a enseada está quieta. Mas contrabandista é praga: logo arrumam outra lâmina.");

        static void TeodoroDialogue(MissionDefinition m) => Lines(m, new[]
            {
                "Capitão. Aproxime-se. O almirantado tem um serviço que não pode esperar pela esquadra.",
                "O forte da Ilha Norte é o ninho daquela gente. Mesmo com os canhões calados, há uma guarnição no pátio. E um guardião.",
                "Outro brutamontes, maior que o da Caveira, segundo os meus batedores. Os soldados o chamam de \"o guardião\". Nenhum voltou para confirmar.",
                "Desembarque, entre no pátio e derrube o guardião. A guarnição vai cair em cima de você: leve pólvora e não fique parado.",
                "O almirantado paga em ouro e em pólvora. E não esquece quem serve bem.",
            },
            "Excelente. Que os ventos estejam a seu favor, capitão.",
            "Compreendo. Nem todo capitão está pronto para isso. Por enquanto.",
            "O guardião do forte continua de pé, capitão. O almirantado aguarda.",
            new[]
            {
                "O guardião caiu? Então o forte da Ilha Norte está sem cabeça. Muito bem.",
                "O seu pagamento, em nome do almirantado. Ouro e pólvora, como combinado.",
            },
            "A guarnição ainda se reorganiza. Quando houver um novo guardião, eu mando avisar.");

        static void ZeDialogue(MissionDefinition m) => Lines(m, new[]
            {
                "Psst! Capitão! Quer ficar rico? Claro que quer. Todo mundo quer.",
                "Ganhei este mapa no carteado, de um pirata que roubava nas cartas. Eu também roubava, mas ele roubava pior.",
                "O X fica numa gruta de pedras, no lado sul do morro da Ilha Oeste, depois da pedra grande. Desembarque pelo lado leste e cave.",
                "Eu iria, mas com um olho só eu enxergo metade do mapa. Leve você, e depois me traga a minha parte.",
            },
            "Isso! Quando voltar, a gente divide. Do jeito justo: eu conto.",
            "Pior para você. Vou oferecer ao próximo caolho que aparecer.",
            "Já cavou? Lado sul do morro da Ilha Oeste, a gruta de pedras! Não vá cavar no lugar errado, hein.",
            new[]
            {
                "Achou o tesouro! Eu sabia que aquele mapa era bom... pelo menos este.",
                "Tome a sua recompensa. Digo, a minha parte da sua parte... Enfim, foi um prazer, capitão.",
            },
            "Estou sem mapas agora. Mas o carteado de hoje à noite promete...");

        static void RatoDialogue(MissionDefinition m) => Lines(m, new[]
            {
                "Shh! Fale baixo, capitão. As paredes deste porto têm ouvidos, e os ouvidos têm facas.",
                "Um contrabandista bêbado deixou cair este mapa no cais. Eu peguei. Ninguém viu. Certo?",
                "O X fica na praia noroeste da Ilha Oeste, junto de um bote virado. O problema: os comparsas dele estão logo ali, no esconderijo.",
                "Eu sou rápido, mas não sou valente. Você parece as duas coisas. Cave antes que eles percebam.",
            },
            "Ótimo, ótimo. E se perguntarem, você nunca me viu.",
            "Tudo bem, tudo bem. Esqueça que eu existo. Todo mundo esquece.",
            "E o bote virado? Praia noroeste da Ilha Oeste. Rápido, antes que o dono volte sóbrio.",
            new[]
            {
                "Você achou! E ainda está vivo. Melhor ainda.",
                "Aqui, a sua parte. E lembre: não fui eu quem te deu aquele mapa.",
            },
            "Sem mapas agora. Mas os bêbados do cais sempre perdem alguma coisa...");
    }
}
