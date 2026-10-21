using System;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Passagem entre dois lugares da ruína (C5, 20/10): o alçapão da capela da Ermida desce às catacumbas e a escada lá embaixo sobe de
    /// volta. F perto (<c>PassageWalker</c>, no capitão) escurece a tela e leva o capitão até <see cref="Destination"/>. Pode depender de uma
    /// tranca (a grade da capela: o alçapão abre junto com ela); a tampa abre e fecha sozinha seguindo a tranca.
    /// </summary>
    public class RuinPassage : MonoBehaviour
    {
        static readonly List<RuinPassage> active = new();
        public static IReadOnlyList<RuinPassage> Active => active;

        [SerializeField] string actionText = "Descer";
        [Tooltip("Para onde a passagem leva (posição e giro do capitão ao chegar).")]
        [SerializeField] Transform destination;
        [Tooltip("Faixa ao chegar (vazio = sem faixa).")]
        [SerializeField] string arrivalTitle, arrivalSubtitle;
        [Tooltip("Tranca (a ruína das alavancas): fechada, a passagem não abre. Vazio = sempre aberta.")]
        [SerializeField] MonoBehaviour lockSource;
        [Tooltip("Tampa que gira no X local ao abrir (o alçapão), de 0 até openAngle.")]
        [SerializeField] Transform lid;
        [SerializeField] float openAngle = 112f, lidSpeed = 140f;
        [Tooltip("O escuro debaixo da tampa (aparece com ela aberta).")]
        [SerializeField] GameObject opening;

        ILockSource lockCache;
        float lidAngle;

        public string ActionText => actionText;
        public Transform Destination => destination;
        public string ArrivalTitle => arrivalTitle;
        public string ArrivalSubtitle => arrivalSubtitle;
        ILockSource Lock => lockCache ??= lockSource as ILockSource;
        public bool IsLocked => Lock != null && Lock.IsLocked;
        public string LockedReason => Lock?.LockedReason;
        /// <summary>Muda quando o texto da tranca muda (alavancas puxadas), para a UI não montar o texto a cada quadro.</summary>
        public int LockProgress => lockSource is RuinPuzzle puzzle ? puzzle.Pulled : 0;

        /// <summary>Alguém passou por aqui (som da tampa/escada).</summary>
        public static event Action<RuinPassage> Used;

        public void Configure(string action, Transform to, string title, string subtitle, MonoBehaviour locker = null, Transform lidPivot = null,
            GameObject hole = null)
        {
            actionText = action;
            destination = to;
            arrivalTitle = title;
            arrivalSubtitle = subtitle;
            lockSource = locker;
            lid = lidPivot;
            opening = hole;
        }

        void OnEnable()
        {
            active.Add(this);
            lidAngle = IsLocked ? 0f : openAngle;
            ApplyLid();
        }

        void OnDisable() => active.Remove(this);

        void Update()
        {
            if (!lid) return;
            float target = IsLocked ? 0f : openAngle;
            if (Mathf.Approximately(lidAngle, target)) return;
            lidAngle = Mathf.MoveTowards(lidAngle, target, lidSpeed * Time.deltaTime);
            ApplyLid();
        }

        void ApplyLid()
        {
            if (lid) lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
            if (opening) opening.SetActive(Mathf.Abs(lidAngle) > 1f || !lid);
        }

        /// <summary>Quem passou avisa (o <c>PassageWalker</c> fica em Controls).</summary>
        public void MarkUsed() => Used?.Invoke(this);
    }
}
