using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Coletáveis a pé (C3, 19/10): a moeda da Frota de Prata é pega ao passar por cima; a garrafa com mensagem, com F (abre a carta
    /// para ler; F ou Esc fecha). Coleção completa = o prêmio vai para o porão do navio e a fama para o capitão.
    /// </summary>
    public class CollectiblePicker : MonoBehaviour, IInteraction
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] Collections collections;
        [Tooltip("Distância para pegar a moeda passando (no chão, sem contar a altura).")]
        [SerializeField] float coinRange = 1.3f;
        [Tooltip("Distância do F na garrafa.")]
        [SerializeField] float bottleRange = 2.3f;
        [Tooltip("Afastou mais que isso lendo a carta: fecha.")]
        [SerializeField] float readRange = 5f;

        public Collectible Nearby { get; private set; }
        /// <summary>A garrafa aberta (lendo a carta).</summary>
        public Collectible Reading { get; private set; }
        public bool IsOpen => Reading;
        public bool CanOpen => Nearby && collections;

        /// <summary>Pegou um coletável: ele, quantos da coleção já tem e o total.</summary>
        public event Action<Collectible, int, int> Picked;
        /// <summary>Coleção completa: ela e o que veio de prêmio (texto curto).</summary>
        public event Action<Collections.Collection, string> CollectionDone;

        Vector3 readFrom;

        public void Configure(PlayerModeSwitcher modeSwitcher, Collections all)
        {
            switcher = modeSwitcher;
            collections = all;
        }

        void OnEnable()
        {
            if (collections) collections.Completed += OnCompleted;
        }

        void OnDisable()
        {
            if (collections) collections.Completed -= OnCompleted;
            Close();
            Nearby = null;
        }

        void Update()
        {
            if (!collections) return;
            if (Reading && FlatDistance(readFrom) > readRange) Close();

            Nearby = null;
            float best = bottleRange * bottleRange, coin = coinRange * coinRange;
            Vector3 me = transform.position;
            var list = Collectible.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                Vector3 d = c.transform.position - me;
                if (Mathf.Abs(d.y) > 2.5f) continue;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (c.Kind == CollectibleKind.Coin)
                {
                    if (sq < coin) Take(c);
                }
                else if (sq < best)
                {
                    best = sq;
                    Nearby = c;
                }
            }
        }

        float FlatDistance(Vector3 p)
        {
            Vector3 d = transform.position - p;
            d.y = 0f;
            return d.magnitude;
        }

        void Take(Collectible c)
        {
            if (!collections.Collect(c)) return;
            Picked?.Invoke(c, collections.Count(c.Kind), collections.Total(c.Kind));
        }

        /// <summary>F na garrafa: pega e abre a carta.</summary>
        public bool Open()
        {
            var c = Nearby;
            if (!c || !collections) return false;
            readFrom = transform.position;
            Take(c);
            Reading = c;
            Nearby = null;
            ScreenPanel.Open = true;
            return true;
        }

        public void Close()
        {
            if (!Reading) return;
            Reading = null;
            ScreenPanel.Open = false;
        }

        void OnCompleted(Collections.Collection spec)
        {
            var ship = switcher ? switcher.Ship : null;
            var hold = ship ? ship.GetComponent<Inventory>() : null;
            var parts = new List<string>();
            if (hold)
                foreach (var s in spec.reward)
                    if (s.item && s.amount > 0)
                    {
                        int added = hold.Add(s.item, s.amount);
                        if (added > 0) parts.Add($"+{added} {s.item.displayName}");
                    }
            if (spec.fame > 0 && CaptainFame.Current)
            {
                CaptainFame.Current.Add(spec.fame, "coleção completa");
                parts.Add($"+{spec.fame} de fama");
            }
            CollectionDone?.Invoke(spec, string.Join(" · ", parts));
        }
    }
}
