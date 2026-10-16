using System;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// F perto de uma gaiola (C2, 16/10): solta o <see cref="Prisoner"/> (com o acampamento limpo) e paga o que ele traz: tripulantes no
    /// porão do navio (até os beliches; quem não cabe vira ouro de gratidão), os itens dele no porão e a fama. Também avisa os
    /// prisioneiros perto para acenarem para o capitão.
    /// </summary>
    public class PrisonerRescuer : MonoBehaviour, IInteraction
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] ItemDefinition gold;
        [SerializeField] float range = 2.6f, noticeRange = 10f;
        [Tooltip("Ouro por tripulante que não coube nos beliches.")]
        [SerializeField] int goldPerExtraCrew = 30;

        public Prisoner Nearby { get; private set; }
        public bool IsOpen => false;
        public bool CanOpen => Nearby && Nearby.CanFree;

        /// <summary>Soltou: o prisioneiro, a fala dele e o que veio (texto curto para o aviso).</summary>
        public event Action<Prisoner, string, string> Freed;

        public void Configure(PlayerModeSwitcher modeSwitcher, ItemDefinition goldItem)
        {
            switcher = modeSwitcher;
            gold = goldItem;
        }

        void Update()
        {
            Nearby = null;
            float best = range * range, notice = noticeRange * noticeRange;
            Vector3 me = transform.position;
            var list = Prisoner.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Freed) continue;
                float d = (p.transform.position - me).sqrMagnitude;
                if (d < notice) p.Notice(me);
                if (d < best)
                {
                    best = d;
                    Nearby = p;
                }
            }
        }

        public bool Open()
        {
            var p = Nearby;
            if (!p || !p.CanFree) return false;
            string line = p.Free();
            string got = Pay(p);
            Freed?.Invoke(p, line, got);
            return true;
        }

        public void Close() { }

        string Pay(Prisoner p)
        {
            var ship = switcher ? switcher.Ship : null;
            var hold = ship ? ship.GetComponent<Inventory>() : null;
            var crew = ship ? ship.GetComponent<ShipCrew>() : null;
            var parts = new System.Collections.Generic.List<string>();
            if (hold && p.Crew > 0)
            {
                int joined = crew && crew.CrewItem ? Mathf.Min(p.Crew, crew.FreeBerths) : 0;
                if (joined > 0) hold.Add(crew.CrewItem, joined);
                if (joined > 0) parts.Add(joined == 1 ? "+1 tripulante a bordo" : $"+{joined} tripulantes a bordo");
                int extra = (p.Crew - joined) * goldPerExtraCrew;
                if (extra > 0 && gold)
                {
                    hold.Add(gold, extra);
                    parts.Add($"beliches cheios: +{extra} {gold.displayName} de gratidão");
                }
            }
            if (hold)
                foreach (var s in p.Reward)
                    if (s.item && s.amount > 0)
                    {
                        hold.Add(s.item, s.amount);
                        parts.Add($"+{s.amount} {s.item.displayName}");
                    }
            if (p.Fame > 0 && CaptainFame.Current) CaptainFame.Current.Add(p.Fame, "prisioneiro solto");
            return string.Join(" · ", parts);
        }
    }
}
