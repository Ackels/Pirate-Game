using UnityEngine;

namespace Pirates.Core
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;
        /// <summary>Munição especial (M9): fração das velas rasgada por este acerto (navio fica mais lento).</summary>
        public float sailDamage;
        /// <summary>Munição especial (M9): fração da tripulação abatida por este acerto (canhões recarregam mais devagar).</summary>
        public float crewDamage;
    }

    /// <summary>Qualquer coisa que recebe dano: casco de navio, barril, inimigo em terra (M4).</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
