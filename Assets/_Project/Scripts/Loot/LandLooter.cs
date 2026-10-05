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

        Inventory backpack;
        Health health;
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

        public void Configure(Inventory knownSchematics) => knownItems = knownSchematics;

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
            Current.Roll(knownItems);
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
                if (Distance(sack.transform) > sackRange) continue;
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
