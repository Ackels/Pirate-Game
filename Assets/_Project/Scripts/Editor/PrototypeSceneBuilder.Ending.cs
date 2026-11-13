using System;
using System.Linq;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// O fim de "A Coroa de Prata" (pacote H, passo 4, 09/11; HISTORIA.md, "Fim"): a Coroa de Prata (relíquia lendária: mais ouro, fama e
    /// tesouro) no tesouro de Lázaro, atrás do altar do templo (trancado até Lázaro cair; destranca de novo a cada vitória, a Coroa só
    /// vem uma vez), o último objetivo do capítulo 6, os quatro quadros do epílogo (no mesmo estilo pintado da abertura, UI/Prologo/Fim_*)
    /// e a conversa dos moradores depois do fim (Porto Alento livre do tributo).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string LazarusTreasureId = "tesouro_lazaro";

        /// <summary>A Coroa de Prata: relíquia lendária (+25% de ouro e de fama, +50% de baú e de equipamento).</summary>
        static ItemDefinition CrownItem() => Relic(Item("Equip_CoroaDePrata", "Coroa de Prata", ItemCategory.Gear, ItemRarity.Legendary,
            new Color(0.86f, 0.9f, 0.98f), 0f,
            "A coroa da nau capitânia da Frota de Prata, perdida há cem anos e tomada de Lázaro Negro na Ilha Amaldiçoada. Quem a usa tem o " +
            "Mar de Prata a seus pés. Relíquia: +25% de ouro (missões e saque), +25% de fama e +50% de chance de baú e de equipamento " +
            "(equipe no encaixe Relíquia da tela I)."), RelicEffect.Crown, 0.25f);

        /// <summary>O tesouro de Lázaro atrás do altar do templo, trancado pela maldição (o LazarusCurse) até Lázaro cair.</summary>
        static void AddLazarusTreasure(Transform island, LazarusCurse curse, LandLootAssets landLoot, LootItems items)
        {
            if (!curse || landLoot == null || items == null) return;
            var table = LoadOrCreateAsset<LootTable>(DataFolder + "/LootTable_Lazaro.asset", t =>
            {
                t.drops = new[]
                {
                    Drop(items.gold, 1f, 700, 1000), Drop(items.powder, 0.8f, 8, 14), Drop(items.iron, 0.8f, 8, 14),
                    Drop(SpiceItem(), 0.7f, 3, 6), Drop(MapPieceItem(), 0.6f, 1, 1), Drop(BombItem(), 0.6f, 2, 4),
                };
                t.chestChance = 0.7f;
                t.schematics = landLoot.chest.schematics;
                t.chestFallback = new ItemStack(items.gold, 500);
            });
            // A Coroa vem sempre, até o jogador ter a dele (como a Lâmina de São Telmo no baú do Capelão).
            if (table.guaranteedGear != CrownItem())
            {
                table.guaranteedGear = CrownItem();
                EditorUtility.SetDirty(table);
            }
            var chest = CreateChest(curse.transform, "Bau_Lazaro", "Tesouro de Lázaro", SkullGround(island, CursedSummit + new Vector2(0f, 8.8f)),
                180f, table, landLoot, withPadlock: true, ChestStyle.RuinsGold);
            chest.SetLock(curse);
            chest.gameObject.AddComponent<WorldMarker>().Configure(LazarusTreasureId, "Tesouro de Lázaro", 2.5f);
            SetItemIcon("Equip_CoroaDePrata", Icon("Equip_CoroaDePrata", CrownIcon, new Vector3(0f, 0.8f, 1f)));
        }

        /// <summary>Coroa de prata: aro de 12 placas, 6 pontas com uma pérola em cada e uma pedra azul na frente.</summary>
        static void CrownIcon(Transform t)
        {
            var silver = GetMaterial("M_Icone_Prata", new Color(0.85f, 0.88f, 0.95f), 0.8f);
            var pearl = GetMaterial("M_Icone_Perola", new Color(0.96f, 0.94f, 1f), 0.8f);
            var gem = GetMaterial("M_Icone_Safira", new Color(0.25f, 0.45f, 0.95f), 0.9f);
            const float r = 0.5f;
            for (int i = 0; i < 12; i++)
            {
                var rot = Quaternion.Euler(0f, i * 30f, 0f);
                Part(PrimitiveType.Cube, "Placa", t, rot * new Vector3(0f, 0.2f, r), new Vector3(0.28f, 0.3f, 0.05f), rot, silver);
            }
            for (int i = 0; i < 6; i++)
            {
                var rot = Quaternion.Euler(0f, i * 60f, 0f);
                Part(PrimitiveType.Cube, "Ponta", t, rot * new Vector3(0f, 0.42f, r), new Vector3(0.17f, 0.17f, 0.05f), rot * Quaternion.Euler(0f, 0f, 45f), silver);
                Part(PrimitiveType.Sphere, "Perola", t, rot * new Vector3(0f, 0.6f, r), Vector3.one * 0.1f, Quaternion.identity, pearl);
            }
            Part(PrimitiveType.Sphere, "Safira", t, new Vector3(0f, 0.2f, r + 0.04f), new Vector3(0.14f, 0.16f, 0.08f), Quaternion.identity, gem);
        }

        /// <summary>Conversa dos moradores depois do fim (Porto Alento livre do tributo). Vazio = continua a de sempre.</summary>
        static string[] EndingTalk(string npc) => npc switch
        {
            "Almirante Teodoro" => new[]
            {
                "Escrevi ao rei: \"Porto Alento livre, Lázaro Negro morto, a Coroa de Prata achada.\" Ele vai ter de mandar uma esquadra só para acreditar.",
                "Um almirante sem esquadra e um corsário com a Coroa. O Mar de Prata nunca teve tanta sorte.",
            },
            "Velho Bento" => new[]
            {
                "Pela primeira vez em cinco anos ninguém veio buscar tributo. Os meninos pescam rindo na ponta do píer.",
                "Eu disse que tinha visto alguma coisa em você naquela praia. Era só água salgada, mas eu disse.",
            },
            "Dona Mercês" => new[]
            {
                "O armazém está cheio e é TODO meu. Quer dizer, nosso. Quer dizer... a vila agradece, capitão.",
                "Sem tributo, os preços até baixaram. Não conte para ninguém que fui eu que falei.",
            },
            "Iara" => new[]
            {
                "A névoa se foi, e as Águas Negras estão mais calmas. Às vezes acho que vejo a vela dele no horizonte.",
                "Obrigada, capitão. Pela vila, e por ter voltado para contar.",
            },
            "Tobias" => new[]
            {
                "Os mortos do meu cemitério dormem melhor desde que a maldição caiu. Bem... quase todos.",
                "Prata amaldiçoada na cabeça de um vivo. Tome cuidado com essa Coroa, capitão. Mas fica bem em você.",
            },
            "Zé Caolho" => new[]
            {
                "Apostei com metade da taverna que você não voltava da ilha da névoa. Estou falido e feliz.",
            },
            "Rato Velho" => new[]
            {
                "Agora todo contrabandista do Mar de Prata sabe o seu nome. Isso é bom para os negócios. Os meus.",
            },
            _ => Array.Empty<string>(),
        };

        // ---------- O epílogo (quatro quadros pintados, como a abertura) ----------

        [MenuItem("Pirates/Prólogo/Gerar quadros do fim")]
        public static void BuildEpilogueFrames()
        {
            for (int i = 1; i <= EpilogueFrames.Length; i++) BuildEpilogueFrame(i);
            Debug.Log($"[Pirates] Quadros do fim em {PrologueFolder}/Fim_*.png.");
        }

        static Action<Stage>[] EpilogueFrames => new Action<Stage>[] { FrameLazarusFalls, FrameCrownInHand, FrameFreePort, FrameOpenSea };

        public static void BuildEpilogueFrame(int number) =>
            BuildPaintedFrame(EpilogueFrames[number - 1], $"{PrologueFolder}/Fim_{number}.png");

        static Texture2D[] LoadEpilogueFrames() => Enumerable.Range(1, EpilogueFrames.Length)
            .Select(n => AssetDatabase.LoadAssetAtPath<Texture2D>($"{PrologueFolder}/Fim_{n}.png")).Where(t => t).ToArray();

        /// <summary>1. O templo do alto ao amanhecer: Lázaro caído diante do altar, o capitão de pé, a névoa verde indo embora.</summary>
        static void FrameLazarusFalls(Stage s)
        {
            Ground(s, new Color(0.42f, 0.42f, 0.43f), 0f, 120f, 0.3f, 31, 0.8f);
            var stone = GetMaterial("M_Prologo_PedraTemplo", new Color(0.62f, 0.6f, 0.58f), 0.1f);
            var dark = GetMaterial("M_Prologo_PedraEscura", new Color(0.36f, 0.35f, 0.36f), 0.1f);
            // O altar no meio, a Coroa em cima; colunas atrás; o capitão de pé atrás do altar, de frente; Lázaro caído na frente.
            Box(s, new Vector3(0f, 0.5f, 4f), new Vector3(2.4f, 1f, 1.3f), stone);
            Box(s, new Vector3(0f, 1.05f, 4f), new Vector3(2.7f, 0.12f, 1.6f), dark);
            foreach (var x in new[] { -4.2f, 4.2f })
                Box(s, new Vector3(x, 2.8f, 8.5f), new Vector3(0.9f, 5.6f, 0.9f), stone);
            Box(s, new Vector3(0f, 5.8f, 8.5f), new Vector3(9.3f, 0.7f, 0.9f), stone);
            Box(s, new Vector3(-6.8f, 1.2f, 6f), new Vector3(0.9f, 2.4f, 0.9f), dark, Quaternion.Euler(0f, 0f, 14f));
            var silver = GetMaterial("M_Prologo_PrataLisa", new Color(0.86f, 0.88f, 0.94f), 0.85f);
            Crown(s, new Vector3(0f, 1.3f, 4f), 0.32f, silver, Quaternion.Euler(-6f, 20f, 0f));
            Glow(s, new Vector3(0f, 2.1f, 3.4f), new Color(0.85f, 0.92f, 1f), 0f, 3f, 5f);
            Character(s, PlayerKitModel, null, "Idle", 0.4f, new Vector3(1.9f, 0f, 3.3f), 215f);
            var lazaro = Character(s, KitCharacters.Captain, CivilianMaterial(KitCharacters.Captain, 2), "Death", 1f, new Vector3(-3f, 0.05f, 5.4f), 120f);
            lazaro.transform.localScale *= 1.45f;
            foreach (var r in lazaro.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Ernest") r.enabled = false;
            // O resto da maldição: luz verde fraca saindo do corpo.
            Glow(s, new Vector3(-3f, 0.8f, 5.4f), new Color(0.45f, 1f, 0.6f), 0f, 2.5f, 4f);
            Sun(s, Quaternion.Euler(22f, 25f, 0f), new Color(1f, 0.74f, 0.5f), 1.5f);
            Sun(s, Quaternion.Euler(40f, -150f, 0f), new Color(0.5f, 0.55f, 0.75f), 0.5f);
            Aim(s, new Vector3(1.6f, 3f, -3.6f), new Vector3(-0.2f, 1f, 4f));
            s.cam.fieldOfView = 42f;
            s.sky = (u, v) => Sky(u, v, new Color(1f, 0.68f, 0.42f), new Color(0.32f, 0.3f, 0.48f), 0.45f, 0.7f);
            s.haze = 0.1f;
            s.hazeColor = new Color(0.6f, 0.62f, 0.6f);
            s.tint = new Color(1.05f, 0.98f, 0.92f);
        }

        /// <summary>2. A Coroa de Prata de perto, numa pedra contra o céu do amanhecer.</summary>
        static void FrameCrownInHand(Stage s)
        {
            Sea(s, new Color(0.3f, 0.45f, 0.55f), -8f, 900f, 0.4f, 33);
            var stone = GetMaterial("M_Prologo_PedraTemplo", new Color(0.62f, 0.6f, 0.58f), 0.1f);
            Box(s, new Vector3(0f, 0.45f, 3f), new Vector3(1.6f, 0.9f, 1.2f), stone);
            var silver = GetMaterial("M_Prologo_PrataLisa", new Color(0.86f, 0.88f, 0.94f), 0.85f);
            Crown(s, new Vector3(0f, 1.08f, 3f), 0.4f, silver, Quaternion.Euler(-4f, 18f, 0f));
            var gem = GetEmissiveMaterial("M_Prologo_Safira", new Color(0.3f, 0.5f, 1f), 1.2f);
            Blob(s, new Vector3(0.12f, 1.16f, 2.63f), new Vector3(0.1f, 0.12f, 0.06f), gem);
            Glow(s, new Vector3(0f, 1.8f, 2.2f), new Color(0.9f, 0.95f, 1f), 0f, 2.5f, 4f);
            Sun(s, Quaternion.Euler(14f, 10f, 0f), new Color(1f, 0.78f, 0.55f), 1.5f);
            Sun(s, Quaternion.Euler(30f, -160f, 0f), new Color(0.6f, 0.65f, 0.85f), 0.5f);
            Aim(s, new Vector3(0.3f, 1.55f, 0.9f), new Vector3(0f, 1.15f, 3f));
            s.cam.fieldOfView = 40f;
            s.sky = (u, v) => Sky(u, v, new Color(1f, 0.74f, 0.46f), new Color(0.4f, 0.52f, 0.76f), 0.3f, 0.9f);
            s.tint = new Color(1.04f, 1f, 0.96f);
            s.vignette = 0.6f;
        }

        /// <summary>3. Porto Alento em festa: o cais cheio de moradores acenando para o navio que chega com a Coroa.</summary>
        static void FrameFreePort(Stage s)
        {
            Sea(s, new Color(0.18f, 0.52f, 0.58f), -0.1f, 500f, 0.2f, 35);
            for (int i = 0; i < 4; i++)
                Kenney("structure-platform-dock", s.root, new Vector3(0f, 1.25f - 1.31f * 1.6f, i * 4f), Quaternion.identity, 1.6f, $"Cais_{i}");
            Kenney("ship-pirate-medium", s.root, new Vector3(9f, -1f, 20f), Quaternion.Euler(0f, 200f, 4f), 1f, "Navio");
            Kenney("flag-pirate", s.root, new Vector3(-1.2f, 1.25f, 13f), Quaternion.identity, 1.4f, "Bandeira");
            Kenney("barrel", s.root, new Vector3(-1.4f, 1.25f, 2f), Quaternion.identity, 0.8f, "Barril");
            var crowd = new (string model, int palette, string clip, float at, Vector3 pos, float yaw)[]
            {
                (KitCharacters.Henry, 6, "Wave", 0.4f, new Vector3(0.9f, 1.25f, 9.5f), 120f),
                (KitCharacters.Anne, 7, "Wave", 0.6f, new Vector3(0.2f, 1.25f, 7.6f), 110f),
                (KitCharacters.Henry, 5, "Yes", 0.5f, new Vector3(-0.9f, 1.25f, 8.8f), 100f),
                (KitCharacters.Anne, 5, "Wave", 0.3f, new Vector3(0.6f, 1.25f, 5.6f), 95f),
                (KitCharacters.Henry, 4, "Wave", 0.7f, new Vector3(-0.6f, 1.25f, 4.2f), 90f),
                (KitCharacters.Henry, 8, "Wave", 0.5f, new Vector3(0.8f, 1.25f, 11.6f), 130f),
            };
            foreach (var p in crowd)
                Character(s, p.model, CivilianMaterial(p.model, p.palette), p.clip, p.at, p.pos, p.yaw);
            // Dois mastros e o cabo das bandeirinhas entre eles.
            var pole = GetMaterial("M_Prologo_Madeira", new Color(0.36f, 0.22f, 0.13f), 0.2f);
            Box(s, new Vector3(0f, 2.75f, 0.4f), new Vector3(0.14f, 3f, 0.14f), pole);
            Box(s, new Vector3(0f, 2.75f, 12.9f), new Vector3(0.14f, 3f, 0.14f), pole);
            var rope = GetMaterial("M_Prologo_Corda", new Color(0.55f, 0.45f, 0.3f), 0.1f);
            for (int i = 0; i < 15; i++)
            {
                float z0 = 0.4f + i * 0.833f, z1 = z0 + 0.833f;
                float y0 = 4.4f - Mathf.Sin(i / 15f * Mathf.PI) * 0.6f, y1 = 4.4f - Mathf.Sin((i + 1) / 15f * Mathf.PI) * 0.6f;
                var mid = new Vector3(0f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                Box(s, mid, new Vector3(0.03f, 0.03f, 0.86f), rope, Quaternion.LookRotation(new Vector3(0f, y1 - y0, z1 - z0)));
            }
            // Fitas de festa: bandeirinhas coloridas num cabo sobre o cais.
            var colors = new[] { new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.25f), new Color(0.25f, 0.5f, 0.9f), new Color(0.3f, 0.75f, 0.35f) };
            for (int i = 0; i < 14; i++)
            {
                float z = 1f + i * 0.85f;
                float y = 4.15f - Mathf.Sin((z - 0.4f) / 12.5f * Mathf.PI) * 0.6f;
                Box(s, new Vector3(0f, y, z), new Vector3(0.05f, 0.4f, 0.3f), GetMaterial("M_Prologo_Fita_" + i % 4, colors[i % 4], 0.1f),
                    Quaternion.Euler(0f, 0f, 0f));
            }
            Sun(s, Quaternion.Euler(35f, 140f, 0f), new Color(1f, 0.92f, 0.78f), 1.25f);
            Sun(s, Quaternion.Euler(30f, -60f, 0f), new Color(0.6f, 0.72f, 0.9f), 0.5f);
            Aim(s, new Vector3(-9f, 4.4f, 2f), new Vector3(3f, 1.8f, 10f));
            s.sky = (u, v) => Sky(u, v, new Color(0.98f, 0.9f, 0.72f), new Color(0.4f, 0.64f, 0.88f), 0.4f, 0f);
            s.tint = new Color(1.04f, 1f, 0.94f);
        }

        /// <summary>4. O navio do capitão saindo para o mar aberto ao nascer do sol ("Fim" entra por cima, na tela do epílogo).</summary>
        static void FrameOpenSea(Stage s)
        {
            Sea(s, new Color(0.22f, 0.32f, 0.48f), 0f, 900f, 0.35f, 37);
            Kenney("ship-pirate-medium", s.root, new Vector3(2f, -1f, 30f), Quaternion.Euler(0f, 215f, 3f), 1f, "Navio");
            Sun(s, Quaternion.Euler(6f, 0f, 0f), new Color(1f, 0.72f, 0.42f), 1.4f);
            Sun(s, Quaternion.Euler(40f, 180f, 0f), new Color(0.5f, 0.5f, 0.7f), 0.4f);
            Aim(s, new Vector3(-2f, 4f, -6f), new Vector3(2f, 5f, 60f));
            s.cam.fieldOfView = 44f;
            s.sky = (u, v) => Sky(u, v, new Color(1f, 0.74f, 0.45f), new Color(0.38f, 0.55f, 0.8f), 0.52f, 1f);
            s.tint = new Color(1.04f, 0.98f, 0.92f);
        }
    }
}
