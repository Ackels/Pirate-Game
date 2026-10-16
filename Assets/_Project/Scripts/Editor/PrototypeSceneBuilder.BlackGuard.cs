using Pirates.Combat;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using UnityEditor;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Guarda Negra, os guarda-costas do Capitão Barba-Negra nas ruínas da Caveira (pacote B, passo 4, passo 2; 16/10): modelos que
    /// ainda não eram inimigos (o Henry e a Anne do kit, de preto e vermelho-sangue, as cores do chefe) com armas novas do kit: dois
    /// espadachins de sabre (Weapon_Sword_1, sempre na mão, mais fortes e rápidos que os piratas, com investida) e uma mosqueteira com o
    /// mosquete-machadinha (Weapon_AxeRifle, um tiro só de longe, com a linha vermelha de aviso). O chefe espera no meio das ruínas
    /// enquanto eles lutam (<see cref="LandBoss.HoldingBack"/>).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string BlackGuardPrefix = "GuardaNegra";
        const float BlackGuardHealth = 120f, MusketeerHealth = 90f;
        // Mosquete-machadinha do kit: cano para −X como o rifle (x de −1,57 a 0,75), pegada no gatilho.
        const float MusketScale = 0.66f;
        static readonly Vector3 MusketGrip = new(-0.15f, 0f, 0f), MusketMuzzle = new(-1.57f, 0.2f, 0f);

        static bool IsBlackGuard(string postName) => postName.StartsWith(BlackGuardPrefix);
        static bool IsMusketeer(string postName) => IsBlackGuard(postName) && postName.Contains("Mosquete");

        /// <summary>Sabre da Guarda Negra: o cutelo dos piratas mais forte (14, era 12) e um pouco mais rápido; sempre na mão.</summary>
        static WeaponDefinition BlackGuardSabre() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Sabre_GuardaNegra.asset", w =>
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(EnemyCutlass()), w);
            const float animSpeed = 0.8f;
            w.displayName = "Sabre da Guarda Negra";
            w.damage = 14f;
            w.range = 2.1f;
            w.attackAnimSpeed = animSpeed;
            w.hitDelay = 0.3f / animSpeed;
            w.cooldown = 0.6f / animSpeed;
            w.comboWindow = 1.1f / animSpeed;
            w.animatedDraw = false;
        });

        /// <summary>
        /// Mosquete da Guarda Negra: as animações e a mira do bacamarte, mas um tiro só (26 de dano), longe (24 m) e recarga de 4 s. A linha
        /// vermelha avisa: correndo de lado ou rolando dá para sair da mira.
        /// </summary>
        static WeaponDefinition BlackGuardMusket() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Mosquete_GuardaNegra.asset", w =>
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(BlunderbussWeapon()), w);
            w.displayName = "Mosquete da Guarda Negra";
            w.damage = 26f;
            w.pellets = 1;
            w.pelletSpread = 0f;
            w.range = 24f;
            w.angle = 3f;
            w.reloadTime = 4f;
        });

        /// <summary>Sabre do kit na mão direita, como a adaga da duelista (lâmina ao longo do antebraço). Sempre na mão.</summary>
        static Transform SabreOn(Transform model, Material iron, Material wood, out Transform handSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            HandGrip(model, out Vector3 arm, out Vector3 thumb);
            var root = new GameObject("Sabre").transform;
            root.SetParent(hand, false);
            Vector3 palm = Vector3.Cross(arm, thumb).normalized;
            Vector3 blade = (0.96f * arm + 0.25f * palm + 0.11f * thumb).normalized;
            Vector3 edge = 0.84f * thumb - 0.54f * palm + 0.04f * arm;
            root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.08f), Quaternion.LookRotation(blade, edge));
            // No kit o cabo vai de y −0,18 a ~0 e a lâmina até 1,19: pegada no meio do cabo; × 0,62 ≈ 85 cm no total.
            if (!KitWeapon(root, "Weapon_Sword_1", Quaternion.LookRotation(Vector3.right, Vector3.forward), 0.62f, new Vector3(0f, -0.09f, 0f)))
            {
                Part(PrimitiveType.Cylinder, "Cabo", root, Vector3.zero, new Vector3(0.03f, 0.07f, 0.03f), Quaternion.Euler(90f, 0f, 0f), wood);
                Part(PrimitiveType.Cube, "Lamina", root, new Vector3(0f, 0f, 0.42f), new Vector3(0.012f, 0.04f, 0.7f), Quaternion.identity, iron);
            }
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_Sabre");
            return root;
        }

        /// <summary>Guarda Negra: vida, velocidade, jeito de lutar e a roupa preta e vermelha.</summary>
        static void MakeBlackGuard(GameObject go, Transform model, EnemyFootAI ai, string kitModel, bool musketeer)
        {
            go.GetComponent<Health>().Configure(musketeer ? MusketeerHealth : BlackGuardHealth);
            // Um pouco mais rápidos que os piratas (2,2/5,2), abaixo do capitão (6).
            go.GetComponent<FootCharacter>().ConfigureSpeeds(2.4f, 5.4f);
            if (musketeer)
            {
                // Fica longe (14 m), recua se o capitão chega a 6 m, começa a mirar a 22 m e segura a mira 1,3–1,7 s (a linha vermelha).
                ai.ConfigureRanged(14f, 6f, 22f, new Vector2(1.3f, 1.7f), 2f);
            }
            else
            {
                // Bate um pouco mais longe que os piratas, emenda até 2 golpes e avança no golpe (pega quem sai rolando cedo).
                ai.ConfigureMelee(2.1f, 3.2f, new Vector2(0.3f, 0.8f), 0.5f);
                ai.ConfigureLunge(3.8f, 6f, 2);
                // Cada espadachim deixa 1 ração ao cair (16/10, pedido do Jhonata: chegar ao chefe com um pouco de vida).
                var sack = AssetDatabase.LoadAssetAtPath<LootSack>($"{LootPrefabsFolder}/Loot_Saco.prefab");
                var ration = LoadItem("Item_Racao");
                if (sack && ration) go.AddComponent<DropOnDeath>().Configure(sack, new[] { new ItemStack(ration, 1) });
            }
            if (!model) return;
            var outfit = CivilianMaterial(kitModel, kitModel == KitCharacters.Anne ? 6 : 9);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = outfit;
                r.sharedMaterials = slots;
            }
        }

        /// <summary>Arena das ruínas: tochas em volta do anel de colunas (acendem à noite) e duas bandeiras pretas na entrada (o arco).</summary>
        static void RuinsArena(Transform island, Transform ruins, Vector2 local)
        {
            var arena = new GameObject("Arena").transform;
            arena.SetParent(ruins, false);
            for (int k = 0; k < 4; k++)
            {
                // Entre as colunas (elas ficam em 20° + 40° × k), no lado de dentro do anel.
                float a = (k * 90f + 40f) * Mathf.Deg2Rad;
                var p = local + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 6.4f;
                PostLantern(arena, SkullGround(island, p), $"Tocha_{k}");
            }
            foreach (var (offset, name) in new[] { (new Vector2(6.2f, 7.6f), "Bandeira_A"), (new Vector2(8.6f, 4.6f), "Bandeira_B") })
            {
                var ground = SkullGround(island, local + offset);
                Kenney("flag-pirate-high", arena, arena.InverseTransformPoint(ground), 45f, 1.4f, name);
            }
        }
    }
}
