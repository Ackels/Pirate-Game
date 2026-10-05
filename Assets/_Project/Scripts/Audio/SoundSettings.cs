using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>Volumes iniciais de cada canal (Data/Audio/SoundSettings). Ajuste de game design; nunca altere em Play.</summary>
    [CreateAssetMenu(menuName = "Pirates/Volumes do som", fileName = "SoundSettings")]
    public class SoundSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float master = 1f;
        [Range(0f, 1f)] public float music = 0.45f, effects = 1f, ambient = 0.8f, interfaceVolume = 0.7f;
    }
}
