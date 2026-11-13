using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Parada de impacto (revisão das animações, passo 5, 21/10): o jogo quase para por alguns centésimos de segundo no acerto, no aparar e
    /// ao levar golpe, para o golpe "pesar". Mexe no <see cref="Time.timeScale"/> e devolve o que estava antes (o F1 acelerando o jogo, por
    /// exemplo); com o jogo pausado (menu) não faz nada, e se alguém mudar a velocidade no meio, não desfaz a mudança dele.
    /// </summary>
    public static class HitStop
    {
        /// <summary>Desliga as paradas (testes pela CLI que contam quadros).</summary>
        public static bool Disabled { get; set; }

        static HitStopRunner runner;

        /// <summary>Quase para o jogo por <paramref name="seconds"/> (tempo real), na velocidade × <paramref name="scale"/>.</summary>
        public static void Request(float seconds, float scale = 0.06f)
        {
            if (Disabled || seconds <= 0f || !Application.isPlaying) return;
            if (!runner)
            {
                var go = new GameObject("ParadaDeImpacto") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<HitStopRunner>();
            }
            runner.Begin(seconds, scale);
        }

        /// <summary>
        /// Encerra na hora a parada que estiver valendo e devolve a velocidade de antes. Quem mexe no <see cref="Time.timeScale"/> (pausa,
        /// roda dos marujos, epílogo) chama isto antes de guardar a velocidade: senão guardaria a da parada (×0,05) e o jogo voltaria preso nela.
        /// </summary>
        public static void Cancel()
        {
            if (runner) runner.Cancel();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Disabled = false;
            runner = null;
        }
    }
}
