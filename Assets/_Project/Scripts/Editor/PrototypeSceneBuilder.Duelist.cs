using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Duelista (pacote B, passo 2, 09/10): a Anne do kit de roupa escura e vermelha, com a adaga do Quaternius. Rápida e ágil, pouca vida a
    /// mais que um pirata: rodeia o capitão, entra com estocadas curtas (às vezes duas) e sai andando de ré; ergue a guarda, apara golpes
    /// e rola para longe (EnemyFootAI, "Duelista"). Golpes = "Stabbing" e "Stabbing1" do Mixamo; pose de pronto, corrida e guarda são as
    /// do cutelo (WeaponType 0; os golpes dela são o AttackVariant 2 e 3). Posto com nome "Duelista_..." nas listas dos acampamentos.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Vida 70 → 80 → 100 e estocada 9 → 12 → 14 (09/10: revisão do chat de otimização e depois o Jhonata jogando: "ficou bem fácil
        // desviando e rolando, principalmente a duelista").
        const float DuelistHealth = 100f;
        // Golpes: impacto medido no clipe (mão direita mais à frente): Stabbing 0,42 s, Stabbing1 0,88 s. Cada um começa "lead" s de clipe
        // antes do impacto e toca a DaggerSpeed. Rápido (Stabbing, ~0,3 s) e lento (Stabbing1 do começo, ~0,74 s, com o braço armando de
        // lado), sorteados: com só o rápido, rolar 0,2 s depois do começo sempre escapava (09/10, o Jhonata achou fácil rolando).
        const float DaggerSpeed = 1.15f;
        const int DaggerVariantOffset = 2;
        static readonly (string clip, float impact, float lead)[] DaggerAttacks = { ("Stabbing", 0.42f, 0.35f), ("Stabbing1", 0.88f, 0.85f) };

        static bool IsDuelist(string postName) => postName.StartsWith("Duelista");

        /// <summary>Adaga da duelista (Data/Weapons/Weapon_Adaga_Duelista; preservada se existe).</summary>
        static WeaponDefinition DuelistDagger() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Adaga_Duelista.asset", w =>
        {
            w.displayName = "Adaga";
            w.kind = WeaponKind.Melee;
            w.animatorWeaponType = 0;
            w.animatorVariantOffset = DaggerVariantOffset;
            w.damage = 14f;
            w.range = 1.9f;
            w.angle = 110f;
            w.attackAnimSpeed = DaggerSpeed;
            w.hitDelay = DaggerAttacks[0].lead / DaggerSpeed;
            w.variantHitDelays = System.Array.ConvertAll(DaggerAttacks, a => a.lead / DaggerSpeed);
            w.randomVariants = true;
            w.cooldown = 0.75f;
            w.attackVariants = DaggerAttacks.Length;
            w.comboWindow = 1.4f;
            w.animatedDraw = false;
            w.moveSpeedWhileAttacking = 0.3f;
        });

        /// <summary>Adaga na mão direita, como o cutelo (lâmina ao longo do antebraço). Sempre na mão.</summary>
        static Transform DaggerOn(Transform model, Material iron, Material wood, out Transform handSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            HandGrip(model, out Vector3 arm, out Vector3 thumb);
            var root = new GameObject("Adaga").transform;
            root.SetParent(hand, false);
            Vector3 palm = Vector3.Cross(arm, thumb).normalized;
            Vector3 blade = (0.96f * arm + 0.25f * palm + 0.11f * thumb).normalized;
            Vector3 edge = 0.84f * thumb - 0.54f * palm + 0.04f * arm;
            root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.08f), Quaternion.LookRotation(blade, edge));
            // No kit o cabo vai de y −0,28 a ~0,05 e a lâmina até 1,04: pegada no meio do cabo; × 0,48 ≈ 63 cm no total (com 0,4 quase
            // sumia na câmera do jogo).
            if (!KitWeapon(root, "Weapon_Dagger", Quaternion.LookRotation(Vector3.right, Vector3.forward), 0.48f, new Vector3(0f, -0.12f, 0f)))
            {
                Part(PrimitiveType.Cylinder, "Cabo", root, Vector3.zero, new Vector3(0.03f, 0.06f, 0.03f), Quaternion.Euler(90f, 0f, 0f), wood);
                Part(PrimitiveType.Cube, "Lamina", root, new Vector3(0f, 0f, 0.24f), new Vector3(0.012f, 0.04f, 0.36f), Quaternion.identity, iron);
            }
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_Adaga");
            return root;
        }

        /// <summary>Transforma o pirata recém-montado em duelista: vida, velocidade, IA de duelo e roupa.</summary>
        static void MakeDuelist(GameObject go, Transform model, EnemyFootAI ai)
        {
            go.GetComponent<Health>().Configure(DuelistHealth);
            // Mais rápida que os piratas (2,2/5,2), ainda abaixo do capitão (6): rolando dá para fugir dela.
            go.GetComponent<FootCharacter>().ConfigureSpeeds(2.6f, 5.8f);
            // Estoca de perto (1,85 m: a guarda empurra para trás), espera mais longe que os outros e emenda até 3 estocadas.
            ai.ConfigureMelee(1.85f, 3.4f, new Vector2(0.2f, 0.6f), 0.7f);
            // Preparo curto: a estocada rápida (0,3 s) continua rápida, mas o "!" aparece antes.
            ai.ConfigureIntent(0.2f);
            // Investida: estoca avançando com o capitão até 4,2 m (pega quem sai rolando: ela vai atrás do rolamento).
            ai.ConfigureLunge(4.2f, 8f, 2);
            ai.ConfigureDuelist(parry: 0.4f, dodge: 0.3f, keepAt: 3.2f, guardChance: 0.45f, pressedGuard: 0.6f);
            // Cambaleia menos que os piratas (0,6 s) ao levar golpe: volta logo à guarda ou ao contra-ataque.
            go.GetComponent<FootHealthReactions>().Configure(model, 0.35f);
            if (!model) return;
            var outfit = CivilianMaterial(KitCharacters.Anne, DuelistPalette);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = outfit;
                r.sharedMaterials = slots;
            }
        }

        /// <summary>Cores da duelista na linha da Anne no atlas (CivilianPalette): camisa vermelho-escura, colete e calça pretos, cabelo preto.</summary>
        const int DuelistPalette = 4;

        /// <summary>
        /// Golpes da adaga (WeaponType 0 + AttackVariant 2 e 3): começam "lead" antes do impacto e voltam à pose de pronto do cutelo
        /// (a guarda e a corrida também são as dele).
        /// </summary>
        static void AddDagger(AnimatorStateMachine sm, AnimatorState ready)
        {
            for (int v = 0; v < DaggerAttacks.Length; v++)
            {
                var (clipName, impact, lead) = DaggerAttacks[v];
                var clip = MixamoClip(clipName);
                if (!clip) continue;
                var stab = State(sm, $"Adaga_Golpe{v}", clipName, 1f, new Vector3(900f, -60f + 80f * v));
                stab.tag = FullBodyTag;
                stab.speedParameterActive = true;
                stab.speedParameter = "AttackSpeed";
                Any(sm, stab, Mathf.Max(0f, impact - lead) / clip.length, 0.1f,
                    ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 0),
                    ("AttackVariant", AnimatorConditionMode.Equals, DaggerVariantOffset + v));
                Exit(stab, ready, Mathf.Min(0.95f, (impact + 0.45f) / clip.length), 0.25f);
            }
        }
    }
}
