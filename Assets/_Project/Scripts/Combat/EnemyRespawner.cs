using System.Collections;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Protótipo: quando o inimigo termina de afundar, outro igual aparece depois de um tempo, no ponto
    /// de surgimento mais longe do jogador. Dá para juntar recursos para os upgrades. Com targetId (o brigue), aceitar
    /// a missão dele traz o navio na hora, se estava esperando para voltar (como o acampamento, que se repovoa).
    /// </summary>
    public class EnemyRespawner : MonoBehaviour
    {
        [Tooltip("Inimigo inicial (já na cena).")]
        [SerializeField] SinkOnDeath firstEnemy;
        [Tooltip("Cópia desativada do inimigo, gerada pelo menu de protótipo; é clonada a cada reaparição.")]
        [SerializeField] GameObject template;
        [SerializeField] Transform player;
        [SerializeField] Vector3[] spawnPoints = { new(-50f, 0.3f, 230f) };
        [Tooltip("Segundos entre o inimigo sumir no fundo e o próximo aparecer.")]
        [SerializeField] float respawnDelay = 25f;
        [Tooltip("Id do navio nas missões: a missão com este alvo traz o navio de volta na hora. Vazio = só pelo tempo.")]
        [SerializeField] string targetId;

        int spawned = 1;
        Coroutine pending;

        /// <summary>Afundou e está esperando o tempo para voltar.</summary>
        public bool IsWaiting => pending != null;

        public void Configure(SinkOnDeath first, GameObject inactiveTemplate, Transform playerShip, Vector3[] points, float delay,
            string missionTargetId = null)
        {
            firstEnemy = first;
            template = inactiveTemplate;
            player = playerShip;
            spawnPoints = points;
            respawnDelay = delay;
            targetId = missionTargetId;
        }

        void Start()
        {
            if (firstEnemy) Watch(firstEnemy);
        }

        void OnEnable() => EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
        void OnDisable() => EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);

        void OnMissionStarted(MissionStartedEvent evt)
        {
            if (string.IsNullOrEmpty(targetId) || evt.targetId != targetId || pending == null) return;
            StopCoroutine(pending);
            pending = null;
            Spawn();
        }

        void Watch(SinkOnDeath enemy) => enemy.Sunk += OnSunk;

        void OnSunk(SinkOnDeath enemy)
        {
            enemy.Sunk -= OnSunk;
            pending = StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            pending = null;
            Spawn();
        }

        void Spawn()
        {
            if (!template || spawnPoints.Length == 0) return;

            Vector3 point = Farthest();
            Vector3 toPlayer = player ? player.position - point : Vector3.back;
            toPlayer.y = 0f;
            var rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            var go = Instantiate(template, point, rotation);
            go.name = $"{template.name}_{++spawned}";
            go.SetActive(true);
            if (go.TryGetComponent(out SinkOnDeath sink)) Watch(sink);
        }

        Vector3 Farthest()
        {
            if (!player) return spawnPoints[0];
            Vector3 best = spawnPoints[0];
            float bestDistance = -1f;
            foreach (var p in spawnPoints)
            {
                float d = (p - player.position).sqrMagnitude;
                if (d <= bestDistance) continue;
                best = p;
                bestDistance = d;
            }
            return best;
        }
    }
}
