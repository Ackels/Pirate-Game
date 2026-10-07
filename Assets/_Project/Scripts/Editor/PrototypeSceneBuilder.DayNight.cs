using Pirates.Ambient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Dia e noite (M11, passo 1): o ciclo (Ambient/DayNightCycle, ajustes em Data/DayNight_Ciclo) em todas as cenas, ligado ao sol da
    /// cena; lanternas que acendem à noite (NightLight: luz de ponto + brilho do vidro) nas lanternas do porto e na popa dos navios (o do
    /// jogador com luz de verdade; os inimigos só com o brilho, para não somar luzes na GPU integrada). Casas e torres com janelas
    /// acesas (placas emissivas, sem luz) e uma luz curta na frente de cada casa; lanternas no esconderijo, no forte e no acampamento.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static readonly Color LanternColor = new(1f, 0.72f, 0.4f);

        static DayNightSettings DayNightAsset() => LoadOrCreateAsset<DayNightSettings>(DataFolder + "/DayNight_Ciclo.asset");

        static void CreateDayNight(Light sun)
        {
            var go = new GameObject("CicloDiaNoite");
            go.AddComponent<DayNightCycle>().Configure(DayNightAsset(), sun);
        }

        /// <summary>
        /// Acende a lanterna à noite: luz de ponto (sem sombra) na altura do vidro e uma bolinha de brilho no lugar da chama. Mede o
        /// modelo para achar o vidro (~70% da altura).
        /// </summary>
        static void NightLantern(GameObject lantern, bool realLight = true, float range = 9f, float intensity = 3f)
        {
            if (!lantern) return;
            var renderers = lantern.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Vector3 flame = new(bounds.center.x, bounds.min.y + bounds.size.y * 0.68f, bounds.center.z);

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "BrilhoDaLanterna";
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.transform.SetParent(lantern.transform, true);
            glow.transform.position = flame;
            float size = Mathf.Max(0.08f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.42f);
            SetWorldScale(glow.transform, Vector3.one * size);
            var renderer = glow.GetComponent<Renderer>();
            renderer.sharedMaterial = GetEmissiveMaterial("M_LanternaAcesa", LanternColor, 2.2f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            glow.SetActive(false);

            Light light = null;
            if (realLight)
            {
                var lightGo = new GameObject("LuzDaLanterna");
                lightGo.transform.SetParent(lantern.transform, false);
                lightGo.transform.position = flame;
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LanternColor;
                light.range = range;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
            }
            lantern.AddComponent<NightLight>().Configure(light, intensity, glow);
        }

        /// <summary>Lanterna na popa, presa ao navio (fora do "Visual": vale para a escuna do jogador também).</summary>
        static void ShipLantern(Transform ship, ShipHull hull, bool realLight)
        {
            Vector3 top = hull.colliderCenter + new Vector3(0f, hull.colliderSize.y * 0.5f + 0.2f, -hull.colliderSize.z * 0.5f + 1.2f);
            var lantern = Model(Kit.Town, "lantern", ship, top, 0f, 1.1f, "Lanterna_Popa");
            foreach (var c in lantern.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            NightLantern(lantern, realLight, range: 12f, intensity: 3.5f);
        }

        // ---------- Janelas acesas e lanternas das construções ----------

        static readonly Color WindowColor = new(1f, 0.64f, 0.3f);

        /// <summary>Luz de dentro vista pela janela: emissivo, dos dois lados (a placa fica no meio da parede).</summary>
        static Material WindowGlowMaterial()
        {
            var mat = GetEmissiveMaterial("M_JanelaAcesa", WindowColor, 2.2f);
            if (mat.HasProperty("_Cull") && mat.GetFloat("_Cull") != 0f)
            {
                mat.SetFloat("_Cull", 0f);
                mat.doubleSidedGI = true;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>
        /// Placa emissiva numa abertura de janela, em coordenadas do modelo <paramref name="piece"/>, posta em <paramref name="group"/>
        /// (o grupo é o "vidro" de um NightLight). A placa fica dentro da parede: só aparece onde a parede tem o buraco ou o nicho.
        /// </summary>
        static void WindowGlow(Transform piece, Transform group, Vector3 center, Vector2 size, float yaw)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Janela";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(piece, false);
            quad.transform.SetLocalPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.transform.SetParent(group, true);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = WindowGlowMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            quad.isStatic = true;
        }

        /// <summary>
        /// Parede com janela do Fantasy Town Kit (peça de 1 m: parede em x −0,5…−0,4, abertura em y 0,2…0,8 e z ±0,2 no máximo, sem
        /// vidro). A placa fica no meio da espessura (x −0,45) e um pouco maior que a maior abertura.
        /// </summary>
        static void TownWindowGlow(Transform piece, Transform group) =>
            WindowGlow(piece, group, new Vector3(-0.45f, 0.5f, 0f), new Vector2(0.5f, 0.66f), 90f);

        /// <summary>
        /// Janelas acesas nas peças de pedra do kit pirata (nichos fundos, sem buraco: a placa fica 1,5 cm na frente do fundo do nicho,
        /// medido com raios): tower-middle-windows (uma em +Z e outra em −Z) e castle-window (uma em +Z).
        /// </summary>
        static void PirateWindowGlow(GameObject piece, string model, float from)
        {
            var group = new GameObject("JanelasAcesas").transform;
            group.SetParent(piece.transform, false);
            if (model == "tower-middle-windows")
            {
                WindowGlow(piece.transform, group, new Vector3(0f, 1.1f, 1.455f), new Vector2(0.74f, 0.76f), 0f);
                WindowGlow(piece.transform, group, new Vector3(0f, 1.1f, -1.455f), new Vector2(0.74f, 0.76f), 0f);
            }
            else WindowGlow(piece.transform, group, new Vector3(0f, 2.9f, 1.115f), new Vector2(0.74f, 0.76f), 0f);
            group.gameObject.SetActive(false);
            piece.AddComponent<NightLight>().Configure(null, 0f, group.gameObject, from);
        }

        /// <summary>
        /// Casa acesa à noite: ~3 de cada 4 janelas brilham e uma luz curta (sem sombra) na frente da porta ilumina a rua; cada casa
        /// acende numa hora (nível sorteado), para a vila não acender toda de uma vez.
        /// </summary>
        static void HouseNightLight(Transform house, Transform group, Vector3 doorLocal, float cell, System.Random rng)
        {
            group.gameObject.SetActive(false);
            var lightGo = new GameObject("LuzDaCasa");
            lightGo.transform.SetParent(house, false);
            lightGo.transform.localPosition = doorLocal + new Vector3(0f, cell * 0.8f, 1.4f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = WindowColor;
            light.range = 7f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            house.gameObject.AddComponent<NightLight>().Configure(light, 1.8f, group.gameObject, 0.12f + (float)rng.NextDouble() * 0.45f);
        }

        /// <summary>Lanterna de poste (Fantasy Town Kit × 1,6, como as do porto) acesa à noite, no chão <paramref name="ground"/> (mundo).</summary>
        static void PostLantern(Transform parent, Vector3 ground, string name, bool realLight = true, float range = 9f) =>
            NightLantern(Model(Kit.Town, "lantern", parent, parent.InverseTransformPoint(ground), 0f, 1.6f, name), realLight, range);

        static void SetWorldScale(Transform t, Vector3 world)
        {
            var parent = t.parent ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(world.x / Mathf.Max(0.0001f, parent.x), world.y / Mathf.Max(0.0001f, parent.y),
                world.z / Mathf.Max(0.0001f, parent.z));
        }
    }
}
