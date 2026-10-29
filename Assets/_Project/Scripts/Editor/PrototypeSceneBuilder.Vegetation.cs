using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Vegetação das matas (28/10, pedido do Jhonata: "mudar a vegetação do mapa... nos pacotes que a gente tem tem outros tipos de árvores";
    /// proposta aprovada pelas folhas de contato): árvores de copa larga e verde e árvores secas das ruínas do Quaternius (Ultimate Modular
    /// Ruins, cor pela paleta das ruínas), palmeiras do Pirate Kit do Quaternius (atlas do kit), arbustos e capim das ruínas e do Pirate Kit do
    /// Kenney, pedras do Pirate Kit do Quaternius e a pedra com musgo do Low Poly Medieval (Loja). Cogumelos, troncos, tocos e flores continuam
    /// do Nature Kit. Cada planta fica dentro de um suporte de escala 1 com o giro (colisor do tronco, <see cref="Pirates.Loot.ResourceNode"/> e
    /// <see cref="Pirates.CameraSystem.CameraOccluder"/> nele): os FBX do Quaternius trazem a raiz girada e com escala 100.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        enum VegSource { Ruins, QuaterniusPirate, Pirate, Prefab }

        /// <summary>Um modelo da lista: de onde vem, escala sorteada, tronco (raio e altura do colisor na escala 1) e peso no sorteio.</summary>
        readonly struct Veg
        {
            public readonly VegSource source;
            public readonly string model;
            public readonly float minScale, maxScale, trunkRadius, trunkHeight, weight;

            public Veg(VegSource source, string model, float minScale, float maxScale, float weight = 1f, float trunkRadius = 0f, float trunkHeight = 0f)
            {
                this.source = source;
                this.model = model;
                this.minScale = minScale;
                this.maxScale = maxScale;
                this.weight = weight;
                this.trunkRadius = trunkRadius;
                this.trunkHeight = trunkHeight;
            }
        }

        const string QuaterniusModels = "Assets/ThirdParty/Quaternius_PirateKit/Models";
        const string MedievalPrefabs = "Assets/ThirdParty/Loja/LowPolyMedievalStarter/Prefabs";

        // Árvores das ruínas: ~2,8 m no modelo, 4,5–6,7 m na mata (com 1,9–2,7 a copa larga cobria o chão visto de cima). A mais cheia
        // (Tree_2, 1.816 triângulos) sai menos.
        static readonly Veg[] ForestTrees =
        {
            new(VegSource.Ruins, "Tree_1", 1.7f, 2.4f, 1.2f, 0.13f, 1.2f),
            new(VegSource.Ruins, "Tree_3", 1.6f, 2.2f, 1f, 0.13f, 1.2f),
            new(VegSource.Ruins, "Tree_2", 1.6f, 2.2f, 0.6f, 0.13f, 1.2f),
        };

        // Secas: só perto do cemitério e das ruínas da Caveira (DeadTreeZone).
        static readonly Veg[] DeadTrees =
        {
            new(VegSource.Ruins, "DeadTree_1", 2f, 2.6f, 1f, 0.1f, 1.2f),
            new(VegSource.Ruins, "DeadTree_2", 2f, 2.6f, 1f, 0.1f, 1.2f),
            new(VegSource.Ruins, "DeadTree_3", 1.9f, 2.4f, 1f, 0.12f, 1.2f),
        };

        // Palmeiras do Quaternius (~2,7–3,6 m no modelo; 6–9 m na praia, como as do Kenney de antes).
        static readonly Veg[] PalmModels =
        {
            new(VegSource.QuaterniusPirate, "Environment_PalmTree_1", 1.8f, 2.4f, 1f, 0.12f, 1.4f),
            new(VegSource.QuaterniusPirate, "Environment_PalmTree_2", 2.3f, 3f, 1f, 0.11f, 1.2f),
            new(VegSource.QuaterniusPirate, "Environment_PalmTree_3", 2.3f, 3f, 1f, 0.11f, 1.2f),
        };

        // Os Bush_1x1/2x1/2x2 das ruínas são sebes quadradas (pareciam caixas no meio da mata): ficaram de fora.
        static readonly Veg[] Bushes =
        {
            new(VegSource.Ruins, "Bush_Large", 0.8f, 1.2f, 1.5f),
            new(VegSource.Ruins, "Bush_Round", 1f, 1.5f, 1.5f),
            new(VegSource.Pirate, "grass-plant", 1.3f, 2f),
            new(VegSource.Pirate, "grass-patch", 1.2f, 1.8f),
        };

        static readonly Veg[] Grasses =
        {
            new(VegSource.Ruins, "Grass", 0.55f, 0.9f, 1.5f),
            new(VegSource.Pirate, "grass", 1.6f, 2.6f),
        };

        // Pedras do Quaternius (~0,7 m; 2,5–4,5 m na mata) e a pedra com musgo (~1,1 m).
        static readonly Veg[] BigRocks =
        {
            new(VegSource.QuaterniusPirate, "Environment_Rock_1", 3.6f, 6f),
            new(VegSource.QuaterniusPirate, "Environment_Rock_2", 2.6f, 4.2f, 0.5f),
            new(VegSource.QuaterniusPirate, "Environment_Rock_3", 3.4f, 5.8f),
            new(VegSource.QuaterniusPirate, "Environment_Rock_4", 3.4f, 5.6f),
            new(VegSource.QuaterniusPirate, "Environment_Rock_5", 3.8f, 6.4f),
            new(VegSource.Prefab, "MossyBoulder", 2.4f, 3.8f, 1.5f),
        };

        /// <summary>Perto daqui (XZ local da ilha) as árvores da mata nascem secas (75%). Posto só durante o plantio da Caveira.</summary>
        static Func<Vector2, bool> DeadTreeZone;

        /// <summary>Escolhe pelo peso com um sorteio só (0–1): a mata gasta o mesmo número de sorteios e as posições não mudam.</summary>
        static Veg PickVeg(Veg[] list, double roll)
        {
            float total = 0f;
            foreach (var v in list) total += v.weight;
            float at = (float)roll * total;
            foreach (var v in list)
            {
                if (at < v.weight) return v;
                at -= v.weight;
            }
            return list[^1];
        }

        /// <summary>
        /// Planta um modelo dentro de um suporte (escala 1, giro <paramref name="yaw"/>): o modelo vai na <paramref name="scale"/> e, com
        /// tronco, o suporte ganha a cápsula (raio e altura × escala). Devolve o suporte.
        /// </summary>
        static GameObject PlaceVeg(Veg v, Transform parent, Vector3 localPosition, float yaw, float scale, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
            switch (v.source)
            {
                case VegSource.Ruins:
                    Model(Kit.Ruins, v.model, holder.transform, Vector3.zero, 0f, scale, v.model);
                    break;
                case VegSource.Pirate:
                    Kenney(v.model, holder.transform, Vector3.zero, 0f, scale, v.model);
                    break;
                case VegSource.QuaterniusPirate:
                {
                    var go = Instance($"{QuaterniusModels}/{v.model}.fbx", holder.transform, scale);
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        var slots = r.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = QuaterniusMaterial;
                        r.sharedMaterials = slots;
                    }
                    break;
                }
                case VegSource.Prefab:
                    Instance($"{MedievalPrefabs}/{v.model}.prefab", holder.transform, scale);
                    break;
            }
            if (v.trunkRadius > 0f)
            {
                var c = holder.AddComponent<CapsuleCollider>();
                c.radius = v.trunkRadius * scale;
                c.height = v.trunkHeight * scale;
                c.center = new Vector3(0f, c.height * 0.5f, 0f);
            }
            return holder;
        }

        /// <summary>Cópia solta (sem ligação de prefab) com a raiz do asset por baixo da escala; sem colisor nem script.</summary>
        static GameObject Instance(string path, Transform parent, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new System.IO.FileNotFoundException($"Modelo de vegetação não encontrado: {path}");
            var go = (GameObject)Object.Instantiate(asset, parent, false);
            go.name = asset.name;
            go.transform.SetLocalPositionAndRotation(Vector3.zero, asset.transform.localRotation);
            go.transform.localScale = asset.transform.localScale * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(m);
            return go;
        }

        /// <summary>Colisor de pedra: a malha de cada parte (só a mais detalhada quando o modelo tem LOD).</summary>
        static void RockColliders(GameObject rock)
        {
            foreach (var filter in rock.GetComponentsInChildren<MeshFilter>())
            {
                string mesh = filter.sharedMesh ? filter.sharedMesh.name : "";
                if (mesh.Contains("_LOD") && !mesh.EndsWith("_LOD0")) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }
    }
}
