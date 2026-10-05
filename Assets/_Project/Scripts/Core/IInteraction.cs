namespace Pirates.Core
{
    /// <summary>
    /// Algo que o jogador abre com a tecla de interagir (F), no navio (saquear destroços, atracar no porto, desembarcar)
    /// ou a pé (abrir um baú, voltar ao navio). O input só conhece esta interface, não cada sistema.
    /// </summary>
    public interface IInteraction
    {
        bool IsOpen { get; }
        bool CanOpen { get; }
        bool Open();
        void Close();
    }
}
