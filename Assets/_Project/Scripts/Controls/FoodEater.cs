using System;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Comida do capitão (05/10): a pé, R come uma ração da mochila e cura na hora (não come com a vida cheia; um intervalo curto
    /// entre uma e outra). Ao desembarcar, o capitão leva algumas rações do porão na mochila; ao embarcar, as que sobraram voltam
    /// para o porão junto com o resto da mochila (BackpackUnloader). Fica no personagem, que só liga em terra.
    /// Pesca (D2, 27/10): sem ração na mochila, R come o peixe comestível mais barato que ela tiver (<see cref="ItemDefinition.foodHeal"/>).
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Health))]
    public class FoodEater : MonoBehaviour
    {
        [SerializeField] ItemDefinition food;
        [Tooltip("Vida que cada ração cura.")]
        [SerializeField, Min(0f)] float healAmount = 40f;
        [Tooltip("Segundos entre uma ração e a próxima (8 s, revisão de equilíbrio 05/10: com 1,5 s dava para comer 5 seguidas no meio " +
                 "da luta e ganhar o acampamento parado, apanhando; com 8 s, comer serve para se recuperar entre uma onda e outra).")]
        [SerializeField, Min(0f)] float cooldown = 8f;
        [Tooltip("Quantas rações o capitão tira do porão ao desembarcar (se tiver e couber na mochila).")]
        [SerializeField, Min(0)] int carryAshore = 5;
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Garrafa de rum pendurada no gancho (aparece só enquanto come).")]
        [SerializeField] GameObject bottle;
        [Tooltip("Segundos da animação de comer: não ataca nem atira e anda mais devagar.")]
        [SerializeField, Min(0f)] float eatSeconds = 1.9f;
        [SerializeField, Range(0.1f, 1f)] float moveWhileEating = 0.55f;

        Inventory backpack;
        Health health;
        CharacterAnimatorDriver animatorDriver;
        CharacterCombat combat;
        FootCharacter footCharacter;
        float nextEat, bottleUntil;
        // Arma da mão escondida durante o gole (a garrafa vai na mesma mão).
        readonly System.Collections.Generic.List<Renderer> hiddenWeapon = new();

        /// <summary>Está comendo (animação tocando).</summary>
        public bool IsEating => Time.time < bottleUntil;

        public ItemDefinition Food => food;
        public float HealAmount => healAmount;
        /// <summary>Segundos até poder comer de novo (0 = pode).</summary>
        public float WaitSeconds => Mathf.Max(0f, nextEat - Time.time);
        /// <summary>Rações na mochila.</summary>
        public int Count => backpack && food ? backpack.Count(food) : 0;
        /// <summary>O que comeu por último (ração ou peixe), para o aviso.</summary>
        public ItemDefinition LastEaten { get; private set; }

        /// <summary>Comeu: quanto curou.</summary>
        public event Action<float> Ate;
        /// <summary>Apertou R e não comeu: o motivo (para o HUD).</summary>
        public event Action<string> Refused;
        /// <summary>Levou rações do porão ao desembarcar: quantas.</summary>
        public event Action<int> Packed;

        public void Configure(ItemDefinition foodItem, PlayerModeSwitcher modeSwitcher, GameObject bottleProp = null)
        {
            food = foodItem;
            switcher = modeSwitcher;
            bottle = bottleProp;
        }

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            health = GetComponent<Health>();
            animatorDriver = GetComponentInChildren<CharacterAnimatorDriver>(true);
            combat = GetComponent<CharacterCombat>();
            footCharacter = GetComponent<FootCharacter>();
            if (bottle) bottle.SetActive(false);
            // No Awake (não no OnEnable): o desembarque liga o personagem e só depois avisa a troca de modo.
            if (switcher) switcher.ModeChanged += OnModeChanged;
        }

        void OnDestroy()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        void Update()
        {
            if (GameInput.Instance.Repair.WasPressedThisFrame()) TryEat();
            if (bottle && bottle.activeSelf && (!IsEating || health.IsDead)) EndEating();
        }

        void OnDisable()
        {
            EndEating();
            bottleUntil = 0f;
        }

        void EndEating()
        {
            if (bottle) bottle.SetActive(false);
            foreach (var r in hiddenWeapon) if (r) r.enabled = true;
            hiddenWeapon.Clear();
        }

        void HideWeaponInHand()
        {
            if (!combat || combat.IsSwapping) return;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual) return;
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                {
                    r.enabled = false;
                    hiddenWeapon.Add(r);
                }
        }

        /// <summary>Come uma ração (tecla R). false se não deu (sem ração, vida cheia, caído ou cedo demais).</summary>
        public bool TryEat()
        {
            if (!food || health.IsDead) return false;
            if (Time.time < nextEat)
            {
                Refused?.Invoke($"ainda mastigando (mais {Mathf.CeilToInt(nextEat - Time.time)} s)");
                return false;
            }
            var meal = Count > 0 ? food : CheapestFish();
            if (!meal)
            {
                Refused?.Invoke("sem ração na mochila (compre no mercado, ache em caixotes e barris ou pesque)");
                return false;
            }
            if (health.Current >= health.Max)
            {
                Refused?.Invoke("vida cheia");
                return false;
            }
            backpack.Remove(meal, 1);
            LastEaten = meal;
            float before = health.Current;
            health.Heal(meal == food ? healAmount : meal.foodHeal);
            nextEat = Time.time + cooldown;
            // Gole na garrafa: animação no corpo de cima, sem atacar e andando devagar até terminar.
            bottleUntil = Time.time + eatSeconds;
            if (bottle)
            {
                bottle.SetActive(true);
                HideWeaponInHand();
            }
            if (animatorDriver) animatorDriver.PlayEat();
            if (combat) combat.Occupy(eatSeconds);
            if (footCharacter) footCharacter.SetSpeedMultiplier(moveWhileEating, eatSeconds);
            Ate?.Invoke(health.Current - before);
            return true;
        }

        /// <summary>O peixe comestível da mochila que vale menos (raridade mais baixa, depois o que cura menos).</summary>
        ItemDefinition CheapestFish()
        {
            ItemDefinition best = null;
            var items = backpack.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!item || item.foodHeal <= 0f || backpack.Count(item) <= 0) continue;
                if (!best || item.rarity < best.rarity || (item.rarity == best.rarity && item.foodHeal < best.foodHeal)) best = item;
            }
            return best;
        }

        void OnModeChanged(bool onFoot)
        {
            if (!onFoot || !food || carryAshore <= 0 || !switcher.Ship || !switcher.Ship.TryGetComponent(out Inventory hold)) return;
            int want = Mathf.Min(carryAshore - Count, hold.Count(food), backpack.MaxFit(food));
            if (want <= 0) return;
            int moved = backpack.Add(food, hold.Remove(food, want));
            if (moved > 0) Packed?.Invoke(moved);
        }
    }
}
