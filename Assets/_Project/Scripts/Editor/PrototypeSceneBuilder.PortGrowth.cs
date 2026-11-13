using System.Collections.Generic;
using Pirates.Base;
using Pirates.Economy;
using Pirates.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Porto Alento cresce (12/11; onde gastar o ouro, pedido do Jhonata: "que todas as evoluções mudem visualmente o porto"). Uma construção
    /// a mais no Mestre de obras, paga quase só com ouro, com 5 níveis; cada um dá desconto no mercado e muda o porto: (1) cais reformado
    /// (bandeirolas e botes no píer), (2) feira do leste, (3) bairro novo, (4) farol na praia leste, (5) estátua do capitão perto da praça.
    /// A vila não tem lugar livre: a feira, o bairro e o farol entram na mata a leste, e as árvores daquele pedaço somem no nível
    /// (BaseBuildings.BuildingVisual.cleared). Posições locais do porto (as de PortGround).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static readonly Vector2 FairArea = new(34f, -6f);
        static readonly Vector2 NewQuarterArea = new(52f, -5f);
        static readonly Vector2 LighthouseSpot = new(65f, 2f);
        static readonly Vector2 StatueSpot = new(0f, 14f);
        // Clareiras na mata: centro, raio (m) e o nível que abre.
        static readonly (Vector2 center, float radius, int level)[] PortClearings =
        {
            (FairArea, 11f, 2),
            (NewQuarterArea, 14f, 3),
            (LighthouseSpot, 5f, 4),
        };

        static BuildingDefinition PortGrowthAsset()
        {
            var g = LoadItem("Item_Ouro");
            var w = LoadItem("Item_Madeira");
            var s = LoadItem("Item_Pedra");
            var c = LoadItem("Item_Lona");
            var i = LoadItem("Item_Ferro");
            return BuildingAsset("Building_PortoAlento", "Porto Alento", BuildingEffect.PortGrowth, null, null,
                "Invista na vila: a cada nível o porto cresce (cais, feira, bairro novo, farol, estátua) e o mercado vende mais barato.",
                BLvl(3f, 60f, (g, 600), (w, 40)),
                BLvl(6f, 90f, (g, 1500), (w, 40), (c, 30)),
                BLvl(9f, 120f, (g, 3000), (w, 80), (s, 120)),
                BLvl(12f, 180f, (g, 5000), (s, 150), (i, 30)),
                BLvl(15f, 240f, (g, 8000), (s, 100)));
        }

        /// <summary>
        /// Põe o Porto Alento na cena do porto (05 e 07): chamado no fim da geração (a mata já existe) e pelo eval nas cenas prontas.
        /// Já posto = refaz os visuais.
        /// </summary>
        static string AddPortGrowth()
        {
            var buildings = Object.FindAnyObjectByType<BaseBuildings>(FindObjectsInactive.Include);
            if (!buildings) return "sem construções";
            portIsland = GameObject.Find("Ilha_Sul")?.transform;
            if (!portIsland) return "sem Ilha_Sul";
            var asset = PortGrowthAsset();

            var old = GameObject.Find("PortoAlento_Crescimento");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("PortoAlento_Crescimento").transform;
            var visual = new BaseBuildings.BuildingVisual
            {
                building = asset,
                perLevel = new[]
                {
                    Group(root, "PortoAlento_N1_Cais", PierUpgrade),
                    Group(root, "PortoAlento_N2_Feira", EastFair),
                    Group(root, "PortoAlento_N3_Bairro", NewQuarter),
                    Group(root, "PortoAlento_N4_Farol", Lighthouse),
                    Group(root, "PortoAlento_N5_Estatua", CaptainStatue),
                },
            };
            foreach (var go in visual.perLevel) go.SetActive(false);
            Clearings(visual);

            var so = new SerializedObject(buildings);
            var list = new List<BuildingDefinition>();
            var avail = so.FindProperty("available");
            for (int k = 0; k < avail.arraySize; k++)
                if (avail.GetArrayElementAtIndex(k).objectReferenceValue is BuildingDefinition b && b != asset) list.Add(b);
            list.Add(asset);
            var visuals = new List<BaseBuildings.BuildingVisual>();
            var field = typeof(BaseBuildings).GetField("visuals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var v in (BaseBuildings.BuildingVisual[])field.GetValue(buildings))
                if (v != null && v.building != asset) visuals.Add(v);
            visuals.Add(visual);
            buildings.Configure(list.ToArray(), visuals.ToArray());
            EditorUtility.SetDirty(buildings);
            return $"ok: {visual.cleared.Length} plantas da mata saem nos níveis 2–4";
        }

        /// <summary>Árvores, palmeiras, pedras e mato da mata dentro das clareiras (o mais baixo nível que pega cada um).</summary>
        static void Clearings(BaseBuildings.BuildingVisual visual)
        {
            var cleared = new List<GameObject>();
            var levels = new List<int>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.parent || t.parent.name != "Mata") continue;
                int at = 0;
                foreach (var (center, radius, level) in PortClearings)
                {
                    var w = IslandCenter + new Vector3(center.x, 0f, center.y);
                    float dx = t.position.x - w.x, dz = t.position.z - w.z;
                    if (dx * dx + dz * dz <= radius * radius && (at == 0 || level < at)) at = level;
                }
                if (at == 0) continue;
                cleared.Add(t.gameObject);
                levels.Add(at);
            }
            visual.cleared = cleared.ToArray();
            visual.clearedLevel = levels.ToArray();
        }

        /// <summary>N1: bandeirolas dos dois lados do píer e dois botes amarrados.</summary>
        static void PierUpgrade(Transform g)
        {
            for (int k = 0; k < 4; k++)
                for (int side = -1; side <= 1; side += 2)
                    Kenney(k % 2 == 0 ? "flag-high-pennant" : "flag-pennant", g, PierShore + new Vector3(side * 1.85f, 1.25f, 4f + k * 6f),
                        side > 0 ? 90f : -90f, 1.1f, $"Bandeirola_{k}_{(side > 0 ? "L" : "O")}");
            Kenney("boat-row-small", g, PierShore + new Vector3(-5f, -0.15f, 9f), 10f, 2f, "Bote_Amarrado_A");
            Kenney("boat-row-large", g, PierShore + new Vector3(5.5f, -0.15f, 15f), -8f, 1.8f, "Bote_Amarrado_B");
            Kenney("crate", g, PierShore + new Vector3(-1.1f, 1.25f, 21f), 25f, 1f, "Caixote_Cais_A");
            Kenney("barrel", g, PierShore + new Vector3(1.2f, 1.25f, 10f), 0f, 0.7f, "Barril_Cais_A");
        }

        /// <summary>N2: feira na clareira a leste da vila (três bancas, carrinho, caixas, lanternas e estandartes).</summary>
        static void EastFair(Transform g)
        {
            var stalls = new (Vector2 offset, float yaw, string canopy)[]
            {
                (new Vector2(-4.5f, 3.5f), 135f, "stall-red"), (new Vector2(4.5f, 3.5f), -135f, "stall-green"), (new Vector2(0f, -5f), 0f, "stall-red"),
            };
            string[] wares = { "resource-planks", "barrel", "box" };
            for (int k = 0; k < stalls.Length; k++)
            {
                var st = stalls[k];
                var stall = new GameObject($"Banca_{k}").transform;
                stall.SetParent(g, false);
                stall.SetPositionAndRotation(PortGround(FairArea + st.offset), Quaternion.Euler(0f, st.yaw, 0f));
                Model(Kit.Town, "stall", stall, Vector3.zero, 0f, 3f, "Mesa");
                Model(Kit.Town, st.canopy, stall, Vector3.zero, 0f, 3f, "Toldo");
                Model(Kit.Survival, wares[k], stall, new Vector3(0.1f, 1.12f, 0f), 90f, SurvivalScale, "Mercadoria");
                var box = stall.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(2f, 3.6f, 3f);
                box.center = new Vector3(0f, 1.8f, 0f);
            }
            Model(Kit.Town, "stall-bench", g, PortGround(FairArea + new Vector2(0f, 2f)), 90f, 3f, "Banco");
            Model(Kit.Town, "cart", g, PortGround(FairArea + new Vector2(6.5f, -3f)), 40f, 2.6f, "Carrinho");
            Model(Kit.Survival, "box-large", g, PortGround(FairArea + new Vector2(-6.5f, -3f)), 15f, SurvivalScale, "Caixa_A");
            Model(Kit.Survival, "box", g, PortGround(FairArea + new Vector2(-7f, -1.6f)), 50f, SurvivalScale, "Caixa_B");
            for (int k = 0; k < 2; k++)
                NightLantern(Model(Kit.Town, "lantern", g, PortGround(FairArea + new Vector2(k == 0 ? -7.5f : 7.5f, 2f)), 0f, 1.6f, $"Lanterna_Feira_{k}"));
            Model(Kit.Town, "banner-red", g, PortGround(FairArea + new Vector2(-2f, 8f)), 180f, 2.5f, "Estandarte_A");
            Model(Kit.Town, "banner-green", g, PortGround(FairArea + new Vector2(2f, 8f)), 180f, 2.5f, "Estandarte_B");
        }

        /// <summary>N3: bairro novo (duas casas, jardim com flores, poste com lanterna).</summary>
        static void NewQuarter(Transform g)
        {
            House(g, new HouseSpec { name = "Casa_Nova_A", width = 2, depth = 2, floors = 2, seed = 71, lit = true }, PortGround(NewQuarterArea + new Vector2(-6f, -7f)), 0f);
            House(g, new HouseSpec { name = "Casa_Nova_B", width = 2, depth = 2, floors = 1, woodGround = true, seed = 72, lit = true },
                PortGround(NewQuarterArea + new Vector2(4f, 7f)), -90f);
            string[] flowers = { "flower_redA", "flower_yellowA", "flower_purpleA", "flower_redB", "flower_yellowB" };
            for (int k = 0; k < 10; k++)
            {
                var off = new Vector2(-6f + (k % 5) * 1.3f, 4f + (k / 5) * 1.3f);
                Model(Kit.Nature, flowers[k % flowers.Length], g, PortGround(NewQuarterArea + off), k * 37f, NatureScale, $"Flor_{k}");
            }
            PostLantern(g, PortGround(NewQuarterArea + new Vector2(0f, 0f)), "Poste_Bairro");
        }

        /// <summary>N4: farol de pedra na praia leste, com fogo no alto (luz de verdade à noite).</summary>
        static void Lighthouse(Transform g)
        {
            var ground = PortGround(LighthouseSpot);
            const float scale = 1.5f, piece = 2f * scale;
            Kenney("tower-base", g, ground, 0f, scale, "Farol_Base");
            for (int k = 1; k <= 2; k++)
                PirateWindowGlow(Kenney("tower-middle-windows", g, ground + Vector3.up * piece * k, k * 45f, scale, $"Farol_Meio_{k}"), "tower-middle-windows", 0.4f);
            Kenney("tower-watch", g, ground + Vector3.up * piece * 3f, 0f, scale, "Farol_Topo");
            var top = ground + Vector3.up * (piece * 3f + 1.4f);
            Fire(g, top, 1.4f);
            NightLantern(Model(Kit.Town, "lantern", g, top, 0f, 2.2f, "Farol_Luz"), true, 30f, 6f);
            var body = g.gameObject.AddComponent<CapsuleCollider>();
            body.center = ground + Vector3.up * piece * 1.5f;
            body.radius = 2.2f;
            body.height = piece * 3f;
            Occluder(g.gameObject);
        }

        /// <summary>N5: estátua de pedra do capitão (o modelo do capitão em pedra, 2,4×) num pedestal, perto da praça.</summary>
        static void CaptainStatue(Transform g)
        {
            var ground = PortGround(StatueSpot);
            var stone = GetMaterial("M_Estatua_Pedra", new Color(0.62f, 0.6f, 0.56f), 0.15f);
            // Pedestal de pedra em dois degraus (o "pillar-stone" do kit é uma vara fina: a estátua ficava no ar).
            var darker = GetMaterial("M_Estatua_Pedestal", new Color(0.5f, 0.48f, 0.45f), 0.1f);
            Part(PrimitiveType.Cube, "Pedestal_Base", g, ground + Vector3.up * 0.3f, new Vector3(3.4f, 0.6f, 3.4f), Quaternion.identity, darker);
            Part(PrimitiveType.Cube, "Pedestal", g, ground + Vector3.up * 1.1f, new Vector3(2.4f, 1.0f, 2.4f), Quaternion.identity, stone);
            var holder = new GameObject("Estatua").transform;
            holder.SetParent(g, false);
            // De frente para a praça (norte).
            holder.SetPositionAndRotation(ground + Vector3.up * 1.6f, Quaternion.identity);
            holder.localScale = Vector3.one * 2.4f;
            KitCharacter(KitCharacters.Captain, holder, stone);
            var col = g.gameObject.AddComponent<CapsuleCollider>();
            col.center = ground + Vector3.up * 2.5f;
            col.radius = 1.2f;
            col.height = 5f;
        }

        /// <summary>Pirates → Protótipo: põe o Porto Alento na cena aberta (porto) e salva.</summary>
        [MenuItem("Pirates/Protótipo/Porto Alento: refazer o crescimento na cena aberta")]
        public static void AddPortGrowthToOpenSceneMenu() => Debug.Log("[Pirates] " + AddPortGrowthToOpenScene());

        public static string AddPortGrowthToOpenScene()
        {
            string r = AddPortGrowth();
            if (!r.StartsWith("ok")) return r;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return r;
        }
    }
}
