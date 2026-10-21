using System;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Munição das armas de fogo do capitão (05/10): cada tiro gasta um da mochila; sem munição, a arma só faz clique (o cutelo
    /// continua). Pistola = cartuchos; outras armas (bacamarte = cargas de chumbo) têm a sua em <see cref="extra"/>. Ao desembarcar,
    /// o capitão leva munição do porão na mochila; ao embarcar, a que sobrou volta (BackpackUnloader). Compra no mercado ou faz na
    /// Forja. Os piratas da IA não usam isto (atiram sem gastar).
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(CharacterCombat))]
    public class PistolAmmo : MonoBehaviour, IAmmoSupply
    {
        /// <summary>Munição própria de uma arma (a que não está aqui usa os cartuchos).</summary>
        [Serializable]
        public class WeaponAmmo
        {
            public WeaponDefinition weapon;
            public ItemDefinition item;
            [Tooltip("Quantos o capitão tira do porão ao desembarcar.")]
            [Min(0)] public int carryAshore = 12;
        }

        [SerializeField] ItemDefinition cartridge;
        [Tooltip("Quantos cartuchos o capitão tira do porão ao desembarcar (se tiver e couber na mochila).")]
        [SerializeField, Min(0)] int carryAshore = 20;
        [Tooltip("Armas com munição própria (bacamarte: cargas de chumbo).")]
        [SerializeField] WeaponAmmo[] extra = Array.Empty<WeaponAmmo>();
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Cartuchos na mochila quando o personagem liga pela primeira vez (cena sem jogo salvo, ex.: 06). No mundo (07), quem dá " +
                 "os do começo é o jogo novo (GameSave).")]
        [SerializeField, Min(0)] int startingCartridges;

        Inventory backpack;

        public ItemDefinition Cartridge => cartridge;

        /// <summary>O item é munição de alguma arma do capitão (cartuchos ou a própria de outra arma).</summary>
        public bool Uses(ItemDefinition item)
        {
            if (!item) return false;
            if (item == cartridge) return true;
            foreach (var e in extra)
                if (e != null && e.item == item) return true;
            return false;
        }
        /// <summary>Cartuchos na mochila.</summary>
        public int Cartridges => backpack && cartridge ? backpack.Count(cartridge) : 0;

        /// <summary>Tentou atirar sem munição: qual.</summary>
        public event Action<ItemDefinition> Empty;
        /// <summary>Levou munição do porão ao desembarcar: qual e quantos.</summary>
        public event Action<ItemDefinition, int> Packed;

        public void Configure(ItemDefinition cartridgeItem, PlayerModeSwitcher modeSwitcher, int startWith = 0)
        {
            cartridge = cartridgeItem;
            switcher = modeSwitcher;
            startingCartridges = startWith;
        }

        /// <summary>Munição própria de outras armas (substitui a lista).</summary>
        public void ConfigureExtra(params WeaponAmmo[] perWeapon) => extra = perWeapon ?? Array.Empty<WeaponAmmo>();

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            if (cartridge) GetComponent<CharacterCombat>().SetAmmo(this);
            if (cartridge && startingCartridges > 0) backpack.Add(cartridge, startingCartridges);
            // No Awake (não no OnEnable): o desembarque liga o personagem e só depois avisa a troca de modo.
            if (switcher) switcher.ModeChanged += OnModeChanged;
        }

        void OnDestroy()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        /// <summary>Item que a arma gasta (cartuchos, se ela não tem munição própria).</summary>
        public ItemDefinition AmmoItem(WeaponDefinition weapon)
        {
            foreach (var e in extra)
                if (e != null && e.weapon == weapon && e.item) return e.item;
            return cartridge;
        }

        public int Count(WeaponDefinition weapon)
        {
            var item = AmmoItem(weapon);
            return backpack && item ? backpack.Count(item) : 0;
        }

        public void Take(WeaponDefinition weapon)
        {
            var item = AmmoItem(weapon);
            if (item) backpack.Remove(item, 1);
        }

        public void OnEmpty(WeaponDefinition weapon) => Empty?.Invoke(AmmoItem(weapon));

        void OnModeChanged(bool onFoot)
        {
            if (!onFoot || !switcher.Ship || !switcher.Ship.TryGetComponent(out Inventory hold)) return;
            Pack(hold, cartridge, carryAshore);
            foreach (var e in extra)
                if (e != null) Pack(hold, e.item, e.carryAshore);
        }

        void Pack(Inventory hold, ItemDefinition item, int carry)
        {
            if (!item || carry <= 0) return;
            int want = Mathf.Min(carry - backpack.Count(item), hold.Count(item), backpack.MaxFit(item));
            if (want <= 0) return;
            int moved = backpack.Add(item, hold.Remove(item, want));
            if (moved > 0) Packed?.Invoke(item, moved);
        }
    }
}
