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
        /// <summary>Partes do tutorial (1 = o porto, 2 = o mar...).</summary>
        int Parts { get; }
        /// <summary>Primeira etapa da parte (1 em diante; sandbox F1).</summary>
        int FirstStepOf(int part);
    }

    /// <summary>
    /// Uma etapa do tutorial foi cumprida (som e aviso). partFinished = foi a última da parte (part = o nome dela, "O porto");
    /// finished = foi a última do tutorial.
    /// </summary>
    public struct TutorialStepEvent
    {
        public int step;
        public string title;
        public bool partFinished;
        public string part;
        public bool finished;
    }
}
