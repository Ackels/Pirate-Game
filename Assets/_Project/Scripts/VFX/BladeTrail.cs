using System.Collections.Generic;
using UnityEngine;

namespace Pirates.VFX
{
    /// <summary>
    /// Rastro do cutelo: uma fita entre a base e a ponta da lâmina, guardada quadro a quadro enquanto o golpe está rápido
    /// e apagando em <see cref="life"/> segundos (a ponta forte, a base transparente). A malha é refeita só enquanto há
    /// rastro, com listas reaproveitadas (sem lixo) e curva Catmull-Rom entre as amostras (golpe a 30 fps fica redondo).
    /// </summary>
    sealed class BladeTrail
    {
        const int MaxSamples = 24, Subdivisions = 3;

        readonly Transform blade;
        readonly Vector3 baseLocal, tipLocal;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Vector3[] bases = new Vector3[MaxSamples], tips = new Vector3[MaxSamples];
        readonly float[] times = new float[MaxSamples];
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<Vector2> uvs = new();
        readonly List<int> triangles = new();
        readonly float life;
        readonly Color color;
        int count;

        public BladeTrail(Transform parent, Transform bladeVisual, Material material, Color trailColor, float seconds)
        {
            blade = bladeVisual;
            life = seconds;
            color = trailColor;
            MeasureBlade(bladeVisual, out baseLocal, out tipLocal);
            var go = new GameObject("Rastro_" + bladeVisual.name);
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Rastro" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        public Vector3 Tip => blade.TransformPoint(tipLocal);
        public bool Valid => blade;

        /// <summary>Lâmina pelo visual da arma (o pivô é a empunhadura): o eixo mais comprido, da guarda até a ponta.</summary>
        static void MeasureBlade(Transform visual, out Vector3 baseLocal, out Vector3 tipLocal)
        {
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var b = filter.sharedMesh.bounds;
                var toVisual = visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                    var p = toVisual.MultiplyPoint3x4(corner);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            if (float.IsInfinity(min.x))
            {
                baseLocal = Vector3.forward * 0.25f;
                tipLocal = Vector3.forward * 0.8f;
                return;
            }
            var size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var center = (min + max) * 0.5f;
            // A ponta é a ponta mais longe da empunhadura (pivô).
            float far = Mathf.Abs(max[axis]) >= Mathf.Abs(min[axis]) ? max[axis] : min[axis];
            float near = far == max[axis] ? min[axis] : max[axis];
            tipLocal = center;
            tipLocal[axis] = far;
            baseLocal = center;
            baseLocal[axis] = Mathf.Lerp(near, far, 0.3f);
        }

        /// <summary>Esquece o rastro (o golpe recomeçou depois de uma pausa: não liga a fita velha na nova).</summary>
        public void Break() => count = 0;

        /// <summary>Uma amostra da lâmina agora.</summary>
        public void Sample()
        {
            if (count == MaxSamples)
            {
                for (int i = 1; i < MaxSamples; i++)
                {
                    bases[i - 1] = bases[i];
                    tips[i - 1] = tips[i];
                    times[i - 1] = times[i];
                }
                count--;
            }
            bases[count] = blade.TransformPoint(baseLocal);
            tips[count] = blade.TransformPoint(tipLocal);
            times[count] = Time.time;
            count++;
        }

        /// <summary>Tira as amostras velhas e remonta a malha. Sem rastro, desliga o desenho.</summary>
        public void Rebuild()
        {
            float now = Time.time;
            int old = 0;
            while (old < count && now - times[old] > life) old++;
            if (old > 0)
            {
                for (int i = old; i < count; i++)
                {
                    bases[i - old] = bases[i];
                    tips[i - old] = tips[i];
                    times[i - old] = times[i];
                }
                count -= old;
            }
            if (count < 2)
            {
                if (renderer.enabled) renderer.enabled = false;
                return;
            }

            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();
            for (int i = 0; i < count - 1; i++)
            {
                int steps = i == count - 2 ? Subdivisions + 1 : Subdivisions;
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)Subdivisions;
                    int a = Mathf.Max(i - 1, 0), d = Mathf.Min(i + 2, count - 1);
                    var b = CatmullRom(bases[a], bases[i], bases[i + 1], bases[d], t);
                    var p = CatmullRom(tips[a], tips[i], tips[i + 1], tips[d], t);
                    float age = (now - Mathf.Lerp(times[i], times[i + 1], t)) / life;
                    float alpha = Mathf.Clamp01(1f - age);
                    alpha *= alpha;
                    vertices.Add(b);
                    vertices.Add(p);
                    colors.Add(new Color(color.r, color.g, color.b, 0f));
                    colors.Add(new Color(color.r, color.g, color.b, color.a * alpha));
                    uvs.Add(new Vector2(age, 0f));
                    uvs.Add(new Vector2(age, 1f));
                }
            }
            for (int v = 0; v + 3 < vertices.Count; v += 2)
            {
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                triangles.Add(v + 2); triangles.Add(v + 1); triangles.Add(v + 3);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (!renderer.enabled) renderer.enabled = true;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        public void Destroy()
        {
            if (renderer) Object.Destroy(renderer.gameObject);
            if (mesh) Object.Destroy(mesh);
        }
    }
}
