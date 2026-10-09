using System;
using Pirates.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    /// <summary>
    /// Caçadores de recompensas (inimigo novo, passo 3 de 09/10): com a fama do capitão (patente do <see cref="ICaptainRank"/>) vem a
    /// cabeça a prêmio. Depois de um tempo navegando fora das zonas seguras, um navio caçador aparece longe, atrás da câmera, e vem direto
    /// atrás do jogador por todo o mapa (<see cref="EnemyShipAI.Hunt"/>), com a barra de chefe no topo (<see cref="ShipBoss"/>). A força
    /// segue a patente (<see cref="Tier"/>: casco, canhões, fama); na patente máxima passa do galeão Tormenta. Um de cada vez; se o
    /// jogador ficar no porto (ou ancorado, a pé) ou sumir longe por um tempo, o caçador desiste e vai embora. Afundado, solta saque e
    /// fama como os outros navios. Nada é salvo: o relógio recomeça ao carregar o jogo.
    /// </summary>
    public class BountyHunters : MonoBehaviour
    {
        [Serializable]
        public class Tier
        {
            [Tooltip("Patente mínima do capitão (0 = Grumete).")]
            public int minRank = 2;
            public string shipLabel = "Caçador de recompensas";
            [Min(1f)] public float hull = 320f;
            public CannonStats cannons;
            [Min(0)] public int fame = 20;
            [Tooltip("Pontaria: erro da antecipação e do alcance (EnemyShipAI.ConfigureGunnery) e tempo de reação (s).")]
            [Range(0f, 0.6f)] public float leadError = 0.15f;
            [Range(0f, 0.3f)] public float rangeError = 0.04f;
            public Vector2 reaction = new(0.15f, 0.4f);
        }

        [Tooltip("Molde do caçador (desligado, fora do mapa); cada caçador é uma cópia dele.")]
        [SerializeField] GameObject template;
        [SerializeField] Transform player;
        [Tooltip("Em ordem de patente: vale a última cuja patente mínima o capitão já tem.")]
        [SerializeField] Tier[] tiers = Array.Empty<Tier>();
        [SerializeField] string[] captains = Array.Empty<string>();
        [Tooltip("Segundos navegando fora das zonas seguras até o primeiro caçador e entre um e outro (mínimo, máximo).")]
        [SerializeField] float firstDelaySeconds = 420f;
        [SerializeField] Vector2 intervalSeconds = new(420f, 720f);
        [Tooltip("Distância do jogador onde o caçador aparece (m).")]
        [SerializeField] float spawnDistance = 230f;
        [Tooltip("Jogador no porto/ancorado ou mais longe que giveUpDistance por este tempo: o caçador desiste e some.")]
        [SerializeField] float giveUpSeconds = 90f;
        [SerializeField] float giveUpDistance = 600f;

        /// <summary>Um caçador acabou de aparecer (o HUD avisa).</summary>
        public static event Action<ShipBoss> Arrived;

        float seaTime, nextAt, lostFor;
        GameObject hunter;
        Health hunterHealth;
        Camera cam;

        public bool HasHunter => hunter;

        public void Configure(GameObject mold, Transform target, Tier[] hunterTiers, string[] captainNames)
        {
            template = mold;
            player = target;
            tiers = hunterTiers;
            captains = captainNames;
        }

        void Start() => nextAt = firstDelaySeconds;

        void Update()
        {
            if (!template || !player) return;
            if (hunter)
            {
                Watch();
                return;
            }
            var tier = CurrentTier();
            if (tier == null || SafeZone.At(player.position)) return;
            seaTime += Time.deltaTime;
            if (seaTime >= nextAt) Spawn(tier);
        }

        /// <summary>Sandbox (F1): chama um caçador agora (na patente atual; abaixo da primeira, o mais fraco).</summary>
        public bool SpawnNow()
        {
            if (!template || !player || hunter || tiers.Length == 0) return false;
            Spawn(CurrentTier() ?? tiers[0]);
            return hunter;
        }

        Tier CurrentTier()
        {
            if (!Services.TryGet(out ICaptainRank rank)) return null;
            Tier found = null;
            foreach (var t in tiers)
                if (t != null && rank.Rank >= t.minRank) found = t;
            return found;
        }

        void Spawn(Tier tier)
        {
            seaTime = 0f;
            nextAt = Random.Range(intervalSeconds.x, intervalSeconds.y);
            lostFor = 0f;
            var point = SpawnPoint();
            var look = player.position - point;
            look.y = 0f;
            point.y = template.transform.position.y;
            hunter = Instantiate(template, point, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            hunter.name = template.name.Replace("_Molde", "");
            hunter.SetActive(true);

            hunterHealth = hunter.GetComponent<Health>();
            hunterHealth.SetMax(tier.hull);
            hunterHealth.SetCurrent(tier.hull);
            if (tier.cannons)
                foreach (var battery in hunter.GetComponentsInChildren<CannonBattery>(true)) battery.SetStats(tier.cannons);
            var ai = hunter.GetComponent<EnemyShipAI>();
            ai.SetFame(tier.fame);
            ai.ConfigureGunnery(tier.rangeError, tier.reaction, 18f, 0f, tier.leadError);
            ai.Hunt(giveUpDistance * 4f);
            string captain = captains.Length > 0 ? captains[Random.Range(0, captains.Length)] : "";
            var boss = hunter.GetComponent<ShipBoss>();
            if (boss)
            {
                boss.Configure(tier.shipLabel, captain, null, 0, 0f);
                Arrived?.Invoke(boss);
            }
        }

        /// <summary>Longe, de preferência atrás da câmera (o caçador chega "de fora da tela"), em água aberta.</summary>
        Vector3 SpawnPoint()
        {
            if (!cam) cam = Camera.main;
            var back = cam ? -cam.transform.forward : -player.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
            var fallback = player.position + back * spawnDistance;
            for (int step = 0; step < 18; step++)
            {
                float angle = (step + 1) / 2 * 20f * (step % 2 == 0 ? 1f : -1f);
                var p = player.position + Quaternion.AngleAxis(angle, Vector3.up) * back * spawnDistance;
                if (!Land(p)) return p;
            }
            return fallback;
        }

        /// <summary>Chão (ilha, raso) ou algo com colisor num raio de 15 m.</summary>
        static bool Land(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out var hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.point.y > -4f) return true;
            return Physics.CheckSphere(new Vector3(p.x, 0f, p.z), 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void Watch()
        {
            // Afundado: o SinkOnDeath cuida do resto (saque, fama, sumir).
            if (!hunterHealth || hunterHealth.IsDead)
            {
                hunter = null;
                return;
            }
            bool lost = SafeZone.At(player.position) || (hunter.transform.position - player.position).sqrMagnitude > giveUpDistance * giveUpDistance;
            lostFor = lost ? lostFor + Time.deltaTime : 0f;
            if (lostFor < giveUpSeconds) return;
            Destroy(hunter);
            hunter = null;
        }
    }
}
