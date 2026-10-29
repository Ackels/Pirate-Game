using Pirates.Combat;
using Pirates.Core;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Brutamontes (pacote B, passo 1, 09/10): pirata grande (homem-tubarão 35% maior, pele mais escura) com o machado duplo do Quaternius
    /// nas duas mãos (27/10: animações do anão e da Explosive, WeaponType 5, ver PrototypeSceneBuilder.BruteAnims). Lento, muita vida, não é interrompido por golpe nem derrubado por
    /// chute (a explosão derruba); golpes largos e pesados, avisados (0,6 s), que derrubam o capitão e cansam a guarda (3× o fôlego);
    /// aparar abre a guarda dele. Grita ao ver o capitão. Posto com nome "Brutamontes_..." nas listas dos acampamentos.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Vida 200 → 280, golpe 30 → 35, espera 2,1 → 1,7 s e combo 0,2 → 0,3 (09/10, revisão do chat de otimização + pedido do Jhonata:
        // sozinho ele morria em ~10 s trocando golpe; "como ele é maior tem que bater mais forte").
        // Depois, 280 → 330 de vida, corrida 3,9 → 4,4 e investida (o Jhonata jogando: "fácil desviando e rolando").
        const float BruteScale = 1.35f, BruteHealth = 330f;
        static readonly Color BruteSkin = new(0.78f, 0.6f, 0.55f);

        static bool IsBrute(string postName) => postName.StartsWith("Brutamontes");

        /// <summary>Machado duplo do brutamontes (Data/Weapons/Weapon_Machado_Brutamontes; preservado se existe, tempos refeitos).</summary>
        static WeaponDefinition BruteAxe() => BruteMoves(BruteAxeAsset());

        static WeaponDefinition BruteAxeAsset() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Machado_Brutamontes.asset", w =>
        {
            w.displayName = "Machado duplo";
            w.kind = WeaponKind.Melee;
            w.animatorWeaponType = 3;
            w.damage = 35f;
            w.range = 2.5f;
            w.angle = 150f;
            // Impacto 0,6 s depois do começo (os estados do machado começam AxeLead antes do impacto, com AttackSpeed 1).
            w.attackAnimSpeed = 1f;
            w.hitDelay = AxeLead;
            w.variantHitDelays = System.Array.ConvertAll(AxeAttacks, a => a.lead);
            w.randomVariants = true;
            w.cooldown = 1.7f;
            w.attackVariants = AxeAttacks.Length;
            w.comboWindow = 3.5f;
            w.animatedDraw = false;
            w.moveSpeedWhileAttacking = 0.1f;
            w.knockdown = Knockdown.Stumble;
            w.guardCost = 3f;
        });

        /// <summary>
        /// Machado duplo na mão direita. Com as animações de duas mãos (27/10) o cabo passa pelas duas mãos e a cabeça fica além da
        /// direita (<see cref="BruteAxeGrip"/>); sem elas, o eixo antigo do cutelo (lâmina ao longo do antebraço).
        /// </summary>
        static Transform AxeOn(Transform model, Material iron, Material wood, out Transform handSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            HandGrip(model, out Vector3 arm, out Vector3 thumb);
            var root = new GameObject("Machado").transform;
            root.SetParent(hand, false);
            float scale = 0.62f;
            Vector3 grip = new(0f, -0.05f, 0f);
            if (BruteAxeGrip(model, out Vector3 handle, out Vector3 swing))
            {
                // O cabo passa pelo meio do punho fechado (~0,08 m à frente do pulso, onde fica o osso da mão do kit).
                root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.08f), Quaternion.LookRotation(handle, swing));
                scale = BruteAxeScale;
                grip = BruteAxeGripPoint;
            }
            else
            {
                Vector3 palm = Vector3.Cross(arm, thumb).normalized;
                Vector3 blade = (0.96f * arm + 0.25f * palm + 0.11f * thumb).normalized;
                Vector3 edge = 0.84f * thumb - 0.54f * palm + 0.04f * arm;
                root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.08f), Quaternion.LookRotation(blade, edge));
            }
            // No kit o cabo vai de y −0,29 a ~0,2 e as cabeças de 0,2 a 0,72. Uma mão: pega no terço de baixo, × 0,62 (com 0,85 as cabeças
            // entravam no chão na pose parada). Duas mãos: a direita logo abaixo das cabeças e a esquerda perto do fim do cabo, × 0,95
            // (× 1,35 do brutamontes ≈ 1,3 m; a cabeça fica para cima na guarda).
            if (!KitWeapon(root, "Weapon_DoubleAxe", Quaternion.LookRotation(Vector3.right, Vector3.forward), scale, grip))
            {
                Part(PrimitiveType.Cylinder, "Cabo", root, new Vector3(0f, 0f, 0.3f), new Vector3(0.04f, 0.42f, 0.04f), Quaternion.Euler(90f, 0f, 0f), wood);
                Part(PrimitiveType.Cube, "Cabeca", root, new Vector3(0f, 0f, 0.62f), new Vector3(0.02f, 0.5f, 0.22f), Quaternion.identity, iron);
            }
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_Machado");
            return root;
        }

        /// <summary>Pele mais escura (o mesmo atlas do kit, tingido), feita uma vez.</summary>
        static Material BruteMaterial()
        {
            var mat = GetTexturedMaterial("M_Quaternius_Brutamontes", QuaterniusAtlas, 0.1f);
            if (mat.GetColor("_BaseColor") == Color.white)
            {
                mat.SetColor("_BaseColor", BruteSkin);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>Transforma o pirata recém-montado em brutamontes: tamanho, vida, velocidade, reações, IA e cor.</summary>
        static void MakeBrute(GameObject go, Transform model, EnemyFootAI ai)
        {
            // Escala na raiz: colisor, alcance visual da arma e o machado (filho da mão) crescem juntos.
            go.transform.localScale = Vector3.one * BruteScale;
            go.GetComponent<Health>().Configure(BruteHealth);
            // Anda mais devagar que os piratas (2,2/5,2) e bem mais que o capitão (6): dá para fugir e para rodear.
            go.GetComponent<FootCharacter>().ConfigureSpeeds(1.9f, 4.4f);
            // Grande: vira mais devagar (dava meia-volta em 0,16 s).
            go.GetComponent<FootCharacter>().ConfigureTurnRate(260f);
            go.GetComponent<FootCharacter>().ConfigurePushScale(0.35f);
            var reactions = go.GetComponent<FootHealthReactions>();
            reactions.Configure(model, 0f);
            reactions.SetHeavy(true);
            ai.ConfigureMelee(2.4f, 4.4f, new Vector2(1.2f, 2.2f), 0.3f);
            // Sem preparo a mais: os golpes do machado já levam 0,6–1,0 s até o impacto (o "!" aparece neles).
            ai.ConfigureIntent(0f);
            ai.ConfigureTaunt(2.2f);
            // Investida: o machado sai avançando com o capitão até 4,5 m (quem rolou para longe ainda leva).
            ai.ConfigureLunge(4.5f, 6.5f, 1);
            // E2: derrotado, deixa o machado dele (o "Machado do carrasco", épico), uma vez.
            DropsBlade(go, "MachadoCarrasco");
            if (!model) return;
            var skin = BruteMaterial();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = skin;
                r.sharedMaterials = slots;
            }
        }
    }
}
