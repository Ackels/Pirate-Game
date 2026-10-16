using System.Linq;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Save;
using Pirates.Ships;
using Pirates.Story;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Protótipo 07 (M5): o mundo. Tudo da cena 05 (porto-base, upgrades, construções, navios inimigos) mais a ilha do
    /// acampamento da cena 06 a leste ("Ilha da Caveira"), um tesouro enterrado na Ilha Oeste e o quadro de missões no
    /// porto (Data/Missions; afundar navios, limpar o acampamento, entregar carga, desenterrar o tesouro) e o jogo salvo
    /// (M6.1: menu inicial Continuar/Novo jogo, salva ao atracar e ao sair do porto). Porto a pé (M6.6): F no cais atraca
    /// (o porão vai para o armazém) e o capitão desce no píer; cada parte do porto abre na casa dela, na vila.
    /// Missões existentes são preservadas (são ajuste de game design).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string WorldScenePath = ScenesFolder + "/Proto07_Mundo.unity";
        const string MissionsFolder = DataFolder + "/Missions";
        // Ilha da Caveira: a leste do porto (Ilha Sul), ~300 m de costa a costa. Mapa espaçado (16/10, pedido do Jhonata: ilhas e inimigos
        // perto demais; ~1,6×, sobra lugar para ilhas novas): era (360, −40), ~120 m de costa a costa do porto.
        static readonly Vector3 WorldIslandCenter = new(580f, 0f, -8f);
        const string CampMissionId = "acampamento_caveira", TreasureMissionId = "tesouro_ilha_oeste";
        // Ilhas Oeste e Norte (M6.9): esconderijo, segundo tesouro, forte e a guarnição dele.
        const string HideoutCampId = "esconderijo_oeste", SmugglerTreasureId = "tesouro_oeste_2", FortMissionId = "forte_norte",
            GarrisonCampId = "guarnicao_norte";
        const string OldZeDescription = "Ganhei um mapa no carteado: o X fica no lado sul da Ilha Oeste, depois da pedra grande. Desembarque e cave!";
        /// <summary>Navio ancorado: inimigos ficam longe dele enquanto o jogador está em terra.</summary>
        const float AnchorSafeRadius = 70f;

        [MenuItem("Pirates/Protótipo/07 - Criar cena do Mundo (missões)")]
        public static void BuildWorld() => Build(WorldScenePath, CombatScenario.World);

        /// <summary>Acrescenta à cena do porto (já com navio, porto, inimigos e HUDs) a terra, o tesouro e as missões.</summary>
        static void AddWorld(ShipController ship, CombatAssets combat, LootAssets loot, Material deck, Material hull)
        {
            var materials = LoadLandMaterials();
            animatorLayersBuilt = false;
            islandCenter = WorldIslandCenter;

            var hold = ship.GetComponent<Inventory>();
            // Desligada pelo PlayerModeSwitcher; liga com o navio ancorado.
            ship.gameObject.AddComponent<SafeZone>().Configure(AnchorSafeRadius);
            var shipLanding = ship.gameObject.AddComponent<ShipLandingInteraction>();

            var island = CreateCampIsland(materials, loot, deck, hull, "Ilha da Caveira", "Ilha da Caveira (praia sul)", trainingDummies: false,
                WorldScenePath);
            var foot = CreateOnFoot(ship, hold, shipLanding, island, materials, "Acampamento da Caveira", CampMissionId);
            island.campSite.gameObject.AddComponent<WorldMarker>().Configure(CampMissionId, "Acampamento da Caveira", 10f);
            // Missões dos inimigos novos (PrototypeSceneBuilder.Bounties.cs): o brutamontes do pátio e o cemitério.
            AddBounty(foot.camp, "Brutamontes", BruteSkullId, "Brutamontes (Caveira)");
            foreach (var lair in Object.FindObjectsByType<EnemyCamp>(FindObjectsInactive.Include))
                if (lair.CampId == ChiefLairId) AddBounty(lair, "Chefe", ChiefBounty, "Barba-Negra (ruínas)");
            if (island.graveyard) island.graveyard.gameObject.AddComponent<WorldMarker>().Configure(GraveyardMissionId, "Cemitério da Caveira", 5f);
            CreateIsles(materials, deck, hull, island.loot, loot.items, foot.character, ship);
            AddBrig(ship, combat, loot);
            AddBoss(ship, combat, loot);
            AddGiantSquid(ship, loot);
            AddMegalodon(ship, loot);
            AddBountyHunters(ship, combat, loot);

            // Porto a pé: F no cais atraca e desce no píer; as casas da vila abrem cada parte do porto.
            var visitor = ship.GetComponent<HarborVisitor>();
            visitor.Configure(openMenuOnDock: false);
            ship.gameObject.AddComponent<HarborShoreLeave>().Configure(foot.switcher, CreatePierLanding());
            var buildingVisitor = foot.character.gameObject.AddComponent<HarborBuildingVisitor>();
            buildingVisitor.Configure(visitor);
            var harborHud = Object.FindAnyObjectByType<HarborHUD>();
            harborHud.Bind(visitor, buildingVisitor);

            var dock = Object.FindAnyObjectByType<HarborDock>();
            if (dock) dock.gameObject.AddComponent<WorldMarker>().Configure(MissionHUD.HarborMarkerId, "Porto Alento", 12f);
            var board = new GameObject("QuadroDeMissoes").AddComponent<MissionBoard>();
            // Warehouse só existe em Play (Awake): no gerador, o Inventory do cais.
            var missions = LoadMissions(loot.items);
            board.Configure(missions, hold, dock ? dock.GetComponent<Inventory>() : null, loot.items.gold);
            // NPCs da vila que dão missões conversando (PrototypeSceneBuilder.Npcs.cs).
            CreateMissionGivers(missions, foot.character);

            // HUDs: os do navio somem em terra (o do porto fica: a pé ele mostra as casas); os de terra e o de missões entram.
            var hud = GameObject.Find("HUD");
            var shipOnly = new Behaviour[] { Object.FindAnyObjectByType<ShipDebugHUD>(), Object.FindAnyObjectByType<LootHUD>() };
            hud.AddComponent<FootHUD>().Bind(foot.switcher, shipLanding, shipOnly);
            hud.AddComponent<LandLootHUD>().Bind(foot.switcher, foot.looter, foot.unloader, null);
            hud.AddComponent<MissionHUD>().Bind(board, foot.switcher);
            var bars = Object.FindAnyObjectByType<HealthBarsHUD>();
            if (bars) bars.Ignore(foot.health);

            // Jogo salvo (M6.1): o menu inicial para o jogo até escolher Continuar ou Novo jogo.
            var save = new GameObject("JogoSalvo").AddComponent<GameSave>();
            save.Configure(ship, dock, board, foot.looter.GetComponent<Inventory>(), AllItems());
            save.SetFame(Object.FindAnyObjectByType<Pirates.Progression.CaptainFame>());
            // Jogo novo: 20 cartuchos e 3 rações no porão (no mundo, a pistola não ganha os 20 de cena de teste: o save cuida da mochila).
            save.SetNewGameKit(new[] { new ItemStack(CartridgeItem(), 20), new ItemStack(RationItem(), 3) });
            foot.character.GetComponent<PistolAmmo>().Configure(CartridgeItem(), foot.switcher, startWith: 0);
            ApplyCampFame();
            // Tutorial do jogo novo (11/10): o Velho Bento mostra o porto e o treino a pé.
            AddTutorial(foot.switcher, hud);
            // História, "A Coroa de Prata" (16/10): capítulos ligados às patentes, com objetivos que usam o que o jogo já tem.
            var story = new GameObject("Historia").AddComponent<StoryLine>();
            story.Configure(Object.FindAnyObjectByType<Pirates.Base.BaseBuildings>(), ship.GetComponent<Pirates.Progression.ShipCrew>());
            hud.AddComponent<StoryHUD>().Bind(story, foot.switcher, board);
            // Mapas do tesouro em pedaços (C1, 16/10): 3 pedaços montam um mapa e um X aparece numa ilha.
            AddTreasureMaps(materials, island.loot, loot, foot.switcher, hud);
            // Prisioneiros em gaiolas nos acampamentos (C2, 16/10): soltos com o acampamento limpo, viram tripulação.
            AddPrisoners(foot.character, foot.switcher, materials, loot.items);
            // Tela de título (11/10): "A Coroa de Prata" sobre o último quadro do prólogo, opções, créditos; o prólogo toca no Novo jogo.
            var prologue = hud.AddComponent<PrologueHUD>();
            var frames = Enumerable.Range(1, 9)
                .Select(n => AssetDatabase.LoadAssetAtPath<Texture2D>($"{PrologueFolder}/Quadro_{n}.png")).Where(t => t).ToArray();
            if (frames.Length < 9) Debug.LogWarning("[Pirates] Faltam quadros do prólogo: rode Pirates → Prólogo → Gerar quadros.");
            prologue.Configure(frames);
            var menu = hud.AddComponent<SaveMenuHUD>();
            menu.Bind(save, "");
            menu.Configure(frames.Length > 0 ? frames[^1] : null, AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/UI/Creditos.txt"), prologue);
        }

        /// <summary>Os contratos do quadro do porto (taverna).</summary>
        static MissionDefinition[] LoadMissions(LootItems i)
        {
            EnsureFolder(DataFolder, "Missions");
            var list = new System.Collections.Generic.List<MissionDefinition>
            {
                Mission("Missao_CacaNoMar", m =>
                {
                    m.missionId = "caca_no_mar";
                    m.title = "Caça no mar";
                    m.giver = "Capitã Rosa Negra";
                    m.description = "Chalupas inimigas andam rondando a nossa costa. Mande duas delas para o fundo.";
                    m.type = MissionType.SinkShips;
                    m.count = 2;
                    m.rewards = new[] { new ItemStack(i.gold, 150) };
                }),
                Mission("Missao_AcampamentoCaveira", m =>
                {
                    m.missionId = "acampamento_caveira";
                    m.title = "O acampamento da Caveira";
                    m.giver = "Velho Bento";
                    m.description = "Piratas montaram acampamento na Ilha da Caveira, a leste. Derrote todos: o baú deles fica com você.";
                    m.type = MissionType.ClearCamp;
                    m.targetId = CampMissionId;
                    m.targetName = "Ilha da Caveira";
                    m.rewards = new[] { new ItemStack(i.gold, 200), new ItemStack(i.powder, 6) };
                }),
                Mission("Missao_MadeiraEstaleiro", m =>
                {
                    m.missionId = "madeira_estaleiro";
                    m.title = "Madeira para o estaleiro";
                    m.giver = "Mestra Joana, carpinteira";
                    m.description = "O estaleiro parou por falta de material. Traga 12 de madeira e 4 de ferro no porão.";
                    m.type = MissionType.Deliver;
                    // 12 + 4 × 3 = 24 de espaço: cabe no porão da chalupa (30).
                    m.delivery = new[] { new ItemStack(i.wood, 12), new ItemStack(i.iron, 4) };
                    m.rewards = new[] { new ItemStack(i.gold, 140) };
                }),
                Mission("Missao_TesouroIlhaOeste", m =>
                {
                    m.missionId = "tesouro_ilha_oeste";
                    m.title = "O mapa do Zé Caolho";
                    m.giver = "Zé Caolho";
                    m.description = ZeDescription;
                    m.type = MissionType.FindTreasure;
                    m.targetId = TreasureMissionId;
                    m.targetName = "Ilha Oeste";
                    m.rewards = new[] { new ItemStack(i.gold, 60) };
                }),
                Mission("Missao_PolvoraFortaleza", m =>
                {
                    m.missionId = "polvora_fortaleza";
                    m.title = "Pólvora para a fortaleza";
                    m.giver = "Sargento Duarte";
                    m.description = "Os canhões da costa estão secos. Dez de pólvora, e rápido!";
                    m.type = MissionType.Deliver;
                    m.delivery = new[] { new ItemStack(i.powder, 10) };
                    m.rewards = new[] { new ItemStack(i.gold, 90), new ItemStack(i.iron, 5) };
                }),
                BrigMission(i),
                BossMission(i),
                Mission("Missao_ContrabandistasOeste", m =>
                {
                    m.missionId = "contrabandistas_oeste";
                    m.title = "Os contrabandistas da Ilha Oeste";
                    m.giver = "Dona Mercês, do armazém";
                    m.description = "Contrabandistas montaram esconderijo na enseada norte da Ilha Oeste e andam roubando a nossa carga. Expulse o bando: o baú do contrabando fica com você.";
                    m.type = MissionType.ClearCamp;
                    m.targetId = HideoutCampId;
                    m.targetName = "Ilha Oeste";
                    m.rewards = new[] { new ItemStack(i.gold, 160), new ItemStack(i.wood, 6) };
                }),
                Mission("Missao_ForteNorte", m =>
                {
                    m.missionId = "forte_norte";
                    m.title = "Calar o forte da Ilha Norte";
                    m.giver = "Almirante Teodoro";
                    m.description = "O forte da costa sul da Ilha Norte afunda quem passa perto e dá abrigo ao brigue Santa Clara. Derrube as muralhas a tiros de canhão até os canhões dele calarem.";
                    m.type = MissionType.ClearCamp;
                    m.targetId = FortMissionId;
                    m.targetName = "Ilha Norte";
                    m.rewards = new[] { new ItemStack(i.gold, 280), new ItemStack(i.iron, 8) };
                }),
                Mission("Missao_MapaContrabandista", m =>
                {
                    m.missionId = "mapa_contrabandista";
                    m.title = "O mapa do contrabandista";
                    m.giver = "Rato Velho";
                    m.description = "Um contrabandista bêbado perdeu este mapa no cais: um X na praia noroeste da Ilha Oeste, junto de um bote virado. Os comparsas dele estão logo ali...";
                    m.type = MissionType.FindTreasure;
                    m.targetId = SmugglerTreasureId;
                    m.targetName = "Ilha Oeste";
                    m.rewards = new[] { new ItemStack(i.gold, 50) };
                }),
            };
            // M10: contratos para capitães famosos (pedem patente).
            list.AddRange(FamousMissions(i));
            // Pacote B: brutamontes, duelista e cemitério.
            list.AddRange(BountyMissions(i));
            return list.ToArray();
        }

        const string ZeDescription = "Ganhei um mapa no carteado: o X fica numa gruta de pedras no lado sul do morro da Ilha Oeste, depois da pedra grande. Desembarque no lado leste e cave!";

        static MissionDefinition Mission(string file, System.Action<MissionDefinition> initialize)
        {
            var mission = LoadOrCreateAsset($"{MissionsFolder}/{file}.asset", initialize);
            // Forte (M6.9): textos próprios (o padrão de ClearCamp fala em acampamento).
            if (mission.targetId == FortMissionId && string.IsNullOrEmpty(mission.objectiveText))
            {
                mission.objectiveText = "cale os canhões do forte da Ilha Norte (tiros nas muralhas)";
                mission.doneText = "forte calado";
                EditorUtility.SetDirty(mission);
            }
            // M6.9: o tesouro do Zé foi para a gruta. Só troca o texto que o gerador escreveu (ajuste seu fica).
            if (mission.description == OldZeDescription)
            {
                mission.description = ZeDescription;
                EditorUtility.SetDirty(mission);
            }
            ApplyMissionFame(mission);
            return mission;
        }
    }
}
