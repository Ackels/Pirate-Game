using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Comida do capitão (05/10): item Ração (Data/Items/Item_Racao), comida a pé com R (FoodEater no personagem: +40 de vida,
    /// leva 5 do porão ao desembarcar), à venda no mercado (6 de ouro; o mercado não compra de volta, sem lucro com o bônus de
    /// negociante) e nas tabelas de saque de terra (caixote, barril, baú do acampamento). Assets e listas existentes são
    /// preservados: só entra o que ainda não está lá.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static ItemDefinition RationItem()
        {
            var item = Item("Item_Racao", "Ração", ItemCategory.Resource, ItemRarity.Common, new Color(0.86f, 0.66f, 0.4f), 0.25f,
                "Biscoito do mar, carne salgada e um gole de rum. A pé, R come uma e cura 40 de vida.");
            // Revisão de equilíbrio (05/10): 0,5 → 0,25 (as 20 que ficam a bordo prendiam 10 de espaço do porão). Só troca o valor antigo.
            if (Mathf.Approximately(item.spacePerUnit, 0.5f))
            {
                item.spacePerUnit = 0.25f;
                EditorUtility.SetDirty(item);
            }
            // Fica a bordo ao descarregar no porto (como a madeira do carpinteiro): o capitão leva de lá ao desembarcar.
            if (item.keepAboard == 0)
            {
                item.keepAboard = 20;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        /// <summary>Ração à venda no mercado da praça (vai para o porão), se ainda não está na lista.</summary>
        static void AddRationMarket(HarborSettings settings)
        {
            var ration = RationItem();
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            if (list.Exists(e => e != null && e.item == ration)) return;
            list.Insert(Mathf.Min(3, list.Count), Market(ration, 6, 0, toHold: true));
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>Ração nos saques de terra (só acrescenta se a tabela ainda não tem).</summary>
        static void AddRationLoot(LootTable table, float chance, int min, int max) => AddLoot(table, RationItem(), chance, min, max);

        /// <summary>Item num saque (só acrescenta se a tabela ainda não tem: chance e quantidade são ajuste de game design).</summary>
        static void AddLoot(LootTable table, ItemDefinition item, float chance, int min, int max)
        {
            if (!table || !item || System.Array.Exists(table.drops, d => d != null && d.item == item)) return;
            var drops = new List<LootTable.Drop>(table.drops) { Drop(item, chance, min, max) };
            table.drops = drops.ToArray();
            EditorUtility.SetDirty(table);
        }

        /// <summary>
        /// O capitão come rações (R) e leva algumas do porão ao desembarcar; a mochila cresce com a melhor que ele tiver; a pistola
        /// gasta cartuchos (20 na mochila de começo nas cenas sem jogo salvo; no mundo quem dá é o jogo novo).
        /// </summary>
        static void AddFoodEater(GameObject character, PlayerModeSwitcher switcher)
        {
            character.AddComponent<FoodEater>().Configure(RationItem(), switcher, RumBottle(character.transform));
            character.AddComponent<BackpackGear>().Configure(BackpackCapacity, Backpacks());
            character.AddComponent<PistolAmmo>().Configure(CartridgeItem(), switcher, startWith: 20);
            AddBlunderbussGear(character);
            AddBladeGear(character);
            AddBombThrower(character, switcher);
        }

        // Garrafa na mão direita (o clipe "Drinking" entra espelhado: o gole da esquerda ficava atrás do papagaio), ~30 cm no mundo,
        // segurada pelo gargalo, o fundo para fora. Giro em relação ao osso da mão (medido em Play, foto de perto).
        static Vector3 BottleOffset = Vector3.zero, BottleEuler = new(25.9f, 216.8f, 98.4f);
        const float BottleHeight = 0.3f;

        static GameObject RumBottle(Transform character)
        {
            Transform hand = null;
            foreach (var t in character.GetComponentsInChildren<Transform>(true))
                if (t.name == "Hand.R") hand = t;
            if (!hand) return null;
            var holder = new GameObject("Garrafa").transform;
            holder.SetParent(hand, false);
            holder.SetLocalPositionAndRotation(BottleOffset / Mathf.Max(0.0001f, hand.lossyScale.y), Quaternion.Euler(BottleEuler));
            var model = Kenney("bottle", holder, Vector3.zero, 0f, 1f, "Modelo");
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(0.01f, bounds.size.y);
            model.transform.localScale = Vector3.one * (BottleHeight / height);
            // O gargalo na mão: o modelo (pivô no fundo) desce a altura da garrafa ao longo do "para cima" do suporte.
            model.transform.localPosition = new Vector3(0f, -BottleHeight / Mathf.Max(0.0001f, holder.lossyScale.y), 0f);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            holder.gameObject.SetActive(false);
            return holder.gameObject;
        }

        // ---------- Cartuchos da pistola (05/10) ----------

        static ItemDefinition CartridgeItem()
        {
            var item = Item("Item_Cartuchos", "Cartuchos de pistola", ItemCategory.Resource, ItemRarity.Common, new Color(0.78f, 0.74f, 0.66f), 0.05f,
                "Bala de chumbo e pólvora num papel encerado: cada tiro da pistola gasta um. O capitão leva 20 do porão ao desembarcar.");
            if (item.keepAboard == 0)
            {
                item.keepAboard = 60;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        /// <summary>Cartuchos no mercado (2 de ouro, não compra de volta) e na Forja (20 por 1 ferro + 1 pólvora, 45 s), se ainda não estão.</summary>
        static void AddCartridgeTrade(HarborSettings settings, LootItems i, Pirates.Progression.BuildingDefinition forge)
        {
            var cartridge = CartridgeItem();
            bool changed = false;
            var market = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            if (!market.Exists(e => e != null && e.item == cartridge))
            {
                market.Insert(Mathf.Min(4, market.Count), Market(cartridge, 2, 0, toHold: true));
                settings.market = market.ToArray();
                changed = true;
            }
            var smithy = new List<HarborSettings.Recipe>(settings.smithy ?? System.Array.Empty<HarborSettings.Recipe>());
            if (!smithy.Exists(r => r?.result.item == cartridge))
            {
                smithy.Insert(0, new HarborSettings.Recipe
                {
                    result = new ItemStack(cartridge, 20),
                    cost = new[] { new ItemStack(i.iron, 1), new ItemStack(i.powder, 1) },
                    requiredBuilding = forge, requiredLevel = 1,
                    craftSeconds = 45f,
                });
                settings.smithy = smithy.ToArray();
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// Mochilas maiores (revisão de equilíbrio 05/10, aprovada pelo Jhonata): couro 25 e contrabandista 35 (a de lona tinha 15). Em
        /// 29/10 (pedido do Jhonata: cortar árvore e minerar enchiam a mochila rápido) lona 25, couro 37, contrabandista 50.
        /// O gerador só põe o espaço em item novo: os assets foram atualizados à mão.
        /// </summary>
        static ItemDefinition[] Backpacks()
        {
            ItemDefinition Pack(string file, string name, float capacity, ItemRarity rarity, Color color, string description)
            {
                var item = Item(file, name, ItemCategory.Gear, rarity, color, 0f, description);
                if (item.backpackCapacity <= 0f)
                {
                    item.backpackCapacity = capacity;
                    EditorUtility.SetDirty(item);
                }
                return item;
            }
            return new[]
            {
                Pack("Equip_MochilaCouro", "Mochila de couro", 37f, ItemRarity.Uncommon, new Color(0.62f, 0.42f, 0.25f),
                    "Couro curtido com fivelas de latão: 37 de espaço para o saque em terra (a de lona tem 25)."),
                Pack("Equip_MochilaContrabandista", "Mochila de contrabandista", 50f, ItemRarity.Rare, new Color(0.36f, 0.55f, 0.75f),
                    "Fundo falso e bolsos escondidos: 50 de espaço para o saque em terra."),
            };
        }

        /// <summary>Mochilas à venda no mercado por patente (uma vez; não compra de volta), se ainda não estão na lista.</summary>
        static void AddBackpackMarket(HarborSettings settings)
        {
            var packs = Backpacks();
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            bool changed = false;
            void Add(ItemDefinition pack, int price, int rank)
            {
                if (list.Exists(e => e != null && e.item == pack)) return;
                var entry = Market(pack, price, 0);
                entry.requiredRank = rank;
                list.Add(entry);
                changed = true;
            }
            Add(packs[0], 200, 1);
            Add(packs[1], 500, 2);
            if (!changed) return;
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }
    }
}
