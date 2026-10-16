using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Chefe pirata a pé (pacote B, passo 4, 10/10): o Capitão Barba-Negra, rival do Barbarossa (o mesmo modelo de capitão do kit, 40%
    /// maior, casaco preto, chapéu e calça vermelho-sangue, barba negra, olhos vermelhos que brilham, punhos e gancho dourados, sem
    /// papagaio) com o espadão-cutelo do Quaternius (Weapon_Sword_2) e o pacote do espadão do Mixamo (WeaponType 4). Golpes, fases e
    /// escolha em <see cref="LandBoss"/>; posto com nome "Chefe_..." nas listas (hoje nas ruínas da Caveira).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Vida 900 → 760 e golpes ~15% mais fracos (16/10, pedido do Jhonata: "ainda difícil, mas sem ter que ser tudo perfeito";
        // com a Guarda Negra antes, só quem aparava quase tudo vencia).
        const float ChiefScale = 1.4f, ChiefHealth = 760f;
        const int ChiefPalette = 1;
        const string ChiefName = "Capitão Barba-Negra", ChiefTitle = "o Carniceiro das Ruínas", ChiefBounty = "chefe_barbanegra";
        const string GreatswordFolder = "Espadao";
        static readonly Color ChiefEyes = new(1f, 0.12f, 0.05f);

        /// <summary>
        /// Golpes do espadão (AttackVariant 0–7). Impacto medido no clipe (mão direita mais rápida; chute = pé direito); cada golpe começa
        /// "lead" s antes do impacto (AttackSpeed 1). "Andando" = corpo inteiro mesmo se movendo (giro, salto, chute, sequência).
        /// </summary>
        static readonly (string clip, float impact, float lead, bool moving)[] GreatswordAttacks =
        {
            ("Slash", 0.57f, 0.50f, false),             // 0 corte
            ("Slash 3", 0.80f, 0.65f, false),           // 1 corte largo
            ("Slash 4", 0.63f, 0.55f, false),           // 2 corte duplo (+0,80 s)
            ("Attack", 0.47f, 0.47f, false),            // 3 golpe de cima (derruba)
            ("High Spin Attack", 1.03f, 0.90f, true),   // 4 giro (imparável, em volta)
            ("Jump Attack", 1.10f, 1.00f, true),        // 5 salto (imparável, de longe)
            ("Kick", 0.55f, 0.50f, true),               // 6 chute (imparável, quebra a guarda)
            ("Slash 2", 0.73f, 0.65f, true),            // 7 sequência de três avançando (fase 2; +0,90 e +1,84 s)
        };

        static StrikeOverride[] GreatswordStrikes() => new[]
        {
            new StrikeOverride { damage = 24f },
            new StrikeOverride { damage = 28f, angle = 170f },
            new StrikeOverride { damage = 18f, followUps = new[] { 0.80f } },
            new StrikeOverride { damage = 32f, angle = 70f, range = 3.3f, customKnockdown = true, knockdown = Knockdown.Stumble },
            new StrikeOverride { damage = 26f, angle = 360f, range = 3.4f, customKnockdown = true, knockdown = Knockdown.Stumble, unblockable = true },
            new StrikeOverride { damage = 36f, angle = 360f, range = 3.2f, customKnockdown = true, knockdown = Knockdown.Blast, unblockable = true, lungeSpeed = 11f },
            new StrikeOverride { damage = 10f, angle = 100f, range = 2.5f, customKnockdown = true, knockdown = Knockdown.Stumble, unblockable = true },
            new StrikeOverride { damage = 17f, followUps = new[] { 0.90f, 1.84f }, lungeSpeed = 3.5f },
        };

        /// <summary>Quando cada golpe sai (LandBoss.Move): distância, peso, fase, espera, brecha depois.</summary>
        static LandBoss.Move[] ChiefMoves() => new[]
        {
            new LandBoss.Move { name = "Corte", variant = 0, maxRange = 4.2f, weight = 3f, recovery = 0.7f, combo = true },
            new LandBoss.Move { name = "Corte largo", variant = 1, maxRange = 4.2f, weight = 2.5f, recovery = 0.8f, combo = true },
            new LandBoss.Move { name = "Corte duplo", variant = 2, maxRange = 4f, weight = 2f, recovery = 0.9f },
            new LandBoss.Move { name = "Golpe de cima", variant = 3, maxRange = 3.6f, weight = 1.5f, cooldown = 4f, recovery = 1.1f },
            new LandBoss.Move { name = "Giro", variant = 4, maxRange = 3.2f, cooldown = 6f, recovery = 1f, breaker = true },
            new LandBoss.Move { name = "Salto", variant = 5, minRange = 5.5f, maxRange = 11f, weight = 1f, cooldown = 7f, recovery = 1.2f },
            new LandBoss.Move { name = "Chute", variant = 6, maxRange = 2.6f, cooldown = 4f, recovery = 0.6f, guardBreaker = true },
            new LandBoss.Move { name = "Sequência", variant = 7, maxRange = 4.5f, weight = 2.5f, phase = 2, cooldown = 6f, recovery = 1f },
        };

        static bool IsChief(string postName) => postName.StartsWith("Chefe");

        /// <summary>Espadão do chefe (Data/Weapons/Weapon_Espadao_Chefe; preservado se existe).</summary>
        static WeaponDefinition ChiefSword() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Espadao_Chefe.asset", w =>
        {
            w.displayName = "Espadão";
            w.kind = WeaponKind.Melee;
            w.animatorWeaponType = 4;
            w.damage = 24f;
            w.range = 3f;
            w.angle = 150f;
            w.attackAnimSpeed = 1f;
            w.hitDelay = GreatswordAttacks[0].lead;
            w.variantHitDelays = System.Array.ConvertAll(GreatswordAttacks, a => a.lead);
            w.variantStrikes = GreatswordStrikes();
            w.randomVariants = false;
            w.attackVariants = GreatswordAttacks.Length;
            w.cooldown = 1f;
            w.comboWindow = 2f;
            w.animatedDraw = false;
            w.moveSpeedWhileAttacking = 0.15f;
            w.guardCost = 3f;
        });

        /// <summary>
        /// Espadão na mão direita. A lâmina e o fio vêm dos clipes do espadão, no espaço do osso da mão (GreatswordGrip); o modelo do kit
        /// (lâmina em +Y, largura em X, cabo de y −0,21 a 0) pega no meio do cabo. × 0,85 (× 1,4 do chefe ≈ 1,55 m).
        /// </summary>
        static Transform GreatswordOn(Transform model, Material iron, Material wood, out Transform handSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            GreatswordGrip(model, out Vector3 blade, out Vector3 edge);
            var root = new GameObject("Espadao").transform;
            root.SetParent(hand, false);
            root.SetLocalPositionAndRotation(LocalOffset(hand, blade, 0.04f), Quaternion.LookRotation(blade, edge));
            if (!KitWeapon(root, "Weapon_Sword_2", Quaternion.LookRotation(Vector3.right, Vector3.forward), 0.85f, new Vector3(0f, -0.1f, 0f)))
            {
                Part(PrimitiveType.Cylinder, "Cabo", root, Vector3.zero, new Vector3(0.04f, 0.12f, 0.04f), Quaternion.Euler(90f, 0f, 0f), wood);
                Part(PrimitiveType.Cube, "Lamina", root, new Vector3(0f, 0f, 0.6f), new Vector3(0.02f, 0.2f, 0.95f), Quaternion.identity, iron);
            }
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_Espadao");
            return root;
        }

        /// <summary>
        /// Lâmina e fio do espadão no espaço do osso da mão direita: a direção que melhor sai para fora do peito no impacto dos golpes e
        /// fica de ponta para cima e à frente parado/andando/correndo (procura numa esfera de direções; a pior pose pesa 2×), e o fio = a
        /// direção do movimento da mão no impacto, perpendicular à lâmina. As duas mãos não ficam juntas no cabo nesse esqueleto.
        /// </summary>
        static void GreatswordGrip(Transform model, out Vector3 blade, out Vector3 edge)
        {
            blade = new Vector3(-0.72f, -0.67f, -0.18f).normalized;
            edge = new Vector3(0.27f, -0.5f, 0.82f).normalized;
            var temp = MeasureCopy(model);
            if (!temp) return;
            var anim = temp.GetComponent<Animator>();
            var samples = new (string clip, float t, bool impact)[]
            {
                ("Idle", 0.5f, false), ("Idle 2", 1f, false), ("Walk", 0.4f, false), ("Run 2", 0.3f, false),
                ("Slash", 0.57f, true), ("Slash 3", 0.8f, true), ("Slash 4", 0.63f, true), ("Slash 4", 1.43f, true), ("Attack", 0.47f, true),
                ("High Spin Attack", 1.03f, true), ("Jump Attack", 1.1f, true), ("Slash 2", 0.73f, true), ("Slash 2", 2.57f, true),
            };
            var rots = new List<Quaternion>();
            var wants = new List<Vector3>();
            var moves = new List<Vector3>();
            foreach (var (name, t, impact) in samples)
            {
                var clip = GreatswordClip(name);
                if (!clip) continue;
                clip.SampleAnimation(temp, t - 0.03f);
                var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                Vector3 before = hand.position;
                clip.SampleAnimation(temp, t);
                var chest = anim.GetBoneTransform(HumanBodyBones.Chest).position;
                rots.Add(hand.rotation);
                moves.Add(impact ? hand.position - before : Vector3.zero);
                wants.Add(impact ? (hand.position - chest).normalized : (Vector3.up * 0.75f + Vector3.forward * 0.65f).normalized);
            }
            Object.DestroyImmediate(temp);
            if (rots.Count < 4) return;

            float best = float.NegativeInfinity;
            const int n = 4000;
            for (int i = 0; i < n; i++)
            {
                float z = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(1f - z * z), phi = i * 2.39996f;
                var v = new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);
                float score = 0f, worst = 9f;
                for (int s = 0; s < rots.Count; s++)
                {
                    float d = Vector3.Dot(rots[s] * v, wants[s]);
                    score += d;
                    worst = Mathf.Min(worst, d);
                }
                score += worst * 2f;
                if (score <= best) continue;
                best = score;
                blade = v;
            }
            var sum = Vector3.zero;
            for (int s = 0; s < rots.Count; s++)
                if (moves[s] != Vector3.zero)
                    sum += Quaternion.Inverse(rots[s]) * Vector3.ProjectOnPlane(moves[s], rots[s] * blade).normalized;
            var e = Vector3.ProjectOnPlane(sum, blade);
            if (e.sqrMagnitude > 0.0001f) edge = e.normalized;
        }

        /// <summary>Clipe do pacote do espadão ("Espadao@nome"): os nomes curtos ("Idle", "Run") repetem nos outros pacotes.</summary>
        static AnimationClip GreatswordClip(string name)
        {
            string folder = MixamoImportSettings.Folder + "/" + GreatswordFolder;
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                string tail = file.Contains('@') ? file[(file.LastIndexOf('@') + 1)..] : file;
                if (!string.Equals(tail.Trim(), name, System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }

        static AnimatorState GreatswordState(AnimatorStateMachine sm, string name, string clip, float speed, Vector3 position)
        {
            var state = sm.AddState(name, position);
            state.motion = GreatswordClip(clip);
            state.speed = speed;
            return state;
        }

        /// <summary>
        /// Espadão do chefe (WeaponType 4): "Espadao_Pronto" (Idle; corpo todo parado, só o tronco andando), "Espadao_Correndo" (Run 2),
        /// os 8 golpes pelo AttackVariant (começam "lead" antes do impacto e voltam ao pronto ~0,6 s depois do último acerto) e
        /// "Espadao_Furia" (Power Up, tocado direto pelo CharacterAnimatorDriver.PlayPowerUp, no tempo do LandBoss).
        /// </summary>
        static void AddGreatsword(AnimatorStateMachine sm, AnimatorState empty)
        {
            if (!GreatswordClip("Idle")) return;
            var ready = GreatswordState(sm, "Espadao_Pronto", "Idle", 1f, new Vector3(-1500f, 60f));
            ready.tag = FullBodyTag;
            Go(empty, ready, 0.25f, ("WeaponType", AnimatorConditionMode.Equals, 4));
            Go(ready, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 4));
            if (GreatswordClip("Run 2"))
            {
                var run = GreatswordState(sm, "Espadao_Correndo", "Run 2", 1f, new Vector3(-1500f, 140f));
                Go(ready, run, 0.25f, ("Speed", AnimatorConditionMode.Greater, 3.2f));
                Go(run, ready, 0.25f, ("Speed", AnimatorConditionMode.Less, 2.8f));
                Go(run, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 4));
            }
            var strikes = GreatswordStrikes();
            for (int v = 0; v < GreatswordAttacks.Length; v++)
            {
                var (clipName, impact, lead, moving) = GreatswordAttacks[v];
                var clip = GreatswordClip(clipName);
                if (!clip) continue;
                var swing = GreatswordState(sm, $"Espadao_Golpe{v}", clipName, 1f, new Vector3(-1800f, 60f + 80f * v));
                swing.tag = moving ? MovingFullBodyTag : FullBodyTag;
                swing.speedParameterActive = true;
                swing.speedParameter = "AttackSpeed";
                Any(sm, swing, Mathf.Max(0f, impact - lead) / clip.length, 0.12f,
                    ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 4), ("AttackVariant", AnimatorConditionMode.Equals, v));
                float last = impact;
                foreach (var t in strikes[v].followUps) last = Mathf.Max(last, impact + t);
                Exit(swing, ready, Mathf.Min(0.95f, (last + 0.6f) / clip.length), 0.3f);
            }
            var powerClip = GreatswordClip("Power Up");
            if (powerClip)
            {
                var fury = GreatswordState(sm, "Espadao_Furia", "Power Up", powerClip.length / ChiefPowerUpSeconds, new Vector3(-1800f, 760f));
                fury.tag = MovingFullBodyTag;
                Exit(fury, ready, 0.95f, 0.25f);
            }
        }

        /// <summary>Igual ao LandBoss.powerUpSeconds (a animação dura o mesmo que a fúria).</summary>
        const float ChiefPowerUpSeconds = 2.6f;

        /// <summary>
        /// Roupa do chefe (linha 7 do atlas, a do capitão; CivilianPalette) com os olhos brilhando: máscara de emissão de 32 × 32 (um pixel por
        /// quadradinho do atlas) acesa só no quadradinho dos olhos. O LandBoss muda a força do brilho numa cópia do material.
        /// </summary>
        static Material ChiefMaterial()
        {
            var mat = CivilianMaterial(KitCharacters.Captain, ChiefPalette);
            string path = $"{Root}/Textures/Civis/Chefe_Olhos.png";
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(path))
            {
                var mask = new Texture2D(32, 32, TextureFormat.RGB24, false);
                var pixels = new Color[32 * 32];
                pixels[(31 - 7) * 32 + 6] = Color.white;
                mask.SetPixels(pixels);
                System.IO.File.WriteAllBytes(path, mask.EncodeToPNG());
                Object.DestroyImmediate(mask);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetColor("_EmissionColor", ChiefEyes * 1.2f);
            Emissive(mat);
            return mat;
        }

        /// <summary>Espadão em brasa (aviso do golpe imparável e fase 2): o atlas do kit com emissão vermelha.</summary>
        static Material ChiefBladeGlow()
        {
            var mat = GetTexturedMaterial("M_Espadao_Furia", QuaterniusAtlas, 0.3f);
            mat.SetColor("_EmissionColor", new Color(1f, 0.16f, 0.04f) * 2.2f);
            Emissive(mat);
            return mat;
        }

        /// <summary>Brasas vermelhas subindo em volta dos pés (fase 2; rajada na onda de choque e no aviso do golpe imparável).</summary>
        static ParticleSystem ChiefAura(Transform parent)
        {
            var go = new GameObject("Aura");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.25f, 0.05f, 0.9f), new Color(0.7f, 0.05f, 0.02f, 0.8f));
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            var emission = ps.emission;
            emission.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.7f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxMaterial("M_FX_Brilho", "circle_05", additive: true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Transforma o pirata recém-montado no chefe: tamanho, vida, velocidade, reações, IA, roupa, olhos, aura e o LandBoss.</summary>
        static void MakeChief(GameObject go, Transform model, EnemyFootAI ai)
        {
            go.transform.localScale = Vector3.one * ChiefScale;
            go.GetComponent<Health>().Configure(ChiefHealth);
            // Anda devagar e corre abaixo do capitão (6): dá para fugir, e o salto pega quem foge para perto.
            go.GetComponent<FootCharacter>().ConfigureSpeeds(2f, 4.6f);
            var reactions = go.GetComponent<FootHealthReactions>();
            reactions.Configure(model, 0f);
            reactions.SetHeavy(true);
            reactions.SetUnstoppable(true);
            ai.ConfigureMelee(2.7f, 4f, new Vector2(0.5f, 1.1f), 0.45f);
            ai.ConfigureLunge(11f, 5f, 2);
            ai.ConfigureTaunt(2.4f);
            ai.ConfigureBounty(ChiefBounty);

            var body = new List<Renderer>();
            if (model)
            {
                // Sem o papagaio do Barbarossa (no ombro e a malha original do kit).
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t && (t.name == "Papagaio" || t.name == "Ernest")) Object.DestroyImmediate(t.gameObject);
                var outfit = ChiefMaterial();
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = outfit;
                    r.sharedMaterials = slots;
                    body.Add(r);
                }
            }
            var blade = new List<Renderer>();
            var sword = go.GetComponentInChildren<CharacterCombat>().WeaponVisual(0);
            if (sword) blade.AddRange(sword.GetComponentsInChildren<MeshRenderer>(true));
            var boss = go.AddComponent<LandBoss>();
            boss.Configure(ChiefName, ChiefTitle, ChiefMoves(), new[] { 0, 1 }, body.ToArray(), blade.ToArray(), ChiefBladeGlow(), ChiefAura(go.transform));
            // A Guarda Negra luta primeiro (passo 2): ele entra quando ela cai, quando o capitão chega a 3,5 m ou quando leva um golpe
            // (com 5 m, quem perseguia a mosqueteira passava perto e o acordava; revisão de 16/10).
            boss.SetWaitForGuards(true, 3.5f);
        }
    }
}
