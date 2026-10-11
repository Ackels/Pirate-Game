using System.Collections.Generic;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Economy;
using Pirates.Loot;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Bomba de arremesso (pacote A, passo 4, 09/10): item "Bomba de pólvora" (Data/Items/Item_Bomba), lançada a pé com G
    /// (BombThrower no capitão: leva 3 do porão ao desembarcar), à venda no mercado (15 de ouro, vai para o porão, não compra de volta), na
    /// Forja (3 por 1 ferro + 2 pólvora, 60 s, Forja 1) e nos barris e baús de terra. O molde é a bala de canhão do Kenney com pavio e
    /// ponta acesa. Assets e listas existentes são preservados: só entra o que ainda não está lá.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const float BombDiameter = 0.28f;

        static ItemDefinition BombItem()
        {
            var item = Item("Item_Bomba", "Bomba de pólvora", ItemCategory.Resource, ItemRarity.Common, new Color(0.3f, 0.3f, 0.34f), 0.5f,
                "Bala oca cheia de pólvora, com pavio. A pé, G lança no cursor: explode ~1,6 s depois, derruba e acende barris.");
            if (item.keepAboard == 0)
            {
                item.keepAboard = 10;
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        /// <summary>Bombas no mercado e na Forja, se ainda não estão.</summary>
        static void AddBombTrade(HarborSettings settings, LootItems i, Pirates.Progression.BuildingDefinition forge)
        {
            var bomb = BombItem();
            var market = new List<HarborSettings.MarketEntry>(settings.market ?? System.Array.Empty<HarborSettings.MarketEntry>());
            if (!market.Exists(e => e != null && e.item == bomb))
            {
                int after = market.FindIndex(e => e != null && e.item == ShotItem());
                market.Insert(after >= 0 ? after + 1 : market.Count, Market(bomb, 15, 0, toHold: true));
                settings.market = market.ToArray();
            }
            var smithy = new List<HarborSettings.Recipe>(settings.smithy ?? System.Array.Empty<HarborSettings.Recipe>());
            if (!smithy.Exists(r => r?.result.item == bomb))
            {
                int after = smithy.FindIndex(r => r?.result.item == ShotItem());
                smithy.Insert(after >= 0 ? after + 1 : smithy.Count, new HarborSettings.Recipe
                {
                    result = new ItemStack(bomb, 3),
                    cost = new[] { new ItemStack(i.iron, 1), new ItemStack(i.powder, 2) },
                    requiredBuilding = forge, requiredLevel = 1,
                    craftSeconds = 60f,
                });
                settings.smithy = smithy.ToArray();
            }
            EditorUtility.SetDirty(settings);
        }

        static void AddBombLoot(LootTable barrel, LootTable chest)
        {
            AddLoot(barrel, BombItem(), 0.15f, 1, 1);
            AddLoot(chest, BombItem(), 0.35f, 1, 2);
        }

        /// <summary>
        /// Molde desligado da bomba ("Bombas/Bomba_Molde", fora do personagem): bala de canhão do Kenney medida para ~28 cm, pavio escuro e
        /// ponta acesa (emissiva, pisca pelo ThrownBomb). Sem colisor: quem para a bomba é o SphereCast dela.
        /// </summary>
        static ThrownBomb BombTemplate()
        {
            var root = new GameObject("Bombas").transform;
            var go = new GameObject("Bomba_Molde");
            go.transform.SetParent(root, false);
            var ball = Kenney("cannon-ball", go.transform, Vector3.zero, 0f, 1f, "Bala");
            var bounds = new Bounds(ball.transform.position, Vector3.zero);
            foreach (var r in ball.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float size = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            ball.transform.localScale = Vector3.one * (BombDiameter / size);
            // Centro da bala na origem (o pivô do Kenney é embaixo).
            ball.transform.localPosition = -(bounds.center - go.transform.position) * (BombDiameter / size);
            var dark = GetMaterial("M_Polvora_Tampa", new Color(0.09f, 0.08f, 0.07f), 0.2f);
            Part(PrimitiveType.Cylinder, "Pavio", go.transform, new Vector3(0f, BombDiameter * 0.5f + 0.04f, 0f), new Vector3(0.025f, 0.05f, 0.025f),
                Quaternion.Euler(0f, 0f, 15f), dark);
            var tip = Part(PrimitiveType.Sphere, "Faisca", go.transform, new Vector3(-0.02f, BombDiameter * 0.5f + 0.1f, 0f), Vector3.one * 0.05f,
                Quaternion.identity, GetEmissiveMaterial("M_Pavio_Aceso", new Color(1f, 0.55f, 0.15f), 3f));
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bomb = go.AddComponent<ThrownBomb>();
            bomb.Configure(tip.transform);
            go.SetActive(false);
            return bomb;
        }

        static void AddBombThrower(GameObject character, PlayerModeSwitcher switcher)
        {
            var thrower = character.AddComponent<BombThrower>();
            thrower.Configure(BombItem(), BombTemplate(), switcher);
            // Mira de segurar G (09/10, pedido do Jhonata): arco do voo, círculo da explosão e ponto do centro (cor posta pelo BombThrower).
            thrower.ConfigureAim(BombAimLine(character.transform, "ArcoDaBomba", 0.08f), BombAimLine(character.transform, "AreaDaBomba", 0.11f),
                BombAimLine(character.transform, "CentroDaBomba", 0.14f));
        }

        static LineRenderer BombAimLine(Transform parent, string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetTransparentMaterial("M_AimLine", Color.white);
            line.enabled = false;
            return line;
        }
    }
}
