using System.Collections;
using Pirates.Combat;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Quando o navio é destruído, sorteia a carga pela LootTable e solta um campo de destroços
    /// que sobe à tona onde ele afundou.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] LootTable table;
        [Tooltip("Segundos entre o golpe final e os destroços se soltarem do casco.")]
        [SerializeField] float dropDelay = 1.5f;
        [Tooltip("Os destroços nascem abaixo da superfície e sobem boiando (m).")]
        [SerializeField] float spawnDepth = 1.5f;

        const string WoodItemName = "Item_Madeira";

        Health health;
        bool dropped;

        public LootTable Table => table;

        public void Configure(LootTable lootTable) => table = lootTable;

        /// <summary>
        /// Já saqueado na abordagem (04/11, Jhonata: abordar, pegar o saque e depois afundar dava o saque duas vezes): afundado, solta só a
        /// madeira do casco quebrado, sem baú. Uma cópia nova do navio (respawn) nasce sem isto.
        /// </summary>
        public bool Plundered { get; private set; }
        public void MarkPlundered() => Plundered = true;

        void Awake() => health = GetComponent<Health>();
        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied()
        {
            if (dropped || !table || !table.wreckPrefab) return;
            dropped = true;
            // O SinkOnDeath desativa o navio só depois de ~7 s; a soltura acontece bem antes.
            StartCoroutine(Drop());
        }

        IEnumerator Drop()
        {
            yield return new WaitForSeconds(dropDelay);

            Vector3 pos = transform.position;
            float water = Services.TryGet(out IOceanSampler ocean) ? ocean.GetHeight(pos) : 0f;
            pos.y = water - spawnDepth;
            var wreck = Instantiate(table.wreckPrefab, pos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            wreck.name = "Destrocos_" + name;
            var cargo = table.RollCargo();
            if (Plundered) cargo.RemoveAll(s => !s.item || s.item.name != WoodItemName);
            wreck.Setup(cargo, !Plundered && table.RollChest() ? table : null, table.lifetime);

            // Não bater no casco que está afundando (senão os destroços ficam presos embaixo dele).
            var hullColliders = GetComponentsInChildren<Collider>();
            foreach (var own in wreck.GetComponentsInChildren<Collider>())
            foreach (var hull in hullColliders)
                Physics.IgnoreCollision(own, hull);
        }
    }
}
