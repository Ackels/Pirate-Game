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

        // Machado com golpes próprios e sorteados (17/11, Jhonata: "cada arma ter seus golpes e mais variantes"): cada posição do combo
        // tem duas versões, sorteadas a cada clique (WeaponDefinition.comboAlternates 2). Ordem: diagonal, gancho que puxa | revés, giro de
        // 360° | machadada de cima, salto do carrasco. As novas tocam a guarda (golpes feitos à mão, GreatswordSwing) com AttackVariant 11–13.
        const int PlayerAxeAltVariant = 11;
        static readonly float[] PlayerAxeComboDelays = { 0.3231f, 0.42f, 0.4231f, 0.40f, 0.4462f, 0.55f };

        // Cutelo do capitão e lâminas de uma mão (17/11): 6 golpes feitos à mão, 2 por posição do combo e sorteados: corte rápido da direita |
        // estocada que avança | espadada de lado · corte de volta | corte em X | gancho · corte de cima (derruba) | corte subindo (derruba e
        // empurra) | pirueta (17/11: 3 por posição). Estados "Cutelo_Golpe40..48" (WeaponType 0, AttackVariant 40–48) tocando a guarda do
        // cutelo; o resto é o GreatswordSwing ("Cutelo").
        const int PlayerCutlassVariant = 40;
        static readonly float[] PlayerCutlassDelays = { 0.29f, 0.32f, 0.30f, 0.30f, 0.24f, 0.28f, 0.42f, 0.40f, 0.50f };
        /// <summary>Segundo corte do X (s depois do primeiro, na velocidade 1).</summary>
        const float CutlassXSecondCut = 0.18f;

        static readonly (string clip, float impact, float lead)[] PlayerAxeAttacks =
        {
            ("Sword And Shield Attack 4", 0.42f, 0.42f),
            ("Sword And Shield Slash 3", 0.83f, 0.55f),
            ("Sword And Shield Slash", 0.58f, 0.58f),
        };

        // Espadão com golpes sorteados (17/11): mais 2 versões por posição, AttackVariant 60–65 ("Espadao_Golpe60..65", tocam a guarda):
        // estocada de duas mãos | corte baixo · giro largo | corte subindo da esquerda · salto com golpe de cima | corte subindo.
        const int PlayerGreatswordAltVariant = 60;
        static readonly float[] PlayerGreatswordAltDelays = { 0.40f, 0.42f, 0.50f, 0.32f, 0.55f, 0.40f };

        static readonly (string clip, float impact, float lead)[] PlayerGreatswordAttacks =
        {
            ("Attack11", 0.60f, 0.50f),
            ("Attack9", 0.38f, 0.38f),
            ("Attack5", 0.43f, 0.43f),
        };

        // Adaga do capitão (E2, passo 2, 23/10): WeaponType 0 (pose, corrida, guarda, sacar e bainha do cutelo) com golpes próprios,
        // AttackVariant 7–9 ("Adaga_Golpe2..4"; 2 e 3 são as estocadas da duelista). Estocada rápida (a estocada de lança do Kevin Iglesias,
        // a mesma da duelista desde 07/11; o "Stabbing" do Mixamo passava a adaga por cima do rosto com a mão a 0,25 m à frente), corte curto ("Sword And Shield Attack 4") e o bote com dois cortes ("Sword And Shield Attack 2", mão rápida aos 0,33 e
        // 0,63 s). Medidos no esqueleto do capitão: tronco ≤ 38°, torção −57..55° (fora a estocada, que o PostureLimiter segura).
        const int PlayerDaggerVariant = 7;

        static readonly (string clip, float impact, float lead)[] PlayerDaggerAttacks =
        {
            ("Kevin:" + DaggerThrust, 0.45f, 0.35f),
            ("Sword And Shield Attack 4", 0.42f, 0.42f),
            ("Sword And Shield Attack 2", 0.33f, 0.33f),
        };

        // Adagas com golpes feitos à mão (17/11): 9, 3 por posição e sorteados (GreatswordSwing "Adaga"); estados "Punhal_Golpe50..58"
        // (WeaponType 0, AttackVariant 50–58) tocando a guarda do cutelo. Tempos na velocidade 1 (÷ attackAnimSpeed).
        const int PlayerDaggerHandVariant = 50;
        static readonly float[] PlayerDaggerHandDelays = { 0.26f, 0.28f, 0.32f, 0.24f, 0.44f, 0.20f, 0.38f, 0.50f, 0.40f };

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
            AnimatorState axeReady = null, swordReady = null, bladeReady = null, bruteReady = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == "MachadoPronto") axeReady = child.state;
                if (child.state.name == "Espadao_Pronto") swordReady = child.state;
                if (child.state.name == "EspadaPronta") bladeReady = child.state;
                if (child.state.name == "Bruto_Pronto") bruteReady = child.state;
            }
            if (bladeReady)
                for (int i = 0; i < PlayerDaggerAttacks.Length; i++)
                {
                    var (clipName, impact, lead) = PlayerDaggerAttacks[i];
                    // O bote volta à pose depois do segundo corte.
                    float last = i == PlayerDaggerAttacks.Length - 1 ? impact + DaggerFinisherSecondCut : impact;
                    PlayerSwing(sm, bladeReady, $"Adaga_Golpe{2 + i}", DaggerClip(clipName), impact, lead, 0, PlayerDaggerVariant + i,
                        new Vector3(900f, 140f + 80f * i), last + 0.45f - impact);
                }
            if (bladeReady && bladeReady.motion is AnimationClip guardClip)
                for (int i = 0; i < PlayerCutlassDelays.Length; i++)
                    PlayerSwing(sm, bladeReady, $"Cutelo_Golpe{PlayerCutlassVariant + i}", guardClip, 0f, 0f, 0, PlayerCutlassVariant + i,
                        new Vector3(1200f, 140f + 80f * i));
            if (bladeReady && bladeReady.motion is AnimationClip enemyGuard)
                // 70–72: cutelo e sabre dos inimigos; 73–74: adaga da duelista.
                for (int i = 0; i < 5; i++)
                    PlayerSwing(sm, bladeReady, $"Inimigo_Golpe{EnemyHandVariant + i}", enemyGuard, 0f, 0f, 0, EnemyHandVariant + i,
                        new Vector3(1800f, 140f + 80f * i));
            // 75–77: machado do brutamontes (WeaponType 5).
            if (bruteReady && bruteReady.motion is AnimationClip bruteGuard)
                for (int i = 0; i < 3; i++)
                    PlayerSwing(sm, bruteReady, $"Inimigo_Golpe{EnemyHandVariant + 5 + i}", bruteGuard, 0f, 0f, BruteWeaponType, EnemyHandVariant + 5 + i,
                        new Vector3(2100f, 140f + 80f * i));
            if (bladeReady && bladeReady.motion is AnimationClip daggerGuard)
                for (int i = 0; i < PlayerDaggerHandDelays.Length; i++)
                    PlayerSwing(sm, bladeReady, $"Punhal_Golpe{PlayerDaggerHandVariant + i}", daggerGuard, 0f, 0f, 0, PlayerDaggerHandVariant + i,
                        new Vector3(1500f, 140f + 80f * i));
            if (axeReady)
                for (int i = 0; i < PlayerAxeAttacks.Length; i++)
                {
                    var (clipName, impact, lead) = PlayerAxeAttacks[i];
                    PlayerSwing(sm, axeReady, $"Machado_Golpe{PlayerAxeVariant + i}", MixamoClip(clipName), impact, lead, 3, PlayerAxeVariant + i,
                        new Vector3(-1200f, 400f + 80f * i));
                    // Golpes feitos à mão (17/11, GreatswordSwing): o estado toca a guarda parada do machado; braços, tronco, pernas e a arma
                    // são postos pelo código.
                    foreach (var child in sm.states)
                        if (child.state.name == $"Machado_Golpe{PlayerAxeVariant + i}") child.state.motion = axeReady.motion;
                    // A outra versão desta posição do combo (gancho, giro, salto).
                    PlayerSwing(sm, axeReady, $"Machado_Golpe{PlayerAxeAltVariant + i}", MixamoClip(clipName), impact, lead, 3, PlayerAxeAltVariant + i,
                        new Vector3(-1500f, 400f + 80f * i));
                    foreach (var child in sm.states)
                        if (child.state.name == $"Machado_Golpe{PlayerAxeAltVariant + i}") child.state.motion = axeReady.motion;
                }
            if (swordReady)
                for (int i = 0; i < PlayerGreatswordAttacks.Length; i++)
                {
                    var (clipName, impact, lead) = PlayerGreatswordAttacks[i];
                    PlayerSwing(sm, swordReady, $"Espadao_Golpe{PlayerGreatswordVariant + i}", ExplosiveClip(clipName), impact, lead, 4,
                        PlayerGreatswordVariant + i, new Vector3(-1800f, 860f + 80f * i));
                    // Golpes feitos à mão (17/11, GreatswordSwing): o estado fica com a guarda parada (as pernas e o corpo calmos) e os braços,
                    // o tronco e a espada são postos pelo código. Os tempos da transição continuam os do golpe gravado.
                    if (BasicMotion(BruteGuard))
                        foreach (var child in sm.states)
                            if (child.state.name == $"Espadao_Golpe{PlayerGreatswordVariant + i}") child.state.motion = BasicMotion(BruteGuard);
                }
            // 80–87 (menos 86, o chute): espadão do Barba-Negra e de Lázaro.
            if (swordReady && swordReady.motion is AnimationClip bossGuard)
                for (int i = 0; i < 8; i++)
                    if (i != 6)
                        PlayerSwing(sm, swordReady, $"Inimigo_Golpe{EnemyHandVariant + 10 + i}", bossGuard, 0f, 0f, 4, EnemyHandVariant + 10 + i,
                            new Vector3(-2400f, 860f + 80f * i));
            if (swordReady && BasicMotion(BruteGuard))
                for (int i = 0; i < PlayerGreatswordAltDelays.Length; i++)
                    PlayerSwing(sm, swordReady, $"Espadao_Golpe{PlayerGreatswordAltVariant + i}", BasicMotion(BruteGuard), 0f, 0f, 4,
                        PlayerGreatswordAltVariant + i, new Vector3(-2100f, 860f + 80f * i));
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
            PlayerDaggerCombo(w);
            EditorUtility.SetDirty(w);
        }

        /// <summary>Adagas: os 9 golpes feitos à mão (3 por posição, sorteados) com os efeitos de cada um. O golpe pelas costas continua.</summary>
        public static void PlayerDaggerCombo(WeaponDefinition w)
        {
            if (!w) return;
            float k = Mathf.Max(0.1f, w.attackAnimSpeed);
            int n = PlayerDaggerHandDelays.Length;
            w.handMoves = "Adaga";
            w.attackVariants = n;
            w.comboAlternates = 3;
            w.randomVariants = false;
            w.animatorVariantOffset = 0;
            w.animatorVariants = new int[n];
            w.variantHitDelays = new float[n];
            for (int i = 0; i < n; i++)
            {
                w.animatorVariants[i] = PlayerDaggerHandVariant + i;
                w.variantHitDelays[i] = PlayerDaggerHandDelays[i] / k;
            }
            w.hitDelay = w.variantHitDelays[0];
            w.variantStrikes = new[]
            {
                new StrikeOverride(),
                new StrikeOverride { angle = w.angle + 20f },
                // Corte em diagonal subindo: arco largo, um pouco mais forte, empurra.
                new StrikeOverride { angle = 130f, damageScale = 1.1f, push = 3f },
                // Duas estocadas.
                new StrikeOverride { damageScale = 0.6f, followUps = new[] { 0.16f / k } },
                // Giro com corte: pega todos em volta.
                new StrikeOverride { angle = 360f, range = w.range + 0.2f, extraRecovery = 0.15f },
                // Gancho e estocada: o gancho empurra pouco.
                new StrikeOverride { damageScale = 0.65f, followUps = new[] { 0.18f / k } },
                new StrikeOverride
                {
                    damageScale = 1.3f, customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, extraRecovery = 0.25f, push = 3.5f,
                },
                // Rolamento com corte: chega pelo lado (pelas costas, o dano dobra).
                new StrikeOverride { angle = 140f, range = w.range + 0.2f, damageScale = 1.1f, extraRecovery = 0.15f },
                // Estocada voadora: salta e crava, mais forte e mais longe.
                new StrikeOverride { damageScale = 1.4f, range = w.range + 0.3f, angle = 70f, push = 4f, extraRecovery = 0.2f },
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
            w.comboAlternates = 1;
            if (axe) PlayerAxeCombo(w);
            else PlayerGreatswordCombo(w);
            EditorUtility.SetDirty(w);
        }

        /// <summary>
        /// Cutelo e lâminas de uma mão do capitão: as 6 versões (2 por posição) com os efeitos de cada uma, a partir do dano, do alcance e
        /// da velocidade da arma (refeito a cada geração). O combo fica no mesmo nível de antes: o X dá dois acertos de 65%, os golpes de baixo
        /// e de cima +30–40% e derrubam.
        /// </summary>
        public static void PlayerCutlassCombo(WeaponDefinition w)
        {
            if (!w) return;
            float k = Mathf.Max(0.1f, w.attackAnimSpeed);
            w.handMoves = "Cutelo";
            int n = PlayerCutlassDelays.Length;
            w.attackVariants = n;
            w.comboAlternates = 3;
            w.randomVariants = false;
            w.animatorVariantOffset = 0;
            w.animatorVariants = new int[n];
            w.variantHitDelays = new float[n];
            for (int i = 0; i < n; i++)
            {
                w.animatorVariants[i] = PlayerCutlassVariant + i;
                w.variantHitDelays[i] = PlayerCutlassDelays[i] / k;
            }
            w.hitDelay = w.variantHitDelays[0];
            w.hitPush = 2.4f;
            w.variantStrikes = new[]
            {
                new StrikeOverride(),
                // Estocada: vai mais longe num arco estreito.
                new StrikeOverride { range = w.range + 0.6f, angle = 50f, push = 3f },
                // Espadada de lado: arco bem largo, braço esticado.
                new StrikeOverride { angle = 170f, range = w.range + 0.2f, push = 3.5f },
                new StrikeOverride(),
                // X: dois cortes, cada um mais fraco.
                new StrikeOverride { damageScale = 0.65f, followUps = new[] { CutlassXSecondCut / k } },
                // Gancho: mais fraco, empurra forte.
                new StrikeOverride { damageScale = 0.8f, range = w.range - 0.4f, push = 5.5f },
                new StrikeOverride
                {
                    damage = Mathf.Round(w.damage * 1.4f), range = w.range + 0.2f, angle = w.angle + 20f,
                    customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, extraRecovery = 0.3f, push = 4f,
                },
                // Corte subindo: derruba e joga ~2 m para trás (v = raiz(2 · freio 10 · 2 m)).
                new StrikeOverride
                {
                    damage = Mathf.Round(w.damage * 1.3f), range = w.range + 0.1f,
                    customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, extraRecovery = 0.3f, push = 6.3f,
                },
                // Pirueta: pega todos em volta.
                new StrikeOverride { angle = 360f, range = w.range + 0.2f, damageScale = 1.2f, push = 4f, extraRecovery = 0.2f },
            };
            EditorUtility.SetDirty(w);
        }

        /// <summary>Espadão: as 9 versões (3 por posição do combo; as 3 primeiras são os golpes de antes) com os efeitos de cada uma.</summary>
        public static void PlayerGreatswordCombo(WeaponDefinition w)
        {
            if (!w) return;
            float k = Mathf.Max(0.1f, w.attackAnimSpeed);
            var a = PlayerGreatswordAltDelays;
            w.attackVariants = 9;
            w.comboAlternates = 3;
            w.randomVariants = false;
            int v = PlayerGreatswordVariant, x = PlayerGreatswordAltVariant;
            w.animatorVariants = new[] { v, x, x + 1, v + 1, x + 2, x + 3, v + 2, x + 4, x + 5 };
            var g = PlayerGreatswordAttacks;
            w.variantHitDelays = new[] { g[0].lead / k, a[0], a[1], g[1].lead / k, a[2], a[3], g[2].lead / k, a[4], a[5] };
            w.hitDelay = w.variantHitDelays[0];
            var down = new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, push = 5f };
            w.variantStrikes = new[]
            {
                new StrikeOverride(),
                // Estocada: mais longe, arco estreito.
                new StrikeOverride { range = w.range + 0.5f, angle = 50f, push = 4f },
                // Corte baixo: arco bem largo, um pouco mais fraco.
                new StrikeOverride { angle = 170f, damageScale = 0.9f },
                new StrikeOverride(),
                // Giro largo: pega todos em volta.
                new StrikeOverride { angle = 360f, range = w.range + 0.2f, damageScale = 0.9f, push = 4f, extraRecovery = 0.15f },
                new StrikeOverride { angle = 140f, push = 3f },
                down,
                // Salto com golpe de cima: mais forte, derruba, deixa uma brecha na aterrissagem.
                new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, push = 5.5f, damageScale = 1.15f, angle = 200f,
                    extraRecovery = 0.2f },
                // Corte subindo: derruba e joga ~2 m para trás.
                new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, push = 6.5f, damageScale = 1.1f, extraRecovery = 0.25f },
            };
            EditorUtility.SetDirty(w);
        }

        /// <summary>Machado: as 6 versões (2 por posição do combo) com os efeitos de cada uma.</summary>
        public static void PlayerAxeCombo(WeaponDefinition w)
        {
            if (!w) return;
            w.attackVariants = 6;
            w.comboAlternates = 2;
            w.randomVariants = false;
            w.animatorVariants = new[] { PlayerAxeVariant, PlayerAxeAltVariant, PlayerAxeVariant + 1, PlayerAxeAltVariant + 1, PlayerAxeVariant + 2, PlayerAxeAltVariant + 2 };
            w.variantHitDelays = (float[])PlayerAxeComboDelays.Clone();
            w.hitDelay = w.variantHitDelays[0];
            w.variantStrikes = new[]
            {
                new StrikeOverride(),
                // Gancho: fisga e traz o inimigo 0,5 m antes de o machado descer.
                new StrikeOverride { pullAt = 0.17f, pullDistance = 0.5f, pullRange = 2.6f, pullAngle = 90f },
                new StrikeOverride(),
                // Giro: pega todos em volta, um pouco mais fraco.
                new StrikeOverride { angle = 360f, range = 2.4f, damageScale = 0.8f },
                new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, push = 5f },
                // Salto do carrasco: mais lento e forte, derruba; deixa uma brecha na aterrissagem.
                new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, push = 5.5f, damageScale = 1.1f, angle = 200f,
                    extraRecovery = 0.15f },
            };
            EditorUtility.SetDirty(w);
        }
    }
}
