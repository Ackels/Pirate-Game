using System;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Armas do capitão que dependem do que ele tem. (1) Compradas (05/10: o bacamarte): a arma <see cref="Entry.weaponIndex"/> só saca
    /// se o jogador tem o item dela (equipamento único, guardado no armazém, porão ou mochila: <see cref="PlayerStorage"/>). (2) Lâmina
    /// equipada (E1, 19/10): a arma 1 vira a do item que está em <see cref="PlayerLoadout"/> (tela I), com o visual dela já montado no
    /// personagem pelo gerador; sem item (ou sem ele guardado) volta ao cutelo de começo. Confere de tempos em tempos e a cada troca.
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

        /// <summary>Uma lâmina que pode ser equipada: a arma e o visual dela (desligado até equipar), e os lugares próprios, se tiver.</summary>
        [Serializable]
        public class Blade
        {
            public WeaponDefinition weapon;
            public Transform visual;
            [Tooltip("Lugar na mão e guardada (vazio = os do cutelo).")]
            public Transform hand, holster;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        [Tooltip("Lâminas equipáveis (E1): a do item equipado em Lâmina substitui a arma 1.")]
        [SerializeField] Blade[] blades = Array.Empty<Blade>();
        [SerializeField, Min(0.1f)] float checkSeconds = 0.5f;

        CharacterCombat combat;
        float nextCheck;
        // A arma 1 de começo (o cutelo), guardada para voltar a ela.
        Blade starter;
        Blade applied;

        /// <summary>O item que libera a arma <paramref name="weaponIndex"/> (null = sempre liberada).</summary>
        public ItemDefinition ItemFor(int weaponIndex)
        {
            foreach (var e in entries)
                if (e != null && e.weaponIndex == weaponIndex) return e.item;
            return null;
        }

        public void Configure(params Entry[] gear) => entries = gear ?? Array.Empty<Entry>();

        public void ConfigureBlades(params Blade[] equipable) => blades = equipable ?? Array.Empty<Blade>();

        /// <summary>A lâmina na arma 1 agora (a do item equipado ou a de começo).</summary>
        public WeaponDefinition CurrentBlade => applied?.weapon;

        /// <summary>A lâmina de começo (o cutelo). Antes do Awake (capitão ainda desligado no navio), a arma 1 de quem monta.</summary>
        public WeaponDefinition StarterBlade =>
            starter != null ? starter.weapon : TryGetComponent(out CharacterCombat c) && c.Weapons.Count > 0 ? c.Weapons[0] : null;

        void Awake()
        {
            combat = GetComponent<CharacterCombat>();
            if (combat.Weapons.Count > 0) starter = new Blade { weapon = combat.Weapons[0], visual = combat.WeaponVisual(0), hand = combat.HandSlot(0), holster = combat.HolsterSlot(0) };
            applied = starter;
            foreach (var b in blades)
                if (b != null && b.visual) b.visual.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            PlayerLoadout.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => PlayerLoadout.Changed -= Refresh;

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
            ApplyBlade();
        }

        void ApplyBlade()
        {
            if (starter == null) return;
            var item = PlayerLoadout.Active(EquipSlot.Blade);
            var want = item ? Find(item.equipment as WeaponDefinition) ?? starter : starter;
            if (want == applied) return;
            var hand = want.hand ? want.hand : starter.hand;
            var holster = want.holster ? want.holster : starter.holster;
            // Ocupado (trocando de arma, golpeando): tenta de novo na próxima conferida.
            if (combat.SetWeapon(0, want.weapon, want.visual, hand, holster)) applied = want;
        }

        Blade Find(WeaponDefinition weapon)
        {
            if (!weapon) return null;
            foreach (var b in blades)
                if (b != null && b.weapon == weapon) return b;
            return null;
        }
    }
}
