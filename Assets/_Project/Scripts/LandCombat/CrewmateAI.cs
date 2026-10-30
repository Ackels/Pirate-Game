using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.LandCombat
{
    /// <summary>Ordem da roda de comando (30/10, pedido do Jhonata) para os marujos em terra.</summary>
    public enum CrewOrder
    {
        /// <summary>Atrás do capitão; brigam com quem estiver em alerta por perto (o jeito do D3).</summary>
        Follow,
        /// <summary>Param onde estão e só lutam com quem chegar perto do lugar.</summary>
        Hold,
        /// <summary>Vão até o ponto apontado e esperam lá, como o Hold.</summary>
        MoveTo,
        /// <summary>Vão no pirata apontado até ele cair; depois voltam à ordem de antes.</summary>
        Attack,
        /// <summary>Colados no capitão; só batem em quem chega nele.</summary>
        Guard,
        /// <summary>Seguem sem atacar (só se defendem de quem bate neles).</summary>
        Passive,
    }

    /// <summary>
    /// Marujo que desce com o capitão (D3, 30/10; pedido do Jhonata: "dois marujos lutando junto a pé"): segue o capitão um pouco atrás, de
    /// um lado, e, com pirata em alerta por perto, vai até ele e golpeia com o cutelo. Os piratas também brigam com ele (quem estiver mais
    /// perto, o capitão ou um marujo: <see cref="EnemyFootAI"/>). Derrubado, fica caído e levanta ferido quando a briga acaba (ou depois de
    /// um tempo): não se perde tripulante. Quem o põe em terra e tira de volta é o CrewEscort (Controls). Anda pelo NavMesh, como os piratas.
    /// O capitão muda o jeito pela roda de comando (<see cref="CrewOrder"/>, <see cref="Command"/>); ao descer, volta a seguir.
    /// </summary>
    public class CrewmateAI : MonoBehaviour, IFootInput
    {
        public static readonly List<CrewmateAI> Active = new();

        [SerializeField] Transform captain;
        [Tooltip("0 = atrás à esquerda do capitão, 1 = à direita.")]
        [SerializeField] int slot;
        [Header("Seguir")]
        [SerializeField] float followBehind = 1.8f, followSide = 1.5f, followSlack = 1.2f, runBeyond = 4.5f, teleportBeyond = 35f;
        [Header("Lutar")]
        [Tooltip("Pirata em alerta a até isto do marujo (e o capitão a até leash dele) vira alvo.")]
        [SerializeField] float sight = 16f, leash = 25f, attackRange = 1.9f;
        [SerializeField] Vector2 swingPause = new(0.5f, 1.1f);
        [Header("Caído")]
        [Tooltip("Levanta depois disto caído com a briga acabada (nenhum pirata em alerta a calmRadius m), ou depois de maxDown de qualquer jeito.")]
        [SerializeField] float downCalm = 4f, maxDown = 25f, calmRadius = 15f;
        [SerializeField, Range(0.1f, 1f)] float reviveHealth = 0.5f;
        [SerializeField] float separation = 1.3f;
        [Header("Ordens (roda de comando)")]
        [Tooltip("Ficar/ir até lá: luta com pirata a até isto do lugar. Proteger: a até isto do capitão.")]
        [SerializeField] float holdRadius = 9f, guardRadius = 6f;
        [SerializeField] float guardBehind = 1.1f, guardSide = 1.1f, postSlack = 0.7f, postSide = 0.9f;

        FootCharacter character;
        CharacterCombat combat;
        Health health;
        FootHealthReactions reactions;
        EnemyFootAI foe;
        NavMeshPath path;
        Vector3[] corners = Array.Empty<Vector3>();
        int corner;
        Vector3 pathGoal;
        float nextRepath, nextPick, nextSwing, downSince = -1f, calmSince = -1f;
        CrewOrder order, afterAttack;
        Vector3 post;
        EnemyFootAI target;

        public bool IsDown => health && health.IsDead;
        public EnemyFootAI Foe => foe;
        public CrewOrder Order => order;
        /// <summary>Onde espera (ficar / ir até lá).</summary>
        public Vector3 Post => post;
        /// <summary>O pirata da ordem "atacar alvo".</summary>
        public EnemyFootAI Target => target;
        public Health Health => health;
        public int Slot => slot;

        /// <summary>Caiu na briga (levanta depois).</summary>
        public static event Action<CrewmateAI> Downed;
        /// <summary>Levantou ferido.</summary>
        public static event Action<CrewmateAI> Revived;

        public void Configure(Transform captainTransform, int side)
        {
            captain = captainTransform;
            slot = side;
        }

        void Awake()
        {
            character = GetComponent<FootCharacter>();
            combat = GetComponent<CharacterCombat>();
            health = GetComponent<Health>();
            reactions = GetComponent<FootHealthReactions>();
            path = new NavMeshPath();
        }

        void OnEnable()
        {
            Active.Add(this);
            health.Died += OnDied;
        }

        void OnDisable()
        {
            Active.Remove(this);
            health.Died -= OnDied;
            foe = null;
        }

        void OnDied()
        {
            downSince = Time.time;
            calmSince = -1f;
            foe = null;
            Downed?.Invoke(this);
        }

        /// <summary>Desce com o capitão: inteiro, no ponto dado (já no chão), virado para onde ele olha.</summary>
        public void Arrive(Vector3 at, Quaternion rotation)
        {
            if (health.IsDead && reactions) reactions.Revive();
            health.SetCurrent(health.Max);
            downSince = -1f;
            foe = target = null;
            order = afterAttack = CrewOrder.Follow;
            corners = Array.Empty<Vector3>();
            character.Teleport(at, rotation);
        }

        /// <summary>
        /// Ordem da roda de comando. <paramref name="point"/> = para onde ir (MoveTo); <paramref name="enemy"/> = em quem bater (Attack).
        /// Ficar = espera onde está agora. Atacar sem alvo vivo não muda nada.
        /// </summary>
        public void Command(CrewOrder newOrder, Vector3 point = default, EnemyFootAI enemy = null)
        {
            switch (newOrder)
            {
                case CrewOrder.Attack:
                    if (!enemy || enemy.IsDead) return;
                    if (order != CrewOrder.Attack) afterAttack = order;
                    target = enemy;
                    break;
                case CrewOrder.Hold:
                    post = OnNavMesh(transform.position);
                    break;
                case CrewOrder.MoveTo:
                    // Lado a lado no ponto, de frente para quem vem do capitão.
                    Vector3 dir = captain ? Flat(point - captain.position) : Vector3.forward;
                    dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    post = OnNavMesh(point + side * (slot == 0 ? -postSide : postSide));
                    break;
            }
            if (newOrder != CrewOrder.Attack) target = null;
            order = newOrder;
            foe = newOrder == CrewOrder.Attack ? target : null;
            nextPick = 0f;
        }

        static Vector3 OnNavMesh(Vector3 point) =>
            NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : point;

        public FootCommand GetCommand()
        {
            var command = default(FootCommand);
            if (!captain) return command;
            if (health.IsDead) return command;
            if (order == CrewOrder.Attack)
            {
                if (target && !target.IsDead && target.isActiveAndEnabled)
                {
                    foe = target;
                    Fight(ref command);
                    return command;
                }
                // O alvo caiu: volta ao que fazia antes.
                order = afterAttack;
                target = null;
                nextPick = 0f;
            }
            if (Time.time >= nextPick)
            {
                nextPick = Time.time + 0.4f;
                foe = PickFoe();
            }
            if (foe) Fight(ref command);
            else Idle(ref command);
            return command;
        }

        void Idle(ref FootCommand command)
        {
            switch (order)
            {
                case CrewOrder.Hold:
                case CrewOrder.MoveTo:
                    GoTo(ref command, post);
                    break;
                case CrewOrder.Guard:
                    Follow(ref command, guardBehind, guardSide);
                    break;
                default:
                    Follow(ref command, followBehind, followSide);
                    break;
            }
        }

        void GoTo(ref FootCommand command, Vector3 goal)
        {
            float toGoal = Flat(goal - transform.position).magnitude;
            if (toGoal < postSlack)
            {
                Move(ref command, Vector3.zero, false);
                return;
            }
            Move(ref command, Steer(goal), toGoal > 2.5f);
        }

        /// <summary>Caído o personagem não pede comando: a conta de levantar fica aqui.</summary>
        void Update()
        {
            if (health.IsDead) UpdateDown();
        }

        // ---------- Seguir ----------

        void Follow(ref FootCommand command, float behind, float sideways)
        {
            Vector3 back = Flat(captain.forward);
            if (back.sqrMagnitude < 0.01f) back = Vector3.forward;
            back.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, back);
            Vector3 goal = captain.position - back * behind + side * (slot == 0 ? -sideways : sideways);
            float toCaptain = Flat(captain.position - transform.position).magnitude;
            if (toCaptain > teleportBeyond)
            {
                // Ficou muito para trás (o capitão rolou morro abaixo, foi carregado de volta à praia): aparece perto dele.
                if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 4f, NavMesh.AllAreas)) character.Teleport(hit.position, captain.rotation);
                return;
            }
            float toGoal = Flat(goal - transform.position).magnitude;
            if (toGoal < followSlack)
            {
                Move(ref command, Vector3.zero, false);
                return;
            }
            Move(ref command, Steer(goal), toGoal > runBeyond || toCaptain > runBeyond + 2f);
        }

        // ---------- Lutar ----------

        /// <summary>
        /// O pirata em alerta (ou colado) mais perto, a até <see cref="sight"/> do marujo e perto da âncora da ordem: seguir = a até
        /// <see cref="leash"/> do capitão, proteger = a até <see cref="guardRadius"/> dele, ficar/ir até lá = a até <see cref="holdRadius"/>
        /// do lugar. Quem bate no marujo colado nele (2 m) vale sempre; sem lutar, só esse.
        /// </summary>
        EnemyFootAI PickFoe()
        {
            EnemyFootAI best = null;
            float bestD = sight * sight;
            Vector3 me = transform.position;
            Vector3 anchor = order is CrewOrder.Hold or CrewOrder.MoveTo ? post : captain.position;
            float reach = order switch
            {
                CrewOrder.Hold or CrewOrder.MoveTo => holdRadius,
                CrewOrder.Guard => guardRadius,
                _ => leash,
            };
            var list = EnemyFootAI.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (!ai || ai.IsDead || !ai.isActiveAndEnabled) continue;
                float d = Flat(ai.transform.position - me).sqrMagnitude;
                bool onMe = d < 4f && ai.IsAlerted && ai.Foe == this;
                if (order == CrewOrder.Passive && !onMe) continue;
                if (!ai.IsAlerted && d > 9f) continue;
                if (!onMe && Flat(ai.transform.position - anchor).sqrMagnitude > reach * reach) continue;
                // Fica no que já escolheu, se ainda vale, a não ser que outro esteja bem mais perto.
                if (ai == foe) d -= 4f;
                if (d < bestD)
                {
                    bestD = d;
                    best = ai;
                }
            }
            return best;
        }

        void Fight(ref FootCommand command)
        {
            Vector3 at = foe.transform.position;
            float distance = Flat(at - transform.position).magnitude;
            command.hasAim = true;
            command.aimPoint = at + Vector3.up;
            command.faceAimPoint = true;
            if (distance > attackRange)
            {
                Move(ref command, Steer(at), distance > 3f);
                return;
            }
            Move(ref command, Vector3.zero, false);
            if (Time.time < nextSwing || !combat.ReadyToAttack) return;
            command.attack = true;
            nextSwing = Time.time + UnityEngine.Random.Range(swingPause.x, swingPause.y);
        }

        // ---------- Caído ----------

        void UpdateDown()
        {
            if (downSince < 0f) downSince = Time.time;
            bool calm = !AlertedNear(transform.position, calmRadius);
            if (!calm) calmSince = -1f;
            else if (calmSince < 0f) calmSince = Time.time;
            bool ready = (calm && Time.time - calmSince >= downCalm && Time.time - downSince >= 2f) || Time.time - downSince >= maxDown;
            if (!ready) return;
            if (reactions) reactions.Revive();
            health.SetCurrent(health.Max * reviveHealth);
            downSince = -1f;
            Revived?.Invoke(this);
        }

        static bool AlertedNear(Vector3 point, float radius)
        {
            var list = EnemyFootAI.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (ai && !ai.IsDead && ai.IsAlerted && Flat(ai.transform.position - point).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        // ---------- Andar ----------

        void Move(ref FootCommand command, Vector3 direction, bool run)
        {
            direction += Separation();
            direction = Vector3.ClampMagnitude(direction, 1f);
            command.move = new Vector2(direction.x, direction.z);
            command.run = run;
        }

        /// <summary>Empurrão para longe do capitão e do outro marujo, muito perto.</summary>
        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            void Away(Vector3 from)
            {
                Vector3 a = Flat(transform.position - from);
                float d = a.magnitude;
                if (d < 0.001f || d > separation) return;
                push += a / d * (1f - d / separation);
            }
            if (captain) Away(captain.position);
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != this && !Active[i].IsDown) Away(Active[i].transform.position);
            return push * 0.8f;
        }

        Vector3 Steer(Vector3 goal)
        {
            if (Time.time >= nextRepath || (goal - pathGoal).sqrMagnitude > 1f) Repath(goal);
            while (corner < corners.Length && Flat(corners[corner] - transform.position).magnitude < 0.5f) corner++;
            Vector3 next = corner < corners.Length ? corners[corner] : goal;
            Vector3 dir = Flat(next - transform.position);
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
        }

        void Repath(Vector3 goal)
        {
            nextRepath = Time.time + 0.5f;
            pathGoal = goal;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit from, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(goal, out NavMeshHit to, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                && path.status != NavMeshPathStatus.PathInvalid)
            {
                corners = path.corners;
                corner = corners.Length > 1 ? 1 : 0;
            }
            else
            {
                corners = Array.Empty<Vector3>();
                corner = 0;
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
