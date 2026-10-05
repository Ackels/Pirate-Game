using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Acabamento de desempenho da cena gerada (O5), para a GPU integrada: vegetação e miudezas sem sombra, ilha desenhada
    /// sem a parte que fica sempre embaixo d'água (o colisor continua inteiro) e fumaça com bolinhas low-poly.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Modelos de vegetação do Nature Kit (nome da malha): capim, flores, arbustos, cogumelos, roça.</summary>
        static readonly string[] NoShadowMeshes = { "grass", "flower_", "plant_", "mushroom_", "crop", "corn" };

        /// <summary>Objetos menores que isto (maior lado, em m) não fazem sombra: ninguém a vê na nossa câmera.</summary>
        const float NoShadowSize = 0.6f;

        /// <summary>
        /// Abaixo disto (y no mundo) a malha da ilha está sempre debaixo d'água: as quatro ondas descem ~1,1 m e a água é
        /// opaca. Triângulos inteiros abaixo saem da malha desenhada (eram ~45% dela).
        /// </summary>
        const float IslandUnderwaterCut = -2f;

        /// <summary>
        /// Chamado antes de salvar a cena: tira a sombra da vegetação e das miudezas paradas. Navios e personagens
        /// (Rigidbody, Animator) ficam como estão.
        /// </summary>
        static void OptimizeSceneRendering()
        {
            int off = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
                if (renderer.GetComponentInParent<Rigidbody>(true) || renderer.GetComponentInParent<Animator>(true)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var size = renderer.bounds.size;
                if (!IsVegetation(filter.sharedMesh.name) && Mathf.Max(size.x, size.y, size.z) >= NoShadowSize) continue;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                off++;
            }
            Debug.Log($"[Pirates] Desempenho: {off} objetos pequenos/vegetação sem sombra.");
        }

        static bool IsVegetation(string meshName)
        {
            foreach (var prefix in NoShadowMeshes)
                if (meshName.StartsWith(prefix)) return true;
            return false;
        }

        /// <summary>Cópia da malha da ilha sem os triângulos que ficam sempre debaixo d'água (só para desenhar).</summary>
        static Mesh IslandVisualMesh(Mesh full, float baseY)
        {
            var vertices = full.vertices;
            var normals = full.normals;
            var uvs = full.uv;
            var triangles = full.triangles;
            var remap = new Dictionary<int, int>();
            var outVertices = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outTriangles = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float top = baseY + Mathf.Max(vertices[triangles[i]].y, vertices[triangles[i + 1]].y, vertices[triangles[i + 2]].y);
                if (top < IslandUnderwaterCut) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = triangles[i + k];
                    if (!remap.TryGetValue(v, out int mapped))
                    {
                        mapped = outVertices.Count;
                        remap[v] = mapped;
                        outVertices.Add(vertices[v]);
                        outNormals.Add(normals[v]);
                        outUvs.Add(uvs[v]);
                    }
                    outTriangles.Add(mapped);
                }
            }
            var mesh = new Mesh { name = full.name + "_Visual" };
            mesh.indexFormat = outVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(outVertices);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUvs);
            mesh.SetTriangles(outTriangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Bolinha da fumaça: icosaedro subdividido uma vez (80 triângulos, raio 1 como o Sphere.fbx do Unity, que tem 760).
        /// Normais suaves: de longe parece redonda.
        /// </summary>
        static Mesh SmokePuffMesh => SaveIslandMesh(BuildIcosphere(), $"{MeshesFolder}/Fumaca_Bolinha.asset");

        static Mesh BuildIcosphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var midpoints = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index)) return index;
                vertices.Add((vertices[a] + vertices[b]) * 0.5f);
                return midpoints[key] = vertices.Count - 1;
            }
            var triangles = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                normals[i] = vertices[i].normalized;
                vertices[i] = normals[i];
            }
            var mesh = new Mesh { name = "Fumaca_Bolinha" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, new Vector2[vertices.Count]);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
