using Pirates.Economy;
using UnityEditor;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Grupos dos filtros das janelas (H4, 28/10, pedido do Jhonata: filtrar o mercado e o armazém por recursos, munição, armas...). O
    /// resto o <see cref="ItemDefinition.Group"/> descobre sozinho (Auto); aqui só o que ele erraria. Só mexe em item ainda em Auto: o
    /// que o Jhonata escolher no Inspector fica.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static readonly (string file, ItemGroup group)[] ItemGroupTags =
        {
            ("Item_Bomba", ItemGroup.Ammo),
            ("Item_CargaCorrente", ItemGroup.Ammo),
            ("Item_CargaMetralha", ItemGroup.Ammo),
            ("Item_Cartuchos", ItemGroup.Ammo),
            ("Item_Chumbo", ItemGroup.Ammo),
            ("Item_Racao", ItemGroup.Food),
            ("Item_Peixe_Baiacu", ItemGroup.Food),
            ("Item_Peixe_Estrela", ItemGroup.Food),
            ("Item_Peixe_CavaloMarinho", ItemGroup.Food),
            ("Item_Perola", ItemGroup.Treasure),
            ("Item_PedacoMapa", ItemGroup.Treasure),
            ("Equip_Bacamarte", ItemGroup.Weapon),
        };

        /// <summary>Chamado a cada geração de cena com itens (e pelo eval, para não precisar regerar).</summary>
        public static void TagItemGroups()
        {
            foreach (var (file, group) in ItemGroupTags)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{file}.asset");
                if (!item || item.group != ItemGroup.Auto) continue;
                item.group = group;
                EditorUtility.SetDirty(item);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
