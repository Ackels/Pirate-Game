using Pirates.Controls;
using Pirates.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Borda do mundo (12/11; cena 07): círculo em volta do mapa (do megalodonte à lula e à névoa da Ilha Amaldiçoada, com as áreas onde eles
    /// lutam), com uma névoa que segue o navio perto da borda e o devolve para dentro.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // O mapa vai de x −930 a 1090 (o megalodonte persegue até 230 m do covil) e de z −660 a 970: centro e raio com folga.
        // Ilha nova ou inimigo mais longe: mude aqui (gerador) ou no objeto "BordaDoMundo" da cena (posição = centro, Raio no Inspector).
        static readonly Vector3 WorldEdgeCenter = new(0f, 0f, 100f);
        const float WorldEdgeRadius = 1350f;

        /// <summary>Névoa do mar (12/11, "como no Sea of Thieves"): mude aqui para enxergar mais longe ou mais perto (todas as cenas).</summary>
        public const float SeaFogDensity = 0.0034f;
        const float SeaFarClip = 1200f;

        static void AddWorldEdge(PlayerModeSwitcher switcher, GameObject hud)
        {
            var root = new GameObject("BordaDoMundo");
            root.transform.position = WorldEdgeCenter;
            var mat = FxMaterial("M_FX_Nevoa", "smoke_04", additive: false);
            var fog = FogSystem(root.transform, "NevoaDaBorda", mat, 220, 1f, edge: false, new Vector2(40f, 70f), new Vector2(24f, 34f),
                new Color(0.66f, 0.71f, 0.72f, 0.55f), 8f);
            // Uma faixa de névoa de 420 m ao longo da borda (o emissor gira para ficar de frente para o navio).
            var main = fog.main;
            main.prewarm = false;
            var shape = fog.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(420f, 6f, 70f);
            shape.rotation = Vector3.zero;
            var edge = root.AddComponent<WorldEdge>();
            edge.Configure(switcher, fog, WorldEdgeRadius);
            hud.AddComponent<WorldEdgeHUD>().Bind(edge);
        }

        /// <summary>Põe a borda na cena aberta (07) sem regerar e salva.</summary>
        public static string AddWorldEdgeToOpenScene()
        {
            if (Object.FindAnyObjectByType<WorldEdge>(FindObjectsInactive.Include)) return "já tinha";
            var switcher = Object.FindAnyObjectByType<PlayerModeSwitcher>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (!switcher || !hud) return "sem navio ou HUD";
            AddWorldEdge(switcher, hud.gameObject);
            var scene = switcher.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "ok";
        }
    }
}
