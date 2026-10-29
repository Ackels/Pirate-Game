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
                // O pé do repouso é chato no chão; esticar a canela (joelho dobrado no kit) girava o pé junto, ~45° de ponta para cima, e
                // o humanoide tomava isso como o pé "reto": toda animação levantava a ponta (27/10, "o pé do brutamontes está bugado").
                var foot = B("Foot." + side);
                var footFlat = foot.rotation;
                Align(B("UpperArm." + side), B("LowerArm." + side).position, root.right * sign);
                Align(B("LowerArm." + side), B("Hand." + side).position, root.right * sign);
                Align(B("UpperLeg." + side), B("LowerLeg." + side).position, -root.up);
                Align(B("LowerLeg." + side), foot.position, -root.up);
                foot.rotation = footFlat;
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
            // Duelista (inimiga, pacote B; não entra nos civis): camisa vermelho-escura, colete e calça pretos, cabelo preto.
            (KitCharacters.Anne, 4) => new[] { (3, 9, "8A1E24"), (7, 9, "1E1C22"), (2, 9, "2A2830"), (5, 9, "18141A") },
            (KitCharacters.Henry, 1) => new[] { (2, 11, "2F5DA8"), (6, 11, "6A2A24"), (3, 11, "D8CCB0"), (8, 11, "6A4A2A") },
            (KitCharacters.Henry, 2) => new[] { (2, 11, "3E7A3A"), (9, 11, "D8B840"), (6, 11, "2A3450"), (3, 11, "9A9A9A"), (8, 11, "8A7A50"), (4, 11, "C9A07A") },
            (KitCharacters.Henry, 3) => new[] { (2, 11, "222222"), (9, 11, "A02020"), (6, 11, "B08A30"), (3, 11, "E0DCD0"), (8, 11, "2E4A4A") },
            // Tobias, o coveiro (NPC de missão; não entra nos civis): bandana e listra pretas, mangas cinza, camisa e calça escuras.
            (KitCharacters.Henry, 4) => new[] { (2, 11, "1E1C1C"), (9, 11, "3A3636"), (3, 11, "6E6A64"), (6, 11, "2E2A28"), (8, 11, "3A3028") },
            // NPCs de missão (passo 2; não entram nos civis): Velho Bento (marinheiro desbotado), Almirante Teodoro (azul-marinho e dourado),
            // Zé Caolho (bandana vermelha, colete de couro), Rato Velho (trapos pardos) e Dona Mercês (colete vinho, cabelo grisalho).
            (KitCharacters.Henry, 5) => new[] { (2, 11, "D8D4C8"), (9, 11, "2F4F8A"), (3, 11, "D8D4C8"), (6, 11, "3A5A8A"), (8, 11, "4A4038") },
            (KitCharacters.Henry, 6) => new[] { (2, 11, "1E2A4A"), (9, 11, "C8A040"), (3, 11, "22305A"), (6, 11, "E8E4D8"), (8, 11, "1E2A4A") },
            (KitCharacters.Henry, 7) => new[] { (2, 11, "A02828"), (9, 11, "E8D8B0"), (3, 11, "C8B890"), (6, 11, "6A3A28"), (8, 11, "3A3A3A") },
            (KitCharacters.Henry, 8) => new[] { (2, 11, "5A5048"), (9, 11, "3A3430"), (3, 11, "6A6052"), (6, 11, "4A4238"), (8, 11, "3A3028") },
            (KitCharacters.Anne, 5) => new[] { (3, 9, "E8E2D0"), (7, 9, "6A2430"), (2, 9, "3A3040"), (5, 9, "8A8A88") },
            // Guarda Negra (guarda-costas do Barba-Negra; não entram nos civis): as cores do chefe. Espadachim (Henry): bandana e mangas
            // pretas, listra e camisa vermelho-sangue, calça preta. Mosqueteira (Anne): camisa preta, colete vermelho-sangue, calça preta,
            // cabelo ruivo escuro.
            (KitCharacters.Henry, 9) => new[] { (2, 11, "161418"), (9, 11, "7A1A1E"), (3, 11, "1E1A20"), (6, 11, "6E0E16"), (8, 11, "1A1616") },
            (KitCharacters.Anne, 6) => new[] { (3, 9, "1E1A20"), (7, 9, "6E0E16"), (2, 9, "1A1616"), (5, 9, "5A1E14") },
            // Iara, a pescadora (NPC da missão da lula; não entra nos civis): camisa azul desbotada, colete de couro, calça cinza, cabelo branco.
            (KitCharacters.Anne, 7) => new[] { (3, 9, "7A9CB8"), (7, 9, "6A4A2A"), (2, 9, "5A5A5E"), (5, 9, "D8D4CC") },
            // Megalodonte (tubarão do kit, linha 8: 0 dorso, 1 barriga, 2 boca, 3 olhos, 4 dentes): cinza-ardósia escuro, barriga quase
            // branca, boca vermelho-escura e dentes amarelados (o azul do tubarão do kit parecia de desenho).
            ("Characters_Shark", 1) => new[] { (0, 8, "3A4349"), (1, 8, "CBC8BC"), (2, 8, "3E1012"), (3, 8, "0B0B0D"), (4, 8, "E2D6AE") },
            // Capitão Barba-Negra (chefe, pacote B, passo 4; linha 7 do capitão): 0 pele, 1 chapéu, 2 bota, 3 calça e faixa do chapéu, 4 camisa,
            // 5 barba e cabelo, 6 olhos, 7 casaco, 8 base do gancho, 9 gancho, 10 perna de pau, 11 punhos. Pele pálida, chapéu e casaco
            // pretos, calça e camisa vermelho-sangue, barba negra, olhos vermelhos (brilham: ChiefMaterial), punhos e gancho dourados.
            (KitCharacters.Captain, 1) => new[]
            {
                (0, 7, "C8B4A2"), (1, 7, "141216"), (2, 7, "1A1616"), (3, 7, "6E0E16"), (4, 7, "7A1A1E"), (5, 7, "1C1818"),
                (6, 7, "E01E14"), (7, 7, "1E1A20"), (8, 7, "3A3A40"), (9, 7, "B8902C"), (10, 7, "3A2A20"), (11, 7, "B8902C"),
            },
            // Lázaro Negro (prólogo de "A Coroa de Prata"): casaco roxo quase preto, chapéu preto, camisa clara, barba e cabelo grisalhos,
            // gancho e punhos de prata (a obsessão dele).
            (KitCharacters.Captain, 2) => new[]
            {
                (0, 7, "D2B49A"), (1, 7, "121014"), (2, 7, "1A1616"), (3, 7, "2A2A30"), (4, 7, "B8B0A8"), (5, 7, "8C8C92"),
                (6, 7, "202024"), (7, 7, "2A1830"), (8, 7, "3A3A40"), (9, 7, "C8C8D2"), (10, 7, "3A2A20"), (11, 7, "C8C8D2"),
            },
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
