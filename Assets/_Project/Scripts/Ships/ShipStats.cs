using System;
using UnityEngine;

namespace Pirates.Ships
{
    /// <summary>
    /// Stats de manobra de uma classe de navio. No M3 os upgrades aplicam
    /// modificadores sobre estes valores base.
    /// </summary>
    [CreateAssetMenu(menuName = "Pirates/Ships/Ship Stats", fileName = "ShipStats")]
    public class ShipStats : ScriptableObject
    {
        [Serializable]
        public struct SailLevel
        {
            public string label;
            [Range(-1f, 1f)] public float throttle;
        }

        [Header("Velas")]
        public SailLevel[] sailLevels =
        {
            new() { label = "Ré",          throttle = -0.15f },
            new() { label = "Recolhidas",  throttle = 0f },
            new() { label = "Meia vela",   throttle = 0.5f },
            new() { label = "Vela cheia",  throttle = 1f },
        };
        public int startingSailIndex = 1;

        [Header("Velocidade (m/s)")]
        public float maxSpeed = 11f;
        [Tooltip("Aceleração máxima ao ganhar velocidade.")]
        public float acceleration = 1.6f;
        [Tooltip("Desaceleração máxima ao perder velocidade.")]
        public float deceleration = 2.2f;
        [Tooltip("Quão rápido a velocidade converge para o alvo das velas.")]
        public float speedResponse = 0.8f;
        [Tooltip("Resistência lateral da quilha (evita derrapar).")]
        public float lateralGrip = 1.5f;

        [Header("Leme")]
        [Tooltip("Giro máximo em graus/s com leme todo e velocidade ideal.")]
        public float turnRate = 28f;
        public float turnResponse = 2.5f;
        [Tooltip("Segundos para o leme ir do centro ao batente.")]
        public float rudderResponseTime = 0.35f;
        [Tooltip("Eficiência do leme x fração da velocidade máxima. Parado quase não vira.")]
        public AnimationCurve steerageBySpeed = new(
            new Keyframe(0f, 0.25f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.8f));

        [Header("Estabilidade")]
        [Tooltip("Inclinação lateral nas curvas.")]
        public float heelStrength = 0.6f;
        public Vector3 centerOfMass = new(0f, -0.6f, 0f);
    }
}
