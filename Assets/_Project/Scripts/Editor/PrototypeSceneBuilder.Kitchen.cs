using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Cozinha da taverna (D2, passo 2 do plano de 30/10; pedido do Jhonata: "dar mais propósitos para os peixes"): os pratos (itens com o
    /// bônus de comida), as receitas na taverna (peixe + um pouco de ouro, na hora), o <see cref="Meals"/> que aplica os bônus e a
    /// plaquinha do HUD. Ícones: o peixe num prato de madeira, ou uma tigela com o caldo.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        sealed class DishSpec
        {
            public string file, name, description;
            public Color color;
            public MealEffect effect;
            public float amount, minutes;
            /// <summary>Ingredientes (arquivo do item, quantidade); o ouro vem à parte.</summary>
            public (string file, int amount)[] cost;
            public int gold = 5;
            /// <summary>Peixe do jogo que vai por cima no ícone (polvo, siba).</summary>
            public string iconFish;
        }

        // Duração e força pensadas para uma ida a uma ilha ou uma luta (8–10 min de jogo); um prato de cada tipo vale ao mesmo tempo.
        static readonly DishSpec[] DishSpecs =
        {
            new() { file = "Item_Prato_Caldeirada", name = "Caldeirada de cavala", color = new Color(0.95f, 0.6f, 0.35f),
                effect = MealEffect.Stamina, amount = 25f, minutes = 10f, cost = new[] { ("Item_Peixe_Cavala", 2), ("Item_Peixe_Camarao", 1) },
                iconFish = "Item_Peixe_Camarao",
                description = "Cavala e camarão no caldo de tomate da taverna. Enche a barriga e o fôlego: rola e segura a guarda por mais tempo." },
            new() { file = "Item_Prato_Atum", name = "Atum grelhado", color = new Color(0.75f, 0.5f, 0.35f),
                effect = MealEffect.Reload, amount = 0.1f, minutes = 8f, cost = new[] { ("Item_Peixe_Atum", 1) }, iconFish = "Item_Peixe_Atum",
                description = "Posta grossa na grelha, para a tripulação inteira. Artilheiro bem alimentado carrega o canhão mais depressa." },
            new() { file = "Item_Prato_Polvo", name = "Ensopado de polvo", color = new Color(0.8f, 0.45f, 0.75f),
                effect = MealEffect.HullRegen, amount = 1f, minutes = 8f, cost = new[] { ("Item_Peixe_Polvo", 1), ("Item_Peixe_Pepino", 1) },
                iconFish = "Item_Peixe_Polvo",
                description = "Polvo e pepino-do-mar no vinho. Com a barriga cheia, os marujos tapam os buracos do casco sem ninguém mandar." },
            new() { file = "Item_Prato_Siba", name = "Sopa de siba", color = new Color(0.35f, 0.35f, 0.5f),
                effect = MealEffect.Stealth, amount = 0.35f, minutes = 10f, cost = new[] { ("Item_Peixe_Siba", 1) }, iconFish = "Item_Peixe_Siba",
                description = "Sopa preta com a tinta da siba. Dizem que quem toma anda como sombra: os piratas de terra demoram a te ver." },
            new() { file = "Item_Prato_Garoupa", name = "Garoupa assada", color = new Color(0.85f, 0.6f, 0.35f),
                effect = MealEffect.Damage, amount = 0.15f, minutes = 8f, cost = new[] { ("Item_Peixe_Garoupa", 1), ("Item_Peixe_Pargo", 1) },
                iconFish = "Item_Peixe_Garoupa", gold = 8,
                description = "Garoupa da Ilha Norte com pargo do porto, assadas no forno de barro. Comida de capitão: o braço pesa mais na lâmina." },
        };

        static ItemDefinition DishItem(DishSpec d)
        {
            var item = Item(d.file, d.name, ItemCategory.Resource, ItemRarity.Uncommon, d.color, 0.3f, d.description);
            // Refeito a cada geração (a tabela manda): o efeito e o grupo do filtro.
            item.mealEffect = d.effect;
            item.mealAmount = d.amount;
            item.mealMinutes = d.minutes;
            item.foodHeal = 0f;   // não entra no R (ração/peixe): come-se pela tela I
            item.group = ItemGroup.Food;
            EditorUtility.SetDirty(item);
            return item;
        }

        static IEnumerable<ItemDefinition> DishItems()
        {
            foreach (var d in DishSpecs) yield return DishItem(d);
        }

        /// <summary>As receitas da cozinha na taverna (só as que ainda não estão lá).</summary>
        static void AddKitchenRecipes(HarborSettings settings)
        {
            var list = new List<HarborSettings.Recipe>(settings.kitchen ?? System.Array.Empty<HarborSettings.Recipe>());
            var gold = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/Item_Ouro.asset");
            bool changed = false;
            foreach (var d in DishSpecs)
            {
                var dish = DishItem(d);
                if (list.Exists(r => r != null && r.result.item == dish)) continue;
                var cost = new List<ItemStack>();
                foreach (var (file, amount) in d.cost)
                {
                    var spec = System.Array.Find(FishSpecs, f => f.file == file);
                    if (spec != null) cost.Add(new ItemStack(FishItem(spec), amount));
                }
                if (gold && d.gold > 0) cost.Add(new ItemStack(gold, d.gold));
                list.Add(new HarborSettings.Recipe { result = new ItemStack(dish, 1), cost = cost.ToArray(), toHold = true, craftSeconds = 0f });
                changed = true;
            }
            if (!changed) return;
            settings.kitchen = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>Os bônus dos pratos (objeto "Refeicoes", sempre ligado) e a plaquinha no HUD.</summary>
        static void AddMeals(ShipController ship, GameObject captain)
        {
            new GameObject("Refeicoes").AddComponent<Meals>().Configure(ship, captain);
            BakeDishIcons();
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<MealHUD>()) hud.gameObject.AddComponent<MealHUD>();
        }

        static void BakeDishIcons()
        {
            foreach (var d in DishSpecs)
            {
                var spec = d;
                SetItemIcon(spec.file, Icon(spec.file.Replace("Item_", ""), t => DishIcon(t, spec), new Vector3(-0.3f, 1f, 1f)));
            }
        }

        /// <summary>
        /// Ícone do prato com as peças do Food Kit do Kenney (30/10, pedido do Jhonata: os de prisma não agradaram): caldeirada na panela,
        /// atum grelhado no prato com limão, ensopado de polvo na frigideira (o polvo do jogo por cima), sopa de siba na tigela (a siba por
        /// cima) e garoupa assada na tábua com limão. O peixe do Kenney vem cinza-azulado: grelhado/assado leva uma cor por cima.
        /// </summary>
        static void DishIcon(Transform t, DishSpec d)
        {
            var grilled = FoodTint("M_KenneyFood_Grelhado", new Color(1f, 0.8f, 0.58f));
            var roasted = FoodTint("M_KenneyFood_Assado", new Color(1f, 0.84f, 0.48f));
            switch (d.file)
            {
                case "Item_Prato_Caldeirada":
                    FoodPiece("pot-stew", t, Vector3.zero, 20f, 1f);
                    break;
                case "Item_Prato_Atum":
                {
                    float top = FoodPiece("plate", t, Vector3.zero, 0f, 1.2f);
                    FoodPiece("fish", t, new Vector3(-0.05f, top, 0f), 25f, 0.95f, grilled, 90f);
                    FoodPiece("lemon-half", t, new Vector3(0.4f, top, 0.28f), 0f, 0.26f);
                    break;
                }
                case "Item_Prato_Polvo":
                {
                    float top = FoodPiece("pan-stew", t, Vector3.zero, 0f, 1.15f);
                    Topping(t, d, new Vector3(0f, top - 0.05f, 0f), 0.75f);
                    break;
                }
                case "Item_Prato_Siba":
                {
                    float top = FoodPiece("bowl-soup", t, Vector3.zero, 0f, 1f);
                    Topping(t, d, new Vector3(0f, top - 0.04f, 0f), 0.7f);
                    break;
                }
                default:
                {
                    float top = FoodPiece("cutting-board", t, Vector3.zero, 10f, 1.3f);
                    FoodPiece("fish", t, new Vector3(-0.05f, top, 0f), 20f, 1f, roasted, 90f);
                    FoodPiece("lemon-half", t, new Vector3(0.45f, top, 0.25f), 0f, 0.26f);
                    break;
                }
            }
        }

        /// <summary>O peixe do jogo (polvo, siba) por cima do prato.</summary>
        static void Topping(Transform t, DishSpec d, Vector3 at, float size)
        {
            var fishSpec = System.Array.Find(FishSpecs, f => f.file == d.iconFish);
            if (fishSpec == null) return;
            var holder = new GameObject("Peixe").transform;
            holder.SetParent(t, false);
            holder.localPosition = at + Vector3.up * size * 0.3f;
            holder.localRotation = Quaternion.Euler(0f, 30f, 0f);
            FishModel(holder, new FishSpec
            {
                file = d.file, kit = fishSpec.kit, folder = fishSpec.folder, prefab = fishSpec.prefab, pose = fishSpec.pose,
                tint = fishSpec.tint, glow = fishSpec.glow, plain = fishSpec.plain, size = size,
            });
        }

        /// <summary>
        /// Uma peça do Food Kit com a largura <paramref name="size"/> (o maior lado no chão), apoiada em <paramref name="at"/>. Devolve a
        /// altura do topo dela (para pôr outra em cima).
        /// </summary>
        static float FoodPiece(string model, Transform parent, Vector3 at, float yaw, float size, Material material = null, float roll = 0f)
        {
            // roll: giro no comprido (o peixe do Kenney vem em pé; 90 = deitado de lado no prato).
            var go = Model(Kit.Food, model, parent, Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll), 1f, null);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (material)
                foreach (var r in renderers)
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    r.sharedMaterials = slots;
                }
            var b = RendererBounds(renderers);
            float wide = Mathf.Max(b.size.x, b.size.z);
            if (wide > 1e-4f) go.transform.localScale *= size / wide;
            b = RendererBounds(renderers);
            var target = parent.TransformPoint(at);
            go.transform.position += target - new Vector3(b.center.x, b.min.y, b.center.z);
            return parent.InverseTransformPoint(new Vector3(0f, RendererBounds(renderers).max.y, 0f)).y;
        }

        /// <summary>O material do Food Kit com uma cor por cima (peixe grelhado/assado).</summary>
        static Material FoodTint(string name, Color tint)
        {
            var mat = GetTexturedMaterial(name, AssetDatabase.LoadAssetAtPath<Texture2D>(KitFolder(Kit.Food) + "/Textures/colormap.png"), 0.12f);
            if (mat.HasProperty("_BaseColor") && mat.GetColor("_BaseColor") != tint)
            {
                mat.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
