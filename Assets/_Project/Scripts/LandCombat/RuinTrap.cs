using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Armadilha da ruína (C4, 19/10), num ciclo fixo (dá para ler e passar no ritmo): espera → <b>aviso</b> → <b>ativa</b>.
    /// <b>Espinhos</b>: as pontas aparecem no aviso e sobem de vez na ativa (golpe em quem está em cima, uma vez por subida, e um
    /// empurrão para fora). <b>Dardo</b>: o buraco na parede brilha vermelho no aviso e o dardo sai na ativa, cruzando o corredor (acerta o
    /// primeiro personagem no caminho). <b>Fogo</b>: fumaça na grade no aviso e a chama na ativa (dano a cada <see cref="fireTick"/> em
    /// quem está dentro). Rolar na hora certa passa (o rolamento é invencível). Só trabalha com a câmera perto. A alavanca da ala
    /// (<see cref="SetLever"/>) desarma: puxada, a armadilha para (espinhos descem, fogo apaga) até a ruína voltar ao começo.
    /// </summary>
    public class RuinTrap : MonoBehaviour
    {
        public enum Kind { Spikes, Dart, Fire }

        static readonly List<RuinTrap> active = new();
        public static IReadOnlyList<RuinTrap> Active => active;
        static readonly Collider[] overlap = new Collider[16];
        static readonly RaycastHit[] hits = new RaycastHit[8];

        [SerializeField] Kind kind;
        [Tooltip("Ciclo inteiro (s) e o deslocamento dele (armadilhas vizinhas fora de fase formam uma onda).")]
        [SerializeField] float period = 3f, offset;
        [SerializeField] float warnSeconds = 0.6f, activeSeconds = 1f;
        [SerializeField] float damage = 18f;
        [Tooltip("Caixa que machuca (local): centro e meio tamanho. Espinhos e fogo.")]
        [SerializeField] Vector3 boxCenter = new(0f, 0.6f, 0f), boxHalf = new(1f, 0.6f, 1f);
        [SerializeField] float pushSpeed = 4f;
        [Header("Espinhos")]
        [SerializeField] Transform spikes;
        [SerializeField] float hiddenY = -0.55f, peekY = -0.38f, raisedY;
        [Header("Dardo")]
        [SerializeField] Transform muzzle;
        [SerializeField] Transform dart;
        [SerializeField] float dartSpeed = 16f, dartRange = 4f;
        [Header("Fogo")]
        [SerializeField] ParticleSystem flames;
        [SerializeField] ParticleSystem smoke;
        [SerializeField] Light fireLight;
        [SerializeField] float fireTick = 0.45f;
        [Header("Aviso (dardo e fogo)")]
        [SerializeField] Renderer warnRenderer;
        [SerializeField] Material idleMaterial, warnMaterial;
        [SerializeField] float wakeRange = 45f;
        [Tooltip("Alavanca que desarma esta armadilha quando puxada (a volta pelo corredor fica livre).")]
        [SerializeField] RuinLever disarmedBy;

        enum Phase { Idle, Warn, Active }

        Phase phase;
        readonly HashSet<Health> struck = new();
        readonly Dictionary<Health, float> nextBurn = new();
        bool dartFlying;
        float dartTravelled;

        public Kind TrapKind => kind;
        /// <summary>Aviso começou (true) ou a armadilha disparou (false): som e efeitos.</summary>
        public static event Action<RuinTrap, bool> Triggered;
        /// <summary>Acertou alguém: a armadilha e o ponto.</summary>
        public static event Action<RuinTrap, Vector3> HitSomeone;

        public void Configure(Kind type, float cycle, float phaseOffset, float warn, float on, float hurt)
        {
            kind = type;
            period = cycle;
            offset = phaseOffset;
            warnSeconds = warn;
            activeSeconds = on;
            damage = hurt;
        }

        public void SetBox(Vector3 center, Vector3 half)
        {
            boxCenter = center;
            boxHalf = half;
        }

        public void SetSpikes(Transform spikeGroup) => spikes = spikeGroup;

        public void SetDart(Transform muzzlePoint, Transform dartVisual, float range)
        {
            muzzle = muzzlePoint;
            dart = dartVisual;
            dartRange = range;
        }

        public void SetFire(ParticleSystem flame, ParticleSystem smokeWarn, Light light)
        {
            flames = flame;
            smoke = smokeWarn;
            fireLight = light;
        }

        public void SetLever(RuinLever lever) => disarmedBy = lever;

        /// <summary>A alavanca da ala foi puxada: parada.</summary>
        public bool Disarmed => disarmedBy && disarmedBy.IsPulled;

        public void SetWarning(Renderer target, Material idle, Material warn)
        {
            warnRenderer = target;
            idleMaterial = idle;
            warnMaterial = warn;
        }

        void OnEnable()
        {
            active.Add(this);
            phase = Phase.Idle;
            ApplyIdle();
        }

        void OnDisable() => active.Remove(this);

        void Update()
        {
            var cam = Camera.main;
            if (cam && (cam.transform.position - transform.position).sqrMagnitude > wakeRange * wakeRange)
            {
                if (phase != Phase.Idle) EnterIdle();
                return;
            }

            float t = Mathf.Repeat(Time.time + offset, period);
            float activeAt = period - activeSeconds, warnAt = activeAt - warnSeconds;
            var now = Disarmed ? Phase.Idle : t >= activeAt ? Phase.Active : t >= warnAt ? Phase.Warn : Phase.Idle;
            if (now != phase)
            {
                if (now == Phase.Warn) EnterWarn();
                else if (now == Phase.Active) EnterActive();
                else EnterIdle();
            }

            switch (kind)
            {
                case Kind.Spikes:
                    if (spikes)
                    {
                        float target = phase == Phase.Active ? raisedY : phase == Phase.Warn ? peekY : hiddenY;
                        var p = spikes.localPosition;
                        // Sobe num estalo (0,06 s), desce devagar.
                        p.y = Mathf.MoveTowards(p.y, target, Time.deltaTime * (target > p.y ? 10f : 1.5f));
                        spikes.localPosition = p;
                        if (phase == Phase.Active && p.y > (hiddenY + raisedY) * 0.5f) HurtInBox(once: true);
                    }
                    break;
                case Kind.Fire:
                    if (phase == Phase.Active) HurtInBox(once: false);
                    break;
                case Kind.Dart:
                    if (dartFlying) FlyDart();
                    break;
            }
        }

        void EnterIdle()
        {
            phase = Phase.Idle;
            ApplyIdle();
        }

        void ApplyIdle()
        {
            if (warnRenderer && idleMaterial) warnRenderer.sharedMaterial = idleMaterial;
            if (flames) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (fireLight) fireLight.enabled = false;
        }

        void EnterWarn()
        {
            phase = Phase.Warn;
            if (warnRenderer && warnMaterial) warnRenderer.sharedMaterial = warnMaterial;
            if (smoke) smoke.Play(true);
            Triggered?.Invoke(this, true);
        }

        void EnterActive()
        {
            phase = Phase.Active;
            struck.Clear();
            if (warnRenderer && idleMaterial) warnRenderer.sharedMaterial = idleMaterial;
            if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (flames) flames.Play(true);
            if (fireLight) fireLight.enabled = true;
            if (kind == Kind.Dart && muzzle && dart)
            {
                dartFlying = true;
                dartTravelled = 0f;
                dart.SetPositionAndRotation(muzzle.position, muzzle.rotation);
                dart.gameObject.SetActive(true);
            }
            Triggered?.Invoke(this, false);
        }

        /// <summary>Golpe em quem está na caixa: uma vez por ativação (espinhos) ou a cada <see cref="fireTick"/> (fogo).</summary>
        void HurtInBox(bool once)
        {
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(boxCenter), Vector3.Scale(boxHalf, transform.lossyScale), overlap,
                transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var character = overlap[i].GetComponentInParent<FootCharacter>();
                if (!character || !character.TryGetComponent(out Health health) || health.IsDead) continue;
                if (once)
                {
                    if (!struck.Add(health)) continue;
                }
                else
                {
                    if (nextBurn.TryGetValue(health, out float at) && Time.time < at) continue;
                    nextBurn[health] = Time.time + fireTick;
                }
                Strike(character, health, character.transform.position + Vector3.up);
            }
        }

        void Strike(FootCharacter character, Health health, Vector3 point)
        {
            Vector3 away = character.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            float before = health.Current;
            health.TakeDamage(new DamageInfo { amount = damage, point = point, direction = away.normalized, source = gameObject });
            // Rolando (invencível) não leva nada, nem o empurrão.
            if (health.Current >= before) return;
            if (pushSpeed > 0f && character.TryGetComponent(out IPushable push)) push.Push(away.normalized * pushSpeed);
            HitSomeone?.Invoke(this, point);
        }

        void FlyDart()
        {
            float step = dartSpeed * Time.deltaTime;
            Vector3 from = dart.position, dir = dart.forward;
            int n = Physics.SphereCastNonAlloc(from, 0.12f, dir, hits, step, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            FootCharacter victim = null;
            Health victimHealth = null;
            bool blocked = false;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance <= 0f || hits[i].distance >= nearest) continue;
                var character = hits[i].collider.GetComponentInParent<FootCharacter>();
                if (character && character.TryGetComponent(out Health h) && !h.IsDead)
                {
                    nearest = hits[i].distance;
                    victim = character;
                    victimHealth = h;
                    blocked = false;
                }
                else if (!hits[i].collider.transform.IsChildOf(transform))
                {
                    nearest = hits[i].distance;
                    victim = null;
                    blocked = true;
                }
            }
            if (victim)
            {
                Strike(victim, victimHealth, from + dir * nearest);
                StopDart();
                return;
            }
            if (blocked)
            {
                StopDart();
                return;
            }
            dart.position = from + dir * step;
            dartTravelled += step;
            if (dartTravelled >= dartRange) StopDart();
        }

        void StopDart()
        {
            dartFlying = false;
            if (dart) dart.gameObject.SetActive(false);
        }
    }
}
