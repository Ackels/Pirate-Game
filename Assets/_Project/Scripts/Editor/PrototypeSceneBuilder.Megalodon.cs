using Pirates.Combat;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.Ships;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Megalodonte (chefe do mar, cena 07; F.1 passo 2b, 16/10): o tubarão do kit Quaternius (Characters_Shark, copiado do _Downloads)
    /// 3,5× maior (~22 m, maior que o galeão), mais encorpado, cinza-ardósia escuro com a barriga clara, cabeça e mandíbula maiores e a
    /// bocarra aberta (pelo <see cref="Megalodon"/>, depois da animação). Covil na "Fossa do Megalodonte", mar aberto a nordeste, longe de
    /// tudo. A luta é do <see cref="Megalodon"/>.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>
        /// Centro da Fossa: ~1,1 km do cais, ~620 m do brigue, ~730 m da Ilha Norte, ~670 m da Caveira (mapa espaçado em 16/10; era (620, 430)).
        /// </summary>
        static Vector3 MegalodonLair => new(860f, 0f, 560f);
        const string MegalodonId = "megalodonte", SharkModel = "Characters_Shark";
        const float MegalodonHealth = 1500f, MegalodonScale = 3.5f;
        // Mais encorpado que o tubarão do kit (largura e altura; o comprimento fica).
        static readonly Vector3 MegalodonBulk = new(1.18f, 1.15f, 1f);
        const string MegalodonAnimatorPath = AnimationFolder + "/Megalodonte.controller";

        static void AddMegalodon(ShipController player, LootAssets loot)
        {
            if (!HasKitCharacter(SharkModel))
            {
                Debug.LogWarning("[Pirates] Megalodonte: falta Characters_Shark no kit do Quaternius (copie do _Downloads).");
                return;
            }
            var root = new GameObject("Megalodonte");
            root.transform.position = MegalodonLair + Vector3.down * 16f;
            root.AddComponent<Health>().Configure(MegalodonHealth);

            var holder = new GameObject("Corpo").transform;
            holder.SetParent(root.transform, false);
            holder.localScale = Vector3.Scale(Vector3.one * MegalodonScale, MegalodonBulk);
            var model = KitCharacter(SharkModel, holder, CivilianMaterial(SharkModel, 1));
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = MegalodonAnimator();
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            Transform head = null, jaw = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head") head = t;
                if (t.name == "LowerJaw") jaw = t;
            }

            // Colisor das balas: cápsula ao longo do corpo, um pouco acima do centro (o dorso fica fora d'água circulando).
            var body = new GameObject("Colisor");
            body.transform.SetParent(root.transform, false);
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = 3.1f;
            capsule.height = 20f;
            capsule.center = new Vector3(0f, 1.2f, 0.5f);

            var aimPoint = new GameObject("Alvo");
            aimPoint.transform.SetParent(root.transform, false);
            aimPoint.transform.localPosition = new Vector3(0f, 1.6f, 3f);
            var aim = aimPoint.AddComponent<AimTarget>();

            root.AddComponent<Megalodon>().Configure(player.transform, holder, animator, head, jaw, aim);
            root.AddComponent<LootDropper>().Configure(MegalodonLoot(loot));
            // Marcador no covil (parado): no próprio tubarão ele andaria junto.
            var marker = new GameObject("Marcador_Megalodonte");
            marker.transform.position = MegalodonLair;
            marker.AddComponent<WorldMarker>().Configure(MegalodonId, "Fossa do Megalodonte", 14f);
            MegalodonLairLandmarks();
        }

        /// <summary>Nadar, nadar rápido (aviso, investida, mergulho) e morder: os três clipes do tubarão do kit, trocados pelo Megalodon.</summary>
        static RuntimeAnimatorController MegalodonAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MegalodonAnimatorPath);
            if (controller) return controller;
            EnsureFolder(Root, "Animation");
            controller = AnimatorController.CreateAnimatorControllerAtPath(MegalodonAnimatorPath);
            var machine = controller.layers[0].stateMachine;
            var swim = machine.AddState("Nadar");
            swim.motion = KitClip(SharkModel, "Swim");
            var fast = machine.AddState("NadarRapido");
            fast.motion = KitClip(SharkModel, "Swim_Fast");
            var bite = machine.AddState("Morder");
            bite.motion = KitClip(SharkModel, "Swim_Bite");
            machine.defaultState = swim;
            AssetDatabase.SaveAssets();
            return controller;
        }

        static LootTable MegalodonLoot(LootAssets loot)
        {
            var items = loot.items;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Megalodonte.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 250, 400), Drop(items.wood, 1f, 10, 20), Drop(items.powder, 0.7f, 6, 12), Drop(items.iron, 0.7f, 5, 10),
                };
                t.chestChance = 0.6f;
                t.schematics = loot.schematics;
                t.chestFallback = new ItemStack(items.gold, 400);
            });
            table.wreckPrefab = loot.enemyTable.wreckPrefab;
            EditorUtility.SetDirty(table);
            return table;
        }

        /// <summary>Marcas da Fossa: pedras num anel fora da área da luta e um casco partido (só visuais, sem colisor).</summary>
        static void MegalodonLairLandmarks()
        {
            var group = new GameObject("FossaDoMegalodonte").transform;
            group.position = MegalodonLair;
            Kenney("ship-wreck", group, Quaternion.Euler(0f, 300f, 0f) * Vector3.forward * 70f + Vector3.down * 1.8f,
                Quaternion.Euler(-6f, 40f, 16f), 1.2f, "CascoPartido");
            var rocks = new (string model, float angle, float distance, float scale)[]
            {
                ("rocks-c", 30f, 82f, 2.6f), ("rocks-a", 110f, 78f, 2.2f), ("rocks-b", 200f, 84f, 2.8f),
            };
            foreach (var (model, angle, distance, scale) in rocks)
            {
                Vector3 local = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance + Vector3.down * 0.6f;
                Kenney(model, group, local, Quaternion.Euler(0f, angle * 3f, 0f), scale, "Pedra");
            }
        }
    }
}
