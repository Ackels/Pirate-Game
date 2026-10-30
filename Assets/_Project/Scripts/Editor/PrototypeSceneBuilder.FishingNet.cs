using System.Collections.Generic;
using System.IO;
using Pirates.Base;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Pesca com rede do navio parado (D2, passo 2, 30/10; pedido do Jhonata: pescar "em lugares que estão com bolhas, onde você para o
    /// barco"): a rede de pesca (item do mercado), a <see cref="FishingNet"/> no navio do jogador com a malha, as boias, os cabos e os
    /// peixes mostrados ao puxar, e os cardumes (<see cref="FishingSpot"/> + <see cref="FishingGrounds"/>) em pontos de mar aberto longe
    /// das ilhas e dos covis dos monstros. Só na cena 07 (o mundo).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const int ActiveFishingSpots = 5;
        const float FishingSpotRadius = 14f;
        const float NetLength = 4.5f;

        static ItemDefinition NetItem() => Item("Equip_RedePesca", "Rede de pesca", ItemCategory.Gear, ItemRarity.Uncommon,
            new Color(0.72f, 0.6f, 0.4f), 0f,
            "Rede de malha grossa com boias de cortiça. No navio parado dentro de um cardume (as bolhas no mar), F lança pelo estibordo: os " +
            "peixes caem nela sozinhos até encher; F de novo puxa e tudo vai para o porão. Precisa de 2 tripulantes.");

        /// <summary>Rede à venda (uma vez), se ainda não está na lista.</summary>
        static void AddNetMarket(HarborSettings settings)
        {
            var net = NetItem();
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            if (list.Exists(e => e != null && e.item == net)) return;
            list.Add(Market(net, 150, 0));
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        // ---------- No navio ----------

        /// <summary>
        /// A rede no navio do jogador (desligada até lançar; a <see cref="FishingNet"/> mede o casco de cada classe na hora): malha que pende
        /// da linha d'água com um bolsão para fora, fileira de boias de cortiça, dois cabos até a amurada e três lugares com os modelos dos
        /// peixes (aparecem dentro da rede ao puxar). Depois de todas as outras interações do F do navio (ela perde para elas).
        /// </summary>
        static void AddFishingNet(ShipController player, LootItems items)
        {
            var root = new GameObject("RedeDePesca").transform;
            root.SetParent(player.transform, false);

            var body = new GameObject("Malha").transform;
            body.SetParent(root, false);
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = NetMesh();
            var meshRenderer = body.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = NetMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var floatRow = new GameObject("Boias").transform;
            floatRow.SetParent(root, false);
            var cork = GetMaterial("M_Rede_Boia", new Color(0.86f, 0.5f, 0.2f), 0.25f);
            for (int i = 0; i < 7; i++)
            {
                float z = Mathf.Lerp(-0.5f, 0.5f, i / 6f) * NetLength;
                // Maiores que boias de verdade: a rede fica quase toda debaixo da água (opaca) e as boias são o que se vê dela.
                var b = Part(PrimitiveType.Sphere, "Boia", floatRow, new Vector3(0f, 0.08f, z), new Vector3(0.4f, 0.26f, 0.4f), Quaternion.identity, cork);
                b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            var rope = GetMaterial("M_Rede_Cabo", new Color(0.55f, 0.42f, 0.26f), 0.1f);
            var ropes = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var line = new GameObject("Cabo").AddComponent<LineRenderer>();
                line.transform.SetParent(root, false);
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = 0.09f;
                line.sharedMaterial = rope;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.enabled = false;
                ropes[i] = line;
            }

            // Peixes dentro da rede ao puxar: um lugar no meio e dois dos lados, cada um com uma cópia de cada peixe (desligadas).
            var slots = new Transform[3];
            for (int k = 0; k < slots.Length; k++)
            {
                var slot = new GameObject("Peixes_" + k).transform;
                slot.SetParent(root, false);
                slot.localPosition = new Vector3(0.22f, -0.28f, (k - 1) * 0.55f);
                slot.localRotation = Quaternion.Euler(0f, 90f + (k - 1) * 25f, (k - 1) * 20f);
                foreach (var f in FishSpecs)
                {
                    if (f.legendary) continue;   // lendário só na vara
                    var holder = new GameObject(FishItem(f).name).transform;
                    holder.SetParent(slot, false);
                    if (!FishModel(holder, f))
                    {
                        Object.DestroyImmediate(holder.gameObject);
                        continue;
                    }
                    // Maiores que os de verdade (no tamanho da pesca com vara sumiam dentro da malha).
                    holder.localScale = Vector3.one * 1.7f;
                    holder.gameObject.SetActive(false);
                }
                slots[k] = slot;
            }

            // No fundo do mar a rede pega os peixes de água funda; às vezes vem madeira boiando ou, raro, um pedaço de mapa.
            var table = new List<FishingNet.Catch>();
            // Os presos a hora, tempo ou lugar (lula, peixe-espada, enguia...) seguem as mesmas regras da vara, pela região do navio.
            foreach (var f in FishSpecs)
                if (!f.legendary) table.Add(new FishingNet.Catch { item = FishItem(f), weight = f.weight * f.deep, when = f.when });
            if (items.wood) table.Add(new FishingNet.Catch { item = items.wood, weight = 6f, extra = true, min = 1, max = 2 });
            var piece = MapPieceItem();
            if (piece) table.Add(new FishingNet.Catch { item = piece, weight = 1f, extra = true });

            var net = player.gameObject.AddComponent<FishingNet>();
            net.Configure(NetItem(), StrongNetItem(), table.ToArray(), root, body, floatRow, ropes, slots);
            root.gameObject.SetActive(false);

            SetItemIcon("Equip_RedePesca", Icon("Item_RedePesca", NetIcon, new Vector3(-0.4f, 0.8f, 1f)));

            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<FishingNetHUD>()) hud.gameObject.AddComponent<FishingNetHUD>().Bind(net);
        }

        /// <summary>
        /// Malha da rede: borda de cima no eixo Z (−0,5…0,5), pendendo até y = −1, com um bolsão para fora (+X) mais fundo no meio de baixo.
        /// A escala dá a fundura e o comprimento. Grava em Meshes/Rede_Pesca (mesmo GUID a cada geração).
        /// </summary>
        static Mesh NetMesh()
        {
            const int nu = 14, nv = 8;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= nv; j++)
            for (int i = 0; i <= nu; i++)
            {
                float u = i / (float)nu, v = j / (float)nv;
                float bulge = 0.4f * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * 0.5f * v);
                // As pontas de baixo puxadas para o meio: a rede vira um saco.
                float pinch = 1f - 0.25f * v * v;
                vertices.Add(new Vector3(bulge, -v, (u - 0.5f) * pinch));
                uvs.Add(new Vector2(u * 9f, v * 5f));
            }
            var triangles = new List<int>();
            for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
            var mesh = new Mesh { name = "Rede_Pesca" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveIslandMesh(mesh, $"{MeshesFolder}/Rede_Pesca.asset");
        }

        /// <summary>Malha em losango de corda (textura feita aqui, recortada pelo alfa, dos dois lados).</summary>
        static Material NetMaterial()
        {
            string path = $"{MaterialsFolder}/M_Rede.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Rede" };
            mat.SetTexture("_BaseMap", NetTexture());
            mat.SetColor("_BaseColor", new Color(0.86f, 0.74f, 0.52f));
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.doubleSidedGI = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Texture2D NetTexture()
        {
            EnsureFolder(Root, "Textures");
            string path = $"{Root}/Textures/Rede_Malha.png";
            if (!File.Exists(path))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Duas diagonais (losango) de ~5 px, um nó mais escuro nos cruzamentos.
                    int d1 = Mathf.Abs((x + y) % size - size / 2), d2 = Mathf.Abs(((x - y) % size + size) % size - size / 2);
                    bool cordA = d1 > size / 2 - 3, cordB = d2 > size / 2 - 3;
                    float shade = cordA && cordB ? 0.72f : 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    tex.SetPixel(x, y, cordA || cordB ? new Color(shade, shade, shade, 1f) : new Color(1f, 1f, 1f, 0f));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Ícone: um monte de rede enrolada com boias de cortiça e um cabo.</summary>
        static void NetIcon(Transform t)
        {
            var net = GetMaterial("M_Icone_Rede", new Color(0.78f, 0.66f, 0.45f), 0.1f);
            var dark = GetMaterial("M_Icone_RedeEscura", new Color(0.55f, 0.43f, 0.27f), 0.1f);
            var cork = GetMaterial("M_Rede_Boia", new Color(0.86f, 0.5f, 0.2f), 0.25f);
            PrismPart(9, "Monte", t, new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.38f, 0.8f), Quaternion.Euler(0f, 20f, 0f), net);
            PrismPart(8, "Dobra", t, new Vector3(0.12f, 0.42f, 0.05f), new Vector3(0.7f, 0.22f, 0.55f), Quaternion.Euler(0f, -15f, 6f), dark);
            PrismPart(7, "Topo", t, new Vector3(-0.05f, 0.57f, -0.02f), new Vector3(0.42f, 0.14f, 0.34f), Quaternion.Euler(0f, 40f, 0f), net);
            for (int k = 0; k < 4; k++)
            {
                float a = k * 1.7f + 0.4f;
                PrismPart(8, "Boia", t, new Vector3(Mathf.Cos(a) * 0.48f, 0.3f + 0.08f * k, Mathf.Sin(a) * 0.36f), new Vector3(0.2f, 0.13f, 0.2f),
                    Quaternion.identity, cork);
            }
            PrismPart(6, "Cabo", t, new Vector3(0.45f, 0.08f, 0.3f), new Vector3(0.06f, 0.5f, 0.06f), Quaternion.Euler(70f, 30f, 0f),
                GetMaterial("M_Rede_Cabo", new Color(0.55f, 0.42f, 0.26f), 0.1f));
        }

        /// <summary>Ícone da rede reforçada (prêmio do diário): a rede com cabos de latão e boias douradas por cima.</summary>
        static void StrongNetIcon(Transform t)
        {
            NetIcon(t);
            var brass = GetMaterial("M_Icone_Ouro", new Color(1f, 0.78f, 0.22f), 0.7f);
            PrismPart(10, "Aro", t, new Vector3(0f, 0.36f, 0f), new Vector3(1.05f, 0.06f, 0.85f), Quaternion.Euler(0f, 20f, 0f), brass);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f + 1.2f;
                PrismPart(8, "BoiaDourada", t, new Vector3(Mathf.Cos(a) * 0.4f, 0.62f, Mathf.Sin(a) * 0.3f), new Vector3(0.17f, 0.11f, 0.17f),
                    Quaternion.identity, brass);
            }
        }

        // ---------- Cardumes ----------

        /// <summary>
        /// Cardumes no mar: <see cref="ActiveFishingSpots"/> ligados ao mesmo tempo, que o <see cref="FishingGrounds"/> espalha pelos pontos de
        /// mar aberto achados aqui (sem terra nem nada sólido a 85 m, a mais de 260 m dos covis da lula e do megalodonte e da patrulha do
        /// Tormenta, até 820 m do centro do mapa, a pelo menos 170 m um do outro). Chamar depois de todas as ilhas (a Ermida inclusive).
        /// </summary>
        static void AddFishingGrounds(ShipController player)
        {
            var root = new GameObject("Cardumes");
            var spots = new FishingSpot[ActiveFishingSpots];
            for (int i = 0; i < spots.Length; i++)
            {
                var go = new GameObject("Cardume_" + i);
                go.transform.SetParent(root.transform, false);
                var fx = new[] { SpotBubbles(go.transform), SpotRings(go.transform) };
                var spot = go.AddComponent<FishingSpot>();
                spot.Configure(FishingSpotRadius, fx, SpotJumpers(go.transform));
                spots[i] = spot;
            }
            var places = FishingPlaces();
            root.AddComponent<FishingGrounds>().Configure(spots, places, player.transform);
            Debug.Log($"[Pirates] Cardumes: {places.Length} pontos de pesca no mar.");
        }

        static Vector3[] FishingPlaces()
        {
            Physics.SyncTransforms();
            var avoid = new List<Vector3> { SquidLair, MegalodonLair };
            avoid.AddRange(BossSpawnPoints);
            var candidates = new List<Vector3>();
            for (float x = -800f; x <= 800f; x += 40f)
            for (float z = -800f; z <= 800f; z += 40f)
            {
                var p = new Vector3(x, 0f, z);
                if (p.magnitude > 820f) continue;
                bool near = false;
                foreach (var a in avoid)
                    if (new Vector2(p.x - a.x, p.z - a.z).magnitude < 260f) near = true;
                if (near || Physics.CheckSphere(p, 85f, ~0, QueryTriggerInteraction.Ignore)) continue;
                candidates.Add(p);
            }
            // Espalhados: sorteio fixo, um ponto só entra se estiver a 170 m de todos os outros.
            var rng = new System.Random(7);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            var chosen = new List<Vector3>();
            foreach (var c in candidates)
            {
                if (chosen.Count >= 18) break;
                bool close = false;
                foreach (var o in chosen)
                    if ((o - c).magnitude < 170f) close = true;
                if (!close) chosen.Add(c);
            }
            return chosen.ToArray();
        }

        /// <summary>Bolhas brancas subindo e estourando na água, espalhadas pelo círculo do cardume.</summary>
        static ParticleSystem SpotBubbles(Transform parent)
        {
            // Grandes e muitas: a câmera do jogo fica a ~40 m (com 0,2–0,45 m e 45 por segundo não se via o cardume; a textura tem muita
            // borda transparente). Tamanhos testados no Play em 30/10.
            var ps = SpotSystem(parent, "Bolhas", FxMaterial("M_FX_Gotas", "circle_05", additive: false), 140);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 1f, 1f, 1f), new Color(0.8f, 0.95f, 1f, 0.9f));
            main.gravityModifier = -0.05f;
            var emission = ps.emission;
            emission.rateOverTime = 85f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.7f, 1f), new Keyframe(1f, 1.3f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            return ps;
        }

        /// <summary>Anéis abrindo onde as bolhas estouram (deitados na água).</summary>
        static ParticleSystem SpotRings(Transform parent)
        {
            var ps = SpotSystem(parent, "Aneis", FxMaterial("M_FX_Anel", "circle_02", additive: false), 70);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(4.5f, 8f);
            main.startColor = new Color(0.95f, 1f, 1f, 0.95f);
            var emission = ps.emission;
            emission.rateOverTime = 32f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1.2f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return ps;
        }

        /// <summary>
        /// Peixes que pulam para fora da água (pedido do Jhonata, 30/10: "além das bolhas, uns peixes pulando"): cavala, arenque e atum
        /// do jogo, maiores que de verdade para aparecer da câmera alta, com o comprido no Z (o <see cref="FishingSpot"/> vira o Z para onde
        /// o salto vai). Desligados até pular.
        /// </summary>
        static Transform[] SpotJumpers(Transform parent)
        {
            var root = new GameObject("PeixesPulando").transform;
            root.SetParent(parent, false);
            var list = new List<Transform>();
            foreach (var (file, size) in new[] { ("Item_Peixe_Cavala", 1f), ("Item_Peixe_Arenque", 0.9f), ("Item_Peixe_Atum", 1.4f), ("Item_Peixe_Cavala", 1.1f) })
            {
                var spec = System.Array.Find(FishSpecs, f => f.file == file);
                if (spec == null) continue;
                var holder = new GameObject("Saltador_" + list.Count).transform;
                holder.SetParent(root, false);
                var model = new GameObject("Modelo").transform;
                model.SetParent(holder, false);
                var big = new FishSpec { file = spec.file, kit = spec.kit, folder = spec.folder, prefab = spec.prefab, pose = spec.pose, size = size };
                if (!FishModel(model, big))
                {
                    Object.DestroyImmediate(holder.gameObject);
                    continue;
                }
                AlongZ(model);
                holder.gameObject.SetActive(false);
                list.Add(holder);
            }
            return list.ToArray();
        }

        /// <summary>Gira o modelo para o lado mais comprido dele ficar no Z do pai (gerado com tudo sem giro: a caixa do mundo vale).</summary>
        static void AlongZ(Transform model)
        {
            var b = RendererBounds(model.GetComponentsInChildren<Renderer>(true));
            if (b.size.x > b.size.z && b.size.x >= b.size.y) model.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.localRotation;
            else if (b.size.y > b.size.z && b.size.y > b.size.x) model.localRotation = Quaternion.Euler(90f, 0f, 0f) * model.localRotation;
        }

        /// <summary>Partículas no mundo, saindo de um disco deitado do tamanho do cardume; começam paradas (o FishingSpot liga perto da câmera).</summary>
        static ParticleSystem SpotSystem(Transform parent, string name, Material material, int max, float radiusScale = 0.9f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = FishingSpotRadius * radiusScale;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }
    }
}
