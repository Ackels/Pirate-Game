using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Integração balística compartilhada entre a simulação das balas e a prévia da mira,
    /// para que o arco mostrado seja exatamente o caminho da bala.
    /// </summary>
    public static class Ballistics
    {
        public static void Step(ref Vector3 position, ref Vector3 velocity, float dt)
        {
            Vector3 g = Physics.gravity;
            position += velocity * dt + 0.5f * dt * dt * g;
            velocity += g * dt;
        }

        /// <summary>Preenche <paramref name="points"/> com o arco e retorna o ponto de impacto (água ou colisor).</summary>
        public static Vector3 Predict(Vector3 origin, Vector3 velocity, IOceanSampler ocean, Rigidbody ignore,
            List<Vector3> points, float step = 0.05f, float maxTime = 6f)
        {
            points.Clear();
            points.Add(origin);
            Vector3 p = origin, v = velocity;

            for (float t = 0f; t < maxTime; t += step)
            {
                Vector3 previous = p;
                Step(ref p, ref v, step);

                if (Physics.Linecast(previous, p, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                    && (ignore == null || hit.rigidbody != ignore))
                {
                    points.Add(hit.point);
                    return hit.point;
                }

                float water = ocean != null ? ocean.GetHeight(p) : 0f;
                if (p.y <= water)
                {
                    float before = previous.y - (ocean != null ? ocean.GetHeight(previous) : 0f);
                    float k = before / Mathf.Max(0.0001f, before - (p.y - water));
                    Vector3 surface = Vector3.Lerp(previous, p, Mathf.Clamp01(k));
                    points.Add(surface);
                    return surface;
                }
                points.Add(p);
            }
            return p;
        }
    }
}
