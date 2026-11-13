namespace Pirates.Core
{
    /// <summary>
    /// Carta náutica (mapa no M, 12/11), para o save sem conhecer o módulo Controls: ids dos lugares do mar já descobertos (ilhas e covis).
    /// </summary>
    public interface ISeaChart
    {
        string[] Discovered { get; }
        /// <summary>Jogo novo (null) ou carregado.</summary>
        void Restore(string[] discovered);
    }
}
