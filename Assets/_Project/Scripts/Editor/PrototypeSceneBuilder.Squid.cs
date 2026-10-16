using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Lula gigante (chefe do mar, cena 07): covil em mar aberto bem a oeste-sudoeste, longe de tudo ("Águas Negras", marcado por
    /// cascos naufragados e pedras em volta). Seis tentáculos do kit Quaternius (Characters_Tentacle, movidos por código pelo
    /// <see cref="SquidTentacle"/>) e a cabeça feita aqui (malha low-poly: manto inclinado com barbatanas e dois olhos de brilho
    /// âmbar). A luta é do <see cref="GiantSquid"/>.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Centro do covil: ~850 m do cais e ~650 m da Ilha Oeste (mapa espaçado em 16/10; era (−560, −330)).</summary>
        static Vector3 SquidLair => new(-760f, 0f, -420f);
        const string SquidId = "lula_gigante";
        const float SquidHealth = 1400f, SquidTentacleHealth = 120f, SquidTentacleScale = 0.8f;
        static readonly Color SquidSkin = new(0.86f, 0.52f, 0.62f);

        static void AddGiantSquid(ShipController player, LootAssets loot)
        {
            var root = new GameObject("LulaGigante");
            root.transform.position = SquidLair;
            root.AddComponent<Health>().Configure(SquidHealth);

            var skin = SquidMaterial();
            var head = SquidHead(root.transform, skin);
            var aim = head.gameObject.AddComponent<AimTarget>();

            var arms = new List<SquidTentacle>();
            for (int i = 0; i < 6; i++) arms.Add(SquidArm(root.transform, i, skin));

            var squid = root.AddComponent<GiantSquid>();
            squid.Configure(player.transform, head, aim, arms.ToArray());
            root.AddComponent<LootDropper>().Configure(SquidLoot(loot));
            root.AddComponent<WorldMarker>().Configure(SquidId, "Águas Negras", 14f);
            SquidLairLandmarks();
        }

        /// <summary>O atlas do kit tingido de vermelho-arroxeado (tentáculos e manto).</summary>
        static Material SquidMaterial()
        {
            var mat = GetTexturedMaterial("M_Lula", QuaterniusAtlas, 0.35f);
            if (mat.GetColor("_BaseColor") == Color.white)
            {
                mat.SetColor("_BaseColor", SquidSkin);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        static SquidTentacle SquidArm(Transform parent, int index, Material skin)
        {
            var holder = new GameObject($"Tentaculo_{index}");
            holder.transform.SetParent(parent, false);
            holder.transform.localScale = Vector3.one * SquidTentacleScale;
            var model = KitCharacter("Characters_Tentacle", holder.transform, skin);
            if (model.TryGetComponent(out Animator animator)) Object.DestroyImmediate(animator);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var chain = new List<Transform>();
            var bone = model.transform.Find("TentacleArmature/Root/Tentacle1");
            while (bone)
            {
                chain.Add(bone);
                bone = bone.childCount > 0 ? bone.GetChild(0) : null;
            }

            holder.AddComponent<Health>().Configure(SquidTentacleHealth);
            var arm = holder.AddComponent<SquidTentacle>();
            arm.Configure(model, chain.ToArray());
            return arm;
        }

        /// <summary>Cabeça (filho que sobe e desce): malha feita aqui, inclinada para trás, e o colisor no manto.</summary>
        static Transform SquidHead(Transform root, Material skin)
        {
            var head = new GameObject("Cabeca").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, -12f, 0f);

            var body = new GameObject("Manto");
            body.transform.SetParent(head, false);
            body.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            body.AddComponent<MeshFilter>().sharedMesh = SquidHeadMesh(skin);
            body.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                skin,
                GetMaterial("M_Lula_Olho", new Color(0.05f, 0.04f, 0.06f), 0.9f),
                GetEmissiveMaterial("M_Lula_Iris", new Color(1f, 0.62f, 0.12f), 2.2f),
            };
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 2.4f;
            capsule.height = 9.5f;
            capsule.center = new Vector3(0f, 4.4f, 0f);
            return head;
        }

        /// <summary>UV do quadradinho do atlas com a cor do tentáculo (a mais usada na malha dele).</summary>
        static Vector2 SquidSkinUv()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/Characters_Tentacle.fbx");
            var mesh = asset ? asset.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh : null;
            if (!mesh) return new Vector2(0.5f, 0.5f);
            var count = new Dictionary<Vector2Int, int>();
            var any = new Dictionary<Vector2Int, Vector2>();
            foreach (var uv in mesh.uv)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(uv.x * 32f), Mathf.FloorToInt(uv.y * 32f));
                count[cell] = count.TryGetValue(cell, out int n) ? n + 1 : 1;
                any[cell] = uv;
            }
            Vector2Int best = default;
            int most = -1;
            foreach (var pair in count)
                if (pair.Value > most) { most = pair.Value; best = pair.Key; }
            return new Vector2((best.x + 0.5f) / 32f, (best.y + 0.5f) / 32f);
        }

        /// <summary>
        /// Manto low-poly (12 lados, facetado, um pouco irregular) de ~9,5 m em pé no eixo Y, duas barbatanas no topo e dois olhos
        /// nos lados perto da base (esfera escura + faixa âmbar em volta da pupila). Três submalhas: pele, olho, íris.
        /// </summary>
        static Mesh SquidHeadMesh(Material skin)
        {
            Vector2 uv = SquidSkinUv();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var body = new List<int>();
            var eye = new List<int>();
            var iris = new List<int>();
            var rng = new System.Random(7);
            float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

            // Virado para fora do ponto "dentro" (o Unity desenha a face cuja normal = (b − a) × (c − a)).
            void Tri(List<int> list, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - inside) < 0f) (b, c) = (c, b);
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
                list.Add(i); list.Add(i + 1); list.Add(i + 2);
            }

            // Perfil: (altura, raio). Base larga na linha d'água, barriga na altura dos olhos, ponta fina.
            var profile = new (float y, float r)[]
            {
                (-1.5f, 2.0f), (0.4f, 2.45f), (1.7f, 2.7f), (3.2f, 2.55f), (4.8f, 2.15f), (6.3f, 1.6f), (7.7f, 1.0f), (8.9f, 0.45f), (9.6f, 0f),
            };
            const int sides = 12;
            var rings = new Vector3[profile.Length, sides];
            for (int p = 0; p < profile.Length; p++)
                for (int s = 0; s < sides; s++)
                {
                    float a = (s + (p % 2) * 0.5f) / sides * Mathf.PI * 2f;
                    float r = profile[p].r * (1f + (p > 0 && p < profile.Length - 1 ? Noise() * 0.06f : 0f));
                    rings[p, s] = new Vector3(Mathf.Sin(a) * r, profile[p].y + (p > 0 ? Noise() * 0.12f : 0f), Mathf.Cos(a) * r);
                }
            for (int p = 0; p + 1 < profile.Length; p++)
                for (int s = 0; s < sides; s++)
                {
                    int n = (s + 1) % sides;
                    Vector3 a = rings[p, s], b = rings[p, n], c = rings[p + 1, s], d = rings[p + 1, n];
                    var axis = new Vector3(0f, (a.y + d.y) * 0.5f, 0f);
                    Tri(body, a, b, c, axis);
                    if (profile[p + 1].r > 0f) Tri(body, b, d, c, axis);
                }

            // Barbatanas: losangos dos dois lados do topo (dupla face).
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 top = new(0f, 9.3f, 0f), low = new(side * 0.9f, 6.2f, 0f), tip = new(side * 3.4f, 7.4f, -0.3f), mid = new(side * 1.2f, 8.4f, -0.1f);
                Vector3 front = new(side * 2f, 7.6f, 3f), back = new(side * 2f, 7.6f, -3f);
                Tri(body, low, tip, mid, front);
                Tri(body, low, tip, mid, back);
                Tri(body, mid, tip, top, front);
                Tri(body, mid, tip, top, back);
            }

            // Olhos: esfera escura nos lados (um pouco para a frente) e a íris como uma faixa da própria esfera.
            for (int side = -1; side <= 1; side += 2)
            {
                float yaw = side * 68f * Mathf.Deg2Rad;
                Vector3 outward = new(Mathf.Sin(yaw), 0.05f, Mathf.Cos(yaw));
                Vector3 center = new Vector3(0f, 1.7f, 0f) + outward.normalized * 2.45f;
                const float radius = 0.85f;
                Quaternion facing = Quaternion.LookRotation(outward.normalized);
                const int lat = 6, lon = 10;
                Vector3 Sphere(int i, int j, float rr)
                {
                    float th = i / (float)lat * Mathf.PI;
                    float ph = j / (float)lon * Mathf.PI * 2f;
                    var local = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th)) * rr;
                    return center + facing * local;
                }
                for (int i = 0; i < lat; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        Vector3 a = Sphere(i, j, radius), b = Sphere(i, j + 1, radius), c = Sphere(i + 1, j, radius), d = Sphere(i + 1, j + 1, radius);
                        // O anel 1 (a partir da frente) é a íris brilhante; o centro (anel 0) é a pupila escura.
                        var list = i == 1 ? iris : eye;
                        float lift = list == iris ? 1.015f : 1f;
                        if (lift != 1f)
                        {
                            a = Sphere(i, j, radius * lift); b = Sphere(i, j + 1, radius * lift);
                            c = Sphere(i + 1, j, radius * lift); d = Sphere(i + 1, j + 1, radius * lift);
                        }
                        Tri(list, a, b, c, center);
                        Tri(list, b, d, c, center);
                    }
            }

            var mesh = new Mesh { name = "Lula_Cabeca" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(eye, 1);
            mesh.SetTriangles(iris, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureFolder(Root, "Meshes");
            string path = $"{MeshesFolder}/Lula_Cabeca.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.SetVertices(verts);
            existing.SetUVs(0, uvs);
            existing.subMeshCount = 3;
            existing.SetTriangles(body, 0);
            existing.SetTriangles(eye, 1);
            existing.SetTriangles(iris, 2);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>Saque grande: baú garantido (esquema que falta ou ouro).</summary>
        static LootTable SquidLoot(LootAssets loot)
        {
            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Lula.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 300, 500), Drop(items.wood, 1f, 15, 30), Drop(items.powder, 0.8f, 8, 16), Drop(items.iron, 0.8f, 6, 12),
                };
                t.chestChance = 1f;
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 500);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            EditorUtility.SetDirty(table);
            return table;
        }

        /// <summary>Marcas das Águas Negras: dois cascos afundados e pedras num anel fora da área da luta (só visuais, sem colisor).</summary>
        static void SquidLairLandmarks()
        {
            var group = new GameObject("AguasNegras").transform;
            group.position = SquidLair;
            var wrecks = new (float angle, float distance, float tilt, float yaw)[] { (35f, 66f, 14f, 120f), (205f, 72f, -12f, 300f) };
            foreach (var (angle, distance, tilt, yaw) in wrecks)
            {
                Vector3 local = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance + Vector3.down * 1.6f;
                Kenney("ship-wreck", group, local, Quaternion.Euler(tilt * 0.4f, yaw, tilt), 1.3f, "CascoAfundado");
            }
            var rocks = new (string model, float angle, float distance, float scale)[]
            {
                ("rocks-a", 80f, 78f, 2.2f), ("rocks-b", 140f, 74f, 2.6f), ("rocks-c", 260f, 80f, 2.4f), ("rocks-b", 320f, 76f, 2f),
            };
            foreach (var (model, angle, distance, scale) in rocks)
            {
                Vector3 local = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance + Vector3.down * 0.6f;
                Kenney(model, group, local, Quaternion.Euler(0f, angle * 3f, 0f), scale, "Pedra");
            }
        }
    }
}
