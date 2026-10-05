using Pirates.Core;
using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Barco ancorado (só visual, sem física): acompanha a altura da água (IOceanSampler) e balança devagar de um lado
    /// para o outro, preso ao lugar onde o gerador o pôs.
    /// </summary>
    public class MooredBob : MonoBehaviour
    {
        [SerializeField] float draft = 0.3f;
        [SerializeField] float rollDegrees = 3f, pitchDegrees = 1.5f;
        [SerializeField] float period = 5f;

        Vector3 home;
        Quaternion homeRotation;
        float phase;
        IOceanSampler ocean;

        public void Configure(float draftBelowWater, float roll, float pitch, float seconds)
        {
            draft = draftBelowWater;
            rollDegrees = roll;
            pitchDegrees = pitch;
            period = seconds;
        }

        void Start()
        {
            home = transform.position;
            homeRotation = transform.rotation;
            phase = Random.value * 10f;
            Services.TryGet(out ocean);
        }

        void Update()
        {
            float t = (Time.time + phase) * Mathf.PI * 2f / period;
            float water = ocean != null ? ocean.GetHeight(home) : 0f;
            transform.SetPositionAndRotation(new Vector3(home.x, water - draft, home.z),
                homeRotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * pitchDegrees, 0f, Mathf.Sin(t) * rollDegrees));
        }
    }
}
