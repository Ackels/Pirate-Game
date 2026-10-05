using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Personagens do Quaternius Pirate Kit (CC0, Assets/ThirdParty/Quaternius_PirateKit): todos pintados por uma textura
    /// (Atlas_Pirate, uma linha da paleta por personagem), com as animações do próprio kit (rig genérico).
    /// <para>
    /// Jogador e piratas inimigos usam as NOSSAS animações humanoides (Starter Assets + Mixamo). O esqueleto do kit não é
    /// humanoide (pés soltos, controlados por IK, e sem osso de mão), então o gerador o adapta na cópia da cena: prende cada
    /// pé na canela, cria um osso de mão no pulso (os dedos passam a ser filhos dele), monta a pose T e gera o avatar
    /// humanoide (salvo em _Project/Characters). A mão nova e uma referência no quadril ("Quadril_Ref") ficam alinhadas,
    /// na pose T, com a mão e o quadril do boneco do Starter Assets: as medidas das armas (empunhadura, bainha, mira),
    /// feitas no Starter, valem também aqui.
    /// </para>
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string QuaterniusFolder = "Assets/ThirdParty/Quaternius_PirateKit/Models";
        const string CharactersFolder = Root + "/Characters";
        const string HipsReference = "Quadril_Ref";

        static class KitCharacters
        {
            public const string Captain = "Characters_Captain_Barbarossa", Anne = "Characters_Anne", Henry = "Characters_Henry";
            public const string Mako = "Characters_Mako", Sharky = "Characters_Sharky";
            public const string Skeleton = "Characters_Skeleton", HeadlessSkeleton = "Characters_Skeleton_Headless";
        }

        /// <summary>Você é o Capitão Barbarossa (perna de pau, gancho e papagaio).</summary>
        const string PlayerKitModel = KitCharacters.Captain;

        static Texture2D QuaterniusAtlas => AssetDatabase.LoadAssetAtPath<Texture2D>(QuaterniusFolder + "/Textures/Atlas_Pirate.png");
        static Material QuaterniusMaterial => GetTexturedMaterial("M_Quaternius", QuaterniusAtlas, 0.1f);

        static bool HasKitCharacter(string model) => AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{model}.fbx");

        /// <summary>
        /// Cópia do personagem do kit ("Model"), sem ligação de prefab, com o material dado e sem as armas que o kit põe
        /// na mão (as nossas entram no lugar). Rig genérico, com o Animator do FBX.
        /// </summary>
        static GameObject KitCharacter(string model, Transform parent, Material material = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{model}.fbx");
            if (!asset) throw new System.IO.FileNotFoundException($"Personagem do Quaternius não encontrado: {model} (copie do _Downloads).");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Model";
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var mat = material ? material : QuaterniusMaterial;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                renderer.sharedMaterials = slots;
            }
            var weapons = new List<GameObject>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Weapon_")) weapons.Add(t.gameObject);
            foreach (var w in weapons) Object.DestroyImmediate(w);
            return go;
        }

        /// <summary>Personagem do kit com o esqueleto adaptado e o avatar humanoide (para o nosso Animator).</summary>
        static GameObject HumanoidKitCharacter(string model, Transform parent, Material material = null)
        {
            var go = KitCharacter(model, parent, material);
            var avatar = AdaptRigToHumanoid(go, model);
            if (!go.TryGetComponent(out Animator animator)) animator = go.AddComponent<Animator>();
            animator.avatar = avatar;
            return go;
        }

        /// <summary>
        /// Adapta o esqueleto do kit (na cópia) e gera/atualiza o avatar humanoide em _Project/Characters/&lt;modelo&gt;_Humano.asset.
        /// Mapa: Body → quadril (dele saem a coluna e as pernas), Hips/Abdomen/Torso → coluna, peito e peito alto.
        /// </summary>
        static Avatar AdaptRigToHumanoid(GameObject go, string model)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name, t);
            Transform B(string name) => bones.TryGetValue(name, out var t) ? t : throw new System.Exception($"{model}: sem o osso {name}");

            var original = new Dictionary<Transform, Quaternion>();
            foreach (var t in bones.Values) original[t] = t.localRotation;

            // 1) Pés presos na canela e mãos novas no pulso (os dedos passam a ser filhos delas).
            foreach (var side in new[] { "L", "R" })
            {
                B("Foot." + side).SetParent(B("LowerLeg." + side), true);
                var lower = B("LowerArm." + side);
                var fingers = new List<Transform>();
                foreach (Transform child in lower) fingers.Add(child);
                var wrist = Vector3.zero;
                foreach (var f in fingers) wrist += f.position;
                wrist /= Mathf.Max(1, fingers.Count);
                var hand = new GameObject("Hand." + side).transform;
                hand.SetParent(lower, false);
                hand.position = Vector3.Lerp(lower.position, wrist, 0.85f);
                foreach (var f in fingers) f.SetParent(hand, true);
                bones["Hand." + side] = hand;
            }
            var hipsRef = new GameObject(HipsReference).transform;
            hipsRef.SetParent(B("Body"), false);
            bones[HipsReference] = hipsRef;

            // 2) Pose T: braços na horizontal para os lados, pernas retas para baixo (o modelo olha para +Z).
            var root = go.transform;
            var body = B("Body");
            var bodyRest = body.localPosition;
            float footRest = B("Foot.L").position.y;
            void Align(Transform bone, Vector3 towards, Vector3 direction) =>
                bone.rotation = Quaternion.FromToRotation((towards - bone.position).normalized, direction) * bone.rotation;
            foreach (var (side, sign) in new[] { ("L", -1f), ("R", 1f) })
            {
                Align(B("UpperArm." + side), B("LowerArm." + side).position, root.right * sign);
                Align(B("LowerArm." + side), B("Hand." + side).position, root.right * sign);
                Align(B("UpperLeg." + side), B("LowerLeg." + side).position, -root.up);
                Align(B("LowerLeg." + side), B("Foot." + side).position, -root.up);
            }
            // O repouso do kit é de joelho dobrado: com as pernas esticadas o pé desceria abaixo do chão. O quadril sobe até
            // o pé voltar ao chão (sem isso o Unity calcula a escala do corpo pela perna dobrada e ele anda agachado).
            body.position += Vector3.up * (footRest - B("Foot.L").position.y);
            StarterTPose(out var starterLeftHand, out var starterRightHand, out var starterHips);
            B("Hand.L").rotation = root.rotation * starterLeftHand;
            B("Hand.R").rotation = root.rotation * starterRightHand;
            hipsRef.rotation = root.rotation * starterHips;

            var skeleton = new List<SkeletonBone>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            var map = new (string human, string bone)[]
            {
                ("Hips", "Body"), ("Spine", "Hips"), ("Chest", "Abdomen"), ("UpperChest", "Torso"), ("Neck", "Neck"), ("Head", "Head"),
                ("LeftShoulder", "Shoulder.L"), ("LeftUpperArm", "UpperArm.L"), ("LeftLowerArm", "LowerArm.L"), ("LeftHand", "Hand.L"),
                ("RightShoulder", "Shoulder.R"), ("RightUpperArm", "UpperArm.R"), ("RightLowerArm", "LowerArm.R"), ("RightHand", "Hand.R"),
                ("LeftUpperLeg", "UpperLeg.L"), ("LeftLowerLeg", "LowerLeg.L"), ("LeftFoot", "Foot.L"),
                ("RightUpperLeg", "UpperLeg.R"), ("RightLowerLeg", "LowerLeg.R"), ("RightFoot", "Foot.R"),
            };
            var human = new List<HumanBone>();
            foreach (var (h, b) in map)
            {
                var bone = new HumanBone { humanName = h, boneName = b };
                bone.limit.useDefaultValues = true;
                human.Add(bone);
            }
            var description = new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var built = AvatarBuilder.BuildHumanAvatar(go, description);
            built.name = model + "_Humano";

            // 3) Volta à pose de repouso do kit (os ossos novos guardam a rotação relativa da pose T).
            foreach (var kv in original) if (kv.Key) kv.Key.localRotation = kv.Value;
            body.localPosition = bodyRest;
            if (!built.isValid || !built.isHuman) throw new System.Exception($"{model}: avatar humanoide inválido");
            return SaveAvatar(built);
        }

        /// <summary>Grava o avatar em _Project/Characters, atualizando o que já existe (mesmo GUID nas outras cenas).</summary>
        static Avatar SaveAvatar(Avatar built)
        {
            EnsureFolder(Root, "Characters");
            string path = $"{CharactersFolder}/{built.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, existing);
            existing.name = built.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        // ---------- Civis (rig genérico, animações do próprio kit) ----------

        const string CivilAnimatorPath = AnimationFolder + "/Civil.controller";

        /// <summary>Visuais dos civis: Anne e Henry, cada um no original (0) e em 3 versões com outras cores (1–3).</summary>
        static readonly (string model, int palette)[] CivilianLooks =
        {
            (KitCharacters.Anne, 0), (KitCharacters.Henry, 0), (KitCharacters.Anne, 1), (KitCharacters.Henry, 1),
            (KitCharacters.Anne, 2), (KitCharacters.Henry, 2), (KitCharacters.Anne, 3), (KitCharacters.Henry, 3),
        };

        /// <summary>
        /// Cores trocadas na textura do kit: cada personagem usa uma linha só dele no atlas (quadradinhos de 32 px; coluna,
        /// linha a partir do topo). Anne (linha 9): 2 calça, 3 camisa, 4 pele, 5 cabelo, 7 colete. Henry (linha 11): 2
        /// bandana, 3 mangas, 4 pele, 6 camisa, 8 calça, 9 listra da bandana.
        /// </summary>
        static (int x, int y, string hex)[] CivilianPalette(string model, int palette) => (model, palette) switch
        {
            (KitCharacters.Anne, 1) => new[] { (3, 9, "E8E2D0"), (7, 9, "2F5A3A"), (2, 9, "2E3A5C"), (5, 9, "4A3020") },
            (KitCharacters.Anne, 2) => new[] { (3, 9, "9DB8D8"), (7, 9, "6A2430"), (2, 9, "A08458"), (5, 9, "1E1A18"), (4, 9, "7A5235") },
            (KitCharacters.Anne, 3) => new[] { (3, 9, "D9C9A0"), (7, 9, "4A3060"), (2, 9, "3A3A40"), (5, 9, "A0452A") },
            (KitCharacters.Henry, 1) => new[] { (2, 11, "2F5DA8"), (6, 11, "6A2A24"), (3, 11, "D8CCB0"), (8, 11, "6A4A2A") },
            (KitCharacters.Henry, 2) => new[] { (2, 11, "3E7A3A"), (9, 11, "D8B840"), (6, 11, "2A3450"), (3, 11, "9A9A9A"), (8, 11, "8A7A50"), (4, 11, "C9A07A") },
            (KitCharacters.Henry, 3) => new[] { (2, 11, "222222"), (9, 11, "A02020"), (6, 11, "B08A30"), (3, 11, "E0DCD0"), (8, 11, "2E4A4A") },
            _ => null,
        };

        static readonly Dictionary<string, Material> civilianMaterials = new();

        /// <summary>Material com a textura do kit recolorida (gerada em _Project/Textures/Civis, refeita a cada geração).</summary>
        static Material CivilianMaterial(string model, int palette)
        {
            var colors = CivilianPalette(model, palette);
            if (colors == null) return QuaterniusMaterial;
            string name = $"Atlas_{model.Replace("Characters_", "")}_{palette}";
            if (civilianMaterials.TryGetValue(name, out var cached) && cached) return cached;

            EnsureFolder(Root, "Textures");
            EnsureFolder(Root + "/Textures", "Civis");
            string path = $"{Root}/Textures/Civis/{name}.png";
            var tex = new Texture2D(2, 2);
            tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(QuaterniusAtlas)));
            int cell = tex.width / 32;
            foreach (var (x, y, hex) in colors)
            {
                ColorUtility.TryParseHtmlString("#" + hex, out var color);
                for (int i = 0; i < cell; i++)
                for (int j = 0; j < cell; j++)
                    tex.SetPixel(x * cell + i, tex.height - 1 - (y * cell + j), color);
            }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            // Igual à paleta original: sem filtro, sem mipmap, sem compressão (cada cor é um quadradinho).
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var material = GetTexturedMaterial("M_Quaternius_" + name, AssetDatabase.LoadAssetAtPath<Texture2D>(path), 0.1f);
            civilianMaterials[name] = material;
            return material;
        }

        static AnimationClip KitClip(string model, string clip)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{QuaterniusFolder}/{model}.fbx"))
                if (o is AnimationClip c && c.name.EndsWith("|" + clip)) return c;
            return null;
        }

        /// <summary>
        /// Animator dos civis: locomoção (parado ↔ andar pela "Speed") e os gestos "Wave", "Yes", "No" (voltam sozinhos).
        /// O controller usa os clipes do Henry; os outros personagens ganham um override com os clipes deles (o rig é o mesmo,
        /// mas as medidas dos ossos não). Criados uma vez.
        /// </summary>
        static RuntimeAnimatorController CivilAnimator(string model)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CivilAnimatorPath);
            if (!controller)
            {
                EnsureFolder(Root, "Animation");
                controller = AnimatorController.CreateAnimatorControllerAtPath(CivilAnimatorPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                var machine = controller.layers[0].stateMachine;
                var locomotion = controller.CreateBlendTreeInController("Locomocao", out var tree);
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                tree.AddChild(KitClip(KitCharacters.Henry, "Idle"), 0f);
                tree.AddChild(KitClip(KitCharacters.Henry, "Walk"), 1.3f);
                machine.defaultState = locomotion;
                foreach (var gesture in new[] { "Wave", "Yes", "No" })
                {
                    var state = machine.AddState(gesture);
                    state.motion = KitClip(KitCharacters.Henry, gesture);
                    var back = state.AddTransition(locomotion);
                    back.hasExitTime = true;
                    back.exitTime = 0.9f;
                    back.duration = 0.25f;
                }
                AssetDatabase.SaveAssets();
            }
            if (model == KitCharacters.Henry) return controller;

            string path = $"{AnimationFolder}/Civil_{model.Replace("Characters_", "")}.overrideController";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (existing) return existing;
            var overrides = new AnimatorOverrideController(controller);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var clip in overrides.animationClips)
                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, KitClip(model, clip.name.Substring(clip.name.LastIndexOf('|') + 1))));
            overrides.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(overrides, path);
            return overrides;
        }

        /// <summary>Rotações (no mundo, com a raiz parada) da mão esquerda, da direita e do quadril do Starter na pose T do avatar dele.</summary>
        static void StarterTPose(out Quaternion leftHand, out Quaternion rightHand, out Quaternion hips)
        {
            leftHand = rightHand = hips = Quaternion.identity;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StarterModel);
            if (!asset) return;
            var temp = Object.Instantiate(asset);
            temp.hideFlags = HideFlags.DontSave;
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = temp.GetComponent<Animator>();
            var byName = new Dictionary<string, Transform>();
            foreach (var t in temp.GetComponentsInChildren<Transform>(true)) byName.TryAdd(t.name, t);
            foreach (var bone in animator.avatar.humanDescription.skeleton)
            {
                if (!byName.TryGetValue(bone.name, out var t) || t == temp.transform) continue;
                t.SetLocalPositionAndRotation(bone.position, bone.rotation);
            }
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).rotation;
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand).rotation;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips).rotation;
            Object.DestroyImmediate(temp);
        }
    }
}
