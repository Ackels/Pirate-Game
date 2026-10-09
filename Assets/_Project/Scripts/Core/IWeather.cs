using System;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>Tempo que o sandbox pode fixar.</summary>
    public enum WeatherKind { Clear, Cloudy, Rain, Storm }

    /// <summary>
    /// Clima (M11, passo 2): o sistema de clima (Ambient/WeatherSystem) registra no <see cref="Services"/>. Luz do dia, vento, chuva na tela,
    /// navios (balanço e mira na tempestade), som, HUD e sandbox leem por aqui, sem conhecer o clima.
    /// </summary>
    public interface IWeather
    {
        /// <summary>0 = céu limpo, 1 = céu fechado (escurece o dia, adensa a névoa).</summary>
        float Overcast { get; }
        /// <summary>0 = sem chuva, 1 = chuva forte.</summary>
        float Rain { get; }
        /// <summary>0 = sem tempestade, 1 = tempestade cheia (vento forte, raios, mar agitado).</summary>
        float Storm { get; }
        /// <summary>Nome do tempo agora ("céu limpo", "nublado", "chuva", "tempestade").</summary>
        string Label { get; }
        /// <summary>Caiu um raio: ponto no mundo e distância até o que a câmera olha (o trovão chega depois).</summary>
        event Action<Vector3, float> LightningStruck;
        /// <summary>Sandbox fixou o tempo.</summary>
        bool Overridden { get; }
        /// <summary>Sandbox: fixa o tempo (null volta ao tempo que muda sozinho). A troca é gradual.</summary>
        void SetOverride(WeatherKind? kind);
        /// <summary>Sandbox: um raio agora.</summary>
        void StrikeNow();
    }
}
