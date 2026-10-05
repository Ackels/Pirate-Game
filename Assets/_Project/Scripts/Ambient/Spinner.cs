using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>Gira sem parar em volta de um eixo local (pás do moinho).</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField] float degreesPerSecond = 25f;

        public void Configure(Vector3 localAxis, float speed)
        {
            axis = localAxis;
            degreesPerSecond = speed;
        }

        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
