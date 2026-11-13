namespace Pirates.Core
{
    /// <summary>Salário da tripulação (12/11), para o save sem conhecer o módulo Base: ouro devido e moral (0–1).</summary>
    public interface ICrewWages
    {
        float Debt { get; }
        float Morale { get; }
        float HoursToPayday { get; }
        float HoursLate { get; }
        /// <summary>Jogo novo ou save antigo (has = false): nada devido, moral cheia, próximo pagamento daqui a um ciclo.</summary>
        void Restore(bool has, float debt, float morale, float hoursToPayday, float hoursLate);
    }
}
