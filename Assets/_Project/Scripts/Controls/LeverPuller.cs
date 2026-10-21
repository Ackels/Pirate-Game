using System.Collections.Generic;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// F perto de uma alavanca da ruína (C4, 19/10): o capitão desliza até a frente dela (<see cref="RuinLever.StandPoint"/>), toca o
    /// "Pulling Lever" do Mixamo (a arma some da mão, não anda nem ataca até acabar) e o cabo deita quando a mão puxa. Também diz qual
    /// alavanca está perto, para o aviso.
    /// </summary>
    public class LeverPuller : MonoBehaviour, IInteraction
    {
        [SerializeField] float range = 2.2f;
        [Tooltip("Tempo da animação inteira (s) e de quando a mão puxa o cabo (s do começo): o \"Pulling Lever\" de 0,8 a 4,6 s em 1,8× (a mão desce aos ~1,4 s).")]
        [SerializeField] float busySeconds = 2.1f, pullAt = 1.38f;
        [Tooltip("Tempo para chegar ao lugar na frente da alavanca.")]
        [SerializeField] float slideSeconds = 0.25f;

        CharacterController controller;
        FootCharacter footCharacter;
        CharacterCombat combat;
        CharacterAnimatorDriver animatorDriver;
        readonly List<Renderer> hiddenWeapon = new();
        float busyUntil, slideUntil;
        Vector3 slideTarget;

        public RuinLever Nearby { get; private set; }
        public bool IsOpen => false;
        public bool CanOpen => Nearby && !Nearby.IsPulled && !IsPulling;
        /// <summary>No meio da animação de puxar.</summary>
        public bool IsPulling => Time.time < busyUntil;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            footCharacter = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            animatorDriver = GetComponentInChildren<CharacterAnimatorDriver>(true);
        }

        void OnDisable()
        {
            busyUntil = slideUntil = 0f;
            ShowWeapon();
        }

        void Update()
        {
            if (Time.time < slideUntil && controller && controller.enabled)
            {
                Vector3 d = slideTarget - transform.position;
                d.y = 0f;
                float left = Mathf.Max(slideUntil - Time.time, Time.deltaTime);
                controller.Move(d * Mathf.Clamp01(Time.deltaTime / left));
            }
            if (hiddenWeapon.Count > 0 && !IsPulling) ShowWeapon();

            Nearby = null;
            float best = range * range;
            Vector3 me = transform.position;
            var list = RuinLever.Active;
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
            var lever = Nearby;
            if (!lever || IsPulling || (footCharacter && (footCharacter.IsDown || footCharacter.IsDodging))) return false;
            Vector3 from = transform.position;
            if (!lever.Pull(from, pullAt)) return false;
            lever.StandPoint(from, out slideTarget, out Quaternion facing);
            slideUntil = Time.time + slideSeconds;
            busyUntil = Time.time + busySeconds;
            if (footCharacter)
            {
                footCharacter.SetSpeedMultiplier(0f, busySeconds);
                footCharacter.FaceDuring(facing * Vector3.forward, busySeconds);
            }
            transform.rotation = facing;
            if (combat) combat.Occupy(busySeconds);
            HideWeaponInHand();
            if (animatorDriver) animatorDriver.PlayLever();
            return true;
        }

        public void Close() { }

        void HideWeaponInHand()
        {
            if (!combat || combat.IsSwapping) return;
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual) return;
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                {
                    r.enabled = false;
                    hiddenWeapon.Add(r);
                }
        }

        void ShowWeapon()
        {
            foreach (var r in hiddenWeapon) if (r) r.enabled = true;
            hiddenWeapon.Clear();
        }
    }
}
