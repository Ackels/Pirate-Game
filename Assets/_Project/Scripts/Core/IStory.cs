namespace Pirates.Core
{
    /// <summary>
    /// Trilha da História ("A Coroa de Prata", 16/10), para o save e o sandbox sem conhecer o módulo Story: capítulo atual (0 em diante;
    /// = número de capítulos quando acabou o que existe) e quanto de cada objetivo dele já foi feito.
    /// </summary>
    public interface IStory
    {
        int Chapter { get; }
        int ChapterCount { get; }
        string ChapterTitle(int chapter);
        /// <summary>Progresso de cada objetivo do capítulo atual (contagem; objetivos de estado não guardam).</summary>
        int[] Progress { get; }
        /// <summary>Jogo novo: capítulo 1 do zero.</summary>
        void Begin();
        /// <summary>Jogo carregado (ou F1): volta ao capítulo salvo com o progresso dos objetivos (null = zero).</summary>
        void Restore(int chapter, int[] progress);
    }

    /// <summary>Um capítulo da História acabou (faixa no meio, vinheta). next = título do próximo (vazio = acabou o que existe).</summary>
    public struct StoryChapterEvent
    {
        public int chapter;
        public string title;
        public string next;
    }
}
