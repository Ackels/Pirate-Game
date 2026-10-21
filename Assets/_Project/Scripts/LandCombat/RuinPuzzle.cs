using System;
using Pirates.Core;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// A ruína das alavancas (C4, 19/10; pacote C, exploração): o portão de grade do santuário só sobe com todas as alavancas puxadas
    /// (cada uma atrás de uma armadilha). Aberto, avisa o jogo como um tesouro achado (<see cref="TreasureFoundEvent"/> com
    /// <see cref="treasureId"/>: a missão da ermida conta e a fama vem junto) e destranca o baú do santuário (este componente é a tranca
    /// dele, <see cref="ILockSource"/>). Volta ao começo (alavancas em cima, braseiros apagados, portão fechado, baú cheio de novo) quando
    /// a missão dela é aceita ou pelo F1; como as ilhas, volta cheia ao carregar o jogo.
    /// </summary>
    public class RuinPuzzle : MonoBehaviour, ILockSource
    {
        [SerializeField] string displayName = "Ermida";
        [SerializeField] string treasureId;
        [SerializeField] int fame = 25;
        [SerializeField] RuinLever[] levers = Array.Empty<RuinLever>();
        [Tooltip("A grade do portão (com o colisor): sobe ao abrir.")]
        [SerializeField] Transform gate;
        [SerializeField] float gateRise = 2.6f, gateSeconds = 2.4f;
        [Tooltip("Outras grades que abrem junto (o atalho de volta à entrada).")]
        [SerializeField] Transform[] extraGates = Array.Empty<Transform>();
        [Tooltip("Onde se entra na ruína (o \"Ir até lá\" do F1 a pé põe o capitão aqui, virado para dentro).")]
        [SerializeField] Transform entrance;

        Vector3 gateClosed;
        float openedAt = -1f;

        public string DisplayName => displayName;
        public string TreasureId => treasureId;
        public bool IsOpen { get; private set; }
        public int LeverCount => levers.Length;
        public RuinLever[] Levers => levers;
        public Transform Gate => gate;
        public Transform Entrance => entrance;

        public int Pulled
        {
            get
            {
                int n = 0;
                foreach (var l in levers)
                    if (l && l.IsPulled) n++;
                return n;
            }
        }

        public bool IsLocked => !IsOpen;
        public string LockedReason => $"puxe as {levers.Length} alavancas da ruína: {Pulled} de {levers.Length}";

        /// <summary>O portão começou a subir.</summary>
        public event Action<RuinPuzzle> Opened;
        /// <summary>Uma alavanca desta ruína foi puxada: quantas já foram e o total.</summary>
        public event Action<RuinPuzzle, int, int> LeverPulled;

        /// <param name="rise">Quanto a grade anda ao abrir (m): negativo afunda no chão (a da Ermida some dentro da plataforma).</param>
        public void Configure(string name, string id, int fameReward, RuinLever[] all, Transform gateBars, float rise = 2.6f)
        {
            gateRise = rise;
            displayName = name;
            treasureId = id;
            fame = fameReward;
            levers = all;
            gate = gateBars;
        }

        /// <summary>Grades a mais que abrem junto e a entrada da ruína (opcionais).</summary>
        public void SetExtras(Transform[] gates, Transform entry)
        {
            extraGates = gates ?? Array.Empty<Transform>();
            entrance = entry;
        }

        Vector3[] extraClosed = Array.Empty<Vector3>();

        void Awake()
        {
            if (gate) gateClosed = gate.localPosition;
            extraClosed = new Vector3[extraGates.Length];
            for (int i = 0; i < extraGates.Length; i++)
                if (extraGates[i]) extraClosed[i] = extraGates[i].localPosition;
            foreach (var l in levers)
                if (l) l.Bind(this);
        }

        void OnEnable()
        {
            RuinLever.Pulled += OnLeverPulled;
            EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void OnDisable()
        {
            RuinLever.Pulled -= OnLeverPulled;
            EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void OnLeverPulled(RuinLever lever)
        {
            if (lever.Puzzle != this || IsOpen) return;
            int n = Pulled;
            LeverPulled?.Invoke(this, n, levers.Length);
            if (n >= levers.Length) Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            openedAt = Time.time;
            Opened?.Invoke(this);
            if (!string.IsNullOrEmpty(treasureId)) EventBus.Publish(new TreasureFoundEvent { treasureId = treasureId, fame = fame });
        }

        void OnMissionStarted(MissionStartedEvent e)
        {
            if (!string.IsNullOrEmpty(treasureId) && e.targetId == treasureId) ResetAll();
        }

        /// <summary>De volta ao começo: alavancas, braseiros, portão fechado (o baú tranca e esvazia sozinho).</summary>
        public void ResetAll()
        {
            IsOpen = false;
            openedAt = -1f;
            foreach (var l in levers)
                if (l) l.ResetLever();
            if (gate) gate.localPosition = gateClosed;
            for (int i = 0; i < extraGates.Length; i++)
                if (extraGates[i]) extraGates[i].localPosition = extraClosed[i];
        }

        void Update()
        {
            if (openedAt < 0f) return;
            float k = Mathf.Clamp01((Time.time - openedAt) / gateSeconds);
            if (gate) gate.localPosition = gateClosed + Vector3.up * (gateRise * k);
            for (int i = 0; i < extraGates.Length; i++)
                if (extraGates[i]) extraGates[i].localPosition = extraClosed[i] + Vector3.up * (gateRise * k);
            if (k >= 1f) openedAt = -1f;
        }
    }
}
