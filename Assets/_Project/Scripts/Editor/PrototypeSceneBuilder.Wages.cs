using Pirates.Base;
using Pirates.Controls;
using Pirates.Progression;
using Pirates.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>Salário da tripulação (12/11; cenas 05 e 07): objeto "SalarioDaTripulacao" (sempre ligado: o navio desliga em terra) e os avisos no HUD.</summary>
    public static partial class PrototypeSceneBuilder
    {
        static string AddCrewWages()
        {
            var input = Object.FindAnyObjectByType<PlayerShipInput>(FindObjectsInactive.Include);
            var crew = input ? input.GetComponent<ShipCrew>() : null;
            var dock = Object.FindAnyObjectByType<HarborDock>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (!crew || !dock || !hud) return "sem tripulação, porto ou HUD";
            foreach (var old in Object.FindObjectsByType<CrewWages>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            foreach (var old in Object.FindObjectsByType<CrewWagesHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(old);
            var go = new GameObject("SalarioDaTripulacao");
            go.AddComponent<CrewWages>().Configure(crew, dock, LoadItem("Item_Ouro"));
            hud.gameObject.AddComponent<CrewWagesHUD>();
            return "ok";
        }

        public static string AddCrewWagesToOpenScene()
        {
            string r = AddCrewWages();
            if (r != "ok") return r;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return r;
        }
    }
}
