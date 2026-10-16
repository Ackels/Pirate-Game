namespace Pirates.Core
{
    /// <summary>
    /// Mapas do tesouro em pedaços (C1, 16/10), para o save sem conhecer o módulo Loot: id do tesouro marcado pelo mapa montado
    /// (vazio = nenhum). Os pedaços em si são itens comuns (o save já guarda).
    /// </summary>
    public interface ITreasureMaps
    {
        string ActiveId { get; }
        /// <summary>Jogo novo (null) ou carregado: marca de novo o X do mapa salvo.</summary>
        void Restore(string activeId);
    }
}
