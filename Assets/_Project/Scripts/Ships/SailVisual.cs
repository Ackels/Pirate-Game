using System;
using UnityEngine;

namespace Pirates.Ships
{
    /// <summary>
    /// As velas do modelo seguem o nível de vela do ShipController: recolhidas ficam enroladas no alto da verga, cheias
    /// ficam abertas, e trocar o nível sobe ou baixa o pano aos poucos. Cada vela é um eixo no topo dela (o gerador
    /// cria) que encolhe na vertical. Só visual: a velocidade continua vindo do ShipController.
    /// </summary>
    public class SailVisual : MonoBehaviour
    {
        [SerializeField] ShipController ship;
        [Tooltip("Eixos no topo de cada vela (encolhem em Y).")]
        [SerializeField] Transform[] sails = Array.Empty<Transform>();
        [Tooltip("Altura da vela recolhida (fração da aberta).")]
        [SerializeField, Range(0.02f, 1f)] float furled = 0.12f;
        [Tooltip("Quanto da vela abre ou fecha por segundo.")]
        [SerializeField, Min(0.05f)] float speed = 0.8f;

        float open = -1f;

        public void Configure(ShipController controller, Transform[] sailPivots)
        {
            ship = controller;
            sails = sailPivots;
        }

        void Update()
        {
            if (!ship || !ship.Stats) return;
            float max = 0f;
            foreach (var level in ship.Stats.sailLevels) max = Mathf.Max(max, Mathf.Abs(level.throttle));
            // Ré também abre um pouco de pano (a ré é vela contra o vento).
            float target = max > 0f ? Mathf.Clamp01(Mathf.Abs(ship.Throttle) / max) : 1f;
            open = open < 0f ? target : Mathf.MoveTowards(open, target, speed * Time.deltaTime);
            float height = Mathf.Lerp(furled, 1f, open);
            foreach (var sail in sails)
                if (sail) sail.localScale = new Vector3(1f, height, 1f);
        }
    }
}
