namespace Pirates.Core
{
    /// <summary>Pintura do navio (16/11), para o save sem conhecer a Progression: cores compradas e a escolhida de cada parte.</summary>
    public interface IShipLivery
    {
        string[] Owned { get; }
        /// <summary>"Parte=id" de cada parte pintada.</summary>
        string[] SelectedIds { get; }
        /// <summary>Jogo novo ou save antigo (has = false): só as cores do kit.</summary>
        void Restore(bool has, string[] owned, string[] selected);
    }
}
