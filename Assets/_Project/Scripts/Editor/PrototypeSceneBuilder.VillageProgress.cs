using System.Collections.Generic;
using Pirates.Base;
using Pirates.Progression;
using Pirates.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// O porto mostra a carreira do capitão (12/11; cenas 05 e 07): bandeiras piratas pela vila a cada patente, o casco do navio novo na
    /// carreira do estaleiro durante a obra (escuna, galeão) e a vila em festa depois do fim da história (bandeirolas coloridas e lanternas
    /// em volta da praça). Componente: <see cref="VillageProgress"/>. Posições locais do porto, conferidas livres (praça: bancas em (±7,5; 1),
    /// (−5; −6,5), (5,5; −6,5), lanternas a 9,5 m em 45°, a rua do píer em x = 0).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static string AddVillageProgress()
        {
            if (!Object.FindAnyObjectByType<BaseBuildings>(FindObjectsInactive.Include)) return "sem construções";
            portIsland = GameObject.Find("Ilha_Sul")?.transform;
            if (!portIsland) return "sem Ilha_Sul";
            var old = GameObject.Find("Vila_Progresso");
            if (old) Object.DestroyImmediate(old);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && t.name.StartsWith("Carreira_Navio_")) Object.DestroyImmediate(t.gameObject);
            var root = new GameObject("Vila_Progresso").transform;

            // Patentes 1–4.
            var flags = new[]
            {
                Group(root, "Patente1_Bandeiras_Cais", g =>
                {
                    RankFlag(g, new Vector2(-3.5f, 51f), 1.4f, 0);
                    RankFlag(g, new Vector2(3.5f, 51f), 1.4f, 1);
                }),
                Group(root, "Patente2_Bandeiras_Praca", g =>
                {
                    int k = 0;
                    foreach (var off in new[] { new Vector2(12f, 0f), new Vector2(-12f, 0f), new Vector2(4.5f, 12.5f), new Vector2(-4.5f, 12.5f) })
                        RankFlag(g, PlazaCenter + off, 1.3f, k++);
                }),
                Group(root, "Patente3_Bandeiras_Morro", g =>
                {
                    RankFlag(g, new Vector2(-11f, 13f), 1.5f, 0);
                    RankFlag(g, new Vector2(-9f, 3f), 1.5f, 1);
                }),
                Group(root, "Patente4_Bandeira_Grande", g => RankFlag(g, new Vector2(5f, 18f), 3f, 0)),
            };
            foreach (var f in flags) f.SetActive(false);

            // Festa do fim da história.
            var festival = Group(root, "Festa_Fim", g =>
            {
                string[] pennants = { "flag-pennant", "flag-high-pennant" };
                int k = 0;
                foreach (float deg in new[] { 30f, 55f, 125f, 150f, 210f, 235f, 305f, 330f })
                {
                    var off = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * 15f;
                    Kenney(pennants[k % 2], g, PortGround(PlazaCenter + off), deg + 90f, 1.6f, $"Bandeirola_Festa_{k}");
                    if (k % 2 == 0) NightLantern(Model(Kit.Town, "lantern", g, PortGround(PlazaCenter + off * 0.93f), 0f, 1.6f, $"Lanterna_Festa_{k}"), true, 8f, 2.5f);
                    k++;
                }
                Model(Kit.Town, "banner-red", g, PortGround(new Vector2(-2.6f, 50f)), 0f, 2.8f, "Estandarte_Festa_A");
                Model(Kit.Town, "banner-green", g, PortGround(new Vector2(2.6f, 50f)), 0f, 2.8f, "Estandarte_Festa_B");
            });
            festival.SetActive(false);

            // Navio novo na carreira do estaleiro.
            Transform ramp = null;
            GameObject idle = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Carreira") ramp = t;
                if (t.name == "Bote_EmObra") idle = t.gameObject;
            }
            var ships = new List<VillageProgress.ShipOnSlipway>();
            if (ramp)
            {
                const float slope = 7f;
                foreach (var (asset, model, scale) in new[] { ("ShipClass_Escuna", "ship-pirate-medium", 0.75f), ("ShipClass_Galeao", "ship-pirate-large", 0.62f) })
                {
                    var cls = AssetDatabase.LoadAssetAtPath<ShipClassDefinition>($"{DataFolder}/Ships/{asset}.asset");
                    if (!cls) continue;
                    var hull = new GameObject($"Carreira_Navio_{asset}").transform;
                    hull.SetParent(ramp, false);
                    hull.SetLocalPositionAndRotation(new Vector3(0f, 0.2f - Mathf.Tan(slope * Mathf.Deg2Rad) * 8.5f, 8.5f), Quaternion.Euler(slope, 0f, 0f));
                    Kenney(model, hull, Vector3.zero, 0f, scale, "Casco");
                    // Andaime: postes dos dois lados do casco.
                    var wood = KenneyColors.WoodDark;
                    for (int s = -1; s <= 1; s += 2)
                        for (int k = 0; k < 3; k++)
                            Part(PrimitiveType.Cube, $"Andaime_{k}_{s}", hull, new Vector3(s * 2.6f, 2f, -3f + k * 3f), new Vector3(0.18f, 4f, 0.18f),
                                Quaternion.identity, wood);
                    hull.gameObject.SetActive(false);
                    ships.Add(new VillageProgress.ShipOnSlipway { shipClass = cls, hull = hull.gameObject });
                }
            }

            root.gameObject.AddComponent<VillageProgress>().Configure(flags, ships.ToArray(), idle, festival);
            return $"ok: {flags.Length} patentes, {ships.Count} navios na carreira, festa";
        }

        static void RankFlag(Transform g, Vector2 local, float scale, int k) =>
            Kenney(k % 2 == 0 ? "flag-pirate-high" : "flag-pirate-high-pennant", g, PortGround(local), k * 35f, scale, $"Bandeira_{k}");

        public static string AddVillageProgressToOpenScene()
        {
            string r = AddVillageProgress();
            if (!r.StartsWith("ok")) return r;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }
    }
}
