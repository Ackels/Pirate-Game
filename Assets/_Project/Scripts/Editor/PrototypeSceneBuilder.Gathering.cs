using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Cortar árvores (D1, passo 1, 28/10; plano de 16/10: "cortar árvores e minerar"): a machadinha (item no mercado, 80 de ouro, uma vez),
    /// o estado "Coletar" do Animator ("MiningOneHand01_R - Wall" do Human Crafting Animations, Kevin Iglesias: machadada de uma mão), a
    /// machadinha na mão direita do capitão (Survival Kit do Kenney, desligada fora da coleta) e o <see cref="Gatherer"/>, o
    /// <see cref="ResourceNode"/> em todas as árvores e palmeiras das matas (<see cref="PlantLush"/>) e o <see cref="GatheringHUD"/>.
    /// Passo 2 (28/10): minerar com a picareta ("MiningOneHand01_R - Ground", estado "Minerar") os veios de ferro: uma em cada três pedras
    /// grandes das matas vira pedra escura com cristais de minério (<see cref="AddOreVeins"/>); a pedra fica, só o minério some e volta.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string ClipChop = "MiningOneHand01_R - Wall";
        const string ClipMine = "MiningOneHand01_R - Ground";
        /// <summary>Do começo do clipe ao golpe pegar (a mão para de repente; medido no capitão: 0,40 s de 0,97; o do chão igual).</summary>
        const float ChopImpact = 0.4f;
        /// <summary>Machadinha do Survival Kit (0,26 m) × isto: ~0,65 m, do tamanho das outras armas do capitão (cabeçudo).</summary>
        const float HatchetScale = 2.5f;
        const float PickaxeScale = 2.5f;

        // Árvore: 5 golpes, 3–4 madeiras; palmeira: 4 golpes, 2–3. Voltam em 8 min (com a câmera longe).
        static readonly Vector2Int TreeWood = new(3, 4), PalmWood = new(2, 3);
        const int TreeHits = 5, PalmHits = 4;
        // Veio: 6 picaretadas, 2–3 de ferro (o ferro vale 8–20 de ouro e ocupa 3 de espaço); o minério volta em 15 min.
        static readonly Vector2Int VeinIron = new(2, 3);
        const int VeinHits = 6;
        const float VeinRegrow = 900f;
        /// <summary>Uma em cada tantas pedras grandes das matas vira veio.</summary>
        const int VeinEvery = 3;

        static ItemDefinition HatchetItem() => Item("Equip_Machadinha", "Machadinha", ItemCategory.Gear, ItemRarity.Common, new Color(0.62f, 0.5f, 0.38f), 0f,
            "Machadinha de lenhador. A pé, perto de uma árvore ou palmeira das matas, F corta: alguns golpes e ela tomba, e a madeira vai para " +
            "a mochila. As árvores voltam a crescer depois de um tempo.");

        static ItemDefinition WoodItem() => Item("Item_Madeira", "Madeira", ItemCategory.Resource, ItemRarity.Common, new Color(0.85f, 0.64f, 0.42f), 1f,
            "Conserta o casco e serve para construir na base.");

        static ItemDefinition PickaxeItem() => Item("Equip_Picareta", "Picareta", ItemCategory.Gear, ItemRarity.Common, new Color(0.6f, 0.6f, 0.66f), 0f,
            "Picareta de minerador. A pé, perto de um veio de ferro (pedra grande com pedras cor de ferrugem em volta), F minera: algumas " +
            "picaretadas e o ferro vai para a mochila. O minério volta a aparecer depois de um tempo.");

        /// <summary>O ferro que já existe (Loot.cs): o veio dá o mesmo item do saque.</summary>
        static ItemDefinition IronItem() => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/Item_Ferro.asset");

        // ---------- Mercado ----------

        /// <summary>Machadinha e picareta à venda (uma vez cada), se ainda não estão na lista.</summary>
        static void AddGatheringMarket(HarborSettings settings)
        {
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            int before = list.Count;
            var hatchet = HatchetItem();
            if (!list.Exists(e => e != null && e.item == hatchet)) list.Add(Market(hatchet, 80, 0));
            var pickaxe = PickaxeItem();
            if (!list.Exists(e => e != null && e.item == pickaxe)) list.Add(Market(pickaxe, 120, 0));
            if (list.Count == before) return;
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        // ---------- Animator ----------

        /// <summary>
        /// "Coletar" (machadada na parede) e "Minerar" (picaretada no chão): em loop enquanto o bool "Gathering" estiver ligado (entram
        /// pelo CharacterAnimatorDriver.PlayGather).
        /// </summary>
        static void AddGatherStates(AnimatorStateMachine sm, AnimatorState empty)
        {
            AddGatherState(sm, empty, "Coletar", ClipChop, new Vector3(-300f, -760f));
            AddGatherState(sm, empty, "Minerar", ClipMine, new Vector3(-300f, -700f));
        }

        static void AddGatherState(AnimatorStateMachine sm, AnimatorState empty, string name, string clipName, Vector3 at)
        {
            var clip = KevinClip(clipName);
            if (!clip)
            {
                Debug.LogWarning("Coleta: clipe " + clipName + " não encontrado em " + KevinWorkFolder);
                return;
            }
            var state = sm.AddState(name, at);
            state.motion = clip;
            state.tag = MovingFullBodyTag;
            Go(state, empty, 0.25f, ("Gathering", AnimatorConditionMode.IfNot, 0));
        }

        // ---------- No capitão ----------

        /// <summary>
        /// Machadinha na mão direita (desligada), o <see cref="Gatherer"/> e o HUD. A pegada sai do clipe no capitão: na hora do golpe o cabo
        /// vai da mão para a frente (e um pouco para cima, até o tronco) e o fio da lâmina na direção em que a mão vinha.
        /// </summary>
        static void AddGatherer(GameObject character)
        {
            var model = character.transform.Find("Model");
            // No kit: cabo em +Y (base na origem), lâmina/cabeça para +X. A mão pega uns 10 cm acima da base do cabo.
            var hatchet = ToolInHand(model, "Machadinha", "tool-axe", HatchetScale, ClipChop, 0.35f);
            // A picareta bate no chão: no impacto o cabo vai para a frente e um pouco para baixo (a mão fica a ~20 cm do chão).
            var pickaxe = ToolInHand(model, "Picareta", "tool-pickaxe", PickaxeScale, ClipMine, -0.2f);

            var gatherer = character.AddComponent<Gatherer>();
            gatherer.Configure(new[]
            {
                new Gatherer.Tool { kind = ResourceNode.Kind.Tree, item = HatchetItem(), visual = hatchet, name = "machadinha", verb = "Cortar" },
                new Gatherer.Tool { kind = ResourceNode.Kind.Ore, item = PickaxeItem(), visual = pickaxe, name = "picareta", verb = "Minerar", ground = true },
            });
            BakeToolIcons();
            AttachGatheringHud();
            // Os veios entram aqui (só nas cenas a pé, 06 e 07): as matas já estão plantadas.
            AddOreVeins();
        }

        /// <summary>Ferramenta do Survival Kit na mão direita, desligada (o Gatherer liga ao coletar), na pegada medida no clipe dela.</summary>
        static GameObject ToolInHand(Transform model, string name, string kitModel, float scale, string clipName, float handleRise)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            ChopGrip(model, clipName, handleRise, out Vector3 arm, out Vector3 handle, out Vector3 edge);
            var root = new GameObject(name).transform;
            root.SetParent(hand, false);
            root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.07f), Quaternion.LookRotation(Vector3.Cross(edge, handle), handle));
            var tool = Model(Kit.Survival, kitModel, root, new Vector3(0f, -0.1f, 0f), Quaternion.identity, scale, "Modelo");
            foreach (var r in tool.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            UndoHandScale(root, hand);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        /// <summary>
        /// Eixos no espaço do osso da mão direita no impacto do clipe: antebraço (para a mão), cabo (para a frente, subindo
        /// <paramref name="handleRise"/>) e fio (na direção em que a mão vinha).
        /// </summary>
        static void ChopGrip(Transform model, string clipName, float handleRise, out Vector3 arm, out Vector3 handle, out Vector3 edge)
        {
            arm = Vector3.right;
            handle = Vector3.forward;
            edge = Vector3.up;
            var clip = KevinClip(clipName);
            var temp = clip ? MeasureCopy(model) : null;
            if (!temp) return;
            var anim = temp.GetComponent<Animator>();
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var fore = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            clip.SampleAnimation(temp, ChopImpact - 0.05f);
            Vector3 before = hand.position;
            clip.SampleAnimation(temp, ChopImpact);
            Vector3 motion = hand.position - before;
            Vector3 handleWorld = (Vector3.forward + Vector3.up * handleRise).normalized;
            Vector3 edgeWorld = Vector3.ProjectOnPlane(motion, handleWorld).normalized;
            if (edgeWorld.sqrMagnitude < 0.5f) edgeWorld = Vector3.ProjectOnPlane(Vector3.down, handleWorld).normalized;
            var inverse = Quaternion.Inverse(hand.rotation);
            arm = inverse * (hand.position - fore.position).normalized;
            handle = inverse * handleWorld;
            edge = inverse * edgeWorld;
            Object.DestroyImmediate(temp);
        }

        /// <summary>O HUD da coleta (se ele já existe; senão o CreateGameHud põe).</summary>
        static void AttachGatheringHud()
        {
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            var gatherer = Object.FindAnyObjectByType<Gatherer>(FindObjectsInactive.Include);
            if (!hud || !gatherer || hud.GetComponent<GatheringHUD>()) return;
            hud.gameObject.AddComponent<GatheringHUD>().Bind(gatherer);
        }

        // ---------- Árvores ----------

        static GameObject stumpTemplate;
        static float stumpRadius;

        /// <summary>Toco das árvores cortadas: um molde desligado por cena ("Coleta/Toco_Molde"), copiado pela árvore na primeira vez.</summary>
        static GameObject StumpTemplate()
        {
            if (stumpTemplate && stumpTemplate.scene == SceneManager.GetActiveScene()) return stumpTemplate;
            var root = new GameObject("Coleta").transform;
            stumpTemplate = Model(Kit.Nature, "stump_round", root, Vector3.zero, 0f, 1f, "Toco_Molde");
            var renderers = stumpTemplate.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            stumpRadius = Mathf.Max(0.01f, Mathf.Max(b.extents.x, b.extents.z));
            stumpTemplate.SetActive(false);
            return stumpTemplate;
        }

        /// <summary>Árvore ou palmeira da mata vira um nó de coleta (o tronco tem <paramref name="radius"/> m).</summary>
        static void Choppable(GameObject tree, float radius, bool palm)
        {
            var template = StumpTemplate();
            tree.AddComponent<ResourceNode>().Configure(ResourceNode.Kind.Tree, WoodItem(), palm ? PalmWood : TreeWood, palm ? PalmHits : TreeHits,
                radius, template, radius * 1.25f / stumpRadius, tree.GetComponent<Pirates.CameraSystem.CameraOccluder>());
        }

        // ---------- Veios de ferro (passo 2) ----------

        /// <summary>Pedregulhos cinza-escuros no pé do veio (Nature Kit; ficam quando o minério acaba).</summary>
        static readonly string[] VeinRubble = { "rock_smallA", "rock_smallB", "rock_smallC", "rock_smallD", "rock_smallE", "rock_smallF" };
        const int CrystalVariants = 4;

        /// <summary>
        /// Minério de ferro (28/10, refeito pela referência do Jhonata: "cristais" facetados cor de cobre-ferrugem cravados numa pedra
        /// escura): laranja-ferrugem com um pouco de brilho, para as faces pegarem a luz. Cor reaplicada a cada geração.
        /// </summary>
        static Material OreMaterial()
        {
            var color = new Color(0.86f, 0.42f, 0.17f);
            var mat = GetMaterial("M_Minerio_Ferro", color, 0.6f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Pedra do veio: cinza-carvão quente e fosco (a pedra clara das matas não deixava o minério aparecer; 0,27 ficava azulada).</summary>
        static Material VeinRockMaterial()
        {
            var color = new Color(0.37f, 0.35f, 0.34f);
            var mat = GetMaterial("M_Veio_Rocha", color, 0.12f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Cristal de minério: bipirâmide larga de 5 ou 6 lados com uma coroa lapidada e o anel e as pontas tortos (variante = semente),
        /// facetada (cada triângulo com os vértices dele), ~1 m de largura e ~0,9 m de altura com o anel na origem: a ponta de cima sai da
        /// pedra, a de baixo fica enterrada. Primeira versão alta e fina parecia espinho.
        /// </summary>
        static Mesh OreCrystalMesh(int variant)
        {
            var rng = new System.Random(4211 + variant * 97);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int sides = variant % 2 == 0 ? 6 : 5;
            var ring = new Vector3[sides];
            float turn = R(0f, 60f);
            for (int i = 0; i < sides; i++)
            {
                float a = (turn + i * 360f / sides + R(-12f, 12f)) * Mathf.Deg2Rad;
                float r = R(0.46f, 0.6f);
                ring[i] = new Vector3(Mathf.Cos(a) * r, R(-0.06f, 0.06f), Mathf.Sin(a) * r);
            }
            // Um segundo anel um pouco acima deixa a ponta "lapidada" (mais faces pegando a luz, como na referência).
            var shoulder = new Vector3[sides];
            for (int i = 0; i < sides; i++)
                shoulder[i] = Vector3.Scale(ring[i], new Vector3(0.6f, 1f, 0.6f)) + Vector3.up * R(0.22f, 0.3f);
            var top = new Vector3(R(-0.08f, 0.08f), R(0.42f, 0.54f), R(-0.08f, 0.08f));
            var bottom = new Vector3(R(-0.05f, 0.05f), -0.4f, R(-0.05f, 0.05f));

            var vertices = new List<Vector3>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
            }
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                Tri(ring[i], shoulder[i], ring[n]);
                Tri(ring[n], shoulder[i], shoulder[n]);
                Tri(shoulder[i], top, shoulder[n]);
                Tri(ring[n], bottom, ring[i]);
            }
            var mesh = new Mesh { name = $"Minerio_Ferro_{variant}" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[vertices.Count]));
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveIslandMesh(mesh, $"{MeshesFolder}/Minerio_Ferro_{variant}.asset");
        }

        /// <summary>
        /// Uma em cada <see cref="VeinEvery"/> pedras grandes das matas ("Pedra_k" dentro de "Mata", feitas pelo <see cref="PlantLush"/>)
        /// vira veio (28/10, refeito pela imagem de referência do Jhonata): a pedra fica cinza-carvão e ganha 16–24 cristais de minério
        /// cravados por toda a face que se vê (raios de fora para dentro só contra ela, de lado até quase de cima; 4 grandes e o resto
        /// menor, quase encostando), com o eixo saindo pela normal; no pé, pedregulhos escuros e uns cristais soltos no chão. Os
        /// cristais ficam no objeto "Veio" (sem colisor), o <see cref="ResourceNode"/> de minério na pedra mexe só nele; os pedregulhos
        /// ficam. A pedra, o colisor e o NavMesh não mudam (nada mais do mapa sai do lugar).
        /// </summary>
        static void AddOreVeins()
        {
            var iron = IronItem();
            if (!iron) return;
            var material = OreMaterial();
            var rockMaterial = VeinRockMaterial();
            var crystals = new Mesh[CrystalVariants];
            for (int v = 0; v < crystals.Length; v++) crystals[v] = OreCrystalMesh(v);
            Physics.SyncTransforms();
            int veins = 0;
            foreach (var rock in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!rock.name.StartsWith("Pedra_") || !rock.parent || rock.parent.name != "Mata") continue;
                if (!int.TryParse(rock.name.Substring(6), out int index) || index % VeinEvery != 0) continue;
                if (rock.GetComponent<ResourceNode>()) continue;
                var colliders = rock.GetComponentsInChildren<Collider>();
                if (colliders.Length == 0) continue;
                var renderers = rock.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                foreach (var r in renderers)
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = rockMaterial;
                    r.sharedMaterials = slots;
                }

                var rng = new System.Random(index * 7919 + Mathf.RoundToInt(rock.position.x * 13f + rock.position.z * 7f));
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                var vein = new GameObject("Veio").transform;
                vein.SetParent(rock, false);
                vein.position = rock.position;
                float ground = rock.position.y;
                float reach = bounds.extents.magnitude + 1f;
                var placed = new List<(Vector3 at, float size)>();

                bool Hit(Ray ray, float length, out RaycastHit face)
                {
                    face = default;
                    float best = float.MaxValue;
                    foreach (var c in colliders)
                        if (c.Raycast(ray, out RaycastHit hit, length) && hit.distance < best)
                        {
                            best = hit.distance;
                            face = hit;
                        }
                    return best < float.MaxValue;
                }

                bool Free(Vector3 at, float size)
                {
                    foreach (var (other, otherSize) in placed)
                        if ((other - at).sqrMagnitude < Mathf.Pow((size + otherSize) * 0.36f, 2f)) return false;
                    return true;
                }

                void Crystal(Transform parent, Vector3 at, Vector3 normal, float size, string name)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    var up = Quaternion.AngleAxis(R(-22f, 22f), Vector3.Cross(normal, new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f))).normalized) * normal;
                    go.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, R(0f, 360f), 0f));
                    go.transform.localScale = new Vector3(size * R(0.85f, 1.1f), size, size * R(0.85f, 1.1f));
                    go.AddComponent<MeshFilter>().sharedMesh = crystals[rng.Next(crystals.Length)];
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                    placed.Add((at, size));
                }

                // Na face da pedra, de lado até quase de cima, bem juntos como na referência: 4 grandes, 7 médios e o resto pequeno.
                // Pedra grande = cristais maiores (num pedregulho de 4 m os de 1 m sumiam).
                float grow = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) / 1.6f, 0.85f, 1.4f);
                int count = Mathf.Clamp(Mathf.RoundToInt(bounds.size.x * bounds.size.z * 1.1f), 16, 28);
                int made = 0;
                for (int attempt = 0; attempt < 200 && made < count; attempt++)
                {
                    float size = grow * (made < 4 ? R(0.75f, 1.1f) : made < 11 ? R(0.45f, 0.7f) : R(0.25f, 0.42f));
                    float a = R(0f, 360f) * Mathf.Deg2Rad, e = R(4f, 75f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * Mathf.Cos(e));
                    // Mira na altura que aparece (várias pedras têm 1–1,7 m enterrados: mirar no meio da caixa mandava os raios para o chão).
                    var aim = new Vector3(bounds.center.x, ground + (bounds.max.y - ground) * R(0.2f, 0.65f), bounds.center.z);
                    if (!Hit(new Ray(aim + dir * reach, -dir), reach * 2f, out RaycastHit face)) continue;
                    if (face.point.y < ground + size * 0.25f || !Free(face.point, size)) continue;
                    Crystal(vein, face.point - face.normal * size * 0.18f, face.normal, size, $"Minerio_{made}");
                    made++;
                }

                // No pé: pedregulhos escuros (ficam) e 2–3 cristais soltos no chão (somem com o minério).
                var rubble = new GameObject("Pedregulhos").transform;
                rubble.SetParent(rock, false);
                int loose = rng.Next(5, 8);
                for (int k = 0; k < loose; k++)
                {
                    float a = R(0f, 360f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var from = new Vector3(rock.position.x, ground + 0.25f, rock.position.z) + dir * reach;
                    if (!Hit(new Ray(from, -dir), reach * 2f, out RaycastHit face)) continue;
                    var spot = face.point + dir * R(0.15f, 0.55f);
                    float floor = ground;
                    foreach (var h in Physics.RaycastAll(spot + Vector3.up * 3f, Vector3.down, 8f))
                        if (System.Array.IndexOf(colliders, h.collider) < 0 && h.point.y > floor - 1.5f)
                        {
                            floor = h.point.y;
                            break;
                        }
                    spot.y = floor;
                    if (k < 3)
                    {
                        float size = R(0.22f, 0.34f);
                        if (Free(spot, size)) Crystal(vein, spot + Vector3.up * size * 0.15f, Vector3.up, size, $"Minerio_Chao_{k}");
                        continue;
                    }
                    var stone = Model(Kit.Nature, VeinRubble[rng.Next(VeinRubble.Length)], rubble, Vector3.zero, 0f, 1f, $"Pedregulho_{k}");
                    var stoneBounds = stone.GetComponentInChildren<Renderer>().bounds;
                    float stoneSize = Mathf.Max(stoneBounds.size.x, Mathf.Max(stoneBounds.size.y, stoneBounds.size.z));
                    stone.transform.localScale *= R(0.3f, 0.55f) / Mathf.Max(0.01f, stoneSize);
                    stone.transform.SetPositionAndRotation(spot, Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)));
                    foreach (var r in stone.GetComponentsInChildren<Renderer>())
                    {
                        var slots = r.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = rockMaterial;
                        r.sharedMaterials = slots;
                    }
                }
                if (vein.childCount == 0)
                {
                    Object.DestroyImmediate(vein.gameObject);
                    continue;
                }
                foreach (var t in rock.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
                // De novo: o translúcido da pedra passa a levar a pedra escura e o minério (o Occluder guarda os materiais da hora).
                Occluder(rock.gameObject);
                rock.gameObject.AddComponent<ResourceNode>().Configure(ResourceNode.Kind.Ore, iron, VeinIron, VeinHits,
                    Mathf.Max(bounds.extents.x, bounds.extents.z), null, 1f, rock.GetComponent<Pirates.CameraSystem.CameraOccluder>(), vein, VeinRegrow);
                veins++;
            }
            Debug.Log($"[Pirates] Veios de ferro: {veins} pedras das matas.");
        }
    }
}
