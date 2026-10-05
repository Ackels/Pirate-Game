namespace Pirates.Ships
{
    /// <summary>Comando de um frame. Jogador e IA produzem o mesmo tipo.</summary>
    public struct ShipCommand
    {
        /// <summary>-1 = bombordo (esquerda), +1 = estibordo (direita).</summary>
        public float steer;
        /// <summary>+1 sobe um nível de vela, -1 desce, 0 mantém.</summary>
        public int sailDelta;
    }

    /// <summary>Fonte de comandos para um ShipController (PlayerShipInput, depois EnemyShipAI).</summary>
    public interface IShipInput
    {
        ShipCommand GetCommand();
    }
}
