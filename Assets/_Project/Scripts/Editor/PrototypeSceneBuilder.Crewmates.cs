using Pirates.Controls;
using Pirates.LandCombat;
using Pirates.OnFoot;
using Pirates.UI;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Marujos que descem com o capitão (D3, 30/10; pedido do Jhonata: "dois marujos lutando junto a pé"): dois personagens do kit com a roupa
    /// dos marujos do convés e o cutelo dos piratas, montados como os piratas (<see cref="CreatePirate"/>) mas com a <see cref="CrewmateAI"/> no
    /// lugar da IA inimiga e no time do capitão; desligados até o capitão desembarcar numa ilha (<see cref="CrewEscort"/>).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const float CrewmateHealth = 90f;

        static CrewEscort AddCrewEscort(PlayerModeSwitcher switcher, FootCharacter captain, Material iron, Material wood, Material brass)
        {
            var group = new GameObject("MarujosEmTerra").transform;
            var cutlass = EnemyCutlassAsset();
            var mates = new CrewmateAI[2];
            for (int i = 0; i < mates.Length; i++)
            {
                var (model, palette) = SailorLooks[i % SailorLooks.Length];
                var ai = CreatePirate(group, $"Marujo_Terra_{i}", captain.transform.position + new Vector3(i * 1.5f - 0.75f, 0f, -2f), 0f, cutlass,
                    captain, null, iron, wood, brass, model);
                var go = ai.gameObject;
                Object.DestroyImmediate(ai);
                go.GetComponent<CharacterCombat>().SetTeam(captain.GetComponent<CharacterCombat>().Team);
                go.GetComponent<Pirates.Combat.Health>().Configure(CrewmateHealth);
                // Acompanha o capitão (corre a 6 m/s): um pouco mais lento, mas não fica para trás.
                go.GetComponent<FootCharacter>().ConfigureSpeeds(2.4f, 5.8f);
                var outfit = CivilianMaterial(model, palette);
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int k = 0; k < slots.Length; k++) slots[k] = outfit;
                    r.sharedMaterials = slots;
                }
                var mate = go.AddComponent<CrewmateAI>();
                mate.Configure(captain.transform, i);
                mates[i] = mate;
            }
            var escort = switcher.gameObject.AddComponent<CrewEscort>();
            escort.Configure(switcher, mates, null);
            foreach (var m in mates) m.gameObject.SetActive(false);
            // Roda de comando (30/10): segurar T a pé; o anel no chão marca onde ir / em quem bater.
            var commander = captain.gameObject.AddComponent<CrewCommander>();
            commander.Configure(escort, BombAimLine(captain.transform, "MarcaDaOrdem", 0.1f));
            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<CrewEscortHUD>()) hud.gameObject.AddComponent<CrewEscortHUD>();
            if (hud && !hud.GetComponent<CrewWheelHUD>()) hud.gameObject.AddComponent<CrewWheelHUD>();
            return escort;
        }
    }
}
