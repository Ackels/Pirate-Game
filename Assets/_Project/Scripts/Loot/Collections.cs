using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Coleções (C3, 19/10; pacote C, exploração): garrafas com mensagem nas praias (as cartas de Afonso Vaz, piloto da nau Coroa de
    /// Prata) e moedas da Frota de Prata espalhadas pelas ilhas. Guarda o que já foi pego (o save leva, <see cref="ICollectibles"/>),
    /// conta por coleção e avisa quando uma coleção fica completa (quem paga o prêmio é o <c>CollectiblePicker</c>, que conhece o
    /// navio e a fama).
    /// </summary>
    public class Collections : MonoBehaviour, ICollectibles
    {
        [Serializable]
        public class Collection
        {
            public CollectibleKind kind;
            public string name;
            [Tooltip("Prêmio ao completar: itens no porão do navio.")]
            public ItemStack[] reward = Array.Empty<ItemStack>();
            public int fame;
            [Tooltip("O prêmio em palavras, para o painel (C).")]
            public string rewardText;
            [Tooltip("Faixa ao completar.")]
            public string doneLine;
        }

        [SerializeField] Collectible[] items = Array.Empty<Collectible>();
        [SerializeField] Collection[] collections = Array.Empty<Collection>();

        readonly HashSet<string> found = new();

        public static Collections Current { get; private set; }

        public IReadOnlyList<Collectible> Items => items;
        public IReadOnlyList<Collection> All => collections;
        /// <summary>Muda quando algo é pego ou o save carrega (o painel só remonta nisso).</summary>
        public int Version { get; private set; }

        public string[] Found
        {
            get
            {
                var list = new string[found.Count];
                found.CopyTo(list);
                return list;
            }
        }

        /// <summary>Pegou um: o coletável, quantos daquela coleção já tem e o total.</summary>
        public event Action<Collectible, int, int> Collected;
        /// <summary>Coleção completa (só ao pegar o último, não ao carregar o jogo).</summary>
        public event Action<Collection> Completed;

        public void Configure(Collectible[] all, Collection[] specs)
        {
            items = all;
            collections = specs;
        }

        void Awake() => Current = this;
        void OnEnable() => Services.Register<ICollectibles>(this);
        void OnDisable() => Services.Unregister<ICollectibles>(this);

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool IsFound(Collectible c) => c && found.Contains(c.Id);

        public int Count(CollectibleKind kind)
        {
            int n = 0;
            foreach (var c in items)
                if (c && c.Kind == kind && found.Contains(c.Id)) n++;
            return n;
        }

        public int Total(CollectibleKind kind)
        {
            int n = 0;
            foreach (var c in items)
                if (c && c.Kind == kind) n++;
            return n;
        }

        public Collection Spec(CollectibleKind kind)
        {
            foreach (var c in collections)
                if (c.kind == kind) return c;
            return null;
        }

        /// <summary>Pega o coletável: some do mundo, conta e, se era o último da coleção, avisa.</summary>
        public bool Collect(Collectible c)
        {
            if (!c || !found.Add(c.Id)) return false;
            c.gameObject.SetActive(false);
            Version++;
            int have = Count(c.Kind), total = Total(c.Kind);
            Collected?.Invoke(c, have, total);
            var spec = Spec(c.Kind);
            if (have == total && spec != null) Completed?.Invoke(spec);
            return true;
        }

        public void Restore(string[] ids)
        {
            found.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) found.Add(id);
            foreach (var c in items)
                if (c) c.gameObject.SetActive(!found.Contains(c.Id));
            Version++;
        }
    }
}
