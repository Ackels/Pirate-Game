using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>O que cada casa de serviço do porto faz (a parte do menu do porto que ela abre).</summary>
    public enum HarborService
    {
        /// <summary>Armazém: porão ↔ armazém.</summary>
        Cargo,
        /// <summary>Estaleiro: upgrades do navio e conserto do casco.</summary>
        Shipyard,
        /// <summary>Mestre de obras: construções da base.</summary>
        Construction,
        /// <summary>Taverna: contratar marinheiros (tripulação), quadro de missões e pagamento.</summary>
        Missions,
        /// <summary>Mercado da praça: comprar e vender recursos e munição por ouro (M9).</summary>
        Market,
        /// <summary>Ferreiro da Forja (só depois de construída): faz munições especiais (M9).</summary>
        Smithy,
    }

    /// <summary>
    /// Porta de uma casa de serviço do porto (fica na frente da porta). A pé, perto dela e com o navio atracado, F abre a
    /// parte do porto dela (HarborBuildingVisitor no personagem).
    /// </summary>
    public class HarborBuilding : MonoBehaviour
    {
        static readonly List<HarborBuilding> active = new();
        public static IReadOnlyList<HarborBuilding> Active => active;

        [SerializeField] string displayName = "Armazém";
        [Tooltip("O que se faz aqui, em poucas palavras (aparece na placa e no aviso da porta).")]
        [SerializeField] string purpose = "carga";
        [SerializeField] HarborService service;
        [Tooltip("Distância (m) da porta para abrir.")]
        [SerializeField] float range = 3f;
        [Tooltip("Altura (m) do nome sobre a porta.")]
        [SerializeField] float labelHeight = 6f;

        public string DisplayName => displayName;
        public string Purpose => purpose;
        public HarborService Service => service;
        public float Range => range;
        public Vector3 LabelPoint => transform.position + Vector3.up * labelHeight;

        public void Configure(string buildingName, string buildingPurpose, HarborService buildingService, float doorRange, float nameHeight)
        {
            displayName = buildingName;
            purpose = buildingPurpose;
            service = buildingService;
            range = doorRange;
            labelHeight = nameHeight;
        }

        public float FlatDistance(Vector3 position)
        {
            Vector3 d = position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
