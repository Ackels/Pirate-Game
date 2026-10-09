using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>Ajustes do clima (M11, passo 2: nuvens, chuva, tempestade e raios). Não mude em Play (fica salvo no asset).</summary>
    [CreateAssetMenu(menuName = "Pirates/Ambient/Weather Settings", fileName = "Clima")]
    public class WeatherSettings : ScriptableObject
    {
        [Header("Quando")]
        [Tooltip("Horas de jogo para o tempo mudar de cara (o dia todo tem 24 h, ~20 min reais).")]
        [Min(0.5f)] public float changeHours = 9f;
        [Tooltip("Ruído do tempo (0–1) a partir do qual fecha o céu, chove e vem a tempestade: quanto maior, mais raro.")]
        [Range(0f, 1f)] public float cloudyFrom = 0.62f, rainFrom = 0.8f, stormFrom = 0.95f;
        [Tooltip("Largura da passagem de um tempo para o outro (no ruído; ~1 hora de jogo).")]
        [Range(0.01f, 0.3f)] public float blend = 0.05f;
        [Tooltip("Segundos para trocar de tempo quando o sandbox fixa (ou a hora pula).")]
        [Min(1f)] public float transitionSeconds = 20f;
        [Tooltip("Jogo novo: horas iniciais sempre de céu limpo.")]
        [Min(0f)] public float clearStartHours = 6f;

        [Header("Vento")]
        [Tooltip("Força mínima do vento com chuva cheia e com tempestade cheia.")]
        [Range(0f, 1f)] public float rainWindFloor = 0.5f, stormWindFloor = 0.95f;

        [Header("Raios (só na tempestade)")]
        [Tooltip("Segundos entre um raio e outro com a tempestade cheia (sorteado entre x e y).")]
        public Vector2 lightningInterval = new(6f, 15f);
        [Tooltip("Distância do raio até o que a câmera olha (m), para um dos lados (fica na tela).")]
        public Vector2 boltDistance = new(18f, 38f);
        [Tooltip("Força do clarão (luz direcional ligada por um instante).")]
        public float flashIntensity = 1.6f;
        public Color flashColor = new(0.82f, 0.87f, 1f);
    }
}
