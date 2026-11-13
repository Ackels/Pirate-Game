using System;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// O armazém por dentro mostra o que está guardado (13/11): cada mercadoria tem pilhas no chão que aparecem conforme a quantidade no
    /// armazém (madeira, ferro, pólvora, ouro...). Confere a cada segundo; só liga e desliga objetos que já estão na cena.
    /// </summary>
    public class WarehouseDisplay : MonoBehaviour
    {
        [Serializable]
        public class Pile
        {
            public ItemDefinition item;
            [Tooltip("Quantidade no armazém para cada pilha aparecer (uma por objeto de 'tiers').")]
            public int[] at;
            public GameObject[] tiers;
        }

        [SerializeField] HarborDock dock;
        [SerializeField] Pile[] piles = Array.Empty<Pile>();
        [SerializeField] float checkSeconds = 1f;

        float nextCheck;

        public void Configure(HarborDock harborDock, Pile[] list)
        {
            dock = harborDock;
            piles = list;
        }

        void OnEnable() => nextCheck = 0f;

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            Refresh();
        }

        /// <summary>Liga as pilhas de cada mercadoria pela quantidade guardada agora.</summary>
        public void Refresh()
        {
            var stock = dock ? dock.Warehouse : null;
            foreach (var p in piles)
            {
                if (p == null || p.tiers == null) continue;
                int count = stock && p.item ? stock.Count(p.item) : 0;
                for (int i = 0; i < p.tiers.Length; i++)
                {
                    if (!p.tiers[i]) continue;
                    bool on = p.at != null && i < p.at.Length && count >= p.at[i];
                    if (p.tiers[i].activeSelf != on) p.tiers[i].SetActive(on);
                }
            }
        }
    }
}
