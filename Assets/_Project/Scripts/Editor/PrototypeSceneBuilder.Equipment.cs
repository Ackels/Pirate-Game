using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.OnFoot;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Equipamento do capitão (pacote E, passo 1, 19/10; pedido do Jhonata: "mais prêmios nos baús, para serem mais reconfortantes"):
    /// lâminas com raridade que caem dos baús e se equipam na tela I (UI/EquipmentHUD) no lugar do cutelo de começo. Cada lâmina é um
    /// item (Data/Items/Equip_*, categoria Gear, único, Equip Slot = Blade) que aponta para a arma dela (Data/Weapons) e tem o visual
    /// montado no capitão, desligado, no mesmo lugar da mão e da bainha do cutelo (Controls/GearWeapons troca). As tabelas de saque
    /// ganham a seção de equipamento (só entra se a tabela ainda não tem: depois o asset é do Jhonata). Raridade = cor do facho do baú,
    /// faixa ao abrir e fanfarra. Assets existentes são preservados.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>
        /// Uma lâmina: arquivo do item e da arma, nome, raridade, modelo do kit (escala e ponto de pegada no espaço do modelo), tinta do
        /// material (branco = o atlas do kit), brilho e os números (dano, velocidade da animação de golpe, alcance).
        /// </summary>
        /// <summary>Que arma é: cutelo e sabres (animações do cutelo), machado (as do brutamontes, WeaponType 3) ou espadão (as do Barba-Negra, 4).</summary>
        enum BladeKind { Cutlass, Axe, Greatsword }

        sealed class BladeSpec
        {
            public BladeKind kind;
            public string file, name, model, description;
            public ItemRarity rarity;
            public float scale = 0.7f;
            public Vector3 grip;
            public Color tint = Color.white, glow = Color.black;
            public float damage = 25f, animSpeed = 1f, range = 2.2f, angle = 110f;
            /// <summary>Lâmina sagrada: fere almas transparentes, os amaldiçoados não levantam, e arde em fogo azul.</summary>
            public bool holy;
            /// <summary>Machado e espadão (E2): espera entre golpes (s) e se quebra a guarda.</summary>
            public float cooldown = 0.85f;
            public bool breaksGuard;
        }

        // Cutelo de começo: 25 de dano, 0,6 s entre golpes (~42 de dano/s). As novas: +20% a +45% de dano por segundo.
        static readonly BladeSpec[] BladeSpecs =
        {
            new()
            {
                file = "CuteloToledo", name = "Cutelo de Toledo", rarity = ItemRarity.Rare, model = "Weapon_Cutlass", scale = 0.7f,
                tint = new Color(0.82f, 0.93f, 1.15f), glow = new Color(0.03f, 0.07f, 0.14f), damage = 31f,
                description = "Aço espanhol temperado: o mesmo cutelo de sempre, mas corta como navalha. Mais dano por golpe.",
            },
            new()
            {
                file = "SabreAbordagem", name = "Sabre de abordagem", rarity = ItemRarity.Rare, model = "Weapon_Sword_1", scale = 0.66f,
                grip = new Vector3(0f, -0.09f, 0f), damage = 23f, animSpeed = 1.25f, range = 2.4f, angle = 100f,
                description = "Lâmina longa e leve de oficial de marinha: golpes mais rápidos e mais longe, um pouco menos de dano em cada.",
            },
            new()
            {
                file = "SabreAlmirante", name = "Sabre do almirante", rarity = ItemRarity.Epic, model = "Weapon_Sword_1", scale = 0.68f,
                grip = new Vector3(0f, -0.09f, 0f), tint = new Color(1.25f, 1.02f, 0.55f), glow = new Color(0.22f, 0.15f, 0.03f),
                damage = 27f, animSpeed = 1.3f, range = 2.5f, angle = 105f,
                description = "Folheado a ouro, tirado de um almirante que não precisava mais dele. Rápido e certeiro: o melhor sabre dos mares.",
            },
            // C5, passo 3 (20/10): só no baú do Capelão Fantasma, garantida (fica fora do sorteio das outras tabelas). ~62 de dano/s.
            new()
            {
                file = "LaminaSaoTelmo", name = "Lâmina de São Telmo", rarity = ItemRarity.Legendary, model = "Weapon_Sword_2", scale = 0.6f,
                grip = new Vector3(0f, -0.1f, 0f), tint = new Color(0.85f, 0.97f, 1.3f), glow = new Color(0.12f, 0.35f, 0.85f),
                damage = 32f, animSpeed = 1.2f, range = 2.4f, angle = 110f, holy = true,
                description = "A espada do capelão da Frota de Prata, benzida contra os mortos: arde no fogo azul de São Telmo. Fere as almas "
                    + "penadas mesmo transparentes, e os mortos que ela derruba não levantam mais.",
            },
            // E2, passo 1 (20/10): armas pesadas, com as animações do brutamontes (machado) e do Barba-Negra (espadão). Combo de 3 golpes; o
            // terceiro derruba (o do espadão corta duas vezes). Os raros caem dos baús; os épicos, do brutamontes e do Barba-Negra.
            new()
            {
                kind = BladeKind.Axe, file = "MachadoAbordagem", name = "Machado de abordagem", rarity = ItemRarity.Rare, model = "Weapon_Axe",
                scale = 0.6f, grip = new Vector3(0f, -0.05f, 0f), damage = 38f, animSpeed = 1.3f, cooldown = 0.85f, range = 2.3f, angle = 115f,
                breaksGuard = true,
                description = "Machado curto de arrombar portas e escotilhas. Golpes pesados que quebram a guarda de quem se defende; o terceiro "
                    + "golpe do combo derruba.",
            },
            new()
            {
                kind = BladeKind.Axe, file = "MachadoCarrasco", name = "Machado do carrasco", rarity = ItemRarity.Epic, model = "Weapon_DoubleAxe",
                scale = 0.5f, grip = new Vector3(0f, -0.05f, 0f), tint = new Color(0.62f, 0.6f, 0.63f),
                damage = 46f, animSpeed = 1.3f, cooldown = 0.85f, range = 2.5f, angle = 120f, breaksGuard = true,
                description = "O machado duplo do brutamontes, o carrasco da Caveira. Quebra qualquer guarda, e o terceiro golpe do combo derruba.",
            },
            new()
            {
                kind = BladeKind.Greatsword, file = "Montante", name = "Montante do corsário", rarity = ItemRarity.Rare, model = "Weapon_Sword_2",
                scale = 0.58f, grip = new Vector3(0f, -0.1f, 0f), tint = new Color(0.95f, 0.96f, 1.02f), damage = 40f, animSpeed = 1.2f,
                cooldown = 0.9f, range = 2.7f, angle = 160f,
                description = "Espadão de duas mãos: lento, mas corta num arco largo e pega vários de uma vez. O terceiro golpe do combo corta "
                    + "duas vezes e derruba.",
            },
            new()
            {
                kind = BladeKind.Greatsword, file = "EspadaoBarbaNegra", name = "Espadão do Barba-Negra", rarity = ItemRarity.Epic,
                model = "Weapon_Sword_2", scale = 0.62f, grip = new Vector3(0f, -0.1f, 0f), tint = new Color(0.4f, 0.35f, 0.37f),
                damage = 48f, animSpeed = 1.2f, cooldown = 0.9f, range = 2.8f, angle = 165f,
                description = "A lâmina negra do Capitão Barba-Negra, que ele largou nas ruínas da Caveira. Arco largo; o terceiro golpe do combo "
                    + "corta duas vezes e derruba.",
            },
        };

        /// <summary>A lâmina do arquivo <paramref name="file"/> (Equip_&lt;file&gt;).</summary>
        static ItemDefinition BladeItemByFile(string file)
        {
            foreach (var spec in BladeSpecs)
                if (spec.file == file) return BladeItem(spec);
            return null;
        }

        /// <summary>A Lâmina de São Telmo (o baú do Capelão Fantasma).</summary>
        static ItemDefinition LegendaryBladeItem()
        {
            foreach (var spec in BladeSpecs)
                if (spec.holy) return BladeItem(spec);
            return null;
        }

        /// <summary>
        /// Onde cada lâmina cai (tabela, chance de vir equipamento no baú, pesos de Toledo/Sabre/Almirante). Os baús em terra sorteiam
        /// junto com a carga; os do mar, no baú dos destroços (Tormenta, lula e megalodonte têm o baú garantido).
        /// </summary>
        static readonly (string table, float chance, float toledo, float sabre, float admiral)[] BladeDrops =
        {
            ("LootTable_BauAcampamento", 0.3f, 3f, 3f, 1f), ("LootTable_Contrabando", 0.3f, 2f, 3f, 1f), ("LootTable_PaiolForte", 0.35f, 2f, 2f, 2f),
            ("LootTable_TesouroEnterrado", 0.3f, 3f, 2f, 1f), ("LootTable_TesouroContrabandista", 0.35f, 2f, 3f, 1f),
            ("LootTable_MapaDoTesouro", 0.5f, 2f, 2f, 2f), ("LootTable_Ermida", 0.6f, 1f, 1f, 3f),
            ("LootTable_Brigue", 0.3f, 2f, 2f, 1f), ("LootTable_Galeao", 0.6f, 1f, 1f, 3f), ("LootTable_Lula", 0.6f, 1f, 1f, 3f),
            ("LootTable_Megalodonte", 0.5f, 1f, 1f, 2f), ("LootTable_Cacador", 0.25f, 2f, 2f, 1f), ("LootTable_ChalupaInimiga", 0.1f, 3f, 3f, 0f),
            ("LootTable_Catacumbas", 0.7f, 1f, 1f, 3f),
        };

        static WeaponDefinition BladeWeapon(BladeSpec spec) =>
            LoadOrCreateAsset<WeaponDefinition>($"{WeaponsFolder}/Weapon_{spec.file}.asset", w =>
            {
                // Parte do cutelo do capitão (mesmas animações, saque e bainha) e muda o que a lâmina tem de seu.
                var cutlass = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponsFolder}/Weapon_Cutelo.asset");
                if (cutlass) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(cutlass), w);
                w.displayName = spec.name;
                w.holy = spec.holy;
                w.damage = spec.damage;
                w.range = spec.range;
                w.angle = spec.angle;
                // Golpe mais rápido: a animação acelera e o impacto, a espera e o combo encurtam no mesmo tanto.
                float k = Mathf.Max(0.1f, spec.animSpeed);
                w.attackAnimSpeed = k;
                w.hitDelay /= k;
                w.cooldown /= k;
                w.comboWindow /= k;
                if (spec.kind != BladeKind.Cutlass) HeavyWeapon(w, spec);
            });

        /// <summary>
        /// Machado ou espadão (E2): o tipo de arma das animações do brutamontes (3) ou do Barba-Negra (4), os três primeiros golpes dele em
        /// sequência (o impacto de cada um é o "lead" da tabela, ÷ a velocidade da animação), sem animação de sacar (vai das costas para a mão
        /// na hora) e o terceiro derrubando (no espadão, o corte duplo).
        /// </summary>
        static void HeavyWeapon(WeaponDefinition w, BladeSpec spec)
        {
            float k = Mathf.Max(0.1f, spec.animSpeed);
            bool axe = spec.kind == BladeKind.Axe;
            w.animatorWeaponType = axe ? 3 : 4;
            w.animatorVariantOffset = 0;
            w.animatorVariants = System.Array.Empty<int>();
            w.attackVariants = 3;
            w.randomVariants = false;
            w.variantHitDelays = new float[3];
            for (int i = 0; i < 3; i++) w.variantHitDelays[i] = (axe ? AxeAttacks[i].lead : GreatswordAttacks[i].lead) / k;
            w.hitDelay = w.variantHitDelays[0];
            w.cooldown = spec.cooldown;
            w.comboWindow = 1.8f;
            w.animatedDraw = false;
            w.moveSpeedWhileAttacking = 0.25f;
            w.breaksGuard = spec.breaksGuard;
            var finisher = new StrikeOverride { customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble };
            // "Slash 4": o segundo corte sai 0,80 s de clipe depois do primeiro.
            if (!axe) finisher.followUps = new[] { 0.8f / k };
            w.variantStrikes = new[] { new StrikeOverride(), new StrikeOverride(), finisher };
        }

        static ItemDefinition BladeItem(BladeSpec spec)
        {
            var item = Item($"Equip_{spec.file}", spec.name, ItemCategory.Gear, spec.rarity, Rarity.ColorOf(spec.rarity), 0f, spec.description);
            if (item.equipSlot == EquipSlot.None || !item.equipment)
            {
                item.equipSlot = EquipSlot.Blade;
                item.equipment = BladeWeapon(spec);
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        static IEnumerable<ItemDefinition> BladeItems()
        {
            foreach (var spec in BladeSpecs) yield return BladeItem(spec);
        }

        /// <summary>
        /// As lâminas no capitão: cada uma montada no lugar da mão do cutelo (mesma pegada: o cabo na origem, a lâmina para fora), desligada,
        /// e o GearWeapons sabendo qual visual é de qual arma. Também põe a tela I no HUD (se ele já existe; senão o CreateGameHud põe).
        /// </summary>
        static void AddBladeGear(GameObject character)
        {
            var combat = character.GetComponent<CharacterCombat>();
            var slot = combat ? combat.HandSlot(0) : null;
            if (!slot) return;
            var hand = slot.parent;
            var gear = character.GetComponent<GearWeapons>();
            if (!gear) gear = character.AddComponent<GearWeapons>();
            var list = new List<GearWeapons.Blade>();
            var model = character.transform.Find("Model");
            foreach (var spec in BladeSpecs)
            {
                if (spec.kind == BladeKind.Cutlass) CutlassFinisher(BladeWeapon(spec));
                else
                {
                    // Machado e espadão (passo 5 da revisão): empurram mais que o cutelo; a finalização, bem mais.
                    var heavyWeapon = BladeWeapon(spec);
                    heavyWeapon.hitPush = 3f;
                    if (heavyWeapon.variantStrikes != null && heavyWeapon.variantStrikes.Length > 2) heavyWeapon.variantStrikes[2].push = 5f;
                    EditorUtility.SetDirty(heavyWeapon);
                }
                if (spec.kind != BladeKind.Cutlass)
                {
                    // Machado e espadão: pegada própria (a dos clipes deles) e guardados nas costas.
                    var heavy = HeavyBladeOn(model, spec, out var heavyHand, out var heavyBack);
                    if (!heavy) continue;
                    if (spec.tint != Color.white || spec.glow != Color.black)
                    {
                        var heavyMat = BladeMaterial(spec);
                        foreach (var r in heavy.GetComponentsInChildren<Renderer>(true))
                        {
                            var slots = r.sharedMaterials;
                            for (int i = 0; i < slots.Length; i++) slots[i] = heavyMat;
                            r.sharedMaterials = slots;
                        }
                    }
                    heavy.gameObject.SetActive(false);
                    list.Add(new GearWeapons.Blade { weapon = BladeWeapon(spec), visual = heavy, hand = heavyHand, holster = heavyBack });
                    continue;
                }
                var root = new GameObject("Lamina_" + spec.file).transform;
                root.SetParent(slot, false);
                // Eixos do kit: lâmina em +Y, largura em X (como o cutelo, M6.5).
                if (!KitWeapon(root, spec.model, Quaternion.LookRotation(Vector3.right, Vector3.forward), spec.scale, spec.grip))
                {
                    Object.DestroyImmediate(root.gameObject);
                    continue;
                }
                UndoHandScale(root, hand);
                if (spec.tint != Color.white || spec.glow != Color.black)
                {
                    var mat = BladeMaterial(spec);
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var slots = r.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                        r.sharedMaterials = slots;
                    }
                }
                if (spec.holy) BladeFlames(root);
                root.gameObject.SetActive(false);
                list.Add(new GearWeapons.Blade { weapon = BladeWeapon(spec), visual = root });
            }
            gear.ConfigureBlades(list.ToArray());
            AddBladeDrops();
            // Tela I em forma de inventário (20/10): ícones dos itens e a vitrine do capitão.
            BakeItemIcons();
            AddLoadoutMannequin(character);
            AttachEquipmentHud();
        }

        /// <summary>
        /// Machado ou espadão na mão direita do capitão, como no brutamontes (eixo do cutelo, cabeças no plano do golpe) e no Barba-Negra
        /// (lâmina e fio medidos nos clipes do espadão, GreatswordGrip), com o lugar próprio na mão e nas costas.
        /// </summary>
        static Transform HeavyBladeOn(Transform model, BladeSpec spec, out Transform handSlot, out Transform backSlot)
        {
            handSlot = backSlot = null;
            if (!model) return null;
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            Vector3 blade, edge, along;
            float offset;
            if (spec.kind == BladeKind.Axe)
            {
                HandGrip(model, out Vector3 arm, out Vector3 thumb);
                Vector3 palm = Vector3.Cross(arm, thumb).normalized;
                blade = (0.96f * arm + 0.25f * palm + 0.11f * thumb).normalized;
                edge = 0.84f * thumb - 0.54f * palm + 0.04f * arm;
                along = arm;
                offset = 0.08f;
            }
            else
            {
                GreatswordGrip(model, out blade, out edge);
                along = blade;
                offset = 0.04f;
            }
            var root = new GameObject("Lamina_" + spec.file).transform;
            root.SetParent(hand, false);
            root.SetLocalPositionAndRotation(LocalOffset(hand, along, offset), Quaternion.LookRotation(blade, edge));
            if (!KitWeapon(root, spec.model, Quaternion.LookRotation(Vector3.right, Vector3.forward), spec.scale, spec.grip))
            {
                Object.DestroyImmediate(root.gameObject);
                return null;
            }
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_" + spec.file);
            backSlot = HeavyBackHolster(model, spec);
            return root;
        }

        /// <summary>
        /// Nas costas, na diagonal contrária à do bacamarte (cabo embaixo à direita, ponta sobre o ombro esquerdo), deitado nas costas e um
        /// pouco mais para fora que ele: os dois cabem juntos.
        /// </summary>
        static Transform HeavyBackHolster(Transform model, BladeSpec spec)
        {
            Transform chest = null;
            if (model.TryGetComponent(out Animator animator) && animator.isHuman)
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!chest) chest = Bone(model, HumanBodyBones.Chest);
            var back = new GameObject("Costas_" + spec.file).transform;
            Vector3 right = model.right, forward = model.forward;
            Vector3 blade = (Vector3.up * 0.75f - right * 0.65f).normalized;
            Vector3 edge = Vector3.Cross(forward, blade).normalized;
            // A arma é montada a partir da pegada: o meio dela fica ~0,3 m (espadão) / ~0,2 m (machado) lâmina acima.
            float half = spec.kind == BladeKind.Greatsword ? 0.3f : 0.2f;
            Vector3 center = chest.position - forward * 0.27f;
            back.SetPositionAndRotation(center - blade * half, Quaternion.LookRotation(blade, edge));
            back.SetParent(chest, true);
            MatchHandScale(back, model);
            return back;
        }

        /// <summary>
        /// Fogo de São Telmo na lâmina: chamas azuis pequenas ao longo do fio (a lâmina vai em +Z do suporte), soltas no mundo para deixar
        /// rastro no golpe. Liga e desliga com o visual da lâmina.
        /// </summary>
        static void BladeFlames(Transform blade)
        {
            var go = new GameObject("FogoDeSaoTelmo");
            go.transform.SetParent(blade, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.48f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.7f, 1f, 0.9f), new Color(0.7f, 0.95f, 1f, 0.7f));
            main.gravityModifier = -0.15f;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.03f, 0.05f, 0.62f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxMaterial("M_FX_ChamaAzul", "flame_04", additive: true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>O atlas do kit tingido (aço azulado, ouro) com um brilho fraco, um material por lâmina.</summary>
        static Material BladeMaterial(BladeSpec spec)
        {
            var mat = GetTexturedMaterial($"M_Lamina_{spec.file}", QuaterniusAtlas, 0.45f);
            if (mat.GetColor("_BaseColor") == Color.white && spec.tint != Color.white)
            {
                mat.SetColor("_BaseColor", spec.tint);
                if (spec.glow != Color.black)
                {
                    mat.SetColor("_EmissionColor", spec.glow);
                    Emissive(mat);
                }
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>Tela de equipamento (I) no objeto do HUD, ligada às armas do capitão. Roda quando os dois já existem.</summary>
        static void AttachEquipmentHud()
        {
            var hud = Object.FindAnyObjectByType<Pirates.UI.GameHud>(FindObjectsInactive.Include);
            var gear = Object.FindAnyObjectByType<GearWeapons>(FindObjectsInactive.Include);
            if (!hud || !gear || hud.GetComponent<EquipmentHUD>()) return;
            hud.gameObject.AddComponent<EquipmentHUD>().Bind(gear, AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconsFolder}/Item_Mochila.png"));
        }

        /// <summary>Seção de equipamento das tabelas de saque (só nas que ainda não têm).</summary>
        static void AddBladeDrops()
        {
            var toledo = BladeItem(BladeSpecs[0]);
            var sabre = BladeItem(BladeSpecs[1]);
            var admiral = BladeItem(BladeSpecs[2]);
            foreach (var (file, chance, wToledo, wSabre, wAdmiral) in BladeDrops)
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTable>($"{DataFolder}/{file}.asset");
                if (!table || table.gear is { Length: > 0 }) continue;
                var gear = new List<LootTable.WeightedItem>();
                if (wToledo > 0f) gear.Add(Weighted(toledo, wToledo));
                if (wSabre > 0f) gear.Add(Weighted(sabre, wSabre));
                if (wAdmiral > 0f) gear.Add(Weighted(admiral, wAdmiral));
                table.gear = gear.ToArray();
                table.gearChance = chance;
                EditorUtility.SetDirty(table);
            }
            AddHeavyDrops();
        }

        /// <summary>
        /// Os raros do E2 (machado de abordagem e montante) entram nas tabelas que já têm equipamento, se ainda não estão (o asset é do
        /// Jhonata: não mexe nos pesos que já existem). Os épicos não entram em baú: caem do brutamontes e do Barba-Negra.
        /// </summary>
        static void AddHeavyDrops()
        {
            var axe = BladeItemByFile("MachadoAbordagem");
            var sword = BladeItemByFile("Montante");
            foreach (var (file, weight) in HeavyDrops)
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTable>($"{DataFolder}/{file}.asset");
                if (!table || table.gear == null || table.gear.Length == 0) continue;
                var gear = new List<LootTable.WeightedItem>(table.gear);
                bool changed = false;
                foreach (var item in new[] { axe, sword })
                {
                    if (!item || gear.Exists(g => g.item == item)) continue;
                    gear.Add(Weighted(item, weight));
                    changed = true;
                }
                if (!changed) continue;
                table.gear = gear.ToArray();
                EditorUtility.SetDirty(table);
            }
        }

        static readonly (string table, float weight)[] HeavyDrops =
        {
            ("LootTable_BauAcampamento", 2f), ("LootTable_Contrabando", 2f), ("LootTable_PaiolForte", 2f), ("LootTable_TesouroEnterrado", 2f),
            ("LootTable_TesouroContrabandista", 2f), ("LootTable_MapaDoTesouro", 2f), ("LootTable_Brigue", 2f), ("LootTable_Cacador", 2f),
            ("LootTable_ChalupaInimiga", 1f), ("LootTable_Ermida", 1f), ("LootTable_Catacumbas", 1f),
        };

        /// <summary>O épico de um chefe a pé (E2): cai uma vez no saco de saque quando ele morre (quem já tem não ganha outro).</summary>
        static void DropsBlade(GameObject go, string file)
        {
            var sack = AssetDatabase.LoadAssetAtPath<LootSack>($"{LootPrefabsFolder}/Loot_Saco.prefab");
            var item = BladeItemByFile(file);
            if (!sack || !item || go.GetComponent<DropOnDeath>()) return;
            go.AddComponent<DropOnDeath>().Configure(sack, new[] { new ItemStack(item, 1) });
        }

        /// <summary>Facho do baú por raridade do melhor prêmio (índice = ItemRarity; comum e incomum ficam com o dourado de sempre).</summary>
        static Material[] RarityBeacons()
        {
            var beacons = new Material[5];
            beacons[(int)ItemRarity.Rare] = GetTransparentMaterial("M_LootBeacon_Raro", new Color(0.4f, 0.68f, 1f, 0.3f));
            beacons[(int)ItemRarity.Epic] = GetTransparentMaterial("M_LootBeacon_Epico", new Color(0.78f, 0.45f, 1f, 0.32f));
            beacons[(int)ItemRarity.Legendary] = GetTransparentMaterial("M_LootBeacon_Lendario", new Color(1f, 0.62f, 0.12f, 0.36f));
            return beacons;
        }
    }
}
