using System;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Bomba de fumaça a pé (D2, passo 4a do plano dos peixes, 30/10; feita com a tinta da siba na Forja): B estoura a bomba no pé do capitão
    /// e sobe uma nuvem escura de <see cref="radius"/> m por <see cref="seconds"/> s. Quem está dentro não é visto (dá para fugir ou chegar
    /// pelas costas); os piratas pegos nela ficam cegos e tossindo por <see cref="blindSeconds"/> s (não atacam, andam devagar e esquecem
    /// onde o capitão estava). Leva até <see cref="carryAshore"/> do porão ao desembarcar, como as bombas.
    /// </summary>
    public class SmokeBomb : MonoBehaviour
    {
        [SerializeField] ItemDefinition smokeBomb;
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField, Min(0)] int carryAshore = 3;
        [SerializeField] float radius = 6f, seconds = 8f, blindSeconds = 4f;
        [SerializeField, Min(0f)] float cooldown = 1.5f;

        Inventory backpack;
        FootCharacter character;
        Health health;
        float nextUse;

        public ItemDefinition Item => smokeBomb;
        public int Count => backpack && smokeBomb ? backpack.Count(smokeBomb) : 0;

        /// <summary>Estourou a fumaça (centro).</summary>
        public event Action<Vector3> Used;
        public event Action<string> Refused;
        public event Action<int> Packed;

        public void Configure(ItemDefinition item, PlayerModeSwitcher modeSwitcher)
        {
            smokeBomb = item;
            switcher = modeSwitcher;
        }

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            character = GetComponent<FootCharacter>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (switcher) switcher.ModeChanged += OnModeChanged;
        }

        void OnDisable()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        void Update()
        {
            var input = GameInput.Instance;
            if (!input.GameplayEnabled || ScreenPanel.Open || !input.Smoke.WasPressedThisFrame()) return;
            TryUse();
        }

        /// <summary>Estoura uma bomba de fumaça da mochila no pé do capitão.</summary>
        public bool TryUse()
        {
            string why = !smokeBomb ? "sem bomba de fumaça"
                : Count <= 0 ? "sem bombas de fumaça na mochila (faça na Forja: tinta de siba e pólvora)"
                : health && health.IsDead ? "caído"
                : Time.time < nextUse ? "espere a fumaça baixar"
                : character && character.InDeepWater ? "na água funda a pólvora molha" : null;
            if (why != null)
            {
                Refused?.Invoke(why);
                return false;
            }
            nextUse = Time.time + cooldown;
            backpack.Remove(smokeBomb, 1);
            var center = transform.position;
            EnemyFootAI.AddSmoke(center, radius, seconds, blindSeconds);
            if (Services.TryGet(out CombatEffects fx)) fx.Smoke(center, radius, seconds);
            Used?.Invoke(center);
            return true;
        }

        void OnModeChanged(bool onFoot)
        {
            if (!onFoot || !smokeBomb || carryAshore <= 0 || !switcher.Ship || !switcher.Ship.TryGetComponent(out Inventory hold)) return;
            int want = Mathf.Min(carryAshore - Count, Mathf.Min(hold.Count(smokeBomb), backpack.MaxFit(smokeBomb)));
            if (want <= 0) return;
            int moved = backpack.Add(smokeBomb, hold.Remove(smokeBomb, want));
            if (moved > 0) Packed?.Invoke(moved);
        }
    }
}
