namespace Pirates.Core
{
    /// <summary>
    /// Patente do capitão (M10), para quem não conhece a Progression (o quadro de missões): patente atual, nome de cada uma
    /// e quanto o ouro das missões rende a mais. Registrado em <see cref="Services"/> pela fama do capitão.
    /// </summary>
    public interface ICaptainRank
    {
        /// <summary>0 = a primeira patente.</summary>
        int Rank { get; }
        string RankTitle(int rank);
        /// <summary>Ouro das missões × isto (patente + bônus escolhidos).</summary>
        float MissionGoldMultiplier { get; }
    }
}
