using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Um painel da tela que usa o mouse está aberto sem ser de uma interação (ex.: a fama do capitão, tecla C): o input a pé
    /// não ataca, não mira e não pula enquanto isso (os cliques são do painel). Quem abre liga; quem fecha desliga.
    /// </summary>
    public static class ScreenPanel
    {
        public static bool Open { get; set; }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Open = false;
    }
}
