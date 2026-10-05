using System;
using Pirates.Combat;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Carpinteiro a bordo (M9): ligado pelo jogador (tecla R), conserta o casco no mar devagar, gastando madeira do porão
    /// (CrewSettings: casco por madeira e por segundo). Só trabalha fora de combate (alguns segundos sem levar tiro) e com
    /// tripulação a bordo; desliga sozinho com o casco inteiro. Depois de consertar (desligou ou terminou), descansa
    /// (CrewSettings.carpenterCooldownSeconds) antes de poder ligar de novo.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Inventory), typeof(ShipCrew))]
    public class ShipCarpenter : MonoBehaviour
    {
        public enum Status { Off, Working, InCombat, NoWood, NoCrew }

        Health health;
        Inventory hold;
        ShipCrew crew;
        // Casco já pago em madeira e ainda não consertado (uma madeira compra carpenterHullPerWood).
        float paidHull;
        float restUntil;
        bool repairedThisRun;

        public bool IsOn { get; private set; }
        public Status State { get; private set; }
        CrewSettings Settings => crew.Settings;
        ItemDefinition Wood => Settings ? Settings.carpenterWood : null;
        /// <summary>Tem carpinteiro neste navio (configurado).</summary>
        public bool Available => Settings && Wood;

        /// <summary>Ligou, desligou ou mudou o motivo de estar parado (HUD).</summary>
        public event Action Changed;
        /// <summary>Terminou: casco inteiro.</summary>
        public event Action Finished;
        /// <summary>Pediu para ligar descansando: segundos que faltam.</summary>
        public event Action<float> Resting;

        /// <summary>Segundos até poder ligar de novo (0 = pronto).</summary>
        public float RestSecondsLeft => Mathf.Max(0f, restUntil - Time.time);

        void Awake()
        {
            health = GetComponent<Health>();
            hold = GetComponent<Inventory>();
            crew = GetComponent<ShipCrew>();
        }

        /// <summary>Tecla R: liga ou desliga. Com o casco inteiro não liga.</summary>
        public void Toggle() => SetOn(!IsOn);

        public void SetOn(bool on)
        {
            if (on && !IsOn && RestSecondsLeft > 0f)
            {
                Resting?.Invoke(RestSecondsLeft);
                return;
            }
            if (on && (!Available || health.IsDead || health.Current >= health.Max)) on = false;
            if (on == IsOn) return;
            IsOn = on;
            // Desligou depois de consertar alguma coisa: descansa (ligar e desligar sem consertar não conta).
            if (!on && repairedThisRun) restUntil = Time.time + Settings.carpenterCooldownSeconds / (1f + CaptainFame.Bonus(PerkEffect.CarpenterPercent));
            repairedThisRun = false;
            SetState(on ? Status.Working : Status.Off);
            Changed?.Invoke();
        }

        void Update()
        {
            if (!IsOn) return;
            if (health.IsDead)
            {
                SetOn(false);
                return;
            }
            if (health.Current >= health.Max)
            {
                SetOn(false);
                Finished?.Invoke();
                return;
            }

            var s = Settings;
            if (crew.Count < s.carpenterMinCrew) SetState(Status.NoCrew);
            else if (Time.time - health.LastDamageTime < s.carpenterQuietSeconds) SetState(Status.InCombat);
            else
            {
                float rate = s.carpenterHullPerSecond * (1f + CaptainFame.Bonus(PerkEffect.CarpenterPercent));
                float want = Mathf.Min(rate * Time.deltaTime, health.Max - health.Current);
                // Paga em madeira inteira, adiantado: o resto fica para os próximos quadros.
                while (paidHull < want && hold.Remove(Wood, 1) > 0) paidHull += s.carpenterHullPerWood;
                float heal = Mathf.Min(want, paidHull);
                if (heal <= 0f)
                {
                    SetState(Status.NoWood);
                    return;
                }
                paidHull -= heal;
                health.Heal(heal);
                repairedThisRun = true;
                SetState(Status.Working);
            }
        }

        void SetState(Status state)
        {
            if (state == State) return;
            State = state;
            Changed?.Invoke();
        }
    }
}
