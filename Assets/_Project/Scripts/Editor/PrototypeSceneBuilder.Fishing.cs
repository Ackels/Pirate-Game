using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Pesca com vara (D2, passo 1, 27/10; pedido do Jhonata: "vamos fazer todos eles, mas pode começar pelo da pesca"): a vara de pesca
    /// (item no mercado), os peixes (itens com modelo, raridade, preço de venda e quanto curam comidos), o <see cref="FishingRod"/> no
    /// capitão com a vara, a boia, a linha e os modelos dos peixes montados aqui, os estados do Animator (Human Crafting Animations do
    /// Kevin Iglesias, Asset Store: lançar, esperar, brigar e puxar; antes eram o "Fishing Cast" e o "Fishing Idle" do Mixamo) e o
    /// <see cref="FishingHUD"/>. Modelos: cavala e atum do Pirate Kit do Quaternius (CC0, copiados do _Downloads), arenque
    /// do Quirky Series e os bichos do mar do Pixit3D Fish (Asset Store, em ThirdParty/Loja).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Kevin Iglesias (medido no capitão): "FishingThrow01" 1,3 s (sobe a vara atrás da cabeça e chicoteia para a frente aos ~0,5 s;
        // das mãos soltas à pose de segurar a vara), "Fishing01 - Loop" 4 s, "FishingFighting01" 2,07 s em loop (trancos para cima),
        // "FishingPullOut01" 1,57 s (puxão para cima até ~0,8 s, depois as mãos descem para os lados).
        const string KevinWorkFolder = "Assets/ThirdParty/Loja/KevinIglesias/Human Animations/Animations/Male/Work";
        const string ClipFishThrow = "FishingThrow01", ClipFishWait = "Fishing01 - Loop", ClipFishFight = "FishingFighting01",
            ClipFishPull = "FishingPullOut01";

        const string PixitFolder = "Assets/ThirdParty/Loja/Pixit_FishFree";
        const string QuirkyFolder = "Assets/ThirdParty/Loja/Quirky_Animals";

        sealed class FishSpec
        {
            public string file, name, description;
            public ItemRarity rarity;
            public Color color;
            public float space, heal, weight, shallow = 1f, deep = 1f, size = 0.45f;
            public int sell;
            /// <summary>Modelo do Pirate Kit do Quaternius (Models/&lt;kit&gt;.fbx) ou prefab de um pacote da Loja (pasta + nome).</summary>
            public string kit, folder, prefab;
            public Quaternion pose = Quaternion.identity;
        }

        // Peso no sorteio (soma ~110): comuns ~70%, incomuns ~18%, raros ~6%, pérola ~1%. No raso (até 1,4 m) caranguejo, camarão e
        // estrela; no fundo (2,6 m+) atum, polvo e a ostra. Venda: um peixe comum vale 1–3 de ouro, a pérola 80; o mercado aceita 12 de
        // cada (volta 1 por minuto). Cura comido (R, sem ração): 8–30.
        static readonly FishSpec[] FishSpecs =
        {
            new() { file = "Item_Peixe_Cavala", name = "Cavala", rarity = ItemRarity.Common, color = new Color(0.55f, 0.72f, 0.85f),
                space = 0.4f, sell = 2, heal = 15f, weight = 24f, size = 0.5f, kit = "Prop_Fish_Mackerel",
                description = "Peixe listrado de cardume, o pão de cada dia do pescador. Assada na brasa, cura um pouco." },
            new() { file = "Item_Peixe_Arenque", name = "Arenque", rarity = ItemRarity.Common, color = new Color(0.6f, 0.78f, 0.9f),
                space = 0.3f, sell = 2, heal = 12f, weight = 20f, shallow = 1.2f, deep = 0.8f, size = 0.42f, folder = QuirkyFolder, prefab = "Herring",
                description = "Pequeno e prateado. Salgado num barril, atravessa meio mar sem estragar." },
            new() { file = "Item_Peixe_Camarao", name = "Camarão", rarity = ItemRarity.Common, color = new Color(0.95f, 0.55f, 0.45f),
                space = 0.2f, sell = 1, heal = 8f, weight = 12f, shallow = 2f, deep = 0.4f, size = 0.3f, folder = PixitFolder, prefab = "Shrimp_Rigged_Unity",
                description = "Pula na isca da beira da praia. Pouca carne, mas é comida." },
            new() { file = "Item_Peixe_Caranguejo", name = "Caranguejo", rarity = ItemRarity.Common, color = new Color(0.9f, 0.35f, 0.3f),
                space = 0.3f, sell = 3, heal = 12f, weight = 9f, shallow = 2.5f, deep = 0.3f, size = 0.38f, folder = PixitFolder, prefab = "Crab_Rigged_Unity",
                description = "Agarrou a isca com a pinça e não largou mais. Mora no raso, perto das pedras." },
            new() { file = "Item_Peixe_Estrela", name = "Estrela-do-mar", rarity = ItemRarity.Common, color = new Color(0.95f, 0.6f, 0.3f),
                space = 0.2f, sell = 1, weight = 6f, shallow = 3f, deep = 0.2f, size = 0.36f, folder = PixitFolder, prefab = "Starfish_Rigged_Unity",
                description = "Não se come, mas o mercado paga uns trocados: os marujos penduram como enfeite na cabine." },
            new() { file = "Item_Peixe_Baiacu", name = "Baiacu", rarity = ItemRarity.Uncommon, color = new Color(0.55f, 0.8f, 0.9f),
                space = 0.3f, sell = 5, weight = 8f, size = 0.38f, folder = PixitFolder, prefab = "BabyPutterfish_Rigged_Unity",
                description = "Incha quando se assusta. Venenoso: não coma. O boticário do porto paga bem pelo veneno." },
            new() { file = "Item_Peixe_Siba", name = "Siba", rarity = ItemRarity.Uncommon, color = new Color(0.45f, 0.6f, 0.95f),
                space = 0.3f, sell = 6, heal = 18f, weight = 7f, shallow = 0.8f, deep = 1.3f, size = 0.42f, folder = PixitFolder, prefab = "Cuttlefish_Rigged_Unity",
                description = "Solta tinta quando sai da água. A carne é macia, e a tinta vira tinta de escrever cartas." },
            new() { file = "Item_Peixe_Atum", name = "Atum", rarity = ItemRarity.Uncommon, color = new Color(0.4f, 0.5f, 0.7f),
                space = 1f, sell = 8, heal = 30f, weight = 5f, shallow = 0.2f, deep = 2.5f, size = 0.8f, kit = "Prop_Fish_Tuna",
                description = "Peixe grande de água funda: só morde longe da praia, na ponta do píer. Alimenta uma tripulação." },
            new() { file = "Item_Peixe_Polvo", name = "Polvo", rarity = ItemRarity.Rare, color = new Color(0.75f, 0.4f, 0.85f),
                space = 0.5f, sell = 16, heal = 25f, weight = 4f, shallow = 0.5f, deep = 1.6f, size = 0.5f, folder = PixitFolder, prefab = "Octopus_Rigged_Unity",
                description = "Oito braços e muita esperteza: escapa de quem fisga devagar. A taverna do porto paga caro." },
            new() { file = "Item_Peixe_CavaloMarinho", name = "Cavalo-marinho", rarity = ItemRarity.Rare, color = new Color(0.85f, 0.65f, 0.35f),
                space = 0.1f, sell = 20, weight = 3f, shallow = 1.3f, deep = 0.7f, size = 0.36f, folder = PixitFolder, prefab = "Seahorse_Rigged_Unity",
                description = "Dizem que dá sorte a bordo. Os mercadores de amuletos compram para secar ao sol." },
            new() { file = "Item_Perola", name = "Pérola", rarity = ItemRarity.Epic, color = new Color(0.95f, 0.92f, 1f),
                space = 0.05f, sell = 80, weight = 1.2f, shallow = 0.6f, deep = 1.6f, size = 0.36f, folder = PixitFolder, prefab = "PearlSeashell_Rigged_Unity",
                description = "Uma ostra fisgada por acaso, com uma pérola grande lá dentro. Vale uma pequena fortuna no mercado." },
        };

        static ItemDefinition FishItem(FishSpec f)
        {
            var item = Item(f.file, f.name, ItemCategory.Resource, f.rarity, f.color, f.space, f.description);
            if (!Mathf.Approximately(item.foodHeal, f.heal))
            {
                item.foodHeal = f.heal;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        static IEnumerable<ItemDefinition> FishItems()
        {
            foreach (var f in FishSpecs) yield return FishItem(f);
            yield return RodItem();
        }

        static ItemDefinition RodItem() => Item("Equip_VaraPesca", "Vara de pesca", ItemCategory.Gear, ItemRarity.Common, new Color(0.7f, 0.52f, 0.3f), 0f,
            "Vara de bambu com carretilha de latão. A pé, de frente para a água funda (beira da praia, píer), F lança; quando a boia afundar, " +
            "F de novo para fisgar. Os peixes vendem no mercado e os comuns matam a fome (R, sem ração).");

        // ---------- Mercado ----------

        /// <summary>Vara à venda (uma vez) e os peixes que o mercado compra, se ainda não estão na lista.</summary>
        static void AddFishingMarket(HarborSettings settings)
        {
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            bool changed = false;
            var rod = RodItem();
            if (!list.Exists(e => e != null && e.item == rod))
            {
                list.Add(Market(rod, 60, 0));
                changed = true;
            }
            foreach (var f in FishSpecs)
            {
                var item = FishItem(f);
                if (list.Exists(e => e != null && e.item == item)) continue;
                var entry = Market(item, 0, f.sell);
                entry.demand = 12;
                entry.demandRefillSeconds = 60f;
                list.Add(entry);
                changed = true;
            }
            if (!changed) return;
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        // ---------- Animator ----------

        /// <summary>
        /// Lançar ("PescarLancar", entra pelo CharacterAnimatorDriver.PlayFishCast), esperar com a vara ("PescarEspera", em loop, enquanto o
        /// bool "Fishing" estiver ligado), brigar com o peixe ("PescarBriga", em loop, entra pelo PlayFishFight) e puxar ("PescarPuxar", peixe
        /// ou linha vazia, sai no fim para a pose da arma). Corpo todo. Clipes do Kevin Iglesias (pasta Work/Fishing do homem).
        /// </summary>
        static void AddFishingStates(AnimatorStateMachine sm, AnimatorState empty)
        {
            var throwClip = KevinClip(ClipFishThrow);
            var waitClip = KevinClip(ClipFishWait);
            var fightClip = KevinClip(ClipFishFight);
            var pullClip = KevinClip(ClipFishPull);
            if (!throwClip || !waitClip || !fightClip || !pullClip)
            {
                Debug.LogWarning("Pesca: clipes do Human Crafting Animations não encontrados em " + KevinWorkFolder);
                return;
            }
            AnimatorState Add(string name, AnimationClip clip, Vector3 at)
            {
                var s = sm.AddState(name, at);
                s.motion = clip;
                s.tag = MovingFullBodyTag;
                return s;
            }
            var throwLine = Add("PescarLancar", throwClip, new Vector3(-300f, -440f));
            var wait = Add("PescarEspera", waitClip, new Vector3(-300f, -520f));
            var fight = Add("PescarBriga", fightClip, new Vector3(-300f, -600f));
            var pull = Add("PescarPuxar", pullClip, new Vector3(-300f, -680f));
            Go(throwLine, empty, 0.3f, ("Fishing", AnimatorConditionMode.IfNot, 0));
            Exit(throwLine, wait, 0.92f, 0.12f);
            Go(wait, empty, 0.3f, ("Fishing", AnimatorConditionMode.IfNot, 0));
            Go(fight, empty, 0.3f, ("Fishing", AnimatorConditionMode.IfNot, 0));
            Exit(pull, empty, 0.9f, 0.2f);
        }

        /// <summary>Clipe do Kevin Iglesias (pasta Work do homem) pelo fim do nome do arquivo ("HumanM@&lt;nome&gt;").</summary>
        static AnimationClip KevinClip(string name)
        {
            if (!AssetDatabase.IsValidFolder(KevinWorkFolder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { KevinWorkFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.EndsWith("@" + name, System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        // ---------- No capitão ----------

        /// <summary>
        /// Vara (cabo, haste e carretilha, ~1,7 m), boia vermelha e branca, linha e os modelos dos peixes (desligados), no capitão; o
        /// <see cref="FishingRod"/> põe a vara entre as mãos a cada quadro. Depois de todas as outras interações do F (ela perde para elas).
        /// </summary>
        static void AddFishingRod(GameObject character)
        {
            var gear = new GameObject("Pesca").transform;
            gear.SetParent(character.transform, false);

            var rod = new GameObject("VaraDePesca").transform;
            rod.SetParent(gear, false);
            var wood = GetMaterial("M_Vara_Cabo", new Color(0.36f, 0.22f, 0.12f), 0.15f);
            var bamboo = GetMaterial("M_Vara_Bambu", new Color(0.78f, 0.66f, 0.38f), 0.2f);
            var brass = GetMaterial("M_Vara_Latao", new Color(0.85f, 0.66f, 0.3f), 0.55f);
            var along = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cylinder, "Cabo", rod, new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.2f, 0.045f), along, wood);
            Part(PrimitiveType.Cylinder, "Haste", rod, new Vector3(0f, 0f, 0.95f), new Vector3(0.022f, 0.75f, 0.022f), along, bamboo);
            Part(PrimitiveType.Cylinder, "Ponteira", rod, new Vector3(0f, 0f, 1.6f), new Vector3(0.012f, 0.1f, 0.012f), along, bamboo);
            Part(PrimitiveType.Cylinder, "Carretilha", rod, new Vector3(0f, -0.06f, 0.12f), new Vector3(0.09f, 0.025f, 0.09f), Quaternion.Euler(0f, 0f, 90f), brass);
            var tip = new GameObject("Ponta").transform;
            tip.SetParent(rod, false);
            tip.localPosition = new Vector3(0f, 0f, 1.7f);
            foreach (var r in rod.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            var bobber = new GameObject("Boia").transform;
            bobber.SetParent(gear, false);
            Part(PrimitiveType.Sphere, "Corpo", bobber, Vector3.zero, Vector3.one * 0.13f, Quaternion.identity,
                GetMaterial("M_Boia_Branca", new Color(0.95f, 0.94f, 0.9f), 0.4f));
            Part(PrimitiveType.Sphere, "Topo", bobber, new Vector3(0f, 0.035f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Quaternion.identity,
                GetMaterial("M_Boia_Vermelha", new Color(0.86f, 0.15f, 0.12f), 0.4f));
            Part(PrimitiveType.Cylinder, "Haste", bobber, new Vector3(0f, 0.09f, 0f), new Vector3(0.015f, 0.04f, 0.015f), Quaternion.identity,
                GetMaterial("M_Boia_Vermelha", new Color(0.86f, 0.15f, 0.12f), 0.4f));

            var line = new GameObject("Linha").AddComponent<LineRenderer>();
            line.transform.SetParent(gear, false);
            line.useWorldSpace = true;
            // Mais grossa que uma linha de verdade: com 1,2 cm sumia no meio do caminho de longe.
            line.widthMultiplier = 0.03f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetTransparentMaterial("M_AimLine", Color.white);
            line.startColor = line.endColor = new Color(0.97f, 0.97f, 0.93f, 0.95f);
            line.enabled = false;

            var show = new GameObject("PeixeMostrado").transform;
            show.SetParent(gear, false);
            show.localPosition = new Vector3(0f, 2.3f, 0.2f);

            var fishRoot = new GameObject("Peixes").transform;
            fishRoot.SetParent(gear, false);
            var table = new List<FishingRod.Catch>();
            foreach (var f in FishSpecs)
            {
                var holder = new GameObject(f.file).transform;
                holder.SetParent(fishRoot, false);
                if (!FishModel(holder, f))
                {
                    Object.DestroyImmediate(holder.gameObject);
                    Debug.LogWarning($"[Pirates] Pesca: modelo do peixe {f.name} não encontrado.");
                    continue;
                }
                holder.gameObject.SetActive(false);
                table.Add(new FishingRod.Catch { item = FishItem(f), model = holder.gameObject, weight = f.weight, shallow = f.shallow, deep = f.deep });
            }

            var fishing = character.AddComponent<FishingRod>();
            fishing.Configure(RodItem(), table.ToArray(), rod, tip, bobber, line, show);
            BakeFishIcons();
            AttachFishingHud();
        }

        /// <summary>A pesca no HUD (se ele já existe; senão o CreateGameHud põe).</summary>
        static void AttachFishingHud()
        {
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            var rod = Object.FindAnyObjectByType<FishingRod>(FindObjectsInactive.Include);
            if (!hud || !rod || hud.GetComponent<FishingHUD>()) return;
            hud.gameObject.AddComponent<FishingHUD>().Bind(rod);
        }

        /// <summary>
        /// O peixe como filho de <paramref name="holder"/>: sem física nem scripts (só o Animator, quem tiver), centrado na origem e com o
        /// maior lado em <see cref="FishSpec.size"/>.
        /// </summary>
        static bool FishModel(Transform holder, FishSpec f)
        {
            GameObject source = f.kit != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{f.kit}.fbx")
                : StorePrefab(f.folder, f.prefab);
            if (!source) return false;
            var go = (GameObject)Object.Instantiate(source, holder, false);
            go.name = source.name;
            go.transform.localRotation = f.pose * go.transform.localRotation;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var b in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            if (f.kit != null)
                foreach (var r in renderers)
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = QuaterniusMaterial;
                    r.sharedMaterials = slots;
                }
            foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;
            var b0 = RendererBounds(renderers);
            float biggest = Mathf.Max(b0.size.x, Mathf.Max(b0.size.y, b0.size.z));
            if (biggest > 1e-4f) go.transform.localScale *= f.size / biggest;
            var b1 = RendererBounds(renderers);
            go.transform.position += holder.position - b1.center;
            return true;
        }

        static Bounds RendererBounds(Renderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject StorePrefab(string folder, string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        /// <summary>Ícones dos peixes e da vara (só os que ainda não existem).</summary>
        static void BakeFishIcons()
        {
            EnsureFolder(HudFolder, "Icones");
            foreach (var f in FishSpecs)
            {
                var spec = f;
                // Na diagonal, como as armas; a siba do Pixit é toda azul: pinta de siba (SibaPaint).
                SetItemIcon(spec.file, Icon("Peixe_" + spec.file.Replace("Item_Peixe_", "").Replace("Item_", ""), t => FishModel(t, spec),
                    new Vector3(-0.5f, 0.6f, 1f), -20f, spec.file == "Item_Peixe_Siba" ? SibaPaint : null));
            }
            SetItemIcon("Equip_VaraPesca", Icon("Item_VaraPesca", t =>
            {
                var wood = GetMaterial("M_Vara_Cabo", new Color(0.36f, 0.22f, 0.12f), 0.15f);
                var bamboo = GetMaterial("M_Vara_Bambu", new Color(0.78f, 0.66f, 0.38f), 0.2f);
                var brass = GetMaterial("M_Vara_Latao", new Color(0.85f, 0.66f, 0.3f), 0.55f);
                // Grossa para o ícone (a de verdade, fina, sumia): cabo, haste, carretilha, linha e anzol.
                var tilt = Quaternion.Euler(0f, 0f, -45f);
                PrismPart(7, "Cabo", t, tilt * new Vector3(0f, -0.6f, 0f), new Vector3(0.13f, 0.42f, 0.13f), tilt, wood);
                PrismPart(6, "Haste", t, tilt * new Vector3(0f, 0.3f, 0f), new Vector3(0.07f, 1.4f, 0.07f), tilt, bamboo);
                PrismPart(10, "Carretilha", t, tilt * new Vector3(-0.12f, -0.42f, 0f), new Vector3(0.24f, 0.07f, 0.24f),
                    tilt * Quaternion.Euler(0f, 0f, 90f), brass);
                var line = GetMaterial("M_Icone_Linha", new Color(0.92f, 0.9f, 0.84f), 0.1f);
                var tip = tilt * new Vector3(0f, 1f, 0f);
                PrismPart(4, "Linha", t, tip + new Vector3(0f, -0.38f, 0f), new Vector3(0.018f, 0.76f, 0.018f), Quaternion.identity, line);
                PrismPart(6, "Anzol", t, tip + new Vector3(0.04f, -0.8f, 0f), new Vector3(0.04f, 0.12f, 0.04f), Quaternion.Euler(0f, 0f, 35f),
                    GetMaterial("M_Icone_Ferro", IconIron, 0.5f));
                PrismPart(8, "Boia", t, tip + new Vector3(0f, -0.55f, 0f), new Vector3(0.11f, 0.12f, 0.11f), Quaternion.identity,
                    GetMaterial("M_Icone_Boia", new Color(0.85f, 0.18f, 0.12f), 0.2f));
            }));
        }
    }
}
