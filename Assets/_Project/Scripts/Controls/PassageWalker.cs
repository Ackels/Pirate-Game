using System;
using System.Collections;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// F perto de uma passagem da ruína (C5, 20/10: o alçapão da Ermida e a escada das catacumbas): a tela escurece
    /// (<see cref="Fade"/>, desenhado pela UI), o capitão aparece no destino, a câmera pula junto e a tela clareia. Parado durante a ida.
    /// </summary>
    public class PassageWalker : MonoBehaviour, IInteraction
    {
        [SerializeField] float range = 1.9f;
        [Tooltip("Escurecer, segurar no preto e clarear (s).")]
        [SerializeField] float fadeOutSeconds = 0.45f, holdSeconds = 0.25f, fadeInSeconds = 0.55f;

        FootCharacter footCharacter;
        CharacterCombat combat;
        Coroutine travel;

        public RuinPassage Nearby { get; private set; }
        /// <summary>0 = tela normal, 1 = preta.</summary>
        public float Fade { get; private set; }
        public bool IsTraveling => travel != null;
        public bool IsOpen => false;
        public bool CanOpen => Nearby && !Nearby.IsLocked && !IsTraveling;

        /// <summary>Chegou do outro lado (a UI mostra a faixa da passagem).</summary>
        public event Action<RuinPassage> Arrived;

        void Awake()
        {
            footCharacter = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
        }

        void OnDisable()
        {
            if (travel != null) StopCoroutine(travel);
            travel = null;
            Fade = 0f;
        }

        void Update()
        {
            Nearby = null;
            if (IsTraveling) return;
            float best = range * range;
            Vector3 me = transform.position;
            var list = RuinPassage.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 d = list[i].transform.position - me;
                if (Mathf.Abs(d.y) > 2f) continue;
                d.y = 0f;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    Nearby = list[i];
                }
            }
        }

        public bool Open()
        {
            var passage = Nearby;
            if (!passage || passage.IsLocked || IsTraveling || !passage.Destination) return false;
            if (footCharacter && (footCharacter.IsDown || footCharacter.IsDodging)) return false;
            travel = StartCoroutine(Travel(passage));
            return true;
        }

        public void Close() { }

        IEnumerator Travel(RuinPassage passage)
        {
            float total = fadeOutSeconds + holdSeconds + fadeInSeconds;
            if (footCharacter) footCharacter.SetSpeedMultiplier(0f, total);
            if (combat) combat.Interrupt(total);
            passage.MarkUsed();
            for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
            {
                Fade = t / fadeOutSeconds;
                yield return null;
            }
            Fade = 1f;
            var to = passage.Destination;
            if (footCharacter) footCharacter.Teleport(to.position, to.rotation);
            else transform.SetPositionAndRotation(to.position, to.rotation);
            if (Services.TryGet(out ICameraFollow cam)) cam.SnapToTarget();
            yield return new WaitForSeconds(holdSeconds);
            Arrived?.Invoke(passage);
            for (float t = 0f; t < fadeInSeconds; t += Time.deltaTime)
            {
                Fade = 1f - t / fadeInSeconds;
                yield return null;
            }
            Fade = 0f;
            travel = null;
        }
    }
}
