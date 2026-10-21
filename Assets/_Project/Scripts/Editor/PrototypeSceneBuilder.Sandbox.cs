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

        /// <summary>
        /// Todos os itens de Data/Items (recursos, tripulação, esquemas), na ordem do painel. As lâminas entram também pela lista delas: a
        /// recém-criada nesta geração ainda não aparece na busca, e fora do registro o save perderia a lâmina ao carregar (C5, passo 3).
        /// </summary>
        static ItemDefinition[] AllItems() =>
            AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Concat(BladeItems())
                .Where(item => item)
                .Distinct()
                .OrderBy(item => item.category)
                .ThenBy(item => item.rarity)
                .ThenBy(item => item.displayName)
                .ToArray();
    }
}
