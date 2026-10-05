using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Caixote ou barril que quebra com golpe ou tiro (Health): some, voa em lascas e deixa no chão um saco de saque
    /// com o que a LootTable sortear (pode vir vazio). Restore() monta de novo (sandbox).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BreakableLoot : MonoBehaviour
    {
        static readonly List<BreakableLoot> active = new();
        public static IReadOnlyList<BreakableLoot> Active => active;

        [SerializeField] LootTable table;
        [SerializeField] LootSack sackPrefab;
        [Tooltip("Visual inteiro (some ao quebrar).")]
        [SerializeField] GameObject visual;
        [SerializeField] Material debrisMaterial;
        [SerializeField] int debrisPieces = 6;
        [SerializeField] float debrisLifetime = 2.5f;

        Health health;
        Collider[] colliders;

        public bool IsBroken => health && health.IsDead;

        public void Configure(LootTable lootTable, LootSack sack, GameObject whole, Material debris)
        {
            table = lootTable;
            sackPrefab = sack;
            visual = whole;
            debrisMaterial = debris;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            colliders = GetComponents<Collider>();
        }

        void OnEnable()
        {
            active.Add(this);
            health.Died += Break;
        }

        void OnDisable()
        {
            active.Remove(this);
            health.Died -= Break;
        }

        void Break()
        {
            if (visual) visual.SetActive(false);
            foreach (var c in colliders) c.enabled = false;
            SpawnDebris();
            var cargo = table ? table.RollCargo() : new List<ItemStack>();
            if (cargo.Count == 0 || !sackPrefab) return;
            var sack = Instantiate(sackPrefab, transform.position + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            sack.Setup(cargo);
        }

        /// <summary>Lascas com física que voam e somem (arte provisória até o M6).</summary>
        void SpawnDebris()
        {
            Vector3 center = transform.position + Vector3.up * 0.4f;
            for (int i = 0; i < debrisPieces; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Lasca";
                piece.transform.SetPositionAndRotation(center + Random.insideUnitSphere * 0.25f, Random.rotation);
                piece.transform.localScale = new Vector3(Random.Range(0.08f, 0.3f), 0.04f, Random.Range(0.06f, 0.14f));
                if (debrisMaterial) piece.GetComponent<Renderer>().sharedMaterial = debrisMaterial;
                var body = piece.AddComponent<Rigidbody>();
                body.mass = 0.2f;
                body.AddForce((Random.insideUnitSphere + Vector3.up * 1.2f) * 2.5f, ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
                Destroy(piece, debrisLifetime);
            }
        }

        /// <summary>Monta de novo, com a vida cheia.</summary>
        public void Restore()
        {
            health.Revive();
            if (visual) visual.SetActive(true);
            foreach (var c in colliders) c.enabled = true;
        }
    }
}
