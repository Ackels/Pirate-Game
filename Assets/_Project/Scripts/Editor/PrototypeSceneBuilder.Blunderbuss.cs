using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.OnFoot;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Bacamarte do capitão (05/10, pedido do Jhonata): a "doze" de pederneira, arma 3 a pé. Chumbo em leque (7 projéteis, muito dano
    /// de perto, curto alcance), duas mãos, munição própria (Data/Items/Item_Chumbo, "Cargas de chumbo"), comprado uma vez no mercado
    /// (Equip_Bacamarte, patente 1; o CharacterCombat só libera a arma 3 com ele guardado: Controls/GearWeapons). Modelo = "Weapon_Rifle"
    /// do Quaternius Pirate Kit (cano de boca larga), encurtado na escala; nas costas quando guardado. Animações de rifle do Mixamo
    /// (Rifle Idle, Rifle Aiming Idle, Firing Rifle, Reloading); sem elas, usa as da pistola. Assets e listas existentes são preservados.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Baixadas pelo Jhonata em 05/10 (personagem Pirate, sem malha; sem a "Rifle Idle", parado usaria a de mira).
        const string ClipRifleIdle = "Rifle Idle", ClipRifleAim = "Rifle Aiming Idle", ClipRifleShoot = "Firing Rifle", ClipRifleReload = "Reloading",
            ClipRifleRun = "Rifle Run";
        /// <summary>
        /// Escala do modelo do kit (2,9 unidades): ~1,7 m de arma, a boca ~1,1 m à frente da mão. Parece muito, mas o capitão é cabeçudo
        /// (quadril a 0,5 m): com 0,4 a arma sumia na câmera do jogo (05/10, foto do Jhonata).
        /// </summary>
        const float BlunderbussScale = 0.6f;
        // No kit: cano em −X (boca larga em x −2,04), coronha em +X; a mão pega o pescoço da coronha, logo atrás do gatilho.
        static readonly Vector3 BlunderbussGrip = new(-0.15f, 0f, 0f), BlunderbussMuzzle = new(-2.04f, 0.22f, 0f);

        static WeaponDefinition BlunderbussWeapon() => LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Bacamarte.asset", w =>
        {
            w.displayName = "Bacamarte";
            w.kind = WeaponKind.Firearm;
            w.animatorWeaponType = 2;
            w.twoHanded = true;
            // 7 chumbos × 14 = 98 colado (um pirata de 60 cai de um tiro); a 10 m pegam ~3 a 4 no corpo.
            w.damage = 14f;
            w.pellets = 7;
            w.pelletSpread = 16f;
            w.range = 16f;
            w.angle = 3f;
            w.hitDelay = 0.3f;
            w.cooldown = 0.6f;
            w.shotsPerReload = 1;
            w.reloadTime = 3.5f;
            w.aimPoseSeconds = 1.5f;
            w.aimedShotDelay = 0.06f;
            w.aimedSpread = 0.5f;
            w.aimMoveSpeed = 0.6f;
            w.recoilDegrees = 24f;
            w.moveSpeedWhileAttacking = 0.25f;
        });

        static ItemDefinition BlunderbussItem() => Item("Equip_Bacamarte", "Bacamarte", ItemCategory.Gear, ItemRarity.Rare,
            new Color(0.55f, 0.4f, 0.28f), 0f,
            "Boca de sino e chumbo grosso: um tiro em leque que derruba um pirata de perto. Arma 3 a pé (duas mãos), gasta cargas de chumbo.");

        static ItemDefinition ShotItem()
        {
            var item = Item("Item_Chumbo", "Cargas de chumbo", ItemCategory.Resource, ItemRarity.Common, new Color(0.55f, 0.57f, 0.62f), 0.1f,
                "Punhado de chumbo e pólvora para o bacamarte: cada tiro gasta uma. O capitão leva 12 do porão ao desembarcar.");
            if (item.keepAboard == 0)
            {
                item.keepAboard = 40;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        /// <summary>A arma 3 só saca com o bacamarte comprado; ele gasta cargas de chumbo (o capitão leva 12 do porão ao desembarcar).</summary>
        static void AddBlunderbussGear(GameObject character)
        {
            var combat = character.GetComponent<CharacterCombat>();
            int index = -1;
            for (int i = 0; i < combat.Weapons.Count; i++) if (combat.Weapons[i] == BlunderbussWeapon()) index = i;
            if (index < 0) return;
            character.AddComponent<GearWeapons>().Configure(new GearWeapons.Entry { weaponIndex = index, item = BlunderbussItem() });
            if (character.TryGetComponent(out PistolAmmo ammo))
                ammo.ConfigureExtra(new PistolAmmo.WeaponAmmo { weapon = BlunderbussWeapon(), item = ShotItem(), carryAshore = 12 });
        }

        /// <summary>
        /// Bacamarte no mercado (350 de ouro, patente 1, uma vez) e as cargas de chumbo (5 de ouro, não compra de volta; Forja: 10 por
        /// 1 ferro + 1 pólvora, 60 s, Forja 1), se ainda não estão.
        /// </summary>
        static void AddBlunderbussTrade(HarborSettings settings, LootItems i, Pirates.Progression.BuildingDefinition forge)
        {
            var gun = BlunderbussItem();
            var shot = ShotItem();
            bool changed = false;
            var market = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            if (!market.Exists(e => e != null && e.item == shot))
            {
                int after = market.FindIndex(e => e != null && e.item == CartridgeItem());
                market.Insert(after >= 0 ? after + 1 : market.Count, Market(shot, 5, 0, toHold: true));
                changed = true;
            }
            if (!market.Exists(e => e != null && e.item == gun))
            {
                var entry = Market(gun, 350, 0);
                entry.requiredRank = 1;
                market.Add(entry);
                changed = true;
            }
            if (changed) settings.market = market.ToArray();
            var smithy = new List<HarborSettings.Recipe>(settings.smithy ?? System.Array.Empty<HarborSettings.Recipe>());
            if (!smithy.Exists(r => r?.result.item == shot))
            {
                int after = smithy.FindIndex(r => r?.result.item == CartridgeItem());
                smithy.Insert(after >= 0 ? after + 1 : 0, new HarborSettings.Recipe
                {
                    result = new ItemStack(shot, 10),
                    cost = new[] { new ItemStack(i.iron, 1), new ItemStack(i.powder, 1) },
                    requiredBuilding = forge, requiredLevel = 1,
                    craftSeconds = 60f,
                });
                settings.smithy = smithy.ToArray();
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// Bacamarte na mão direita, cano para a frente: com o clipe de mira de rifle, na direção da mão direita para a esquerda (a que
        /// apoia o cano) e o alto da arma para cima; sem ele, como a pistola (cano seguindo o antebraço). Guardado: nas costas, cano
        /// para cima sobre o ombro direito (o esquerdo é do papagaio).
        /// </summary>
        static Transform BlunderbussOn(Transform model, Material iron, Material wood, out Transform muzzle, out Transform handSlot,
            out Transform holsterSlot)
        {
            Transform hand = Bone(model, HumanBodyBones.RightHand);
            RifleGrip(model, out Vector3 barrel, out Vector3 top);
            var root = new GameObject("Bacamarte").transform;
            root.SetParent(hand, false);
            // Eixos da arma: +X = cano, +Y = alto (cão), como a pistola.
            root.SetLocalPositionAndRotation(LocalOffset(hand, barrel, 0.04f), Quaternion.LookRotation(Vector3.Cross(barrel, top), top));
            var kitRotation = Quaternion.Euler(0f, 180f, 0f);
            muzzle = new GameObject("Boca").transform;
            muzzle.SetParent(root, false);
            if (KitWeapon(root, "Weapon_Rifle", kitRotation, BlunderbussScale, BlunderbussGrip))
                muzzle.SetLocalPositionAndRotation(kitRotation * (BlunderbussMuzzle - BlunderbussGrip) * BlunderbussScale,
                    Quaternion.LookRotation(Vector3.right, Vector3.up));
            else
            {
                Part(PrimitiveType.Cube, "Coronha", root, new Vector3(-0.18f, -0.04f, 0f), new Vector3(0.3f, 0.07f, 0.045f), Quaternion.Euler(0f, 0f, -12f), wood);
                Part(PrimitiveType.Cylinder, "Cano", root, new Vector3(0.3f, 0.03f, 0f), new Vector3(0.045f, 0.3f, 0.045f), Quaternion.Euler(0f, 0f, 90f), iron);
                Part(PrimitiveType.Cylinder, "Sino", root, new Vector3(0.6f, 0.03f, 0f), new Vector3(0.085f, 0.03f, 0.085f), Quaternion.Euler(0f, 0f, 90f), iron);
                muzzle.SetLocalPositionAndRotation(new Vector3(0.62f, 0.03f, 0f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            }
            UndoHandScale(root, hand);
            handSlot = SlotFor(root, hand, "Mao_Bacamarte");
            holsterSlot = BackHolster(model);
            return root;
        }

        /// <summary>
        /// Costas, na diagonal: cano para cima e para a direita (sobre o ombro direito), coronha para baixo e à esquerda (inclinada o
        /// bastante para não bater no chão), o alto da arma para fora das costas.
        /// </summary>
        static Transform BackHolster(Transform model)
        {
            Transform chest = null;
            if (model.TryGetComponent(out Animator animator) && animator.isHuman)
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!chest) chest = Bone(model, HumanBodyBones.Chest);
            var back = new GameObject("Costas_Bacamarte").transform;
            Vector3 right = model.right, forward = model.forward;
            Vector3 barrel = (Vector3.up * 0.6f + right * 0.8f).normalized;
            Vector3 top = -forward;
            // A arma é montada a partir da mão (origem = pegada): o meio dela fica (0,59 − 0,15) × escala cano acima; centra nas costas.
            Vector3 center = chest.position - forward * 0.22f;
            back.SetPositionAndRotation(center - barrel * (0.44f * BlunderbussScale), Quaternion.LookRotation(Vector3.Cross(barrel, top), top));
            back.SetParent(chest, true);
            MatchHandScale(back, model);
            return back;
        }

        /// <summary>
        /// Eixos do bacamarte no espaço do osso da mão direita: "barrel" (cano) e "top" (alto da arma). Do clipe "Rifle Aiming Idle":
        /// cano da mão direita para a esquerda (que segura o cano), alto = para cima. Sem o clipe, os da pistola (antebraço, polegar).
        /// </summary>
        static void RifleGrip(Transform model, out Vector3 barrel, out Vector3 top)
        {
            HandGrip(model, out barrel, out top);
            var clip = MixamoClip(ClipRifleAim);
            var temp = clip ? MeasureCopy(model) : null;
            if (!temp) return;
            var anim = temp.GetComponent<Animator>();
            clip.SampleAnimation(temp, 1f);
            var right = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var left = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 barrelWorld = (left.position - right.position).normalized;
            Vector3 topWorld = Vector3.ProjectOnPlane(Vector3.up, barrelWorld).normalized;
            var inverse = Quaternion.Inverse(right.rotation);
            barrel = inverse * barrelWorld;
            top = inverse * topWorld;
            Object.DestroyImmediate(temp);
        }

        /// <summary>
        /// Para onde o cano aponta na pose de mira de rifle (graus a partir da frente do personagem; da mão direita para a esquerda), com
        /// o corpo inteiro e só com o tronco (quadril da locomoção). Sem o clipe, os da pistola.
        /// </summary>
        static void RifleAimYaw(Transform model, out float upperBody, out float fullBody)
        {
            AimPoseArmYaw(model, ClipPistolAim, out upperBody, out fullBody);
            var clip = MixamoClip(ClipRifleAim);
            var temp = clip ? MeasureCopy(model) : null;
            if (!temp) return;
            var anim = temp.GetComponent<Animator>();
            clip.SampleAnimation(temp, 1f);
            var right = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var left = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float barrel = Yaw(left.position - right.position);
            fullBody = barrel;
            upperBody = Mathf.DeltaAngle(Yaw(hips.forward), barrel);
            Object.DestroyImmediate(temp);
        }

        /// <summary>Cargas de chumbo nos barris e no baú do acampamento (só acrescenta se a tabela ainda não tem).</summary>
        static void AddShotLoot(LootTable barrel, LootTable chest)
        {
            AddLoot(barrel, ShotItem(), 0.2f, 2, 4);
            AddLoot(chest, ShotItem(), 0.35f, 4, 8);
        }
    }
}
