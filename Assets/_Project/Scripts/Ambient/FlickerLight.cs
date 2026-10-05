using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>Luz de fogo: a intensidade treme em volta do valor configurado (ruído suave, não pisca).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] float baseIntensity = 2f, amount = 0.35f, speed = 6f;

        Light fire;
        float seed;

        public void Configure(float intensity, float flicker)
        {
            baseIntensity = intensity;
            amount = flicker;
        }

        void Awake()
        {
            fire = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update() => fire.intensity = baseIntensity * (1f + amount * (Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f));
    }
}
