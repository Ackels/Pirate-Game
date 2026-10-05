using System.Collections;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Tesouro enterrado: um X no chão, que aparece quando uma missão escolhe este tesouro (MissionStartedEvent com o
    /// mesmo id) ou desde o início, se configurado. Perto do X, F cava (LandLooter): o baú sobe do chão e vira um
    /// LandChest comum (destrancado), e o jogo avisa TreasureFoundEvent (missão cumprida). Nova missão enterra de novo.
    /// </summary>
    public class BuriedTreasure : MonoBehaviour
    {
        static readonly List<BuriedTreasure> active = new();
        public static IReadOnlyList<BuriedTreasure> Active => active;

        [Tooltip("Id para as missões: mesmo texto do targetId da missão.")]
        [SerializeField] string treasureId = "tesouro";
        [Tooltip("Fama do capitão ao desenterrar (M10).")]
        [SerializeField, Min(0)] int fame = 10;
        [SerializeField] string displayName = "Tesouro enterrado";
        [Tooltip("O X no chão (some depois de cavar).")]
        [SerializeField] GameObject mark;
        [Tooltip("Baú enterrado: fica desligado até cavar.")]
        [SerializeField] LandChest chest;
        [Tooltip("Desligado: o X só aparece quando uma missão pede este tesouro.")]
        [SerializeField] bool visibleFromStart;
        [SerializeField] float riseSeconds = 0.9f;
        [SerializeField] float buriedDepth = 0.9f;

        bool revealed, dug;

        public string TreasureId => treasureId;
        public string DisplayName => displayName;
        /// <summary>X visível e ainda não cavado.</summary>
        public bool CanDig => revealed && !dug;

        public void Configure(string id, string name, GameObject xMark, LandChest buriedChest, bool showFromStart)
        {
            treasureId = id;
            displayName = name;
            mark = xMark;
            chest = buriedChest;
            visibleFromStart = showFromStart;
        }

        void Awake()
        {
            revealed = visibleFromStart;
            if (mark) mark.SetActive(revealed);
            if (chest) chest.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            active.Add(this);
            EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void OnDisable()
        {
            active.Remove(this);
            EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void OnMissionStarted(MissionStartedEvent evt)
        {
            if (evt.targetId == treasureId) Bury();
        }

        /// <summary>Enterra (de novo) e mostra o X.</summary>
        public void Bury()
        {
            StopAllCoroutines();
            dug = false;
            revealed = true;
            if (mark) mark.SetActive(true);
            if (!chest) return;
            chest.ResetChest();
            chest.gameObject.SetActive(false);
        }

        /// <summary>Cava: o X some, o baú sobe do chão e o tesouro conta como achado.</summary>
        public void Dig()
        {
            if (!CanDig) return;
            dug = true;
            if (mark) mark.SetActive(false);
            if (chest)
            {
                chest.gameObject.SetActive(true);
                StartCoroutine(Rise(chest.transform));
            }
            EventBus.Publish(new TreasureFoundEvent { treasureId = treasureId, fame = fame });
        }

        IEnumerator Rise(Transform t)
        {
            Vector3 top = Vector3.zero, bottom = Vector3.down * buriedDepth;
            for (float k = 0f; k < riseSeconds; k += Time.deltaTime)
            {
                t.localPosition = Vector3.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, k / riseSeconds));
                yield return null;
            }
            t.localPosition = top;
        }
    }
}
