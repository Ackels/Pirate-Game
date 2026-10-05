using System;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Armas compradas (05/10: o bacamarte): a arma <see cref="Entry.weaponIndex"/> do personagem só saca se o jogador tem o item
    /// dela (equipamento único, guardado no armazém, porão ou mochila: <see cref="PlayerStorage"/>). Confere de tempos em tempos
    /// (compra é rara), como a <see cref="BackpackGear"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterCombat))]
    public class GearWeapons : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Índice da arma no CharacterCombat (0 = cutelo, 1 = pistola, 2 = bacamarte).")]
            public int weaponIndex;
            [Tooltip("Item que libera a arma.")]
            public ItemDefinition item;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        [SerializeField, Min(0.1f)] float checkSeconds = 0.5f;

        CharacterCombat combat;
        float nextCheck;

        public void Configure(params Entry[] gear) => entries = gear ?? Array.Empty<Entry>();

        void Awake()
        {
            combat = GetComponent<CharacterCombat>();
            Refresh();
        }

        void OnEnable() => Refresh();

        void Update()
        {
            if (Time.time >= nextCheck) Refresh();
        }

        public void Refresh()
        {
            nextCheck = Time.time + checkSeconds;
            if (!combat) return;
            foreach (var e in entries)
                if (e != null && e.item) combat.SetAvailable(e.weaponIndex, PlayerStorage.Has(e.item));
        }
    }
}
