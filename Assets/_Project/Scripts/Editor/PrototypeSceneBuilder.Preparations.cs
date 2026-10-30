using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Preparos dos peixes na Forja (D2, passo 4a do plano de 30/10; pedido do Jhonata: "dar mais propósitos para os peixes"): o veneno do
    /// baiacu passado na lâmina (cada golpe envenena: dano por segundo por alguns segundos) e a bomba de fumaça com a tinta da siba (tecla B
    /// a pé: ninguém vê quem está dentro, e os piratas pegos nela ficam cegos). Os dois usam o mesmo sistema dos pratos da taverna
    /// (<see cref="Meals"/>): o veneno dura alguns minutos; a fumaça estoura na hora.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static ItemDefinition PoisonItem()
        {
            var item = Item("Item_Veneno_Baiacu", "Veneno de baiacu", ItemCategory.Resource, ItemRarity.Uncommon, new Color(0.45f, 0.85f, 0.3f), 0.1f,
                "Frasco com o veneno do baiacu, fervido na Forja. Passado na lâmina (tela I, Usar), cada golpe envenena quem ela corta por 2 minutos.");
            return Preparation(item, MealEffect.Poison, 4f, 2f);
        }

        static ItemDefinition SmokeBombItem()
        {
            var item = Item("Item_Bomba_Fumaca", "Bomba de fumaça", ItemCategory.Resource, ItemRarity.Uncommon, new Color(0.45f, 0.4f, 0.55f), 0.3f,
                "Pólvora fraca com a tinta da siba. A pé, B estoura no seu pé uma nuvem escura: ninguém vê quem está dentro, e os piratas pegos nela " +
                "ficam cegos e tossindo. Boa para fugir ou para chegar pelas costas (a adaga dobra o dano). O capitão leva 3 do porão ao desembarcar.");
            return Preparation(item, MealEffect.Smoke, 0f, 0f);
        }

        static ItemDefinition Preparation(ItemDefinition item, MealEffect effect, float amount, float minutes)
        {
            item.mealEffect = effect;
            item.mealAmount = amount;
            item.mealMinutes = minutes;
            item.foodHeal = 0f;
            item.group = ItemGroup.Ammo;
            EditorUtility.SetDirty(item);
            return item;
        }

        static IEnumerable<ItemDefinition> PreparationItems()
        {
            yield return PoisonItem();
            yield return SmokeBombItem();
            yield return IncendiaryItem();
            yield return PearlNecklaceItem();
            yield return SeahorseAmuletItem();
        }

        // ---------- 4b: munição incendiária e relíquias ----------

        /// <summary>Carga incendiária (óleo de cavala): tecla 4 no navio; o navio acertado pega fogo.</summary>
        static ItemDefinition IncendiaryItem() => Item("Item_CargaIncendiaria", "Carga incendiária", ItemCategory.Resource, ItemRarity.Uncommon,
            new Color(1f, 0.5f, 0.2f), 1f,
            "Bala enrolada em estopa encharcada no óleo de cavala. Menos dano na hora, mas o navio acertado pega fogo: 4 de casco por segundo por " +
            "8 s (um acerto novo recomeça). Uma carga por bordada (tecla 4 no navio).");

        /// <summary>Relíquia: colar de pérolas (+15% de ouro nas missões e no saque).</summary>
        static ItemDefinition PearlNecklaceItem() => Relic(Item("Equip_ColarPerolas", "Colar de pérolas", ItemCategory.Gear, ItemRarity.Epic,
            new Color(0.95f, 0.92f, 1f), 0f,
            "Três pérolas grandes num fio de ouro. Dizem que atrai ouro como o ouro atrai pirata. Relíquia: +15% de ouro nas missões e no saque " +
            "(equipe no encaixe Relíquia da tela I)."), RelicEffect.Gold, 0.15f);

        /// <summary>Relíquia: cavalo-marinho seco (+50% de chance de baú nos destroços e de equipamento nos baús).</summary>
        static ItemDefinition SeahorseAmuletItem() => Relic(Item("Equip_CavaloMarinhoSeco", "Cavalo-marinho seco", ItemCategory.Gear, ItemRarity.Rare,
            new Color(0.85f, 0.6f, 0.3f), 0f,
            "Amuleto de marinheiro: três cavalos-marinhos secos ao sol, amarrados num cordão. Dá sorte a bordo. Relíquia: +50% de chance de baú nos " +
            "destroços e de equipamento nos baús (equipe no encaixe Relíquia da tela I)."), RelicEffect.Treasure, 0.5f);

        static ItemDefinition Relic(ItemDefinition item, RelicEffect effect, float amount)
        {
            item.equipSlot = EquipSlot.Relic;
            item.relicEffect = effect;
            item.relicAmount = amount;
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>As receitas na Forja (Forja nível 1), depois da bomba de pólvora, se ainda não estão lá.</summary>
        static void AddPreparationRecipes(HarborSettings settings, LootItems i, Pirates.Progression.BuildingDefinition forge)
        {
            var smithy = new List<HarborSettings.Recipe>(settings.smithy ?? System.Array.Empty<HarborSettings.Recipe>());
            ItemDefinition Fish(string file)
            {
                var f = System.Array.Find(FishSpecs, x => x.file == file);
                return f != null ? FishItem(f) : null;
            }
            var poison = PoisonItem();
            var smoke = SmokeBombItem();
            bool changed = false;
            if (!smithy.Exists(r => r?.result.item == poison) && Fish("Item_Peixe_Baiacu"))
            {
                smithy.Add(new HarborSettings.Recipe
                {
                    result = new ItemStack(poison, 2), cost = new[] { new ItemStack(Fish("Item_Peixe_Baiacu"), 2) },
                    requiredBuilding = forge, requiredLevel = 1, craftSeconds = 40f,
                });
                changed = true;
            }
            if (!smithy.Exists(r => r?.result.item == smoke) && Fish("Item_Peixe_Siba"))
            {
                smithy.Add(new HarborSettings.Recipe
                {
                    result = new ItemStack(smoke, 2), cost = new[] { new ItemStack(Fish("Item_Peixe_Siba"), 2), new ItemStack(i.powder, 1) },
                    requiredBuilding = forge, requiredLevel = 1, craftSeconds = 50f,
                });
                changed = true;
            }
            void Add(ItemDefinition result, int amount, ItemStack[] cost, float seconds)
            {
                if (!result || smithy.Exists(r => r?.result.item == result)) return;
                foreach (var c in cost) if (!c.item) return;
                smithy.Add(new HarborSettings.Recipe
                {
                    result = new ItemStack(result, amount), cost = cost, requiredBuilding = forge, requiredLevel = 1, craftSeconds = seconds,
                });
                changed = true;
            }
            Add(IncendiaryItem(), 6, new[] { new ItemStack(Fish("Item_Peixe_Cavala"), 4), new ItemStack(i.powder, 2) }, 90f);
            Add(PearlNecklaceItem(), 1, new[] { new ItemStack(Fish("Item_Perola"), 3), new ItemStack(i.gold, 50) }, 120f);
            Add(SeahorseAmuletItem(), 1, new[] { new ItemStack(Fish("Item_Peixe_CavaloMarinho"), 3) }, 60f);
            if (!changed) return;
            settings.smithy = smithy.ToArray();
            EditorUtility.SetDirty(settings);
        }

        static void AddSmokeBomb(GameObject character, PlayerModeSwitcher switcher)
        {
            character.AddComponent<SmokeBomb>().Configure(SmokeBombItem(), switcher);
            BakePreparationIcons();
        }

        static void BakePreparationIcons()
        {
            SetItemIcon("Item_Veneno_Baiacu", Icon("Item_Veneno_Baiacu", PoisonIcon, new Vector3(-0.3f, 0.5f, 1f)));
            SetItemIcon("Item_Bomba_Fumaca", Icon("Item_Bomba_Fumaca", SmokeBombIcon, new Vector3(-0.5f, 0.5f, 1f)));
            SetItemIcon("Item_CargaIncendiaria", Icon("Item_CargaIncendiaria", IncendiaryIcon, new Vector3(-0.5f, 0.5f, 1f)));
            SetItemIcon("Equip_ColarPerolas", Icon("Equip_ColarPerolas", NecklaceIcon, new Vector3(0f, 1f, 0.6f)));
            SetItemIcon("Equip_CavaloMarinhoSeco", Icon("Equip_CavaloMarinhoSeco", AmuletIcon, new Vector3(-0.2f, 0.3f, 1f)));
        }

        /// <summary>Frasco verde (a garrafa de óleo do Food Kit) com uma etiqueta de papel e uma gota de veneno escorrendo.</summary>
        static void PoisonIcon(Transform t)
        {
            float top = FoodPiece("bottle-oil", t, Vector3.zero, 0f, 0.55f, GetMaterial("M_Icone_Veneno", new Color(0.35f, 0.8f, 0.25f), 0.7f));
            PrismPart(12, "Etiqueta", t, new Vector3(0f, top * 0.35f, 0f), new Vector3(0.6f, top * 0.22f, 0.6f), Quaternion.identity,
                GetMaterial("M_Icone_Etiqueta", new Color(0.93f, 0.86f, 0.68f), 0.1f));
            Part(PrimitiveType.Sphere, "Gota", t, new Vector3(0.36f, top * 0.12f, 0.1f), new Vector3(0.16f, 0.22f, 0.16f), Quaternion.identity,
                GetMaterial("M_Icone_Veneno", new Color(0.35f, 0.8f, 0.25f), 0.7f));
        }

        /// <summary>Bala de canhão enrolada em estopa (faixa clara) com as chamas por cima.</summary>
        static void IncendiaryIcon(Transform t)
        {
            var ball = Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Bala");
            var b = FitIcon(ball, 0.6f, Vector3.zero);
            PrismPart(12, "Estopa", t, new Vector3(0f, b.size.y * 0.5f, 0f), new Vector3(0.66f, 0.14f, 0.66f), Quaternion.Euler(0f, 0f, 15f),
                GetMaterial("M_Icone_Estopa", new Color(0.82f, 0.68f, 0.42f), 0.1f));
            var flame = GetMaterial("M_Icone_Chama", new Color(1f, 0.55f, 0.15f), 0.1f);
            var core = GetMaterial("M_Icone_ChamaClara", new Color(1f, 0.85f, 0.3f), 0.1f);
            Part(PrimitiveType.Sphere, "Chama1", t, new Vector3(0f, b.size.y + 0.12f, 0f), new Vector3(0.3f, 0.5f, 0.3f), Quaternion.Euler(0f, 0f, 8f), flame);
            Part(PrimitiveType.Sphere, "Chama2", t, new Vector3(0.17f, b.size.y + 0.02f, 0.04f), new Vector3(0.2f, 0.32f, 0.2f), Quaternion.Euler(0f, 0f, -20f), flame);
            Part(PrimitiveType.Sphere, "Chama3", t, new Vector3(-0.16f, b.size.y + 0.01f, 0f), new Vector3(0.18f, 0.28f, 0.18f), Quaternion.Euler(0f, 0f, 22f), flame);
            Part(PrimitiveType.Sphere, "Miolo", t, new Vector3(0.01f, b.size.y + 0.06f, 0.12f), new Vector3(0.16f, 0.28f, 0.16f), Quaternion.identity, core);
        }

        /// <summary>Colar visto de cima: o fio de ouro em anel de pérolas pequenas e três pérolas grandes embaixo.</summary>
        static void NecklaceIcon(Transform t)
        {
            var pearl = GetMaterial("M_Icone_Perola", new Color(0.96f, 0.94f, 1f), 0.8f);
            var gold = GetMaterial("M_Icone_Ouro", new Color(1f, 0.78f, 0.22f), 0.7f);
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.42f + 0.08f);
                Part(PrimitiveType.Sphere, "Conta", t, at, Vector3.one * (k % 2 == 0 ? 0.09f : 0.06f), Quaternion.identity, k % 2 == 0 ? pearl : gold);
            }
            for (int k = -1; k <= 1; k++)
                Part(PrimitiveType.Sphere, "Perola", t, new Vector3(k * 0.17f, 0.02f, -0.42f - (k == 0 ? 0.08f : 0f)), Vector3.one * (k == 0 ? 0.2f : 0.15f),
                    Quaternion.identity, pearl);
        }

        /// <summary>O cavalo-marinho do jogo, seco (marrom), pendurado num cordão.</summary>
        static void AmuletIcon(Transform t)
        {
            var spec = System.Array.Find(FishSpecs, f => f.file == "Item_Peixe_CavaloMarinho");
            if (spec == null) return;
            var holder = new GameObject("Cavalo").transform;
            holder.SetParent(t, false);
            FishModel(holder, new FishSpec
            {
                file = "Equip_CavaloMarinhoSeco", folder = spec.folder, prefab = spec.prefab, pose = spec.pose, size = 0.9f,
                tint = new Color(1f, 0.78f, 0.5f),
            });
            var cord = GetMaterial("M_Icone_Cordao", new Color(0.45f, 0.3f, 0.18f), 0.1f);
            PrismPart(6, "Cordao1", t, new Vector3(-0.08f, 0.5f, 0f), new Vector3(0.035f, 0.4f, 0.035f), Quaternion.Euler(0f, 0f, -22f), cord);
            PrismPart(6, "Cordao2", t, new Vector3(0.08f, 0.5f, 0f), new Vector3(0.035f, 0.4f, 0.035f), Quaternion.Euler(0f, 0f, 22f), cord);
        }

        /// <summary>Bala de canhão roxa-escura (tinta de siba) com o pavio e uma nuvenzinha cinza saindo.</summary>
        static void SmokeBombIcon(Transform t)
        {
            var ball = Kenney("cannon-ball", t, Vector3.zero, 0f, 1f, "Bomba");
            foreach (var r in ball.GetComponentsInChildren<Renderer>()) r.sharedMaterial = GetMaterial("M_Icone_Fumaca", new Color(0.28f, 0.22f, 0.36f), 0.4f);
            var b = FitIcon(ball, 0.6f, Vector3.zero);
            PrismPart(6, "Bocal", t, new Vector3(0f, b.size.y * 0.97f, 0f), new Vector3(0.14f, 0.07f, 0.14f), Quaternion.identity,
                GetMaterial("M_Icone_Ferro", IconIron, 0.5f));
            var cloud = GetMaterial("M_Icone_Nuvem", new Color(0.72f, 0.72f, 0.78f), 0.1f);
            Part(PrimitiveType.Sphere, "Nuvem1", t, new Vector3(0.08f, b.size.y + 0.16f, 0f), new Vector3(0.26f, 0.2f, 0.26f), Quaternion.identity, cloud);
            Part(PrimitiveType.Sphere, "Nuvem2", t, new Vector3(0.22f, b.size.y + 0.3f, 0.02f), new Vector3(0.22f, 0.17f, 0.22f), Quaternion.identity, cloud);
            Part(PrimitiveType.Sphere, "Nuvem3", t, new Vector3(0.02f, b.size.y + 0.38f, -0.02f), new Vector3(0.19f, 0.15f, 0.19f), Quaternion.identity, cloud);
        }
    }
}
