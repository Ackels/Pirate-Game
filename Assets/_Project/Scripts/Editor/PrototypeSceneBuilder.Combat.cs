using Pirates.Combat;
using Pirates.Controls;
using Pirates.Ships;
using Pirates.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Parte de combate naval do gerador: canhões no navio, sistemas, vida/naufrágio e alvos flutuantes.
    /// Protótipo 02 (tiro ao alvo) tem um navio-alvo parado; Protótipo 03 (duelo) tem um inimigo com IA;
    /// Protótipo 04 é o duelo com loot (PrototypeSceneBuilder.Loot.cs); o 05 acrescenta o porto-base
    /// (PrototypeSceneBuilder.Harbor.cs).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        enum CombatScenario { TargetPractice, Duel, LootHunt, Harbor, World }

        sealed class CombatAssets
        {
            public CannonStats cannons, enemyCannons;
            public ShipStats enemyShip;
            public Material iron, particles, aimLine, aimMarker, targetWood, flag, enemyHull, enemySail;
        }

        static CombatAssets LoadCombatAssets() => new()
        {
            cannons = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_Chalupa.asset"),
            // Inimigo um pouco mais lento e com recarga maior: dá chance de fugir ou de ganhar a troca de tiros.
            enemyCannons = LoadOrCreateAsset<CannonStats>(DataFolder + "/CannonStats_Inimigo.asset", c => c.reloadTime = 5f),
            enemyShip = LoadOrCreateAsset<ShipStats>(DataFolder + "/ShipStats_ChalupaInimiga.asset", s => s.maxSpeed = 9.5f),
            iron = GetMaterial("M_Iron", new Color(0.11f, 0.11f, 0.12f), 0.55f),
            targetWood = GetMaterial("M_TargetWood", new Color(0.46f, 0.31f, 0.16f), 0.2f),
            flag = GetMaterial("M_Flag", new Color(0.78f, 0.1f, 0.1f), 0.1f),
            enemyHull = GetMaterial("M_EnemyHull", new Color(0.13f, 0.1f, 0.09f), 0.25f),
            enemySail = GetMaterial("M_EnemySail", new Color(0.32f, 0.3f, 0.29f), 0.05f),
            particles = GetTransparentMaterial("M_FxParticles", Color.white),
            aimLine = GetTransparentMaterial("M_AimLine", Color.white),
            aimMarker = GetTransparentMaterial("M_AimMarker", new Color(1f, 0.8f, 0.25f, 0.35f)),
        };

        /// <summary>Canhão do kit no convés: distância do centro do navio e escala (o modelo tem 1,9 m de comprimento).</summary>
        const float CannonX = 1.25f, CannonScale = 0.75f;

        // Vida base de cada tipo de alvo. Um tiro de canhão tira 12 (CannonStats.damagePerBall).
        const float PlayerHull = 150f, TargetShipHull = 100f, EnemyHull = 100f, RaftHull = 36f, BarrelHull = 12f;

        /// <summary>
        /// "CombatSystems" com o <see cref="CombatEffects"/> (efeitos e sons ouvem por ele): um por cena, criado por quem precisar primeiro
        /// (canhões ou barris de pólvora; a cena 06 não tem combate naval e precisa dele para as explosões).
        /// </summary>
        static GameObject CombatSystemsObject()
        {
            var existing = Object.FindAnyObjectByType<CombatEffects>(FindObjectsInactive.Include);
            if (existing) return existing.gameObject;
            var systems = new GameObject("CombatSystems");
            systems.AddComponent<CombatEffects>();
            return systems;
        }

        static void AddNavalCombat(CombatAssets assets, CombatScenario scenario, ShipController ship, ShipStats stats,
            Material deck, LootAssets loot)
        {
            var systems = CombatSystemsObject();
            systems.AddComponent<CannonballSystem>().Configure(assets.iron, assets.particles);

            var body = ship.GetComponent<Rigidbody>();
            var cannons = new GameObject("Cannons").transform;
            cannons.SetParent(ship.transform, false);
            var playerHull = SloopHull(PlayerShipModel);
            var port = CreateBattery(BroadsideSide.Port, assets.cannons, assets, cannons, body, playerHull, withAimPreview: true);
            var starboard = CreateBattery(BroadsideSide.Starboard, assets.cannons, assets, cannons, body, playerHull, withAimPreview: true);
            ship.gameObject.AddComponent<PlayerCannonInput>().Configure(port, starboard);
            var playerHealth = MakeSinkable(ship.gameObject, PlayerHull);

            if (scenario == CombatScenario.TargetPractice)
            {
                // Navio-alvo parado com o costado virado para o jogador. Sem fonte de comando: fica à deriva.
                var target = CreateShip("NavioAlvo", new Vector3(65f, 0.3f, 0f), Quaternion.identity, stats, SloopHull(EnemyShipModel));
                MakeSinkable(target.gameObject, TargetShipHull);
            }
            else
            {
                var enemy = CreateEnemyShip(assets, ship.transform, deck);
                if (loot != null) AddLoot(loot, ship, enemy);
            }

            CreateTargets(assets, deck);

            var debug = new GameObject("DebugTools");
            debug.AddComponent<HealthBarsHUD>().Ignore(playerHealth);
            debug.AddComponent<DebugSceneReset>();
            if (scenario != CombatScenario.TargetPractice) debug.AddComponent<EnemyStateLabels>();
        }

        /// <summary>Chalupa inimiga ao norte, logo além do alcance de detecção: aparece patrulhando e logo vem atrás.</summary>
        static ShipController CreateEnemyShip(CombatAssets assets, Transform player, Material deck) =>
            CreateEnemy("NavioInimigo", EnemySpawnPoints[0], Quaternion.Euler(0f, 160f, 0f), assets.enemyShip,
                assets.enemyCannons, SloopHull(EnemyShipModel), EnemyHull, assets, player);

        /// <summary>Navio inimigo: casco, um bordo de canhões de cada lado, vida com naufrágio e a IA atrás do jogador.</summary>
        static ShipController CreateEnemy(string name, Vector3 position, Quaternion rotation, ShipStats stats, CannonStats cannonStats,
            ShipHull hull, float hullHealth, CombatAssets assets, Transform player)
        {
            var enemy = CreateShip(name, position, rotation, stats, hull);
            var body = enemy.GetComponent<Rigidbody>();
            var cannons = new GameObject("Cannons").transform;
            cannons.SetParent(enemy.transform, false);
            var port = CreateBattery(BroadsideSide.Port, cannonStats, assets, cannons, body, hull, withAimPreview: false);
            var starboard = CreateBattery(BroadsideSide.Starboard, cannonStats, assets, cannons, body, hull, withAimPreview: false);
            MakeSinkable(enemy.gameObject, hullHealth);
            enemy.gameObject.AddComponent<ShipDebuffs>();
            enemy.gameObject.AddComponent<EnemyShipAI>().Configure(player, port, starboard);
            return enemy;
        }

        static Health MakeSinkable(GameObject go, float maxHealth)
        {
            var health = go.AddComponent<Health>();
            health.Configure(maxHealth);
            go.AddComponent<SinkOnDeath>();
            return health;
        }

        /// <param name="hullModel">Modelo do casco medido para os canhões (vazio = o "Visual/Casco" do navio).</param>
        static CannonBattery CreateBattery(BroadsideSide side, CannonStats stats, CombatAssets assets, Transform parent,
            Rigidbody body, ShipHull hull, bool withAimPreview, Transform hullModel = null)
        {
            int s = (int)side;
            var go = new GameObject(side == BroadsideSide.Port ? "Battery_Port" : "Battery_Starboard");
            go.transform.SetParent(parent, false);

            // Canhões do Kenney virados para fora, cada um no convés que há ali (principal, castelo de proa ou popa, medido
            // no modelo); onde o casco afina (proa) ficam mais para dentro. A boca (+Z do modelo) passa um pouco da borda.
            int count = Mathf.Max(1, stats.cannonsPerSide);
            var muzzles = new Transform[count];
            using var deck = new DeckProbe(hullModel ? hullModel : body.transform.Find("Visual/Casco"));
            for (int i = 0; i < count; i++)
            {
                float z = count == 1 ? 0f : Mathf.Lerp(hull.cannonZMin, hull.cannonZMax, i / (float)(count - 1));
                float x = CannonX, deckY = float.NaN;
                for (float tryX = CannonX; tryX >= 0.6f && float.IsNaN(deckY); tryX -= 0.15f)
                {
                    deckY = deck.DeckHeight(s * tryX, z);
                    x = tryX;
                }
                if (float.IsNaN(deckY)) deckY = ShipDeckY - ShipModelKeel;
                float half = deck.HalfWidth(deckY + 0.3f, z);
                if (!float.IsNaN(half)) x = Mathf.Min(x, half - 0.95f);
                float y = ShipModelKeel + deckY;
                Kenney("cannon", go.transform, new Vector3(s * x, y, z), s * 90f, CannonScale, $"Canhao_{i}");
                var muzzle = new GameObject($"Muzzle_{i}").transform;
                muzzle.SetParent(go.transform, false);
                muzzle.localPosition = new Vector3(s * (x + 0.99f * CannonScale + 0.1f), y + 0.62f * CannonScale, z);
                muzzles[i] = muzzle;
            }

            var battery = go.AddComponent<CannonBattery>();
            if (!withAimPreview)
            {
                battery.Configure(stats, side, muzzles, body, null, null);
                return battery;
            }

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.3f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.sharedMaterial = assets.aimLine;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0.9f, 1f) });
            line.colorGradient = gradient;
            line.enabled = false;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = go.name + "_AimMarker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = assets.aimMarker;
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            marker.SetActive(false);

            battery.Configure(stats, side, muzzles, body, line, marker.transform);
            return battery;
        }

        static void CreateTargets(CombatAssets assets, Material deck)
        {
            var parent = new GameObject("Alvos").transform;
            Raft(parent, "Balsa_Bombordo", new Vector3(-50f, 0f, 12f), assets, deck);
            Raft(parent, "Balsa_Estibordo", new Vector3(55f, 0f, 25f), assets, deck);
            Raft(parent, "Balsa_Longe_A", new Vector3(-110f, 0f, 95f), assets, deck);
            Raft(parent, "Balsa_Longe_B", new Vector3(130f, 0f, 70f), assets, deck);

            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                MakeSinkable(FloatingBarrel(parent, $"Barril_E{i}", new Vector3(30f + Mathf.Cos(a) * 6f, 0f, -45f + Mathf.Sin(a) * 6f)), BarrelHull);
                MakeSinkable(FloatingBarrel(parent, $"Barril_B{i}", new Vector3(-45f + Mathf.Cos(a) * 6f, 0f, -40f + Mathf.Sin(a) * 6f)), BarrelHull);
            }
        }

        /// <summary>Barril-alvo boiando: a física da boia de antes (cápsula de 1,1 m, 4 pontos) com o barril do Kenney.</summary>
        static GameObject FloatingBarrel(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 80f;
            // Centro de massa abaixo dos pontos de empuxo (peso no fundo): com ele acima, o barril capota e boia de
            // cabeça para baixo, 1 m submerso. No equilíbrio o barril fica com metade para fora d'água.
            body.centerOfMass = new Vector3(0f, -0.65f, 0f);
            var collider = go.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 1.1f;
            go.AddComponent<Buoyancy>().Configure(new[]
            {
                new Vector3(-0.35f, -0.45f, -0.35f), new Vector3(0.35f, -0.45f, -0.35f),
                new Vector3(-0.35f, -0.45f, 0.35f), new Vector3(0.35f, -0.45f, 0.35f),
            }, depth: 0.8f, horizontalWaterDrag: 0.8f);
            // 0,85 × o modelo = 1,14 de largura e 1,05 de altura, com o centro na raiz (o pivô do modelo é embaixo).
            Kenney("barrel", go.transform, new Vector3(0f, -0.52f, 0f), 0f, 0.85f, "Barril");
            return go;
        }

        static void Raft(Transform parent, string name, Vector3 position, CombatAssets assets, Material deck)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 500f;
            body.angularDamping = 1f;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(4f, 0.6f, 4f);

            root.AddComponent<Buoyancy>().Configure(new[]
            {
                new Vector3(-1.8f, -0.3f, -1.8f), new Vector3(1.8f, -0.3f, -1.8f),
                new Vector3(-1.8f, -0.3f, 1.8f), new Vector3(1.8f, -0.3f, 1.8f),
            }, depth: 0.6f, horizontalWaterDrag: 0.6f);

            Part(PrimitiveType.Cube, "Deck", root.transform, Vector3.zero, new Vector3(4f, 0.5f, 4f), Quaternion.identity, deck);
            Part(PrimitiveType.Cylinder, "Mastro", root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.15f, 1.5f, 0.15f), Quaternion.identity, assets.targetWood);
            Part(PrimitiveType.Cube, "Bandeira", root.transform, new Vector3(0.6f, 2.6f, 0f), new Vector3(1.2f, 0.8f, 0.05f), Quaternion.identity, assets.flag);
            MakeSinkable(root, RaftHull);
        }

        /// <summary>Material transparente e sem iluminação (URP Particles/Unlit) para efeitos e mira.</summary>
        static Material GetTransparentMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = name };
            SetIfPresent(mat, "_Surface", 1f);
            SetIfPresent(mat, "_Blend", 0f);
            SetIfPresent(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetIfPresent(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetIfPresent(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetIfPresent(mat, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetIfPresent(mat, "_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void SetIfPresent(Material mat, string property, float value)
        {
            if (mat.HasProperty(property)) mat.SetFloat(property, value);
        }
    }
}
