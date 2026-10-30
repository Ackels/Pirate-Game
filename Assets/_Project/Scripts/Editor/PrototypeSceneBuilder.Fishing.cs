using System.Collections.Generic;
using Pirates.Base;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Progression;
using Pirates.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Pesca com vara (D2, passo 1, 27/10; pedido do Jhonata: "vamos fazer todos eles, mas pode começar pelo da pesca"): a vara de pesca
    /// (item no mercado), os peixes (itens com modelo, raridade, preço de venda e quanto curam comidos), o <see cref="FishingRod"/> no
    /// capitão com a vara, a boia, a linha e os modelos dos peixes montados aqui, os estados do Animator (Human Crafting Animations do
    /// Kevin Iglesias, Asset Store: lançar, esperar, brigar e puxar; antes eram o "Fishing Cast" e o "Fishing Idle" do Mixamo) e o
    /// <see cref="FishingHUD"/>. Modelos: cavala e atum do Pirate Kit do Quaternius (CC0, copiados do _Downloads), arenque
    /// do Quirky Series e os bichos do mar do Pixit3D Fish (Asset Store, em ThirdParty/Loja).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Kevin Iglesias (medido no capitão): "FishingThrow01" 1,3 s (sobe a vara atrás da cabeça e chicoteia para a frente aos ~0,5 s;
        // das mãos soltas à pose de segurar a vara), "Fishing01 - Loop" 4 s, "FishingFighting01" 2,07 s em loop (trancos para cima),
        // "FishingPullOut01" 1,57 s (puxão para cima até ~0,8 s, depois as mãos descem para os lados).
        const string KevinWorkFolder = "Assets/ThirdParty/Loja/KevinIglesias/Human Animations/Animations/Male/Work";
        const string ClipFishThrow = "FishingThrow01", ClipFishWait = "Fishing01 - Loop", ClipFishFight = "FishingFighting01",
            ClipFishPull = "FishingPullOut01";

        const string PixitFolder = "Assets/ThirdParty/Loja/Pixit_FishFree";
        const string QuirkyFolder = "Assets/ThirdParty/Loja/Quirky_Animals";

        sealed class FishSpec
        {
            public string file, name, description;
            public ItemRarity rarity;
            public Color color;
            public float space, heal, weight, shallow = 1f, deep = 1f, size = 0.45f;
            public int sell;
            /// <summary>Modelo do Pirate Kit do Quaternius (Models/&lt;kit&gt;.fbx) ou prefab de um pacote da Loja (pasta + nome).</summary>
            public string kit, folder, prefab;
            public Quaternion pose = Quaternion.identity;
            /// <summary>Cor por cima da do modelo (peixes novos feitos de outro: garoupa, pargo...) e brilho (peixe-lanterna).</summary>
            public Color tint = Color.white, glow = Color.black;
            /// <summary>Cor lisa no lugar da textura do modelo (a cobra do Quirky é marrom-avermelhada: enguia e serpente; o atum prateado).</summary>
            public bool plain;
            /// <summary>Estica o modelo no comprimento (peixe-espada = atum esticado).</summary>
            public float stretch = 1f;
            /// <summary>Tamanho de verdade sorteado a cada peixe (m): o diário guarda o maior.</summary>
            public Vector2 length = new(0.2f, 0.4f);
            /// <summary>Dica do diário: onde e quando morde.</summary>
            public string where = "";
            /// <summary>Lendário: um só (some do sorteio depois de pescado), só na vara.</summary>
            public bool legendary;
            public FishWhen when;
        }

        // Regiões do diário do pescador (círculos em volta das ilhas e das Águas Negras; AddFishJournal).
        const string RegionPort = "porto", RegionSkull = "caveira", RegionNorth = "norte", RegionWest = "oeste", RegionErmida = "ermida",
            RegionDark = "aguas_negras";

        static FishWhen When(FishTime time = FishTime.Any, FishWeather weather = FishWeather.Any, bool once = false, params string[] regions) =>
            new() { time = time, weather = weather, once = once, regions = regions };

        // Peso no sorteio (soma ~110 sem os presos a lugar/hora): comuns ~70%, incomuns ~18%, raros ~6%, pérola ~1%. No raso (até 1,4 m)
        // caranguejo, camarão, estrela, pepino, ouriço e concha; no fundo (2,6 m+) atum, polvo e a ostra. Venda: um peixe comum vale 1–4 de
        // ouro, a pérola 80; o mercado aceita 12 de cada (volta 1 por minuto). Cura comido (R, sem ração): 8–35.
        // Novos (30/10, pedido do Jhonata: "mais tipos de peixes"): garoupa (Ilha Norte e Caveira) e pargo (Porto Alento e Ilha Oeste) presos
        // a lugar; lula e peixe-lanterna só à noite; peixe-espada só na tempestade; enguia negra na Ermida (e na rede, nas Águas Negras); e um
        // lendário por ilha (peso 2: ~2% das mordidas no lugar e na hora certos, briga difícil, um só).
        static readonly FishSpec[] FishSpecs =
        {
            new() { file = "Item_Peixe_Cavala", name = "Cavala", rarity = ItemRarity.Common, color = new Color(0.55f, 0.72f, 0.85f),
                space = 0.4f, sell = 2, heal = 15f, weight = 24f, size = 0.5f, kit = "Prop_Fish_Mackerel", length = new(0.25f, 0.45f),
                where = "Em qualquer água.",
                description = "Peixe listrado de cardume, o pão de cada dia do pescador. Assada na brasa, cura um pouco." },
            new() { file = "Item_Peixe_Arenque", name = "Arenque", rarity = ItemRarity.Common, color = new Color(0.6f, 0.78f, 0.9f),
                space = 0.3f, sell = 2, heal = 12f, weight = 20f, shallow = 1.2f, deep = 0.8f, size = 0.42f, folder = QuirkyFolder, prefab = "Herring",
                length = new(0.18f, 0.32f), where = "Em qualquer água, mais no raso.",
                description = "Pequeno e prateado. Salgado num barril, atravessa meio mar sem estragar." },
            new() { file = "Item_Peixe_Camarao", name = "Camarão", rarity = ItemRarity.Common, color = new Color(0.95f, 0.55f, 0.45f),
                space = 0.2f, sell = 1, heal = 8f, weight = 12f, shallow = 2f, deep = 0.4f, size = 0.3f, folder = PixitFolder, prefab = "Shrimp_Rigged_Unity",
                length = new(0.06f, 0.14f), where = "No raso das praias.",
                description = "Pula na isca da beira da praia. Pouca carne, mas é comida." },
            new() { file = "Item_Peixe_Caranguejo", name = "Caranguejo", rarity = ItemRarity.Common, color = new Color(0.9f, 0.35f, 0.3f),
                space = 0.3f, sell = 3, heal = 12f, weight = 9f, shallow = 2.5f, deep = 0.3f, size = 0.38f, folder = PixitFolder, prefab = "Crab_Rigged_Unity",
                length = new(0.1f, 0.24f), where = "No raso, perto das pedras.",
                description = "Agarrou a isca com a pinça e não largou mais. Mora no raso, perto das pedras." },
            new() { file = "Item_Peixe_Estrela", name = "Estrela-do-mar", rarity = ItemRarity.Common, color = new Color(0.95f, 0.6f, 0.3f),
                space = 0.2f, sell = 1, weight = 6f, shallow = 3f, deep = 0.2f, size = 0.36f, folder = PixitFolder, prefab = "Starfish_Rigged_Unity",
                length = new(0.12f, 0.3f), where = "No raso das praias.",
                description = "Não se come, mas o mercado paga uns trocados: os marujos penduram como enfeite na cabine." },
            new() { file = "Item_Peixe_Pepino", name = "Pepino-do-mar", rarity = ItemRarity.Common, color = new Color(0.55f, 0.45f, 0.35f),
                space = 0.3f, sell = 2, heal = 10f, weight = 7f, shallow = 2.5f, deep = 0.3f, size = 0.4f, folder = PixitFolder, prefab = "SeaCucumber_Rigged_Unity",
                length = new(0.15f, 0.4f), where = "No raso das praias.",
                description = "Mole e feio, mas os cozinheiros do oriente pagam por ele seco. Cozido, mata a fome." },
            new() { file = "Item_Peixe_Ourico", name = "Ouriço-do-mar", rarity = ItemRarity.Common, color = new Color(0.4f, 0.25f, 0.45f),
                space = 0.3f, sell = 4, weight = 6f, shallow = 2.2f, deep = 0.3f, size = 0.3f, folder = PixitFolder, prefab = "SeaUrchin_Static_Unity",
                length = new(0.06f, 0.14f), where = "No raso, perto das pedras.",
                description = "Bola de espinhos que prendeu no anzol. Não se come assim: o boticário usa os espinhos." },
            new() { file = "Item_Peixe_Concha", name = "Concha", rarity = ItemRarity.Common, color = new Color(0.95f, 0.8f, 0.65f),
                space = 0.1f, sell = 3, weight = 7f, shallow = 2.5f, deep = 0.3f, size = 0.3f, folder = PixitFolder, prefab = "SeaShell_Static_Unity",
                length = new(0.05f, 0.16f), where = "No raso das praias.",
                description = "Concha vazia que veio enroscada na linha. Bonita: os mercadores fazem colares." },
            new() { file = "Item_Peixe_Baiacu", name = "Baiacu", rarity = ItemRarity.Uncommon, color = new Color(0.55f, 0.8f, 0.9f),
                space = 0.3f, sell = 5, weight = 8f, size = 0.38f, folder = PixitFolder, prefab = "BabyPutterfish_Rigged_Unity",
                length = new(0.15f, 0.35f), where = "Em qualquer água.",
                description = "Incha quando se assusta. Venenoso: não coma. O boticário do porto paga bem pelo veneno." },
            new() { file = "Item_Peixe_Siba", name = "Siba", rarity = ItemRarity.Uncommon, color = new Color(0.45f, 0.6f, 0.95f),
                space = 0.3f, sell = 6, heal = 18f, weight = 7f, shallow = 0.8f, deep = 1.3f, size = 0.42f, folder = PixitFolder, prefab = "Cuttlefish_Rigged_Unity",
                length = new(0.18f, 0.4f), where = "Em qualquer água, mais no fundo.",
                description = "Solta tinta quando sai da água. A carne é macia, e a tinta vira tinta de escrever cartas." },
            new() { file = "Item_Peixe_Atum", name = "Atum", rarity = ItemRarity.Uncommon, color = new Color(0.4f, 0.5f, 0.7f),
                space = 1f, sell = 8, heal = 30f, weight = 5f, shallow = 0.2f, deep = 2.5f, size = 0.8f, kit = "Prop_Fish_Tuna",
                length = new(0.8f, 1.8f), where = "Água funda: ponta do píer ou a rede do navio.",
                description = "Peixe grande de água funda: só morde longe da praia, na ponta do píer. Alimenta uma tripulação." },
            new() { file = "Item_Peixe_Garoupa", name = "Garoupa", rarity = ItemRarity.Uncommon, color = new Color(0.62f, 0.45f, 0.3f),
                space = 0.8f, sell = 10, heal = 28f, weight = 8f, shallow = 0.4f, deep = 1.6f, size = 0.7f, kit = "Prop_Fish_Tuna",
                tint = new Color(0.78f, 0.55f, 0.36f), length = new(0.5f, 1.3f), when = When(regions: new[] { RegionNorth, RegionSkull }),
                where = "Ilha Norte e Ilha da Caveira, na água funda perto das pedras.",
                description = "Bocuda e pintada, mora nas tocas de pedra das ilhas grandes. Carne branca, cura bem." },
            new() { file = "Item_Peixe_Pargo", name = "Pargo vermelho", rarity = ItemRarity.Uncommon, color = new Color(0.9f, 0.35f, 0.3f),
                space = 0.6f, sell = 9, heal = 24f, weight = 9f, shallow = 0.7f, deep = 1.3f, size = 0.55f, kit = "Prop_Fish_Mackerel",
                tint = new Color(1f, 0.42f, 0.36f), length = new(0.3f, 0.7f), when = When(regions: new[] { RegionPort, RegionWest }),
                where = "Porto Alento e Ilha Oeste.",
                description = "Vermelho como brasa. A taverna do porto põe na grelha nos dias de festa." },
            new() { file = "Item_Peixe_Lula", name = "Lula", rarity = ItemRarity.Uncommon, color = new Color(0.9f, 0.65f, 0.7f),
                space = 0.4f, sell = 7, heal = 20f, weight = 10f, shallow = 0.5f, deep = 1.5f, size = 0.5f, folder = QuirkyFolder, prefab = "Squid_LOD0",
                length = new(0.2f, 0.5f), when = When(FishTime.Night), where = "Só à noite, em qualquer água.",
                description = "Sobe da escuridão atrás da luz da lua. Prima pequena e inofensiva da lula das Águas Negras." },
            new() { file = "Item_Peixe_Polvo", name = "Polvo", rarity = ItemRarity.Rare, color = new Color(0.75f, 0.4f, 0.85f),
                space = 0.5f, sell = 16, heal = 25f, weight = 4f, shallow = 0.5f, deep = 1.6f, size = 0.5f, folder = PixitFolder, prefab = "Octopus_Rigged_Unity",
                length = new(0.4f, 1.2f), where = "Água funda.",
                description = "Oito braços e muita esperteza: escapa de quem fisga devagar. A taverna do porto paga caro." },
            new() { file = "Item_Peixe_CavaloMarinho", name = "Cavalo-marinho", rarity = ItemRarity.Rare, color = new Color(0.85f, 0.65f, 0.35f),
                space = 0.1f, sell = 20, weight = 3f, shallow = 1.3f, deep = 0.7f, size = 0.36f, folder = PixitFolder, prefab = "Seahorse_Rigged_Unity",
                length = new(0.08f, 0.2f), where = "No raso, raro.",
                description = "Dizem que dá sorte a bordo. Os mercadores de amuletos compram para secar ao sol." },
            new() { file = "Item_Peixe_Lanterna", name = "Peixe-lanterna", rarity = ItemRarity.Rare, color = new Color(0.3f, 0.75f, 0.95f),
                space = 0.2f, sell = 22, weight = 4f, shallow = 0.4f, deep = 1.6f, size = 0.4f, folder = QuirkyFolder, prefab = "Herring",
                tint = new Color(0.32f, 0.42f, 0.72f), glow = new Color(0.12f, 0.6f, 0.85f), length = new(0.08f, 0.18f),
                when = When(FishTime.Night), where = "Só à noite, na água funda.",
                description = "Brilha azul no escuro. Não se come: os marujos guardam num pote para ver no porão sem acender lampião." },
            new() { file = "Item_Peixe_Espada", name = "Peixe-espada", rarity = ItemRarity.Rare, color = new Color(0.7f, 0.82f, 0.95f),
                space = 1f, sell = 26, heal = 35f, weight = 6f, shallow = 0.3f, deep = 1.8f, size = 0.95f, kit = "Prop_Fish_Tuna", stretch = 1.8f,
                tint = new Color(0.78f, 0.88f, 1.05f), length = new(1.2f, 2.6f), when = When(weather: FishWeather.Storm),
                where = "Só na tempestade, na água funda.",
                description = "Só sobe quando o mar ferve na tempestade. Comprido e prateado: rende um banquete." },
            new() { file = "Item_Peixe_Enguia", name = "Enguia negra", rarity = ItemRarity.Rare, color = new Color(0.25f, 0.22f, 0.3f),
                space = 0.6f, sell = 18, heal = 22f, weight = 6f, shallow = 0.6f, deep = 1.4f, size = 0.8f, folder = QuirkyFolder, prefab = "Taipan_LOD0",
                plain = true, tint = new Color(0.17f, 0.17f, 0.21f), length = new(0.6f, 1.6f), when = When(regions: new[] { RegionErmida, RegionDark }),
                where = "Ilha da Ermida; na rede, perto das Águas Negras.",
                description = "Escorregadia e escura como o fundo das Águas Negras. Defumada, é a ceia dos pescadores velhos." },
            new() { file = "Item_Perola", name = "Pérola", rarity = ItemRarity.Epic, color = new Color(0.95f, 0.92f, 1f),
                space = 0.05f, sell = 80, weight = 1.2f, shallow = 0.6f, deep = 1.6f, size = 0.36f, folder = PixitFolder, prefab = "PearlSeashell_Rigged_Unity",
                length = new(0.006f, 0.018f), where = "Água funda, muito rara.",
                description = "Uma ostra fisgada por acaso, com uma pérola grande lá dentro. Vale uma pequena fortuna no mercado." },

            // ---------- Lendários: um por ilha ----------
            new() { file = "Item_Peixe_ReiDePrata", name = "Rei de Prata", rarity = ItemRarity.Legendary, color = new Color(0.9f, 0.92f, 1f),
                space = 1f, sell = 300, weight = 2f, shallow = 0.3f, deep = 1.5f, size = 1.3f, kit = "Prop_Fish_Tuna", legendary = true,
                plain = true, tint = new Color(0.82f, 0.86f, 0.94f), glow = new Color(0.16f, 0.18f, 0.24f), length = new(2.2f, 3f),
                when = When(once: true, regions: new[] { RegionPort }), where = "Porto Alento, na água funda (ponta do píer).",
                description = "O atum de escamas de prata que os pescadores de Porto Alento juram ter visto no tempo da Frota de Prata. Troféu: vale uma fortuna." },
            new() { file = "Item_Peixe_VelhaBocuda", name = "Velha Bocuda", rarity = ItemRarity.Legendary, color = new Color(0.55f, 0.4f, 0.28f),
                space = 1f, sell = 260, weight = 2f, shallow = 0.3f, deep = 1.5f, size = 1.25f, kit = "Prop_Fish_Tuna", legendary = true,
                tint = new Color(0.55f, 0.4f, 0.28f), length = new(1.8f, 2.6f), when = When(once: true, regions: new[] { RegionNorth }),
                where = "Ilha Norte, na água funda.",
                description = "Uma garoupa do tamanho de um bote, que engole anzol e linha há cem anos. Agora está na sua mochila." },
            new() { file = "Item_Peixe_DamaRubra", name = "Dama Rubra", rarity = ItemRarity.Legendary, color = new Color(0.95f, 0.25f, 0.3f),
                space = 1f, sell = 240, weight = 2f, shallow = 0.4f, deep = 1.4f, size = 1f, kit = "Prop_Fish_Mackerel", legendary = true,
                tint = new Color(1f, 0.25f, 0.3f), glow = new Color(0.3f, 0f, 0.05f), length = new(1f, 1.5f), when = When(once: true, regions: new[] { RegionWest }),
                where = "Ilha Oeste.",
                description = "O pargo rubro dos contrabandistas da enseada: dizem que quem a pesca nunca mais erra o caminho de casa." },
            new() { file = "Item_Peixe_Fantasma", name = "Peixe-Fantasma", rarity = ItemRarity.Legendary, color = new Color(0.7f, 1f, 0.8f),
                space = 1f, sell = 320, weight = 2f, shallow = 0.4f, deep = 1.4f, size = 0.9f, folder = QuirkyFolder, prefab = "Herring", legendary = true,
                plain = true, tint = new Color(0.78f, 0.95f, 0.85f), glow = new Color(0.2f, 0.7f, 0.38f), length = new(0.8f, 1.3f),
                when = When(FishTime.Night, once: true, regions: new[] { RegionSkull }), where = "Ilha da Caveira, só à noite.",
                description = "Pálido e verde como os mortos do cemitério da Caveira. Atravessa a rede como se não existisse." },
            new() { file = "Item_Peixe_Serpente", name = "Serpente da Ermida", rarity = ItemRarity.Legendary, color = new Color(0.2f, 0.45f, 0.4f),
                space = 1f, sell = 280, weight = 2f, shallow = 0.4f, deep = 1.4f, size = 1.3f, folder = QuirkyFolder, prefab = "Taipan_LOD0", legendary = true,
                plain = true, tint = new Color(0.16f, 0.4f, 0.33f), glow = new Color(0f, 0.22f, 0.16f), length = new(2.5f, 4f),
                when = When(weather: FishWeather.Rain, once: true, regions: new[] { RegionErmida }), where = "Ilha da Ermida, na chuva.",
                description = "A enguia gigante que os monges de São Telmo pintaram na parede da cripta. Só sobe quando chove." },
        };

        static ItemDefinition FishItem(FishSpec f)
        {
            var item = Item(f.file, f.name, ItemCategory.Resource, f.rarity, f.color, f.space, f.description);
            if (!Mathf.Approximately(item.foodHeal, f.heal))
            {
                item.foodHeal = f.heal;
                EditorUtility.SetDirty(item);
            }
            // Peixe que não se come (estrela, ouriço, lendários) continua no filtro "Comida e pesca" (a pérola é tesouro).
            if (item.group == ItemGroup.Auto && f.heal <= 0f && f.file != "Item_Perola")
            {
                item.group = ItemGroup.Food;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        static IEnumerable<ItemDefinition> FishItems()
        {
            foreach (var f in FishSpecs) yield return FishItem(f);
            yield return RodItem();
            yield return NetItem();
            yield return GoldRodItem();
            yield return StrongNetItem();
        }

        static ItemDefinition RodItem() => Item("Equip_VaraPesca", "Vara de pesca", ItemCategory.Gear, ItemRarity.Common, new Color(0.7f, 0.52f, 0.3f), 0f,
            "Vara de bambu com carretilha de latão. A pé, de frente para a água funda (beira da praia, píer), F lança; quando a boia afundar, " +
            "F de novo para fisgar. Os peixes vendem no mercado e os comuns matam a fome (R, sem ração).");

        /// <summary>Prêmio do diário do pescador (10 espécies): vale como vara, fisga com mais tempo e a barra da briga fica maior.</summary>
        static ItemDefinition GoldRodItem() => Item("Equip_VaraOuro", "Vara de ouro", ItemCategory.Gear, ItemRarity.Epic, new Color(1f, 0.8f, 0.3f), 0f,
            "Prêmio do diário do pescador. Bambu escolhido e carretilha de ouro: o peixe dá mais tempo para fisgar (×1,4) e a barra da briga " +
            "fica maior (×1,2). Vale no lugar da vara comum.");

        /// <summary>Prêmio do diário do pescador (todas as espécies): a rede do navio segura mais 4 peixes.</summary>
        static ItemDefinition StrongNetItem() => Item("Equip_RedeReforcada", "Rede reforçada", ItemCategory.Gear, ItemRarity.Epic,
            new Color(0.85f, 0.7f, 0.4f), 0f,
            "Prêmio do diário do pescador. Malha dobrada e boias de cortiça novas: a rede do navio segura mais 4 peixes. Vale no lugar da rede comum.");

        // ---------- Mercado ----------

        /// <summary>Vara à venda (uma vez) e os peixes que o mercado compra, se ainda não estão na lista.</summary>
        static void AddFishingMarket(HarborSettings settings)
        {
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            bool changed = false;
            var rod = RodItem();
            if (!list.Exists(e => e != null && e.item == rod))
            {
                list.Add(Market(rod, 60, 0));
                changed = true;
            }
            foreach (var f in FishSpecs)
            {
                var item = FishItem(f);
                if (list.Exists(e => e != null && e.item == item)) continue;
                var entry = Market(item, 0, f.sell);
                entry.demand = 12;
                entry.demandRefillSeconds = 60f;
                list.Add(entry);
                changed = true;
            }
            if (!changed) return;
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        // ---------- Animator ----------

        /// <summary>
        /// Lançar ("PescarLancar", entra pelo CharacterAnimatorDriver.PlayFishCast), esperar com a vara ("PescarEspera", em loop, enquanto o
        /// bool "Fishing" estiver ligado), brigar com o peixe ("PescarBriga", em loop, entra pelo PlayFishFight) e puxar ("PescarPuxar", peixe
        /// ou linha vazia, sai no fim para a pose da arma). Corpo todo. Clipes do Kevin Iglesias (pasta Work/Fishing do homem).
        /// </summary>
        static void AddFishingStates(AnimatorStateMachine sm, AnimatorState empty)
        {
            var throwClip = KevinClip(ClipFishThrow);
            var waitClip = KevinClip(ClipFishWait);
            var fightClip = KevinClip(ClipFishFight);
            var pullClip = KevinClip(ClipFishPull);
            if (!throwClip || !waitClip || !fightClip || !pullClip)
            {
                Debug.LogWarning("Pesca: clipes do Human Crafting Animations não encontrados em " + KevinWorkFolder);
                return;
            }
            AnimatorState Add(string name, AnimationClip clip, Vector3 at)
            {
                var s = sm.AddState(name, at);
                s.motion = clip;
                s.tag = MovingFullBodyTag;
                return s;
            }
            var throwLine = Add("PescarLancar", throwClip, new Vector3(-300f, -440f));
            var wait = Add("PescarEspera", waitClip, new Vector3(-300f, -520f));
            var fight = Add("PescarBriga", fightClip, new Vector3(-300f, -600f));
            var pull = Add("PescarPuxar", pullClip, new Vector3(-300f, -680f));
            Go(throwLine, empty, 0.3f, ("Fishing", AnimatorConditionMode.IfNot, 0));
            Exit(throwLine, wait, 0.92f, 0.12f);
            Go(wait, empty, 0.3f, ("Fishing", AnimatorConditionMode.IfNot, 0));
            Go(fight, empty, 0.3f, ("Fishing", AnimatorConditionMode.IfNot, 0));
            Exit(pull, empty, 0.9f, 0.2f);
        }

        /// <summary>Clipe do Kevin Iglesias (pasta Work do homem) pelo fim do nome do arquivo ("HumanM@&lt;nome&gt;").</summary>
        static AnimationClip KevinClip(string name)
        {
            if (!AssetDatabase.IsValidFolder(KevinWorkFolder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { KevinWorkFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.EndsWith("@" + name, System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        // ---------- No capitão ----------

        /// <summary>
        /// Vara (cabo, haste e carretilha, ~1,7 m), boia vermelha e branca, linha e os modelos dos peixes (desligados), no capitão; o
        /// <see cref="FishingRod"/> põe a vara entre as mãos a cada quadro. Depois de todas as outras interações do F (ela perde para elas).
        /// </summary>
        static void AddFishingRod(GameObject character)
        {
            var gear = new GameObject("Pesca").transform;
            gear.SetParent(character.transform, false);

            var rod = new GameObject("VaraDePesca").transform;
            rod.SetParent(gear, false);
            var wood = GetMaterial("M_Vara_Cabo", new Color(0.36f, 0.22f, 0.12f), 0.15f);
            var bamboo = GetMaterial("M_Vara_Bambu", new Color(0.78f, 0.66f, 0.38f), 0.2f);
            var brass = GetMaterial("M_Vara_Latao", new Color(0.85f, 0.66f, 0.3f), 0.55f);
            var along = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cylinder, "Cabo", rod, new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.2f, 0.045f), along, wood);
            Part(PrimitiveType.Cylinder, "Haste", rod, new Vector3(0f, 0f, 0.95f), new Vector3(0.022f, 0.75f, 0.022f), along, bamboo);
            Part(PrimitiveType.Cylinder, "Ponteira", rod, new Vector3(0f, 0f, 1.6f), new Vector3(0.012f, 0.1f, 0.012f), along, bamboo);
            Part(PrimitiveType.Cylinder, "Carretilha", rod, new Vector3(0f, -0.06f, 0.12f), new Vector3(0.09f, 0.025f, 0.09f), Quaternion.Euler(0f, 0f, 90f), brass);
            var tip = new GameObject("Ponta").transform;
            tip.SetParent(rod, false);
            tip.localPosition = new Vector3(0f, 0f, 1.7f);
            foreach (var r in rod.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            var bobber = new GameObject("Boia").transform;
            bobber.SetParent(gear, false);
            Part(PrimitiveType.Sphere, "Corpo", bobber, Vector3.zero, Vector3.one * 0.13f, Quaternion.identity,
                GetMaterial("M_Boia_Branca", new Color(0.95f, 0.94f, 0.9f), 0.4f));
            Part(PrimitiveType.Sphere, "Topo", bobber, new Vector3(0f, 0.035f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Quaternion.identity,
                GetMaterial("M_Boia_Vermelha", new Color(0.86f, 0.15f, 0.12f), 0.4f));
            Part(PrimitiveType.Cylinder, "Haste", bobber, new Vector3(0f, 0.09f, 0f), new Vector3(0.015f, 0.04f, 0.015f), Quaternion.identity,
                GetMaterial("M_Boia_Vermelha", new Color(0.86f, 0.15f, 0.12f), 0.4f));

            var line = new GameObject("Linha").AddComponent<LineRenderer>();
            line.transform.SetParent(gear, false);
            line.useWorldSpace = true;
            // Mais grossa que uma linha de verdade: com 1,2 cm sumia no meio do caminho de longe.
            line.widthMultiplier = 0.03f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetTransparentMaterial("M_AimLine", Color.white);
            line.startColor = line.endColor = new Color(0.97f, 0.97f, 0.93f, 0.95f);
            line.enabled = false;

            var show = new GameObject("PeixeMostrado").transform;
            show.SetParent(gear, false);
            show.localPosition = new Vector3(0f, 2.3f, 0.2f);

            var fishRoot = new GameObject("Peixes").transform;
            fishRoot.SetParent(gear, false);
            var table = new List<FishingRod.Catch>();
            foreach (var f in FishSpecs)
            {
                var holder = new GameObject(f.file).transform;
                holder.SetParent(fishRoot, false);
                if (!FishModel(holder, f))
                {
                    Object.DestroyImmediate(holder.gameObject);
                    Debug.LogWarning($"[Pirates] Pesca: modelo do peixe {f.name} não encontrado.");
                    continue;
                }
                holder.gameObject.SetActive(false);
                table.Add(new FishingRod.Catch
                {
                    item = FishItem(f), model = holder.gameObject, weight = f.weight, shallow = f.shallow, deep = f.deep, when = f.when,
                });
            }

            var fishing = character.AddComponent<FishingRod>();
            fishing.Configure(RodItem(), GoldRodItem(), table.ToArray(), rod, tip, bobber, line, show);
            fishing.ConfigureBaits(ShrimpBaitItem(), GoldenBaitItem());
            BakeFishIcons();
            BakeBaitIcons();
            AttachFishingHud();
        }

        /// <summary>A pesca no HUD (se ele já existe; senão o CreateGameHud põe).</summary>
        static void AttachFishingHud()
        {
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            var rod = Object.FindAnyObjectByType<FishingRod>(FindObjectsInactive.Include);
            if (!hud || !rod || hud.GetComponent<FishingHUD>()) return;
            hud.gameObject.AddComponent<FishingHUD>().Bind(rod);
        }

        /// <summary>
        /// O peixe como filho de <paramref name="holder"/>: sem física nem scripts (só o Animator, quem tiver), centrado na origem e com o
        /// maior lado em <see cref="FishSpec.size"/>.
        /// </summary>
        static bool FishModel(Transform holder, FishSpec f)
        {
            GameObject source = f.kit != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{QuaterniusFolder}/{f.kit}.fbx")
                : StorePrefab(f.folder, f.prefab);
            if (!source) return false;
            var go = (GameObject)Object.Instantiate(source, holder, false);
            go.name = source.name;
            go.transform.localRotation = f.pose * go.transform.localRotation;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var b in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            if (f.kit != null)
                foreach (var r in renderers)
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = QuaterniusMaterial;
                    r.sharedMaterials = slots;
                }
            if (f.tint != Color.white || f.glow.maxColorComponent > 0f)
                foreach (var r in renderers)
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = FishMaterial(slots[i], f);
                    r.sharedMaterials = slots;
                }
            if (!Mathf.Approximately(f.stretch, 1f)) Stretch(go.transform, renderers, f.stretch);
            foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;
            var b0 = RendererBounds(renderers);
            float biggest = Mathf.Max(b0.size.x, Mathf.Max(b0.size.y, b0.size.z));
            if (biggest > 1e-4f) go.transform.localScale *= f.size / biggest;
            var b1 = RendererBounds(renderers);
            go.transform.position += holder.position - b1.center;
            return true;
        }

        /// <summary>
        /// Cópia do material do modelo com a cor do peixe por cima (e o brilho), em Materials/Peixes, uma por peixe e material de origem;
        /// refeita a cada geração (mudou a cor na tabela, muda aqui).
        /// </summary>
        static Material FishMaterial(Material source, FishSpec f)
        {
            if (!source) return null;
            EnsureFolder(MaterialsFolder, "Peixes");
            string shortName = f.file.Replace("Item_Peixe_", "").Replace("Item_", "");
            string path = $"{MaterialsFolder}/Peixes/M_Peixe_{shortName}_{source.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.CopyPropertiesFromMaterial(source);
            if (f.plain && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", f.plain ? f.tint : source.GetColor("_BaseColor") * f.tint);
            if (f.glow.maxColorComponent > 0f && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", f.glow);
                Emissive(mat);
            }
            else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Estica o modelo no lado mais comprido dele (medido no espaço do próprio modelo, antes de escalar para o tamanho).</summary>
        static void Stretch(Transform model, Renderer[] renderers, float amount)
        {
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var r in renderers)
            {
                Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
                if (!mesh) continue;
                var b = mesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = model.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            var size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var scale = model.localScale;
            scale[axis] *= amount;
            model.localScale = scale;
        }

        static Bounds RendererBounds(Renderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject StorePrefab(string folder, string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        /// <summary>Ícones dos peixes e da vara (só os que ainda não existem).</summary>
        static void BakeFishIcons()
        {
            EnsureFolder(HudFolder, "Icones");
            foreach (var f in FishSpecs)
            {
                var spec = f;
                // Na diagonal, como as armas; a siba do Pixit é toda azul: pinta de siba (SibaPaint).
                SetItemIcon(spec.file, Icon("Peixe_" + spec.file.Replace("Item_Peixe_", "").Replace("Item_", ""), t => FishModel(t, spec),
                    new Vector3(-0.5f, 0.6f, 1f), -20f, spec.file == "Item_Peixe_Siba" ? SibaPaint : null));
            }
            SetItemIcon("Equip_VaraPesca", Icon("Item_VaraPesca", t => RodIcon(t,
                GetMaterial("M_Vara_Bambu", new Color(0.78f, 0.66f, 0.38f), 0.2f), GetMaterial("M_Vara_Latao", new Color(0.85f, 0.66f, 0.3f), 0.55f))));
            SetItemIcon("Equip_VaraOuro", Icon("Item_VaraOuro", t => RodIcon(t,
                GetMaterial("M_Icone_BambuClaro", new Color(0.93f, 0.82f, 0.5f), 0.3f), GetMaterial("M_Icone_Ouro", new Color(1f, 0.78f, 0.22f), 0.7f))));
        }

        /// <summary>Ícone da vara: cabo, haste, carretilha, linha, boia e anzol (grossa: a de verdade, fina, sumia).</summary>
        static void RodIcon(Transform t, Material bamboo, Material brass)
        {
            {
                var wood = GetMaterial("M_Vara_Cabo", new Color(0.36f, 0.22f, 0.12f), 0.15f);
                // Grossa para o ícone (a de verdade, fina, sumia): cabo, haste, carretilha, linha e anzol.
                var tilt = Quaternion.Euler(0f, 0f, -45f);
                PrismPart(7, "Cabo", t, tilt * new Vector3(0f, -0.6f, 0f), new Vector3(0.13f, 0.42f, 0.13f), tilt, wood);
                PrismPart(6, "Haste", t, tilt * new Vector3(0f, 0.3f, 0f), new Vector3(0.07f, 1.4f, 0.07f), tilt, bamboo);
                PrismPart(10, "Carretilha", t, tilt * new Vector3(-0.12f, -0.42f, 0f), new Vector3(0.24f, 0.07f, 0.24f),
                    tilt * Quaternion.Euler(0f, 0f, 90f), brass);
                var line = GetMaterial("M_Icone_Linha", new Color(0.92f, 0.9f, 0.84f), 0.1f);
                var tip = tilt * new Vector3(0f, 1f, 0f);
                PrismPart(4, "Linha", t, tip + new Vector3(0f, -0.38f, 0f), new Vector3(0.018f, 0.76f, 0.018f), Quaternion.identity, line);
                PrismPart(6, "Anzol", t, tip + new Vector3(0.04f, -0.8f, 0f), new Vector3(0.04f, 0.12f, 0.04f), Quaternion.Euler(0f, 0f, 35f),
                    GetMaterial("M_Icone_Ferro", IconIron, 0.5f));
                PrismPart(8, "Boia", t, tip + new Vector3(0f, -0.55f, 0f), new Vector3(0.11f, 0.12f, 0.11f), Quaternion.identity,
                    GetMaterial("M_Icone_Boia", new Color(0.85f, 0.18f, 0.12f), 0.2f));
            }
        }

        // ---------- Diário do pescador ----------

        /// <summary>
        /// O diário do pescador (objeto "DiarioDoPescador"): as espécies da tabela com o tamanho e a dica, as regiões (círculos em volta das
        /// ilhas que existem na cena e das Águas Negras, se a lula está lá), os prêmios (vara de ouro e rede reforçada chegam no porão) e a
        /// aba Pesca do HUD. Chamar depois de todas as ilhas.
        /// </summary>
        static void AddFishJournal(Inventory hold)
        {
            var species = new List<FishJournal.Species>();
            foreach (var f in FishSpecs)
                species.Add(new FishJournal.Species { item = FishItem(f), size = f.length, where = f.where, legendary = f.legendary });

            var regions = new List<FishJournal.Region>();
            void Island(string objectName, string id, string label, float radius)
            {
                var go = GameObject.Find(objectName);
                if (go) regions.Add(new FishJournal.Region { id = id, name = label, center = go.transform.position, radius = radius });
            }
            Island("Ilha_Sul", RegionPort, "Porto Alento", 120f);
            Island("IlhaGrande", RegionSkull, "Ilha da Caveira", 200f);
            Island("Ilha_Norte", RegionNorth, "Ilha Norte", 160f);
            Island("Ilha_Oeste", RegionWest, "Ilha Oeste", 160f);
            Island("Ilha_Ermida", RegionErmida, "Ilha da Ermida", 120f);
            if (Object.FindAnyObjectByType<Pirates.Combat.GiantSquid>(FindObjectsInactive.Include))
                regions.Add(new FishJournal.Region { id = RegionDark, name = "Águas Negras", center = SquidLair, radius = 420f });

            var journal = new GameObject("DiarioDoPescador").AddComponent<FishJournal>();
            journal.Configure(species.ToArray(), regions.ToArray(), hold, GoldRodItem(), StrongNetItem(),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/Item_Ouro.asset"));
            BakeFishIcons();
            SetItemIcon("Equip_RedeReforcada", Icon("Item_RedeReforcada", StrongNetIcon, new Vector3(-0.4f, 0.8f, 1f)));

            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<FishJournalHUD>()) hud.gameObject.AddComponent<FishJournalHUD>().Bind(journal);
        }
    }
}
