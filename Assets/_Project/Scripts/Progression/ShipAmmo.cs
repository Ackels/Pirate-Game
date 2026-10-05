using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Munição escolhida no navio do jogador (M9): 0 = bala comum (infinita); as especiais (corrente, metralha) gastam uma
    /// carga do porão por bordada de cada bateria. Sem carga, volta para a comum. Põe a munição nas baterias ligadas (e nas
    /// do casco novo, quando o navio troca de classe).
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(ShipController))]
    public class ShipAmmo : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public AmmoDefinition ammo;
            [Tooltip("Item gasto do porão, um por bordada.")]
            public ItemDefinition item;
        }

        [Tooltip("Munições especiais na ordem das teclas (a 1 é a bala comum; depois 2, 3...).")]
        [SerializeField] Slot[] slots = Array.Empty<Slot>();

        Inventory hold;
        ShipController ship;
        CannonBattery[] batteries = Array.Empty<CannonBattery>();
        int seenVersion = -1;

        public IReadOnlyList<Slot> Slots => slots;
        /// <summary>0 = bala comum; 1.. = slots[Selected - 1].</summary>
        public int Selected { get; private set; }
        public AmmoDefinition Current => Selected > 0 ? slots[Selected - 1].ammo : null;

        /// <summary>Escolha ou quantidade de cargas mudou (HUD).</summary>
        public event Action Changed;
        /// <summary>Pediu uma munição sem carga no porão (ou acabou a que estava em uso).</summary>
        public event Action<Slot> OutOfAmmo;

        public void Configure(Slot[] ammoSlots) => slots = ammoSlots;

        /// <summary>Cargas no porão (−1 = bala comum, infinita).</summary>
        public int Count(int index) => index <= 0 || index > slots.Length ? -1 : hold.Count(slots[index - 1].item);

        void Awake()
        {
            hold = GetComponent<Inventory>();
            ship = GetComponent<ShipController>();
        }

        void OnEnable() => ship.HullChanged += Bind;

        void OnDisable() => ship.HullChanged -= Bind;

        void Start() => Bind();

        void OnDestroy() => Unbind();

        void Update()
        {
            if (hold.Version == seenVersion) return;
            seenVersion = hold.Version;
            // Descarregou no armazém (ou gastou) a munição em uso: volta para a comum.
            if (Selected > 0 && Count(Selected) <= 0)
            {
                var slot = slots[Selected - 1];
                Select(0);
                OutOfAmmo?.Invoke(slot);
            }
            Changed?.Invoke();
        }

        /// <summary>Escolhe a munição (false se não há carga daquela no porão).</summary>
        public bool Select(int index)
        {
            if (index < 0 || index > slots.Length) return false;
            if (index > 0 && Count(index) <= 0)
            {
                OutOfAmmo?.Invoke(slots[index - 1]);
                return false;
            }
            Selected = index;
            Apply();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Próxima munição com carga (gamepad).</summary>
        public void Cycle()
        {
            for (int step = 1; step <= slots.Length + 1; step++)
            {
                int index = (Selected + step) % (slots.Length + 1);
                if (index == 0 || Count(index) > 0)
                {
                    Select(index);
                    return;
                }
            }
        }

        void Bind()
        {
            Unbind();
            batteries = GetComponentsInChildren<CannonBattery>();
            foreach (var battery in batteries) battery.Fired += OnFired;
            Apply();
        }

        void Unbind()
        {
            foreach (var battery in batteries)
                if (battery) battery.Fired -= OnFired;
        }

        void Apply()
        {
            var ammo = Current;
            foreach (var battery in batteries)
                if (battery) battery.Ammo = ammo;
        }

        // A bateria já levou a munição para a salva antes de avisar: tirar a carga e trocar agora não muda o tiro.
        void OnFired()
        {
            if (Selected <= 0) return;
            var slot = slots[Selected - 1];
            hold.Remove(slot.item, 1);
            // Acabou: comum já nesta bordada (senão a outra bateria, no mesmo quadro, ainda sairia com a especial de graça).
            if (Count(Selected) > 0) return;
            Select(0);
            OutOfAmmo?.Invoke(slot);
        }
    }
}
