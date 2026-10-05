namespace Pirates.Core
{
    /// <summary>Algo que tranca outra coisa até uma condição (ex.: acampamento com piratas vivos tranca o baú).</summary>
    public interface ILockSource
    {
        bool IsLocked { get; }
        /// <summary>Por que está trancado, para o jogador ("derrote os 2 piratas do acampamento").</summary>
        string LockedReason { get; }
    }
}
