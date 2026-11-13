namespace Pirates.Core
{
    /// <summary>
    /// Time de quem luta a pé (o CharacterCombat), para quem não conhece o módulo OnFoot (a explosão, 12/11: bomba ou barril aceso pelo
    /// capitão não fere os marujos dele).
    /// </summary>
    public interface ITeamMember
    {
        int Team { get; }
    }

    public static class Teams
    {
        /// <summary>O capitão e os marujos em terra.</summary>
        public const int Player = 0;
    }
}
