using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Saque a pé (fica no personagem): a mochila (Inventory, com limite de espaço) recebe o que couber.
    /// Baú perto e destrancado: F abre a janela para escolher itens (baú ↔ mochila), como nos destroços; o jogo não
    /// pausa. Tesouro enterrado (X no chão): F cava, e o baú que sobe abre do mesmo jeito. Saco de saque no chão: pega
    /// sozinho passando por cima. Ao embarcar, a mochila descarrega no porão.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class LandLooter : MonoBehaviour, IInteraction
    {
        [SerializeField] float chestRange = 2.6f;
        [Tooltip("Folga (m) antes de fechar o baú sozinho ao se afastar.")]
        [SerializeField] float closeMargin = 1.5f;
        [SerializeField] float sackRange = 1.4f;
        [Tooltip("Onde ficam os esquemas que o jogador já tem (porão do navio): o baú não sorteia repetido.")]
        [SerializeField] Inventory knownItems;
        [Header("Largar itens (21/10)")]
        [Tooltip("Saco que aparece no chão quando o capitão larga itens da mochila (o mesmo dos caixotes).")]
        [SerializeField] LootSack dropSack;
        [Tooltip("Segundos até o saco largado sumir (sem prazo, dava para estocar saque pelo mapa).")]
        [SerializeField, Min(10f)] float dropLifetime = 180f;
        [Tooltip("Largar de novo a menos disto do último saco largado junta no mesmo saco (o prazo não renova).")]
        [SerializeField] float dropMergeRange = 1.6f;

        Inventory backpack;
        Health health;
        LootSack lastDropped;
        static readonly RaycastHit[] groundHits = new RaycastHit[8];
        float fullWarningAt = float.NegativeInfinity;

        public Inventory Backpack => backpack;
        /// <summary>Baú mais próximo ao alcance (trancado ou não; null se nenhum).</summary>
        public LandChest Nearby { get; private set; }
        /// <summary>Baú com a janela aberta (null se fechado).</summary>
        public LandChest Current { get; private set; }
        /// <summary>X de tesouro enterrado ao alcance (F cava).</summary>
        public BuriedTreasure NearbyDig { get; private set; }
        public bool IsOpen => Current;
        public bool CanOpen => ((Nearby && !Nearby.IsLocked) || NearbyDig) && !IsDead;
        bool IsDead => health && health.IsDead;

        /// <summary>Item que entrou na mochila (true) ou voltou ao baú (false).</summary>
        public event Action<ItemStack, bool> Transferred;
        /// <summary>Passou por um saque e não coube tudo.</summary>
        public event Action BackpackFull;
        /// <summary>Largou itens da mochila num saco no chão (o que largou e os segundos até o saco sumir).</summary>
        public event Action<ItemStack, float> Dropped;

        public float DropLifetime => dropLifetime;

        public void Configure(Inventory knownSchematics) => knownItems = knownSchematics;

        public void ConfigureDrop(LootSack sack, float lifetime = 180f)
        {
            dropSack = sack;
            dropLifetime = lifetime;
        }

        /// <summary>Por que não dá para largar este item agora (null = dá). Equipamento nunca: sumiria para sempre com o saco.</summary>
        public string DropBlockReason(ItemDefinition item)
        {
            if (!dropSack) return "não dá para largar itens aqui";
            if (!item || backpack.Count(item) <= 0) return "não está na mochila";
            if (item.category == ItemCategory.Gear) return "equipamento não se larga: guarde no armazém do porto";
            if (IsDead) return "o capitão caiu";
            if (Current) return "feche o baú primeiro";
            return TryDropPoint(out _) ? null : "aqui não dá: largue em terra firme";
        }

        /// <summary>
        /// Larga até <paramref name="amount"/> de um item da mochila num saco no chão, à frente do capitão (ou no último saco largado, se
        /// estiver perto). Devolve quantos saíram.
        /// </summary>
        public int Drop(ItemDefinition item, int amount)
        {
            if (DropBlockReason(item) != null || amount <= 0 || !TryDropPoint(out var point)) return 0;
            int n = backpack.Remove(item, Mathf.Min(amount, backpack.Count(item)));
            if (n <= 0) return 0;
            var stack = new ItemStack(item, n);
            if (lastDropped && (lastDropped.transform.position - point).sqrMagnitude < dropMergeRange * dropMergeRange)
            {
                lastDropped.Add(stack);
                lastDropped.Hold(transform);
            }
            else
            {
                lastDropped = Instantiate(dropSack, point, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                lastDropped.Setup(new List<ItemStack> { stack }, dropLifetime, transform);
            }
            Dropped?.Invoke(stack, lastDropped.SecondsLeft);
            return n;
        }

        /// <summary>Chão firme um passo à frente do capitão (fora da água funda); senão aos pés dele.</summary>
        bool TryDropPoint(out Vector3 point)
        {
            var forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            if (Ground(transform.position + forward * 0.8f, out point) || Ground(transform.position, out point)) return true;
            return false;
        }

        bool Ground(Vector3 at, out Vector3 point)
        {
            point = at;
            int hits = Physics.RaycastNonAlloc(at + Vector3.up * 1.5f, Vector3.down, groundHits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                var h = groundHits[i];
                if (h.collider.transform.IsChildOf(transform) || h.normal.y < 0.6f || h.distance >= best) continue;
                best = h.distance;
                point = h.point + Vector3.up * 0.05f;
            }
            if (float.IsPositiveInfinity(best)) return false;
            // Na água funda o saco sumiria debaixo d'água.
            return !Services.TryGet(out IOceanSampler ocean) || point.y > ocean.GetHeight(point) - 0.25f;
        }

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            TryGetComponent(out health);
            PlayerStorage.Register(backpack);
            PlayerStorage.Register(knownItems);
        }

        void OnDestroy()
        {
            PlayerStorage.Unregister(backpack);
            PlayerStorage.Unregister(knownItems);
        }

        void OnDisable() => Close();

        void Update()
        {
            Nearby = FindNearestChest();
            NearbyDig = FindNearestDig();
            if (Current && (IsDead || Current.IsLocked || Distance(Current.transform) > chestRange + closeMargin)) Close();
            if (!IsDead) CollectSacks();
        }

        public bool Open()
        {
            if (!CanOpen) return false;
            // Cavar vem antes: o baú só existe depois.
            if (NearbyDig && (!Nearby || Nearby.IsLocked))
            {
                NearbyDig.Dig();
                return true;
            }
            Current = Nearby;
            Current.Open(knownItems);
            Current.IsBeingLooted = true;
            return true;
        }

        public void Close()
        {
            if (Current) Current.IsBeingLooted = false;
            Current = null;
        }

        /// <summary>Baú → mochila, até onde couber. Devolve quantas unidades passaram.</summary>
        public int Take(ItemDefinition item, int amount)
        {
            if (!Current) return 0;
            int available = 0;
            foreach (var s in Current.Contents)
                if (s.item == item) available = s.amount;
            int moved = backpack.Add(item, Mathf.Min(amount, available));
            if (moved <= 0) return 0;
            Current.Remove(item, moved);
            Transferred?.Invoke(new ItemStack(item, moved), true);
            return moved;
        }

        /// <summary>Mochila → baú (abrir espaço). Esquemas não se jogam fora.</summary>
        public int Discard(ItemDefinition item, int amount)
        {
            if (!Current || !item || item.IsUnique) return 0;
            int moved = backpack.Remove(item, amount);
            if (moved <= 0) return 0;
            Current.Add(item, moved);
            Transferred?.Invoke(new ItemStack(item, moved), false);
            return moved;
        }

        /// <summary>Pega tudo o que couber, do item mais compacto para o mais volumoso.</summary>
        public void TakeAll()
        {
            if (!Current) return;
            var order = new List<ItemStack>(Current.Contents);
            order.Sort((a, b) => a.item.spacePerUnit.CompareTo(b.item.spacePerUnit));
            foreach (var stack in order) Take(stack.item, stack.amount);
        }

        void CollectSacks()
        {
            for (int i = LootSack.Active.Count - 1; i >= 0; i--)
            {
                var sack = LootSack.Active[i];
                float d = Distance(sack.transform);
                // Saco que este capitão acabou de largar: só volta a pegar depois de se afastar.
                if (sack.HeldFrom == transform)
                {
                    if (d > sackRange + 0.6f) sack.Release();
                    continue;
                }
                if (d > sackRange) continue;
                var moved = sack.TakeInto(backpack, out bool leftovers);
                foreach (var stack in moved) Transferred?.Invoke(stack, true);
                // Mochila cheia: avisa de vez em quando (o saco continua ali).
                if (leftovers && Time.time - fullWarningAt > 3f)
                {
                    fullWarningAt = Time.time;
                    BackpackFull?.Invoke();
                }
            }
        }

        LandChest FindNearestChest()
        {
            LandChest best = null;
            float bestDistance = chestRange;
            for (int i = 0; i < LandChest.Active.Count; i++)
            {
                var chest = LandChest.Active[i];
                if (chest.IsEmpty) continue;
                float d = Distance(chest.transform);
                if (d > bestDistance) continue;
                best = chest;
                bestDistance = d;
            }
            return best;
        }

        BuriedTreasure FindNearestDig()
        {
            BuriedTreasure best = null;
            float bestDistance = chestRange;
            for (int i = 0; i < BuriedTreasure.Active.Count; i++)
            {
                var spot = BuriedTreasure.Active[i];
                if (!spot.CanDig) continue;
                float d = Distance(spot.transform);
                if (d > bestDistance) continue;
                best = spot;
                bestDistance = d;
            }
            return best;
        }

        float Distance(Transform other)
        {
            Vector3 d = other.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
