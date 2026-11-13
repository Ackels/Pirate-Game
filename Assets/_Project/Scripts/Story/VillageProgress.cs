using System;
using Pirates.Base;
using Pirates.Missions;
using Pirates.Progression;
using UnityEngine;

namespace Pirates.Story
{
    /// <summary>
    /// O porto mostra a carreira do capitão (12/11, pedido do Jhonata: "todas as evoluções mudem visualmente o porto"): bandeiras pela vila a
    /// cada patente, o casco do navio novo na carreira do estaleiro enquanto a obra anda, e a vila em festa depois do fim da história. As
    /// construções e o Porto Alento já mudam o porto pelo BaseBuildings; isto cuida do resto. Confere uma vez por segundo.
    /// </summary>
    public class VillageProgress : MonoBehaviour
    {
        [Serializable]
        public class ShipOnSlipway
        {
            public ShipClassDefinition shipClass;
            public GameObject hull;
        }

        [Tooltip("Ligados a partir da patente (índice 0 = patente 1, Marujo de Respeito).")]
        [SerializeField] GameObject[] rankFlags = Array.Empty<GameObject>();
        [Tooltip("O casco de cada classe na carreira, ligado enquanto o estaleiro constrói aquele navio.")]
        [SerializeField] ShipOnSlipway[] slipway = Array.Empty<ShipOnSlipway>();
        [Tooltip("O bote em obra de sempre (some quando há um navio de verdade na carreira).")]
        [SerializeField] GameObject idleBoat;
        [Tooltip("A vila em festa depois do fim de A Coroa de Prata.")]
        [SerializeField] GameObject festival;

        HarborDock dock;
        float next;
        int shownRank = -1, shownShip = -2, shownFest = -1;

        public void Configure(GameObject[] flags, ShipOnSlipway[] ships, GameObject boat, GameObject party)
        {
            rankFlags = flags;
            slipway = ships;
            idleBoat = boat;
            festival = party;
        }

        void Start() => dock = FindAnyObjectByType<HarborDock>();

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            Refresh();
        }

        /// <summary>Confere tudo agora (testes).</summary>
        public void Refresh()
        {
            int rank = CaptainFame.Current ? CaptainFame.Current.Rank : 0;
            if (rank != shownRank)
            {
                shownRank = rank;
                for (int i = 0; i < rankFlags.Length; i++)
                    if (rankFlags[i]) rankFlags[i].SetActive(i < rank);
            }

            var building = dock && dock.Job != null ? dock.Job.shipClass : null;
            int ship = -1;
            for (int i = 0; i < slipway.Length; i++)
                if (slipway[i] != null && slipway[i].shipClass && slipway[i].shipClass == building) ship = i;
            if (ship != shownShip)
            {
                shownShip = ship;
                for (int i = 0; i < slipway.Length; i++)
                    if (slipway[i] != null && slipway[i].hull) slipway[i].hull.SetActive(i == ship);
                if (idleBoat) idleBoat.SetActive(ship < 0);
            }

            int fest = MissionGiver.StoryEnded ? 1 : 0;
            if (fest != shownFest)
            {
                shownFest = fest;
                if (festival) festival.SetActive(fest == 1);
            }
        }
    }
}
