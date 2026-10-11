using System;
using System.Collections;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Bomba de arremesso do capitão (pacote A de 09/10): a pé, segurar G mostra a mira (arco do voo e círculo da explosão no chão,
    /// seguindo o cursor) e soltar lança a bomba da mochila ali (entre minRange e maxRange), por cima do ombro ("Throw Object" do Mixamo). A bomba aparece na mão no preparo (a arma da mão some), sai no meio da
    /// animação e só então sai da mochila (derrubado no preparo, não gasta). Ao desembarcar, o capitão leva algumas do porão, como as
    /// rações. Bombas = cópias de um molde, criadas no começo e reaproveitadas.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Health), typeof(FootCharacter))]
    public class BombThrower : MonoBehaviour
    {
        [SerializeField] ItemDefinition bomb;
        [Tooltip("Molde da bomba (desligado, fora do personagem).")]
        [SerializeField] ThrownBomb template;
        [SerializeField] PlayerModeSwitcher switcher;
        [Tooltip("Bombas que o capitão tira do porão ao desembarcar (se tiver e couber na mochila).")]
        [SerializeField, Min(0)] int carryAshore = 3;
        [Tooltip("Segundos entre um arremesso e o próximo.")]
        [SerializeField, Min(0f)] float cooldown = 2.5f;
        [Tooltip("Segundos do aperto até a bomba sair da mão (medido no clipe: a mão direita solta a ~2,45 s, tocado de 1,3 s a 1,8×) e da animação inteira.")]
        [SerializeField] float releaseDelay = 0.64f, throwSeconds = 1.1f;
        [Tooltip("Distância (m) mínima e máxima do arremesso; voo = base + metros × por metro.")]
        [SerializeField] float minRange = 4f, maxRange = 14f, flightBase = 0.5f, flightPerMeter = 0.035f;
        [SerializeField, Range(0.1f, 1f)] float moveWhileThrowing = 0.3f;
        [Tooltip("Posição da bomba no osso da mão direita (na palma).")]
        [SerializeField] Vector3 handOffset = new(0f, 0.08f, 0.04f);
        [SerializeField, Min(1)] int poolSize = 3;

        [Header("Mira (segurar G)")]
        [Tooltip("Arco do voo, círculo da explosão no chão e o ponto do centro (criados no gerador).")]
        [SerializeField] LineRenderer aimArc, aimArea, aimCenter;
        [Tooltip("De onde a bomba sai, em relação ao personagem virado para o alvo (direita, cima, frente); medido no clipe aos 2,45 s.")]
        [SerializeField] Vector3 releasePoint = new(0.3f, 0.95f, 0.9f);
        [Tooltip("Velocidade andando com a mira armada.")]
        [SerializeField, Range(0.1f, 1f)] float moveWhileAiming = 0.6f;
        [SerializeField] Color aimReady = new(1f, 0.72f, 0.2f, 0.95f), aimWaiting = new(0.85f, 0.85f, 0.85f, 0.45f),
            aimDanger = new(1f, 0.15f, 0.08f, 0.95f);

        Inventory backpack;
        Health health;
        FootCharacter character;
        CharacterCombat combat;
        CharacterAnimatorDriver animatorDriver;
        PlayerFootInput input;
        Transform hand;
        float nextThrow;
        readonly List<ThrownBomb> pool = new();
        readonly List<Renderer> hiddenWeapon = new();
        Coroutine throwing;
        Vector3? pinnedTarget;

        public ItemDefinition Bomb => bomb;
        public int Count => backpack && bomb ? backpack.Count(bomb) : 0;
        public bool IsThrowing => throwing != null;
        /// <summary>Segurando G: a mira aparece e soltar lança (o ataque com o mouse espera).</summary>
        public bool IsAiming { get; private set; }
        /// <summary>Onde a mira diz que a bomba vai parar (testes).</summary>
        public Vector3 AimLanding { get; private set; }
        public float WaitSeconds => Mathf.Max(0f, nextThrow - Time.time);

        /// <summary>A bomba saiu da mão.</summary>
        public event Action<ThrownBomb> Thrown;
        /// <summary>Apertou G e não lançou: o motivo (HUD).</summary>
        public event Action<string> Refused;
        /// <summary>Levou bombas do porão ao desembarcar: quantas.</summary>
        public event Action<int> Packed;

        public void Configure(ItemDefinition bombItem, ThrownBomb bombTemplate, PlayerModeSwitcher modeSwitcher)
        {
            bomb = bombItem;
            template = bombTemplate;
            switcher = modeSwitcher;
        }

        public void ConfigureAim(LineRenderer arc, LineRenderer area, LineRenderer center)
        {
            aimArc = arc;
            aimArea = area;
            aimCenter = center;
        }

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            health = GetComponent<Health>();
            character = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            input = GetComponent<PlayerFootInput>();
            animatorDriver = GetComponentInChildren<CharacterAnimatorDriver>(true);
            var animator = GetComponentInChildren<Animator>(true);
            if (animator && animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (template)
                for (int i = 0; i < poolSize; i++)
                {
                    var copy = Instantiate(template, template.transform.parent);
                    copy.name = template.name.Replace("_Molde", "") + "_" + i;
                    pool.Add(copy);
                }
            // No Awake (não no OnEnable): o desembarque liga o personagem e só depois avisa a troca de modo.
            if (switcher) switcher.ModeChanged += OnModeChanged;
        }

        void OnDestroy()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        void Update()
        {
            // Segurar G = mira (arco + círculo da explosão seguindo o cursor); soltar = lança ali. Toque rápido lança direto no cursor.
            // Botão direito cancela; janela aberta, derrubado ou morto também.
            var game = GameInput.Instance;
            bool blocked = input && input.IsWindowOpen;
            if (pinnedTarget is Vector3 pinned)
            {
                // Mira por código (testes): ignora o teclado até ReleaseAim/StopAim.
                if (health.IsDead || character.IsDown) StopAim();
                else UpdateAim(pinned);
                return;
            }
            if (!IsAiming)
            {
                if (game.ThrowBomb.WasPressedThisFrame() && !blocked) BeginAim();
                return;
            }
            if (blocked || health.IsDead || character.IsDown || game.Aim.WasPressedThisFrame())
            {
                StopAim();
                return;
            }
            Vector3 target = CursorTarget();
            if (!game.ThrowBomb.IsPressed())
            {
                StopAim();
                TryThrowAt(target);
                return;
            }
            UpdateAim(target);
        }

        void BeginAim()
        {
            if (!bomb || health.IsDead || character.IsDown) return;
            if (Count <= 0)
            {
                Refused?.Invoke("sem bomba na mochila (compre no mercado, faça na Forja ou ache em barris)");
                return;
            }
            IsAiming = true;
            UpdateAim(CursorTarget());
        }

        /// <summary>Arma a mira por código (testes) em <paramref name="target"/>; fica até <see cref="ReleaseAim"/> ou <see cref="StopAim"/>.</summary>
        public void AimAt(Vector3 target)
        {
            pinnedTarget = target;
            IsAiming = true;
            UpdateAim(target);
        }

        /// <summary>Solta a mira armada por código: lança onde ela estava (como soltar o G).</summary>
        public bool ReleaseAim()
        {
            if (pinnedTarget is not Vector3 target) return false;
            StopAim();
            return TryThrowAt(target);
        }

        /// <summary>Esconde a mira sem lançar.</summary>
        public void StopAim()
        {
            pinnedTarget = null;
            IsAiming = false;
            ShowAim(false);
        }

        Vector3 CursorTarget()
        {
            Vector3 target = transform.position + transform.forward * 8f;
            if (input && input.TryGetCursorPoint(out var cursor)) target = cursor;
            return target;
        }

        void ShowAim(bool show)
        {
            if (aimArc) aimArc.enabled = show;
            if (aimArea) aimArea.enabled = show;
            if (aimCenter) aimCenter.enabled = show;
        }

        const int ArcSteps = 28, AreaSegments = 40;
        static readonly RaycastHit[] arcHits = new RaycastHit[8];
        static readonly Vector3[] arcPoints = new Vector3[ArcSteps + 1];

        /// <summary>
        /// Vira o capitão para o alvo, anda mais devagar e desenha o voo como o da bomba (mesmo ponto no chão, mesmo tempo, mesma
        /// esfera batendo no caminho): se uma parede ou pedra estiver no meio, o círculo aparece onde ela vai parar de verdade.
        /// Vermelho = o capitão está dentro da explosão; cinza = ainda acendendo o pavio (espera entre bombas).
        /// </summary>
        void UpdateAim(Vector3 target)
        {
            Vector3 landing = Landing(target, out Vector3 direction, out float distance);
            character.FaceDuring(direction, 0.15f);
            character.SetSpeedMultiplier(moveWhileAiming, 0.15f);

            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Vector3 from = transform.position + right * releasePoint.x + Vector3.up * releasePoint.y + direction * releasePoint.z;
            float t = Mathf.Max(0.2f, flightBase + distance * flightPerMeter);
            Vector3 velocity = (landing - from) / t - 0.5f * t * Physics.gravity;
            float radius = template ? template.Radius : 3.5f;
            float ball = template ? template.BallRadius : 0.14f;

            int count = 1;
            arcPoints[0] = from;
            Vector3 stop = landing;
            for (int i = 1; i <= ArcSteps; i++)
            {
                float s = t * i / ArcSteps;
                Vector3 p = from + velocity * s + 0.5f * s * s * Physics.gravity;
                Vector3 prev = arcPoints[count - 1];
                Vector3 step = p - prev;
                float length = step.magnitude;
                if (length > 0.0001f && SphereHit(prev, ball, step / length, length, out float hitDistance))
                {
                    stop = prev + step / length * hitDistance;
                    arcPoints[count++] = stop;
                    break;
                }
                arcPoints[count++] = p;
                stop = p;
            }
            AimLanding = stop;

            Vector3 chest = transform.position + Vector3.up * 1f;
            Color color = Vector3.Distance(stop, chest) < radius * 0.9f ? aimDanger : Time.time < nextThrow ? aimWaiting : aimReady;
            if (aimArc)
            {
                aimArc.positionCount = count;
                for (int i = 0; i < count; i++) aimArc.SetPosition(i, arcPoints[i]);
                aimArc.startColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
                aimArc.endColor = color;
            }
            if (aimArea) GroundRing(aimArea, stop, radius, AreaSegments, color);
            if (aimCenter) GroundRing(aimCenter, stop, 0.3f, 16, color);
            ShowAim(true);
        }

        bool SphereHit(Vector3 from, float ball, Vector3 direction, float length, out float distance)
        {
            distance = float.MaxValue;
            int n = Physics.SphereCastNonAlloc(from, ball, direction, arcHits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hit = arcHits[i];
                if (hit.transform.IsChildOf(transform) || hit.distance <= 0f) continue;
                if (hit.distance < distance) distance = hit.distance;
            }
            return distance < float.MaxValue;
        }

        /// <summary>Anel deitado que acompanha o chão (raios de cima para baixo; personagens no caminho não contam).</summary>
        void GroundRing(LineRenderer ring, Vector3 center, float radius, int segments, Color color)
        {
            ring.loop = true;
            ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                float y = center.y;
                if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)
                    && !(hit.collider is CharacterController)) y = hit.point.y;
                ring.SetPosition(i, new Vector3(p.x, y + 0.08f, p.z));
            }
            ring.startColor = ring.endColor = color;
        }

        /// <summary>Ponto no chão a <c>minRange–maxRange</c> na direção de <paramref name="target"/> (o mesmo da mira e do lançamento).</summary>
        Vector3 Landing(Vector3 target, out Vector3 direction, out float distance)
        {
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            direction = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            distance = Mathf.Clamp(flat.magnitude, minRange, maxRange);
            Vector3 landing = transform.position + direction * distance;
            if (Physics.Raycast(landing + Vector3.up * 8f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform)) landing = hit.point;
            return landing + Vector3.up * 0.15f;
        }

        void OnDisable()
        {
            StopAim();
            if (throwing != null) StopCoroutine(throwing);
            throwing = null;
            // Embarcou ou morreu com a bomba ainda na mão: volta para a reserva (as que já voaram seguem até explodir).
            foreach (var b in pool) if (b && !b.IsFree && b.transform.IsChildOf(transform)) b.Cancel();
            ShowWeapon();
        }

        /// <summary>Lança uma bomba no ponto do cursor (sem cursor: à frente). false se não deu.</summary>
        public bool TryThrow()
        {
            Vector3 target = transform.position + transform.forward * 8f;
            if (input && input.TryGetCursorPoint(out var cursor)) target = cursor;
            return TryThrowAt(target);
        }

        /// <summary>Lança uma bomba em <paramref name="target"/> (testes; o alcance é limitado a minRange–maxRange). false se não deu.</summary>
        public bool TryThrowAt(Vector3 target)
        {
            if (!bomb || health.IsDead || character.IsDown || character.IsDodging || throwing != null) return false;
            if (combat && (combat.IsAttacking || combat.IsSwapping)) return false;
            if (Time.time < nextThrow)
            {
                Refused?.Invoke($"acendendo o pavio (mais {Mathf.CeilToInt(nextThrow - Time.time)} s)");
                return false;
            }
            if (Count <= 0)
            {
                Refused?.Invoke("sem bomba na mochila (compre no mercado, faça na Forja ou ache em barris)");
                return false;
            }
            var free = pool.Find(b => b && b.IsFree);
            if (!free) return false;
            throwing = StartCoroutine(Throw(free, target));
            return true;
        }

        IEnumerator Throw(ThrownBomb bombCopy, Vector3 target)
        {
            Landing(target, out Vector3 direction, out float distance);
            nextThrow = Time.time + cooldown;
            character.FaceDuring(direction, releaseDelay + 0.2f);
            character.SetSpeedMultiplier(moveWhileThrowing, throwSeconds);
            if (combat) combat.Interrupt(throwSeconds);
            if (animatorDriver) animatorDriver.PlayThrow();
            HideWeapon();
            if (hand) bombCopy.Hold(hand, handOffset);

            yield return new WaitForSeconds(releaseDelay);
            if (health.IsDead || character.IsDown || !hand)
            {
                // Derrubado no preparo: a bomba some e não sai da mochila.
                bombCopy.Cancel();
                ShowWeapon();
                throwing = null;
                yield break;
            }
            backpack.Remove(bomb, 1);
            // Mira de novo na hora de soltar: o alvo no chão, na distância pedida (ou no limite), um pouco acima para não raspar no chão.
            Vector3 landing = Landing(transform.position + direction * distance, out _, out _);
            bombCopy.Launch(landing, flightBase + distance * flightPerMeter, gameObject);
            Thrown?.Invoke(bombCopy);

            yield return new WaitForSeconds(Mathf.Max(0f, throwSeconds - releaseDelay - 0.2f));
            ShowWeapon();
            throwing = null;
        }

        void HideWeapon()
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

        void OnModeChanged(bool onFoot)
        {
            if (!onFoot || !bomb || carryAshore <= 0 || !switcher.Ship || !switcher.Ship.TryGetComponent(out Inventory hold)) return;
            int want = Mathf.Min(carryAshore - Count, Mathf.Min(hold.Count(bomb), backpack.MaxFit(bomb)));
            if (want <= 0) return;
            int moved = backpack.Add(bomb, hold.Remove(bomb, want));
            if (moved > 0) Packed?.Invoke(moved);
        }
    }
}
