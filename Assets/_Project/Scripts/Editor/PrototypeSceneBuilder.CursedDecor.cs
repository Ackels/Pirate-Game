using System;
using System.Collections.Generic;
using Pirates.Ambient;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Ambientação da Ilha Amaldiçoada (pacote H, passo 1, ajuste de 06/11, pedido do Jhonata: "meio vazia... mais decorações, mais vida e
    /// ambientação"): pinheiros tortos e troncos caídos do Graveyard Kit, o cemitério cercado dos que vieram antes (grade de ferro, cripta,
    /// túmulos em fileira, poste com lanterna), muros caídos em volta do alto, braseiros de fogo verde (sempre acesos; a luz só à noite),
    /// esqueletos caídos pelo caminho, restos de naufrágio nas praias (botes virados, barris, caixotes, tábuas), fogos-fátuos verdes
    /// flutuando e névoa baixa rente ao chão. Tudo sem colisor nos miúdos; colisor pela caixa nas peças grandes (cripta, muros, braseiros).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static readonly Color CursedFlame = new(0.45f, 1f, 0.55f);

        static Transform DecorRoot(Transform island)
        {
            var t = island.Find("Ambientacao");
            if (t) return t;
            t = new GameObject("Ambientacao").transform;
            t.SetParent(island, false);
            return t;
        }

        static void CursedDecor(Transform island)
        {
            var root = DecorRoot(island);
            var rng = new Random(63);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Mais árvores secas (a mata do PlantLush fica rala para uma ilha morta), fora da trilha, do alto e do cemitério. Os pinheiros do
            // kit do cemitério foram testados e saíram: verde-vivo e laranja, alegres demais.
            for (int k = 0; k < 46; k++)
                if (CursedSpot(island, rng, CursedWild, 1.6f, 26f, out var spot))
                {
                    var v = PickVeg(DeadTrees, rng.NextDouble());
                    float scale = Mathf.Lerp(v.minScale, v.maxScale, (float)rng.NextDouble()) * 1.1f;
                    var tree = PlaceVeg(v, root, root.InverseTransformPoint(spot) + Vector3.down * 0.1f, R(0f, 360f), scale, $"ArvoreSeca_{k}");
                    Occluder(tree);
                    Choppable(tree, v.trunkRadius * scale, palm: false);
                }

            CursedCemeteryYard(island, root, rng);
            CursedSummitWalls(island, root);

            // Braseiros de fogo verde: no começo da trilha, no meio dela, na entrada do cemitério e na entrada do anel do alto.
            var braziers = new[]
            {
                (new Vector2(-3.5f, -49f), true), (new Vector2(3.5f, -49f), false), (new Vector2(-14f, -21f), true),
                (new Vector2(17f, -17.5f), true), (new Vector2(-4f, 6f), true), (new Vector2(4f, 6f), false),
            };
            for (int k = 0; k < braziers.Length; k++) GreenBrazier(island, root, braziers[k].Item1, braziers[k].Item2, k);

            // Esqueletos caídos pelo caminho (os marujos que tentaram antes), um ou outro com uma lápide quebrada do lado.
            var bones = new[] { new Vector2(6f, -42f), new Vector2(-15f, -30f), new Vector2(-12f, -8f), new Vector2(8f, -5f), new Vector2(-30f, 10f), new Vector2(34f, 2f) };
            for (int k = 0; k < bones.Length; k++)
            {
                var ground = SkullGround(island, bones[k]);
                Model(Kit.Graveyard, "character-skeleton", root, root.InverseTransformPoint(ground) + Vector3.up * 0.15f,
                    Quaternion.Euler(-88f, R(0f, 360f), R(-20f, 20f)), 2.2f, $"Ossada_{k}");
                if (k % 2 == 0)
                    Model(Kit.Graveyard, "gravestone-debris", root, root.InverseTransformPoint(SkullGround(island, bones[k] + new Vector2(1.6f, 0.8f))),
                        R(0f, 360f), 2.6f, $"Entulho_{k}");
            }

            // Restos de naufrágio nas praias: botes virados, barris e caixotes deitados, tábuas.
            var beach = new[]
            {
                ("boat-row-small", new Vector2(-14f, -58f), 175f), ("boat-row-large", new Vector2(40f, -50f), 170f), ("boat-row-small", new Vector2(-60f, 22f), 185f),
                ("barrel", new Vector2(10f, -57f), 90f), ("barrel", new Vector2(-62f, -26f), 90f), ("crate", new Vector2(-58f, -30f), 25f),
                ("crate-bottles", new Vector2(44f, -46f), 0f), ("barrel", new Vector2(62f, 18f), 90f), ("chest", new Vector2(-56f, 30f), 15f),
            };
            for (int k = 0; k < beach.Length; k++)
            {
                var (model, at, roll) = beach[k];
                var ground = SkullGround(island, at);
                var rot = Quaternion.Euler(0f, R(0f, 360f), roll);
                Kenney(model, root, root.InverseTransformPoint(ground) + Vector3.up * (roll > 100f ? 0.25f : 0.05f), rot,
                    model.StartsWith("boat") ? 1.25f : 1.1f, $"Destroco_{k}");
            }
            for (int k = 0; k < 8; k++)
                if (CursedSpot(island, rng, p => !CursedWild(p) ? false : true, 0.8f, 30f, out var spot, minHeight: 0.2f, maxHeight: CursedPlateau + 0.1f))
                    Model(Kit.Graveyard, "debris-wood", root, root.InverseTransformPoint(spot), R(0f, 360f), 2.4f, $"Tabuas_{k}");

            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (!t.GetComponent<ParticleSystem>() && !t.GetComponent<Light>() && !t.GetComponentInParent<NightLight>())
                    t.gameObject.isStatic = true;
        }

        /// <summary>Ponto livre no chão da ilha (sem tronco, pedra ou peça a <paramref name="clearance"/> m), na altura e inclinação pedidas.</summary>
        static bool CursedSpot(Transform island, Random rng, Func<Vector2, bool> allowed, float clearance, float maxSlope, out Vector3 world,
            float minHeight = CursedPlateau + 0.05f, float maxHeight = 99f)
        {
            var islandCollider = island.GetComponent<Collider>();
            var hits = new Collider[8];
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * CursedRX * 1.05f, (float)(rng.NextDouble() * 2 - 1) * CursedRZ * 1.05f);
                if (!allowed(p)) continue;
                if (!IslandGround(island, island.TransformPoint(new Vector3(p.x, 0f, p.y)), out var hit, out float slope)) continue;
                float h = hit.y - island.position.y;
                if (h < minHeight || h > maxHeight || slope > maxSlope) continue;
                int n = Physics.OverlapSphereNonAlloc(hit + Vector3.up * (clearance + 0.3f), clearance, hits, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < n; i++)
                    if (hits[i] != islandCollider) blocked = true;
                if (blocked) continue;
                world = hit;
                return true;
            }
            world = default;
            return false;
        }

        /// <summary>O cemitério: cerca de ferro com portão para o caminho, a cripta no fundo, túmulos em fileiras, poste com lanterna.</summary>
        static void CursedCemeteryYard(Transform island, Transform root, Random rng)
        {
            var yard = new GameObject("Cemiterio").transform;
            yard.SetParent(root, false);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const float halfX = 8f, halfZ = 7f, step = 2f;
            // Grade: o portão no lado oeste (de frente para o caminho), alguns trechos quebrados ou faltando.
            int n = 0;
            void Fence(Vector2 a, Vector2 dir, float length)
            {
                var side = Vector2.Perpendicular(dir);
                for (float d = step * 0.5f; d < length; d += step)
                {
                    var p = CursedCemetery + a + dir * d;
                    bool gate = Mathf.Abs(p.y - CursedCemetery.y) < 1.5f && Mathf.Abs(p.x - (CursedCemetery.x - halfX)) < 0.1f;
                    double roll = rng.NextDouble();
                    if (!gate && roll < 0.12) continue;
                    string model = gate ? "iron-fence-border-gate" : roll < 0.3 ? "iron-fence-damaged" : "iron-fence";
                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
                    var g = SkullGround(island, p);
                    Model(Kit.Graveyard, model, yard, yard.InverseTransformPoint(g), Quaternion.Euler(0f, yaw, gate ? 0f : R(-4f, 4f)), 2f, $"Grade_{n++}");
                }
                var corner = SkullGround(island, CursedCemetery + a);
                Model(Kit.Graveyard, "iron-fence-border-column", yard, yard.InverseTransformPoint(corner), 0f, 2f, $"Coluna_{n++}");
            }
            Fence(new Vector2(-halfX, -halfZ), Vector2.right, halfX * 2f);
            Fence(new Vector2(halfX, -halfZ), Vector2.up, halfZ * 2f);
            Fence(new Vector2(halfX, halfZ), Vector2.left, halfX * 2f);
            Fence(new Vector2(-halfX, halfZ), Vector2.down, halfZ * 2f);

            // A cripta no fundo (leste), virada para o portão.
            var crypt = Model(Kit.Graveyard, "crypt-large", yard, yard.InverseTransformPoint(SkullGround(island, CursedCemetery + new Vector2(4.5f, 0f))),
                -90f, 2.6f, "Cripta");
            SolidProp(crypt);
            Occluder(crypt);
            // Túmulos em fileiras, uns tortos, outros caídos.
            string[] graves = { "gravestone-cross", "gravestone-round", "gravestone-bevel", "gravestone-decorative", "gravestone-wide", "gravestone-broken", "grave" };
            int k = 0;
            for (float x = -5f; x <= 1f; x += 3f)
            for (float z = -4.5f; z <= 4.5f; z += 3f)
            {
                if (rng.NextDouble() < 0.15) continue;
                var g = SkullGround(island, CursedCemetery + new Vector2(x + R(-0.3f, 0.3f), z + R(-0.3f, 0.3f)));
                Model(Kit.Graveyard, graves[rng.Next(graves.Length)], yard, yard.InverseTransformPoint(g) + Vector3.down * 0.05f,
                    Quaternion.Euler(R(-10f, 10f), 90f + R(-15f, 15f), R(-8f, 8f)), 2.8f, $"Tumulo_C{k++}");
            }
            // Velas e urna na porta da cripta; poste com lanterna no portão (acende à noite).
            Model(Kit.Graveyard, "candle-multiple", yard, yard.InverseTransformPoint(SkullGround(island, CursedCemetery + new Vector2(2.2f, 1.3f))), 0f, 2.4f, "Velas");
            Model(Kit.Graveyard, "urn-round", yard, yard.InverseTransformPoint(SkullGround(island, CursedCemetery + new Vector2(2.2f, -1.4f))), 30f, 2.4f, "Urna");
            var post = Model(Kit.Graveyard, "lightpost-single", yard,
                yard.InverseTransformPoint(SkullGround(island, CursedCemetery + new Vector2(-halfX - 1.2f, 2.4f))), 90f, 2.4f, "Poste");
            NightLantern(post, realLight: true, range: 8f, intensity: 2.5f);
        }

        /// <summary>Muros de pedra caídos em meia-volta atrás do anel do alto (o lado da trilha fica aberto).</summary>
        static void CursedSummitWalls(Transform island, Transform root)
        {
            const float radius = 15.5f;
            string[] walls = { "stone-wall-damaged", "stone-wall", "stone-wall-column", "stone-wall-damaged", "stone-wall", "stone-wall-damaged", "stone-wall-column" };
            for (int i = 0; i < 14; i++)
            {
                // De 20° a 160° (o norte) e um pouco dos lados; o sul (a trilha) fica livre.
                float deg = -25f + i * (230f / 13f);
                if (i % 5 == 3) continue;
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var g = SkullGround(island, CursedSummit + dir * radius);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                var wall = Model(Kit.Graveyard, walls[i % walls.Length], root, root.InverseTransformPoint(g) + Vector3.down * 0.1f, yaw, 4.6f, $"Muro_{i}");
                SolidProp(wall);
                Occluder(wall);
            }
        }

        /// <summary>Braseiro de ferro com chama verde (sempre acesa); a luz verde só à noite (NightLight).</summary>
        static void GreenBrazier(Transform island, Transform root, Vector2 at, bool withLight, int index)
        {
            // Num pedestal de pedra (a cesta do kit é rente ao chão e sumia na câmera alta).
            var g = SkullGround(island, at);
            var stand = Model(Kit.Graveyard, "pillar-square", root, root.InverseTransformPoint(g), index * 40f, 2f, $"Pedestal_{index}");
            SolidProp(stand);
            var standRenderers = stand.GetComponentsInChildren<Renderer>();
            var standBounds = standRenderers[0].bounds;
            foreach (var r in standRenderers) standBounds.Encapsulate(r.bounds);
            var basket = Model(Kit.Graveyard, "fire-basket", root, root.InverseTransformPoint(new Vector3(g.x, standBounds.max.y - 0.05f, g.z)),
                index * 40f, 3.2f, $"Braseiro_{index}");
            var renderers = basket.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var top = new Vector3(bounds.center.x, bounds.max.y - 0.1f, bounds.center.z);

            var fire = new GameObject("ChamaVerde");
            fire.transform.SetParent(basket.transform, true);
            fire.transform.position = top;
            var ps = fire.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 1f, 0.45f, 0.9f), new Color(0.7f, 1f, 0.5f, 0.8f));
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.rateOverTime = 34f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var renderer = fire.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxMaterial("M_FX_ChamaVerde", "flame_04", additive: true);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Light light = null;
            if (withLight)
            {
                var lightGo = new GameObject("LuzVerde");
                lightGo.transform.SetParent(basket.transform, true);
                lightGo.transform.position = top + Vector3.up * 0.4f;
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = CursedFlame;
                light.range = 9f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
            }
            basket.AddComponent<NightLight>().Configure(light, 3f, null);
        }

        /// <summary>Fogos-fátuos verdes flutuando devagar sobre a ilha e névoa baixa rente ao chão.</summary>
        static void CursedAir(Transform fogRoot)
        {
            // Numa caixa sobre a ilha, de 2 a 8 m de altura (o centro da névoa fica no nível do mar).
            var wisps = FogSystem(fogRoot, "FogosFatuos", FxMaterial("M_FX_Brilho", "circle_05", additive: true), 45, 62f, edge: false,
                new Vector2(0.5f, 1.1f), new Vector2(7f, 12f), new Color(0.5f, 1f, 0.6f, 0.9f), 5f);
            var main = wisps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            var shape = wisps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(CursedRX * 1.7f, 6f, CursedRZ * 1.7f);
            var noise = wisps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.2f;
            wisps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.5f;

            FogSystem(fogRoot, "NevoaBaixa", FxMaterial("M_FX_Nevoa", "smoke_04", additive: false), 140, 66f, edge: false, new Vector2(7f, 13f),
                new Vector2(14f, 22f), new Color(0.6f, 0.66f, 0.64f, 0.28f), 1.8f);
        }
    }
}
