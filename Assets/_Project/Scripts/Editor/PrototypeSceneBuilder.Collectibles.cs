using System.Collections.Generic;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.OnFoot;
using Pirates.UI;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Coletáveis (C3, 19/10; pacote C, exploração; cena 07): 8 garrafas com mensagem nas praias (as cartas de Afonso Vaz, piloto da nau
    /// Coroa de Prata, cem anos atrás; F abre e lê) e 12 moedas da Frota de Prata nos morros e cantos das ilhas (pega ao passar). Cada
    /// coleção completa dá um prêmio (<see cref="Collections"/>); o contador fica no painel da fama (C). Lugares achados pela cena
    /// (chão firme, nada em volta nem em cima, NavMesh ligado ao desembarque, ≥ 16 m dos acampamentos).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        sealed class CollectibleSpec
        {
            public string island, place, message;
            public Vector2 local;
            public CollectibleKind kind;
            public int number;
        }

        static CollectibleSpec Letter(int n, string island, float x, float z, string place, string message) =>
            new() { kind = CollectibleKind.Bottle, number = n, island = island, local = new Vector2(x, z), place = place, message = message };

        static CollectibleSpec Coin(int n, string island, float x, float z, string place) =>
            new() { kind = CollectibleKind.Coin, number = n, island = island, local = new Vector2(x, z), place = place };

        static readonly CollectibleSpec[] CollectibleSpots =
        {
            Letter(1, "Ilha_Sul", 7.4f, -53.6f, "Ilha Sul, praia dos fundos",
                "A quem achar esta garrafa: meu nome é Afonso Vaz, piloto da nau Coroa de Prata. Saímos de Porto Alento com doze navios " +
                "carregados da prata das minas. O almirante jura que é a frota mais rica que já cruzou estes mares."),
            Letter(2, "Ilha_Oeste", -34.4f, 11.9f, "Ilha Oeste, praia oeste",
                "Terceiro dia. O capitão-mor mandou fundir a melhor prata numa coroa para o rei, e deu o nome dela à nossa nau. Os marujos " +
                "dizem que prata demais num casco só chama desgraça. Eu rio deles."),
            Letter(3, "Ilha_Oeste", 9.6f, -32.1f, "Ilha Oeste, praia sul",
                "Névoa a oeste, e uma água negra como piche. Dois navios da frota sumiram de noite sem um tiro. De manhã, só tábuas boiando " +
                "e um silêncio que não é de Deus."),
            Letter(4, "Ilha_Norte", -33.5f, 36f, "Ilha Norte, praia noroeste",
                "Uma coisa enorme passou por baixo do casco. Os braços dela eram mais grossos que o nosso mastro. O capelão diz que o mar " +
                "quer a prata de volta."),
            Letter(5, "Ilha_Norte", -17.5f, -36f, "Ilha Norte, praia sul",
                "Restam quatro navios. Escondi o meu quinhão num caixote e dei uma moeda a cada homem da vigia, para dar sorte. Se você " +
                "achou uma moeda com a coroa gravada, foi de um deles."),
            Letter(6, "IlhaGrande", -129.9f, -34.6f, "Ilha da Caveira, praia oeste",
                "A Coroa de Prata afundou esta noite, com o capitão-mor abraçado à coroa. Os braços a puxaram para o fundo das Águas " +
                "Negras. Escapei num bote com três homens."),
            Letter(7, "IlhaGrande", 26.1f, -102.6f, "Ilha da Caveira, praia sul",
                "Chegamos a uma ilha com uma caveira de pedra no morro. Os homens que guardaram prata da frota não acordaram de manhã... e " +
                "de noite andaram. Enterrei-os com as moedas. Que Deus me perdoe."),
            Letter(8, "IlhaGrande", 122.1f, -18.6f, "Ilha da Caveira, ponta leste",
                "Última garrafa. Desenhei nas outras cartas, pedaço por pedaço, onde enterrei o que salvei. Junte as oito e siga o X. Mas " +
                "não leve a prata da Coroa para casa: ela sempre volta para o mar."),

            Coin(1, "Ilha_Sul", 3.4f, -17.6f, "Ilha Sul, alto do morro"),
            Coin(2, "Ilha_Sul", -44.6f, -29.6f, "Ilha Sul, morro oeste"),
            Coin(3, "Ilha_Oeste", -10.4f, 3.9f, "Ilha Oeste, alto do morro"),
            Coin(4, "Ilha_Oeste", -18.4f, -24.1f, "Ilha Oeste, mata do sul"),
            Coin(5, "Ilha_Oeste", 21.6f, 11.9f, "Ilha Oeste, lado leste"),
            Coin(6, "Ilha_Norte", -13.5f, 12f, "Ilha Norte, alto do morro"),
            Coin(7, "Ilha_Norte", 22.5f, 20f, "Ilha Norte, morro leste"),
            Coin(8, "Ilha_Norte", -33.5f, -16f, "Ilha Norte, morro oeste"),
            Coin(9, "IlhaGrande", 74.1f, -62.6f, "Ilha da Caveira, morro sudeste"),
            Coin(10, "IlhaGrande", -61.9f, -50.6f, "Ilha da Caveira, perto das ruínas"),
            Coin(11, "IlhaGrande", 62.1f, 53.4f, "Ilha da Caveira, morro nordeste"),
            Coin(12, "IlhaGrande", -97.9f, 5.4f, "Ilha da Caveira, morro oeste"),
        };

        static void AddCollectibles(FootCharacter captain, PlayerModeSwitcher switcher, GameObject hud, LootItems items)
        {
            var root = new GameObject("Coletaveis").transform;
            // Brilho fraco: a moeda se vê de longe e à noite. O relevo é um pouco mais escuro (escuro demais parecia um furo).
            var coinMat = SilverMaterial("M_MoedaPrata", new Color(0.86f, 0.88f, 0.92f), new Color(0.3f, 0.32f, 0.36f));
            var reliefMat = SilverMaterial("M_MoedaPrata_Relevo", new Color(0.74f, 0.76f, 0.8f), new Color(0.22f, 0.24f, 0.27f));

            var made = new List<Collectible>();
            foreach (var spec in CollectibleSpots)
            {
                var island = GameObject.Find(spec.island);
                if (!island)
                {
                    Debug.LogWarning($"[Pirates] Coletável: ilha {spec.island} não achada.");
                    continue;
                }
                var ground = Ground(island.transform, spec.local);
                bool bottle = spec.kind == CollectibleKind.Bottle;
                string id = bottle ? $"garrafa_{spec.number}" : $"moeda_{spec.number}";
                var go = new GameObject(bottle ? $"Garrafa_{spec.number}" : $"Moeda_{spec.number}");
                go.transform.SetParent(root, false);
                // Giro sorteado pelo número (a garrafa não fica toda virada para o mesmo lado).
                go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, spec.number * 47f, 0f));

                Transform spinner = null;
                if (bottle) BottleOnSand(go.transform);
                else spinner = SilverCoin(go.transform, coinMat, reliefMat);
                Sparkle(go.transform, bottle ? 0.45f : 0.8f);

                var c = go.AddComponent<Collectible>();
                c.Configure(id, spec.kind, spec.number, bottle ? "Garrafa com mensagem" : "Moeda da Frota de Prata", spec.message, spec.place, spinner);
                made.Add(c);
            }

            var collections = new GameObject("Colecoes").AddComponent<Collections>();
            collections.Configure(made.ToArray(), new[]
            {
                new Collections.Collection
                {
                    kind = CollectibleKind.Bottle, name = "Garrafas com mensagem",
                    reward = new[] { new ItemStack(MapPieceItem(), 3) }, fame = 40,
                    rewardText = "as oito cartas juntas viram um mapa: 3 pedaços de mapa do tesouro e 40 de fama",
                    doneLine = "As cartas de Afonso Vaz formam um mapa!",
                },
                new Collections.Collection
                {
                    kind = CollectibleKind.Coin, name = "Moedas da Frota de Prata",
                    reward = new[] { new ItemStack(items.gold, 750) }, fame = 60,
                    rewardText = "as doze valem o peso em prata: 750 de ouro no porão e 60 de fama",
                    doneLine = "As doze moedas da Frota de Prata!",
                },
            });
            var picker = captain.gameObject.AddComponent<CollectiblePicker>();
            picker.Configure(switcher, collections);
            hud.AddComponent<CollectiblesHUD>().Bind(picker, collections);
        }

        /// <summary>Garrafa do kit deitada na areia (meio enterrada), com a rolha para o mar e o bilhete enrolado dentro.</summary>
        static void BottleOnSand(Transform parent)
        {
            // Maior que uma garrafa de verdade: na câmera do jogo (~15 m) uma de 50 cm sumia.
            const float length = 0.8f;
            var holder = new GameObject("Visual").transform;
            holder.SetParent(parent, false);
            var model = Kenney("bottle", holder, Vector3.zero, 0f, 1f, "Modelo");
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(0.01f, bounds.size.y), radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            float scale = length / height;
            model.transform.localScale = Vector3.one * scale;
            // Deitada (o pivô do kit é no fundo): de lado, um pouco inclinada e afundada na areia.
            holder.localPosition = new Vector3(-length * 0.5f, radius * scale * 0.7f, 0f);
            holder.localRotation = Quaternion.Euler(0f, 0f, -82f);
            var paper = GetMaterial("M_Bilhete", new Color(0.93f, 0.86f, 0.68f), 0.1f);
            Part(PrimitiveType.Cylinder, "Bilhete", holder, new Vector3(0f, length * 0.4f, 0f),
                new Vector3(radius * scale * 0.9f, length * 0.2f, radius * scale * 0.9f), Quaternion.identity, paper);
        }

        static Material SilverMaterial(string name, Color color, Color glow)
        {
            var mat = GetMaterial(name, color, 0.8f);
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
            mat.SetColor("_EmissionColor", glow);
            Emissive(mat);
            return mat;
        }

        /// <summary>Moeda de prata grande (42 cm) em pé, com o relevo da coroa no meio dos dois lados; o suporte gira e balança.</summary>
        static Transform SilverCoin(Transform parent, Material silver, Material relief)
        {
            var spinner = new GameObject("Moeda").transform;
            spinner.SetParent(parent, false);
            spinner.localPosition = new Vector3(0f, 0.75f, 0f);
            var face = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cylinder, "Disco", spinner, Vector3.zero, new Vector3(0.42f, 0.024f, 0.42f), face, silver);
            Part(PrimitiveType.Cylinder, "Relevo", spinner, Vector3.zero, new Vector3(0.3f, 0.032f, 0.3f), face, relief);
            // A coroa gravada: uma faixa e três pontas, saltadas dos dois lados.
            Part(PrimitiveType.Cube, "Coroa", spinner, new Vector3(0f, -0.03f, 0f), new Vector3(0.14f, 0.04f, 0.08f), Quaternion.identity, silver);
            for (int k = -1; k <= 1; k++)
                Part(PrimitiveType.Cube, "Ponta", spinner, new Vector3(k * 0.05f, 0.015f, 0f), new Vector3(0.03f, 0.05f, 0.08f), Quaternion.identity, silver);
            return spinner;
        }

        /// <summary>Faíscas fracas que piscam em cima do coletável (para achar de longe; partículas paradas, poucas).</summary>
        static void Sparkle(Transform parent, float height)
        {
            var go = new GameObject("Brilho");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.85f, 0.9f), new Color(0.85f, 0.92f, 1f, 0.8f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8;
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxMaterial("M_FX_Brilho", "circle_05", additive: true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
