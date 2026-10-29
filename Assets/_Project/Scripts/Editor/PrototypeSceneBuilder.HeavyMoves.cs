using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    public static partial class PrototypeSceneBuilder
    {
        // Golpes do capitão com as armas pesadas (23/10, pedido do Jhonata: "não estão fluidas"; os do brutamontes e do Barba-Negra dobram e
        // giram demais no capitão). Os inimigos continuam com os deles; o capitão usa AttackVariants próprios na mesma camada.
        // Machado: "Sword and Shield" do Mixamo (cortes de uma mão, corpo erguido): curto e rápido, corte largo, machadada de cima (derruba).
        // Espadão: "2Hand-Sword" do RPG Character da Explosive: corte de lado, estocada, golpe de cima (derruba). Escolhidos pela torção da
        // cabeça em relação ao peito só para um lado (Attack11 −67..4°, Attack9 −73..13°, Attack5 −86..25°); o Attack4 ia de −96 a +78° e a
        // cabeça pulava de um lado para o outro no limite do PostureLimiter.
        // Impacto = mão direita mais rápida em relação ao quadril, medido no esqueleto do capitão; cada golpe começa "lead" antes dele.
        const int PlayerAxeVariant = 3, PlayerGreatswordVariant = 8;

        static readonly (string clip, float impact, float lead)[] PlayerAxeAttacks =
        {
            ("Sword And Shield Attack 4", 0.42f, 0.42f),
            ("Sword And Shield Slash 3", 0.83f, 0.55f),
            ("Sword And Shield Slash", 0.58f, 0.58f),
        };

        static readonly (string clip, float impact, float lead)[] PlayerGreatswordAttacks =
        {
            ("Attack11", 0.60f, 0.50f),
            ("Attack9", 0.38f, 0.38f),
            ("Attack5", 0.43f, 0.43f),
        };

        // Adaga do capitão (E2, passo 2, 23/10): WeaponType 0 (pose, corrida, guarda, sacar e bainha do cutelo) com golpes próprios,
        // AttackVariant 7–9 ("Adaga_Golpe2..4"; 2 e 3 são as estocadas da duelista). Estocada rápida ("Stabbing" do Mixamo, a mesma
        // da duelista), corte curto ("Sword And Shield Attack 4") e o bote com dois cortes ("Sword And Shield Attack 2", mão rápida aos 0,33 e
        // 0,63 s). Medidos no esqueleto do capitão: tronco ≤ 38°, torção −57..55° (fora a estocada, que o PostureLimiter segura).
        const int PlayerDaggerVariant = 7;

        static readonly (string clip, float impact, float lead)[] PlayerDaggerAttacks =
        {
            ("Stabbing", 0.42f, 0.35f),
            ("Sword And Shield Attack 4", 0.42f, 0.42f),
            ("Sword And Shield Attack 2", 0.33f, 0.33f),
        };

        /// <summary>Segundo corte do bote (s depois do primeiro, na velocidade normal do clipe).</summary>
        const float DaggerFinisherSecondCut = 0.30f;

        const string ExplosiveSwordFolder = ThirdPartyImportSettings.ExplosiveFolder + "/Animations/2Hand-Sword";

        /// <summary>Clipe "RPG-Character@2Hand-Sword-&lt;nome&gt;" do pacote da Explosive.</summary>
        static AnimationClip ExplosiveClip(string name)
        {
            string path = $"{ExplosiveSwordFolder}/RPG-Character@2Hand-Sword-{name}.FBX";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        /// <summary>
        /// Estados dos golpes do capitão: "Machado_Golpe3..5" (WeaponType 3) e "Espadao_Golpe8..10" (WeaponType 4), entrando pelo Any State
        /// como os dos inimigos e voltando ao pronto da arma 0,75 s depois do impacto.
        /// </summary>
        static void AddPlayerHeavyAttacks(AnimatorStateMachine sm)
        {
            AnimatorState axeReady = null, swordReady = null, bladeReady = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == "MachadoPronto") axeReady = child.state;
                if (child.state.name == "Espadao_Pronto") swordReady = child.state;
                if (child.state.name == "EspadaPronta") bladeReady = child.state;
            }
            if (bladeReady)
                for (int i = 0; i < PlayerDaggerAttacks.Length; i++)
                {
                    var (clipName, impact, lead) = PlayerDaggerAttacks[i];
                    // O bote volta à pose depois do segundo corte.
                    float last = i == PlayerDaggerAttacks.Length - 1 ? impact + DaggerFinisherSecondCut : impact;
                    PlayerSwing(sm, bladeReady, $"Adaga_Golpe{2 + i}", MixamoClip(clipName), impact, lead, 0, PlayerDaggerVariant + i,
                        new Vector3(900f, 140f + 80f * i), last + 0.45f - impact);
                }
            if (axeReady)
                for (int i = 0; i < PlayerAxeAttacks.Length; i++)
                {
                    var (clipName, impact, lead) = PlayerAxeAttacks[i];
                    PlayerSwing(sm, axeReady, $"Machado_Golpe{PlayerAxeVariant + i}", MixamoClip(clipName), impact, lead, 3, PlayerAxeVariant + i,
                        new Vector3(-1200f, 400f + 80f * i));
                }
            if (swordReady)
                for (int i = 0; i < PlayerGreatswordAttacks.Length; i++)
                {
                    var (clipName, impact, lead) = PlayerGreatswordAttacks[i];
                    PlayerSwing(sm, swordReady, $"Espadao_Golpe{PlayerGreatswordVariant + i}", ExplosiveClip(clipName), impact, lead, 4,
                        PlayerGreatswordVariant + i, new Vector3(-1800f, 860f + 80f * i));
                }
        }

        static void PlayerSwing(AnimatorStateMachine sm, AnimatorState ready, string name, AnimationClip clip, float impact, float lead, int weaponType,
            int variant, Vector3 position, float tail = 0.75f)
        {
            if (!clip) return;
            var swing = sm.AddState(name, position);
            swing.motion = clip;
            swing.tag = FullBodyTag;
            swing.speedParameterActive = true;
            swing.speedParameter = "AttackSpeed";
            Any(sm, swing, Mathf.Max(0f, impact - lead) / clip.length, HeavyAttackBlend,
                ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, weaponType),
                ("AttackVariant", AnimatorConditionMode.Equals, variant));
            Exit(swing, ready, Mathf.Min(0.95f, (impact + tail) / clip.length), 0.3f);
        }

        /// <summary>
        /// Adaga do capitão: os três golpes acima em sequência (impacto ÷ a velocidade da animação), o bote cortando duas vezes com um pouco
        /// mais de dano, e o golpe pelas costas (<see cref="WeaponDefinition.backstabDamage"/>) dobrando o dano. Refeito a cada geração.
        /// </summary>
        static void PlayerDaggerMoves(WeaponDefinition w, float damage, float backstab)
        {
            if (!w) return;
            w.damage = damage;
            float k = Mathf.Max(0.1f, w.attackAnimSpeed);
            w.animatorWeaponType = 0;
            w.attackVariants = PlayerDaggerAttacks.Length;
            w.randomVariants = false;
            w.animatorVariantOffset = 0;
            w.animatorVariants = new int[PlayerDaggerAttacks.Length];
            w.variantHitDelays = new float[PlayerDaggerAttacks.Length];
            for (int i = 0; i < PlayerDaggerAttacks.Length; i++)
            {
                w.animatorVariants[i] = PlayerDaggerVariant + i;
                w.variantHitDelays[i] = PlayerDaggerAttacks[i].lead / k;
            }
            w.hitDelay = w.variantHitDelays[0];
            w.hitPush = 1.6f;
            w.backstabDamage = backstab;
            w.breaksGuard = false;
            w.moveSpeedWhileAttacking = 0.6f;
            w.variantStrikes = new[]
            {
                new StrikeOverride(), new StrikeOverride(),
                new StrikeOverride
                {
                    damage = Mathf.Round(w.damage * 1.15f), range = w.range + 0.2f, followUps = new[] { DaggerFinisherSecondCut / k },
                    extraRecovery = 0.15f, push = 2.6f,
                },
            };
            EditorUtility.SetDirty(w);
        }

        /// <summary>
        /// Arma pesada do capitão nos golpes acima (refeito a cada geração: o asset já existe desde o E2): os três em sequência, o impacto
        /// de cada um (÷ a velocidade da animação) e o terceiro derrubando com empurrão maior.
        /// </summary>
        static void PlayerHeavyMoves(WeaponDefinition w, bool axe)
        {
            if (!w) return;
            // Espadões de duas mãos: a mão de apoio no cabo (os machados do capitão usam golpes de uma mão).
            w.supportOffset = axe ? 0f : GreatswordSupport;
            float k = Mathf.Max(0.1f, w.attackAnimSpeed);
            var table = axe ? PlayerAxeAttacks : PlayerGreatswordAttacks;
            int first = axe ? PlayerAxeVariant : PlayerGreatswordVariant;
            w.attackVariants = table.Length;
            w.randomVariants = false;
            w.animatorVariantOffset = 0;
            w.animatorVariants = new int[table.Length];
            w.variantHitDelays = new float[table.Length];
            for (int i = 0; i < table.Length; i++)
            {
                w.animatorVariants[i] = first + i;
                w.variantHitDelays[i] = table[i].lead / k;
            }
            w.hitDelay = w.variantHitDelays[0];
            w.hitPush = 3f;
            w.variantStrikes = new[]
            {
                new StrikeOverride(), new StrikeOverride(),
                new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, push = 5f },
            };
            EditorUtility.SetDirty(w);
        }
    }
}
