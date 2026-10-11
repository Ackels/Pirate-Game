using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>Volumes iniciais de cada canal (Data/Audio/SoundSettings). Ajuste de game design; nunca altere em Play.</summary>
    [CreateAssetMenu(menuName = "Pirates/Volumes do som", fileName = "SoundSettings")]
    public class SoundSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float master = 1f;
        // 09/10 (Jhonata: "música e ambiente muito altos"): música 0,45 → 0,3 e ambiente 0,8 → 0,5. Menu de volumes fica para depois.
        [Range(0f, 1f)] public float music = 0.3f, effects = 1f, ambient = 0.5f, interfaceVolume = 0.7f;
    }
}
