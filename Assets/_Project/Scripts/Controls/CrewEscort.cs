using System;
using Pirates.LandCombat;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.Controls
{
    /// <summary>
    /// Marujos que descem com o capitão (D3, 30/10): ao desembarcar numa ilha (no porto, não: lá é o píer da vila), até
    /// <see cref="CrewmateAI"/> marujos (os que a tripulação a bordo der: 1 por tripulante, no máximo 2) aparecem atrás dele, na praia, e o
    /// seguem e lutam; ao embarcar voltam para o navio (somem). Saem dos beliches só no visual: a tripulação não muda, e um marujo derrubado
    /// levanta ferido depois da briga (ninguém morre de verdade). Fica no objeto do PlayerModeSwitcher.
    /// </summary>
    public class CrewEscort : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] CrewmateAI[] crewmates = Array.Empty<CrewmateAI>();
        [Tooltip("Desembarque no píer do porto (sem marujos lá).")]
        [SerializeField] HarborShoreLeave portLeave;

        ShipCrew crew;
        bool pendingLanding;

        public int Count { get; private set; }
        public System.Collections.Generic.IReadOnlyList<CrewmateAI> Crewmates => crewmates;

        /// <summary>Desceram com o capitão (quantos).</summary>
        public event Action<int> Landed;

        public void Configure(PlayerModeSwitcher modeSwitcher, CrewmateAI[] mates, HarborShoreLeave port)
        {
            switcher = modeSwitcher;
            crewmates = mates;
            portLeave = port;
        }

        void Awake()
        {
            foreach (var c in crewmates) if (c) c.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (switcher) switcher.ModeChanged += OnModeChanged;
        }

        void OnDisable()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        void OnModeChanged(bool onFoot)
        {
            if (!onFoot)
            {
                Dismiss();
                return;
            }
            // O capitão é posto na praia no mesmo quadro: os marujos descem no seguinte, atrás dele.
            pendingLanding = true;
        }

        void LateUpdate()
        {
            if (!pendingLanding) return;
            pendingLanding = false;
            Land();
        }

        void Land()
        {
            Dismiss();
            if (!switcher || !switcher.IsOnFoot || !switcher.Character) return;
            if (portLeave && switcher.Landing && switcher.Landing == portLeave.Pier) return;
            if (!crew && switcher.Ship) crew = switcher.Ship.GetComponent<ShipCrew>();
            int want = Mathf.Min(crewmates.Length, crew ? crew.Count : 0);
            var cap = switcher.Character.transform;
            Vector3 back = cap.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, back);
            Count = 0;
            for (int i = 0; i < want; i++)
            {
                var mate = crewmates[i];
                if (!mate) continue;
                Vector3 at = cap.position - back * 1.8f + side * (i == 0 ? -1.4f : 1.4f);
                if (NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas)) at = hit.position;
                else if (NavMesh.SamplePosition(cap.position, out hit, 3f, NavMesh.AllAreas)) at = hit.position;
                mate.gameObject.SetActive(true);
                mate.Configure(cap, i);
                mate.Arrive(at, cap.rotation);
                Count++;
            }
            if (Count > 0) Landed?.Invoke(Count);
        }

        void Dismiss()
        {
            foreach (var c in crewmates) if (c) c.gameObject.SetActive(false);
            Count = 0;
        }

        /// <summary>O píer do porto (a cena 07 cria o desembarque do porto depois dos marujos).</summary>
        public void SetPort(HarborShoreLeave port) => portLeave = port;

        /// <summary>Teste: põe os marujos em terra agora, atrás do capitão.</summary>
        public void LandNow() => Land();
    }
}
