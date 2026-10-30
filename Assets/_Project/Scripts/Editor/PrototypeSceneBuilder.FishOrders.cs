using System.Collections.Generic;
using Pirates.Base;
using Pirates.Economy;
using Pirates.Missions;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Encomendas da Iara e iscas (D2, passo 3 do plano de 30/10; pedido do Jhonata: "dar mais propósitos para os peixes"): a pescadora pede
    /// peixes (contratos de entrega repetíveis, entregues a ela, que conta porão, armazém e mochila) em troca de ouro, fama e iscas: a de
    /// camarão (também feita na cozinha da taverna) e a dourada (só nas encomendas difíceis), que chamam peixe raro e lendário na vara.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static ItemDefinition ShrimpBaitItem()
        {
            var item = Item("Item_Isca_Camarao", "Isca de camarão", ItemCategory.Resource, ItemRarity.Common, new Color(0.95f, 0.55f, 0.45f), 0.05f,
                "Camarão no anzol. Peixe raro morde mais (e o comum menos). O capitão leva 10 do porão ao desembarcar; gasta uma por lançamento.");
            return Grouped(item);
        }

        static ItemDefinition GoldenBaitItem()
        {
            var item = Item("Item_Isca_Dourada", "Isca dourada", ItemCategory.Resource, ItemRarity.Epic, new Color(1f, 0.8f, 0.3f), 0.05f,
                "A receita secreta da Iara: camarão no óleo de peixe-lanterna com pó de pérola. Chama os lendários (×4) e os raros. Só é gasta onde " +
                "um lendário pode morder agora (lugar, hora e tempo certos).");
            return Grouped(item);
        }

        static ItemDefinition Grouped(ItemDefinition item)
        {
            if (item.group != ItemGroup.Food)
            {
                item.group = ItemGroup.Food;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        static IEnumerable<ItemDefinition> BaitItems()
        {
            yield return ShrimpBaitItem();
            yield return GoldenBaitItem();
        }

        /// <summary>A isca de camarão na cozinha da taverna: 2 camarões = 4 iscas (se ainda não está lá).</summary>
        static void AddBaitRecipe(HarborSettings settings)
        {
            var bait = ShrimpBaitItem();
            var shrimp = System.Array.Find(FishSpecs, f => f.file == "Item_Peixe_Camarao");
            if (shrimp == null) return;
            var list = new List<HarborSettings.Recipe>(settings.kitchen ?? System.Array.Empty<HarborSettings.Recipe>());
            if (list.Exists(r => r != null && r.result.item == bait)) return;
            list.Add(new HarborSettings.Recipe { result = new ItemStack(bait, 4), cost = new[] { new ItemStack(FishItem(shrimp), 2) }, toHold = true, craftSeconds = 0f });
            settings.kitchen = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        static void BakeBaitIcons()
        {
            var shrimp = System.Array.Find(FishSpecs, f => f.file == "Item_Peixe_Camarao");
            if (shrimp == null) return;
            foreach (var (file, gold) in new[] { ("Item_Isca_Camarao", false), ("Item_Isca_Dourada", true) })
            {
                bool golden = gold;
                string f = file;
                SetItemIcon(f, Icon(f.Replace("Item_", ""), t =>
                {
                    var holder = new GameObject("Camarao").transform;
                    holder.SetParent(t, false);
                    FishModel(holder, new FishSpec
                    {
                        file = f, folder = shrimp.folder, prefab = shrimp.prefab, pose = shrimp.pose, size = 1.5f,
                        plain = golden, tint = golden ? new Color(1f, 0.78f, 0.25f) : Color.white, glow = golden ? new Color(0.35f, 0.25f, 0.05f) : Color.black,
                    });
                    // Anzol de ferro em J (haste, olho em cima, curva e ponta), com o camarão preso na curva.
                    var iron = GetMaterial("M_Icone_Ferro", IconIron, 0.5f);
                    PrismPart(6, "Haste", t, new Vector3(0.12f, 0.3f, 0.05f), new Vector3(0.07f, 0.6f, 0.07f), Quaternion.identity, iron);
                    PrismPart(10, "Olho", t, new Vector3(0.12f, 0.64f, 0.05f), new Vector3(0.2f, 0.05f, 0.2f), Quaternion.Euler(90f, 0f, 0f), iron);
                    PrismPart(6, "Curva", t, new Vector3(0.02f, -0.1f, 0.05f), new Vector3(0.07f, 0.3f, 0.07f), Quaternion.Euler(0f, 0f, -55f), iron);
                    PrismPart(6, "Ponta", t, new Vector3(-0.1f, 0.02f, 0.05f), new Vector3(0.06f, 0.24f, 0.06f), Quaternion.Euler(0f, 0f, 12f), iron);
                }, new Vector3(-0.15f, 0.2f, 1f)));
            }
        }

        // ---------- Encomendas ----------

        /// <summary>Uma encomenda: o que ela pede, o que paga e o que diz.</summary>
        sealed class OrderSpec
        {
            public string file, id, title, description;
            public (string fish, int amount)[] wants;
            public int gold, shrimpBait, goldenBait, fame;
            public float cooldown;
            public string[] offer, done;
            public string accept, waiting, resting;
        }

        // Ouro bem acima do que o mercado paga pelos mesmos peixes (6 cavalas + 4 arenques valem 20 no mercado; a Iara paga 70): o lucro é a
        // viagem. As difíceis (noite, tempestade, Ermida, pérola) pagam a isca dourada. Uma encomenda por vez com ela.
        static readonly OrderSpec[] OrderSpecs =
        {
            new() { file = "Missao_Encomenda_Ceia", id = "encomenda_ceia", title = "A ceia dos pescadores",
                wants = new[] { ("Item_Peixe_Cavala", 6), ("Item_Peixe_Arenque", 4) }, gold = 70, shrimpBait = 4, fame = 5, cooldown = 600f,
                description = "Os pescadores velhos ceiam juntos no cais aos domingos, e a minha rede rasgou. Me traga cavala e arenque.",
                offer = new[] { "Capitão, uma encomenda pequena. A minha rede rasgou e domingo tem ceia no cais.",
                    "Seis cavalas e quatro arenques. Pesca na beira da praia mesmo, ou com a rede do navio." },
                accept = "Que santo te pague! Na entrega eu pago, e ainda dou umas iscas de camarão.",
                waiting = "Seis cavalas e quatro arenques, capitão. Os velhos estão com fome.",
                done = new[] { "Olha que beleza de peixe! A ceia está salva.", "Tome o ouro e umas iscas de camarão: peixe raro gosta delas." },
                resting = "A ceia de domingo está garantida. Volte na semana que vem, capitão." },
            new() { file = "Missao_Encomenda_Tinta", id = "encomenda_tinta", title = "Tinta para as cartas",
                wants = new[] { ("Item_Peixe_Siba", 3) }, gold = 60, shrimpBait = 4, fame = 6, cooldown = 600f,
                description = "O escrivão do almirantado ficou sem tinta. Tinta boa vem da siba. Três delas, capitão.",
                offer = new[] { "O escrivão do almirantado ficou sem tinta para as cartas do Teodoro. Tinta boa é a da siba.",
                    "Três sibas, capitão. Elas gostam de água mais funda." },
                accept = "Boa! Na entrega ainda tem iscas de camarão: ajudam a siba a morder.",
                waiting = "Três sibas para a tinta, capitão.",
                done = new[] { "Três sibas inteirinhas, com a tinta e tudo.", "O almirantado agradece. E eu pago." },
                resting = "O escrivão tem tinta para um mês. Por enquanto, nada." },
            new() { file = "Missao_Encomenda_Boticario", id = "encomenda_boticario", title = "O remédio do boticário",
                wants = new[] { ("Item_Peixe_Baiacu", 2), ("Item_Peixe_Ourico", 2) }, gold = 80, fame = 6, cooldown = 900f,
                description = "O boticário faz remédio com o veneno do baiacu e os espinhos do ouriço. Dois de cada.",
                offer = new[] { "O boticário me pediu peixe esquisito: baiacu, pelo veneno, e ouriço, pelos espinhos.",
                    "Dois de cada, capitão. Cuidado com o baiacu: não coma, por nada." },
                accept = "Combinado. E não me apareça inchado!",
                waiting = "Dois baiacus e dois ouriços para o boticário, capitão.",
                done = new[] { "Veneno e espinho, tudo certo. O boticário vai ficar feliz.", "Tome o pagamento dele, e o meu obrigado." },
                resting = "O boticário tem remédio para a vila inteira. Volte depois." },
            new() { file = "Missao_Encomenda_Festa", id = "encomenda_festa", title = "Peixe para a festa do santo",
                wants = new[] { ("Item_Peixe_Garoupa", 2), ("Item_Peixe_Pargo", 2) }, gold = 150, shrimpBait = 6, fame = 10, cooldown = 900f,
                description = "Festa de São Telmo: o povo quer garoupa da Ilha Norte e pargo vermelho daqui do porto. Dois de cada.",
                offer = new[] { "Chegando a festa de São Telmo, e a vila inteira quer peixe de festa.",
                    "Duas garoupas, que moram nas pedras da Ilha Norte e da Caveira, e dois pargos vermelhos daqui do porto ou da Ilha Oeste." },
                accept = "Isso! Eu guardo um lugar na mesa para o senhor.",
                waiting = "Duas garoupas e dois pargos para a festa, capitão.",
                done = new[] { "Que garoupas! A festa vai ser lembrada por anos.", "O ouro da festa, e iscas para o senhor continuar pescando." },
                resting = "A festa passou. Ano que vem tem outra." },
            new() { file = "Missao_Encomenda_Luar", id = "encomenda_luar", title = "Lula ao luar",
                wants = new[] { ("Item_Peixe_Lula", 3) }, gold = 100, goldenBait = 1, fame = 8, cooldown = 1200f,
                description = "Lula só sobe de noite, atrás da lua. Três delas, e eu pago com a minha isca dourada.",
                offer = new[] { "A lula pequena só sobe de noite, atrás da luz da lua.",
                    "Me traga três, e eu te dou uma coisa que pescador nenhum dá: uma isca dourada, a minha receita." },
                accept = "Espere escurecer, capitão. E jogue a linha no fundo.",
                waiting = "Três lulas, capitão. Só de noite.",
                done = new[] { "Três lulas! O senhor pesca como gente do mar.", "Aqui: a isca dourada. Use onde os peixes grandes moram. Ela chama os lendários." },
                resting = "Lula para o mês inteiro. Volte depois de umas luas." },
            new() { file = "Missao_Encomenda_Enguia", id = "encomenda_enguia", title = "Enguia defumada",
                wants = new[] { ("Item_Peixe_Enguia", 2) }, gold = 130, goldenBait = 1, fame = 10, cooldown = 1200f,
                description = "Enguia negra defumada é a ceia dos pescadores velhos. Ela mora nas águas da Ermida.",
                offer = new[] { "Os velhos da vila sentem falta de enguia defumada. A negra, das águas da Ilha da Ermida.",
                    "Duas, capitão. Pague com uma isca dourada." },
                accept = "Que bom! Vá pela Ermida, e cuidado com as Águas Negras logo depois.",
                waiting = "Duas enguias negras da Ermida, capitão.",
                done = new[] { "Enguias da Ermida! Vou acender o defumador hoje mesmo.", "Tome o ouro e a isca dourada." },
                resting = "O defumador está cheio. Por enquanto, nada." },
            new() { file = "Missao_Encomenda_Espada", id = "encomenda_espada", title = "O peixe da tempestade",
                wants = new[] { ("Item_Peixe_Espada", 1) }, gold = 120, goldenBait = 1, fame = 12, cooldown = 1500f,
                description = "O peixe-espada só sobe quando o mar ferve na tempestade. Um só, capitão, para provar que existe.",
                offer = new[] { "O meu sobrinho jura que o peixe-espada é lenda de bêbado. Eu digo que existe.",
                    "Ele só sobe na tempestade, na água funda. Traga um, e calamos a boca do moleque." },
                accept = "Espere o céu fechar e o mar ferver, capitão. É aí que ele vem.",
                waiting = "Um peixe-espada, capitão. Só na tempestade.",
                done = new[] { "Ha! Olha o tamanho disso! Quero ver o moleque falar agora.", "O ouro, e a minha isca dourada. O senhor mereceu." },
                resting = "O moleque ainda está de boca aberta. Volte depois." },
            new() { file = "Missao_Encomenda_Perola", id = "encomenda_perola", title = "Uma pérola para o noivado",
                wants = new[] { ("Item_Perola", 1) }, gold = 220, goldenBait = 1, fame = 15, cooldown = 1800f,
                description = "A filha do ferreiro vai casar, e o noivo quer dar uma pérola. Ostra fisgada no fundo, rara.",
                offer = new[] { "A filha do ferreiro vai casar! E o noivo, coitado, quer dar uma pérola de presente.",
                    "Pérola vem em ostra, na água funda, e é rara. Traga uma, e ele paga bem." },
                accept = "Que o mar seja generoso, capitão.",
                waiting = "Uma pérola para o noivado, capitão.",
                done = new[] { "Que pérola linda! O noivo vai chorar.", "Tome o pagamento dele, e uma isca dourada minha." },
                resting = "O casamento foi lindo. Por enquanto, ninguém mais vai casar." },
        };

        static IEnumerable<MissionDefinition> FishOrderMissions(LootItems i)
        {
            var shrimp = ShrimpBaitItem();
            var golden = GoldenBaitItem();
            foreach (var o in OrderSpecs)
            {
                var spec = o;
                var mission = Mission(spec.file, m =>
                {
                    m.missionId = spec.id;
                    m.title = spec.title;
                    m.giver = "Iara";
                    m.description = spec.description;
                    m.type = MissionType.Deliver;
                    var want = new List<ItemStack>();
                    foreach (var (fish, amount) in spec.wants)
                    {
                        var f = System.Array.Find(FishSpecs, x => x.file == fish);
                        if (f != null) want.Add(new ItemStack(FishItem(f), amount));
                    }
                    m.delivery = want.ToArray();
                    var pay = new List<ItemStack> { new(i.gold, spec.gold) };
                    if (spec.shrimpBait > 0) pay.Add(new ItemStack(shrimp, spec.shrimpBait));
                    if (spec.goldenBait > 0) pay.Add(new ItemStack(golden, spec.goldenBait));
                    m.rewards = pay.ToArray();
                    m.repeatable = true;
                    m.cooldownSeconds = spec.cooldown;
                    m.requiredRank = 0;
                    m.fameReward = spec.fame;
                });
                yield return mission;
            }
        }

        /// <summary>As falas de uma encomenda (escritas só na primeira vez, como as outras missões de NPC).</summary>
        static System.Action<MissionDefinition> OrderDialogue(OrderSpec o) =>
            m => Lines(m, o.offer, o.accept, "Tudo bem, capitão. Tenho outras encomendas, se quiser.", o.waiting, o.done, o.resting);
    }
}
