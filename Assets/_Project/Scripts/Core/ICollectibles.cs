namespace Pirates.Core
{
    /// <summary>
    /// Coletáveis (C3, 19/10: garrafas com mensagem e moedas da Frota de Prata), para o save sem conhecer o módulo Loot: ids de tudo o
    /// que o jogador já pegou (some do mundo para sempre).
    /// </summary>
    public interface ICollectibles
    {
        string[] Found { get; }
        /// <summary>Jogo novo (null) ou carregado: some com o que já foi pego e põe o resto de volta.</summary>
        void Restore(string[] found);
    }
}
