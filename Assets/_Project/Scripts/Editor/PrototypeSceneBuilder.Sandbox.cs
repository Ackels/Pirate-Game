using System.Linq;
using Pirates.Economy;
using Pirates.Sandbox;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>Modo sandbox (F1) nas cenas com itens: painel de debug com todos os itens de Data/Items.</summary>
    public static partial class PrototypeSceneBuilder
    {
        static void AddSandbox(ShipController player) =>
            new GameObject("Sandbox").AddComponent<SandboxPanel>().Configure(player, AllItems());

        /// <summary>Todos os itens de Data/Items (recursos, tripulação, esquemas), na ordem do painel.</summary>
        static ItemDefinition[] AllItems() =>
            AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item)
                .OrderBy(item => item.category)
                .ThenBy(item => item.rarity)
                .ThenBy(item => item.displayName)
                .ToArray();
    }
}
