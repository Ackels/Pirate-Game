using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// "Mais coisas no porto" (M9, cenas com porto): munições especiais (itens Carga de corrente/metralha + Data/Ammo, ShipAmmo
    /// no navio, teclas 1/2/3), carpinteiro a bordo (ShipCarpenter, tecla R, números no CrewSettings), preços do mercado da praça
    /// e receitas do ferreiro da Forja (no HarborSettings_Base). Assets e listas que já existem são preservados (ajuste de game
    /// design); as listas só são preenchidas vazias. Preços pela revisão do chat de otimização (04/10): comprar ~2× o que o
    /// mercado paga; o mercado só compra até a demanda (20 un. por item, volta 1 a cada 45 s), senão a produção da Forja e do
    /// Paiol vendida virava ouro infinito; munição não é recomprada (fazer e vender daria lucro).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string AmmoFolder = DataFolder + "/Ammo";

        sealed class SupplyAssets
        {
            public ItemDefinition chainItem, grapeItem, fireItem;
            public AmmoDefinition chain, grape, fire;
        }

        static SupplyAssets LoadSupplies()
        {
            EnsureFolder(DataFolder, "Ammo");
            var s = new SupplyAssets
            {
                chainItem = Item("Item_CargaCorrente", "Carga de corrente", ItemCategory.Resource, ItemRarity.Uncommon, new Color(0.66f, 0.72f, 0.82f), 1f,
                    "Duas meias-balas presas por corrente: rasgam as velas e o navio acertado fica lento. Pouco dano no casco. " +
                    "Uma carga por bordada (tecla 2 no navio)."),
                grapeItem = Item("Item_CargaMetralha", "Carga de metralha", ItemCategory.Resource, ItemRarity.Uncommon, new Color(0.9f, 0.62f, 0.45f), 1f,
                    "Saco de balinhas de ferro: espalha muito e alcança pouco (60 m), mas de perto faz mais dano e abate a tripulação " +
                    "inimiga (ela recarrega mais devagar). Uma carga por bordada (tecla 3 no navio)."),
                fireItem = IncendiaryItem(),
            };
            // Munição fica a bordo ao desembarcar no porto (o "descarregar tudo" não leva).
            foreach (var item in new[] { s.chainItem, s.grapeItem, s.fireItem })
                if (item.keepAboard == 0)
                {
                    item.keepAboard = 9999;
                    EditorUtility.SetDirty(item);
                }
            s.chain = LoadOrCreateAsset<AmmoDefinition>(AmmoFolder + "/Ammo_Corrente.asset", a =>
            {
                a.displayName = "Corrente";
                a.damageMultiplier = 0.5f;      // 6 por bala na chalupa
                a.spreadMultiplier = 1.3f;
                a.sailDamagePerHit = 0.05f;     // bordada de 4 acertando: −20% de velocidade (até −50%)
            });
            s.chain.chainVisual = true;
            EditorUtility.SetDirty(s.chain);
            s.grape = LoadOrCreateAsset<AmmoDefinition>(AmmoFolder + "/Ammo_Metralha.asset", a =>
            {
                a.displayName = "Metralha";
                a.damageMultiplier = 1.5f;      // 18 por canhão na chalupa, em 4 balinhas de 4,5
                a.spreadMultiplier = 3.5f;
                a.maxRange = 60f;
                a.pellets = 4;
                a.visualScale = 0.45f;
                a.crewDamagePerHit = 0.02f;     // bordada de 16 balinhas acertando: recarga +32% (até +60%)
            });
            // Incendiária (30/10, óleo de cavala): menos dano na hora, mas o navio acertado queima 4 por segundo por 8 s (um acerto novo
            // recomeça; com 8/s por 10 s uma bordada quase afundava a chalupa sozinha). Refeita a cada geração (a tabela manda).
            s.fire = LoadOrCreateAsset<AmmoDefinition>(AmmoFolder + "/Ammo_Incendiaria.asset", a => a.displayName = "Incendiária");
            s.fire.damageMultiplier = 0.8f;
            s.fire.spreadMultiplier = 1.1f;
            s.fire.burnPerSecond = 4f;
            s.fire.burnSeconds = 8f;
            EditorUtility.SetDirty(s.fire);
            return s;
        }

        /// <summary>Navio do jogador: munição, carpinteiro e as teclas deles (depois do ShipCrew/ShipUpgrades).</summary>
        static void AddPlayerSupplies(ShipController player, SupplyAssets s, LootItems items)
        {
            player.gameObject.AddComponent<ShipAmmo>().Configure(new[]
            {
                new ShipAmmo.Slot { ammo = s.chain, item = s.chainItem },
                new ShipAmmo.Slot { ammo = s.grape, item = s.grapeItem },
                new ShipAmmo.Slot { ammo = s.fire, item = s.fireItem },
            });
            var crew = player.GetComponent<ShipCrew>();
            if (crew && crew.Settings)
            {
                crew.Settings.carpenterWood = items.wood;
                EditorUtility.SetDirty(crew.Settings);
                // O carpinteiro guarda 10 madeiras a bordo ao desembarcar (dá para descarregar à mão).
                if (items.wood.keepAboard == 0)
                {
                    items.wood.keepAboard = 10;
                    EditorUtility.SetDirty(items.wood);
                }
                player.gameObject.AddComponent<ShipCarpenter>();
            }
            player.gameObject.AddComponent<PlayerShipExtrasInput>();
        }

        /// <summary>Mercado da praça e ferreiro da Forja no HarborSettings (listas vazias são preenchidas; o resto é preservado).</summary>
        static void ConfigureHarborTrade(HarborSettings settings, LootItems i, SupplyAssets s, BuildingDefinition forge)
        {
            if (settings.market == null || settings.market.Length == 0)
                settings.market = new[]
                {
                    Market(i.wood, 10, 4),
                    Market(i.powder, 12, 5),
                    Market(i.iron, 20, 8),
                    // Mais caro que fazer na Forja, mesmo com ferro e pólvora comprados aqui (14 e ~12,7 por carga).
                    Market(s.chainItem, 18, 0, toHold: true),
                    Market(s.grapeItem, 16, 0, toHold: true),
                };
            if (settings.smithy == null || settings.smithy.Length == 0)
                settings.smithy = new[]
                {
                    // 6 cargas por receita. Com ferro e pólvora comprados no mercado (20 e 12): corrente 14 e metralha ~12,7 por
                    // carga (o mercado vende a 18 e 16); com o que a Forja e o Paiol produzem, sai bem mais barato.
                    new HarborSettings.Recipe
                    {
                        result = new ItemStack(s.chainItem, 6),
                        cost = new[] { new ItemStack(i.iron, 3), new ItemStack(i.powder, 2) },
                        requiredBuilding = forge, requiredLevel = 1,
                    },
                    new HarborSettings.Recipe
                    {
                        result = new ItemStack(s.grapeItem, 6),
                        cost = new[] { new ItemStack(i.iron, 2), new ItemStack(i.powder, 3) },
                        requiredBuilding = forge, requiredLevel = 2,
                        craftSeconds = 120f,
                    },
                };
            AddRationMarket(settings);
            AddBackpackMarket(settings);
            AddCartridgeTrade(settings, i, forge);
            AddBlunderbussTrade(settings, i, forge);
            AddBombTrade(settings, i, forge);
            AddPreparationRecipes(settings, i, forge);
            AddFishingMarket(settings);
            AddNetMarket(settings);
            AddKitchenRecipes(settings);
            AddBaitRecipe(settings);
            AddGatheringMarket(settings);
            // Forja com tempo (05/10): receitas de antes nasceram com o padrão (90 s); a metralha leva 2 min. Só troca o padrão.
            foreach (var recipe in settings.smithy)
                if (recipe?.result.item == s.grapeItem && Mathf.Approximately(recipe.craftSeconds, 90f)) recipe.craftSeconds = 120f;
            EditorUtility.SetDirty(settings);
        }

        static HarborSettings.MarketEntry Market(ItemDefinition item, int buy, int sell, bool toHold = false) => new()
        {
            item = item, buyPrice = buy, sellPrice = sell, toHold = toHold, demand = 20, demandRefillSeconds = 45f,
        };

        /// <summary>Porta do ferreiro na Forja (dentro do grupo do nível 1: só aparece com a Forja construída).</summary>
        static void SmithyDoor(Transform group, Transform forge)
        {
            var door = forge.Find("Porta");
            var front = (door ? door.position : forge.position) + forge.forward * 1.6f;
            var ground = PortGround(new Vector2(front.x - IslandCenter.x, front.z - IslandCenter.z));
            var entrance = new GameObject("Entrada_Forja");
            entrance.transform.SetParent(group, false);
            entrance.transform.SetPositionAndRotation(ground, forge.rotation);
            entrance.AddComponent<HarborBuilding>().Configure("Forja", "munições especiais", HarborService.Smithy, 3f, 3.2f);
        }

        /// <summary>Banca do mercado da praça: entrada na frente do balcão (virado para o chafariz) e o vendedor atrás.</summary>
        static void MarketStall(Transform village, Transform stall)
        {
            var front = stall.position + stall.forward * 2.4f;
            var ground = PortGround(new Vector2(front.x - IslandCenter.x, front.z - IslandCenter.z));
            var entrance = new GameObject("Entrada_Mercado");
            entrance.transform.SetParent(village, false);
            entrance.transform.SetPositionAndRotation(ground, stall.rotation);
            entrance.AddComponent<HarborBuilding>().Configure("Mercado", "comprar e vender", HarborService.Market, 3f, 4f);
            var behind = stall.position - stall.forward * 1.3f;
            serviceDoors.Add((PortGround(new Vector2(behind.x - IslandCenter.x, behind.z - IslandCenter.z)), stall.eulerAngles.y));
        }

        // ---------- Nível 4 (M9): mais um nível nas construções que tinham 3, nível 2 no Estaleiro e na Fortaleza ----------

        /// <summary>
        /// Acrescenta os níveis novos a uma construção que já existe com <paramref name="current"/> níveis (preservada: só cresce
        /// se ainda está do tamanho antigo). Custos ~1,6× o nível anterior.
        /// </summary>
        static void AddBuildingLevels(BuildingDefinition b, int current, params BuildingDefinition.Level[] extra)
        {
            if (!b || b.levels.Length != current) return;
            var list = new System.Collections.Generic.List<BuildingDefinition.Level>(b.levels);
            list.AddRange(extra);
            b.levels = list.ToArray();
            EditorUtility.SetDirty(b);
        }

        static void AddLevelFour(BuildingAssets b, LootItems items)
        {
            var g = items.gold;
            var w = items.wood;
            var i = items.iron;
            var p = items.powder;
            AddBuildingLevels(b.lodging, 3, BLvl(2.5f, 130f, (w, 110), (i, 20), (g, 420)));
            AddBuildingLevels(b.forge, 3, BLvl(9f, 130f, (w, 95), (i, 29), (g, 450)));
            AddBuildingLevels(b.magazine, 3, BLvl(9f, 130f, (w, 105), (i, 22), (g, 450)));
            // Estaleiro: valor = nível dos upgrades liberado (o nível 1 antigo tem 0 = nível 3).
            AddBuildingLevels(b.shipyard, 1, BLvl(4f, 120f, (w, 80), (i, 25), (g, 400)));
            if (b.shipyard && b.shipyard.levels.Length > 0 && b.shipyard.levels[0].value <= 0f)
            {
                b.shipyard.levels[0].value = 3f;
                EditorUtility.SetDirty(b.shipyard);
            }
            if (b.shipyard && b.shipyard.description.Contains("no futuro"))
            {
                b.shipyard.description = "Carreira e guindastes para obras grandes: libera navios maiores e o nível 3 dos upgrades do navio " +
                                         "(nível 2: o nível 4 dos upgrades).";
                EditorUtility.SetDirty(b.shipyard);
            }
            // Fortaleza: valor = metros a mais na zona segura (nível 2 no lugar do 1, não somado).
            AddBuildingLevels(b.fortress, 1, BLvl(FortressZoneBonus2, 150f, (w, 90), (i, 45), (p, 30), (g, 480)));
        }

        const float FortressZoneBonus2 = 45f;
    }
}
