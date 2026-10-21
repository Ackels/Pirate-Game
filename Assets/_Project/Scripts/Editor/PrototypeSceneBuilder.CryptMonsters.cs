using System.Collections.Generic;
using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Monstros das catacumbas (C5, passo 2, 20/10): almas penadas (o fantasma-caveira do Ultimate Monsters do Quaternius, CC0) num
    /// material próprio sem luz, verde e meio transparente, com ectoplasma brilhando; morcegos (Animated Monsters do Quaternius, CC0) com
    /// as cores clareadas (o roxo do kit sumia no escuro) e os olhos vermelhos acesos. Três grupos (<see cref="CryptEncounter"/>): 3 almas no
    /// ossuário, o bando de 6 morcegos com 1 alma na cripta e o Capelão Fantasma no salão (passo 3, PrototypeSceneBuilder.Chaplain.cs).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string GhostModelPath = "Assets/ThirdParty/Quaternius_UltimateMonsters/Models/Flying_Ghost_Skull.fbx";
        const string BatModelPath = "Assets/ThirdParty/Quaternius_AnimatedMonsters/Models/Bat.fbx";
        const string MonstersAtlasPath = "Assets/ThirdParty/Quaternius_UltimateMonsters/Models/Textures/Atlas_Monsters.png";
        const float GhostScale = 0.55f, BatScale = 0.24f;

        /// <summary>Os monstros das três salas; devolve o Capelão (tranca o baú do salão).</summary>
        static ChaplainGhost AddCryptMonsters(Transform root, FootCharacter captain)
        {
            LoopClips(GhostModelPath, "Fast_Flying", "Flying_Idle");
            LoopClips(BatModelPath, "Bat_Flying");
            var ghostMat = GhostMaterial();
            var glow = FxMaterial("M_FX_Brilho", "circle_05", additive: true);
            var batMats = BatMaterials();
            var g = new GameObject("Monstros").transform;
            g.SetParent(root, false);

            // Ossuário (x −20…−10, z −5…5): três almas.
            var ossuary = new List<CryptMonster>();
            foreach (var (x, z) in new[] { (-15f, 0f), (-17f, 2f), (-13f, -2f) })
                ossuary.Add(Ghost(g, new Vector3(x, 0f, z), ossuary.Count, ghostMat, glow));
            Encounter(g, "Encontro_Ossuario", root, new Rect(-20f, -5f, 10f, 10f), 2, ossuary, captain);

            // Cripta (x −22…−8, z −23…−15): o bando de morcegos no alto e uma alma; 2 atacando por vez (com 3 os avisos se embolavam).
            var crypt = new List<CryptMonster>();
            var rng = new System.Random(83);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f, r = 1.2f + (float)rng.NextDouble() * 1.4f;
                crypt.Add(Bat(g, new Vector3(-15f + Mathf.Cos(a) * r, 3.4f + (float)rng.NextDouble() * 0.5f, -19f + Mathf.Sin(a) * r * 0.6f), k, batMats));
            }
            crypt.Add(Ghost(g, new Vector3(-11f, 0f, -19f), 3, ghostMat, glow));
            Encounter(g, "Encontro_Cripta", root, new Rect(-22f, -23f, 14f, 8f), 2, crypt, captain);

            // Salão (x 4…20, z −28…−10): o Capelão e as duas almas que ele chama na fúria (1 alma atacando por vez; ele não espera a vez).
            var called = new List<CryptMonster>
            {
                Ghost(g, new Vector3(7.5f, 0f, -25.5f), 4, ghostMat, glow, summoned: true),
                Ghost(g, new Vector3(16.5f, 0f, -25.5f), 5, ghostMat, glow, summoned: true),
            };
            var chaplain = Chaplain(g, root, new Vector3(12f, 0f, -20.5f), called, glow);
            var hall = new List<CryptMonster>(called) { chaplain };
            Encounter(g, "Encontro_Salao", root, new Rect(4f, -28f, 16f, 18f), 1, hall, captain);

            // Aviso de golpe sobre os monstros ("!" e "!!" = erga a guarda), no HUD da cena.
            var hud = Object.FindAnyObjectByType<Pirates.UI.GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<Pirates.UI.CryptWarningHUD>()) hud.gameObject.AddComponent<Pirates.UI.CryptWarningHUD>();   // já vem do CreateGameHud
            return chaplain;
        }

        static void Encounter(Transform parent, string name, Transform root, Rect room, int attackers, List<CryptMonster> members, FootCharacter captain)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var area = new Bounds(root.TransformPoint(new Vector3(room.center.x, 2.5f, room.center.y)), new Vector3(room.width, 7f, room.height));
            go.AddComponent<CryptEncounter>().Configure(captain, area, attackers, members.ToArray(), ErmidaId);
        }

        // ---------- Alma penada ----------

        /// <summary>Uma alma penada; <paramref name="summoned"/> = só acorda chamada pelo Capelão (e não dá fama).</summary>
        static RestlessSoul Ghost(Transform parent, Vector3 local, int index, Material mat, Material glow, bool summoned = false)
        {
            var root = new GameObject($"AlmaPenada_{index}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = local + Vector3.up * 0.35f;
            var health = root.AddComponent<Health>();
            health.Configure(60f);
            var body = new GameObject("Corpo").transform;
            body.SetParent(root.transform, false);
            var scale = new GameObject("Escala").transform;
            scale.SetParent(body, false);
            scale.localScale = Vector3.one * GhostScale;
            var model = MonsterModel(GhostModelPath, scale);
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
            animator.runtimeAnimatorController = GhostAnimator();
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.95f, 0f);
            capsule.radius = 0.5f;
            capsule.height = 1.9f;
            capsule.enabled = false;
            var soul = root.AddComponent<RestlessSoul>();
            soul.ConfigureBase(health, animator, "alma_penada", 3, summoned);
            soul.Configure(body, renderers.ToArray(), capsule, GhostWisps(body, glow));
            return soul;
        }

        /// <summary>
        /// Material das almas: sem luz (aparecem no escuro), o atlas do kit tingido de verde, transparência por alma no jogo. O Capelão usa
        /// o mesmo com outro nome e outra tinta (branco-azulado).
        /// </summary>
        static Material GhostMaterial(string name = "M_AlmaPenada", Color? tint = null, Color? emission = null)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !mat;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            SetIfPresent(mat, "_Surface", 1f);
            SetIfPresent(mat, "_Blend", 0f);
            SetIfPresent(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetIfPresent(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetIfPresent(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetIfPresent(mat, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetIfPresent(mat, "_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(MonstersAtlasPath);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            mat.mainTexture = atlas;
            // Tom acima de 1 e brilho verde por dentro: o corpo roxo do atlas ficava escuro e sumia no fundo (só a caveira aparecia).
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint ?? new Color(1.1f, 1.7f, 1.25f, 1f));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emission ?? new Color(0.12f, 0.42f, 0.22f));
                mat.EnableKeyword("_EMISSION");
            }
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Ectoplasma: brilhos verdes que sobem devagar em volta da alma (e um punhado ao aparecer, sumir e morrer).</summary>
        static ParticleSystem GhostWisps(Transform parent, Material glow)
        {
            var go = new GameObject("Ectoplasma");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1f, 0.6f, 0.8f), new Color(0.75f, 1f, 0.85f, 0.5f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Animações da alma (o Capelão usa o mesmo esqueleto, com o "Lancar" do soco para os feitiços).</summary>
        static RuntimeAnimatorController GhostAnimator(string file = "AlmaPenada", bool caster = false)
        {
            string path = $"{AnimationFolder}/{file}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller) return controller;
            EnsureFolder(Root, "Animation");
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var idle = State(machine, "Flutuar", MonsterClip(GhostModelPath, "Flying_Idle"));
            State(machine, "Voar", MonsterClip(GhostModelPath, "Fast_Flying"));
            BackTo(State(machine, "Golpe", MonsterClip(GhostModelPath, "Headbutt")), idle);
            if (caster) BackTo(State(machine, "Lancar", MonsterClip(GhostModelPath, "Punch")), idle);
            BackTo(State(machine, "Levar", MonsterClip(GhostModelPath, "HitReact")), idle);
            State(machine, "Morrer", MonsterClip(GhostModelPath, "Death"));
            machine.defaultState = idle;
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ---------- Morcego ----------

        static CryptBat Bat(Transform parent, Vector3 local, int index, Dictionary<string, Material> mats)
        {
            var root = new GameObject($"Morcego_{index}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = local;
            var health = root.AddComponent<Health>();
            health.Configure(15f);
            var scale = new GameObject("Corpo").transform;
            scale.SetParent(root.transform, false);
            scale.localScale = Vector3.one * BatScale;
            var model = MonsterModel(BatModelPath, scale);
            // O pivô do kit fica longe do corpo: centraliza a caixa da pose no objeto.
            var b = new Bounds();
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] && mats.TryGetValue(slots[i].name, out var m)) slots[i] = m;
                r.sharedMaterials = slots;
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            if (any) model.transform.position -= b.center - root.transform.position;
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = BatAnimator();
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.4f;
            var bat = root.AddComponent<CryptBat>();
            bat.ConfigureBase(health, animator, "morcego", 1);
            bat.Configure(sphere);
            return bat;
        }

        /// <summary>Cores do morcego por nome do material do kit, mais claras (de noite o roxo quase preto sumia); olhos vermelhos acesos.</summary>
        static Dictionary<string, Material> BatMaterials()
        {
            var eyes = GetMaterial("M_Morcego_Olhos", new Color(0.9f, 0.08f, 0.05f), 0.4f);
            eyes.SetColor("_EmissionColor", new Color(3f, 0.25f, 0.1f));
            Emissive(eyes);
            return new Dictionary<string, Material>
            {
                ["Main"] = GetMaterial("M_Morcego_Pelo", new Color(0.26f, 0.18f, 0.32f), 0.15f),
                ["Belly"] = GetMaterial("M_Morcego_Barriga", new Color(0.55f, 0.4f, 0.26f), 0.1f),
                ["Black"] = GetMaterial("M_Morcego_Asa", new Color(0.12f, 0.1f, 0.14f), 0.1f),
                ["Nose"] = GetMaterial("M_Morcego_Focinho", new Color(0.45f, 0.24f, 0.32f), 0.2f),
                ["Eyes"] = eyes,
            };
        }

        static RuntimeAnimatorController BatAnimator()
        {
            string path = AnimationFolder + "/Morcego.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller) return controller;
            EnsureFolder(Root, "Animation");
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var fly = State(machine, "Voar", MonsterClip(BatModelPath, "Bat_Flying"));
            BackTo(State(machine, "Atacar", MonsterClip(BatModelPath, "Bat_Attack")), fly);
            BackTo(State(machine, "Levar", MonsterClip(BatModelPath, "Bat_Hit")), fly);
            State(machine, "Morrer", MonsterClip(BatModelPath, "Bat_Death"));
            machine.defaultState = fly;
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ---------- Ajuda ----------

        static GameObject MonsterModel(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new System.IO.FileNotFoundException($"Monstro não encontrado: {path}");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Model";
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return go;
        }

        static AnimationClip MonsterClip(string path, string clip)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && c.name.EndsWith("|" + clip)) return c;
            Debug.LogWarning($"[Pirates] Clipe {clip} não encontrado em {path}");
            return null;
        }

        /// <summary>Liga o loop dos clipes de voo no importador (o kit traz o Fast_Flying e o Bat_Flying sem loop).</summary>
        static void LoopClips(string path, params string[] endings)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) return;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (var c in clips)
            foreach (var e in endings)
                if (c.name.EndsWith(e) && !c.loopTime)
                {
                    c.loopTime = true;
                    changed = true;
                }
            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
        {
            var s = machine.AddState(name);
            s.motion = motion;
            return s;
        }

        /// <summary>Clipe que toca uma vez e volta sozinho (golpe, levar golpe).</summary>
        static void BackTo(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = 0.1f;
        }

        // ---------- Sons ----------

        const string GhostMoanPath = GeneratedAudioFolder + "/AlmaPenada_Gerado.wav", BatScreechPath = GeneratedAudioFolder + "/Morcego_Gerado.wav";

        /// <summary>
        /// Gemido da alma (2,2 s, feito aqui): tom de 210–290 Hz que sobe e desce devagar com tremor, o 2º harmônico e um sopro de ruído
        /// filtrado (a caixinha do notebook não toca abaixo de ~150 Hz); entra em 0,4 s e some em 0,9 s.
        /// </summary>
        static AudioClip GhostMoanClip() => GeneratedClip(GhostMoanPath, "alma-v1", 0.12f, () =>
        {
            const int rate = AudioRate;
            int n = (int)(2.2f * rate);
            var s = new float[n];
            var rng = new System.Random(707);
            double phase = 0.0;
            float low = 0f, band = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = 230f + 45f * Mathf.Sin(2f * Mathf.PI * 0.45f * t + 0.6f) + 20f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01(t / 0.8f);
                phase += 2.0 * System.Math.PI * f / rate;
                float tone = (float)(System.Math.Sin(phase) + 0.35 * System.Math.Sin(2.0 * phase) + 0.12 * System.Math.Sin(3.0 * phase));
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float fc = 2f * Mathf.Sin(Mathf.PI * (f * 3f) / rate);
                low += fc * band;
                float high = white - low - 0.6f * band;
                band += fc * high;
                float env = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((2.2f - t) / 0.9f);
                s[i] = (tone * 0.6f + band * 0.5f) * env * env;
            }
            return s;
        }, loop: false);

        /// <summary>Guincho do morcego (0,5 s, feito aqui): três chiados de 5,2 → 3,2 kHz com harmônico e um pouco de ruído, cada um mais fraco.</summary>
        static AudioClip BatScreechClip() => GeneratedClip(BatScreechPath, "morcego-v1", 0f, () =>
        {
            const int rate = AudioRate;
            int n = (int)(0.5f * rate);
            var s = new float[n];
            var rng = new System.Random(909);
            float[] starts = { 0f, 0.12f, 0.27f };
            float[] amps = { 1f, 0.75f, 0.5f };
            for (int k = 0; k < starts.Length; k++)
            {
                int start = (int)(starts[k] * rate), len = (int)(0.07f * rate);
                double phase = 0.0;
                for (int j = 0; j < len && start + j < n; j++)
                {
                    float tt = (float)j / len;
                    float f = Mathf.Lerp(5200f, 3200f, tt);
                    phase += 2.0 * System.Math.PI * f / rate;
                    float env = Mathf.Sin(Mathf.PI * tt);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.15f;
                    s[start + j] += ((float)(System.Math.Sin(phase) + 0.3 * System.Math.Sin(2.0 * phase)) + noise) * env * amps[k];
                }
            }
            return s;
        }, loop: false);
    }
}
