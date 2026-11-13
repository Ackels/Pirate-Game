namespace Pirates.Core
{
    /// <summary>
    /// Descanso do cerco do altar amaldiçoado (e, junto, de Lázaro Negro e do tesouro dele), para o save sem conhecer o módulo LandCombat:
    /// segundos de jogo que faltam para o altar poder despertar de novo.
    /// </summary>
    public interface ISiegeCooldown
    {
        float CooldownLeft { get; }
        /// <summary>Jogo novo (0) ou carregado: o que faltava quando salvou.</summary>
        void Restore(float secondsLeft);
    }
}
