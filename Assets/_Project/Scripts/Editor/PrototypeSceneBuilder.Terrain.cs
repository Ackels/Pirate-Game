using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ilhas geradas por código (M6.3): malha low-poly (triângulos chapados, cores da paleta do Kenney no mesmo material
    /// dos modelos) com litoral irregular, praia que sobe da água, morros e ondulação no miolo. Pontos de que o jogo
    /// depende ficam garantidos: "âncoras" fixam o litoral numa direção (praia de desembarque, píer) e "áreas planas"
    /// fixam a altura (acampamento, anel de prédios do porto, topo da fortaleza, tesouro). A mesma malha é o
    /// MeshCollider (navio encalha, personagem anda, NavMesh). Decoração: palmeiras, pedras e tufos de grama do kit.
    /// A malha é salva como asset (Assets/_Project/Meshes/&lt;ilha&gt;.asset), a mesma em todas as cenas: regerar uma cena
    /// atualiza o arquivo no lugar (mesmo GUID), e as outras cenas continuam apontando para ele.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string MeshesFolder = Root + "/Meshes";
        /// <summary>Cena sendo gerada (para mensagens; as malhas das ilhas são compartilhadas entre as cenas).</summary>
        static string generatingScenePath = "Cena";

        sealed class IslandSpec
        {
            public string name;
            public Vector3 center;
            /// <summary>Raio do litoral (onde o chão cruza o nível do mar) em X e Z antes das irregularidades.</summary>
            public float radiusX, radiusZ;
            /// <summary>Irregularidade do litoral (fração do raio). Só para fora: nunca encolhe abaixo do raio.</summary>
            public float coastNoise = 0.12f;
            public bool coastOutwardOnly;
            public int seed;
            /// <summary>Altura do chão no fim da praia e quantos metros a praia leva para subir até ela.</summary>
            public float plateau = 0.8f, beachWidth = 6f;
            /// <summary>Ondulação do miolo (m).</summary>
            public float inlandNoise = 0.5f;
            public float cell = 2f;
            /// <summary>Litoral fixo numa direção: graus (0 = leste, 90 = norte), raio, meia abertura em graus.</summary>
            public (float angle, float radius, float width)[] anchors = Array.Empty<(float, float, float)>();
            /// <summary>Morros: centro (XZ local), raio, altura acima do platô.</summary>
            public (Vector2 pos, float radius, float height)[] hills = Array.Empty<(Vector2, float, float)>();
            /// <summary>Áreas planas: centro, raio plano, faixa de transição, altura.</summary>
            public (Vector2 pos, float radius, float blend, float height)[] flats = Array.Empty<(Vector2, float, float, float)>();
            /// <summary>Ruas e trilhas de terra batida (só a cor do chão): de, até (XZ local), largura. a = b faz uma praça redonda.</summary>
            public (Vector2 a, Vector2 b, float width)[] paths = Array.Empty<(Vector2, Vector2, float)>();
        }

        /// <summary>Quadradinhos da paleta (colormap 16 × 4): coluna e a faixa de V da linha.</summary>
        static class Palette
        {
            public static Vector2 Sand(float t) => Swatch(t < 0.8f ? 1 : 0, 1, t);
            public static Vector2 Seabed(float t) => Swatch(15, 3, t);
            public static Vector2 Grass(float t) => Swatch(t < 0.72f ? 2 : 3, 1, t);
            public static Vector2 Rock(float t) => Swatch(t < 0.7f ? 3 : 2, 3, t);
            public static Vector2 DarkRock(float t) => Swatch(4, 3, t);
            public static Vector2 Dirt(float t) => Swatch(t < 0.6f ? 14 : 15, 3, t);

            /// <summary>row 1 = cores vivas (v 0,25–0,5), row 3 = cinzas/madeira (v 0–0,25); t varia o tom dentro do quadrado.</summary>
            static Vector2 Swatch(int column, int row, float t)
            {
                float vTop = row == 1 ? 0.48f : 0.23f, vBottom = row == 1 ? 0.27f : 0.02f;
                return new Vector2((column + 0.5f) / 16f, Mathf.Lerp(vBottom, vTop, Mathf.Repeat(t * 7.3f, 1f)));
            }
        }

        static float Smooth01(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static bool OnPath(IslandSpec s, Vector2 p)
        {
            foreach (var path in s.paths)
                if (DistanceToSegment(p, path.a, path.b) < path.width * 0.5f) return true;
            return false;
        }

        static float CoastRadius(IslandSpec s, float angle)
        {
            float c = Mathf.Cos(angle), si = Mathf.Sin(angle);
            float ellipse = 1f / Mathf.Sqrt(c * c / (s.radiusX * s.radiusX) + si * si / (s.radiusZ * s.radiusZ));
            var rng = new Random(s.seed);
            float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f;
            float n = 0.55f * Mathf.Sin(2f * angle + p1) + 0.3f * Mathf.Sin(3f * angle + p2) + 0.15f * Mathf.Sin(5f * angle + p3);
            if (s.coastOutwardOnly) n = Mathf.Abs(n);
            float r = ellipse * (1f + s.coastNoise * n);
            float degrees = angle * Mathf.Rad2Deg;
            foreach (var a in s.anchors)
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(degrees, a.angle));
                if (off < a.width) r = Mathf.Lerp(r, a.radius, 1f - Smooth01(off / a.width));
            }
            return r;
        }

        /// <summary>Altura do chão no ponto local (XZ). 0 no litoral; a praia sobe até o platô; fora, o fundo cai rápido.</summary>
        static float IslandHeight(IslandSpec s, Vector2 p)
        {
            float coast = CoastRadius(s, Mathf.Atan2(p.y, p.x));
            float inside = coast - p.magnitude;
            float y;
            if (inside < 0f)
            {
                // Fundo íngreme (~31°): o navio encalha a ~1 m da praia, o personagem entra até a cintura e afunda depois.
                y = -Mathf.Min(6f, -inside * 0.6f);
            }
            else
            {
                float beach = Smooth01(inside / s.beachWidth);
                y = s.plateau * beach;
                float inland = Smooth01((inside - s.beachWidth) / s.beachWidth);
                if (s.inlandNoise > 0f)
                    y += s.inlandNoise * inland * (Mathf.PerlinNoise(s.seed * 0.37f + p.x / 22f, s.seed * 0.91f + p.y / 22f) * 1.6f - 0.4f);
                foreach (var h in s.hills)
                {
                    float t = Vector2.Distance(p, h.pos) / h.radius;
                    if (t < 1f) y += h.height * 0.5f * (1f + Mathf.Cos(Mathf.PI * t)) * beach;
                }
            }
            foreach (var f in s.flats)
            {
                float w = 1f - Smooth01((Vector2.Distance(p, f.pos) - f.radius) / Mathf.Max(0.01f, f.blend));
                if (w > 0f) y = Mathf.Lerp(y, f.height, w);
            }
            return y;
        }

        /// <summary>
        /// Malha da ilha + MeshCollider, estática, com o material da paleta. Devolve a raiz (centro da ilha).
        /// O colisor usa a malha inteira; a desenhada (`_Visual`) não tem a parte sempre debaixo d'água (O5).
        /// </summary>
        static Transform CreateIslandTerrain(Transform parent, IslandSpec s)
        {
            var mesh = SaveIslandMesh(BuildIslandMesh(s), $"{MeshesFolder}/{s.name}.asset");
            var visual = SaveIslandMesh(IslandVisualMesh(mesh, s.center.y), $"{MeshesFolder}/{s.name}_Visual.asset");

            var go = new GameObject(s.name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = s.center;
            go.AddComponent<MeshFilter>().sharedMesh = visual;
            go.AddComponent<MeshRenderer>().sharedMaterial = KenneyMaterial;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            Physics.SyncTransforms();
            sceneIslands.Add(s);   // mapa da costa do oceano (BakeShoreMap)
            return go.transform;
        }

        /// <summary>Grava a malha no asset, atualizando o que já existe (mantém o GUID que as outras cenas usam).</summary>
        static Mesh SaveIslandMesh(Mesh built, string path)
        {
            EnsureFolder(Root, "Meshes");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.indexFormat = built.indexFormat;
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetUVs(0, built.uv);
            existing.SetTriangles(built.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        static Mesh BuildIslandMesh(IslandSpec s)
        {
            float reach = 1f + s.coastNoise;
            foreach (var a in s.anchors) reach = Mathf.Max(reach, a.radius / Mathf.Min(s.radiusX, s.radiusZ));
            float extentX = s.radiusX * reach + 12f, extentZ = s.radiusZ * reach + 12f;
            int nx = Mathf.CeilToInt(2f * extentX / s.cell), nz = Mathf.CeilToInt(2f * extentZ / s.cell);
            var rng = new Random(s.seed * 31 + 7);

            // Grade com os pontos de dentro um pouco deslocados: os triângulos não ficam todos iguais.
            var points = new Vector3[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                float jx = 0f, jz = 0f;
                if (i > 0 && j > 0 && i < nx && j < nz)
                {
                    jx = ((float)rng.NextDouble() - 0.5f) * 0.7f * s.cell;
                    jz = ((float)rng.NextDouble() - 0.5f) * 0.7f * s.cell;
                }
                float x = -extentX + i * s.cell + jx, z = -extentZ + j * s.cell + jz;
                points[i, j] = new Vector3(x, IslandHeight(s, new Vector2(x, z)), z);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                // Ordem horária vista de cima (frente para cima no Unity).
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var mid = (a + b + c) / 3f;
                float slope = Vector3.Angle(normal, Vector3.up);
                float inside = CoastRadius(s, Mathf.Atan2(mid.z, mid.x)) - new Vector2(mid.x, mid.z).magnitude;
                float t = (float)rng.NextDouble();
                Vector2 uv;
                if (mid.y < -0.3f) uv = Palette.Seabed(t);
                else if (slope > 40f) uv = Palette.DarkRock(t);
                else if (slope > 30f) uv = Palette.Rock(t);
                else if (OnPath(s, new Vector2(mid.x, mid.z))) uv = Palette.Dirt(t);
                else if (inside < s.beachWidth * 0.95f) uv = Palette.Sand(t);
                else uv = Palette.Grass(t);
                foreach (var v in new[] { a, b, c })
                {
                    vertices.Add(v);
                    normals.Add(normal);
                    uvs.Add(uv);
                }
            }

            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vector3 p00 = points[i, j], p10 = points[i + 1, j], p01 = points[i, j + 1], p11 = points[i + 1, j + 1];
                // Diagonal alternada: o chão não fica "listrado".
                if (((i + j) & 1) == 0)
                {
                    Triangle(p00, p01, p11);
                    Triangle(p00, p11, p10);
                }
                else
                {
                    Triangle(p00, p01, p10);
                    Triangle(p10, p01, p11);
                }
            }

            var mesh = new Mesh { name = s.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            var indices = new int[vertices.Count];
            for (int k = 0; k < indices.Length; k++) indices[k] = k;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Decoração ----------

        static readonly string[] PalmModels = { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        static readonly string[] GrayRocks = { "rocks-a", "rocks-b", "rocks-c" };
        static readonly string[] SandRocks = { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        static readonly string[] Tufts = { "grass", "grass-patch", "grass-plant" };

        /// <summary>Raio de cima para baixo: o ponto no chão da ilha e a inclinação (graus). false se não caiu na ilha.</summary>
        static bool IslandGround(Transform island, Vector3 world, out Vector3 point, out float slope)
        {
            point = world;
            slope = 90f;
            foreach (var hit in Physics.RaycastAll(new Vector3(world.x, 80f, world.z), Vector3.down, 160f))
            {
                if (hit.collider.transform != island) continue;
                point = hit.point;
                slope = Vector3.Angle(hit.normal, Vector3.up);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Palmeiras (com colisor no tronco), pedras (colisor da malha) e tufos de grama (sem colisor) espalhados pela ilha,
        /// onde <paramref name="allowed"/> deixa (XZ local). Tudo estático (entra no NavMesh como obstáculo).
        /// </summary>
        static void DecorateIsland(Transform island, IslandSpec s, int palms, int rocks, int tufts, Func<Vector2, bool> allowed,
            int seed)
        {
            var rng = new Random(seed);
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var root = new GameObject("Decoracao").transform;
            root.SetParent(island, false);
            float maxR = Mathf.Max(s.radiusX, s.radiusZ);

            bool Spot(float minHeight, float maxSlope, out Vector3 local)
            {
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    var p = new Vector2(R(-maxR, maxR), R(-maxR, maxR));
                    if (allowed != null && !allowed(p)) continue;
                    if (!IslandGround(island, island.TransformPoint(new Vector3(p.x, 0f, p.y)), out var ground, out float slope)) continue;
                    if (ground.y - island.position.y < minHeight || slope > maxSlope) continue;
                    local = island.InverseTransformPoint(ground);
                    return true;
                }
                local = default;
                return false;
            }

            for (int k = 0; k < palms; k++)
            {
                if (!Spot(s.plateau * 0.7f, 22f, out var local)) continue;
                float scale = R(1.45f, 2.15f);
                var palm = Kenney(PalmModels[rng.Next(PalmModels.Length)], root, local, R(0f, 360f), scale, $"Palmeira_{k}");
                var trunk = palm.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0f, 1.2f, 0f);
                trunk.radius = 0.18f;
                trunk.height = 2.4f;
                Occluder(palm);
            }
            for (int k = 0; k < rocks; k++)
            {
                if (!Spot(0.1f, 35f, out var local)) continue;
                // Arenito perto da praia, pedra cinza no miolo e nos morros.
                bool beach = local.y < s.plateau + 0.4f;
                var rock = Kenney((beach ? SandRocks : GrayRocks)[rng.Next(3)], root, local + Vector3.down * 0.3f, R(0f, 360f),
                    R(0.6f, 1.2f), $"Pedra_{k}");
                foreach (var filter in rock.GetComponentsInChildren<MeshFilter>())
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            for (int k = 0; k < tufts; k++)
            {
                if (!Spot(s.plateau + 0.05f, 25f, out var local)) continue;
                Kenney(Tufts[rng.Next(Tufts.Length)], root, local, R(0f, 360f), R(1.5f, 2.6f), $"Tufo_{k}");
            }
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Physics.SyncTransforms();
        }

        /// <summary>Pedra grande do kit, com colisor, num ponto da ilha (o chão decide a altura).</summary>
        static GameObject IslandRock(Transform island, string name, Vector2 local, string model, float scale, float yaw)
        {
            IslandGround(island, island.TransformPoint(new Vector3(local.x, 0f, local.y)), out var ground, out _);
            var rock = Kenney(model, island, island.InverseTransformPoint(ground) + Vector3.down * 0.4f, yaw, scale, name);
            foreach (var filter in rock.GetComponentsInChildren<MeshFilter>())
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            Occluder(rock);
            foreach (var t in rock.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return rock;
        }
    }
}
