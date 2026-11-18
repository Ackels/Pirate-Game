using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Mago esqueleto (18/11, pedido do Jhonata: o esqueleto de chapéu de mago "em vez dele usar arma... usar magia, segurar um cajado...
    /// fogo"). O KayKit Skeleton_Mage (Ilha Amaldiçoada e Tormenta) deixa o bacamarte e leva o cajado do mesmo pacote (Skeleton_Staff,
    /// CC0) em pé no punho; mira com o braço esticado (a pose de mira da pistola) e solta uma bola de fogo lenta (Combat.Fireball) que
    /// estoura pequeno: dá para sair da frente ou rolar, a guarda não segura (caveira azul). O aviso é o mesmo dos atiradores: brasa e
    /// faíscas no alto do cajado e o medalhão sobre a cabeça.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string StaffModel = KayKitFolder + "/Skeleton_Staff.fbx";
        // Medidas do Skeleton_Staff.fbx (raiz na pegada): de −0,846 a 1,255 em Y; a cabeça (cajado de caveira) fica em cima.
        const float StaffBottom = -0.846f, StaffTop = 1.255f, StaffLength = 1.75f;

        static bool IsSkeletonMage(string kitModel) => kitModel == KayKitCharacters.Mage;

        /// <summary>Cajado do mago: magia de fogo pelo <see cref="WeaponDefinition.projectile"/>. Refeito a cada geração.</summary>
        static WeaponDefinition MageStaffWeapon()
        {
            var w = LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Cajado_Mago.asset", _ => { });
            w.displayName = "Cajado de fogo";
            w.kind = WeaponKind.Firearm;
            w.animatorWeaponType = 1;
            w.twoHanded = false;
            w.supportOffset = 0f;
            // Dano no centro do estouro (borda 6); o capitão tem 100. Uma bola a cada ~3 s com a mira.
            w.damage = 20f;
            w.range = 18f;
            w.angle = 2f;
            w.hitDelay = 0.37f / ShootSpeed;
            w.cooldown = 2.4f;
            // Sem recarga (a de pistola soca a carga com a vareta): o ritmo vem do cooldown e da mira da IA.
            w.shotsPerReload = 999;
            w.reloadTime = 0f;
            w.aimPoseSeconds = 1.5f;
            w.moveSpeedWhileAttacking = 0.3f;
            w.aimedShotDelay = 0.04f;
            w.aimedSpread = 0.3f;
            w.aimMoveSpeed = 0.6f;
            w.recoilDegrees = 6f;
            w.projectile = FireballPrefab();
            w.projectileSpeed = 11f;
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>Bola de fogo: esfera em brasa, luz laranja curta (sem sombra) e chamas que ficam para trás. Refeita a cada geração.</summary>
        static GameObject FireballPrefab()
        {
            EnsureFolder(Root, "Prefabs");
            string path = PrefabsFolder + "/Magia_BolaDeFogo.prefab";
            var root = new GameObject("Magia_BolaDeFogo");
            root.AddComponent<Fireball>();

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Brasa";
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.38f;
            var mat = GetMaterial("M_BolaDeFogo", new Color(1f, 0.55f, 0.15f), 0.2f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 3f);
                Emissive(mat);
            }
            var r = core.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 5f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;

            FireballFlames(root.transform);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        static void FireballFlames(Transform parent)
        {
            var go = new GameObject("Chamas");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f, 1f), new Color(1f, 0.4f, 0.1f, 1f));
            main.gravityModifier = -0.15f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            emission.rateOverDistance = 6f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxMaterial("M_FX_Fogo", "flame_05", additive: false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Cajado no punho direito, em pé (o pé do cajado para baixo, a caveira para cima, um pouco para a frente), nos eixos da pistola
        /// (+X = antebraço, +Y = polegar). A "Boca" fica na caveira do alto e aponta como o cano da pistola: na mira, o braço estica para o
        /// alvo e o cajado continua em pé.
        /// </summary>
        static Transform StaffOn(Transform model, out Transform muzzle, out Transform handSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            HandGrip(model, PistolAimClip(), out Vector3 arm, out Vector3 thumb);
            var root = new GameObject("Cajado").transform;
            root.SetParent(hand, false);
            root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.05f), Quaternion.LookRotation(Vector3.Cross(arm, thumb), thumb));
            float scale = StaffLength / (StaffTop - StaffBottom);
            // Em pé com 12° para a frente (para o lado do antebraço), raiz do FBX na pegada.
            var tilt = Quaternion.Euler(0f, 0f, -12f);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StaffModel);
            if (asset)
            {
                var pivot = new GameObject("Modelo").transform;
                pivot.SetParent(root, false);
                pivot.SetLocalPositionAndRotation(Vector3.zero, tilt);
                pivot.localScale = Vector3.one * scale;
                pivot.gameObject.AddComponent<UprightStaff>();
                var go = (GameObject)Object.Instantiate(asset, pivot, false);
                go.name = "Skeleton_Staff";
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = KayKitMaterial;
                    r.sharedMaterials = slots;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
            }
            muzzle = new GameObject("Boca").transform;
            muzzle.SetParent(root, false);
            muzzle.SetLocalPositionAndRotation(tilt * (Vector3.up * (StaffTop - 0.25f) * scale), Quaternion.LookRotation(Vector3.right, Vector3.up));
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_Cajado");
            return root;
        }

        /// <summary>Monta o mago no personagem (arma, cajado, mira, aviso no alto do cajado) e o jeito da IA. Usado pelo CreatePirate.</summary>
        static void SetupSkeletonMage(GameObject go, Transform model, CharacterCombat combat)
        {
            var weapon = MageStaffWeapon();
            var staff = StaffOn(model, out Transform muzzle, out Transform hand);
            combat.Configure(new[] { weapon }, new[] { staff }, new[] { hand }, new[] { hand }, muzzle, null);
            combat.SetMuzzles(new[] { muzzle });
            combat.SetAimSnap(0f);
            var rig = AddAimRig(model, combat, muzzle);
            if (rig)
            {
                // As faíscas e a brasa na caveira do cajado (no modelo, que fica sempre em pé).
                var top = staff.Find("Modelo");
                var at = new GameObject("Cacoleta").transform;
                if (top)
                {
                    at.SetParent(top, false);
                    at.localPosition = Vector3.up * (StaffTop - 0.2f);
                    at.localRotation = Quaternion.LookRotation(Vector3.up);
                    at.localScale = Vector3.one / Mathf.Max(0.0001f, top.lossyScale.x);
                }
                else
                {
                    at.SetParent(muzzle, false);
                    at.localRotation = Quaternion.LookRotation(Vector3.up);
                }
                rig.ConfigureFuse(FuseParticles(at, "Faiscas", FuseKind.Sparks), FuseParticles(at, "Fumacinha", FuseKind.Smoke),
                    FuseParticles(at, "Brasa", FuseKind.Glow));
            }
        }

        /// <summary>Mago a distância: fica a ~9 m, começa a mirar a 14 m, recua com o capitão a 4 m, mira 1,1–1,5 s.</summary>
        static void ConfigureMageAI(EnemyFootAI ai) => ai.ConfigureRanged(9f, 4f, 14f, new Vector2(1.1f, 1.5f), 2.5f);

        /// <summary>
        /// Troca, na cena aberta e sem regerar, o bacamarte dos esqueletos de chapéu de mago (KayKit Skeleton_Mage) pelo cajado de fogo.
        /// Devolve quantos trocou. Salve a cena depois.
        /// </summary>
        public static int PatchSkeletonMagesInOpenScene()
        {
            int count = 0;
            foreach (var ai in Object.FindObjectsByType<EnemyFootAI>(FindObjectsInactive.Include))
            {
                var model = ai.transform.Find("Model");
                if (!model || !model.TryGetComponent(out Animator animator) || !animator.avatar || !animator.avatar.name.StartsWith(KayKitCharacters.Mage)) continue;
                var combat = ai.GetComponent<CharacterCombat>();
                if (!combat) continue;
                // O bacamarte, o lugar dele na mão e nas costas, e a mira de duas mãos saem.
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t && (t.name == "Mao_Bacamarte" || t.name == "Costas_Bacamarte" || t.name == "Mao_Cajado" || t.name == "Bacamarte"))
                        Object.DestroyImmediate(t.gameObject);
                foreach (var rig in model.GetComponents<CharacterAimRig>()) Object.DestroyImmediate(rig);
                var tracer = ai.transform.Find("RastroDoTiro");
                if (tracer) Object.DestroyImmediate(tracer.gameObject);
                SetupSkeletonMage(ai.gameObject, model, combat);
                ConfigureMageAI(ai);
                EditorUtility.SetDirty(ai.gameObject);
                count++;
            }
            if (count > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return count;
        }
    }
}
