using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Navio do jogador com mais de um casco: o gerador monta um "rig" por classe (visual e canhões, ligados só no casco
    /// atual, mais colisor, massa, inércia e pontos de flutuação). Trocar de classe troca as peças no mesmo objeto, então
    /// tudo que aponta para o navio (câmera, HUD, save, missões) continua valendo. Navio novo do estaleiro começa com os
    /// upgrades no zero (eles rendem e custam conforme a classe). Quem guardou peças do navio ouve
    /// <see cref="ShipController.HullChanged"/>.
    /// </summary>
    [RequireComponent(typeof(ShipController), typeof(Health), typeof(Inventory))]
    public class PlayerShipClass : MonoBehaviour
    {
        [Serializable]
        public class Rig
        {
            public ShipClassDefinition shipClass;
            [Tooltip("Objetos deste casco (visual e canhões): ficam ligados só com ele.")]
            public GameObject[] parts = Array.Empty<GameObject>();
            [Tooltip("Eixos das velas deste casco (SailVisual).")]
            public Transform[] sails = Array.Empty<Transform>();
            public float mass = 6000f;
            public Vector3 inertia;
            public Vector3 colliderCenter, colliderSize;
            public Vector3[] floatPoints = Array.Empty<Vector3>();
            [Tooltip("Lanterna da popa (a mesma em todos os cascos) e onde ela fica neste (popa de cada tamanho).")]
            public Transform lantern;
            public Vector3 lanternPosition;
        }

        [Tooltip("Cascos do navio; o primeiro é o da cena.")]
        [SerializeField] Rig[] rigs = Array.Empty<Rig>();

        int current;
        CaptainFame fame;
        readonly List<ShipClassDefinition> classes = new();

        public ShipClassDefinition Current => rigs.Length > 0 ? rigs[current].shipClass : null;
        /// <summary>Todas as classes que este navio pode ter, na ordem dos rigs.</summary>
        public IReadOnlyList<ShipClassDefinition> Classes
        {
            get
            {
                if (classes.Count != rigs.Length)
                {
                    classes.Clear();
                    foreach (var rig in rigs) classes.Add(rig.shipClass);
                }
                return classes;
            }
        }

        public event Action<ShipClassDefinition> Changed;

        public void Configure(Rig[] shipRigs) => rigs = shipRigs;

        void Start()
        {
            // Beliches e rendimento dos upgrades da classe atual (o casco físico já nasce configurado pelo gerador).
            var shipClass = Current;
            if (!shipClass) return;
            if (TryGetComponent(out ShipCrew crew)) crew.SetBerths(shipClass.crewBerths);
            if (TryGetComponent(out ShipUpgrades upgrades)) upgrades.SetBase(shipClass);
            fame = CaptainFame.Current;
            if (fame) fame.PerksChanged += ApplyHold;
            ApplyHold();
        }

        void OnDestroy()
        {
            if (fame) fame.PerksChanged -= ApplyHold;
        }

        /// <summary>Porão da classe atual, com o bônus de patente "porão arrumado" (M10).</summary>
        void ApplyHold()
        {
            var shipClass = Current;
            if (shipClass) GetComponent<Inventory>().Configure(shipClass.holdCapacity * (1f + CaptainFame.Bonus(PerkEffect.HoldPercent)));
        }

        /// <summary>
        /// Troca o navio pela classe <paramref name="shipClass"/>. <paramref name="newShip"/>: saiu do estaleiro, com o casco
        /// cheio e os upgrades no zero (ao carregar o jogo, não: a vida e os níveis salvos vêm depois).
        /// </summary>
        public bool SetClass(ShipClassDefinition shipClass, bool newShip)
        {
            int index = Array.FindIndex(rigs, r => r.shipClass == shipClass);
            if (index < 0) return false;
            var health = GetComponent<Health>();
            if (newShip && index != current && TryGetComponent(out ShipUpgrades upgrades)) upgrades.ResetLevels();
            if (index != current)
            {
                current = index;
                ApplyRig(rigs[index]);
            }
            if (newShip) health.Heal(health.Max);
            return true;
        }

        void ApplyRig(Rig rig)
        {
            // Desliga os outros antes de ligar este: as peças de dois cascos nunca ficam ligadas juntas.
            foreach (var other in rigs)
                if (other != rig)
                    foreach (var part in other.parts)
                        if (part) part.SetActive(false);
            foreach (var part in rig.parts)
                if (part) part.SetActive(true);

            if (TryGetComponent(out BoxCollider box))
            {
                box.center = rig.colliderCenter;
                box.size = rig.colliderSize;
            }
            var body = GetComponent<Rigidbody>();
            body.mass = rig.mass;
            body.inertiaTensor = rig.inertia;
            body.inertiaTensorRotation = Quaternion.identity;
            GetComponent<Buoyancy>().SetFloatPoints(rig.floatPoints);

            if (rig.lantern) rig.lantern.localPosition = rig.lanternPosition;

            var ship = GetComponent<ShipController>();
            if (TryGetComponent(out SailVisual sails)) sails.Configure(ship, rig.sails);

            var def = rig.shipClass;
            ApplyHold();
            if (TryGetComponent(out ShipCrew crew)) crew.SetBerths(def.crewBerths);
            if (TryGetComponent(out ShipUpgrades upgrades)) upgrades.SetBase(def);
            else
            {
                if (def.stats) ship.Configure(def.stats);
                if (def.cannons)
                    foreach (var battery in GetComponentsInChildren<CannonBattery>()) battery.SetStats(def.cannons);
                GetComponent<Health>().SetMax(def.hullMax);
            }

            ship.NotifyHullChanged();
            Changed?.Invoke(def);
        }
    }
}
