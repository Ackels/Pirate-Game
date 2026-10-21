using System.Collections.Generic;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Baú em terra (tesouro do acampamento). Fica trancado (cadeado) enquanto a fonte de tranca disser (o acampamento
    /// com piratas vivos); ao abrir pela primeira vez sorteia o conteúdo na LootTable (carga + chance de esquema que o
    /// jogador ainda não tem). O jogador escolhe os itens pelo LandLooter; o que ele devolver volta para cá. Trancado de
    /// novo depois de aberto (a missão repovoou o acampamento) = baú novo: esvazia e sorteia de novo na próxima abertura.
    /// Equipamento (E1, 19/10): o sorteio também pode trazer uma peça de equipamento, e passou a ser feito quando o baú destranca com a
    /// câmera perto (antes era ao abrir): o facho já sai na cor do melhor prêmio (esquema ou equipamento raro, épico ou lendário).
    /// </summary>
    public class LandChest : MonoBehaviour
    {
        static readonly List<LandChest> active = new();
        public static IReadOnlyList<LandChest> Active => active;

        [SerializeField] string displayName = "Baú do tesouro";
        [SerializeField] LootTable table;
        [Tooltip("Quem tranca o baú (ILockSource, ex.: o EnemyCamp). Vazio = sempre aberto.")]
        [SerializeField] MonoBehaviour lockSource;
        [Tooltip("Tampa (gira no eixo X local para abrir).")]
        [SerializeField] Transform lid;
        [SerializeField] GameObject padlock;
        [Tooltip("Facho dourado: aparece destrancado e com coisa dentro.")]
        [SerializeField] GameObject beacon;
        [SerializeField] float lidOpenAngle = -105f;
        [Tooltip("Baú de uma peça só (sem tampa que gira): o modelo fechado some e o aberto aparece na primeira abertura.")]
        [SerializeField] GameObject closedModel, openModel;
        [Tooltip("Material do facho por raridade do melhor prêmio (índice = ItemRarity; vazio = o material de sempre).")]
        [SerializeField] Material[] rarityBeacons = System.Array.Empty<Material>();
        [Tooltip("Sorteia o conteúdo destrancado com a câmera a até esta distância (m), para o facho já mostrar a raridade.")]
        [SerializeField] float revealDistance = 45f;

        readonly List<ItemStack> contents = new();
        bool rolled, opened;
        Renderer beaconRenderer;
        Material beaconDefault;
        ItemRarity shownRarity;
        float lidAngle;
        bool wasLocked = true;

        public string DisplayName => displayName;
        public IReadOnlyList<ItemStack> Contents => contents;
        public bool IsLocked => lockSource is ILockSource source && source.IsLocked;
        public string LockedReason => lockSource is ILockSource source ? source.LockedReason : "";
        /// <summary>Já foi aberto e esvaziado.</summary>
        public bool IsEmpty => rolled && contents.Count == 0;
        public bool IsBeingLooted { get; set; }
        /// <summary>Já foi aberto (a tampa fica aberta).</summary>
        public bool IsOpened => opened;

        /// <summary>
        /// Raridade do melhor prêmio que ainda está dentro (esquema ou equipamento; recurso não conta: o pedaço de mapa é raro e viria toda
        /// hora). Comum = nenhum prêmio.
        /// </summary>
        public ItemRarity BestRarity
        {
            get
            {
                var best = ItemRarity.Common;
                for (int i = 0; i < contents.Count; i++)
                {
                    var item = contents[i].item;
                    if (item && item.IsUnique && item.rarity > best) best = item.rarity;
                }
                return best;
            }
        }

        /// <summary>O melhor prêmio dentro (null = nenhum esquema ou equipamento).</summary>
        public ItemDefinition BestPrize
        {
            get
            {
                ItemDefinition best = null;
                for (int i = 0; i < contents.Count; i++)
                {
                    var item = contents[i].item;
                    if (item && item.IsUnique && (!best || item.rarity > best.rarity)) best = item;
                }
                return best;
            }
        }

        public void Configure(string name, LootTable lootTable, Transform chestLid, GameObject chestPadlock, GameObject chestBeacon)
        {
            displayName = name;
            table = lootTable;
            lid = chestLid;
            padlock = chestPadlock;
            beacon = chestBeacon;
        }

        /// <summary>Troca de modelo ao abrir (baú pirata do Quaternius: fechado → aberto cheio de ouro).</summary>
        public void SetModels(GameObject closed, GameObject open)
        {
            closedModel = closed;
            openModel = open;
        }

        public void SetLock(MonoBehaviour source) => lockSource = source is ILockSource ? source : null;

        /// <summary>Facho na cor da raridade do melhor prêmio (índice = ItemRarity).</summary>
        public void SetRarityBeacons(Material[] byRarity) => rarityBeacons = byRarity ?? System.Array.Empty<Material>();

        void Awake()
        {
            if (beacon && beacon.TryGetComponent(out beaconRenderer)) beaconDefault = beaconRenderer.sharedMaterial;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        /// <summary>Sorteia o conteúdo na primeira abertura. <paramref name="known"/>: onde ficam os esquemas que o jogador já tem.</summary>
        public void Roll(Inventory known)
        {
            if (rolled) return;
            rolled = true;
            if (!table) return;
            foreach (var stack in table.RollCargo()) Add(stack.item, stack.amount);
            var gear = table.RollGear(known);
            if (gear.item) Add(gear.item, gear.amount);
            if (!table.RollChest()) return;
            var prize = table.RollSchematic(known);
            if (prize.item) Add(prize.item, prize.amount);
        }

        /// <summary>Abre (sorteia se ainda não sorteou): a tampa abre e fica aberta.</summary>
        public void Open(Inventory known)
        {
            Roll(known);
            opened = true;
        }

        public void Add(ItemDefinition item, int amount)
        {
            if (!item || amount <= 0) return;
            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i].item != item) continue;
                contents[i] = new ItemStack(item, contents[i].amount + amount);
                return;
            }
            contents.Add(new ItemStack(item, amount));
        }

        public int Remove(ItemDefinition item, int amount)
        {
            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i].item != item) continue;
                int taken = Mathf.Min(amount, contents[i].amount);
                int left = contents[i].amount - taken;
                if (left > 0) contents[i] = new ItemStack(item, left);
                else contents.RemoveAt(i);
                return taken;
            }
            return 0;
        }

        /// <summary>Fecha e esvazia: sorteia de novo na próxima abertura (sandbox).</summary>
        public void ResetChest()
        {
            contents.Clear();
            rolled = false;
            opened = false;
            IsBeingLooted = false;
        }

        void Update()
        {
            bool locked = IsLocked;
            // Os piratas voltaram (missão aceita de novo): o tesouro também volta, fechado, até limpar o acampamento outra vez.
            if (locked && !wasLocked && rolled) ResetChest();
            wasLocked = locked;
            if (padlock && padlock.activeSelf != locked) padlock.SetActive(locked);
            // Destrancado com a câmera perto (fora do menu): sorteia já, para o facho mostrar a raridade do prêmio.
            if (!rolled && !locked && !TitleScreen.Showing && Time.timeSinceLevelLoad > 1f && Camera.main
                && (Camera.main.transform.position - transform.position).sqrMagnitude < revealDistance * revealDistance)
                Roll(null);
            bool glow = !locked && !IsEmpty;
            if (beacon && beacon.activeSelf != glow) beacon.SetActive(glow);
            if (glow) ShowRarity();
            if (openModel && openModel.activeSelf != opened)
            {
                openModel.SetActive(opened);
                if (closedModel) closedModel.SetActive(!opened);
            }
            if (!lid) return;
            // Tampa abre na primeira abertura e fica aberta.
            float target = opened ? lidOpenAngle : 0f;
            lidAngle = Mathf.MoveTowards(lidAngle, target, 240f * Time.deltaTime);
            lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
        }

        void ShowRarity()
        {
            if (!beaconRenderer) return;
            var rarity = BestRarity;
            if (rarity == shownRarity) return;
            shownRarity = rarity;
            int i = (int)rarity;
            var mat = i < rarityBeacons.Length && rarityBeacons[i] ? rarityBeacons[i] : beaconDefault;
            if (mat) beaconRenderer.sharedMaterial = mat;
        }
    }
}
