using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Fica no navio do jogador: perto de destroços e devagar, abre o saque e move itens entre
    /// os destroços e o porão (que tem limite de espaço). O jogo não pausa.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(ShipController))]
    public class WreckSalvager : MonoBehaviour, IInteraction
    {
        [Tooltip("Distância (m) do centro do navio ao centro dos destroços para poder saquear.")]
        [SerializeField] float salvageRange = 15f;
        [Tooltip("Velocidade máxima (nós) para abrir o saque: a tripulação precisa do navio quase parado.")]
        [SerializeField] float maxSpeedKnots = 3f;
        [Tooltip("Folga antes de fechar sozinho ao se afastar ou acelerar (m e nós).")]
        [SerializeField] float closeMargin = 5f;

        Inventory inventory;
        ShipController ship;
        Health health;

        public Inventory Inventory => inventory;
        public float MaxSpeedKnots => maxSpeedKnots;
        public float SpeedKnots => ship.SpeedKnots;
        /// <summary>Destroços mais próximos ao alcance (null se nenhum).</summary>
        public ShipWreck Nearby { get; private set; }
        /// <summary>Destroços com o saque aberto agora (null se fechado).</summary>
        public ShipWreck Current { get; private set; }
        public bool IsOpen => Current;
        public bool TooFast => ship.SpeedKnots > maxSpeedKnots;
        public bool CanOpen => Nearby && !TooFast && !IsDead;
        bool IsDead => health && health.IsDead;

        /// <summary>Item que passou para o porão (true) ou foi jogado de volta aos destroços (false).</summary>
        public event Action<ItemStack, bool> Transferred;

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            ship = GetComponent<ShipController>();
            TryGetComponent(out health);
            // No Awake, não no OnEnable: em terra o navio fica desligado, mas o porão continua contando para o baú raro.
            PlayerStorage.Register(inventory);
        }

        void OnDestroy() => PlayerStorage.Unregister(inventory);

        void Update()
        {
            Nearby = FindNearest(salvageRange);
            if (!Current) return;
            bool lost = !Current.isActiveAndEnabled || IsDead
                        || Distance(Current) > salvageRange + closeMargin
                        || ship.SpeedKnots > maxSpeedKnots + closeMargin;
            if (lost) Close();
        }

        void OnDisable() => Close();

        public bool Open()
        {
            if (!CanOpen) return false;
            Current = Nearby;
            Current.IsBeingSalvaged = true;
            Current.OpenChest(inventory);
            return true;
        }

        public void Close()
        {
            if (Current) Current.IsBeingSalvaged = false;
            Current = null;
        }

        /// <summary>Destroços → porão, até onde couber. Devolve quantas unidades passaram.</summary>
        public int Take(ItemDefinition item, int amount)
        {
            if (!Current) return 0;
            int available = 0;
            foreach (var s in Current.Contents)
                if (s.item == item) available = s.amount;
            int moved = inventory.Add(item, Mathf.Min(amount, available));
            if (moved <= 0) return 0;
            Current.Remove(item, moved);
            Transferred?.Invoke(new ItemStack(item, moved), true);
            return moved;
        }

        /// <summary>Porão → destroços (abrir espaço). Esquemas não se jogam fora.</summary>
        public int Discard(ItemDefinition item, int amount)
        {
            if (!Current || !item || item.IsUnique) return 0;
            int moved = inventory.Remove(item, amount);
            if (moved <= 0) return 0;
            Current.Add(item, moved);
            Transferred?.Invoke(new ItemStack(item, moved), false);
            return moved;
        }

        /// <summary>
        /// Pega tudo o que couber: primeiro o que não ocupa espaço (esquemas, tripulação),
        /// depois do item mais compacto para o mais volumoso.
        /// </summary>
        public void TakeAll()
        {
            if (!Current) return;
            var order = new List<ItemStack>(Current.Contents);
            order.Sort((a, b) => a.item.spacePerUnit.CompareTo(b.item.spacePerUnit));
            foreach (var stack in order) Take(stack.item, stack.amount);
        }

        ShipWreck FindNearest(float range)
        {
            ShipWreck best = null;
            float bestDistance = range;
            for (int i = 0; i < ShipWreck.Active.Count; i++)
            {
                var wreck = ShipWreck.Active[i];
                if (wreck.IsEmpty) continue;
                float d = Distance(wreck);
                if (d > bestDistance) continue;
                best = wreck;
                bestDistance = d;
            }
            return best;
        }

        float Distance(ShipWreck wreck)
        {
            Vector3 d = wreck.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
