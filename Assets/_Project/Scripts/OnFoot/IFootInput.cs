using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>Comando de um frame para o personagem a pé: vem do jogador (PlayerFootInput) ou da IA de terra.</summary>
    public struct FootCommand
    {
        /// <summary>Direção pedida no mundo (x = X do mundo, y = Z do mundo). Magnitude 0–1.</summary>
        public Vector2 move;
        public bool run;
        public bool jump;
        /// <summary>Rolar (esquiva) neste frame, na direção de <see cref="move"/> (parado: para a frente).</summary>
        public bool dodge;
        /// <summary>Chutar neste frame (derruba quem está na frente).</summary>
        public bool kick;
        /// <summary>Atacar com a arma atual neste frame.</summary>
        public bool attack;
        /// <summary>Segurando mirar (pistola): fica em pose de mira seguindo o ponto de mira, e o ataque atira na hora.</summary>
        public bool aim;
        /// <summary>Trocar para a arma N (1 = primeira, 2 = segunda...). 0 = não trocar.</summary>
        public int selectWeapon;
        /// <summary>Próxima arma (gamepad).</summary>
        public bool nextWeapon;
        /// <summary>Ponto do mundo para onde mirar (cursor no chão; o alvo, na IA). Sem mira (gamepad), ataca para a frente.</summary>
        public bool hasAim;
        public Vector3 aimPoint;
        /// <summary>Fica virado para o ponto de mira mesmo andando (IA encarando o jogador enquanto espera a vez).</summary>
        public bool faceAimPoint;
    }

    public interface IFootInput
    {
        FootCommand GetCommand();
    }
}
