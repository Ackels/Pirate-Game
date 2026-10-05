using System.Collections.Generic;
using System.IO;
using Pirates.Base;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Missions;
using Pirates.Progression;
using Pirates.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Fama do capitão (M10, cenas 05 e 07): Data/FameSettings_Capitao (patentes e bônus; preservado se existe), o objeto
    /// "FamaDoCapitao" (CaptainFame), o painel da tecla C (FameHUD) com o retrato do capitão (UI/Retrato_Capitao.png, feito
    /// aqui a partir do modelo do jogador), esquemas à venda no mercado por patente, patente e fama de cada missão e três
    /// contratos novos para capitães famosos. Quanto vale cada feito: navios no Brig/Boss (chalupas usam o padrão do
    /// EnemyShipAI), acampamentos e postos em <see cref="ApplyCampFame"/>, forte e tesouros com o padrão dos componentes.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string PortraitPath = Root + "/UI/Retrato_Capitao.png";
        // Enquadramento do retrato: fração da altura do modelo na foto, giro da câmera em volta dele e altura do olhar.
        // O papagaio fica no ombro esquerdo do capitão: a câmera vem desse lado e o centro da foto anda um pouco para ele.
        static float PortraitFrame = 0.78f, PortraitYaw = -25f, PortraitLookUp = 0.0f, PortraitShift = 0.12f;

        static FameSettings LoadFameSettings() => LoadOrCreateAsset<FameSettings>(DataFolder + "/FameSettings_Capitao.asset", f =>
        {
            f.ranks = new[]
            {
                Rank("Grumete", 0, "Ninguém nas tavernas sabe o seu nome... ainda."),
                Rank("Marujo de Respeito", 60, "Os marinheiros do porto já cumprimentam você."),
                Rank("Corsário", 200, "Mercadores pagam para você ficar longe das rotas deles."),
                Rank("Capitão Temido", 450, "Bandeiras inimigas descem quando a sua aparece no horizonte."),
                Rank("Lenda dos Sete Mares", 900, "Contam histórias suas em portos que você nunca visitou."),
            };
            f.perks = new[]
            {
                Perk("artilheiro_nato", "Artilheiro nato", "Canhões do navio com +10% de dano.", PerkEffect.CannonDamagePercent, 0.1f),
                Perk("lobo_do_mar", "Lobo do mar", "Velocidade máxima do navio +8%.", PerkEffect.MaxSpeedPercent, 0.08f),
                Perk("casco_de_carvalho", "Casco de carvalho", "Casco máximo do navio +15%.", PerkEffect.HullPercent, 0.15f),
                Perk("negociante", "Negociante", "O mercado paga 20% a mais pelo que você vende e cobra 10% a menos.", PerkEffect.MarketPercent, 0.2f),
                Perk("mestre_de_obras", "Mestre de obras", "Obras do estaleiro e da base 25% mais rápidas.", PerkEffect.BuildSpeedPercent, 0.25f),
                Perk("fama_de_generoso", "Nome que vale ouro", "Missões pagam 20% a mais de ouro (menos as entregas).", PerkEffect.MissionGoldPercent, 0.2f),
                Perk("carpinteiro_habil", "Carpinteiro hábil", "O carpinteiro conserta 50% mais rápido e descansa menos.", PerkEffect.CarpenterPercent, 0.5f),
                Perk("porao_arrumado", "Porão arrumado", "Porão do navio com 15% mais espaço.", PerkEffect.HoldPercent, 0.15f),
                Perk("tripulacao_calejada", "Tripulação calejada", "Tiros no casco matam metade dos tripulantes.", PerkEffect.CrewToughnessPercent, 1f),
            };
            f.perksOffered = 3;
            f.missionGoldPerRank = 0.1f;
            f.defaultCaptainName = "Capitão Barbarossa";
        });

        static FameSettings.Rank Rank(string title, int fame, string description) => new() { title = title, fame = fame, description = description };

        static FameSettings.Perk Perk(string id, string title, string description, PerkEffect effect, float value) =>
            new() { id = id, title = title, description = description, effect = effect, value = value };

        /// <summary>Fama do capitão no porto (cenas 05 e 07) e esquemas à venda por patente no mercado.</summary>
        static CaptainFame AddCaptainFame(HarborSettings settings)
        {
            var fame = new GameObject("FamaDoCapitao").AddComponent<CaptainFame>();
            fame.Configure(LoadFameSettings());
            AddRankedMarket(settings);
            return fame;
        }

        /// <summary>
        /// Esquemas no mercado por patente (M10): quem tem fama compra o que o baú raro não deu. Uma unidade só (esquema é
        /// único); entra uma vez (se já está na lista, fica como está: preço e patente são ajuste de game design).
        /// </summary>
        static void AddRankedMarket(HarborSettings settings)
        {
            var list = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            bool changed = false;
            void Add(string file, int price, int rank)
            {
                var item = LoadItem(file);
                if (!item || list.Exists(e => e != null && e.item == item)) return;
                var entry = Market(item, price, 0);
                entry.requiredRank = rank;
                list.Add(entry);
                changed = true;
            }
            Add("Esquema_Estaleiro", 250, 1);
            Add("Esquema_Forja", 300, 2);
            Add("Esquema_Paiol", 300, 2);
            Add("Esquema_Escuna", 650, 3);
            Add("Esquema_Fortaleza", 900, 3);
            if (!changed) return;
            settings.market = list.ToArray();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>Painel da tecla C no HUD (depois do objeto "HUD").</summary>
        static void AddFameHud(GameObject hud)
        {
            var fame = Object.FindAnyObjectByType<CaptainFame>();
            if (!fame) return;
            hud.AddComponent<FameHUD>().Bind(fame, BakeCaptainPortrait());
        }

        /// <summary>Patente mínima e fama de cada contrato (M10). Só na primeira vez (asset ainda com os valores padrão).</summary>
        static void ApplyMissionFame(MissionDefinition mission)
        {
            if (mission.requiredRank != 0 || mission.fameReward != 10) return;
            (int rank, int fame) = mission.missionId switch
            {
                "caca_no_mar" => (0, 20),
                CampMissionId => (0, 30),
                "madeira_estaleiro" => (0, 10),
                TreasureMissionId => (0, 15),
                "polvora_fortaleza" => (0, 10),
                "mapa_contrabandista" => (1, 15),
                "contrabandistas_oeste" => (1, 30),
                BrigMissionId => (1, 50),
                FortMissionId => (2, 60),
                BossMissionId => (3, 120),
                _ => (0, 10),
            };
            if (rank == 0 && fame == 10) return;
            mission.requiredRank = rank;
            mission.fameReward = fame;
            EditorUtility.SetDirty(mission);
        }

        /// <summary>Contratos para capitães famosos (M10): mais difíceis e mais bem pagos.</summary>
        static IEnumerable<MissionDefinition> FamousMissions(LootItems i)
        {
            yield return Mission("Missao_TerrorDasRotas", m =>
            {
                m.missionId = "terror_das_rotas";
                m.title = "O terror das rotas";
                m.giver = "Capitã Rosa Negra";
                m.description = "Os mercadores pagam bem a quem limpar as rotas. Mande cinco navios inimigos para o fundo e o seu nome vai correr pelas tavernas.";
                m.type = MissionType.SinkShips;
                m.count = 5;
                m.rewards = new[] { new ItemStack(i.gold, 380), new ItemStack(i.powder, 6) };
                m.requiredRank = 2;
                m.fameReward = 45;
            });
            var chain = LoadItem("Item_CargaCorrente");
            var grape = LoadItem("Item_CargaMetralha");
            if (chain && grape)
                yield return Mission("Missao_EncomendaArsenal", m =>
                {
                    m.missionId = "encomenda_arsenal";
                    m.title = "Encomenda para o arsenal";
                    m.giver = "Almirante Teodoro";
                    m.description = "A esquadra do almirante vai caçar o Tormenta e precisa de munição de verdade: doze cargas de corrente e doze de metralha. Só confio isso a um capitão temido.";
                    m.type = MissionType.Deliver;
                    m.delivery = new[] { new ItemStack(chain, 12), new ItemStack(grape, 12) };
                    m.rewards = new[] { new ItemStack(i.gold, 250), new ItemStack(i.iron, 6) };
                    // Feita com a produção da Forja e do Paiol, sem sair do porto: paga pouco e volta devagar (revisão do M10).
                    m.cooldownSeconds = 1800f;
                    m.requiredRank = 3;
                    m.fameReward = 50;
                });
            yield return Mission("Missao_SenhorDosMares", m =>
            {
                m.missionId = "senhor_dos_mares";
                m.title = "O senhor dos mares";
                m.giver = "O governador da ilha";
                m.description = "Dizem que nenhum capitão vivo afundou tantos navios quanto você. Prove: oito navios no fundo do mar e o governador paga uma fortuna.";
                m.type = MissionType.SinkShips;
                m.count = 8;
                m.rewards = new[] { new ItemStack(i.gold, 900), new ItemStack(i.iron, 15), new ItemStack(i.powder, 10) };
                m.requiredRank = 4;
                m.fameReward = 100;
            });
        }

        /// <summary>Fama dos acampamentos do mundo: o da Caveira vale mais, os postos de guarda pouco (o resto fica no padrão).</summary>
        static void ApplyCampFame()
        {
            foreach (var camp in Object.FindObjectsByType<EnemyCamp>(FindObjectsInactive.Include))
            {
                if (camp.CampId == CampMissionId) camp.SetFame(20);
                else if (camp.CampId != HideoutCampId && camp.CampId != GarrisonCampId) camp.SetFame(5);
                EditorUtility.SetDirty(camp);
            }
        }

        // ---------- Retrato ----------

        /// <summary>
        /// Retrato do capitão (cabeça e ombros, de três quartos) do modelo do jogador numa cena de prévia: renderiza com fundo preto
        /// e branco para tirar o recorte (alfa = 1 − (branco − preto)) e pinta por baixo um céu e um mar. 512 px. Sem o kit,
        /// devolve o que já existe.
        /// </summary>
        static Texture2D BakeCaptainPortrait()
        {
            if (!HasKitCharacter(PlayerKitModel)) return AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath);
            const int size = 512;
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var holder = new GameObject("Retrato");
                SceneManager.MoveGameObjectToScene(holder, scene);
                var model = KitCharacter(PlayerKitModel, holder.transform);
                var idle = KitClip(PlayerKitModel, "Idle");
                if (idle) idle.SampleAnimation(model, 0.4f);

                var bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float height = bounds.size.y;
                Transform head = null;
                foreach (var t in model.GetComponentsInChildren<Transform>())
                    if (t.name == "Head") head = t;
                var target = head ? head.position + Vector3.up * height * 0.02f : new Vector3(bounds.center.x, bounds.max.y - height * 0.15f, bounds.center.z);

                PortraitLight(scene, Quaternion.Euler(30f, 150f, 0f), 1.25f, new Color(1f, 0.95f, 0.85f));
                PortraitLight(scene, Quaternion.Euler(15f, -120f, 0f), 0.55f, new Color(0.7f, 0.8f, 1f));

                var camGo = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.fieldOfView = 24f;
                cam.nearClipPlane = 0.05f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.targetTexture = rt;
                // De três quartos (um pouco para a direita do capitão), na altura dos olhos; enquadra ~45% da altura.
                float frame = height * PortraitFrame;
                float distance = frame * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                var dir = Quaternion.Euler(0f, PortraitYaw, 0f) * model.transform.forward;
                camGo.transform.position = target + dir * distance + Vector3.up * height * 0.02f;
                camGo.transform.LookAt(target + Vector3.up * height * PortraitLookUp);
                camGo.transform.position += camGo.transform.right * frame * PortraitShift;

                var black = Render(cam, rt, Color.black);
                var white = Render(cam, rt, Color.white);
                cam.targetTexture = null;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int k = y * size + x;
                    Color b = black[k], w = white[k];
                    float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                    Color fg = alpha > 0.01f ? b / alpha : Color.black;
                    fg.a = 1f;
                    pixels[k] = Color.Lerp(Backdrop(x / (float)size, y / (float)size), fg, alpha);
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(PortraitPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.ImportAsset(PortraitPath);
            if (AssetImporter.GetAtPath(PortraitPath) is TextureImporter importer &&
                (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath);
        }

        static void PortraitLight(Scene scene, Quaternion rotation, float intensity, Color color)
        {
            var go = new GameObject("Luz");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
        }

        static Color[] Render(Camera cam, RenderTexture rt, Color background)
        {
            cam.backgroundColor = background;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            var pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>Fundo do retrato: céu claro em cima, horizonte e mar turquesa embaixo (u, v de 0 a 1, v de baixo para cima).</summary>
        static Color Backdrop(float u, float v)
        {
            var skyTop = new Color(0.42f, 0.66f, 0.86f);
            var horizon = new Color(0.93f, 0.86f, 0.68f);
            var sea = new Color(0.12f, 0.45f, 0.55f);
            var deep = new Color(0.07f, 0.3f, 0.4f);
            const float line = 0.36f;
            if (v >= line) return Color.Lerp(horizon, skyTop, Mathf.SmoothStep(0f, 1f, (v - line) / (1f - line)));
            return Color.Lerp(deep, sea, v / line);
        }
    }
}
