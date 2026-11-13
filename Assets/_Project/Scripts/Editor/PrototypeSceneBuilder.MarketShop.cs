using Pirates.Base;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Barraquinha do mercado (13/11, pedido do Jhonata: o mercado fica a céu aberto, "uma barraquinha com um lojista nela"): a banca verde
    /// da praça (a mesa pequena do kit da vila, virada de ponta para quem chegava) vira a carroça-banca do Quaternius, de frente para o
    /// freguês, com toldo, mercadoria no balcão e em volta (frutas, pães, queijo, garrafas, sacos, caixotes, barris) e o lojista atrás do
    /// balcão. O F na frente continua abrindo o comprar e vender.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const float MarketCartScale = 0.85f;
        // Giro da carroça dentro da banca para o balcão ficar de frente para o freguês (+Z da banca).
        const float MarketCartYaw = 180f;
        // Lojista atrás do balcão: distância do meio da banca (para trás).
        const float MarketKeeperBehind = 0.95f;

        [MenuItem("Pirates/Protótipo/Mercado: refazer a barraquinha")]
        static void MenuMarketShop() => Debug.Log(AddMarketShopToOpenScene());

        /// <summary>Troca a banca do mercado na cena aberta (sem regerar) e põe o lojista atrás do balcão; salva.</summary>
        public static string AddMarketShopToOpenScene()
        {
            HarborBuilding entrance = null;
            foreach (var b in Object.FindObjectsByType<HarborBuilding>(FindObjectsInactive.Include))
                if (b.Service == HarborService.Market && b.name.StartsWith("Entrada_")) entrance = b;
            if (!entrance) return "sem a banca do mercado";
            // A banca é a "Barraca" mais perto, atrás da entrada.
            Transform stall = null;
            float best = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name != "Barraca") continue;
                float d = (t.position - entrance.transform.position).sqrMagnitude;
                if (d < best) { best = d; stall = t; }
            }
            if (!stall || best > 16f) return "sem a barraca do mercado";
            MarketShop(stall);
            entrance.SetAction("Negociar");
            EditorUtility.SetDirty(entrance);
            // O lojista: o atendente mais perto da banca vai para trás do balcão, de frente para o freguês.
            Transform keeper = null;
            best = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (!t.name.StartsWith("Atendente_")) continue;
                float d = (t.position - stall.position).sqrMagnitude;
                if (d < best) { best = d; keeper = t; }
            }
            string who = "nenhum";
            if (keeper && best < 25f)
            {
                keeper.SetPositionAndRotation(MarketKeeperSpot(stall), Quaternion.Euler(0f, stall.eulerAngles.y, 0f));
                who = keeper.name;
            }
            var scene = stall.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"ok: barraquinha em {stall.position}, lojista {who}";
        }

        static Vector3 MarketKeeperSpot(Transform stall)
        {
            var p = stall.position - stall.forward * MarketKeeperBehind;
            return PortGround(new Vector2(p.x - IslandCenter.x, p.z - IslandCenter.z));
        }

        /// <summary>Monta a barraquinha dentro da "Barraca" (tira a mesa, o toldo e a mercadoria antigos e o colisor grande).</summary>
        static void MarketShop(Transform stall)
        {
            for (int i = stall.childCount - 1; i >= 0; i--)
            {
                var c = stall.GetChild(i);
                if (c.name is "Mesa" or "Toldo" or "Mercadoria" or "Banca_Mercado" or "Mercadoria_Banca" or "Luzes_Banca") Object.DestroyImmediate(c.gameObject);
            }
            var oldBox = stall.GetComponent<BoxCollider>();
            if (oldBox) Object.DestroyImmediate(oldBox);
            var oldObstacle = stall.GetComponent<NavMeshObstacle>();
            if (oldObstacle) Object.DestroyImmediate(oldObstacle);

            float s = MarketCartScale;
            var cart = Furnish(stall, Kit.Props, "Stall_Cart_Empty", Vector3.zero, MarketCartYaw, s, "Banca_Mercado");
            foreach (var r in cart.GetComponentsInChildren<Renderer>()) r.gameObject.isStatic = true;
            // Colisor só no corpo da banca (sem os varais da carroça) e um obstáculo no NavMesh dos moradores (o NavMesh do porto já foi assado).
            var box = stall.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f * s, 0f);
            box.size = new Vector3(3.02f * s, 1.2f * s, 1.06f * s);
            var obstacle = stall.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size + new Vector3(0.2f, 0f, 0.2f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            // Mercadoria no balcão (o tampo fica a 0,83 da carroça), na metade da frente.
            var goods = new GameObject("Mercadoria_Banca").transform;
            goods.SetParent(stall, false);
            float top = 0.83f * s;
            var wares = new (string model, Kit kit, float x, float z, float scale, float yaw)[]
            {
                ("apple", Kit.Food, -0.95f, 0.18f, 0.7f, 0f), ("orange", Kit.Food, -0.8f, 0.3f, 0.7f, 40f), ("pear", Kit.Food, -0.65f, 0.16f, 0.7f, 10f),
                ("loaf-round", Kit.Food, -0.3f, 0.22f, 0.55f, 20f), ("bread", Kit.Food, 0.05f, 0.25f, 0.6f, -15f),
                ("cheese", Kit.Food, 0.4f, 0.2f, 0.45f, 0f), ("wine-red", Kit.Food, 0.75f, 0.12f, 0.6f, 0f), ("grapes", Kit.Food, 0.95f, 0.28f, 0.55f, 30f),
                ("Pouch_Large", Kit.Props, -0.45f, -0.05f, 0.6f, 60f), ("Coin_Pile", Kit.Props, 0.2f, 0.0f, 0.45f, 0f),
            };
            foreach (var w in wares)
                Furnish(goods, w.kit, w.model, new Vector3(w.x * s, top, w.z * s), w.yaw, w.scale, "Mercadoria");
            // Em volta: caixas de fruta, barril de maçãs, sacos, caixotes e uma melancia no chão.
            Furnish(goods, Kit.Props, "FarmCrate_Apple", new Vector3(-1.7f, 0f, 0.55f), 70f, 0.8f, "Caixa_Macas");
            Furnish(goods, Kit.Props, "Barrel_Apples", new Vector3(1.75f, 0f, 0.35f), 0f, 0.6f, "Barril_Macas", solid: true);
            Furnish(goods, Kit.Props, "FarmCrate_Carrot", new Vector3(1.6f, 0f, -0.55f), -20f, 0.75f, "Caixa_Cenouras");
            Furnish(goods, Kit.Food, "bag", new Vector3(-1.75f, 0f, -0.4f), 20f, 1f, "Saco");
            Furnish(goods, Kit.Props, "Crate_Wooden", new Vector3(-0.6f, 0f, -1.35f), 10f, 0.75f, "Caixote", solid: true);
            Furnish(goods, Kit.Props, "Crate_Wooden", new Vector3(-0.6f, 0.68f, -1.35f), -12f, 0.7f, "Caixote");
            Furnish(goods, Kit.Pirate, "barrel", new Vector3(0.7f, 0f, -1.4f), 20f, 0.5f, "Barril", solid: true);
            Furnish(goods, Kit.Food, "watermelon", new Vector3(-1.2f, 0f, 0.95f), 0f, 0.9f, "Melancia");
            Occluder(stall.gameObject);

            // Luzes da noite (13/11, pedido do Jhonata): duas lanternas penduradas nos postes da frente do toldo, com luz de verdade
            // (acendem com a noite e na tempestade, como as do porto), e uma vela no balcão.
            var lights = new GameObject("Luzes_Banca").transform;
            lights.SetParent(stall, false);
            // Os postes do toldo ficam em x ≈ +1,15 e −0,1 (a carroça é assimétrica: do outro lado ficam os varais).
            foreach (float x in new[] { 1.15f, -0.12f })
                NightLantern(Furnish(lights, Kit.Props, "Lantern_Wall", new Vector3(x, 1.5f, 0.42f), 0f, 0.45f, "Lanterna_Banca"), true, 6.5f, 3f);
            NightLantern(Furnish(lights, Kit.Props, "CandleStick", new Vector3(-0.15f * s, top, -0.12f * s), 0f, 0.9f, "Vela_Banca"), false);
        }
    }
}
