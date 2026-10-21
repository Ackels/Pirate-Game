using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Modelos do Kenney Pirate Kit (CC0, Assets/ThirdParty/Kenney_PirateKit): todos usam uma textura só (colormap,
    /// uma paleta), então um material URP serve para todos. Os modelos são só visual: colisores e física continuam os
    /// dos protótipos. As peças que ainda são primitivas (tábuas, saco, padlock) usam cores tiradas da mesma paleta.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string KenneyFolder = "Assets/ThirdParty/Kenney_PirateKit/Models";

        // Navios: o jogador é pirata (velas pretas com caveira); os inimigos, de velas brancas.
        const string PlayerShipModel = "ship-pirate-small", EnemyShipModel = "ship-small";
        /// <summary>
        /// Quilha do modelo abaixo da raiz do navio. A raiz boia ~0,13 m abaixo da água (medido): assim ~0,9 m de casco
        /// fica submerso e o convés (2,1 m acima da quilha) fica ~1,2 m acima da água.
        /// </summary>
        const float ShipModelKeel = -0.8f;
        /// <summary>Altura do convés no espaço do navio (quilha + 2,1 m do modelo).</summary>
        const float ShipDeckY = ShipModelKeel + 2.1f;

        static Material KenneyMaterial => GetTexturedMaterial("M_Kenney",
            AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyFolder + "/Textures/colormap.png"), 0.12f);

        /// <summary>Cores da paleta do Kenney para as primitivas que sobraram.</summary>
        static class KenneyColors
        {
            public static Material WoodLight => GetMaterial("M_K_MadeiraClara", new Color(0.93f, 0.6f, 0.43f), 0.12f);
            public static Material WoodDark => GetMaterial("M_K_MadeiraEscura", new Color(0.66f, 0.38f, 0.25f), 0.12f);
            public static Material Iron => GetMaterial("M_K_Ferro", new Color(0.24f, 0.24f, 0.27f), 0.4f);
            public static Material Sail => GetMaterial("M_K_Vela", new Color(0.92f, 0.92f, 0.96f), 0.05f);
            public static Material Gold => GetMaterial("M_K_Ouro", new Color(1f, 0.76f, 0.27f), 0.6f);
            public static Material Cloth => GetMaterial("M_K_Pano", new Color(0.96f, 0.85f, 0.68f), 0.05f);
            public static Material Stone => GetMaterial("M_K_Pedra", new Color(0.53f, 0.55f, 0.63f), 0.15f);
        }

        /// <summary>Põe um modelo do kit (cópia, sem ligação de prefab) com o material da paleta.</summary>
        static GameObject Kenney(string model, Transform parent, Vector3 localPosition, float yaw = 0f, float scale = 1f,
            string objectName = null) =>
            Kenney(model, parent, localPosition, Quaternion.Euler(0f, yaw, 0f), scale, objectName);

        static GameObject Kenney(string model, Transform parent, Vector3 localPosition, Quaternion localRotation, float scale,
            string objectName) =>
            Model(Kit.Pirate, model, parent, localPosition, localRotation, scale, objectName);

        /// <summary>
        /// Kits do Kenney em Assets/ThirdParty (CC0). Pirate, Town (Fantasy Town Kit: casas modulares, cercas, feira) e
        /// Survival (fogueiras, tendas, caixas, ferramentas) pintam com uma paleta cada (um material por kit). Nature (árvores,
        /// arbustos, flores, troncos) pinta com uma cor por material: o gerador troca pelas cópias compartilhadas M_N_*.
        /// 19/10 (ruínas): Castle (Castle Kit: torres, muralhas, escadas, pontes) e Graveyard (Graveyard Kit: criptas, túmulos,
        /// grades, colunas) com paleta, e Ruins (Ultimate Modular Ruins do Quaternius: paredes com arco gótico, colunas, pisos,
        /// estátuas, versões com mato; grade de 1 m) com cor por material, nas cópias M_R_*.
        /// </summary>
        enum Kit { Pirate, Town, Survival, Nature, Castle, Graveyard, Ruins }

        static string KitFolder(Kit kit) => kit switch
        {
            Kit.Town => "Assets/ThirdParty/Kenney_FantasyTownKit/Models",
            Kit.Survival => "Assets/ThirdParty/Kenney_SurvivalKit/Models",
            Kit.Nature => "Assets/ThirdParty/Kenney_NatureKit/Models",
            Kit.Castle => "Assets/ThirdParty/Kenney_CastleKit/Models",
            Kit.Graveyard => "Assets/ThirdParty/Kenney_GraveyardKit/Models",
            Kit.Ruins => "Assets/ThirdParty/Quaternius_ModularRuins/Models",
            _ => KenneyFolder,
        };

        static Material KitMaterial(Kit kit) => kit switch
        {
            Kit.Pirate => KenneyMaterial,
            Kit.Nature or Kit.Ruins => null,
            _ => GetTexturedMaterial(kit switch { Kit.Town => "M_KenneyTown", Kit.Castle => "M_KenneyCastle", Kit.Graveyard => "M_KenneyGraveyard", _ => "M_KenneySurvival" },
                AssetDatabase.LoadAssetAtPath<Texture2D>(KitFolder(kit) + "/Textures/colormap.png"), 0.12f),
        };

        static GameObject Model(Kit kit, string model, Transform parent, Vector3 localPosition, float yaw = 0f, float scale = 1f,
            string objectName = null) =>
            Model(kit, model, parent, localPosition, Quaternion.Euler(0f, yaw, 0f), scale, objectName);

        /// <summary>Põe um modelo de um kit (cópia, sem ligação de prefab, sem colisor) com o material do kit.</summary>
        static GameObject Model(Kit kit, string model, Transform parent, Vector3 localPosition, Quaternion localRotation, float scale,
            string objectName)
        {
            string path = $"{KitFolder(kit)}/{model}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new System.IO.FileNotFoundException($"Modelo do kit não encontrado: {path} (copie do _Downloads).");
            var go = (GameObject)Object.Instantiate(asset, parent, false);
            go.name = objectName ?? model;
            // A raiz dos FBX do Blender (ruínas do Quaternius) vem girada −90° em X e com escala 100: mantém por baixo do nosso giro e escala
            // (nos kits do Kenney a raiz é neutra e nada muda).
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation * asset.transform.localRotation);
            go.transform.localScale = asset.transform.localScale * scale;
            var material = KitMaterial(kit);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = material ? material : kit == Kit.Ruins ? RuinsMaterial(slots[i]) : NatureMaterial(slots[i]);
                renderer.sharedMaterials = slots;
            }
            return go;
        }

        /// <summary>Cópia compartilhada (em _Project/Materials) do material de cor do Nature Kit: um por cor, não um por modelo.</summary>
        static Material NatureMaterial(Material imported)
        {
            if (!imported) return KenneyColors.WoodDark;
            var color = imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : imported.color;
            return GetMaterial("M_N_" + imported.name, color, 0.1f);
        }

        /// <summary>
        /// Paleta das ruínas do Quaternius (19/10): as cores do Blender vêm quase pretas e o mato branco; aqui a pedra é quente (bege/ocre,
        /// como as referências do Jhonata), o musgo e as folhas verdes. Por nome do material do FBX; reaplicada a cada geração.
        /// </summary>
        static readonly Dictionary<string, Color> RuinsPalette = new()
        {
            ["Main"] = new Color(0.63f, 0.56f, 0.46f), ["Highlights"] = new Color(0.75f, 0.68f, 0.56f), ["Main2"] = new Color(0.55f, 0.49f, 0.4f),
            ["Black"] = new Color(0.17f, 0.15f, 0.13f), ["Stone"] = new Color(0.66f, 0.63f, 0.57f),
            ["Green"] = new Color(0.36f, 0.52f, 0.24f), ["Leaf_Texture"] = new Color(0.3f, 0.5f, 0.22f), ["Texture_Leaves"] = new Color(0.32f, 0.54f, 0.24f),
            ["Bark"] = new Color(0.36f, 0.27f, 0.19f), ["Dirt"] = new Color(0.38f, 0.29f, 0.19f),
            ["Wood"] = new Color(0.5f, 0.34f, 0.2f), ["DarkWood"] = new Color(0.32f, 0.22f, 0.14f),
            ["Metal"] = new Color(0.32f, 0.33f, 0.36f), ["DarkMetal"] = new Color(0.16f, 0.16f, 0.18f), ["Metal_Light"] = new Color(0.55f, 0.57f, 0.6f),
            ["Gold"] = new Color(0.88f, 0.68f, 0.28f), ["Flag"] = new Color(0.55f, 0.12f, 0.08f),
            ["Pot_Main"] = new Color(0.66f, 0.43f, 0.26f), ["Pot_Detail"] = new Color(0.58f, 0.22f, 0.13f),
            ["Bone"] = new Color(0.86f, 0.81f, 0.69f), ["Candle"] = new Color(0.92f, 0.87f, 0.68f),
            ["Light"] = new Color(1f, 0.6f, 0.25f), ["Fire"] = new Color(1f, 0.5f, 0.15f),
            // Piso (mais escuro e frio que as paredes: vista de cima, a câmera precisa separar o chão do alto das paredes).
            ["Piso_Main"] = new Color(0.36f, 0.34f, 0.31f), ["Piso_Highlights"] = new Color(0.45f, 0.42f, 0.37f), ["Piso_Main2"] = new Color(0.4f, 0.37f, 0.33f),
            ["Piso_Dirt"] = new Color(0.33f, 0.25f, 0.17f), ["Piso_Fundo"] = new Color(0.17f, 0.16f, 0.14f),
            // Catacumbas da Ermida (C5): pedra fria e escura nas paredes; o piso mais escuro ainda.
            ["Cripta_Main"] = new Color(0.44f, 0.44f, 0.46f), ["Cripta_Highlights"] = new Color(0.52f, 0.52f, 0.53f),
            ["Cripta_Main2"] = new Color(0.4f, 0.4f, 0.42f), ["Cripta_Stone"] = new Color(0.48f, 0.47f, 0.46f),
            ["Cripta_Black"] = new Color(0.1f, 0.1f, 0.11f), ["Cripta_Green"] = new Color(0.26f, 0.36f, 0.24f),
            ["CriptaPiso_Main"] = new Color(0.27f, 0.27f, 0.28f), ["CriptaPiso_Highlights"] = new Color(0.33f, 0.33f, 0.33f),
            ["CriptaPiso_Main2"] = new Color(0.3f, 0.29f, 0.29f), ["CriptaPiso_Dirt"] = new Color(0.24f, 0.2f, 0.16f),
            ["CriptaPiso_Stone"] = new Color(0.3f, 0.3f, 0.3f), ["CriptaPiso_Green"] = new Color(0.22f, 0.3f, 0.2f),
            ["CriptaPiso_Fundo"] = new Color(0.16f, 0.15f, 0.14f),
        };

        /// <summary>Troca os materiais de uma peça das ruínas pela variante com prefixo (ex.: "Piso_"), quando a paleta tem.</summary>
        static void RuinsRecolor(Transform piece, string prefix)
        {
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i] || !slots[i].name.StartsWith("M_R_")) continue;
                    string key = prefix + slots[i].name.Substring(4);
                    if (RuinsPalette.ContainsKey(key)) slots[i] = RuinsMaterial(key);
                }
                r.sharedMaterials = slots;
            }
        }

        static Material RuinsMaterial(Material imported)
        {
            string name = imported ? imported.name : "Main";
            var fallback = imported ? (imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : imported.color) : Color.gray;
            return RuinsMaterial(name, fallback);
        }

        /// <summary>Material da paleta das ruínas pelo nome ("Main", "Highlights"...): também para as primitivas que completam as peças.</summary>
        static Material RuinsMaterial(string name, Color fallback = default)
        {
            if (!RuinsPalette.TryGetValue(name, out var color)) color = fallback == default ? Color.gray : fallback;
            var mat = GetMaterial("M_R_" + name, color, 0.1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (name is "Fire" or "Light")
            {
                mat.SetColor("_EmissionColor", color * 2.2f);
                Emissive(mat);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Cada vela do modelo ("sail-a", "sail-b") ganha um eixo no topo dela: encolher o eixo em Y enrola o pano para
        /// cima, junto da verga (o pivô original fica embaixo da vela).
        /// </summary>
        static Transform[] FurlableSails(Transform model)
        {
            var pivots = new List<Transform>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var sail = filter.transform;
                if (!sail.name.StartsWith("sail")) continue;
                var pivot = new GameObject(sail.name + "_Eixo").transform;
                pivot.SetParent(sail.parent, false);
                pivot.SetPositionAndRotation(sail.TransformPoint(new Vector3(0f, filter.sharedMesh.bounds.max.y, 0f)), sail.rotation);
                sail.SetParent(pivot, true);
                pivots.Add(pivot);
            }
            return pivots.ToArray();
        }

        /// <summary>
        /// Mede o convés de um navio do kit com raios contra a malha do casco (altura do convés e largura do casco em cada
        /// ponto), para os canhões sentarem no convés certo (principal, castelo de proa ou popa) sem furar a proa estreita.
        /// O MeshCollider vai num objeto à parte, sem Rigidbody (malha côncava em corpo que se mexe não colide), e some no fim.
        /// Coordenadas no espaço do modelo (quilha em y = 0).
        /// </summary>
        sealed class DeckProbe : System.IDisposable
        {
            /// <summary>Raios descem de logo abaixo da verga mais baixa (3,14 m): só o convés e as amuradas.</summary>
            const float StartHeight = 3.05f;

            readonly GameObject probe;
            readonly MeshCollider collider;

            public DeckProbe(Transform model)
            {
                probe = new GameObject("SondaConves");
                probe.transform.SetPositionAndRotation(model.position, model.rotation);
                probe.transform.localScale = model.lossyScale;
                collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = model.GetComponent<MeshFilter>().sharedMesh;
                Physics.SyncTransforms();
            }

            /// <summary>Altura do convés em (x, z); NaN se ali não há casco.</summary>
            public float DeckHeight(float x, float z, float startHeight = StartHeight)
            {
                float top = float.NaN;
                var from = probe.transform.TransformPoint(new Vector3(x, startHeight, z));
                foreach (var hit in Physics.RaycastAll(from, -probe.transform.up, startHeight + 1f))
                {
                    if (hit.collider != collider) continue;
                    float y = probe.transform.InverseTransformPoint(hit.point).y;
                    if (float.IsNaN(top) || y > top) top = y;
                }
                return top;
            }

            /// <summary>Meia largura do casco na altura y, em z (raio de fora para dentro); NaN se não pegou o casco.</summary>
            public float HalfWidth(float y, float z)
            {
                float half = float.NaN;
                var from = probe.transform.TransformPoint(new Vector3(5f, y, z));
                foreach (var hit in Physics.RaycastAll(from, -probe.transform.right, 5f))
                {
                    if (hit.collider != collider) continue;
                    float x = probe.transform.InverseTransformPoint(hit.point).x;
                    if (float.IsNaN(half) || x > half) half = x;
                }
                return half;
            }

            /// <summary>Um raio de lado na altura y, em z, acerta o casco perto do meio (|x| ≤ <paramref name="half"/>)? = mastro (M12).</summary>
            public bool HitsNearCenter(float y, float z, float half)
            {
                var from = probe.transform.TransformPoint(new Vector3(5f, y, z));
                foreach (var hit in Physics.RaycastAll(from, -probe.transform.right, 10f))
                {
                    if (hit.collider != collider) continue;
                    if (Mathf.Abs(probe.transform.InverseTransformPoint(hit.point).x) <= half) return true;
                }
                return false;
            }

            static readonly Collider[] overlaps = new Collider[64];

            /// <summary>Uma esfera (centro no espaço do mundo) encosta no casco? Amurada, mastro, parede da cabine (M12).</summary>
            public bool Touches(Vector3 worldCenter, float radius)
            {
                int n = Physics.OverlapSphereNonAlloc(worldCenter, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                    if (overlaps[i] == collider) return true;
                return false;
            }

            public void Dispose() => Object.DestroyImmediate(probe);
        }

        /// <summary>Material URP Lit com textura (criado uma vez em _Project/Materials).</summary>
        static Material GetTexturedMaterial(string name, Texture2D texture, float smoothness)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
