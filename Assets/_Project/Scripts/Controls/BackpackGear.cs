using Pirates.Economy;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Mochila do capitão (05/10): o espaço é o da melhor mochila que o jogador tem guardada (porão, mochila ou armazém;
    /// <see cref="ItemDefinition.backpackCapacity"/>, compradas no mercado) ou o da mochila de lona de começo. A nova substitui a
    /// velha; o que já estava dentro nunca sai (só barra entrar mais). Confere de tempos em tempos (compra é rara) e ao desembarcar.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class BackpackGear : MonoBehaviour
    {
        [Tooltip("Espaço da mochila de lona (sem nenhuma comprada).")]
        [SerializeField, Min(0f)] float baseCapacity = 15f;
        [Tooltip("Mochilas que podem existir (itens com Backpack Capacity).")]
        [SerializeField] ItemDefinition[] packs = System.Array.Empty<ItemDefinition>();
        [SerializeField, Min(0.1f)] float checkSeconds = 0.5f;

        Inventory backpack;
        float nextCheck;

        /// <summary>Mochila em uso (null = a de lona).</summary>
        public ItemDefinition Current { get; private set; }

        public void Configure(float startCapacity, ItemDefinition[] backpacks)
        {
            baseCapacity = startCapacity;
            packs = backpacks;
        }

        void Awake() => backpack = GetComponent<Inventory>();

        void OnEnable() => Refresh();

        void Update()
        {
            if (Time.time < nextCheck) return;
            Refresh();
        }

        public void Refresh()
        {
            nextCheck = Time.time + checkSeconds;
            float best = baseCapacity;
            ItemDefinition bestPack = null;
            foreach (var pack in packs)
                if (pack && pack.backpackCapacity > best && PlayerStorage.Has(pack))
                {
                    best = pack.backpackCapacity;
                    bestPack = pack;
                }
            Current = bestPack;
            if (!Mathf.Approximately(backpack.Capacity, best)) backpack.Configure(best);
        }
    }
}
