namespace Pirates.Core
{
    /// <summary>
    /// Hora do dia (M11): o ciclo de dia e noite (Ambient/DayNightCycle) registra no <see cref="Services"/>. Quem não conhece o ciclo
    /// (IA de terra, save, HUD, sandbox) lê e muda a hora por aqui.
    /// </summary>
    public interface ITimeOfDay
    {
        /// <summary>Hora do relógio, 0 a 24.</summary>
        float Hour { get; }
        /// <summary>Dia do jogo (o primeiro é 1).</summary>
        int Day { get; }
        /// <summary>0 = dia claro, 1 = noite fechada (sobe e desce no amanhecer e no anoitecer).</summary>
        float Night { get; }
        /// <summary>Vezes mais rápido que o normal (só o sandbox mexe; 1 = normal).</summary>
        float Speed { get; set; }
        void SetTime(int day, float hour);
        /// <summary>Jogo novo: dia 1, na hora de começar.</summary>
        void ResetToStart();
    }
}
