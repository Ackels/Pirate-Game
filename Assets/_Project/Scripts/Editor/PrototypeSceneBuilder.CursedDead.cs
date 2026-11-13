using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Os mortos da Ilha Amaldiçoada (pacote H, passo 2, 06/11): esqueletos do KayKit que sobem da terra a qualquer hora quando o capitão
    /// chega perto (dois grupos: na trilha e no cemitério; voltam para a terra com ele longe), e o cerco do altar no alto da ilha: três
    /// ondas de mortos, as duas últimas com as feras da maldição (o ogro de caveira e os dois demônios do Ultimate Monsters do Quaternius,
    /// <see cref="CursedBeast"/>). Como no cemitério da Caveira, caem de vez só com pólvora ou a Lâmina de São Telmo; senão levantam uma vez.
    /// Vencer o cerco conta para o capítulo 6 da História ("altar_amaldicoado").
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string AltarSiegeId = "altar_amaldicoado";
        const string OrcModelPath = "Assets/ThirdParty/Quaternius_UltimateMonsters/Models/Big_Orc_Skull.fbx";
        const string DemonModelPath = "Assets/ThirdParty/Quaternius_UltimateMonsters/Models/Big_Demon.fbx";
        const string BlueDemonModelPath = "Assets/ThirdParty/Quaternius_UltimateMonsters/Models/Big_BlueDemon.fbx";
        // Impacto nos clipes do kit (s), medido pela mão mais rápida: soco e pancada com a arma.
        const float BeastPunchImpact = 0.38f, BeastSlamImpact = 0.42f;

        sealed class BeastSpec
        {
            public string name, label, path, controller, material;
            public float scale, health, walk, run, pitch;
            public Color tint, glow;
            public CursedBeast.Move[] moves;
        }

        static BeastSpec OrcSpec => new()
        {
            name = "Fera_Ogro", label = "Ogro de ossos", path = OrcModelPath, controller = "Fera_Ogro", material = "M_Fera_Ogro",
            scale = 0.85f, health = 300f, walk = 1.9f, run = 4f, pitch = 0.42f,
            tint = new Color(0.85f, 0.95f, 0.85f), glow = new Color(0.05f, 0.2f, 0.08f),
            moves = new[]
            {
                new CursedBeast.Move { state = "Punch", range = 2.6f, windUp = 0.8f, damage = 22f, recover = 0.7f, weight = 3f },
                new CursedBeast.Move { state = "Weapon", range = 2.9f, windUp = 1.1f, damage = 34f, recover = 1.1f, cooldown = 3.5f, weight = 2f, knockdown = Knockdown.Stumble },
            },
        };

        static BeastSpec DemonSpec(bool blue) => new()
        {
            name = blue ? "Fera_DemonioAzul" : "Fera_Demonio", label = blue ? "Demônio azul" : "Demônio da prata",
            path = blue ? BlueDemonModelPath : DemonModelPath, controller = blue ? "Fera_DemonioAzul" : "Fera_Demonio",
            material = blue ? "M_Fera_DemonioAzul" : "M_Fera_Demonio",
            scale = 0.85f, health = 260f, walk = 2.2f, run = 4.6f, pitch = blue ? 0.6f : 0.52f,
            tint = Color.white, glow = blue ? new Color(0.05f, 0.12f, 0.3f) : new Color(0.25f, 0.06f, 0.03f),
            moves = new[]
            {
                new CursedBeast.Move { state = "Punch", range = 2.6f, windUp = 0.7f, damage = 20f, recover = 0.6f, weight = 3f },
                new CursedBeast.Move { state = "Weapon", range = 2.9f, windUp = 1f, damage = 28f, recover = 1f, cooldown = 4f, weight = 1.5f, knockdown = Knockdown.Stumble },
                new CursedBeast.Move { state = "Duck", minRange = 5f, range = 12f, windUp = 0.8f, damage = 30f, recover = 1.2f, cooldown = 7f, weight = 6f, knockdown = Knockdown.Blast, leap = true },
            },
        };

        /// <summary>Depois do NavMesh da ilha: os grupos da trilha e do cemitério e o cerco do altar.</summary>
        static void AddCursedDead(Transform island, FootCharacter player, GameObject hud, LandLootAssets landLoot = null, LootItems items = null)
        {
            if (!player) return;
            var mats = LoadLandMaterials();
            var cutlass = EnemyCutlass();
            var pistol = EnemyPistol();
            Physics.SyncTransforms();

            // Trilha: entre a praia e o alto (4) e o cemitério (5). Sobem a 13 m do capitão, voltam para a terra com ele a 90 m.
            var trail = DeadGroup(island, "Mortos_Trilha", new Vector2(-4.5f, -29f), "mortos_trilha", "Mortos da trilha", player, mats, cutlass, pistol,
                new[] { "Pirata_Trilha_1", "Pirata_Trilha_2", "Atirador_Trilha", "Pirata_Trilha_3" }, 4.5f, 13f);
            var yard = DeadGroup(island, "Mortos_Cemiterio", CursedCemetery, "mortos_cemiterio", "Mortos do cemitério", player, mats, cutlass, pistol,
                new[] { "Pirata_Cova_1", "Pirata_Cova_2", "Bacamarteiro_Cova", "Pirata_Cova_3", "Pirata_Cova_4" }, 4f, 12f);
            trail.ConfigureAnyHour(90f, true);
            yard.ConfigureAnyHour(90f, true);

            // O cerco do altar.
            var altar = SkullGround(island, CursedSummit);
            var root = new GameObject("CercoDoAltar").transform;
            root.SetParent(island, true);
            root.position = altar;
            var waves = new[]
            {
                new AltarSiege.Wave
                {
                    dead = WaveGroup(root, island, 0, player, mats, cutlass, pistol,
                        new[] { "Pirata_Altar1_1", "Pirata_Altar1_2", "Atirador_Altar1", "Pirata_Altar1_3", "Pirata_Altar1_4" }),
                    beasts = new CursedBeast[0],
                },
                new AltarSiege.Wave
                {
                    dead = WaveGroup(root, island, 1, player, mats, cutlass, pistol,
                        new[] { "Pirata_Altar2_1", "Bacamarteiro_Altar2", "Pirata_Altar2_2", "Pirata_Altar2_3" }),
                    beasts = new[] { Beast(root, island, OrcSpec, 0f) },
                },
                new AltarSiege.Wave
                {
                    dead = WaveGroup(root, island, 2, player, mats, cutlass, pistol,
                        new[] { "Pirata_Altar3_1", "Atirador_Altar3", "Pirata_Altar3_2" }),
                    beasts = new[] { Beast(root, island, DemonSpec(false), 70f), Beast(root, island, DemonSpec(true), -70f) },
                },
            };
            var siege = root.gameObject.AddComponent<AltarSiege>();
            siege.Configure(player, waves, 40);
            root.gameObject.AddComponent<WorldMarker>().Configure(AltarSiegeId, "Altar amaldiçoado", 5f);
            hud.AddComponent<AltarSiegeHUD>().Bind(siege);

            // Lázaro Negro no templo (passo 3): aparece quando o cerco é quebrado.
            var curse = AddLazarus(island, root, siege, player, mats, cutlass, pistol);
            // A Coroa de Prata no tesouro de Lázaro, atrás do altar (passo 4).
            AddLazarusTreasure(island, curse, landLoot, items);
        }

        /// <summary>Esqueletos do KayKit pelo nome do posto (guerreiro e soldado de cutelo, ladino de pistola, mago de bacamarte).</summary>
        static string CursedDeadLook(string name, int k) =>
            name.StartsWith("Atirador") ? KayKitCharacters.Rogue :
            name.StartsWith("Bacamarteiro") ? KayKitCharacters.Mage :
            k % 2 == 0 ? KayKitCharacters.Warrior : KayKitCharacters.Minion;

        /// <summary>Um grupo de mortos em volta de um ponto (lugares livres no chão), num EnemyCamp com o CursedGraveyard.</summary>
        static CursedGraveyard DeadGroup(Transform island, string objectName, Vector2 local, string campId, string label, FootCharacter player,
            LandMaterials mats, WeaponDefinition cutlass, WeaponDefinition pistol, string[] names, float spread, float trigger)
        {
            var site = new GameObject(objectName).transform;
            site.SetParent(island, true);
            site.position = SkullGround(island, local);
            var rng = new System.Random(objectName.GetHashCode() & 0xffff);
            var posts = new List<Vector3>();
            for (int k = 0; k < names.Length; k++)
            {
                Vector3 spot = default;
                bool found = false;
                for (int tries = 0; tries < 40 && !found; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = spread * (0.4f + 0.6f * (float)rng.NextDouble());
                    var p = local + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var w = SkullGround(island, p);
                    // No chão (não em cima da cripta ou de um túmulo: o NavMesh também cobre o telhado).
                    if (!NavMesh.SamplePosition(w, out var hit, 0.6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - w.y) > 0.4f) continue;
                    // Nada por cima (o personagem nasce no primeiro chão que um raio de cima acha).
                    if (Physics.Raycast(hit.position + Vector3.up * 8f, Vector3.down, out var top, 10f, ~0, QueryTriggerInteraction.Ignore) &&
                        top.point.y > hit.position.y + 0.4f) continue;
                    bool near = false;
                    foreach (var q in posts) if ((q - hit.position).sqrMagnitude < 1.6f * 1.6f) near = true;
                    if (near) continue;
                    spot = hit.position;
                    found = true;
                }
                if (found) posts.Add(spot);
            }
            return CursedMembers(site, campId, label, player, mats, cutlass, pistol, names, posts, trigger, leash: 30f, attackers: 2);
        }

        /// <summary>Os mortos de uma onda do cerco: em volta do altar, entre ele e o anel de pilares (sobem pelo cerco, não sozinhos).</summary>
        static CursedGraveyard WaveGroup(Transform root, Transform island, int wave, FootCharacter player, LandMaterials mats,
            WeaponDefinition cutlass, WeaponDefinition pistol, string[] names)
        {
            var site = new GameObject($"Onda_{wave + 1}").transform;
            site.SetParent(root, false);
            var posts = new List<Vector3>();
            for (int k = 0; k < names.Length; k++)
            {
                // Espalhados no anel (5,5–7,5 m), cada onda girada, longe da entrada sul (por onde o capitão chega, se for o caso).
                for (int tries = 0; tries < 12; tries++)
                {
                    float a = (wave * 37f + k * (360f / names.Length) + tries * 13f) * Mathf.Deg2Rad;
                    float r = 5.5f + (k % 2) * 2f;
                    var p = CursedSummit + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                    if (!NavMesh.SamplePosition(SkullGround(island, p), out var hit, 0.8f, NavMesh.AllAreas)) continue;
                    posts.Add(hit.position);
                    break;
                }
            }
            return CursedMembers(site, $"altar_onda{wave + 1}", $"Mortos do altar ({wave + 1})", player, mats, cutlass, pistol, names, posts,
                trigger: 0f, leash: 20f, attackers: 2, publish: false);
        }

        static CursedGraveyard CursedMembers(Transform site, string campId, string label, FootCharacter player, LandMaterials mats,
            WeaponDefinition cutlass, WeaponDefinition pistol, string[] names, List<Vector3> posts, float trigger, float leash, int attackers,
            bool publish = true)
        {
            var camp = site.gameObject.AddComponent<EnemyCamp>();
            var parent = new GameObject("Mortos").transform;
            parent.SetParent(site, false);
            var members = new List<EnemyFootAI>();
            for (int k = 0; k < posts.Count; k++)
            {
                string name = names[k];
                bool gunner = name.StartsWith("Atirador");
                Vector3 to = site.position - posts[k];
                float yaw = to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 180f : k * 70f;
                keepKitLook = true;
                var ai = CreatePirate(parent, name, posts[k], yaw, PostWeapon(name, gunner, cutlass, pistol), player, camp, mats.iron, mats.wood,
                    mats.brass, CursedDeadLook(name, k));
                keepKitLook = false;
                foreach (var drop in ai.GetComponents<Pirates.Loot.DropOnDeath>()) Object.DestroyImmediate(drop);
                ai.GetComponent<Health>().Configure(SkeletonHealth);
                ai.GetComponent<FootCharacter>().ConfigureSpeeds(2f, 4.6f);
                if (k % 3 == 1 && !gunner && !name.StartsWith("Bacamarteiro")) ai.ConfigureLunge(3.6f, 6f, 1);
                members.Add(ai);
            }
            camp.Configure(label, campId, members.ToArray(), leash: leash, attackersAtOnce: attackers);
            camp.SetFame(publish ? 5 : 0);
            camp.SetHiddenFromHud(true);
            var graveyard = site.gameObject.AddComponent<CursedGraveyard>();
            graveyard.Configure(player.transform, trigger, GraveyardRiseClips());
            graveyard.ConfigureAnyHour(0f, publish);
            return graveyard;
        }

        // ---------- Feras ----------

        /// <summary>Uma fera do cerco, escondida (sai da terra a 7 m do altar, no ângulo dado a partir do norte).</summary>
        static CursedBeast Beast(Transform root, Transform island, BeastSpec spec, float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            var local = CursedSummit + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 7f;
            var ground = SkullGround(island, local);
            if (NavMesh.SamplePosition(ground, out var hit, 1.5f, NavMesh.AllAreas)) ground = hit.position;

            var go = new GameObject(spec.name);
            go.transform.SetParent(root, true);
            Vector3 toAltar = root.position - ground;
            toAltar.y = 0f;
            go.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(toAltar.sqrMagnitude > 0.01f ? -toAltar : Vector3.forward));
            var health = go.AddComponent<Health>();
            health.Configure(spec.health);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.7f;
            agent.height = 2.8f;
            agent.acceleration = 18f;
            agent.angularSpeed = 0f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.4f, 0f);
            capsule.radius = 0.75f;
            capsule.height = 2.8f;

            var body = new GameObject("Corpo").transform;
            body.SetParent(go.transform, false);
            var scale = new GameObject("Escala").transform;
            scale.SetParent(body, false);
            scale.localScale = Vector3.one * spec.scale;
            LoopClips(spec.path, "Idle", "Walk", "Run");
            var model = MonsterModel(spec.path, scale);
            var mat = BeastMaterial(spec);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = BeastAnimator(spec);
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var beast = go.AddComponent<CursedBeast>();
            beast.ConfigureBase(health, animator, "fera_amaldicoada", 8);
            beast.Configure(body, capsule, spec.walk, spec.run, spec.moves, spec.pitch, 3.6f);
            go.SetActive(false);
            return beast;
        }

        /// <summary>O atlas do kit com uma tinta e um brilho bem fraco da maldição.</summary>
        static Material BeastMaterial(BeastSpec spec)
        {
            // Refeito a cada geração (material nosso). Brilho bem fraco: mais forte, de noite as feras viravam silhuetas chapadas, como fantasmas.
            var mat = GetTexturedMaterial(spec.material, AssetDatabase.LoadAssetAtPath<Texture2D>(MonstersAtlasPath), 0.15f);
            mat.SetColor("_BaseColor", spec.tint);
            mat.SetColor("_EmissionColor", spec.glow * 0.3f);
            Emissive(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Animações de cada fera (os clipes são do FBX dela): parado, andar, correr, soco e pancada (a velocidade de cada estado faz o
        /// impacto cair no fim do preparo do golpe), agachar (preparo do salto), salto, pouso, levar golpe e morte.
        /// </summary>
        static RuntimeAnimatorController BeastAnimator(BeastSpec spec)
        {
            string path = $"{AnimationFolder}/{spec.controller}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path)) AssetDatabase.DeleteAsset(path);
            EnsureFolder(Root, "Animation");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var idle = State(machine, "Idle", MonsterClip(spec.path, "Idle"));
            State(machine, "Walk", MonsterClip(spec.path, "Walk"));
            State(machine, "Run", MonsterClip(spec.path, "Run"));
            foreach (var m in spec.moves)
            {
                if (m.leap) continue;
                var s = State(machine, m.state, MonsterClip(spec.path, m.state));
                s.speed = (m.state == "Weapon" ? BeastSlamImpact : BeastPunchImpact) / Mathf.Max(0.2f, m.windUp);
                BackTo(s, idle);
            }
            var duck = State(machine, "Duck", MonsterClip(spec.path, "Duck"));
            duck.speed = 1.6f;
            BackTo(duck, idle);
            var jump = State(machine, "Jump", MonsterClip(spec.path, "Jump"));
            var air = State(machine, "Jump_Idle", MonsterClip(spec.path, "Jump_Idle"));
            BackTo(jump, air);
            var land = State(machine, "Jump_Land", MonsterClip(spec.path, "Jump_Land"));
            BackTo(land, idle);
            BackTo(State(machine, "HitReact", MonsterClip(spec.path, "HitReact")), idle);
            State(machine, "Death", MonsterClip(spec.path, "Death"));
            machine.defaultState = idle;
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
