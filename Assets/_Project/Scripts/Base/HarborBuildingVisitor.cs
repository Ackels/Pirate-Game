using System;
using Pirates.Combat;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Base
{
    /// <summary>
    /// Fica no personagem a pé: perto da porta de uma casa de serviço do porto (HarborBuilding), F abre a parte do porto
    /// dela, com o navio atracado (HarborVisitor). Ao entrar, a câmera vira para a fachada (o personagem não fica escondido
    /// atrás da casa). Afastar-se, morrer ou o navio sair do porto fecha. O jogo não pausa.
    /// </summary>
    public class HarborBuildingVisitor : MonoBehaviour, IInteraction
    {
        [SerializeField] HarborVisitor ship;
        [Tooltip("Folga (m) antes de fechar sozinho ao se afastar da porta.")]
        [SerializeField] float closeMargin = 1.5f;

        Health health;

        public HarborVisitor Ship => ship;
        /// <summary>Porta mais próxima ao alcance (null se nenhuma).</summary>
        public HarborBuilding Nearby { get; private set; }
        /// <summary>Casa aberta (null se nenhuma).</summary>
        public HarborBuilding Current { get; private set; }
        public bool IsOpen => Current;
        public bool CanOpen => Nearby && ShipDocked && !IsDead;
        public bool ShipDocked => ship && ship.IsDocked;
        bool IsDead => health && health.IsDead;

        /// <summary>Entrou numa casa e saiu dela.</summary>
        public event Action<HarborBuilding> Entered, Left;

        public void Configure(HarborVisitor playerShip) => ship = playerShip;

        void Awake() => TryGetComponent(out health);

        // Embarcar desliga o personagem: sai da casa.
        void OnDisable() => Close();

        void Update()
        {
            Nearby = FindNearest();
            if (Current && (IsDead || !ShipDocked || Current.FlatDistance(transform.position) > Current.Range + closeMargin)) Close();
        }

        public bool Open()
        {
            if (!CanOpen) return false;
            Current = Nearby;
            // A entrada olha para fora da porta: a câmera olha para o lado oposto, de frente para a casa.
            if (Services.TryGet(out ICameraFollow cam)) cam.LookToward(Current.transform.eulerAngles.y + 180f);
            Entered?.Invoke(Current);
            return true;
        }

        public void Close()
        {
            var left = Current;
            Current = null;
            if (left) Left?.Invoke(left);
        }

        HarborBuilding FindNearest()
        {
            HarborBuilding best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < HarborBuilding.Active.Count; i++)
            {
                var building = HarborBuilding.Active[i];
                float d = building.FlatDistance(transform.position);
                if (d > building.Range || d > bestDistance) continue;
                best = building;
                bestDistance = d;
            }
            return best;
        }
    }
}
