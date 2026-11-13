using System;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// O cursor está em cima de algo clicável do HUD (botão, janela)? Quem dispara pelo clique (os canhões) pergunta aqui para não atirar
    /// junto com o clique no HUD (12/11). O GameHUD responde (UI Toolkit); sem HUD, nunca.
    /// </summary>
    public static class PointerOverUI
    {
        /// <summary>Recebe a posição do mouse na tela (origem embaixo à esquerda).</summary>
        public static Func<Vector2, bool> Check;

        public static bool At(Vector2 screenPosition) => Check != null && Check(screenPosition);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Check = null;
    }
}
