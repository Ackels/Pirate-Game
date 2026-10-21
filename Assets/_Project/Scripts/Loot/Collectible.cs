using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Loot
{
    public enum CollectibleKind
    {
        /// <summary>Garrafa com mensagem na praia (F abre e lê).</summary>
        Bottle,
        /// <summary>Moeda da Frota de Prata (pega ao passar por cima).</summary>
        Coin,
    }

    /// <summary>
    /// Um coletável no mundo (C3, 19/10): garrafa com mensagem ou moeda da Frota de Prata. Fica até ser pego (some para sempre; a
    /// lista do que foi pego está em <see cref="Collections"/> e no save). A moeda gira e sobe e desce devagar (só perto da câmera).
    /// </summary>
    public class Collectible : MonoBehaviour
    {
        static readonly List<Collectible> active = new();
        public static IReadOnlyList<Collectible> Active => active;

        [SerializeField] string collectibleId;
        [SerializeField] CollectibleKind kind;
        [Tooltip("Ordem na coleção (a carta 3 de 8): o painel mostra nessa ordem.")]
        [SerializeField] int number = 1;
        [SerializeField] string title;
        [SerializeField, TextArea(3, 8)] string message;
        [Tooltip("Onde fica, para o painel (\"Ilha Oeste, praia oeste\").")]
        [SerializeField] string place;
        [Tooltip("Parte que gira e balança (a moeda). Vazio = parada.")]
        [SerializeField] Transform spinner;
        [SerializeField] float spinDegrees = 90f, bobHeight = 0.08f, animateRange = 60f;

        Vector3 spinnerBase;
        float phase;

        public string Id => collectibleId;
        public CollectibleKind Kind => kind;
        public int Number => number;
        public string Title => title;
        public string Message => message;
        public string Place => place;

        public void Configure(string id, CollectibleKind type, int order, string titleText, string messageText, string placeText, Transform spinPart)
        {
            collectibleId = id;
            kind = type;
            number = order;
            title = titleText;
            message = messageText;
            place = placeText;
            spinner = spinPart;
        }

        void Awake()
        {
            if (spinner) spinnerBase = spinner.localPosition;
            phase = (collectibleId != null ? collectibleId.GetHashCode() & 1023 : 0) * 0.01f;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Update()
        {
            if (!spinner) return;
            var cam = Camera.main;
            if (cam && (cam.transform.position - transform.position).sqrMagnitude > animateRange * animateRange) return;
            float t = Time.time + phase;
            spinner.localRotation = Quaternion.Euler(0f, t * spinDegrees, 0f);
            spinner.localPosition = spinnerBase + Vector3.up * (Mathf.Sin(t * 2f) * bobHeight);
        }
    }
}
