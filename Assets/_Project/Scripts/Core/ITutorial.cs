namespace Pirates.Core
{
    /// <summary>
    /// Progresso do tutorial (Services), para o save sem conhecer o módulo do tutorial: etapa atual (−1 = fora do tutorial:
    /// terminado, pulado ou save de antes dele).
    /// </summary>
    public interface ITutorial
    {
        int Step { get; }
        bool Running { get; }
        /// <summary>Jogo novo: começa da primeira etapa (ou nem começa, sem tutorial).</summary>
        void Begin(bool enabled);
        /// <summary>Jogo carregado: volta à etapa salva (−1 = sem tutorial).</summary>
        void Restore(int step);
    }

    /// <summary>Uma etapa do tutorial foi cumprida (som e aviso). finished = foi a última.</summary>
    public struct TutorialStepEvent
    {
        public int step;
        public string title;
        public bool finished;
    }
}
