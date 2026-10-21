using System.Collections.Generic;
using Pirates.Combat;
using Pirates.LandCombat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// O Capelão Fantasma (C5, passo 3, 20/10): o mesmo fantasma-caveira das almas, quase o dobro do tamanho, branco-azulado, com uma
    /// luz fria própria, um anel de velas girando sobre a cabeça e o ectoplasma azul. Fica no salão das catacumbas, na frente do baú (que
    /// ele tranca), com o círculo das almas do chão (LineRenderer) e três bolas de fogo-fátuo prontas e desligadas. Combate em
    /// <see cref="ChaplainGhost"/>.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const float ChaplainScale = 0.95f;
        static readonly Color ChaplainTint = new(1.35f, 1.65f, 2.3f, 1f);

        static ChaplainGhost Chaplain(Transform parent, Transform root, Vector3 local, List<CryptMonster> calledSouls, Material glow)
        {
            var go = new GameObject("CapelaoFantasma");
            go.transform.SetParent(parent, false);
            // Virado para a entrada do salão (oeste).
            go.transform.SetLocalPositionAndRotation(local + Vector3.up * 0.6f, Quaternion.Euler(0f, -90f, 0f));
            var health = go.AddComponent<Health>();
            health.Configure(480f);
            var body = new GameObject("Corpo").transform;
            body.SetParent(go.transform, false);
            var scale = new GameObject("Escala").transform;
            scale.SetParent(body, false);
            scale.localScale = Vector3.one * ChaplainScale;
            var model = MonsterModel(GhostModelPath, scale);
            var mat = GhostMaterial("M_CapelaoFantasma", ChaplainTint, new Color(0.15f, 0.3f, 0.6f));
            var renderers = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                renderers.Add(r);
            }
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = GhostAnimator("CapelaoFantasma", caster: true);
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.6f, 0f);
            capsule.radius = 0.85f;
            capsule.height = 3.2f;
            capsule.enabled = false;

            var wisps = GhostWisps(body, glow);
            wisps.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var main = wisps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.75f, 1f, 0.85f), new Color(0.8f, 0.95f, 1f, 0.55f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.maxParticles = 160;
            var shape = wisps.shape;
            shape.radius = 0.9f;
            var emission = wisps.emission;
            emission.rateOverTime = 14f;

            // Luz fria própria: no escuro do salão ele ilumina o chão em volta (sem sombra).
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(body, false);
            lightGo.transform.localPosition = new Vector3(0f, 2f, 0.4f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.78f, 1f);
            light.intensity = 2.4f;
            light.range = 7f;
            light.shadows = LightShadows.None;

            var halo = ChaplainHalo(body);
            // Largura posta pelo Capelão (a do traço × 1).
            var ring = BombAimLine(parent, "CirculoDasAlmas", 1f);
            ring.positionCount = 41;
            ring.loop = false;
            var orbs = new Transform[3];
            for (int i = 0; i < orbs.Length; i++) orbs[i] = WillOWisp(parent, i, glow);

            var souls = new List<RestlessSoul>();
            foreach (var s in calledSouls)
                if (s is RestlessSoul soul) souls.Add(soul);
            var chaplain = go.AddComponent<ChaplainGhost>();
            chaplain.ConfigureBase(health, animator, "capelao_fantasma", 40);
            chaplain.Configure(body, renderers.ToArray(), capsule, wisps, light, halo, ring, orbs, souls.ToArray());
            return chaplain;
        }

        /// <summary>Cinco velas do kit do cemitério girando num anel acima da cabeça, com a chama acesa.</summary>
        static Transform ChaplainHalo(Transform body)
        {
            var halo = new GameObject("Aureola").transform;
            halo.SetParent(body, false);
            halo.localPosition = new Vector3(0f, 2.7f, 0f);
            var flame = GetMaterial("M_Capelao_Chama", new Color(1f, 0.75f, 0.35f), 0.2f);
            flame.SetColor("_EmissionColor", new Color(3f, 1.9f, 0.7f));
            Emissive(flame);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.65f;
                var candle = Model(Kit.Graveyard, "candle", halo, at, i * 72f, 1.4f, "Vela");
                foreach (var r in candle.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                Part(PrimitiveType.Sphere, "Chama", halo, at + Vector3.up * 0.42f, new Vector3(0.07f, 0.11f, 0.07f), Quaternion.identity, flame);
            }
            return halo;
        }

        /// <summary>Uma bola de fogo-fátuo: núcleo azul aceso, luz curta e um rastro de brilhos soltos no mundo.</summary>
        static Transform WillOWisp(Transform parent, int index, Material glow)
        {
            var root = new GameObject($"FogoFatuo_{index}").transform;
            root.SetParent(parent, false);
            var core = GetMaterial("M_FogoFatuo", new Color(0.55f, 0.85f, 1f), 0.2f);
            // Azul-claro (com 1,2/2,4/4 o núcleo estourava em branco).
            core.SetColor("_EmissionColor", new Color(0.3f, 0.75f, 1.5f));
            Emissive(core);
            var ball = Part(PrimitiveType.Sphere, "Nucleo", root, Vector3.zero, Vector3.one * 0.3f, Quaternion.identity, core);
            ball.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.8f, 1f);
            light.intensity = 1.6f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;

            var go = new GameObject("Rastro");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.75f, 1f, 0.8f), new Color(0.8f, 0.95f, 1f, 0.6f));
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 6f;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            root.gameObject.SetActive(false);
            return root;
        }
    }
}
