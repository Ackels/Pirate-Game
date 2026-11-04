using System.Collections.Generic;
using Pirates.Base;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Progression;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Recursos novos (R1, 03/11; pedido do Jhonata: "apenas madeira, ouro, ferro e pólvora... está muito pouco", escolhidos por ele): os
    /// materiais <b>pedra</b>, <b>cordame</b>, <b>lona</b> e <b>breu</b> e a carga de comércio <b>rum</b> e <b>especiarias</b>. Este passo os
    /// coloca no mundo: saque (mercantes, piratas no mar, contrabandistas, baús), mercado, Forja (breu e cordame com madeira) e a pedra minerada
    /// nas pedras comuns das matas (<see cref="AddStoneQuarries"/>). Pedir os materiais nas obras, melhorias e navios = R2.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static ItemDefinition StoneItem() => Item("Item_Pedra", "Pedra", ItemCategory.Resource, ItemRarity.Common, new Color(0.62f, 0.6f, 0.56f), 2f,
            "Pedra cortada das pedreiras das ilhas (picareta, numa pedra grande das matas) ou comprada no mercado. Para as construções da base.");

        static ItemDefinition RopeItem() => Item("Item_Cordame", "Cordame", ItemCategory.Resource, ItemRarity.Common, new Color(0.78f, 0.64f, 0.4f), 0.5f,
            "Rolos de corda de cânhamo. Velas, mastros e canhões precisam de muita: vem dos mercantes e dos navios piratas, do mercado " +
            "ou da Forja (trançada com madeira).");

        static ItemDefinition CanvasItem() => Item("Item_Lona", "Lona", ItemCategory.Resource, ItemRarity.Common, new Color(0.9f, 0.86f, 0.74f), 1f,
            "Pano grosso de vela, enrolado. Vem dos porões dos mercantes e do mercado: o velame novo e os navios maiores pedem muita.");

        static ItemDefinition TarItem() => Item("Item_Breu", "Breu", ItemCategory.Resource, ItemRarity.Common, new Color(0.18f, 0.15f, 0.12f), 0.5f,
            "Piche preto de resina cozida, para calafetar o casco. Feito na Forja com madeira, ou achado nos porões dos piratas.");

        static ItemDefinition RumItem() => Item("Item_Rum", "Rum", ItemCategory.Resource, ItemRarity.Uncommon, new Color(0.62f, 0.34f, 0.14f), 0.5f,
            "Garrafas de rum das Antilhas. Carga de comércio: vende bem no mercado. Os mercantes levam, e os contrabandistas guardam.");

        static ItemDefinition SpiceItem() => Item("Item_Especiarias", "Especiarias", ItemCategory.Resource, ItemRarity.Rare, new Color(0.85f, 0.45f, 0.15f), 0.25f,
            "Pimenta, cravo e canela em sacos pequenos. Carga cara e leve dos mercantes: só para vender.");

        /// <summary>Os seis (registro do save e do F1).</summary>
        static IEnumerable<ItemDefinition> MaterialItems()
        {
            yield return StoneItem();
            yield return RopeItem();
            yield return CanvasItem();
            yield return TarItem();
            yield return RumItem();
            yield return SpiceItem();
        }

        /// <summary>Grupos do filtro: o rum e as especiarias ficam em Tesouros (carga para vender); os materiais, em Recursos.</summary>
        static void TagMaterialGroups()
        {
            foreach (var item in new[] { RumItem(), SpiceItem() })
                if (item && item.group != ItemGroup.Treasure)
                {
                    item.group = ItemGroup.Treasure;
                    EditorUtility.SetDirty(item);
                }
        }

        /// <summary>Mercado (compra e venda; a carga de comércio só se vende) e as receitas da Forja (breu e cordame com madeira).</summary>
        static void AddMaterialTrade(HarborSettings settings, LootItems i, BuildingDefinition forge)
        {
            TagMaterialGroups();
            var market = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            bool changed = false;
            void Sell(ItemDefinition item, int buy, int sell, int demand = 20)
            {
                if (!item || market.Exists(e => e != null && e.item == item)) return;
                var entry = Market(item, buy, sell);
                entry.demand = demand;
                market.Add(entry);
                changed = true;
            }
            // Comprar sempre mais caro que vender (sem lucro no vai e vem). O breu e o cordame do mercado custam mais que fazer na Forja.
            Sell(StoneItem(), 8, 3);
            Sell(RopeItem(), 16, 6);
            Sell(CanvasItem(), 18, 7);
            Sell(TarItem(), 14, 5);
            Sell(RumItem(), 0, 16, 15);
            Sell(SpiceItem(), 0, 40, 10);
            if (changed) settings.market = market.ToArray();

            var smithy = new List<HarborSettings.Recipe>(settings.smithy ?? System.Array.Empty<HarborSettings.Recipe>());
            bool forged = false;
            void Recipe(ItemDefinition result, int amount, ItemStack[] cost, float seconds)
            {
                if (!result || smithy.Exists(r => r?.result.item == result)) return;
                smithy.Add(new HarborSettings.Recipe
                {
                    result = new ItemStack(result, amount), cost = cost, requiredBuilding = forge, requiredLevel = 1, craftSeconds = seconds,
                    toHold = false,
                });
                forged = true;
            }
            Recipe(TarItem(), 2, new[] { new ItemStack(i.wood, 4) }, 40f);
            Recipe(RopeItem(), 2, new[] { new ItemStack(i.wood, 3) }, 45f);
            // R3: lona tecida do cordame (para não depender dos navios).
            Recipe(CanvasItem(), 2, new[] { new ItemStack(RopeItem(), 3) }, 60f);
            if (forged) settings.smithy = smithy.ToArray();
            if (changed || forged) EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// Os materiais nos saques que já existem (só entram se a tabela ainda não tem). Mercante = a carga boa (lona, cordame, rum,
        /// especiarias); piratas no mar = cordame, lona e breu do próprio navio; contrabandistas = rum; baús de terra = um pouco de cada.
        /// </summary>
        static void AddMaterialLoot()
        {
            LootTable T(string file) => AssetDatabase.LoadAssetAtPath<LootTable>($"{DataFolder}/{file}.asset");
            var rope = RopeItem();
            var canvas = CanvasItem();
            var tar = TarItem();
            var rum = RumItem();
            var spice = SpiceItem();
            var stone = StoneItem();

            var merchant = T("LootTable_Mercante");
            AddLoot(merchant, canvas, 0.7f, 3, 6);
            AddLoot(merchant, rope, 0.7f, 3, 7);
            AddLoot(merchant, rum, 0.55f, 2, 5);
            AddLoot(merchant, spice, 0.45f, 1, 3);
            AddLoot(merchant, tar, 0.3f, 2, 4);

            foreach (var file in new[] { "LootTable_ChalupaInimiga", "LootTable_Brigue", "LootTable_Cacador", "LootTable_Galeao" })
            {
                var t = T(file);
                bool big = file != "LootTable_ChalupaInimiga";
                AddLoot(t, rope, 0.5f, big ? 3 : 1, big ? 6 : 3);
                AddLoot(t, canvas, 0.35f, big ? 2 : 1, big ? 4 : 2);
                AddLoot(t, tar, 0.4f, big ? 2 : 1, big ? 5 : 3);
                if (big) AddLoot(t, rum, 0.3f, 1, 3);
            }
            foreach (var file in new[] { "LootTable_Contrabando", "LootTable_TesouroContrabandista" })
            {
                AddLoot(T(file), rum, 0.7f, 3, 6);
                AddLoot(T(file), spice, 0.3f, 1, 2);
            }
            foreach (var file in new[] { "LootTable_BauAcampamento", "LootTable_PaiolForte" })
            {
                AddLoot(T(file), rope, 0.4f, 2, 4);
                AddLoot(T(file), tar, 0.35f, 1, 3);
                AddLoot(T(file), rum, 0.3f, 1, 3);
            }
            AddLoot(T("LootTable_Caixote"), rope, 0.12f, 1, 2);
            AddLoot(T("LootTable_Barril"), tar, 0.12f, 1, 2);
            AddLoot(T("LootTable_Barril"), rum, 0.08f, 1, 1);
            AddLoot(T("LootTable_Ermida"), stone, 0.5f, 3, 6);
            AddMaterialLootMore();
        }

        /// <summary>
        /// R3 (03/11, pedido do Jhonata: "não fica travado em conseguir só dos mercantes"): lona, rum e especiarias também nos baús de terra,
        /// tesouros, chefes e na Esquadra Real; a ilha dá cordame (palmeira), breu (árvore) e pedra (veio de ferro) no corte e na mineração.
        /// </summary>
        static void AddMaterialLootMore()
        {
            LootTable T(string file) => AssetDatabase.LoadAssetAtPath<LootTable>($"{DataFolder}/{file}.asset");
            var rope = RopeItem();
            var canvas = CanvasItem();
            var tar = TarItem();
            var rum = RumItem();
            var spice = SpiceItem();
            var stone = StoneItem();
            foreach (var file in new[] { "LootTable_BauAcampamento", "LootTable_PaiolForte" })
            {
                AddLoot(T(file), canvas, 0.35f, 2, 4);
                AddLoot(T(file), spice, 0.15f, 1, 2);
            }
            foreach (var file in new[] { "LootTable_Contrabando", "LootTable_TesouroContrabandista" })
            {
                AddLoot(T(file), canvas, 0.4f, 2, 4);
                AddLoot(T(file), rope, 0.4f, 2, 4);
            }
            foreach (var file in new[] { "LootTable_TesouroEnterrado", "LootTable_MapaDoTesouro" })
            {
                AddLoot(T(file), rum, 0.5f, 2, 4);
                AddLoot(T(file), spice, 0.5f, 1, 3);
                AddLoot(T(file), canvas, 0.3f, 2, 4);
                AddLoot(T(file), rope, 0.35f, 2, 5);
            }
            AddLoot(T("LootTable_Caixote"), canvas, 0.12f, 1, 2);
            AddLoot(T("LootTable_Caixote"), rum, 0.06f, 1, 1);
            AddLoot(T("LootTable_ChalupaInimiga"), rum, 0.15f, 1, 2);
            AddLoot(T("LootTable_Ermida"), spice, 0.3f, 1, 3);
            AddLoot(T("LootTable_Ermida"), rum, 0.3f, 2, 4);
            AddLoot(T("LootTable_Catacumbas"), spice, 0.5f, 2, 4);
            AddLoot(T("LootTable_Catacumbas"), stone, 0.4f, 3, 6);
            AddLoot(T("LootTable_Galeao"), spice, 0.5f, 2, 4);
            AddLoot(T("LootTable_Cacador"), spice, 0.25f, 1, 2);
            // Os monstros do mar deixam o que sobrou dos navios que afundaram: velas e cabos.
            foreach (var file in new[] { "LootTable_Lula", "LootTable_Megalodonte" })
            {
                AddLoot(T(file), canvas, 0.6f, 3, 6);
                AddLoot(T(file), rope, 0.6f, 3, 6);
                AddLoot(T(file), tar, 0.4f, 2, 4);
            }
        }

        // ---------- R2: quem pede os materiais ----------

        /// <summary>
        /// R2 (03/11, tabela aprovada pelo Jhonata): os materiais novos <b>somados</b> aos custos de hoje, nível a nível. Obras = pedra
        /// (Alojamento também lona para as redes; Estaleiro também cordame e breu; Paiol também breu); melhorias do navio = lona e cordame no
        /// Velame, breu no Casco, cordame nos Canhões e nos de caça; navios novos = lona, cordame e breu. Artilheiros, Mira e o conserto
        /// ficam como estão. Põe só o item que o nível ainda não pede (rodar de novo não soma duas vezes).
        /// </summary>
        static void AddMaterialCosts()
        {
            var stone = StoneItem();
            var rope = RopeItem();
            var canvas = CanvasItem();
            var tar = TarItem();

            void Building(string file, params (ItemDefinition item, int[] perLevel)[] extra)
            {
                var b = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"{BuildingsFolder}/{file}.asset");
                if (!b) return;
                for (int lv = 0; lv < b.levels.Length; lv++)
                    b.levels[lv].cost = WithExtra(b.levels[lv].cost, extra, lv);
                EditorUtility.SetDirty(b);
            }
            void Upgrade(string file, params (ItemDefinition item, int[] perLevel)[] extra)
            {
                var u = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>($"{UpgradesFolder}/{file}.asset");
                if (!u) return;
                for (int lv = 0; lv < u.levels.Length; lv++)
                    u.levels[lv].cost = WithExtra(u.levels[lv].cost, extra, lv);
                EditorUtility.SetDirty(u);
            }
            void Ship(string file, params (ItemDefinition item, int[] perLevel)[] extra)
            {
                var s = AssetDatabase.LoadAssetAtPath<ShipClassDefinition>($"{ShipsFolder}/{file}.asset");
                if (!s) return;
                s.cost = WithExtra(s.cost, extra, 0);
                EditorUtility.SetDirty(s);
            }

            Building("Building_Alojamento", (stone, new[] { 10, 20, 35, 50 }), (canvas, new[] { 0, 5, 10, 15 }));
            Building("Building_Estaleiro", (stone, new[] { 20, 40 }), (rope, new[] { 10, 20 }), (tar, new[] { 5, 15 }));
            Building("Building_Forja", (stone, new[] { 15, 25, 40, 60 }));
            Building("Building_Paiol", (stone, new[] { 15, 25, 40, 60 }), (tar, new[] { 5, 8, 12, 18 }));
            Building("Building_Fortaleza", (stone, new[] { 60, 100 }));

            Upgrade("Upgrade_Velame", (canvas, new[] { 5, 10, 16, 25 }), (rope, new[] { 5, 8, 12, 20 }));
            Upgrade("Upgrade_Casco", (tar, new[] { 4, 8, 12, 20 }));
            Upgrade("Upgrade_Canhoes", (rope, new[] { 3, 6, 9, 15 }));
            Upgrade("Upgrade_CanhoesCaca", (rope, new[] { 6, 10 }));

            Ship("ShipClass_Escuna", (canvas, new[] { 30 }), (rope, new[] { 30 }), (tar, new[] { 15 }));
            Ship("ShipClass_Galeao", (canvas, new[] { 60 }), (rope, new[] { 60 }), (tar, new[] { 30 }));
        }

        static ItemStack[] WithExtra(ItemStack[] cost, (ItemDefinition item, int[] perLevel)[] extra, int level)
        {
            var list = new List<ItemStack>(cost ?? System.Array.Empty<ItemStack>());
            foreach (var (item, perLevel) in extra)
            {
                if (!item || level >= perLevel.Length || perLevel[level] <= 0) continue;
                if (list.Exists(c => c.item == item)) continue;
                list.Add(new ItemStack(item, perLevel[level]));
            }
            return list.ToArray();
        }

        // ---------- Ícones (low-poly, do mesmo jeito dos outros) ----------

        static void BakeMaterialIcons()
        {
            SetItemIcon("Item_Pedra", Icon("Item_Pedra", StoneIcon, new Vector3(-0.5f, 0.6f, 1f)));
            SetItemIcon("Item_Cordame", Icon("Item_Cordame", RopeIcon, new Vector3(-0.4f, 0.9f, 1f)));
            SetItemIcon("Item_Lona", Icon("Item_Lona", CanvasIcon, new Vector3(-0.5f, 0.6f, 1f)));
            SetItemIcon("Item_Breu", Icon("Item_Breu", TarIcon, new Vector3(-0.4f, 0.8f, 1f)));
            SetItemIcon("Item_Rum", Icon("Item_Rum", RumIcon, new Vector3(-0.5f, 0.45f, 1f)));
            SetItemIcon("Item_Especiarias", Icon("Item_Especiarias", SpiceIcon, new Vector3(-0.5f, 0.6f, 1f)));
        }

        /// <summary>Pedra cortada do Survival Kit, com uma menor do lado.</summary>
        static void StoneIcon(Transform t)
        {
            var gray = GetMaterial("M_Icone_Pedra", new Color(0.6f, 0.58f, 0.55f), 0.15f);
            var big = Model(Kit.Survival, "resource-stone-large", t, Vector3.zero, 20f, 1f, "Pedra");
            SetMaterial(big, gray);
            FitIcon(big, 0.8f, new Vector3(-0.08f, 0f, 0f));
            var small = Model(Kit.Survival, "resource-stone", t, Vector3.zero, 70f, 1f, "Pedrinha");
            SetMaterial(small, GetMaterial("M_Icone_PedraClara", new Color(0.7f, 0.68f, 0.64f), 0.15f));
            FitIcon(small, 0.38f, new Vector3(0.42f, 0f, 0.2f));
        }

        /// <summary>Rolo de corda: anéis de gomos em três voltas, um em cima do outro, e a ponta solta.</summary>
        static void RopeIcon(Transform t)
        {
            var rope = GetMaterial("M_Icone_Corda", new Color(0.78f, 0.62f, 0.36f), 0.1f);
            var dark = GetMaterial("M_Icone_CordaEscura", new Color(0.62f, 0.47f, 0.26f), 0.1f);
            for (int layer = 0; layer < 3; layer++)
            {
                float radius = 0.34f - layer * 0.03f;
                const int beads = 14;
                for (int k = 0; k < beads; k++)
                {
                    float a = (k + layer * 0.5f) / beads * Mathf.PI * 2f;
                    var at = new Vector3(Mathf.Cos(a) * radius, 0.06f + layer * 0.1f, Mathf.Sin(a) * radius);
                    var rot = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))) * Quaternion.Euler(90f, 0f, 0f);
                    PrismPart(6, $"Gomo_{layer}_{k}", t, at, new Vector3(0.11f, 0.09f, 0.11f), rot, (k + layer) % 2 == 0 ? rope : dark);
                }
            }
            PrismPart(6, "Ponta", t, new Vector3(0.36f, 0.06f, -0.3f), new Vector3(0.1f, 0.18f, 0.1f), Quaternion.Euler(80f, 40f, 0f), rope);
        }

        /// <summary>Rolo de lona deitado em cima de um pano dobrado, com uma amarra.</summary>
        static void CanvasIcon(Transform t)
        {
            var cloth = GetMaterial("M_Icone_Lona", new Color(0.9f, 0.86f, 0.74f), 0.1f);
            var shade = GetMaterial("M_Icone_LonaSombra", new Color(0.78f, 0.74f, 0.62f), 0.1f);
            Part(PrimitiveType.Cube, "Dobrado", t, new Vector3(0f, 0.07f, 0f), new Vector3(0.9f, 0.14f, 0.6f), Quaternion.Euler(0f, 10f, 0f), shade);
            PrismPart(10, "Rolo", t, new Vector3(0f, 0.34f, 0f), new Vector3(0.4f, 0.42f, 0.4f), Quaternion.Euler(0f, 10f, 90f), cloth);
            var rope = GetMaterial("M_Icone_Corda", new Color(0.78f, 0.62f, 0.36f), 0.1f);
            PrismPart(10, "Amarra", t, new Vector3(0f, 0.34f, 0f), new Vector3(0.44f, 0.05f, 0.44f), Quaternion.Euler(0f, 10f, 90f), rope);
        }

        /// <summary>Balde do Survival Kit cheio de piche preto, com uma gota escorrendo.</summary>
        static void TarIcon(Transform t)
        {
            var bucket = Model(Kit.Survival, "bucket", t, Vector3.zero, 30f, 1f, "Balde");
            var b = FitIcon(bucket, 0.75f, Vector3.zero);
            var tar = GetMaterial("M_Icone_Breu", new Color(0.12f, 0.1f, 0.09f), 0.8f);
            var top = t.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            PrismPart(12, "Piche", t, top + Vector3.down * 0.08f, new Vector3(b.size.x * 0.82f, 0.06f, b.size.z * 0.82f), Quaternion.identity, tar);
            Part(PrimitiveType.Sphere, "Gota", t, top + new Vector3(b.size.x * 0.42f, -0.18f, 0f), new Vector3(0.09f, 0.16f, 0.09f), Quaternion.identity, tar);
        }

        /// <summary>Garrafa de rum do Pirate Kit (vidro escuro) e uma deitada atrás.</summary>
        static void RumIcon(Transform t)
        {
            var glass = GetMaterial("M_Icone_Rum", new Color(0.45f, 0.24f, 0.1f), 0.8f);
            var lying = Model(Kit.Pirate, "bottle", t, Vector3.zero, Quaternion.Euler(0f, 35f, 90f), 1f, "Deitada");
            SetMaterial(lying, glass);
            FitIcon(lying, 0.7f, new Vector3(0.1f, 0f, 0.25f));
            var bottle = Model(Kit.Pirate, "bottle-large", t, Vector3.zero, 0f, 1f, "Garrafa");
            FitIcon(bottle, 0.95f, new Vector3(-0.12f, 0f, -0.1f));
        }

        /// <summary>Saco de especiarias do Food Kit, laranja, com o pilão do lado.</summary>
        static void SpiceIcon(Transform t)
        {
            FoodPiece("bag", t, new Vector3(-0.1f, 0f, 0f), 20f, 0.7f, GetMaterial("M_Icone_Especiaria", new Color(0.86f, 0.48f, 0.16f), 0.1f));
            FoodPiece("mortar-pestle", t, new Vector3(0.38f, 0f, 0.2f), 0f, 0.38f);
        }
    }
}
