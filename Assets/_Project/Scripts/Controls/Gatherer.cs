using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Cortar árvores (D1, passo 1, 28/10; plano de 16/10: "cortar árvores e minerar, machado e picareta no mercado, árvores e veios que
    /// voltam"): a pé, com a machadinha (item comprado no mercado, guardado em qualquer lugar) e perto de uma árvore ou palmeira da mata
    /// (<see cref="ResourceNode"/>), F começa a cortar: o capitão se põe na frente do tronco, guarda a arma, pega a machadinha e bate
    /// ("MiningOneHand01_R - Wall" do Kevin Iglesias, uma machadada por volta do clipe) até a árvore tombar; a madeira vai para a mochila.
    /// Esc, andar, levar golpe, cair ou rolar param (a árvore guarda os golpes por um tempo). Não corta com piratas em alerta por perto
    /// nem com a mochila cheia; perde para qualquer outra interação do F disponível ali (baú, bote, alavanca), como a pesca.
    /// Fica no capitão (só liga em terra).
    /// </summary>
    public class Gatherer : MonoBehaviour, IInteraction
    {
        /// <summary>Ferramenta de um tipo de nó: o item que libera e o visual na mão (desligado fora da coleta).</summary>
        [Serializable]
        public class Tool
        {
            public ResourceNode.Kind kind;
            public ItemDefinition item;
            public GameObject visual;
            [Tooltip("Nome para os avisos (\"sem machadinha\").")]
            public string name = "machadinha";
            [Tooltip("Verbo do aviso [F] (\"Cortar\").")]
            public string verb = "Cortar";
            [Tooltip("Golpe no chão (\"Minerar\": picareta no pé da pedra) em vez do golpe na parede (\"Coletar\": machadinha no tronco).")]
            public bool ground;
        }

        [SerializeField] Tool[] tools = Array.Empty<Tool>();
        [Tooltip("Procura nós até esta distância da frente do capitão (m).")]
        [SerializeField] float reach = 1.6f;
        [Tooltip("Do tronco até o capitão, além do raio do tronco (a machadinha alcança ~0,6 m à frente da mão).")]
        [SerializeField] float standOff = 0.62f;
        [Tooltip("Da face da pedra até o capitão, no veio (a picareta bate no chão ~1 m à frente; com 0,8 o chapéu entrava na pedra).")]
        [SerializeField] float oreStandOff = 1f;
        [Tooltip("Pirata em alerta a menos disto (m) não deixa coletar.")]
        [SerializeField] float enemyRange = 22f;
        [Header("Tempos (s) do clipe (0,97 s por machadada; o golpe pega aos 0,40)")]
        [SerializeField] float swingSeconds = 0.97f;
        [SerializeField] float firstImpact = 0.55f;
        [SerializeField] float slideSeconds = 0.25f;

        Inventory backpack;
        Health health;
        FootCharacter footCharacter;
        CharacterCombat combat;
        CharacterAnimatorDriver animatorDriver;
        IInteraction[] others = Array.Empty<IInteraction>();
        readonly List<Renderer> hiddenWeapon = new();
        static readonly Collider[] scanHits = new Collider[32];

        Tool tool;
        Vector3 facing, slideTo, strikePoint;
        CharacterController controller;
        float startedAt, nextImpact, nextScan;

        /// <summary>A árvore à frente (atualizada a cada 0,2 s), cortando ou não.</summary>
        public ResourceNode Target { get; private set; }
        /// <summary>Por que não dá para cortar a árvore à frente agora (vazio = dá ou não há árvore).</summary>
        public string BlockReason { get; private set; } = "";
        public bool Working { get; private set; }
        /// <summary>Ferramenta e verbo do nó à frente (para o aviso "[F] Cortar a árvore").</summary>
        public Tool TargetTool => Target ? ToolFor(Target.Type) : null;

        public bool IsOpen => Working;
        public bool CanOpen => !Working && Target && Target.Standing && BlockReason.Length == 0 && !OtherInteraction();

        /// <summary>Tombou e deu isto (já na mochila); o que não coube vai no segundo número.</summary>
        public static event Action<Gatherer, ItemDefinition, int, int> Gathered;
        /// <summary>Parou sem terminar: o motivo (para o aviso; vazio = parou de propósito).</summary>
        public static event Action<Gatherer, string> Stopped;

        public void Configure(Tool[] toolList) => tools = toolList;

        void Awake()
        {
            backpack = GetComponent<Inventory>();
            health = GetComponent<Health>();
            footCharacter = GetComponent<FootCharacter>();
            controller = GetComponent<CharacterController>();
            combat = GetComponent<CharacterCombat>();
            animatorDriver = GetComponentInChildren<CharacterAnimatorDriver>(true);
            var list = new List<IInteraction>();
            foreach (var i in GetComponents<IInteraction>()) if (!ReferenceEquals(i, this)) list.Add(i);
            others = list.ToArray();
            HideTools();
        }

        void OnEnable()
        {
            if (health) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health) health.Damaged -= OnDamaged;
            if (Working) End();
        }

        bool OtherInteraction()
        {
            for (int i = 0; i < others.Length; i++)
                if (others[i] != null && others[i] is not FishingRod && others[i].CanOpen) return true;
            return false;
        }

        Tool ToolFor(ResourceNode.Kind kind)
        {
            foreach (var t in tools) if (t != null && t.kind == kind) return t;
            return null;
        }

        static bool Has(Tool t) => t != null && t.item && PlayerStorage.Count(t.item) > 0;

        void OnDamaged(DamageInfo info)
        {
            if (Working) Stop("levou um golpe");
        }

        void Update()
        {
            if (!Working)
            {
                if (Time.time >= nextScan)
                {
                    nextScan = Time.time + 0.2f;
                    Scan();
                }
                return;
            }

            // Cortando: parado, virado para o tronco, sem atacar.
            if (footCharacter)
            {
                footCharacter.SetSpeedMultiplier(0f, 0.25f);
                footCharacter.FaceDuring(facing, 0.25f);
            }
            if (combat) combat.Occupy(0.25f);
            // Desliza até a frente do tronco (como na alavanca: pelo CharacterController, sem teleportar).
            float slideLeft = startedAt + slideSeconds - Time.time;
            if (slideLeft > 0f && controller && controller.enabled)
            {
                Vector3 d = slideTo - transform.position;
                d.y = 0f;
                controller.Move(d * Mathf.Clamp01(Time.deltaTime / Mathf.Max(slideLeft, Time.deltaTime)));
            }
            bool moving = GameInput.Instance.Move.ReadValue<Vector2>().sqrMagnitude > 0.16f;
            if (moving || (footCharacter && (footCharacter.IsDown || footCharacter.IsDodging)) || (health && health.IsDead))
            {
                Stop(moving ? "" : "parou");
                return;
            }
            if (!Target || !Target.Standing)
            {
                End();
                return;
            }
            if (Time.time >= nextImpact)
            {
                nextImpact += swingSeconds;
                Impact();
            }
        }

        /// <summary>Procura a árvore em pé mais perto à frente e diz por que não dá para cortar.</summary>
        void Scan()
        {
            Target = null;
            BlockReason = "";
            if (!footCharacter || !footCharacter.IsGrounded || footCharacter.InDeepWater) return;
            Vector3 me = transform.position, fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return;
            fwd.Normalize();
            int count = Physics.OverlapSphereNonAlloc(me + fwd * (reach * 0.5f) + Vector3.up * 0.6f, reach, scanHits, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var node = scanHits[i].GetComponentInParent<ResourceNode>();
                if (!node || !node.Standing || ToolFor(node.Type) == null) continue;
                Vector3 to = node.transform.position - me;
                to.y = 0f;
                // Veio: a pedra é irregular, vale a distância até a caixa dela; árvore: até o tronco redondo.
                float d = node.Type == ResourceNode.Kind.Ore
                    ? Vector3.Distance(Flat(scanHits[i].bounds.ClosestPoint(me)), Flat(me))
                    : to.magnitude - node.Radius;
                if (d > reach || (to.sqrMagnitude > 0.01f && Vector3.Angle(fwd, to) > 75f) || d >= best) continue;
                best = d;
                Target = node;
            }
            if (!Target) return;
            var t = ToolFor(Target.Type);
            if (!Has(t)) BlockReason = $"sem {t.name} (compre no mercado)";
            else if (backpack && backpack.FreeSpace < (Target.Item ? Target.Item.spacePerUnit : 1f)) BlockReason = "mochila cheia";
            else if (EnemiesNear()) BlockReason = "piratas por perto";
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        bool EnemiesNear()
        {
            var list = EnemyFootAI.Active;
            float r2 = enemyRange * enemyRange;
            Vector3 me = transform.position;
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (!ai || !ai.IsAlerted || ai.IsDead) continue;
                if ((ai.transform.position - me).sqrMagnitude < r2) return true;
            }
            return false;
        }

        /// <summary>F: começa a cortar a árvore à frente.</summary>
        public bool Open()
        {
            Scan();
            if (!CanOpen) return false;
            Begin(Target);
            return true;
        }

        /// <summary>Esc: para de cortar.</summary>
        public void Close()
        {
            if (Working) Stop("");
        }

        void Begin(ResourceNode node)
        {
            Target = node;
            tool = ToolFor(node.Type);
            Working = true;
            Vector3 center = node.transform.position;
            Vector3 from = transform.position - center;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) from = -transform.forward;
            from.Normalize();
            facing = -from;
            slideTo = center + from * (node.Radius + standOff);
            strikePoint = center - facing * node.Radius + Vector3.up * 0.6f;
            if (node.Type == ResourceNode.Kind.Ore && FaceOf(node, out Vector3 face))
            {
                // Veio: de frente para o ponto da pedra que está na frente do capitão, a picareta no pé dela.
                slideTo = face + from * oreStandOff;
                strikePoint = face + Vector3.up * 0.15f;
            }
            slideTo.y = transform.position.y;
            startedAt = Time.time;
            nextImpact = Time.time + firstImpact;
            node.Hold(true);
            HideWeaponInHand();
            if (tool.visual) tool.visual.SetActive(true);
            if (animatorDriver) animatorDriver.PlayGather(tool.ground);
        }

        /// <summary>Ponto da face da pedra na linha do capitão até o centro dela, rente ao chão (raio só contra os colisores do nó).</summary>
        bool FaceOf(ResourceNode node, out Vector3 face)
        {
            face = default;
            Vector3 start = transform.position + Vector3.up * 0.3f;
            Vector3 target = node.transform.position;
            target.y = start.y;
            var ray = new Ray(start, (target - start).normalized);
            float best = float.MaxValue;
            foreach (var c in node.GetComponentsInChildren<Collider>())
                if (c.enabled && c.Raycast(ray, out RaycastHit hit, 12f) && hit.distance < best)
                {
                    best = hit.distance;
                    face = hit.point;
                    face.y = transform.position.y;
                }
            return best < float.MaxValue;
        }

        /// <summary>O golpe pega: na frente do tronco, na altura da mão (no veio, no pé da pedra).</summary>
        void Impact()
        {
            Vector3 point = strikePoint;
            int got = Target.Strike(transform.position, point);
            if (Target.Standing) return;
            int added = got > 0 && backpack ? backpack.Add(Target.Item, got) : 0;
            Gathered?.Invoke(this, Target.Item, added, got - added);
            End();
        }

        void Stop(string reason)
        {
            Stopped?.Invoke(this, reason);
            End();
        }

        void End()
        {
            Working = false;
            if (Target && Target.Standing) Target.Hold(false);
            if (animatorDriver) animatorDriver.SetGathering(false);
            HideTools();
            ShowWeapon();
            tool = null;
            nextScan = 0f;
        }

        void HideTools()
        {
            foreach (var t in tools) if (t?.visual) t.visual.SetActive(false);
        }

        void HideWeaponInHand()
        {
            if (!combat || combat.IsSwapping || hiddenWeapon.Count > 0) return;
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

        // ---------- Testes ----------

        /// <summary>Teste: começa a cortar uma árvore escolhida, sem conferir a frente (a ferramenta, a mochila e os piratas valem).</summary>
        public bool StartOn(ResourceNode node)
        {
            if (Working || !node || !node.Standing) return false;
            var t = ToolFor(node.Type);
            if (!Has(t) || (backpack && backpack.FreeSpace < 1f)) return false;
            Begin(node);
            return true;
        }
    }
}
