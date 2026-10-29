using System.Collections.Generic;
using Pirates.Combat;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Barris de pólvora (pacote A de 09/10, "mais coisas a pé"): o barril do Kenney com duas faixas vermelhas e a tampa preta (diferente
    /// dos barris de saque), perto de onde os piratas ficam, para o jogador explodir com um tiro. Acampamento da Caveira, esconderijo
    /// dos contrabandistas e o paiol do forte da Ilha Norte. Criados antes do NavMesh (viram obstáculo, como os caixotes); o
    /// acampamento dono os monta de novo quando é repovoado (<see cref="Pirates.LandCombat.EnemyCamp.SetKegs"/>).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string PowderKegGroup = "BarrisDePolvora";

        // No espaço do acampamento da Caveira: entre a fogueira e os bacamarteiros, atrás do portão norte, ao pé da torre e no portão sul.
        static readonly Vector3[] SkullCampKegs = { new(0.2f, 0f, 3.6f), new(3.8f, 0f, CampRadius - 4.6f), new(6.6f, 0f, -7.4f), new(0.8f, 0f, -11.8f) };
        // No espaço do esconderijo: perto do cais e da fogueira, e entre o atirador e o bacamarteiro.
        static readonly Vector3[] HideoutKegs = { new(1.8f, 0f, 3.2f), new(2.5f, 0f, -2.2f) };

        /// <summary>Barris nos pontos dados (locais de <paramref name="site"/>); se o ponto estiver ocupado, procura um livre perto.</summary>
        static PowderKeg[] PowderKegsAt(Transform site, IReadOnlyList<Vector3> locals)
        {
            CombatSystemsObject();
            var parent = new GameObject(PowderKegGroup).transform;
            parent.SetParent(site, false);
            var kegs = new List<PowderKeg>();
            Physics.SyncTransforms();
            for (int i = 0; i < locals.Count; i++)
            {
                if (!FreeKegSpot(site.TransformPoint(locals[i]), out Vector3 ground))
                {
                    Debug.LogWarning($"[Pirates] Barril de pólvora {site.name}/{i} sem lugar livre perto de {site.TransformPoint(locals[i])}.");
                    continue;
                }
                var keg = PowderKegAt(parent, $"BarrilPolvora_{i}", ground, i * 47f + 15f);
                kegs.Add(keg);
                Physics.SyncTransforms();
            }
            return kegs.ToArray();
        }

        /// <summary>Chão seco da ilha e nada (paliçada, cabana, caixote, pedra) num raio de 0,6 m; tenta em anéis até 2,5 m do ponto.</summary>
        static bool FreeKegSpot(Vector3 wanted, out Vector3 ground)
        {
            for (float ring = 0f; ring <= 2.5f; ring += 0.5f)
            {
                int steps = ring == 0f ? 1 : 8;
                for (int s = 0; s < steps; s++)
                {
                    float a = s * Mathf.PI * 2f / steps;
                    var p = wanted + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                    // Só no chão da ilha (malha côncava): o raio de cima também acha telhado, caixote e pedra.
                    if (!Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)
                        || hit.collider is not MeshCollider { convex: false } || hit.point.y < 0.5f) continue;
                    ground = hit.point;
                    if (!KegSpotBlocked(ground)) return true;
                }
            }
            ground = default;
            return false;
        }

        static readonly Collider[] kegProbe = new Collider[16];

        static bool KegSpotBlocked(Vector3 ground)
        {
            int count = Physics.OverlapSphereNonAlloc(ground + Vector3.up * 0.75f, 0.6f, kegProbe, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                // O chão da ilha (malha côncava) sempre encosta na esfera em ladeira: só conta o que está em cima dele.
                if (kegProbe[i] is MeshCollider { convex: false }) continue;
                // Outro barril de pólvora pode ficar encostado (pilhas no paiol: explosão em cadeia).
                if (kegProbe[i].GetComponent<PowderKeg>()) continue;
                return true;
            }
            return false;
        }

        static PowderKeg PowderKegAt(Transform parent, string name, Vector3 ground, float yaw)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // O barril do ícone (29/10, pedido do Jhonata: como o do Sea of Thieves): vermelho, deitado num cavalete, aros pretos, etiqueta
            // com os ossos cruzados e o pavio em cima; ~0,9 m de comprido e 0,9 m de altura até o pavio, como o barril do Kenney de antes.
            PowderKegIcon(visual);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.42f, 0f);
            col.size = new Vector3(0.95f, 0.84f, 0.7f);
            var keg = root.AddComponent<PowderKeg>();
            keg.Configure(visual.gameObject);
            return keg;
        }
    }
}
