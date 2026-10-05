using System.Collections.Generic;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Forte inimigo (Ilha Norte): as balas de navio que acertam muralhas e torre (colisores filhos) vão para a Health do
    /// forte (num filho, para ela não ser achada pelos colisores: só este filtro recebe o dano). Cutelo e pistola não
    /// arranham pedra: só conta dano vindo de navio. Zerou,
    /// o forte está <b>calado</b> — os canhões (FortressCannon filhos) param, a bandeira cai, sobe fumaça e fogo, e sai o
    /// CampClearedEvent com o <c>fortId</c> (missões "ClearCamp" com esse alvo contam). Missão nova contra ele com o forte
    /// já calado: ele volta inteiro.
    /// </summary>
    public class EnemyFort : MonoBehaviour, IDamageable
    {
        static readonly List<EnemyFort> active = new();
        public static IReadOnlyList<EnemyFort> Active => active;

        [SerializeField] string displayName = "Forte";
        [Tooltip("Vida do forte (num filho sem colisores).")]
        [SerializeField] Health health;
        [Tooltip("Id para as missões: mesmo texto do targetId da missão.")]
        [SerializeField] string fortId = "forte";
        [Tooltip("Fama do capitão ao calar o forte (M10).")]
        [SerializeField, Min(0)] int fame = 40;
        [Tooltip("Ligados enquanto o forte resiste (bandeira).")]
        [SerializeField] GameObject[] standing = System.Array.Empty<GameObject>();
        [Tooltip("Ligados quando o forte é calado (fumaça, fogo).")]
        [SerializeField] GameObject[] ruined = System.Array.Empty<GameObject>();

        FortressCannon[] cannons = System.Array.Empty<FortressCannon>();

        public string DisplayName => displayName;
        public string FortId => fortId;
        public Health Health => health;
        public bool IsSilenced => health && health.IsDead;

        /// <summary>Algum canhão do forte está mirando no jogador agora.</summary>
        public bool Engaging
        {
            get
            {
                if (IsSilenced) return false;
                for (int i = 0; i < cannons.Length; i++)
                    if (cannons[i] && cannons[i].enabled && cannons[i].Aim) return true;
                return false;
            }
        }

        public void Configure(string name, string id, Health fortHealth, GameObject[] standingObjects, GameObject[] ruinedObjects)
        {
            health = fortHealth;
            displayName = name;
            fortId = id;
            standing = standingObjects;
            ruined = ruinedObjects;
        }

        void Awake()
        {
            cannons = GetComponentsInChildren<FortressCannon>(true);
            Show(false);
        }

        void OnEnable()
        {
            active.Add(this);
            health.Died += OnDied;
            EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
        }

        void OnDisable()
        {
            active.Remove(this);
            health.Died -= OnDied;
            EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>Bala de canhão de navio: passa para a vida do forte. Golpes e tiros a pé não contam.</summary>
        public void TakeDamage(in DamageInfo info)
        {
            if (info.source && info.source.TryGetComponent(out ShipController _)) health.TakeDamage(info);
        }

        void OnDied()
        {
            Show(true);
            EventBus.Publish(new CampClearedEvent { campId = fortId, fame = fame });
        }

        void OnMissionStarted(MissionStartedEvent evt)
        {
            if (evt.targetId == fortId && IsSilenced) Restore();
        }

        /// <summary>Forte inteiro de novo (missão aceita outra vez, sandbox).</summary>
        public void Restore()
        {
            health.Revive();
            Show(false);
        }

        /// <summary>Sandbox: cala o forte na hora.</summary>
        public void Silence()
        {
            if (!IsSilenced) health.TakeDamage(new DamageInfo { amount = health.Max * 10f, point = transform.position, source = gameObject });
        }

        void Show(bool silenced)
        {
            foreach (var c in cannons) if (c) c.enabled = !silenced;
            foreach (var go in standing) if (go) go.SetActive(!silenced);
            foreach (var go in ruined) if (go) go.SetActive(silenced);
        }
    }
}
