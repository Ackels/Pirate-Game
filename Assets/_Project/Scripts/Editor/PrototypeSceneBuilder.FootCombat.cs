using System.Linq;
using Pirates.Combat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Combate a pé do gerador (Protótipo 06, M4.3): armas (Data/Weapons), cutelo e pistola de primitivas com um lugar
    /// na mão e outro guardado (bainha na cintura esquerda, onde a mão passa no clipe de sacar; pistola no cinto),
    /// CharacterCombat, a camada "Tronco" do Animator (Avatar Mask do tronco: ataca andando) com os clipes do Mixamo,
    /// a "CorpoInteiro" sincronizada com ela (parado, golpes e pistola usam quadril e pernas do clipe), a mira
    /// procedural da pistola (CharacterAimRig) e bonecos de treino na praia. Tempos dos golpes medidos nos clipes
    /// (mão mais rápida = impacto).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string WeaponsFolder = DataFolder + "/Weapons";
        const string UpperBodyMaskPath = AnimationFolder + "/UpperBody.mask";
        /// <summary>Camada do gole (05/10): só o braço direito e a cabeça (o braço do gancho, com o papagaio, fica como está).</summary>
        const string EatLayerName = "Gole", EatMaskPath = AnimationFolder + "/BracoDireito_Cabeca.mask";
        const string AttackLayerName = "Tronco";
        const string FullBodyLayerName = "CorpoInteiro";
        // Tags dos estados do tronco: "CorpoInteiro" = parado usa o corpo todo; "Mira" = idem + braço segue a mira.
        const string FullBodyTag = "CorpoInteiro", AimTag = "Mira", MovingFullBodyTag = "CorpoInteiroAndando";
        const string ClipRoll = "Sprinting Forward Roll";
        // Chute e quedas (pacote A, passo 3). Medidos no esqueleto do capitão: Mma Kick1 com o pé esticado de 0,6 a 0,8 s; Stunned
        // cambaleia e cai sentado (quadril no chão a ~0,9 s); Knocked Down arremessado de costas (de pé até ~0,6 s, no chão a ~1,1 s);
        // Getting Up começa deitado e fica de pé a ~1,9 s.
        const string ClipKick = "Mma Kick1", ClipStumble = "Stunned", ClipBlast = "Knocked Down", ClipGetUp = "Getting Up";
        // Tempos (revisão de 09/10, para não travar o pirata no chão): chute ~1,5 s no chão (cai sentado 0,8 s + levanta rápido 0,77 s);
        // explosão nos piratas ~2,9 s (arremessado + levantar normal); no capitão ~1,3 s (arremessado rápido 0,47 s + levanta rápido).
        const float KickSpeed = 1.4f, StumbleSpeed = 1.6f, StumbleExit = 0.6f, BlastSpeed = 1.4f, BlastStart = 0.18f, GetUpSpeed = 1.5f;
        const string ClipThrow = "Throw Object";
        // Defesa (pacote A, passo 5): a guarda é o "Blocking" do espadão (0,5 s erguendo a lâmina na frente do rosto, segura o fim: a do
        // machado deixava o cutelo deitado na cintura); impacto = "Blocking 2"; quem é aparado cambaleia para trás ("React Large Gut" do
        // machado, 1,6 s, tocado no tempo do CharacterCombat.parryStun).
        const string ClipBlockIdle = "Blocking", ClipBlockHit = "Blocking 2", ClipParried = "Standing React Large Gut";
        const float BlockRaiseSpeed = 1.4f, BlockHitSpeed = 1.3f, ParriedSpeed = 1.15f;
        // Brutamontes (pacote B, passo 1): pacote do machado. Impacto (s de clipe, mão mais rápida): para baixo 0,70, horizontal 0,97,
        // revés 1,03. Golpe começa AxeLead antes do impacto (= hitDelay do machado com AttackSpeed 1).
        const string ClipAxeIdle = "Standing Idle", ClipAxeRun = "Standing Run Forward", ClipTaunt = "Standing Taunt Battlecry";
        const float AxeLead = 0.6f, TauntSpeed = 1.2f;
        // Para baixo e revés começam do início do clipe (impacto 0,7 e 1,0 s depois): golpes lentos que pegam quem rola cedo (09/10, "fácil
        // rolando"); o horizontal continua com 0,6 s.
        static readonly (string clip, float impact, float lead)[] AxeAttacks =
        {
            ("Standing Melee Attack Downward", 0.70f, 0.70f), ("Standing Melee Attack Horizontal", 0.97f, AxeLead),
            ("Standing Melee Attack Backhand", 1.03f, 1.0f),
        };
        const float ThrowStart = 1.3f, ThrowEnd = 3.4f, ThrowSpeed = 1.8f;
        const string ClipLever = "Pulling Lever";
        const float LeverEnd = 4.6f, LeverSpeed = 1.8f;
        const float QuickBlastSpeed = 1.8f, QuickBlastExit = 0.48f, QuickGetUpSpeed = 2f, QuickGetUpStart = 0.15f;
        /// <summary>Igual ao padrão do FootCharacter.dodgeSeconds (a animação dura o mesmo que o rolamento).</summary>
        const float DodgeSeconds = 0.8f;

        // Clipes do Mixamo (arquivos "X Bot@<nome>.fbx") e como tocam. Medidos nos clipes: golpe para fora acerta em 0,87 s,
        // para dentro em 1,30 s; o saque pega a espada na cintura em 0,60 s; o guardar solta em 1,27 s; o tiro sai em ~0,37 s.
        const string ClipSlashOut = "Stable Sword Outward Slash", ClipSlashIn = "Stable Sword Inward Slash";
        const string ClipDraw = "Withdrawing Sword", ClipSheath = "Sheathing Sword", ClipRunSword = "Run With Sword";
        const string ClipShoot = "Shooting", ClipGunplay = "Gunplay", ClipPistolIdle = "Pistol Idle", ClipPistolAim = "Pistol Aim";
        const string ClipDrinking = "Drinking";
        /// <summary>Gole: o trecho de 4,1 s do clipe em ~1,9 s (FoodEater.eatSeconds).</summary>
        const float EatSpeed = 2.15f;
        const float DrawGrabClipTime = 0.60f;
        const float SlashSpeed = 1.5f, DrawSpeed = 1.3f, SheathSpeed = 1.5f, ShootSpeed = 1.2f;
        /// <summary>
        /// 3º golpe do cutelo do capitão (revisão das animações, 21/10): o golpe de cima do pacote do machado ("Standing Melee Attack
        /// Downward", impacto aos 0,70 s do clipe), 1,3× (× a velocidade da lâmina), começando <see cref="FinisherLead"/> s de clipe antes do
        /// impacto (~0,42 s de preparo, mais que os 0,3 s dos cortes: dá para ver vindo) e voltando ao pronto 0,6 s de clipe depois. AttackVariant 6
        /// (2 e 3 são a adaga da duelista, que também é WeaponType 0).
        /// </summary>
        const float FinisherSpeed = 1.3f, FinisherLead = 0.55f, FinisherFollow = 0.6f;
        const int CutlassFinisherVariant = 6;
        /// <summary>Aparar: do corte para fora, de 0,72 s (logo antes da mão mais rápida, 0,87 s) a 1,2 s do clipe, 1,6× (~0,3 s).</summary>
        const float ParrySwingStart = 0.72f, ParrySwingEnd = 1.2f, ParrySwingSpeed = 1.6f;

        // Levar golpe (sorteados): ficam no lugar (quadril anda < 0,2 m) e terminam de pé. (clipe, início em s, velocidade)
        // "Hit On Side Of Head" ficou de fora: o corpo anda 6 m no clipe (cambaleio correndo), deslizaria no lugar.
        static readonly (string clip, float start, float speed)[] HitClips =
        {
            ("Head Hit", 0f, 1.25f), ("Hit To Body", 0f, 1.25f), ("Rib Hit", 0f, 1.2f), ("Stomach Hit", 0f, 1.25f),
        };
        // Morrer (sorteados), começando perto da queda: medido, o quadril chega ao chão aos 2,4 s (para trás), 4,4 s
        // ("Dying" cambaleia 2,4 s antes) e 1,6 s (ombro).
        static readonly (string clip, float start, float speed)[] DeathClips =
        {
            ("Dying Backwards", 0.6f, 1.3f), ("Dying", 2.2f, 1.3f), ("Shoulder Hit And Fall", 0.2f, 1.2f),
        };

        static void AddFootCombat(FootCharacter character, Transform model, Material iron, Material wood, Material brass)
        {
            EnsureFolder(DataFolder, "Weapons");
            var cutlass = LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Cutelo.asset", w =>
            {
                w.displayName = "Cutelo";
                w.kind = WeaponKind.Melee;
                w.animatorWeaponType = 0;
                w.damage = 25f;
                w.range = 2.2f;
                w.angle = 110f;
                w.hitDelay = 0.3f;            // (0,87 − 0,41 s de início) / 1,5 e (1,30 − 0,88) / 1,5 ≈ 0,3 s
                w.cooldown = 0.6f;
                w.attackVariants = 2;
                w.comboWindow = 1.1f;
                w.animatedDraw = true;
                w.drawGrabTime = DrawGrabClipTime / DrawSpeed;
                w.drawDuration = 0.95f;
                w.holsterReleaseTime = 1.27f / SheathSpeed;
                w.holsterDuration = 1.05f;
                // Quase para no golpe: o corpo inteiro do clipe (postura de luta) não desliza.
                w.moveSpeedWhileAttacking = 0.15f;
            });
            CutlassFinisher(cutlass);
            var pistol = LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Pistola.asset", w =>
            {
                w.displayName = "Pistola de pederneira";
                w.kind = WeaponKind.Firearm;
                w.animatorWeaponType = 1;
                w.damage = 45f;
                w.range = 35f;
                w.angle = 3f;
                w.hitDelay = 0.37f / ShootSpeed;
                w.cooldown = 0.5f;
                w.shotsPerReload = 1;
                w.reloadTime = 2.5f;
                w.aimPoseSeconds = 1.5f;
                w.moveSpeedWhileAttacking = 0.3f;
                w.aimedShotDelay = 0.04f;
                w.aimedSpread = 0.3f;
                w.aimMoveSpeed = 0.75f;
                w.recoilDegrees = 14f;
            });

            var blunderbuss = BlunderbussWeapon();

            var sword = CutlassOn(model, iron, wood, brass, out Transform swordHand, out Transform swordHolster);
            var gun = PistolOn(model, iron, wood, brass, out Transform muzzle, out Transform gunHand, out Transform gunHolster);
            var bigGun = BlunderbussOn(model, iron, wood, out Transform bigMuzzle, out Transform bigGunHand, out Transform bigGunHolster);
            var tracer = ShotTracer(character.transform);
            // Mira (botão direito, 05/10: pedido do Jhonata): laser vermelho forte da boca até onde a bala bate, com brilho largo em
            // volta e um ponto no fim; travado num alvo, um anel dourado contorna o corpo dele (a linha branca fina quase não se via).
            var laserColor = new Color(1f, 0.12f, 0.08f, 1f);
            var aimLine = AimLine(character.transform, laserColor, 0.065f);
            var glow = AimLine(character.transform, laserColor, 0.15f);
            glow.name = "BrilhoDoLaser";

            // Começa com o cutelo na mão, a pistola no cinto e o bacamarte nas costas (o CharacterCombat refaz isso ao iniciar).
            gun.SetParent(gunHolster, false);
            bigGun.SetParent(bigGunHolster, false);
            var combat = character.gameObject.AddComponent<CharacterCombat>();
            combat.Configure(new[] { cutlass, pistol, blunderbuss }, new[] { sword, gun, bigGun },
                new[] { swordHand, gunHand, bigGunHand }, new[] { swordHolster, gunHolster, bigGunHolster }, muzzle, tracer);
            combat.SetMuzzles(new[] { null, muzzle, bigMuzzle });
            var rig = AddAimRig(model, combat, muzzle, aimLine);
            if (!rig) return;
            rig.ConfigureReticle(Reticle(character.transform), glow, laserColor, new Color(1f, 0.8f, 0.25f, 1f));
            RifleAimYaw(model, out float rifleUpper, out float rifleFull);
            rig.ConfigureTwoHanded(rifleUpper, rifleFull);
        }

        /// <summary>Anel da mira do jogador (desenhado pelo CharacterAimRig no ponto mirado).</summary>
        static LineRenderer Reticle(Transform parent)
        {
            var ring = new GameObject("AnelDaMira").AddComponent<LineRenderer>();
            ring.transform.SetParent(parent, false);
            ring.useWorldSpace = true;
            ring.loop = true;
            // Grosso: pequeno, vira o ponto do laser; grande, o anel em volta do alvo travado.
            ring.widthMultiplier = 0.07f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.sharedMaterial = GetTransparentMaterial("M_AimLine", Color.white);
            ring.enabled = false;
            return ring;
        }

        /// <summary>Pistola de primitivas na mão direita do modelo, com o lugar na mão e o do cinto.</summary>
        static Transform PistolOn(Transform model, Material iron, Material wood, Material brass, out Transform muzzle,
            out Transform handSlot, out Transform holsterSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            Transform hips = Bone(model, HumanBodyBones.Hips);
            HandGrip(model, out Vector3 arm, out Vector3 thumb);
            var gun = Pistol(hand, arm, thumb, iron, wood, brass, out muzzle);
            handSlot = SlotFor(gun, hand, "Mao_Pistola");
            holsterSlot = BeltHolster(hips, model);
            return gun;
        }

        /// <summary>Rastro rápido do tiro (linha da boca até onde a bala parou).</summary>
        static LineRenderer ShotTracer(Transform parent)
        {
            var tracer = new GameObject("RastroDoTiro").AddComponent<LineRenderer>();
            tracer.transform.SetParent(parent, false);
            tracer.useWorldSpace = true;
            tracer.widthMultiplier = 0.04f;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.sharedMaterial = GetTransparentMaterial("M_AimLine", Color.white);
            tracer.startColor = new Color(1f, 0.9f, 0.6f, 0.9f);
            tracer.endColor = new Color(1f, 0.9f, 0.6f, 0.2f);
            tracer.enabled = false;
            return tracer;
        }

        /// <summary>Linha fina da mira segurada, da boca da pistola até o alvo, sumindo na ponta.</summary>
        static LineRenderer AimLine(Transform parent, Color color, float width = 0.015f)
        {
            var line = new GameObject("LinhaDeMira").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.sharedMaterial = GetTransparentMaterial("M_AimLine", Color.white);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.enabled = false;
            return line;
        }

        /// <summary>Mira procedural da pistola (braço e tronco seguem o alvo), com a pose medida no clipe.</summary>
        static CharacterAimRig AddAimRig(Transform model, CharacterCombat combat, Transform muzzle, LineRenderer aimLine)
        {
            if (!model || !model.TryGetComponent(out Animator animator) || !animator.isHuman) return null;
            AimPoseArmYaw(model, ClipPistolAim, out float upperBodyYaw, out float fullBodyYaw);
            var rig = model.gameObject.AddComponent<CharacterAimRig>();
            rig.Configure(combat, muzzle, aimLine, upperBodyYaw, fullBodyYaw);
            return rig;
        }

        /// <summary>Pistola dos piratas: menos dano e precisão, recarga mais longa. Mirando (a IA sempre mira) atira na hora.</summary>
        static WeaponDefinition EnemyPistol() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Pistola_Pirata.asset", w =>
        {
            w.displayName = "Pistola de pirata";
            w.kind = WeaponKind.Firearm;
            w.animatorWeaponType = 1;
            w.damage = 18f;
            w.range = 26f;
            w.angle = 4f;
            w.hitDelay = 0.37f / ShootSpeed;
            w.cooldown = 0.5f;
            w.shotsPerReload = 1;
            w.reloadTime = 3f;
            w.aimPoseSeconds = 1.5f;
            w.moveSpeedWhileAttacking = 0.3f;
            w.aimedShotDelay = 0.04f;
            w.aimedSpread = 0.6f;
            w.aimMoveSpeed = 0.6f;
            w.recoilDegrees = 14f;
        });

        /// <summary>
        /// Bacamarte dos piratas (inimigo novo, passo 3 de 09/10): 6 chumbos × 7 = 42 colado (o capitão tem 100), leque de 18°, só até 13 m,
        /// recarga de 4,5 s: perigoso de perto, fácil de punir na recarga. Mesmo modelo e animações do bacamarte do jogador.
        /// </summary>
        static WeaponDefinition EnemyBlunderbuss() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Bacamarte_Pirata.asset", w =>
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(BlunderbussWeapon()), w);
            w.displayName = "Bacamarte de pirata";
            w.damage = 7f;
            w.pellets = 6;
            w.pelletSpread = 18f;
            w.range = 13f;
            w.reloadTime = 4.5f;
        });

        /// <summary>Arma de quem está no posto: "Bacamarteiro_..." leva o bacamarte; os outros, pistola (atirador) ou cutelo.</summary>
        static WeaponDefinition PostWeapon(string name, bool gunner, WeaponDefinition cutlass, WeaponDefinition pistol) =>
            IsBlackGuard(name) ? (IsMusketeer(name) ? BlackGuardMusket() : BlackGuardSabre()) :
            name.StartsWith("Bacamarteiro") ? EnemyBlunderbuss() : IsChief(name) ? ChiefSword() : IsBrute(name) ? BruteAxe() : IsDuelist(name) ? DuelistDagger() : gunner ? pistol : cutlass;

        /// <summary>Cutelo dos piratas inimigos: mais fraco e com o golpe mais lento (dá para ver vindo e reagir).</summary>
        static WeaponDefinition EnemyCutlass() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Cutelo_Pirata.asset", w =>
        {
            const float animSpeed = 0.7f;
            w.displayName = "Cutelo de pirata";
            w.kind = WeaponKind.Melee;
            w.animatorWeaponType = 0;
            w.damage = 12f;
            w.range = 2f;
            w.angle = 100f;
            w.attackAnimSpeed = animSpeed;
            w.hitDelay = 0.3f / animSpeed;   // mesmo impacto do cutelo do jogador, com a animação a 70%
            w.cooldown = 0.6f / animSpeed;
            w.attackVariants = 2;
            w.comboWindow = 1.1f / animSpeed;
            w.animatedDraw = true;
            w.drawGrabTime = DrawGrabClipTime / DrawSpeed;
            w.drawDuration = 0.95f;
            w.holsterReleaseTime = 1.27f / SheathSpeed;
            w.holsterDuration = 1.05f;
            w.moveSpeedWhileAttacking = 0.15f;
        });

        /// <summary>Cutelo de primitivas na mão direita do modelo, com o lugar na mão e a bainha na cintura esquerda.</summary>
        static Transform CutlassOn(Transform model, Material iron, Material wood, Material brass, out Transform handSlot, out Transform holsterSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            Transform hips = Bone(model, HumanBodyBones.Hips);
            HandGrip(model, out Vector3 arm, out Vector3 thumb);
            var sword = Cutlass(hand, arm, thumb, iron, wood, brass);
            handSlot = SlotFor(sword, hand, "Mao_Cutelo");
            holsterSlot = SwordHolster(model, hips, handSlot);
            return sword;
        }

        /// <summary>
        /// Arma do Quaternius Pirate Kit como visual de <paramref name="root"/>: girada para os eixos da nossa empunhadura,
        /// em escala, com o ponto de pegada (no espaço do modelo do kit) na origem. false se o kit não está no projeto.
        /// </summary>
        static bool KitWeapon(Transform root, string model, Quaternion rotation, float scale, Vector3 grip)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{model}.fbx");
            if (!asset) return false;
            var pivot = new GameObject("Modelo").transform;
            pivot.SetParent(root, false);
            pivot.SetLocalPositionAndRotation(-(rotation * grip) * scale, rotation);
            pivot.localScale = Vector3.one * scale;
            // A raiz do FBX já vem girada e em escala 100 (Blender): mantém isso dentro do pivô.
            var go = (GameObject)Object.Instantiate(asset, pivot, false);
            go.name = model;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = QuaterniusMaterial;
                r.sharedMaterials = slots;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            return true;
        }

        /// <summary>
        /// Cópia temporária do modelo humanoide (mesmo esqueleto e avatar), parada na origem olhando para +Z: as medidas
        /// nos clipes (empunhadura, bainha, braço da mira) saem do corpo de cada personagem, não de um modelo fixo.
        /// </summary>
        static GameObject MeasureCopy(Transform model)
        {
            if (!model || !model.TryGetComponent(out Animator animator) || !animator.isHuman) return null;
            var temp = Object.Instantiate(model.gameObject);
            temp.hideFlags = HideFlags.DontSave;
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return temp;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static Transform Bone(Transform model, HumanBodyBones bone)
        {
            // Personagens do kit: bainha e cinto vão na referência do quadril alinhada com a do Starter (eixos x direita,
            // y cima, z frente na pose T), não no osso do kit (eixos do Blender).
            if (bone == HumanBodyBones.Hips && model && FindChild(model, HipsReference) is { } hipsRef) return hipsRef;
            if (model && model.TryGetComponent(out Animator animator) && animator.isHuman)
            {
                var t = animator.GetBoneTransform(bone);
                if (t) return t;
            }
            // Cápsula provisória (sem esqueleto): pontos fixos no corpo.
            var anchor = new GameObject(bone.ToString()).transform;
            anchor.SetParent(model, false);
            anchor.localPosition = bone == HumanBodyBones.RightHand ? new Vector3(0.3f, 1.1f, 0.25f) : new Vector3(0f, 0.95f, 0f);
            return anchor;
        }

        /// <summary>Cria o lugar da arma na mão (com a pose em que ela foi montada) e põe a arma nele.</summary>
        static Transform SlotFor(Transform weapon, Transform hand, string name)
        {
            var slot = new GameObject(name).transform;
            slot.SetParent(hand, false);
            slot.SetLocalPositionAndRotation(weapon.localPosition, weapon.localRotation);
            weapon.SetParent(slot, false);
            weapon.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return slot;
        }

        /// <summary>
        /// Bainha do cutelo na cintura esquerda: o cabo fica onde a mão pega a espada no clipe de sacar (e a solta no
        /// de guardar), vista do quadril; a lâmina pende para baixo e para trás. Na troca a espada assenta da bainha
        /// para a mão (e de volta) em ~0,15 s (CharacterCombat), sem estalo.
        /// </summary>
        static Transform SwordHolster(Transform model, Transform hips, Transform handSlot)
        {
            var sheath = new GameObject("Bainha_Cutelo").transform;
            sheath.SetParent(hips, false);
            // Lâmina na bainha, no espaço do quadril (x direita, y cima, z frente): para a esquerda, baixo e trás.
            var blade = new Vector3(-0.45f, -0.75f, -0.5f).normalized;
            var clip = MixamoClip(ClipDraw);
            Vector3 grip = new(-0.1f, 0.04f, 0.12f);
            var temp = clip ? MeasureCopy(model) : null;
            if (temp)
            {
                var anim = temp.GetComponent<Animator>();
                clip.SampleAnimation(temp, DrawGrabClipTime);
                // O mesmo osso de quadril em que a bainha vai (no kit, a referência alinhada ao quadril do Starter).
                var tHips = FindChild(temp.transform, hips.name) ?? anim.GetBoneTransform(HumanBodyBones.Hips);
                var tHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                grip = tHips.InverseTransformPoint(tHand.TransformPoint(handSlot.localPosition));
                Object.DestroyImmediate(temp);
            }
            sheath.SetLocalPositionAndRotation(grip, Quaternion.LookRotation(blade, Vector3.forward));
            return sheath;
        }

        /// <summary>
        /// Pistola enfiada na faixa, do lado direito do quadril: deitada, cabo para a frente e cano para trás, um pouco para baixo, o cão
        /// para cima. Em pé (cano para baixo) ela quase chegava ao chão: o quadril do capitão fica a ~0,5 m (05/10, foto do Jhonata).
        /// </summary>
        static Transform BeltHolster(Transform hips, Transform model)
        {
            var belt = new GameObject("Cinto_Pistola").transform;
            Vector3 right = model ? model.right : Vector3.right, forward = model ? model.forward : Vector3.forward;
            // Eixos da pistola: +X = cano, +Y = cão (como na mão).
            Vector3 barrel = (-forward * 0.9f + Vector3.down * 0.35f + right * 0.1f).normalized;
            Vector3 top = Vector3.ProjectOnPlane(Vector3.up, barrel).normalized;
            belt.SetPositionAndRotation(hips.position + right * 0.2f + forward * 0.14f + Vector3.up * 0.04f,
                Quaternion.LookRotation(Vector3.Cross(barrel, top), top));
            belt.SetParent(hips, true);
            MatchHandScale(belt, model);
            return belt;
        }

        /// <summary>
        /// Lugar de guardar criado no mundo (escala 1) e depois posto num osso: a arma muda de lugar mantendo a escala local
        /// (CharacterCombat), então o lugar precisa da mesma escala no mundo que o da mão. Os ossos do kit têm escala 100 (FBX em cm):
        /// sem isto a pistola no cinto ficava com 1 cm (sumia) até 05/10.
        /// </summary>
        static void MatchHandScale(Transform slot, Transform model)
        {
            var hand = Bone(model, HumanBodyBones.RightHand);
            if (!hand || !slot.parent) return;
            slot.localScale = Vector3.one * (hand.lossyScale.x / Mathf.Max(0.0001f, slot.parent.lossyScale.x));
        }

        /// <summary>
        /// Eixos da mão direita no espaço do osso da mão: "arm" (antebraço → dedos) e "thumb" (lado do polegar).
        /// O modelo não tem ossos de dedo mapeados, então vêm do clipe "Pistol Aim" do Mixamo: nele o antebraço aponta
        /// para o alvo e o polegar fica para cima. Valem para qualquer pose (são do osso, não do mundo).
        /// </summary>
        static void HandGrip(Transform model, out Vector3 arm, out Vector3 thumb)
        {
            // Sem o clipe: eixos típicos de um osso de mão humanoide (dedos em +X local, polegar em +Z).
            arm = Vector3.right;
            thumb = Vector3.forward;
            var clip = MixamoClip(ClipPistolAim);
            var temp = clip ? MeasureCopy(model) : null;
            if (!temp) return;

            var anim = temp.GetComponent<Animator>();
            clip.SampleAnimation(temp, 1f);
            var tHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var tFore = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Vector3 armWorld = (tHand.position - tFore.position).normalized;
            Vector3 thumbWorld = Vector3.ProjectOnPlane(Vector3.up, armWorld).normalized;
            var inverse = Quaternion.Inverse(tHand.rotation);
            arm = inverse * armWorld;
            thumb = inverse * thumbWorld;
            Object.DestroyImmediate(temp);
        }

        /// <summary>
        /// Para onde o braço aponta na pose de mira do clipe (graus a partir da frente do personagem, negativo = esquerda):
        /// com o corpo inteiro (o quadril do clipe fica de lado) e só com o tronco (quadril da locomoção, de frente).
        /// O personagem vira o contrário disso ao mirar, e o braço já sai apontado para o alvo.
        /// </summary>
        static void AimPoseArmYaw(Transform model, string clipName, out float upperBody, out float fullBody)
        {
            upperBody = -45f;
            fullBody = -20f;
            var clip = MixamoClip(clipName);
            var temp = clip ? MeasureCopy(model) : null;
            if (!temp) return;

            var anim = temp.GetComponent<Animator>();
            clip.SampleAnimation(temp, 1f);
            var tHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var tFore = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var tHips = anim.GetBoneTransform(HumanBodyBones.Hips);
            float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float arm = Yaw(tHand.position - tFore.position);
            fullBody = arm;
            upperBody = Mathf.DeltaAngle(Yaw(tHips.forward), arm);
            Object.DestroyImmediate(temp);
        }

        /// <summary>
        /// Cutelo no punho (empunhadura de sabre: lâmina seguindo a linha da mão). Direções procuradas nos clipes, na
        /// base (polegar, palma, antebraço) da mão: a lâmina que melhor sai para fora do corpo no impacto dos dois golpes,
        /// no fim do saque e correndo é 0,96 antebraço + 0,25 palma + 0,11 polegar (para fora 0,85–0,99); o fio (+Y) que
        /// vai na frente do movimento da mão nos golpes é 0,84 polegar − 0,54 palma (0,92–0,98). A medida antiga
        /// (lâmina = −polegar) só acertava a bainha: correndo e no golpe de volta a lâmina apontava para o corpo.
        /// </summary>
        static Transform Cutlass(Transform hand, Vector3 arm, Vector3 thumb, Material iron, Material wood, Material brass)
        {
            var root = new GameObject("Cutelo").transform;
            root.SetParent(hand, false);
            Vector3 palm = Vector3.Cross(arm, thumb).normalized;
            Vector3 blade = (0.96f * arm + 0.25f * palm + 0.11f * thumb).normalized;
            Vector3 edge = 0.84f * thumb - 0.54f * palm + 0.04f * arm;
            root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.08f), Quaternion.LookRotation(blade, edge));
            // Cutelo do Quaternius (M6.5): no kit a lâmina sobe em +Y, a largura (fio curvo) é X e o cabo fica na origem.
            if (KitWeapon(root, "Weapon_Cutlass", Quaternion.LookRotation(Vector3.right, Vector3.forward), 0.7f, Vector3.zero))
            {
                UndoHandScale(root, hand);
                return root;
            }
            Part(PrimitiveType.Cylinder, "Cabo", root, Vector3.zero, new Vector3(0.035f, 0.07f, 0.035f), Quaternion.Euler(90f, 0f, 0f), wood);
            Part(PrimitiveType.Cube, "Guarda", root, new Vector3(0f, 0f, 0.08f), new Vector3(0.14f, 0.025f, 0.03f), Quaternion.identity, brass);
            Part(PrimitiveType.Cube, "Lamina", root, new Vector3(0f, 0f, 0.43f), new Vector3(0.012f, 0.045f, 0.68f), Quaternion.identity, iron);
            Part(PrimitiveType.Cube, "Ponta", root, new Vector3(0f, 0.01f, 0.79f), new Vector3(0.012f, 0.03f, 0.06f), Quaternion.Euler(20f, 0f, 0f), iron);
            UndoHandScale(root, hand);
            return root;
        }

        const float PistolScale = 0.45f;

        /// <summary>Pistola no punho: cano (+X) seguindo o antebraço, cão (+Y) do lado do polegar, coronha para baixo.</summary>
        static Transform Pistol(Transform hand, Vector3 arm, Vector3 thumb, Material iron, Material wood, Material brass, out Transform muzzle)
        {
            var root = new GameObject("Pistola").transform;
            root.SetParent(hand, false);
            root.SetLocalPositionAndRotation(LocalOffset(hand, arm, 0.06f), Quaternion.LookRotation(Vector3.Cross(arm, thumb), thumb));
            // Pistola do Quaternius (M6.5): no kit o cano aponta para -X, o cão para +Y e a coronha desce perto de x 0,15.
            var kitRotation = Quaternion.Euler(0f, 180f, 0f);
            var kitGrip = new Vector3(0.15f, -0.12f, 0f);
            // 0,3 (37 cm) quase não aparecia na câmera do jogo (05/10, pedido do Jhonata): 0,45 = ~56 cm, de pistola de abordagem.
            const float kitScale = PistolScale;
            if (KitWeapon(root, "Weapon_Pistol", kitRotation, kitScale, kitGrip))
            {
                muzzle = new GameObject("Boca").transform;
                muzzle.SetParent(root, false);
                muzzle.SetLocalPositionAndRotation(kitRotation * (new Vector3(-1.03f, 0.25f, 0f) - kitGrip) * kitScale,
                    Quaternion.LookRotation(Vector3.right, Vector3.up));
                UndoHandScale(root, hand);
                return root;
            }
            Part(PrimitiveType.Cube, "Coronha", root, new Vector3(0f, -0.05f, 0f), new Vector3(0.05f, 0.12f, 0.035f), Quaternion.Euler(0f, 0f, 20f), wood);
            Part(PrimitiveType.Cylinder, "Cano", root, new Vector3(0.17f, 0.02f, 0f), new Vector3(0.03f, 0.16f, 0.03f), Quaternion.Euler(0f, 0f, 90f), iron);
            Part(PrimitiveType.Cube, "Cao", root, new Vector3(-0.01f, 0.05f, 0f), new Vector3(0.03f, 0.03f, 0.015f), Quaternion.identity, brass);
            muzzle = new GameObject("Boca").transform;
            muzzle.SetParent(root, false);
            // Z da boca = cano (a mira procedural aponta este eixo para o alvo).
            muzzle.SetLocalPositionAndRotation(new Vector3(0.34f, 0.02f, 0f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            UndoHandScale(root, hand);
            return root;
        }

        /// <summary>Deslocamento de <paramref name="meters"/> ao longo de um eixo local da mão, descontando a escala do osso.</summary>
        static Vector3 LocalOffset(Transform hand, Vector3 axis, float meters)
        {
            float scale = Mathf.Max(0.0001f, hand.lossyScale.x);
            return axis.normalized * (meters / scale);
        }

        /// <summary>Armas em tamanho real mesmo se o osso tiver escala (FBX importado em cm, por exemplo).</summary>
        static void UndoHandScale(Transform weapon, Transform hand)
        {
            float scale = Mathf.Max(0.0001f, hand.lossyScale.x);
            weapon.localScale = Vector3.one / scale;
        }

        /// <summary>
        /// Camada "Tronco" (Override + Avatar Mask do tronco), refeita a cada geração:
        /// cutelo = EspadaPronta ↔ CorrerComEspada, combo Golpe1/Golpe2, Sacar/Guardar;
        /// pistola = PistolaParada ↔ PistolaMirando (botão direito, ou logo depois do tiro), Tiro → Recarga (gira a pistola).
        /// Parâmetros: WeaponType, Attack + AttackVariant, Equip, Holster, Reloading, Aiming (e o Speed da locomoção).
        /// Mais a "CorpoInteiro": cópia sincronizada sem máscara, com peso pelo CharacterAnimatorDriver. A máscara do
        /// tronco não leva o quadril: nos golpes o clipe gira o quadril até 80° e abaixa 20 cm, e só com o tronco a coluna
        /// torcia sobre as pernas retas (ficava torto). Parado, a ação usa o corpo todo; andando, só o tronco.
        /// </summary>
        static void EnsureAttackLayer(AnimatorController controller)
        {
            AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "WeaponType", AnimatorControllerParameterType.Int);
            AddParameter(controller, "AttackVariant", AnimatorControllerParameterType.Int);
            AddParameter(controller, "Equip", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Holster", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Reloading", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Aiming", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "PoseTime", AnimatorControllerParameterType.Float);
            AddParameter(controller, "AttackSpeed", AnimatorControllerParameterType.Float);
            AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Revive", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "HitVariant", AnimatorControllerParameterType.Int);
            AddParameter(controller, "DeathVariant", AnimatorControllerParameterType.Int);
            AddParameter(controller, "Eat", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Dodge", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Kick", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Knock", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "KnockVariant", AnimatorControllerParameterType.Int);
            AddParameter(controller, "Down", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Block", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "BlockHit", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Parried", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "Taunt", AnimatorControllerParameterType.Trigger);

            var parameters = controller.parameters;
            foreach (var p in parameters)
            {
                if (p.name == "PoseTime") p.defaultFloat = 0.95f;
                if (p.name == "AttackSpeed") p.defaultFloat = 1f;
            }
            controller.parameters = parameters;

            foreach (var name in new[] { EatLayerName, FullBodyLayerName, AttackLayerName })
            {
                int old = System.Array.FindIndex(controller.layers, l => l.name == name);
                if (old >= 0) controller.RemoveLayer(old);
            }
            controller.AddLayer(AttackLayerName);
            var layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].defaultWeight = 1f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].avatarMask = UpperBodyMask();
            controller.layers = layers;
            var sm = controller.layers[index].stateMachine;

            var empty = sm.AddState("Vazio", new Vector3(300f, 0f));
            sm.defaultState = empty;
            var run = State(sm, "CorrerComEspada", ClipRunSword, 1f, new Vector3(300f, -120f));
            // Cutelo na mão, parado ou andando: pose de "pronto" congelada no fim do saque (lâmina baixa à frente).
            var ready = State(sm, "EspadaPronta", ClipDraw, 1f, new Vector3(0f, -60f));
            ready.timeParameterActive = true;
            ready.timeParameter = "PoseTime";
            var slash1 = State(sm, "Golpe1", ClipSlashOut, SlashSpeed, new Vector3(600f, -60f));
            var slash2 = State(sm, "Golpe2", ClipSlashIn, SlashSpeed, new Vector3(600f, 20f));
            var draw = State(sm, "Sacar", ClipDraw, DrawSpeed, new Vector3(600f, 100f));
            var sheath = State(sm, "Guardar", ClipSheath, SheathSpeed, new Vector3(600f, 180f));
            var idle = State(sm, "PistolaParada", ClipPistolIdle, 1f, new Vector3(0f, 120f));
            var aim = State(sm, "PistolaMirando", ClipPistolAim, 1f, new Vector3(0f, 220f));
            var shoot = State(sm, "Tiro", ClipShoot, ShootSpeed, new Vector3(300f, 300f));
            var reload = State(sm, "Recarga", ClipGunplay, 1f, new Vector3(0f, 320f));
            foreach (var st in new[] { slash1, slash2, draw, sheath, idle, reload }) st.tag = FullBodyTag;
            aim.tag = AimTag;
            shoot.tag = AimTag;
            // Velocidade do ataque vem da arma (piratas golpeiam mais devagar): velocidade do estado × AttackSpeed.
            foreach (var st in new[] { slash1, slash2, shoot })
            {
                st.speedParameterActive = true;
                st.speedParameter = "AttackSpeed";
            }
            // (o bacamarte, WeaponType 2, ganha os mesmos estados mais abaixo: AddFirearmStates)

            // Ataques e trocas: de qualquer estado, pelo trigger. O fim do golpe 1 é quase a pose do começo do golpe 2
            // (medido: quadril −6°/−17°, peito −96°/−100°), então o combo emenda com uma mistura curta.
            Any(sm, slash1, 0.2f, 0.12f, ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 0), ("AttackVariant", AnimatorConditionMode.Equals, 0));
            Any(sm, slash2, 0.4f, 0.12f, ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 0), ("AttackVariant", AnimatorConditionMode.Equals, 1));
            var (finisherName, finisherImpact, _) = AxeAttacks[0];
            var finisherClip = MixamoClip(finisherName);
            if (finisherClip)
            {
                var slash3 = State(sm, "Golpe3", finisherName, FinisherSpeed, new Vector3(600f, -140f));
                slash3.tag = FullBodyTag;
                slash3.speedParameterActive = true;
                slash3.speedParameter = "AttackSpeed";
                Any(sm, slash3, Mathf.Max(0f, finisherImpact - FinisherLead) / finisherClip.length, 0.12f, ("Attack", AnimatorConditionMode.If, 0),
                    ("WeaponType", AnimatorConditionMode.Equals, 0), ("AttackVariant", AnimatorConditionMode.Equals, CutlassFinisherVariant));
                Exit(slash3, ready, Mathf.Min(0.95f, (finisherImpact + FinisherFollow) / finisherClip.length), 0.3f);
            }
            Any(sm, shoot, 0f, 0.1f, ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 1));
            Any(sm, draw, 0f, 0.15f, ("Equip", AnimatorConditionMode.If, 0));
            Any(sm, sheath, 0f, 0.15f, ("Holster", AnimatorConditionMode.If, 0));

            Exit(slash1, ready, 0.7f, 0.3f);
            Exit(slash2, ready, 0.85f, 0.3f);
            Exit(draw, ready, 0.85f, 0.25f);
            Exit(sheath, empty, 0.9f, 0.25f);

            // Cutelo na mão: pose de pronto; correndo, braços de corrida segurando a espada.
            Go(empty, ready, 0.25f, ("WeaponType", AnimatorConditionMode.Equals, 0), ("Speed", AnimatorConditionMode.Less, 4.5f));
            Go(empty, run, 0.25f, ("WeaponType", AnimatorConditionMode.Equals, 0), ("Speed", AnimatorConditionMode.Greater, 4.5f));
            Go(ready, run, 0.25f, ("Speed", AnimatorConditionMode.Greater, 4.5f));
            Go(run, ready, 0.25f, ("Speed", AnimatorConditionMode.Less, 4f));
            Go(ready, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 0));
            Go(run, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 0));

            // Pistola na mão: pose parada, ou de mira logo depois do tiro / durante a recarga.
            Go(empty, idle, 0.2f, ("WeaponType", AnimatorConditionMode.Equals, 1), ("Aiming", AnimatorConditionMode.IfNot, 0));
            Go(empty, aim, 0.15f, ("WeaponType", AnimatorConditionMode.Equals, 1), ("Aiming", AnimatorConditionMode.If, 0));
            Go(idle, aim, 0.15f, ("Aiming", AnimatorConditionMode.If, 0));
            Go(aim, idle, 0.3f, ("Aiming", AnimatorConditionMode.IfNot, 0));
            Go(idle, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 1));
            Go(aim, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 1));

            var toReload = shoot.AddTransition(reload);
            toReload.hasExitTime = true;
            toReload.exitTime = 0.65f;
            toReload.duration = 0.2f;
            toReload.AddCondition(AnimatorConditionMode.If, 0, "Reloading");
            Exit(shoot, aim, 0.8f, 0.2f);
            // Tiro mirando não passa pelo "Tiro" (o braço já está erguido): a recarga sai da própria pose de mira.
            Go(aim, reload, 0.2f, ("Reloading", AnimatorConditionMode.If, 0));
            Go(reload, aim, 0.25f, ("Reloading", AnimatorConditionMode.IfNot, 0));
            Go(reload, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 1));

            // Bacamarte: sem "Rifle Idle", parado fica na pose de mira de rifle (as duas mãos na arma); sem nada de rifle, as da pistola.
            AddFirearmStates(sm, empty, 2, "Bacamarte", new Vector3(-300f, 120f),
                Or(ClipRifleIdle, Or(ClipRifleAim, ClipPistolIdle)), Or(ClipRifleAim, ClipPistolAim), Or(ClipRifleShoot, ClipShoot),
                Or(ClipRifleReload, ClipGunplay), MixamoClip(ClipRifleRun) ? ClipRifleRun : null);

            AddHitAndDeath(sm, empty);
            AddDodge(sm, empty);
            AddKickAndKnockdown(sm, empty);
            AddBlock(sm, empty, ready, run);
            AddAxe(sm, empty);
            AddDagger(sm, ready);
            AddRise(sm, empty);
            AddGreatsword(sm, empty);
            ExitsToWeaponPose(sm, empty);
            AllowInterruptions(sm);

            // Corpo inteiro: mesmos estados e tempos do tronco, sem máscara. Peso 0 até o driver ligar.
            controller.AddLayer(FullBodyLayerName);
            layers = controller.layers;
            int full = layers.Length - 1;
            layers[full].defaultWeight = 0f;
            layers[full].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[full].avatarMask = null;
            layers[full].syncedLayerIndex = index;
            layers[full].syncedLayerAffectsTiming = false;
            // Camada sincronizada começa com os estados sem clipe: cada um recebe o mesmo do tronco.
            foreach (var child in sm.states)
                if (child.state.motion) layers[full].SetOverrideMotion(child.state, child.state.motion);
            controller.layers = layers;

            AddEatLayer(controller);
            AddStrafeLocomotion(controller);
            PruneOrphans(controller);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var missing = new[] { ClipSlashOut, ClipSlashIn, ClipDraw, ClipSheath, ClipRunSword, ClipShoot, ClipGunplay, ClipPistolIdle, ClipPistolAim }
                .Where(n => !MixamoClip(n)).ToArray();
            if (missing.Length > 0)
                Debug.Log($"[Pirates] Animações do Mixamo faltando em {MixamoImportSettings.Folder}: {string.Join(", ", missing)} (os estados ficam vazios).");
        }

        /// <summary>Nome do clipe do Mixamo, se está no projeto; senão o de reserva (bacamarte sem as animações de rifle usa as da pistola).</summary>
        static string Or(string clip, string fallback) => MixamoClip(clip) ? clip : fallback;

        /// <summary>
        /// Estados de uma arma de fogo (bacamarte = WeaponType 2), como os da pistola: parada ↔ mirando (Aiming), tiro (Attack) que volta
        /// à mira, recarga (Reloading) saindo do tiro ou da mira. Tags: parada/recarga = corpo inteiro parado; mira e tiro = "Mira".
        /// </summary>
        static void AddFirearmStates(AnimatorStateMachine sm, AnimatorState empty, int weaponType, string prefix, Vector3 at,
            string idleClip, string aimClip, string shootClip, string reloadClip, string runClip = null)
        {
            var idle = State(sm, prefix + "Parado", idleClip, 1f, at);
            var aim = State(sm, prefix + "Mirando", aimClip, 1f, at + new Vector3(0f, 100f));
            var shoot = State(sm, prefix + "Tiro", shootClip, shootClip == ClipShoot ? ShootSpeed : 1f, at + new Vector3(-250f, 180f));
            var reload = State(sm, prefix + "Recarga", reloadClip, 1f, at + new Vector3(0f, 200f));
            idle.tag = FullBodyTag;
            reload.tag = FullBodyTag;
            aim.tag = AimTag;
            shoot.tag = AimTag;
            shoot.speedParameterActive = true;
            shoot.speedParameter = "AttackSpeed";

            Any(sm, shoot, 0f, 0.1f, ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, weaponType));
            Go(empty, idle, 0.2f, ("WeaponType", AnimatorConditionMode.Equals, weaponType), ("Aiming", AnimatorConditionMode.IfNot, 0));
            Go(empty, aim, 0.15f, ("WeaponType", AnimatorConditionMode.Equals, weaponType), ("Aiming", AnimatorConditionMode.If, 0));
            Go(idle, aim, 0.15f, ("Aiming", AnimatorConditionMode.If, 0));
            Go(aim, idle, 0.3f, ("Aiming", AnimatorConditionMode.IfNot, 0));
            Go(idle, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, weaponType));
            Go(aim, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, weaponType));

            var toReload = shoot.AddTransition(reload);
            toReload.hasExitTime = true;
            toReload.exitTime = 0.65f;
            toReload.duration = 0.2f;
            toReload.AddCondition(AnimatorConditionMode.If, 0, "Reloading");
            Exit(shoot, aim, 0.8f, 0.2f);
            Go(aim, reload, 0.2f, ("Reloading", AnimatorConditionMode.If, 0));
            Go(reload, aim, 0.25f, ("Reloading", AnimatorConditionMode.IfNot, 0));
            Go(reload, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, weaponType));

            // Correndo (sem mirar: mirando não corre), braços de corrida segurando a arma, como o cutelo.
            if (runClip == null) return;
            var run = State(sm, prefix + "Correndo", runClip, 1f, at + new Vector3(-250f, 0f));
            Go(idle, run, 0.25f, ("Speed", AnimatorConditionMode.Greater, 4.5f));
            Go(run, idle, 0.25f, ("Speed", AnimatorConditionMode.Less, 4f));
            Go(run, aim, 0.15f, ("Aiming", AnimatorConditionMode.If, 0));
            Go(run, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, weaponType));
        }

        /// <summary>
        /// Levar golpe ("Golpeado_N", trigger Hit + HitVariant) e morrer ("Morte_N", trigger Die + DeathVariant; Revive
        /// levanta), com as variações sorteadas pelo CharacterAnimatorDriver. Clipe que faltar fica de fora; sem nenhum,
        /// o FootHealthReactions faz o tranco/queda por código.
        /// </summary>
        static void AddHitAndDeath(AnimatorStateMachine sm, AnimatorState empty)
        {
            var hits = new System.Collections.Generic.List<string>();
            foreach (var (name, start, speed) in HitClips)
            {
                var clip = MixamoClip(name);
                if (!clip) continue;
                var state = State(sm, $"Golpeado_{hits.Count}", name, speed, new Vector3(900f, 60f * hits.Count));
                state.tag = FullBodyTag;
                Any(sm, state, start / clip.length, 0.1f, ("Hit", AnimatorConditionMode.If, 0), ("HitVariant", AnimatorConditionMode.Equals, hits.Count));
                Exit(state, empty, 0.85f, 0.2f);
                hits.Add(name);
            }
            var deaths = new System.Collections.Generic.List<string>();
            foreach (var (name, start, speed) in DeathClips)
            {
                var clip = MixamoClip(name);
                if (!clip) continue;
                var state = State(sm, $"Morte_{deaths.Count}", name, speed, new Vector3(1200f, 60f * deaths.Count));
                state.tag = FullBodyTag;
                var toDeath = sm.AddAnyStateTransition(state);
                toDeath.hasExitTime = false;
                toDeath.duration = 0.15f;
                toDeath.offset = start / clip.length;
                toDeath.canTransitionToSelf = false;
                toDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
                toDeath.AddCondition(AnimatorConditionMode.Equals, deaths.Count, "DeathVariant");
                toDeath.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
                deaths.Add(name);
            }
            // Levantou: volta ao vazio (e dali à pose da arma). Existe mesmo sem clipe, para consumir o trigger.
            var revive = sm.AddAnyStateTransition(empty);
            revive.hasExitTime = false;
            revive.duration = 0.1f;
            revive.AddCondition(AnimatorConditionMode.If, 0, "Revive");
            Debug.Log($"[Pirates] Levar golpe: {(hits.Count > 0 ? string.Join(", ", hits) : "nenhum clipe")}; morrer: " +
                      $"{(deaths.Count > 0 ? string.Join(", ", deaths) : "nenhum clipe")} (sem clipe: tranco e queda por código).");
        }

        static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == name)) controller.AddParameter(name, type);
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, string clip, float speed, Vector3 position)
        {
            var state = sm.AddState(name, position);
            state.motion = MixamoClip(clip);
            state.speed = speed;
            return state;
        }

        static void Any(AnimatorStateMachine sm, AnimatorState to, float startAt, float duration, params (string p, AnimatorConditionMode mode, float v)[] conditions)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.offset = startAt;   // começa o clipe já adiantado (pula o preparo lento do Mixamo)
            t.canTransitionToSelf = true;
            foreach (var c in conditions) t.AddCondition(c.mode, c.v, c.p);
        }

        /// <summary>
        /// Rolamento (pacote A de 09/10): "Sprinting Forward Roll" do Mixamo inteiro (rola do começo ao fim e termina de pé; o
        /// deslocamento do clipe fica fora da pose e quem move é o FootCharacter), no tempo do <see cref="FootCharacter.DodgeSeconds"/>.
        /// Tag "CorpoInteiroAndando": corpo todo mesmo correndo.
        /// </summary>
        static void AddDodge(AnimatorStateMachine sm, AnimatorState empty)
        {
            var clip = MixamoClip(ClipRoll);
            if (!clip) return;
            var roll = State(sm, "Rolar", ClipRoll, clip.length / DodgeSeconds, new Vector3(-300f, -120f));
            roll.tag = MovingFullBodyTag;
            Any(sm, roll, 0f, 0.06f, ("Dodge", AnimatorConditionMode.If, 0));
            Exit(roll, empty, 0.94f, 0.1f);
        }

        /// <summary>
        /// Chute (trigger "Kick", corpo todo) e as quedas (trigger "Knock" + "KnockVariant": 0 = chute, cambaleia e cai sentado; 1 = explosão,
        /// arremessado de costas) que emendam no levantar e voltam ao vazio. Tempos iguais aos do FootHealthReactions (stumbleSeconds 2,6 s,
        /// blastSeconds 2,9 s). Morrer no chão ("Down" ligado) fica deitado: a morte normal começa de pé e ele levantaria para cair de novo.
        /// </summary>
        static void AddKickAndKnockdown(AnimatorStateMachine sm, AnimatorState empty)
        {
            if (MixamoClip(ClipKick))
            {
                var kick = State(sm, "Chute", ClipKick, KickSpeed, new Vector3(-300f, -200f));
                kick.tag = MovingFullBodyTag;
                Any(sm, kick, 0.05f, 0.08f, ("Kick", AnimatorConditionMode.If, 0));
                Exit(kick, empty, 0.8f, 0.15f);
            }
            // Bomba (passo 4): "Throw Object" do preparo (1,3 s) até o braço voltar (~3,4 s), 1,8× (solta aos ~0,64 s do aperto).
            var throwClip = MixamoClip(ClipThrow);
            if (throwClip)
            {
                var toss = State(sm, "Arremessar", ClipThrow, ThrowSpeed, new Vector3(-300f, -280f));
                toss.tag = MovingFullBodyTag;
                // Entra pelo CharacterAnimatorDriver.PlayThrow (CrossFade direto, a partir de ThrowStart): sem transição do "Any State".
                Exit(toss, empty, ThrowEnd / throwClip.length, 0.2f);
            }
            // Alavanca da ruína (C4): "Pulling Lever" de 0,8 s (a mão sai do quadril) até 4,6 s (volta), 1,8× (~2,1 s; puxa aos ~1,5 s).
            var leverClip = MixamoClip(ClipLever);
            if (leverClip)
            {
                var pull = State(sm, "PuxarAlavanca", ClipLever, LeverSpeed, new Vector3(-300f, -360f));
                pull.tag = MovingFullBodyTag;
                // Entra pelo CharacterAnimatorDriver.PlayLever (CrossFade direto a partir de 0,8 s).
                Exit(pull, empty, LeverEnd / leverClip.length, 0.25f);
            }
            if (!MixamoClip(ClipStumble) || !MixamoClip(ClipBlast) || !MixamoClip(ClipGetUp)) return;
            var getUp = State(sm, "Levantar", ClipGetUp, GetUpSpeed, new Vector3(-600f, -300f));
            getUp.tag = MovingFullBodyTag;
            Exit(getUp, empty, 0.72f, 0.25f);
            var quickGetUp = State(sm, "LevantarRapido", ClipGetUp, QuickGetUpSpeed, new Vector3(-600f, -480f));
            quickGetUp.tag = MovingFullBodyTag;
            Exit(quickGetUp, empty, 0.72f, 0.2f);
            var stumble = State(sm, "Tropeco", ClipStumble, StumbleSpeed, new Vector3(-900f, -360f));
            stumble.tag = MovingFullBodyTag;
            Any(sm, stumble, 0f, 0.1f, ("Knock", AnimatorConditionMode.If, 0), ("KnockVariant", AnimatorConditionMode.Equals, 0));
            ExitAlive(stumble, quickGetUp, StumbleExit, 0.15f, QuickGetUpStart);
            var blast = State(sm, "Arremessado", ClipBlast, BlastSpeed, new Vector3(-900f, -240f));
            blast.tag = MovingFullBodyTag;
            Any(sm, blast, BlastStart, 0.08f, ("Knock", AnimatorConditionMode.If, 0), ("KnockVariant", AnimatorConditionMode.Equals, 1));
            ExitAlive(blast, getUp, 0.9f, 0.25f);
            var quickBlast = State(sm, "ArremessadoRapido", ClipBlast, QuickBlastSpeed, new Vector3(-900f, -480f));
            quickBlast.tag = MovingFullBodyTag;
            Any(sm, quickBlast, BlastStart, 0.08f, ("Knock", AnimatorConditionMode.If, 0), ("KnockVariant", AnimatorConditionMode.Equals, 2));
            ExitAlive(quickBlast, quickGetUp, QuickBlastExit, 0.15f, QuickGetUpStart);

            var dead = State(sm, "MortoNoChao", ClipBlast, 1f, new Vector3(-600f, -420f));
            dead.tag = FullBodyTag;
            var toDead = sm.AddAnyStateTransition(dead);
            toDead.hasExitTime = false;
            toDead.duration = 0.2f;
            toDead.offset = 0.85f;
            toDead.canTransitionToSelf = false;
            toDead.AddCondition(AnimatorConditionMode.If, 0, "Die");
            toDead.AddCondition(AnimatorConditionMode.If, 0, "Down");
        }

        /// <summary>
        /// Defesa com o cutelo (pacote A, passo 5): "Defesa" (bool "Block", sai do pronto/correndo) ergue a lâmina e segura o fim do clipe, corpo todo parado e
        /// só o tronco andando; "DefesaImpacto" (trigger "BlockHit") volta à guarda; "Aparado" (trigger "Parried") é o cambaleio de quem
        /// teve o golpe aparado, corpo todo.
        /// </summary>
        static void AddBlock(AnimatorStateMachine sm, AnimatorState empty, AnimatorState ready, AnimatorState run)
        {
            if (MixamoClip(ClipBlockIdle))
            {
                var guard = State(sm, "Defesa", ClipBlockIdle, BlockRaiseSpeed, new Vector3(0f, -200f));
                guard.tag = FullBodyTag;
                Go(ready, guard, 0.1f, ("Block", AnimatorConditionMode.If, 0));
                Go(run, guard, 0.1f, ("Block", AnimatorConditionMode.If, 0));
                Go(empty, guard, 0.1f, ("WeaponType", AnimatorConditionMode.Equals, 0), ("Block", AnimatorConditionMode.If, 0));
                // A mesma guarda serve ao machado (3) e ao espadão (4) do capitão (E2): os dois entram e saem dela pelo AddAxe/AddGreatsword.
                Go(guard, ready, 0.2f, ("Block", AnimatorConditionMode.IfNot, 0), ("WeaponType", AnimatorConditionMode.Equals, 0));
                Go(guard, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 0), ("WeaponType", AnimatorConditionMode.NotEqual, 3),
                    ("WeaponType", AnimatorConditionMode.NotEqual, 4));
                if (MixamoClip(ClipBlockHit))
                {
                    var react = State(sm, "DefesaImpacto", ClipBlockHit, BlockHitSpeed, new Vector3(-300f, -360f));
                    react.tag = FullBodyTag;
                    Any(sm, react, 0f, 0.05f, ("BlockHit", AnimatorConditionMode.If, 0));
                    // Volta à guarda já erguida (fim do "Blocking"), sem erguer de novo.
                    var back = react.AddTransition(guard);
                    back.hasExitTime = true;
                    back.exitTime = 0.85f;
                    back.duration = 0.15f;
                    back.offset = 0.95f;
                    // Soltou a guarda no meio do tranco: baixa já (antes ficava ~0,6 s erguida).
                    Go(react, empty, 0.15f, ("Block", AnimatorConditionMode.IfNot, 0));
                }
                // Aparar (revisão das animações, passo 5): o trecho rápido do corte para fora desvia a lâmina; entra pelo
                // CharacterAnimatorDriver (CrossFade em ParrySwingStart) e volta à guarda, se segurada, ou à pose da arma.
                var slashClip = MixamoClip(ClipSlashOut);
                if (slashClip)
                {
                    var deflect = State(sm, "Aparar", ClipSlashOut, ParrySwingSpeed, new Vector3(-300f, -440f));
                    deflect.tag = FullBodyTag;
                    var toGuard = deflect.AddTransition(guard);
                    toGuard.hasExitTime = true;
                    toGuard.exitTime = ParrySwingEnd / slashClip.length;
                    toGuard.duration = 0.12f;
                    toGuard.offset = 0.95f;
                    toGuard.AddCondition(AnimatorConditionMode.If, 0, "Block");
                    Exit(deflect, empty, ParrySwingEnd / slashClip.length, 0.2f);
                }
            }
            if (MixamoClip(ClipParried))
            {
                var parried = State(sm, "Aparado", ClipParried, ParriedSpeed, new Vector3(-600f, -560f));
                parried.tag = MovingFullBodyTag;
                Any(sm, parried, 0f, 0.06f, ("Parried", AnimatorConditionMode.If, 0));
                Exit(parried, empty, 0.85f, 0.2f);
            }
        }

        /// <summary>
        /// Machado do brutamontes (pacote B, passo 1; WeaponType 3, pacote do machado do Mixamo): "MachadoPronto" (Standing Idle; corpo todo
        /// parado, só o tronco andando), "MachadoCorrendo" (Standing Run Forward) e três golpes sorteados pelo AttackVariant (para baixo,
        /// horizontal, de revés). Cada golpe começa <see cref="AxeLead"/> s de clipe antes do impacto medido (mão mais rápida), então o
        /// impacto cai sempre no mesmo tempo (WeaponDefinition.hitDelay do machado) e o aviso é igual para aparar. "Provocar" (trigger
        /// "Taunt", Taunt Battlecry) é o grito de guerra ao ver o capitão.
        /// </summary>
        static void AddAxe(AnimatorStateMachine sm, AnimatorState empty)
        {
            if (!MixamoClip(ClipAxeIdle)) return;
            var ready = State(sm, "MachadoPronto", ClipAxeIdle, 1f, new Vector3(-900f, 60f));
            ready.tag = FullBodyTag;
            Go(empty, ready, 0.25f, ("WeaponType", AnimatorConditionMode.Equals, 3));
            Go(ready, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 3));
            AnimatorState axeRun = null;
            if (MixamoClip(ClipAxeRun))
            {
                axeRun = State(sm, "MachadoCorrendo", ClipAxeRun, 1f, new Vector3(-900f, 140f));
                Go(ready, axeRun, 0.25f, ("Speed", AnimatorConditionMode.Greater, 3f));
                Go(axeRun, ready, 0.25f, ("Speed", AnimatorConditionMode.Less, 2.6f));
                Go(axeRun, empty, 0.2f, ("WeaponType", AnimatorConditionMode.NotEqual, 3));
            }
            HeavyGuard(sm, empty, 3, ready, axeRun);
            for (int v = 0; v < AxeAttacks.Length; v++)
            {
                var (clipName, impact, lead) = AxeAttacks[v];
                var clip = MixamoClip(clipName);
                if (!clip) continue;
                var swing = State(sm, $"Machado_Golpe{v}", clipName, 1f, new Vector3(-1200f, 60f + 80f * v));
                swing.tag = FullBodyTag;
                swing.speedParameterActive = true;
                swing.speedParameter = "AttackSpeed";
                Any(sm, swing, Mathf.Max(0f, impact - lead) / clip.length, HeavyAttackBlend,
                    ("Attack", AnimatorConditionMode.If, 0), ("WeaponType", AnimatorConditionMode.Equals, 3), ("AttackVariant", AnimatorConditionMode.Equals, v));
                Exit(swing, ready, Mathf.Min(0.95f, (impact + 0.75f) / clip.length), 0.3f);
            }
            if (MixamoClip(ClipTaunt))
            {
                var taunt = State(sm, "Provocar", ClipTaunt, TauntSpeed, new Vector3(-1200f, 320f));
                taunt.tag = FullBodyTag;
                Any(sm, taunt, 0f, 0.15f, ("Taunt", AnimatorConditionMode.If, 0));
                Exit(taunt, empty, 0.92f, 0.25f);
            }
        }

        /// <summary>
        /// Guarda com o machado (3) ou o espadão (4) do capitão (E2): a mesma "Defesa" do cutelo (o "Blocking" do pacote do espadão), entrando
        /// do pronto, da corrida e do vazio daquela arma, e voltando ao pronto dela.
        /// </summary>
        static void HeavyGuard(AnimatorStateMachine sm, AnimatorState empty, int weaponType, AnimatorState ready, AnimatorState run)
        {
            AnimatorState guard = null;
            foreach (var child in sm.states)
                if (child.state.name == "Defesa") guard = child.state;
            if (!guard) return;
            Go(ready, guard, 0.1f, ("Block", AnimatorConditionMode.If, 0));
            if (run) Go(run, guard, 0.1f, ("Block", AnimatorConditionMode.If, 0));
            Go(empty, guard, 0.1f, ("WeaponType", AnimatorConditionMode.Equals, weaponType), ("Block", AnimatorConditionMode.If, 0));
            Go(guard, ready, 0.2f, ("Block", AnimatorConditionMode.IfNot, 0), ("WeaponType", AnimatorConditionMode.Equals, weaponType));
        }

        /// <summary>Saída pelo tempo só se estiver vivo ("Dead" desligado): morto no meio da queda fica deitado no fim do clipe.</summary>
        static void ExitAlive(AnimatorState from, AnimatorState to, float exitTime, float duration, float startAt = 0f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
            t.offset = startAt;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }

        static void Exit(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
        }

        static void Go(AnimatorState from, AnimatorState to, float duration, params (string p, AnimatorConditionMode mode, float v)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach (var c in conditions) t.AddCondition(c.mode, c.v, c.p);
        }

        /// <summary>
        /// Mistura de entrada dos golpes do machado e do espadão (revisão de 21/10: era 0,12 s; no combo o fim de um golpe pesado e o preparo
        /// do seguinte são poses bem diferentes e o corpo "pulava").
        /// </summary>
        const float HeavyAttackBlend = 0.18f;

        /// <summary>
        /// Combo de 3 no cutelo do capitão e nas lâminas dele (revisão de 21/10; refeito a cada geração a partir do dano, do impacto e da
        /// velocidade da lâmina): os dois cortes de sempre e a finalização de cima (+40% de dano, alcance +0,2 m, arco +20°, derruba, e 0,3 s
        /// a mais de brecha antes do próximo golpe). Os cutelos da IA ficam com os 2 golpes.
        /// </summary>
        static void CutlassFinisher(WeaponDefinition w)
        {
            if (!w || w.kind != WeaponKind.Melee || w.animatorWeaponType != 0) return;
            float k = Mathf.Max(0.1f, w.attackAnimSpeed);
            w.attackVariants = 3;
            w.randomVariants = false;
            w.animatorVariantOffset = 0;
            w.animatorVariants = new[] { 0, 1, CutlassFinisherVariant };
            w.variantHitDelays = new[] { w.hitDelay, w.hitDelay, FinisherLead / FinisherSpeed / k };
            // Empurrão (passo 5): o alvo recua um pouco a cada corte e bem mais na finalização.
            w.hitPush = 2.4f;
            w.variantStrikes = new[]
            {
                new StrikeOverride(), new StrikeOverride(),
                new StrikeOverride
                {
                    damage = Mathf.Round(w.damage * 1.4f), range = w.range + 0.2f, angle = w.angle + 20f,
                    customKnockdown = true, knockdown = Pirates.Core.Knockdown.Stumble, extraRecovery = 0.3f, push = 4f,
                },
            };
            EditorUtility.SetDirty(w);
        }

        /// <summary>
        /// Andar de lado e de ré (revisão das animações, 21/10): clipes do pacote do machado (andar nas 4 direções, correr para a frente e
        /// para trás) e do espadão (de lado rápido), com a velocidade que cada um anda (m/s, medida no clipe), num blend tree 2D pela
        /// velocidade no espaço do personagem (MoveX, MoveZ). Estado "Lateral" na camada de base, pelo bool "Strafe" (FootCharacter.IsStrafing).
        /// </summary>
        static readonly (string clip, float x, float z)[] StrafeClips =
        {
            ("Standing Walk Forward", 0f, 1.05f), ("Standing Walk Back", 0f, -0.88f), ("Standing Walk Left", -0.89f, 0f), ("Standing Walk Right", 1.1f, 0f),
            ("Standing Run Forward", 0f, 2.47f), ("Standing Run Back", 0f, -1.88f), ("Strafe 3", -2.28f, 0f), ("Strafe 4", 3f, 0f),
        };

        static void AddStrafeLocomotion(AnimatorController controller)
        {
            AddParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            AddParameter(controller, "MoveZ", AnimatorControllerParameterType.Float);
            AddParameter(controller, "Strafe", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == "Lateral") sm.RemoveState(child.state);
                else if (child.state.motion is BlendTree && child.state.name == "Idle Walk Run Blend") locomotion = child.state;
            }
            if (!locomotion || locomotion.motion is not BlendTree walkTree || walkTree.children.Length == 0) return;
            var clips = new System.Collections.Generic.List<(AnimationClip clip, Vector2 at)>();
            foreach (var (name, x, z) in StrafeClips)
            {
                var clip = MixamoClip(name);
                if (clip) clips.Add((clip, new Vector2(x, z)));
            }
            if (clips.Count < 4) return;
            var tree = new BlendTree
            {
                name = "Lateral", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveZ",
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            // Parado: o mesmo "Idle" da locomoção.
            tree.AddChild(walkTree.children[0].motion, Vector2.zero);
            foreach (var (clip, at) in clips) tree.AddChild(clip, at);
            var lateral = sm.AddState("Lateral", new Vector3(500f, 250f));
            lateral.motion = tree;
            Go(locomotion, lateral, 0.2f, ("Strafe", AnimatorConditionMode.If, 0));
            Go(lateral, locomotion, 0.2f, ("Strafe", AnimatorConditionMode.IfNot, 0));
            foreach (var t in lateral.transitions) t.interruptionSource = TransitionInterruptionSource.Destination;
        }

        /// <summary>Pose de cada arma na mão (WeaponType; nas armas de fogo, parada ou mirando pelo "Aiming").</summary>
        static readonly (int type, string state, int aiming)[] WeaponPoses =
        {
            (0, "EspadaPronta", -1), (1, "PistolaMirando", 1), (1, "PistolaParada", 0), (2, "BacamarteMirando", 1), (2, "BacamarteParado", 0),
            (3, "MachadoPronto", -1), (4, "Espadao_Pronto", -1),
        };

        /// <summary>
        /// Revisão das animações (21/10): a volta de uma ação (levar golpe, rolar, chutar, cambalear, levantar, provocar, sair da cova, guarda
        /// soltada no tranco) ia para o "Vazio" e só dali para a pose da arma: o braço da espada caía para a pose sem arma por ~0,45 s. Cada
        /// saída para o "Vazio" sem condição de WeaponType ganha, antes dela, uma igual para a pose de cada arma; a original fica por último
        /// (mãos vazias, no meio de uma troca). "Guardar" fica de fora (termina sem arma).
        /// </summary>
        static void ExitsToWeaponPose(AnimatorStateMachine sm, AnimatorState empty)
        {
            var byName = new System.Collections.Generic.Dictionary<string, AnimatorState>();
            foreach (var child in sm.states) byName[child.state.name] = child.state;
            foreach (var child in sm.states)
            {
                var from = child.state;
                if (from == empty || from.name == "Guardar") continue;
                var original = from.transitions;
                var ordered = new System.Collections.Generic.List<AnimatorStateTransition>();
                bool changed = false;
                foreach (var t in original)
                {
                    if (t.destinationState == empty && !t.conditions.Any(c => c.parameter == "WeaponType"))
                    {
                        foreach (var (type, name, aiming) in WeaponPoses)
                        {
                            if (!byName.TryGetValue(name, out var pose) || pose == from) continue;
                            var copy = from.AddTransition(pose);
                            copy.hasExitTime = t.hasExitTime;
                            copy.exitTime = t.exitTime;
                            copy.hasFixedDuration = t.hasFixedDuration;
                            copy.duration = t.duration;
                            foreach (var c in t.conditions) copy.AddCondition(c.mode, c.threshold, c.parameter);
                            copy.AddCondition(AnimatorConditionMode.Equals, type, "WeaponType");
                            if (aiming >= 0) copy.AddCondition(aiming == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Aiming");
                            ordered.Add(copy);
                        }
                        changed = true;
                    }
                    ordered.Add(t);
                }
                if (changed) from.transitions = ordered.ToArray();
            }
        }

        /// <summary>
        /// Revisão das animações (21/10): nenhuma transição deixava ser interrompida, então rolar, chutar, levar golpe e o golpe seguinte do
        /// combo esperavam a mistura que estava rodando acabar (até 0,3 s; medido: rolar apertado aos 0,70 s do golpe aparecia aos 1,00 s),
        /// enquanto o jogo já contava o tempo da ação. Agora as transições dos estados podem ser cortadas pelas do estado de origem e as do
        /// destino (e pelas do "Any State"); as do "Any State", pelas do destino.
        /// </summary>
        static void AllowInterruptions(AnimatorStateMachine sm)
        {
            foreach (var child in sm.states)
                foreach (var t in child.state.transitions) t.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            foreach (var t in sm.anyStateTransitions) t.interruptionSource = TransitionInterruptionSource.Destination;
        }

        /// <summary>
        /// Tira do controller as máquinas de estado, estados e transições que nenhuma camada usa mais (sobravam ~280 máquinas
        /// "CorpoInteiro" vazias das camadas refeitas a cada geração: o arquivo tinha 343 KB).
        /// </summary>
        static void PruneOrphans(AnimatorController controller)
        {
            var keep = new System.Collections.Generic.HashSet<UnityEngine.Object> { controller };
            void KeepMotion(Motion motion)
            {
                if (motion is BlendTree tree && keep.Add(tree))
                    foreach (var child in tree.children) KeepMotion(child.motion);
            }
            void KeepState(AnimatorState state)
            {
                if (!state || !keep.Add(state)) return;
                foreach (var t in state.transitions) keep.Add(t);
                foreach (var b in state.behaviours) keep.Add(b);
                KeepMotion(state.motion);
            }
            void KeepMachine(AnimatorStateMachine machine)
            {
                if (!machine || !keep.Add(machine)) return;
                foreach (var child in machine.states) KeepState(child.state);
                foreach (var t in machine.anyStateTransitions) keep.Add(t);
                foreach (var t in machine.entryTransitions) keep.Add(t);
                foreach (var b in machine.behaviours) keep.Add(b);
                foreach (var sub in machine.stateMachines)
                {
                    KeepMachine(sub.stateMachine);
                    foreach (var t in machine.GetStateMachineTransitions(sub.stateMachine)) keep.Add(t);
                }
            }
            foreach (var layer in controller.layers) KeepMachine(layer.stateMachine);
            int removed = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)))
            {
                if (!o || keep.Contains(o)) continue;
                if (o is AnimatorStateMachine or AnimatorState or AnimatorTransitionBase or BlendTree or StateMachineBehaviour)
                {
                    AssetDatabase.RemoveObjectFromAsset(o);
                    UnityEngine.Object.DestroyImmediate(o, true);
                    removed++;
                }
            }
            if (removed > 0) Debug.Log($"[Pirates] Animator: {removed} peças sem uso tiradas do controller.");
        }

        /// <summary>
        /// Comer (05/10): gole na garrafa do Mixamo ("Drinking", 8,9 s, importado espelhado: bebe com a mão direita), numa camada
        /// por cima de tudo com máscara do braço direito e da cabeça: o resto do corpo segue andando/parado com a arma, e o braço do
        /// gancho (com o papagaio) não mexe. Só o trecho do gole (2,0 → 6,1 s), mais rápido (~1,9 s). Sem o clipe, não entra.
        /// </summary>
        static void AddEatLayer(AnimatorController controller)
        {
            var eatClip = MixamoClip(ClipDrinking);
            if (!eatClip) return;
            controller.AddLayer(EatLayerName);
            var layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].defaultWeight = 1f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].avatarMask = RightArmHeadMask();
            controller.layers = layers;
            var sm = controller.layers[index].stateMachine;
            var empty = sm.AddState("Vazio", new Vector3(300f, 0f));
            sm.defaultState = empty;
            var eat = State(sm, "Comer", ClipDrinking, EatSpeed, new Vector3(300f, 120f));
            Any(sm, eat, 2.0f / eatClip.length, 0.2f, ("Eat", AnimatorConditionMode.If, 0));
            Exit(eat, empty, 6.1f / eatClip.length, 0.3f);
            // Morreu comendo: larga o gole na hora.
            var die = sm.AddAnyStateTransition(empty);
            die.hasExitTime = false;
            die.duration = 0.1f;
            die.canTransitionToSelf = false;
            die.AddCondition(AnimatorConditionMode.If, 0, "Die");
        }

        static AvatarMask RightArmHeadMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(EatMaskPath);
            if (mask) return mask;
            mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part is AvatarMaskBodyPart.RightArm or AvatarMaskBodyPart.RightFingers
                    or AvatarMaskBodyPart.RightHandIK or AvatarMaskBodyPart.Head);
            }
            AssetDatabase.CreateAsset(mask, EatMaskPath);
            return mask;
        }

        static AvatarMask UpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask) return mask;
            mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head or AvatarMaskBodyPart.LeftArm
                    or AvatarMaskBodyPart.RightArm or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers
                    or AvatarMaskBodyPart.LeftHandIK or AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            return mask;
        }

        /// <summary>Clipe "X Bot@&lt;nome&gt;" da pasta do Mixamo (ou qualquer arquivo cujo nome termine com o nome).</summary>
        static AnimationClip MixamoClip(string name)
        {
            if (!AssetDatabase.IsValidFolder(MixamoImportSettings.Folder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { MixamoImportSettings.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                string tail = file.Contains('@') ? file[(file.LastIndexOf('@') + 1)..] : file;
                if (!string.Equals(tail.Trim(), name, System.StringComparison.OrdinalIgnoreCase)) continue;
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip) return clip;
            }
            return null;
        }

        /// <summary>Bonecos de treino na praia, perto do desembarque.</summary>
        static void CreateTrainingDummies(Material wood, Material sack)
        {
            var parent = new GameObject("BonecosDeTreino").transform;
            // Na praia de desembarque (relativo ao centro da ilha).
            var spots = new[] { new Vector3(-7f, 0f, 40f), new Vector3(6f, 0f, 37f), new Vector3(-2f, 0f, 31f), new Vector3(11f, 0f, 43f) };
            for (int k = 0; k < spots.Length; k++) spots[k] += BigIslandCenter;
            for (int k = 0; k < spots.Length; k++)
            {
                GroundAt(spots[k], out Vector3 ground);
                Dummy(parent, ground, 180f + k * 25f, $"Boneco_{k}", wood, sack);
            }
        }

        /// <summary>Um boneco de treino (poste, saco, cabeça e braços) com vida, que balança e tomba (TrainingDummy).</summary>
        static void Dummy(Transform parent, Vector3 ground, float yaw, string name, Material wood, Material sack)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.95f, 0f);
            col.height = 1.9f;
            col.radius = 0.35f;
            root.AddComponent<Health>().Configure(100f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Part(PrimitiveType.Cylinder, "Poste", visual, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 0.7f, 0.12f), Quaternion.identity, wood);
            Part(PrimitiveType.Capsule, "Saco", visual, new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.45f, 0.45f), Quaternion.identity, sack);
            Part(PrimitiveType.Sphere, "Cabeca", visual, new Vector3(0f, 1.75f, 0f), Vector3.one * 0.32f, Quaternion.identity, sack);
            Part(PrimitiveType.Cube, "Bracos", visual, new Vector3(0f, 1.3f, 0f), new Vector3(1.1f, 0.08f, 0.08f), Quaternion.identity, wood);
            root.AddComponent<TrainingDummy>().Configure(visual);
        }
    }
}
