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
        /// <summary>Fogo de pólvora (tiro, bala de canhão, explosão): acende barril de pólvora. Golpe de cutelo não acende.</summary>
        public bool ignites;
        /// <summary>Derruba o personagem a pé: <see cref="Knockdown.Stumble"/> (chute: cambaleia e cai sentado) ou <see cref="Knockdown.Blast"/> (explosão: arremessado de costas).</summary>
        public Knockdown knockdown;
        /// <summary>Golpe que pegou na guarda (cutelo erguido): só o arranhão, sem tranco nem reação de golpe.</summary>
        public bool blocked;
        /// <summary>Lâmina sagrada (Lâmina de São Telmo, C5): o morto amaldiçoado que ela derruba não levanta de novo.</summary>
        public bool holy;
    }

    /// <summary>Como o golpe derruba quem está a pé (pacote A, 09/10).</summary>
    public enum Knockdown : byte
    {
        None,
        Stumble,
        Blast,
    }

    /// <summary>Qualquer coisa que recebe dano: casco de navio, barril, inimigo em terra (M4).</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }

    /// <summary>Pode ser empurrado por uma explosão (personagem a pé): velocidade somada à dele, que some aos poucos.</summary>
    /// <summary>Alvo que não é personagem mas puxa o golpe do capitão (bonecos de treino).</summary>
    public interface IAssistTarget { }

    public interface IPushable
    {
        void Push(Vector3 velocity);
    }
}
