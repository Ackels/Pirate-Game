using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Cemitério amaldiçoado da Caveira (pacote B, passo 3, 10/10): covas ao lado da trilha oeste (a caminho das ruínas), com montes de
    /// terra, placas de madeira tortas do Survival Kit, uma pá fincada, cerca quebrada e uma lanterna. Esqueletos do kit (com e sem
    /// cabeça) de cutelo, de osso esverdeado com brilho, que saem das covas à noite quando o capitão chega perto (CursedGraveyard) e viram
    /// pó ao amanhecer. Animações "Zombie Stand Up 1–3" do Mixamo (estados "Erguer0..2").
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Centro do cemitério (local da Caveira): 12 m ao lado da trilha oeste, fora do morro das ruínas.</summary>
        static Vector2 GraveyardSpot => new(-36f, -27f);
        const float GraveyardHeight = 1.7f, GraveyardTrigger = 16f;
        // 10/10, revisão do chat de otimização + Jhonata ("as 4 melhorias, mas 10 esqueletos"): 5 → 10 esqueletos, vida 45 → 60, golpe
        // 12 → 14 (cutelo próprio), 3 atacando por vez, covas espaçadas (um corte pegava 3–5 saindo juntos), investida em 3 deles.
        const float SkeletonHealth = 60f;

        // Sair da cova: clipe, início deitado (s do clipe) e quando fica de pé (s do clipe), medidos pela altura do quadril.
        const float RiseSpeed = 1.3f;
        static readonly (string clip, float start, float standing)[] RiseClips =
        {
            ("Zombie Stand Up1", 0.9f, 2.6f), ("Zombie Stand Up2", 0.6f, 3.0f), ("Zombie Stand Up3", 0.3f, 2.4f),
        };

        // Covas (local do cemitério; ângulo da cova): 3 fileiras de 4, a ~4 m uma da outra, viradas para a trilha.
        static readonly (Vector2 at, float yaw)[] Graves =
        {
            (new(-6f, 4.4f), 8f), (new(-2f, 4.8f), -5f), (new(2f, 4.5f), 4f), (new(6f, 4.2f), -9f),
            (new(-6.3f, 0.4f), -3f), (new(-2.2f, 0.6f), 6f), (new(1.8f, 0.2f), -6f), (new(6.1f, 0.5f), 5f),
            (new(-5.8f, -3.6f), 2f), (new(-1.9f, -3.4f), -7f), (new(2.2f, -3.8f), 9f), (new(5.9f, -3.5f), -4f),
        };
        // Quantos esqueletos (o resto das covas fica vazio, só enfeite).
        const int SkeletonCount = 10;

        /// <summary>Estados "Erguer0..N" no tronco (corpo inteiro, "CorpoInteiroAndando"); entram direto pelo CharacterAnimatorDriver.PlayRise.</summary>
        static void AddRise(AnimatorStateMachine sm, AnimatorState empty)
        {
            for (int v = 0; v < RiseClips.Length; v++)
            {
                var (clipName, _, standing) = RiseClips[v];
                var clip = MixamoClip(clipName);
                if (!clip) continue;
                var rise = State(sm, CharacterAnimatorDriver.RiseStatePrefix + v, clipName, RiseSpeed, new Vector3(-1500f, 60f + 80f * v));
                rise.tag = MovingFullBodyTag;
                Exit(rise, empty, Mathf.Min(0.95f, standing / clip.length), 0.25f);
            }
        }

        /// <summary>Para o CursedGraveyard: onde cada animação começa e quanto tempo o esqueleto fica parado levantando.</summary>
        static CursedGraveyard.RiseClip[] GraveyardRiseClips()
        {
            var list = new System.Collections.Generic.List<CursedGraveyard.RiseClip>();
            foreach (var (clipName, start, standing) in RiseClips)
                if (MixamoClip(clipName))
                    list.Add(new CursedGraveyard.RiseClip { startSeconds = start, seconds = (standing - start) / RiseSpeed + 0.15f });
            return list.ToArray();
        }

        /// <summary>Visual do cemitério na ilha (antes do NavMesh). Devolve a raiz (as covas são os postos dos esqueletos).</summary>
        static Transform Graveyard(Transform island, Vector2 local)
        {
            var root = new GameObject("Cemiterio").transform;
            root.SetParent(island, false);
            root.position = SkullGround(island, local);
            // De frente para a trilha oeste (o ponto dela mais perto).
            var toTrail = (TrailWest[1] - local).normalized;
            root.rotation = Quaternion.Euler(0f, Mathf.Atan2(toTrail.x, toTrail.y) * Mathf.Rad2Deg, 0f);
            var dirt = GetMaterial("M_Terra_Cova", new Color(0.33f, 0.24f, 0.16f), 0.05f);
            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Vector3 Ground(Vector2 at)
            {
                var w = root.TransformPoint(new Vector3(at.x, 0f, at.y));
                return root.InverseTransformPoint(SkullGround(island, new Vector2(w.x - island.position.x, w.z - island.position.z)));
            }

            for (int k = 0; k < Graves.Length; k++)
            {
                var (at, yaw) = Graves[k];
                var grave = new GameObject($"Cova_{k}").transform;
                grave.SetParent(root, false);
                grave.SetLocalPositionAndRotation(Ground(at), Quaternion.Euler(0f, yaw, 0f));
                // Monte de terra (meio enterrado) e a placa de madeira na cabeceira, torta.
                Part(PrimitiveType.Sphere, "Monte", grave, new Vector3(0f, -0.05f, 0f), new Vector3(0.95f, 0.35f, 1.9f), Quaternion.identity, dirt);
                var sign = Model(Kit.Survival, "signpost-single", grave, new Vector3(0f, 0f, 1.05f), 180f + R(-12f, 12f), SurvivalScale * 0.8f, "Placa");
                sign.transform.localRotation *= Quaternion.Euler(R(-10f, 10f), 0f, R(-12f, 12f));
            }
            Model(Kit.Survival, "tool-shovel", root, Ground(new Vector2(8.4f, -1.6f)) + Vector3.up * 0.35f, 30f, SurvivalScale, "Pa")
                .transform.localRotation *= Quaternion.Euler(0f, 0f, 70f);
            for (int k = 0; k < 5; k++)
            {
                var at = new Vector2(-8.6f + k * 4.3f, 7.6f);
                var fence = Model(Kit.Survival, "fence", root, Ground(at), R(-6f, 6f), SurvivalScale * 0.8f, $"Cerca_{k}");
                if (k == 2) fence.transform.localRotation *= Quaternion.Euler(0f, 0f, 25f);   // a caída
            }
            PostLantern(root, root.TransformPoint(Ground(new Vector2(-9.2f, 2.4f))), "Lanterna_Cemiterio", realLight: true, range: 8f);
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return root;
        }

        /// <summary>Esqueletos amaldiçoados: um por cova (os primeiros), num EnemyCamp com o CursedGraveyard.</summary>
        static CursedGraveyard CreateGraveyardSkeletons(Transform site, FootCharacter player, Material iron, Material wood, Material brass)
        {
            var camp = site.gameObject.AddComponent<EnemyCamp>();
            var parent = new GameObject("Esqueletos").transform;
            var cutlass = SkeletonCutlass();
            int count = Mathf.Min(SkeletonCount, Graves.Length);
            var members = new EnemyFootAI[count];
            for (int k = 0; k < count; k++)
            {
                var (at, yaw) = Graves[k];
                // Em cima do monte, virado para a trilha (o rosto sai da terra olhando para quem chega).
                var position = site.TransformPoint(new Vector3(at.x, 0f, at.y));
                string model = k % 2 == 0 ? KitCharacters.Skeleton : KitCharacters.HeadlessSkeleton;
                var ai = CreatePirate(parent, $"Esqueleto_Cova{k}", position, site.eulerAngles.y + yaw, cutlass, player, camp, iron, wood, brass, model);
                MakeCursed(ai.gameObject, ai.transform.Find("Model"));
                // Três deles com investida (avançam no golpe e vão atrás de quem rola), como a duelista.
                if (k % 3 == 1) ai.ConfigureLunge(3.6f, 6f, 1);
                members[k] = ai;
            }
            camp.Configure("Cemitério amaldiçoado", "cemiterio_caveira", members, leash: 26f, attackersAtOnce: 3);
            camp.SetFame(8);
            var graveyard = site.gameObject.AddComponent<CursedGraveyard>();
            graveyard.Configure(player.transform, GraveyardTrigger, GraveyardRiseClips());
            return graveyard;
        }

        /// <summary>Cutelo enferrujado dos esqueletos: o dos piratas com 14 de dano (Data/Weapons/Weapon_Cutelo_Esqueleto; preservado se existe).</summary>
        static WeaponDefinition SkeletonCutlass() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Cutelo_Esqueleto.asset", w =>
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(EnemyCutlass()), w);
            w.displayName = "Cutelo enferrujado";
            w.damage = 14f;
        });

        /// <summary>Esqueleto amaldiçoado: um pouco mais lento que os piratas, osso esverdeado com brilho fraco.</summary>
        static void MakeCursed(GameObject go, Transform model)
        {
            go.GetComponent<Health>().Configure(SkeletonHealth);
            go.GetComponent<FootCharacter>().ConfigureSpeeds(2f, 4.6f);
            if (!model) return;
            var mat = CursedMaterial();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
            }
        }

        /// <summary>Atlas do kit tingido de verde-osso com emissão verde fraca (aparece no escuro), feito uma vez.</summary>
        static Material CursedMaterial()
        {
            var mat = GetTexturedMaterial("M_Quaternius_Amaldicoado", QuaterniusAtlas, 0.1f);
            if (KeepsEmission(mat)) return mat;
            mat.SetColor("_BaseColor", new Color(0.78f, 0.95f, 0.8f));
            mat.SetColor("_EmissionColor", new Color(0.06f, 0.24f, 0.1f));
            Emissive(mat);
            return mat;
        }
    }
}
