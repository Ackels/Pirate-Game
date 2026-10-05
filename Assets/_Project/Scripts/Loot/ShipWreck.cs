using System;
using System.Collections.Generic;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Campo de destroços boiando onde um navio afundou, com a carga dele (precisa de Rigidbody + Buoyancy no prefab).
    /// O jogador saqueia pelo WreckSalvager; itens jogados fora do porão voltam para cá.
    /// Cada peça visual aparece só enquanto o item correspondente ainda está nos destroços.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipWreck : MonoBehaviour
    {
        [Serializable]
        public class ContentVisual
        {
            public ItemDefinition item;
            public GameObject visual;
        }

        static readonly List<ShipWreck> active = new();

        /// <summary>Destroços na água agora (o saque e o HUD percorrem esta lista).</summary>
        public static IReadOnlyList<ShipWreck> Active => active;

        [SerializeField] ContentVisual[] contentVisuals = Array.Empty<ContentVisual>();
        [Tooltip("Baú e facho dourado: aparecem enquanto o baú não foi aberto ou o esquema dele não foi levado.")]
        [SerializeField] GameObject[] chestVisuals = Array.Empty<GameObject>();
        [Tooltip("Filhos que ficam sempre na vertical (fachos de luz), mesmo com os destroços balançando.")]
        [SerializeField] Transform[] uprightChildren = Array.Empty<Transform>();
        [SerializeField] float blinkSeconds = 15f;
        [Tooltip("Segundos até destroços vazios afundarem depois de fechar o saque.")]
        [SerializeField] float emptySinkDelay = 3f;

        readonly List<ItemStack> contents = new();
        LootTable chestSource;
        bool chestOpened;
        float expiresAt;
        float emptySince = -1f;
        Renderer[] renderers;
        Vector3[] uprightOffsets;

        public IReadOnlyList<ItemStack> Contents => contents;
        /// <summary>Tem um baú ainda fechado (o conteúdo é sorteado ao abrir o saque).</summary>
        public bool HasClosedChest => chestSource && !chestOpened;
        public bool HasChestLoot => HasClosedChest || contents.Exists(s => s.item.category == ItemCategory.Schematic);
        public bool IsEmpty => contents.Count == 0 && !HasClosedChest;
        public bool IsBeingSalvaged { get; set; }
        public float SecondsLeft => Mathf.Max(0f, expiresAt - Time.time);
        public string Label => IsEmpty ? "Destroços vazios" : HasChestLoot ? "Destroços + baú raro!" : CountLabel();

        // O HUD lê o rótulo a cada quadro: refaz o texto só quando muda a quantidade.
        string countLabel;
        int countLabelFor = -1;

        string CountLabel()
        {
            if (countLabelFor != contents.Count)
            {
                countLabelFor = contents.Count;
                countLabel = $"Destroços ({contents.Count} itens)";
            }
            return countLabel;
        }

        /// <summary>Usado pelo gerador de protótipo para montar o prefab.</summary>
        public void Configure(ContentVisual[] visuals, GameObject[] chest, Transform[] upright)
        {
            contentVisuals = visuals;
            chestVisuals = chest;
            uprightChildren = upright;
        }

        public void Setup(List<ItemStack> cargo, LootTable chestTable, float lifetime)
        {
            contents.Clear();
            foreach (var stack in cargo) Add(stack.item, stack.amount);
            chestSource = chestTable;
            expiresAt = Time.time + lifetime;
            RefreshVisuals();
        }

        /// <summary>Abre o baú (se houver): o esquema entra na lista dos destroços.</summary>
        public void OpenChest(Inventory forPlayer)
        {
            if (!HasClosedChest) return;
            chestOpened = true;
            var prize = chestSource.RollSchematic(forPlayer);
            if (prize.item) Add(prize.item, prize.amount);
        }

        public void Add(ItemDefinition item, int amount)
        {
            if (!item || amount <= 0) return;
            int i = contents.FindIndex(s => s.item == item);
            if (i >= 0) contents[i] = new ItemStack(item, contents[i].amount + amount);
            else contents.Add(new ItemStack(item, amount));
            emptySince = -1f;
            RefreshVisuals();
        }

        public int Remove(ItemDefinition item, int amount)
        {
            int i = contents.FindIndex(s => s.item == item);
            if (i < 0 || amount <= 0) return 0;
            amount = Mathf.Min(amount, contents[i].amount);
            int left = contents[i].amount - amount;
            if (left > 0) contents[i] = new ItemStack(item, left);
            else contents.RemoveAt(i);
            RefreshVisuals();
            return amount;
        }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            expiresAt = Time.time + 120f;
            uprightOffsets = new Vector3[uprightChildren.Length];
            for (int i = 0; i < uprightChildren.Length; i++)
                if (uprightChildren[i]) uprightOffsets[i] = uprightChildren[i].localPosition;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void Update()
        {
            // Com o saque aberto o tempo para: ninguém perde a carga no meio da escolha.
            if (IsBeingSalvaged)
            {
                expiresAt = Mathf.Max(expiresAt, Time.time + blinkSeconds + 5f);
                SetVisible(true);
                return;
            }

            if (IsEmpty)
            {
                if (emptySince < 0f) emptySince = Time.time;
                if (Time.time - emptySince > emptySinkDelay) Destroy(gameObject);
                return;
            }

            float left = SecondsLeft;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            // Pisca cada vez mais rápido perto de afundar.
            if (left < blinkSeconds)
            {
                float rate = Mathf.Lerp(8f, 2.5f, left / blinkSeconds);
                SetVisible(Mathf.Repeat(Time.time * rate, 1f) > 0.3f);
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < uprightChildren.Length; i++)
                if (uprightChildren[i])
                    uprightChildren[i].SetPositionAndRotation(transform.position + uprightOffsets[i], Quaternion.identity);
        }

        void RefreshVisuals()
        {
            foreach (var cv in contentVisuals)
                if (cv.visual) cv.visual.SetActive(contents.Exists(s => s.item == cv.item));
            bool chest = HasChestLoot;
            foreach (var go in chestVisuals)
                if (go) go.SetActive(chest);
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers)
                if (r) r.enabled = visible;
        }
    }
}
