using Pirates.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Clima, passo 1 (17/11): bandeiras que viram e tremulam com o vento. Toda malha de bandeira do Pirate Kit (as dos navios, "flag-a/b/c" e as
    /// cópias da pintura, e as do cenário, "flag-pirate-high", "flag-pennant"...) ganha um <see cref="FlagWave"/>. As dos navios têm o pano
    /// para trás (−Z) a partir do mastro; as do cenário, para −X. Roda antes de salvar cada cena (OptimizeSceneRendering) e pelo menu.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        public static int AddFlagWavesToOpenScene()
        {
            int n = 0;
            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                var mesh = filter.sharedMesh;
                if (!mesh || !IsFlagMesh(mesh.name)) continue;
                var b = mesh.bounds;
                bool alongX = -b.min.x > -b.min.z;
                var wave = filter.GetComponent<FlagWave>() ?? filter.gameObject.AddComponent<FlagWave>();
                wave.Configure(alongX ? Vector3.left : Vector3.back, alongX ? 0.21f : 0.19f, float.NegativeInfinity, true);
                EditorUtility.SetDirty(wave);
                n++;
            }
            return n;
        }

        static bool IsFlagMesh(string name)
        {
            int i = name.LastIndexOf("__", System.StringComparison.Ordinal);
            string own = i >= 0 ? name.Substring(i + 2) : name;
            return own.StartsWith("flag");
        }

        [MenuItem("Pirates/Protótipo/Clima: bandeiras com vento na cena aberta")]
        static void AddFlagWavesMenu()
        {
            int n = AddFlagWavesToOpenScene();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Pirates] {n} bandeiras com vento.");
        }
    }
}
