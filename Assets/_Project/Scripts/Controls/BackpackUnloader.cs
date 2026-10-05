using System;
using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Embarcou: o que está na mochila do personagem passa para o porão do navio, no que couber (o resto fica na
    /// mochila para a próxima vez).
    /// </summary>
    public class BackpackUnloader : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;

        Inventory backpack, hold;

        /// <summary>O que passou para o porão, e se sobrou algo na mochila (porão cheio).</summary>
        public event Action<List<ItemStack>, bool> Unloaded;

        public void Configure(PlayerModeSwitcher modeSwitcher) => switcher = modeSwitcher;

        void Awake()
        {
            if (!switcher) return;
            if (switcher.Character) switcher.Character.TryGetComponent(out backpack);
            if (switcher.Ship) switcher.Ship.TryGetComponent(out hold);
        }

        void OnEnable()
        {
            if (switcher) switcher.ModeChanged += OnModeChanged;
        }

        void OnDisable()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        void OnModeChanged(bool onFoot)
        {
            if (onFoot || !backpack || !hold || backpack.Items.Count == 0) return;
            var moved = new List<ItemStack>();
            foreach (var item in new List<ItemDefinition>(backpack.Items))
            {
                int n = hold.Add(item, backpack.Count(item));
                if (n <= 0) continue;
                backpack.Remove(item, n);
                moved.Add(new ItemStack(item, n));
            }
            Unloaded?.Invoke(moved, backpack.Items.Count > 0);
        }
    }
}
