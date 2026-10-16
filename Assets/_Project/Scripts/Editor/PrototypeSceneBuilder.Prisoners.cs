using Pirates.Controls;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Prisioneiros em gaiolas (C2, 16/10; pacote C, exploração; cena 07): uma gaiola de madeira com barras de ferro na borda de três
    /// acampamentos (Caveira, contrabandistas, ruínas do Barba-Negra), a porta virada para o centro do acampamento, e um morador do kit
    /// com roupa de prisioneiro dentro. Lugares achados na cena (chão plano, nada em volta, alcançável a pé, ≥ 2,5 m dos postos); o
    /// forte da Ilha Norte ficou sem (pátio pequeno, torre no meio). O capitão ganha o <see cref="PrisonerRescuer"/> (F solta e paga).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        sealed class PrisonerSpec
        {
            public string camp, name, model;
            public Vector2 offset;
            public int palette, crew, gold, mapPieces, fame;
            public string[] thanks;
        }

        static readonly PrisonerSpec[] Prisoners =
        {
            new()
            {
                camp = "acampamento_caveira", offset = new Vector2(-2.7f, 13.7f), name = "Joaquim, o pescador", model = KitCharacters.Henry,
                palette = 8, crew = 1, gold = 40, fame = 5,
                thanks = new[]
                {
                    "Os cobradores me pegaram por atrasar o tributo! Três dias a pão e água... Vou com você, capitão.",
                    "Que Deus te pague, capitão! Eu remo, puxo cabo e conserto rede. Me leva no seu navio!",
                },
            },
            new()
            {
                camp = "esconderijo_oeste", offset = new Vector2(-9.9f, 9.9f), name = "Marta, a marinheira", model = KitCharacters.Anne,
                palette = 1, crew = 1, mapPieces = 1, fame = 5,
                thanks = new[]
                {
                    "Esses ratos iam me vender no próximo porto! Tome: roubei este pedaço de mapa do chefe deles.",
                    "Finalmente! Sei atirar, capitão, e não esqueço quem me tira de uma jaula. Ah, e isto aqui é seu: um pedaço de mapa.",
                },
            },
            new()
            {
                camp = "covil_barbanegra", offset = new Vector2(-10.8f, 2.1f), name = "Tenente Duarte, da Coroa", model = KitCharacters.Henry,
                palette = 5, crew = 1, gold = 150, mapPieces = 1, fame = 15,
                thanks = new[]
                {
                    "O Barba-Negra me guardava como troféu. O almirante vai saber disso! Leve o soldo que escondi e este pedaço de mapa.",
                    "Um oficial da Coroa devendo a vida a um pirata... Que seja. Conte comigo a bordo, capitão.",
                },
            },
        };

        static void AddPrisoners(FootCharacter captain, PlayerModeSwitcher switcher, LandMaterials m, LootItems items)
        {
            var camps = Object.FindObjectsByType<EnemyCamp>(FindObjectsInactive.Include);
            var root = new GameObject("Prisioneiros").transform;
            foreach (var spec in Prisoners)
            {
                EnemyCamp camp = null;
                foreach (var c in camps)
                    if (c.CampId == spec.camp) camp = c;
                if (!camp)
                {
                    Debug.LogWarning($"[Pirates] Prisioneiro sem o acampamento {spec.camp}.");
                    continue;
                }
                Vector3 center = camp.transform.position;
                Vector3 spot = center + new Vector3(spec.offset.x, 0f, spec.offset.y);
                if (!IslandFloor(spot, out spot))
                {
                    Debug.LogWarning($"[Pirates] Prisioneiro {spec.name}: sem chão em {spot}.");
                    continue;
                }
                Vector3 toCamp = center - spot;
                toCamp.y = 0f;
                var cage = Cage(root, "Gaiola_" + spec.camp, spot, Quaternion.LookRotation(toCamp), m, out var door);

                var holder = new GameObject("Prisioneiro").transform;
                holder.SetParent(cage, false);
                holder.localPosition = new Vector3(0f, 0.1f, -0.15f);
                var visual = KitCharacter(spec.model, holder, CivilianMaterial(spec.model, spec.palette));
                if (!visual.TryGetComponent(out Animator animator)) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = CivilAnimator(spec.model);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var reward = new System.Collections.Generic.List<ItemStack>();
                if (spec.gold > 0) reward.Add(new ItemStack(items.gold, spec.gold));
                if (spec.mapPieces > 0) reward.Add(new ItemStack(MapPieceItem(), spec.mapPieces));
                cage.gameObject.AddComponent<Prisoner>().Configure(spec.name, camp, animator, holder.gameObject, door, spec.crew,
                    reward.ToArray(), spec.fame, spec.thanks);
            }
            captain.gameObject.AddComponent<PrisonerRescuer>().Configure(switcher, items.gold);
        }

        /// <summary>Chão da ilha (só o colisor das ilhas: árvores e pedras têm colisor também).</summary>
        static bool IslandFloor(Vector3 near, out Vector3 floor)
        {
            floor = near;
            var hits = Physics.RaycastAll(near + Vector3.up * 40f, Vector3.down, 80f);
            float best = float.MinValue;
            foreach (var h in hits)
                if (h.collider is MeshCollider && h.collider.name.StartsWith("Ilha") && h.point.y > best)
                {
                    best = h.point.y;
                    floor = h.point;
                }
            return best > float.MinValue;
        }

        /// <summary>
        /// Gaiola de 2 × 2 × 2,2 m: piso e teto de tábuas, quatro postes, barras de ferro de três lados e a porta de barras na frente (+Z),
        /// com dobradiça no poste da esquerda. Colisores nas paredes e na porta (que gira junto).
        /// </summary>
        static Transform Cage(Transform parent, string name, Vector3 ground, Quaternion rotation, LandMaterials m, out Transform door)
        {
            var cage = new GameObject(name).transform;
            cage.SetParent(parent, false);
            cage.SetPositionAndRotation(ground, rotation);
            const float half = 0.95f, height = 2.2f;
            Part(PrimitiveType.Cube, "Piso", cage, new Vector3(0f, 0.05f, 0f), new Vector3(2.05f, 0.1f, 2.05f), Quaternion.identity, m.wood);
            Part(PrimitiveType.Cube, "Teto", cage, new Vector3(0f, height + 0.05f, 0f), new Vector3(2.25f, 0.1f, 2.25f), Quaternion.identity, m.wood);
            foreach (var (x, z) in new[] { (-half, -half), (half, -half), (-half, half), (half, half) })
                Part(PrimitiveType.Cube, "Poste", cage, new Vector3(x, height * 0.5f, z), new Vector3(0.14f, height, 0.14f), Quaternion.identity, m.wood);

            // Fundo (−Z) e lados (±X): duas travessas de madeira e cinco barras de ferro em cada.
            // Cada parede é o mesmo painel em z = +half, girado: 180° = fundo, −90° = esquerda, 90° = direita.
            foreach (float yaw in new[] { 180f, -90f, 90f })
            {
                var wall = new GameObject("Parede").transform;
                wall.SetParent(cage, false);
                wall.localRotation = Quaternion.Euler(0f, yaw, 0f);
                BarPanel(wall, new Vector3(0f, 0f, half), m);
            }
            AddBox(cage, new Vector3(0f, height * 0.5f, -half), new Vector3(2f, height, 0.15f));
            AddBox(cage, new Vector3(-half, height * 0.5f, 0f), new Vector3(0.15f, height, 2f));
            AddBox(cage, new Vector3(half, height * 0.5f, 0f), new Vector3(0.15f, height, 2f));

            // Porta: dobradiça no poste da frente à esquerda; as barras saem dela para +X.
            door = new GameObject("Porta").transform;
            door.SetParent(cage, false);
            door.localPosition = new Vector3(-half + 0.07f, 0f, half);
            var panel = new GameObject("Barras").transform;
            panel.SetParent(door, false);
            panel.localPosition = new Vector3(half - 0.07f, 0f, -half);
            BarPanel(panel, new Vector3(0f, 0f, half), m);
            var lockBox = Part(PrimitiveType.Cube, "Cadeado", door, new Vector3(1.7f, 1.05f, 0.06f), new Vector3(0.16f, 0.2f, 0.08f),
                Quaternion.identity, m.brass);
            lockBox.name = "Cadeado";
            AddBox(door, new Vector3(half - 0.07f, height * 0.5f, 0f), new Vector3(1.9f, height, 0.12f));
            return cage;
        }

        /// <summary>Um lado de barras em z = <paramref name="at"/>.z: travessas em cima e embaixo e cinco barras de ferro.</summary>
        static void BarPanel(Transform parent, Vector3 at, LandMaterials m)
        {
            Part(PrimitiveType.Cube, "Travessa", parent, at + new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.09f, 0.09f), Quaternion.identity, m.wood);
            Part(PrimitiveType.Cube, "Travessa", parent, at + new Vector3(0f, 1.95f, 0f), new Vector3(1.8f, 0.09f, 0.09f), Quaternion.identity, m.wood);
            for (int k = 0; k < 5; k++)
            {
                float x = -0.64f + k * 0.32f;
                Part(PrimitiveType.Cylinder, "Barra", parent, at + new Vector3(x, 1.1f, 0f), new Vector3(0.05f, 1.05f, 0.05f), Quaternion.identity, m.iron);
            }
        }

        static void AddBox(Transform parent, Vector3 center, Vector3 size)
        {
            var box = parent.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
    }
}
