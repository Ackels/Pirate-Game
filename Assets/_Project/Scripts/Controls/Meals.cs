using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.OnFoot;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Pratos da taverna (D2, passo 2 do plano de 30/10; pedido do Jhonata: "dar mais propósitos para os peixes"): comido pela tela I, cada
    /// prato dá um bônus por alguns minutos de jogo: fôlego a mais a pé (caldeirada), recarga dos canhões mais rápida (atum grelhado),
    /// casco que se conserta devagar (ensopado de polvo), piratas que demoram a ver o capitão (sopa de siba) e golpes mais fortes a pé
    /// (garoupa assada, só os golpes de lâmina). Um de cada tipo: comer outro do mesmo tipo recomeça o tempo. Não é salvo (carregar o jogo começa sem prato),
    /// como o fôlego. Fica no objeto "Refeicoes" (sempre ligado: o navio e o capitão se desligam um ao outro).
    /// </summary>
    public class Meals : MonoBehaviour, IMealBuffs
    {
        [SerializeField] ShipController ship;
        [SerializeField] GameObject captain;
        [Tooltip("Veneno na lâmina: quanto tempo o veneno corre no alvo depois de cada golpe (s).")]
        [SerializeField] float poisonSeconds = 5f;

        struct Active
        {
            public ItemDefinition dish;
            public float amount, until;
        }

        static readonly int EffectCount = Enum.GetValues(typeof(MealEffect)).Length;

        readonly Active[] active = new Active[EffectCount];
        readonly List<CannonBattery> batteries = new();
        Health hull;
        CharacterCombat combat;
        Stamina stamina;
        float nextBatteryScan, appliedReload = 1f, appliedDamage = 1f, appliedStamina;

        public static Meals Current { get; private set; }
        public int Version { get; private set; }

        /// <summary>Comeu um prato.</summary>
        public static event Action<ItemDefinition> Eaten;
        /// <summary>O bônus de um prato acabou.</summary>
        public static event Action<ItemDefinition> Expired;

        public void Configure(ShipController playerShip, GameObject playerCaptain)
        {
            ship = playerShip;
            captain = playerCaptain;
        }

        void Awake()
        {
            Current = this;
            if (ship) hull = ship.GetComponent<Health>();
            if (captain)
            {
                combat = captain.GetComponent<CharacterCombat>();
                stamina = captain.GetComponent<Stamina>();
            }
        }

        void OnEnable()
        {
            Services.Register<IMealBuffs>(this);
            CharacterCombat.Struck += OnStruck;
        }

        void OnDisable()
        {
            Services.Unregister<IMealBuffs>(this);
            CharacterCombat.Struck -= OnStruck;
        }

        /// <summary>Lâmina envenenada (veneno de baiacu): golpe de lâmina do capitão que acerta (não na guarda) envenena o alvo.</summary>
        void OnStruck(CharacterCombat.StrikeReport report)
        {
            if (!combat || report.attacker != combat) return;
            if ((report.flags & (CharacterCombat.StrikeFlags.Ranged | CharacterCombat.StrikeFlags.Blocked)) != 0) return;
            float dps = Amount(MealEffect.Poison);
            if (dps <= 0f || !report.victim) return;
            var target = report.victim.GetComponentInParent<Health>();
            if (target) PoisonDot.Apply(target, dps, poisonSeconds, captain);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public float Amount(MealEffect effect)
        {
            var a = active[(int)effect];
            return a.dish && Time.time < a.until ? a.amount : 0f;
        }

        /// <summary>Segundos que faltam do prato daquele tipo (0 = nenhum).</summary>
        public float Remaining(MealEffect effect)
        {
            var a = active[(int)effect];
            return a.dish ? Mathf.Max(0f, a.until - Time.time) : 0f;
        }

        public ItemDefinition Dish(MealEffect effect) => active[(int)effect].dish;

        public static bool IsMeal(ItemDefinition item) => item && item.mealEffect != MealEffect.None;

        /// <summary>Come um prato de <paramref name="from"/> (mochila ou porão).</summary>
        public bool Eat(ItemDefinition dish, Inventory from)
        {
            if (!IsMeal(dish) || !from || from.Count(dish) <= 0) return false;
            // A bomba de fumaça não dura: estoura na hora, a pé.
            if (dish.mealEffect == MealEffect.Smoke)
                return captain && captain.activeInHierarchy && captain.TryGetComponent(out SmokeBomb smoke) && smoke.TryUse();
            from.Remove(dish, 1);
            active[(int)dish.mealEffect] = new Active { dish = dish, amount = dish.mealAmount, until = Time.time + dish.mealMinutes * 60f };
            Version++;
            Apply(0f);
            Eaten?.Invoke(dish);
            return true;
        }

        void Update()
        {
            for (int i = 1; i < active.Length; i++)
            {
                if (!active[i].dish || Time.time < active[i].until) continue;
                var dish = active[i].dish;
                active[i] = default;
                Version++;
                Expired?.Invoke(dish);
            }
            Apply(Time.deltaTime);
        }

        /// <summary>Põe os bônus no navio e no capitão (só mexe no que mudou).</summary>
        void Apply(float dt)
        {
            float reload = 1f - Amount(MealEffect.Reload);
            if (ship && (Time.time >= nextBatteryScan || !Mathf.Approximately(reload, appliedReload)))
            {
                // Cascos novos (escuna, galeão) têm baterias próprias: confere de vez em quando.
                if (Time.time >= nextBatteryScan)
                {
                    nextBatteryScan = Time.time + 1f;
                    ship.GetComponentsInChildren(true, batteries);
                }
                appliedReload = reload;
                for (int i = 0; i < batteries.Count; i++)
                    if (batteries[i]) batteries[i].ReloadMultiplier = reload;
            }

            float regen = Amount(MealEffect.HullRegen);
            if (regen > 0f && hull && !hull.IsDead && hull.Current < hull.Max && dt > 0f) hull.Heal(regen * dt);

            float damage = 1f + Amount(MealEffect.Damage);
            if (combat && !Mathf.Approximately(damage, appliedDamage))
            {
                appliedDamage = damage;
                combat.DamageMultiplier = damage;
            }

            float extra = Amount(MealEffect.Stamina);
            if (stamina && !Mathf.Approximately(extra, appliedStamina))
            {
                appliedStamina = extra;
                stamina.BonusMax = extra;
            }
        }

        /// <summary>Teste: acaba com todos os pratos agora.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < active.Length; i++) active[i].until = 0f;
        }

        /// <summary>Coisa que se usa (veneno, fumaça), não se come.</summary>
        public static bool IsTool(ItemDefinition item) => item && item.mealEffect is MealEffect.Poison or MealEffect.Smoke;

        /// <summary>O bônus do prato em texto ("+25 de fôlego máximo a pé, por 10 min").</summary>
        public static string Describe(ItemDefinition dish)
        {
            if (!IsMeal(dish)) return "";
            string what = dish.mealEffect switch
            {
                MealEffect.Stamina => $"+{dish.mealAmount:0} de fôlego máximo a pé",
                MealEffect.Reload => $"canhões recarregam {dish.mealAmount * 100f:0}% mais rápido",
                MealEffect.HullRegen => $"o casco se conserta sozinho ({dish.mealAmount * 60f:0} por minuto)",
                MealEffect.Stealth => $"os piratas de terra só te enxergam mais de perto (vista {dish.mealAmount * 100f:0}% menor)",
                MealEffect.Damage => $"golpes de lâmina a pé {dish.mealAmount * 100f:0}% mais fortes",
                MealEffect.Poison => $"a lâmina envenena quem ela corta: {dish.mealAmount:0} de dano por segundo por 5 s a cada golpe",
                MealEffect.Smoke => "estoura no pé do capitão uma nuvem de fumaça (6 m, 8 s): ninguém vê quem está dentro, e os piratas pegos nela ficam cegos e tossindo por 4 s (tecla B a pé)",
                _ => "",
            };
            return dish.mealEffect == MealEffect.Smoke ? what : $"{what}, por {dish.mealMinutes:0.#} min";
        }

        /// <summary>"7:32".</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
