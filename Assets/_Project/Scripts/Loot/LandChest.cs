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

        readonly List<ItemStack> contents = new();
        bool rolled;
        float lidAngle;
        bool wasLocked = true;

        public string DisplayName => displayName;
        public IReadOnlyList<ItemStack> Contents => contents;
        public bool IsLocked => lockSource is ILockSource source && source.IsLocked;
        public string LockedReason => lockSource is ILockSource source ? source.LockedReason : "";
        /// <summary>Já foi aberto e esvaziado.</summary>
        public bool IsEmpty => rolled && contents.Count == 0;
        public bool IsBeingLooted { get; set; }

        public void Configure(string name, LootTable lootTable, Transform chestLid, GameObject chestPadlock, GameObject chestBeacon)
        {
            displayName = name;
            table = lootTable;
            lid = chestLid;
            padlock = chestPadlock;
            beacon = chestBeacon;
        }

        public void SetLock(MonoBehaviour source) => lockSource = source is ILockSource ? source : null;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        /// <summary>Sorteia o conteúdo na primeira abertura. <paramref name="known"/>: onde ficam os esquemas que o jogador já tem.</summary>
        public void Roll(Inventory known)
        {
            if (rolled) return;
            rolled = true;
            if (!table) return;
            foreach (var stack in table.RollCargo()) Add(stack.item, stack.amount);
            if (!table.RollChest()) return;
            var prize = table.RollSchematic(known);
            if (prize.item) Add(prize.item, prize.amount);
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
            IsBeingLooted = false;
        }

        void Update()
        {
            bool locked = IsLocked;
            // Os piratas voltaram (missão aceita de novo): o tesouro também volta, fechado, até limpar o acampamento outra vez.
            if (locked && !wasLocked && rolled) ResetChest();
            wasLocked = locked;
            if (padlock && padlock.activeSelf != locked) padlock.SetActive(locked);
            bool glow = !locked && !IsEmpty;
            if (beacon && beacon.activeSelf != glow) beacon.SetActive(glow);
            if (!lid) return;
            // Tampa abre na primeira abertura e fica aberta.
            float target = rolled ? lidOpenAngle : 0f;
            lidAngle = Mathf.MoveTowards(lidAngle, target, 240f * Time.deltaTime);
            lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
        }
    }
}
