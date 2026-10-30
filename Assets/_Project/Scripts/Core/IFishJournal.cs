namespace Pirates.Core
{
    /// <summary>
    /// Diário do pescador (D2, 30/10), para o save sem conhecer o módulo: cada espécie já pescada (id = nome do asset do item), quantas e o
    /// maior tamanho (m), e os prêmios do diário já recebidos (bits).
    /// </summary>
    public interface IFishJournal
    {
        void Capture(out string[] ids, out int[] counts, out float[] best, out int prizes);
        /// <summary>Jogo novo (null) ou carregado.</summary>
        void Restore(string[] ids, int[] counts, float[] best, int prizes);
    }
}
