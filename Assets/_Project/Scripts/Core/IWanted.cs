namespace Pirates.Core
{
    /// <summary>
    /// Nível de procurado pela Coroa (G, passo 2, 03/11), para quem não conhece a Progression: o save, a conversa com o Almirante
    /// (perdão pela multa) e, depois, a Esquadra Real. Estrelas 0–5 pela "fama ruim" (<see cref="Heat"/>), que cai com o tempo.
    /// </summary>
    public interface IWanted
    {
        int Stars { get; }
        float Heat { get; }
        /// <summary>Segundos de jogo desde o último crime (a procura só começa a cair depois de um tempo).</summary>
        float SecondsSinceCrime { get; }
        /// <summary>Ouro da multa para o perdão (0 sem estrelas).</summary>
        int Fine { get; }
        /// <summary>Ouro que o jogador tem (armazém, porão e mochila).</summary>
        int GoldOwned { get; }
        /// <summary>Paga a multa (ouro do armazém, depois do porão) e zera a procura. Falso sem estrelas ou sem ouro.</summary>
        bool PayFine();
        /// <summary>Jogo novo (0, 0) ou carregado.</summary>
        void Restore(float heat, float secondsSinceCrime);
    }
}
