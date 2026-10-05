using System;
using System.Collections;
using System.Collections.Generic;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Missions;
using Pirates.Progression;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Pirates.Save
{
    /// <summary>
    /// Salva e carrega o progresso do mundo (cena 07): porão, mochila, casco, upgrades do navio, armazém, construções
    /// da base, obras em andamento e missões. Salva sozinho ao atracar no porto e ao sair dele, ao sair de uma casa do
    /// porto a pé (compras feitas lá) e ao fechar o jogo atracado. Carregar põe o navio parado na vaga do porto. As ilhas não são salvas: voltam cheias.
    /// Quem escolhe entre continuar e começar de novo é o menu inicial (SaveMenuHUD), enquanto WaitingForChoice.
    /// </summary>
    public class GameSave : MonoBehaviour
    {
        public enum StartMode { Menu, Continue, NewGame }

        // Pedido para a próxima vez que a cena começar (o sandbox recarrega a cena para carregar ou recomeçar).
        static StartMode requestedStart = StartMode.Menu;

        [SerializeField] ShipController ship;
        [SerializeField] HarborDock harbor;
        [SerializeField] MissionBoard missions;
        [Tooltip("Mochila do personagem a pé.")]
        [SerializeField] Inventory backpack;
        [Tooltip("Todos os itens que podem estar num save (o gerador põe tudo de Data/Items).")]
        [SerializeField] ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [Tooltip("Fama do capitão (M10).")]
        [SerializeField] CaptainFame fame;
        [Tooltip("O que o jogo novo traz no porão (cartuchos da pistola, rações).")]
        [SerializeField] ItemStack[] newGameKit = Array.Empty<ItemStack>();

        readonly Dictionary<string, ItemDefinition> itemsById = new();
        Inventory hold;
        Health hull;
        ShipUpgrades upgrades;
        PlayerShipClass shipClass;
        HarborVisitor visitor;
        HarborBuildingVisitor buildingVisitor;
        bool suspended;

        /// <summary>O menu inicial está na tela: o jogo espera Continue() ou NewGame().</summary>
        public bool WaitingForChoice { get; private set; }
        /// <summary>Saiu do menu. Antes disso nada é salvo (o menu não pode sobrescrever o save com um jogo vazio).</summary>
        public bool Started { get; private set; }
        public bool HasSave => SaveFile.Exists;
        public string FilePath => SaveFile.FilePath;
        public DateTime? LastSaved { get; private set; }

        public event Action<SaveData> Saved;
        /// <summary>Não deu para salvar ou carregar (o motivo, para o HUD).</summary>
        public event Action<string> Failed;

        public void Configure(ShipController playerShip, HarborDock baseHarbor, MissionBoard board, Inventory characterBackpack,
            ItemDefinition[] allItems)
        {
            ship = playerShip;
            harbor = baseHarbor;
            missions = board;
            backpack = characterBackpack;
            items = allItems;
        }

        public void SetFame(CaptainFame captainFame) => fame = captainFame;

        public void SetNewGameKit(ItemStack[] kit) => newGameKit = kit;

        void Awake()
        {
            foreach (var item in items)
                if (item) itemsById[item.name] = item;
            if (ship)
            {
                ship.TryGetComponent(out hold);
                ship.TryGetComponent(out hull);
                ship.TryGetComponent(out upgrades);
                ship.TryGetComponent(out shipClass);
                ship.TryGetComponent(out visitor);
            }
            if (backpack) backpack.TryGetComponent(out buildingVisitor);
            WaitingForChoice = requestedStart == StartMode.Menu;
        }

        IEnumerator Start()
        {
            var mode = requestedStart;
            requestedStart = StartMode.Menu;
            if (mode == StartMode.Menu) yield break;
            // Um frame: todos os Start (upgrades, construções, câmera) já rodaram.
            yield return null;
            if (mode == StartMode.Continue && !Continue()) NewGame();
            else if (mode == StartMode.NewGame) NewGame();
        }

        void OnEnable()
        {
            if (buildingVisitor) buildingVisitor.Left += OnBuildingLeft;
            if (!visitor) return;
            visitor.Docked += OnHarborChanged;
            visitor.Undocked += OnHarborChanged;
        }

        void OnDisable()
        {
            if (buildingVisitor) buildingVisitor.Left -= OnBuildingLeft;
            if (!visitor) return;
            visitor.Docked -= OnHarborChanged;
            visitor.Undocked -= OnHarborChanged;
        }

        void OnApplicationQuit()
        {
            // Fechar o jogo com o porto aberto: as compras feitas lá não se perdem.
            if (visitor && visitor.IsDocked) AutoSave();
            suspended = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => requestedStart = StartMode.Menu;

        void OnHarborChanged(HarborDock _) => AutoSave();
        void OnBuildingLeft(HarborBuilding _) => AutoSave();

        void AutoSave()
        {
            if (Started && !suspended) SaveNow();
        }

        // ---------- Menu / sandbox ----------

        /// <summary>Lê o save sem aplicar (resumo no menu).</summary>
        public bool TryPeek(out SaveData data, out string error) => SaveFile.TryRead(out data, out error);

        public ItemDefinition FindItem(string id) => id != null && itemsById.TryGetValue(id, out var item) ? item : null;

        /// <summary>Carrega o save e começa o jogo nele (navio na vaga do porto).</summary>
        public bool Continue()
        {
            if (!SaveFile.TryRead(out var data, out string error))
            {
                Failed?.Invoke($"Não deu para carregar: {error}.");
                return false;
            }
            Apply(data);
            LastSaved = ParseDate(data.savedAt);
            WaitingForChoice = false;
            Started = true;
            return true;
        }

        /// <summary>
        /// Começa do zero (o jogo como a cena nasce), com o capitão <paramref name="captainName"/> (vazio = o nome padrão). Um save
        /// que existir vai para o .bak.
        /// </summary>
        public void NewGame(string captainName = null)
        {
            SaveFile.Archive();
            if (fame) fame.ResetAll(captainName);
            if (hold)
                foreach (var stack in newGameKit)
                    if (stack.item && stack.amount > 0) hold.Add(stack.item, stack.amount);
            LastSaved = null;
            WaitingForChoice = false;
            Started = true;
        }

        /// <summary>Recarrega a cena e já continua do save ou começa de novo, sem o menu (botões do sandbox).</summary>
        public void ReloadScene(StartMode mode)
        {
            if (mode == StartMode.NewGame) SaveFile.Archive();
            requestedStart = mode;
            suspended = true;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        /// <summary>Grava o progresso agora. false (e o evento Failed) se não deu.</summary>
        public bool SaveNow()
        {
            if (hull && hull.IsDead)
            {
                Failed?.Invoke("Não dá para salvar com o navio afundando.");
                return false;
            }
            try
            {
                var data = Capture();
                SaveFile.Write(data);
                LastSaved = ParseDate(data.savedAt);
                Saved?.Invoke(data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Não deu para salvar em {FilePath}: {e}");
                Failed?.Invoke($"Não deu para salvar: {e.Message}");
                return false;
            }
        }

        // ---------- Jogo → arquivo ----------

        SaveData Capture()
        {
            var data = new SaveData
            {
                savedAt = DateTime.Now.ToString("o"),
                harborName = harbor ? harbor.DisplayName : "",
                hull = hull ? hull.Current : 0f,
                shipClass = shipClass && shipClass.Current ? shipClass.Current.name : "",
                hold = Items(hold),
                backpack = Items(backpack),
                warehouse = Items(harbor ? harbor.Warehouse : null),
            };

            if (upgrades)
            {
                var list = new List<LevelEntry>();
                foreach (var u in upgrades.Available)
                    if (u && upgrades.Level(u) > 0) list.Add(new LevelEntry(u.name, upgrades.Level(u)));
                data.upgrades = list.ToArray();
            }
            if (harbor)
            {
                var demand = new List<TimerEntry>();
                foreach (var (item, left) in harbor.DemandLeft)
                    if (item) demand.Add(new TimerEntry(item.name, left));
                data.marketDemand = demand.ToArray();
            }
            if (harbor && harbor.Smithy != null)
                data.smithyOrder = Job(harbor.Smithy.recipe.result.item, harbor.Smithy.times, harbor.Smithy.duration, harbor.Smithy.progress, harbor.Smithy.paid);
            if (harbor && harbor.Job != null)
                data.shipyardJob = Job(harbor.Job.shipClass ? harbor.Job.shipClass : harbor.Job.upgrade, harbor.Job.level,
                    harbor.Job.duration, harbor.Job.progress, harbor.Job.paid);

            var buildings = harbor ? harbor.Buildings : null;
            if (buildings)
            {
                var levels = new List<LevelEntry>();
                foreach (var b in buildings.Available)
                    if (b && buildings.Level(b) > 0) levels.Add(new LevelEntry(b.name, buildings.Level(b)));
                data.buildings = levels.ToArray();
                var timers = new List<TimerEntry>();
                foreach (var (b, seconds) in buildings.ProductionTimers)
                    if (b) timers.Add(new TimerEntry(b.name, seconds));
                data.production = timers.ToArray();
                if (buildings.Job != null)
                    data.buildingJob = Job(buildings.Job.building, buildings.Job.level, buildings.Job.duration, buildings.Job.progress, buildings.Job.paid);
            }

            if (fame)
            {
                data.hasFame = true;
                data.captainName = fame.CaptainName;
                data.fame = fame.Fame;
                data.perks = Ids(fame.Perks);
                data.perkOffer = Ids(fame.Offer);
                var c = fame.Counts;
                data.shipsSunk = c.shipsSunk;
                data.campsCleared = c.campsCleared;
                data.treasuresFound = c.treasuresFound;
                data.missionsDone = c.missionsDone;
            }

            if (missions)
            {
                var active = new List<MissionEntry>();
                foreach (var m in missions.Active)
                    active.Add(new MissionEntry { id = m.definition.missionId, progress = m.progress, objectiveDone = m.objectiveDone });
                data.activeMissions = active.ToArray();
                var finished = new List<string>();
                foreach (var m in missions.Finished)
                    if (m) finished.Add(m.missionId);
                data.finishedMissions = finished.ToArray();
                var resting = new List<TimerEntry>();
                foreach (var (m, seconds) in missions.Cooldowns)
                    if (m && seconds > 0f) resting.Add(new TimerEntry(m.missionId, seconds));
                data.missionCooldowns = resting.ToArray();
            }
            return data;
        }

        static string[] Ids(IReadOnlyList<FameSettings.Perk> perks)
        {
            var ids = new string[perks.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = perks[i].id;
            return ids;
        }

        static ItemCount[] Items(Inventory inventory)
        {
            if (!inventory) return Array.Empty<ItemCount>();
            var list = new List<ItemCount>();
            foreach (var item in inventory.Items) list.Add(new ItemCount(item.name, inventory.Count(item)));
            return list.ToArray();
        }

        static ItemCount[] Items(ItemStack[] stacks)
        {
            var list = new List<ItemCount>();
            if (stacks != null)
                foreach (var s in stacks)
                    if (s.item) list.Add(new ItemCount(s.item.name, s.amount));
            return list.ToArray();
        }

        static JobEntry Job(Object what, int level, float duration, float progress, ItemStack[] paid) => new()
        {
            id = what ? what.name : null,
            level = level,
            duration = duration,
            progress = progress,
            paid = Items(paid),
        };

        // ---------- Arquivo → jogo ----------

        void Apply(SaveData data)
        {
            var missing = new List<string>();

            if (hold) hold.SetContents(Stacks(data.hold, missing));
            if (backpack) backpack.SetContents(Stacks(data.backpack, missing));
            if (harbor) harbor.Warehouse.SetContents(Stacks(data.warehouse, missing));
            RemoveDuplicateSchematics();

            // Classe do navio antes dos upgrades (eles somam em cima dos valores base dela) e do casco.
            if (shipClass && !string.IsNullOrEmpty(data.shipClass))
            {
                var saved = Find(data.shipClass, shipClass.Classes, missing);
                if (saved) shipClass.SetClass(saved, newShip: false);
            }

            // Upgrades antes do casco: o upgrade de casco muda a vida máxima.
            if (upgrades) upgrades.SetLevels(Levels(data.upgrades, upgrades.Available, missing));
            if (hull && data.hull > 0f) hull.SetCurrent(data.hull);

            if (harbor)
            {
                HarborDock.ConstructionJob shipyard = null;
                var newShip = data.shipyardJob.Active && shipClass && upgrades ? FindQuiet(data.shipyardJob.id, shipClass.Classes) : null;
                var upgrade = data.shipyardJob.Active && upgrades && !newShip ? Find(data.shipyardJob.id, upgrades.Available, missing) : null;
                if (newShip && newShip != shipClass.Current)
                    shipyard = new HarborDock.ConstructionJob
                    {
                        shipClass = newShip,
                        ship = upgrades,
                        paid = Stacks(data.shipyardJob.paid, missing).ToArray(),
                        duration = data.shipyardJob.duration,
                        progress = data.shipyardJob.progress,
                    };
                else if (upgrade && !upgrades.IsMaxed(upgrade))
                    shipyard = new HarborDock.ConstructionJob
                    {
                        upgrade = upgrade,
                        level = upgrades.Level(upgrade) + 1,
                        ship = upgrades,
                        paid = Stacks(data.shipyardJob.paid, missing).ToArray(),
                        duration = data.shipyardJob.duration,
                        progress = data.shipyardJob.progress,
                    };
                harbor.RestoreJob(shipyard);
                var demandLeft = new List<KeyValuePair<ItemDefinition, float>>();
                foreach (var d in data.marketDemand ?? Array.Empty<TimerEntry>())
                {
                    var item = FindItem(d.id);
                    if (item) demandLeft.Add(new KeyValuePair<ItemDefinition, float>(item, d.seconds));
                }
                harbor.RestoreDemand(demandLeft);

                HarborDock.SmithyOrder smithy = null;
                if (data.smithyOrder != null && data.smithyOrder.Active && harbor.Settings)
                    foreach (var recipe in harbor.Settings.smithy)
                        if (recipe?.result.item && recipe.result.item.name == data.smithyOrder.id && data.smithyOrder.level > 0)
                        {
                            smithy = new HarborDock.SmithyOrder
                            {
                                recipe = recipe,
                                times = data.smithyOrder.level,
                                paid = Stacks(data.smithyOrder.paid, missing).ToArray(),
                                duration = data.smithyOrder.duration,
                                progress = data.smithyOrder.progress,
                            };
                            break;
                        }
                harbor.RestoreSmithy(smithy);

                var buildings = harbor.Buildings;
                if (buildings)
                {
                    BaseBuildings.BuildJob job = null;
                    var building = data.buildingJob.Active ? Find(data.buildingJob.id, buildings.Available, missing) : null;
                    if (building && data.buildingJob.level <= building.MaxLevel)
                        job = new BaseBuildings.BuildJob
                        {
                            building = building,
                            level = data.buildingJob.level,
                            paid = Stacks(data.buildingJob.paid, missing).ToArray(),
                            duration = data.buildingJob.duration,
                            progress = data.buildingJob.progress,
                        };
                    var timers = new List<KeyValuePair<BuildingDefinition, float>>();
                    foreach (var t in data.production)
                    {
                        var b = Find(t.id, buildings.Available, missing);
                        if (b) timers.Add(new KeyValuePair<BuildingDefinition, float>(b, t.seconds));
                    }
                    buildings.Restore(Levels(data.buildings, buildings.Available, missing), job, timers);
                }
            }

            if (missions)
            {
                var active = new List<MissionBoard.ActiveMission>();
                foreach (var m in data.activeMissions)
                {
                    var def = missions.Find(m.id);
                    if (def) active.Add(new MissionBoard.ActiveMission { definition = def, progress = m.progress, objectiveDone = m.objectiveDone });
                    else missing.Add(m.id);
                }
                var finished = new List<MissionDefinition>();
                foreach (string id in data.finishedMissions)
                {
                    var def = missions.Find(id);
                    if (def) finished.Add(def);
                    else missing.Add(id);
                }
                var resting = new List<KeyValuePair<MissionDefinition, float>>();
                foreach (var t in data.missionCooldowns ?? Array.Empty<TimerEntry>())
                {
                    var def = missions.Find(t.id);
                    if (def) resting.Add(new KeyValuePair<MissionDefinition, float>(def, t.seconds));
                }
                missions.Restore(active, finished, resting);
            }

            // Fama depois das missões (save de antes do M10: a fama das que já foram entregues) e antes de nada usar os bônus
            // (o navio e o porto recalculam no PerksChanged).
            if (fame)
            {
                var stats = new CaptainFame.Stats
                {
                    shipsSunk = data.shipsSunk, campsCleared = data.campsCleared,
                    treasuresFound = data.treasuresFound, missionsDone = data.missionsDone,
                };
                int fameValue = data.fame;
                if (!data.hasFame && missions)
                {
                    fameValue = 0;
                    int floorRank = 0;
                    foreach (var m in missions.Finished)
                        if (m)
                        {
                            fameValue += m.fameReward;
                            floorRank = Mathf.Max(floorRank, m.requiredRank);
                        }
                    // Nada que já foi feito fica trancado: no mínimo a patente da missão mais difícil já entregue.
                    fameValue = Mathf.Max(fameValue, fame.RankFame(floorRank));
                    stats.missionsDone = missions.Finished.Count;
                }
                fame.Restore(data.captainName, fameValue, data.perks ?? Array.Empty<string>(), data.perkOffer ?? Array.Empty<string>(), stats);
            }

            PlaceShipAtBerth();
            if (missing.Count > 0)
                Debug.LogWarning($"[Save] O save cita coisas que o jogo não tem mais (ignoradas): {string.Join(", ", missing)}");
        }

        /// <summary>
        /// Esquema repetido (05/10): o armazém já tem, então as cópias do porão e da mochila saem (de antes do E1, o baú raro podia
        /// repetir um esquema guardado no armazém; ter dois não serve para nada e ocupava a lista).
        /// </summary>
        void RemoveDuplicateSchematics()
        {
            if (!harbor) return;
            var removed = new List<string>();
            foreach (var item in new List<ItemDefinition>(harbor.Warehouse.Items))
            {
                if (item.category != ItemCategory.Schematic) continue;
                foreach (var inv in new[] { hold, backpack })
                    if (inv && inv.Remove(item, inv.Count(item)) > 0) removed.Add(item.name);
            }
            if (removed.Count > 0) Debug.Log($"[Save] Esquemas repetidos tirados do porão/mochila (o armazém já tem): {string.Join(", ", removed)}");
        }

        /// <summary>Navio parado na vaga do porto, a câmera já nele.</summary>
        void PlaceShipAtBerth()
        {
            if (!ship || !harbor) return;
            var berth = harbor.Berth;
            ship.Teleport(new Vector3(berth.position.x, ship.transform.position.y, berth.position.z), berth.rotation);
            if (Services.TryGet(out ICameraFollow camera)) camera.SnapToTarget();
        }

        List<ItemStack> Stacks(ItemCount[] counts, List<string> missing)
        {
            var list = new List<ItemStack>();
            if (counts == null) return list;
            foreach (var c in counts)
            {
                var item = FindItem(c.id);
                if (item) list.Add(new ItemStack(item, c.amount));
                else missing.Add(c.id);
            }
            return list;
        }

        static List<KeyValuePair<T, int>> Levels<T>(LevelEntry[] entries, IReadOnlyList<T> known, List<string> missing) where T : Object
        {
            var list = new List<KeyValuePair<T, int>>();
            if (entries == null) return list;
            foreach (var e in entries)
            {
                var def = Find(e.id, known, missing);
                if (def) list.Add(new KeyValuePair<T, int>(def, e.level));
            }
            return list;
        }

        static T FindQuiet<T>(string id, IReadOnlyList<T> known) where T : Object
        {
            foreach (var def in known)
                if (def && def.name == id) return def;
            return null;
        }

        static T Find<T>(string id, IReadOnlyList<T> known, List<string> missing) where T : Object
        {
            foreach (var def in known)
                if (def && def.name == id) return def;
            missing.Add(id);
            return null;
        }

        static DateTime? ParseDate(string iso) =>
            DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date) ? date : null;
    }
}
