using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ilha Amaldiçoada (pacote H, passo 1, 06/11; HISTORIA.md, capítulo 6; cena 07): ilha nova no lugar vazio "D" do mapa espaçado
    /// (noroeste), onde Lázaro Negro fez o trato com a maldição da prata. Chão de pedra cinza e terra escura, só árvores secas, túmulos ao
    /// longo da trilha que sobe da praia do sul até o alto (o anel de pilares e o altar; o templo de Lázaro é o passo 3), um casco
    /// naufragado na praia oeste. Em volta, a muralha de névoa (<see cref="CursedFog"/>): fechada até o capítulo 6 (o navio se perde e
    /// volta), rala depois. Layout local (X leste, Z norte) a partir do centro da ilha.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string CursedId = "ilha_amaldicoada", CursedName = "Ilha Amaldiçoada";
        static Vector3 CursedCenter => new(-600f, 0f, 640f);
        const float CursedRX = 70f, CursedRZ = 62f, CursedPlateau = 1.4f;
        /// <summary>O alto da ilha (onde vai o templo de Lázaro) e a altura do chão lá.</summary>
        static Vector2 CursedSummit => new(0f, 18f);
        const float CursedSummitHeight = 6.2f;
        static readonly Vector2[] CursedTrail = { new(0f, -57f), new(0f, -38f), new(-9f, -20f), new(-2f, 0f), new(0f, 8f) };
        /// <summary>O cemitério dos que vieram antes (leste da trilha), com o caminho de terra que sai dela.</summary>
        static Vector2 CursedCemetery => new(27f, -20f);
        static readonly Vector2[] CemeteryPath = { new(-6f, -26f), new(10f, -22f), new(19f, -20f) };

        static IslandSpec CursedIslandSpec()
        {
            var paths = new List<(Vector2, Vector2, float)> { (CursedSummit, CursedSummit, 9f) };
            for (int k = 1; k < CursedTrail.Length; k++) paths.Add((CursedTrail[k - 1], CursedTrail[k], 3.5f));
            for (int k = 1; k < CemeteryPath.Length; k++) paths.Add((CemeteryPath[k - 1], CemeteryPath[k], 2.5f));
            return new IslandSpec
            {
                name = "Ilha_Amaldicoada", center = CursedCenter, radiusX = CursedRX, radiusZ = CursedRZ, coastNoise = 0.16f, coastOutwardOnly = true,
                seed = 61, plateau = CursedPlateau, beachWidth = 5f, inlandNoise = 0.5f, cell = 1.8f, cursed = true,
                anchors = new[] { (-90f, CursedRZ, 20f) },
                hills = new[]
                {
                    (CursedSummit, 44f, 5f), (new Vector2(-44f, -6f), 14f, 3f), (new Vector2(44f, 12f), 15f, 4f), (new Vector2(26f, -34f), 10f, 2f),
                },
                flats = new[]
                {
                    (CursedSummit, 10f, 16f, CursedSummitHeight),
                    (new Vector2(0f, -46f), 6f, 4f, CursedPlateau),
                    (CursedCemetery, 9f, 6f, CursedPlateau + 0.6f),
                },
                paths = paths.ToArray(),
            };
        }

        static bool CursedWild(Vector2 p)
        {
            if (Mathf.Abs(p.x) < 9f && p.y < -44f) return false;
            if ((p - CursedSummit).magnitude < 19f) return false;
            if ((p - CursedCemetery).magnitude < 12f) return false;
            for (int k = 1; k < CemeteryPath.Length; k++)
                if (DistanceToSegment(p, CemeteryPath[k - 1], CemeteryPath[k]) < 3f) return false;
            for (int k = 1; k < CursedTrail.Length; k++)
                if (DistanceToSegment(p, CursedTrail[k - 1], CursedTrail[k]) < 4f) return false;
            return true;
        }

        static void AddCursedIsle(PlayerModeSwitcher switcher, Material deck, Material hull, GameObject hud, LandLootAssets landLoot = null,
            LootItems items = null)
        {
            var spec = CursedIslandSpec();
            var islands = GameObject.Find("Islands");
            var island = CreateIslandTerrain(islands ? islands.transform : null, spec);
            // Só árvores secas (as mesmas do cemitério da Caveira); nada verde.
            DeadTreeZone = _ => true;
            DeadTreeShare = 1.0;
            PlantLush(island, spec, CursedWild, trees: 42, bushes: 0, flowers: 0, grass: 0, ground: 14, rocks: 16, palms: 0, seed: 61);
            DeadTreeZone = null;
            DeadTreeShare = 0.75;

            var landing = SkullLanding(island, "Desembarque_IlhaAmaldicoada", CursedName, new Vector2(0f, -CursedRZ), Vector2.down, deck, hull,
                "flag-pirate");
            landing.gameObject.AddComponent<WorldMarker>().Configure(CursedId, CursedName, 6f);
            Shipwreck(island, new Vector2(-66f, -16f));
            CursedGraves(island);
            CursedSummitRing(island);
            CursedTemple(island);
            CursedDecor(island);
            foreach (var t in island.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Tumulo_") || t.name.StartsWith("Pilar_") || t.name == "Altar") t.gameObject.isStatic = true;

            BakeNavMeshVolume("NavMesh_IlhaAmaldicoada", "_NavMesh_Amaldicoada", CursedCenter, new Vector3(CursedRX * 2.5f, 36f, CursedRZ * 2.5f));
            // Os mortos da ilha (passo 2): depois do NavMesh (os corpos não entram no NavMesh).
            AddCursedDead(island, switcher ? switcher.Character : null, hud, landLoot, items);

            // A muralha de névoa e a névoa rala sobre a ilha.
            var root = new GameObject("NevoaAmaldicoada");
            root.transform.position = CursedCenter;
            var mat = FxMaterial("M_FX_Nevoa", "smoke_04", additive: false);
            var wall = FogSystem(root.transform, "Muralha", mat, 260, 230f, edge: true, new Vector2(34f, 58f), new Vector2(26f, 36f),
                new Color(0.62f, 0.68f, 0.66f, 0.5f), 7f);
            var mist = FogSystem(root.transform, "NevoaRala", mat, 90, 110f, edge: false, new Vector2(16f, 30f), new Vector2(18f, 26f),
                new Color(0.55f, 0.62f, 0.6f, 0.22f), 4f);
            CursedAir(root.transform);
            var fog = root.AddComponent<CursedFog>();
            fog.Configure(switcher, wall, mist);
            hud.AddComponent<CursedFogHUD>().Bind(fog);
        }

        /// <summary>Túmulos tortos dos dois lados da trilha, uns caídos (os marujos que vieram antes).</summary>
        static void CursedGraves(Transform island)
        {
            string[] models = { "gravestone-cross", "gravestone-broken", "gravestone-round", "gravestone-bevel", "gravestone-debris", "cross-wood", "grave" };
            var rng = new System.Random(62);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int n = 0;
            for (int k = 1; k < CursedTrail.Length; k++)
            {
                var a = CursedTrail[k - 1];
                var b = CursedTrail[k];
                var dir = (b - a).normalized;
                var side = new Vector2(dir.y, -dir.x);
                float length = (b - a).magnitude;
                for (float d = 4f; d < length - 2f; d += R(5f, 8f))
                {
                    float s = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var p = a + dir * d + side * s * R(3.5f, 6.5f);
                    if ((p - CursedSummit).magnitude < 14f) continue;
                    var ground = SkullGround(island, p);
                    var rot = Quaternion.Euler(R(-12f, 12f), Mathf.Atan2(-side.x * s, -side.y * s) * Mathf.Rad2Deg + R(-25f, 25f), R(-10f, 10f));
                    Model(Kit.Graveyard, models[rng.Next(models.Length)], island, island.InverseTransformPoint(ground) + Vector3.down * 0.05f,
                        rot, 2.8f, $"Tumulo_{n++}");
                }
            }
        }

        /// <summary>No alto: um anel de pilares velhos (alguns quebrados) em volta de um altar de pedra (o templo é o passo 3).</summary>
        static void CursedSummitRing(Transform island)
        {
            string[] pillars = { "pillar-large", "pillar-obelisk", "pillar-large", "gravestone-debris", "pillar-square", "pillar-large", "pillar-obelisk", "debris" };
            const float radius = 9.5f;
            for (int i = 0; i < pillars.Length; i++)
            {
                float a = (i + 0.5f) / pillars.Length * Mathf.PI * 2f;
                var p = CursedSummit + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                // A entrada do anel fica virada para a trilha (sul): sem pilar ali.
                if (Vector2.Angle(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), Vector2.down) < 25f) continue;
                var ground = SkullGround(island, p);
                var pillar = Model(Kit.Graveyard, pillars[i], island, island.InverseTransformPoint(ground) + Vector3.down * 0.1f,
                    Quaternion.Euler(0f, i * 47f, i % 3 == 1 ? 6f : 0f), 5f, $"Pilar_{i}");
                SolidProp(pillar);
            }
            var altar = Model(Kit.Graveyard, "altar-stone", island, island.InverseTransformPoint(SkullGround(island, CursedSummit)), 0f, 4f, "Altar");
            SolidProp(altar);
        }

        /// <summary>Névoa de partículas em espaço de mundo: na borda de um círculo (a muralha) ou espalhada nele; já cheia ao começar.</summary>
        static ParticleSystem FogSystem(Transform parent, string name, Material material, int max, float radius, bool edge, Vector2 size,
            Vector2 lifetime, Color color, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            var emission = ps.emission;
            emission.rateOverTime = max / ((lifetime.x + lifetime.y) * 0.5f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = edge ? 0.06f : 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            // Entra e sai devagar (sem "estalar" na tela).
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 3f;
            return ps;
        }
    }
}
