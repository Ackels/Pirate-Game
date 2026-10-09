using System.Collections.Generic;
using Pirates.Progression;
using Pirates.Ships;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Marujos no convés (M12, passo 1, cenas 05 e 07): o <see cref="DeckCrew"/> no navio do jogador, com o convés de cada casco medido
    /// aqui (grade de pontos livres na altura do convés principal, longe da amurada, dos mastros e dos canhões; estações nos mastros e
    /// atrás de cada canhão) e os marujos (moradores do kit recoloridos, rig genérico com as animações do próprio kit; sem colisor).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string SailorAnimatorPath = AnimationFolder + "/Marujo.controller";
        const float DeckCell = 0.25f;

        /// <summary>Visuais dos marujos: Henry de bandana nas 4 cores e Anne em 3 (mais de um igual no convés pequeno não aparece tanto).</summary>
        static readonly (string model, int palette)[] SailorLooks =
        {
            (KitCharacters.Henry, 1), (KitCharacters.Henry, 3), (KitCharacters.Anne, 1), (KitCharacters.Henry, 2),
            (KitCharacters.Henry, 0), (KitCharacters.Anne, 3), (KitCharacters.Anne, 2),
        };

        static void AddDeckCrew(ShipController player)
        {
            if (!HasKitCharacter(KitCharacters.Henry)) return;
            var root = player.transform;
            var layouts = new List<DeckCrew.Layout>();
            var sloop = AssetDatabase.LoadAssetAtPath<ShipClassDefinition>(ShipsFolder + "/ShipClass_Chalupa.asset");
            var schooner = AssetDatabase.LoadAssetAtPath<ShipClassDefinition>(ShipsFolder + "/ShipClass_Escuna.asset");
            if (root.Find("Visual/Casco") is { } sloopHull && root.Find("Cannons") is { } sloopCannons)
                layouts.Add(MeasureDeck(root, sloopHull, sloopCannons, sloop, 4));
            if (root.Find("Visual_Escuna/Casco") is { } schoonerHull && root.Find("Cannons_Escuna") is { } schoonerCannons)
                layouts.Add(MeasureDeck(root, schoonerHull, schoonerCannons, schooner, 6));
            var galleon = AssetDatabase.LoadAssetAtPath<ShipClassDefinition>(ShipsFolder + "/ShipClass_Galeao.asset");
            if (root.Find("Visual_Galeao/Casco") is { } galleonHull && root.Find("Cannons_Galeao") is { } galleonCannons)
                layouts.Add(MeasureDeck(root, galleonHull, galleonCannons, galleon, 8));

            int count = 0;
            foreach (var l in layouts) count = Mathf.Max(count, l.maxSailors);
            var group = new GameObject("Marujos").transform;
            group.SetParent(root, false);
            var animators = new Animator[count];
            for (int i = 0; i < count; i++)
            {
                var (model, palette) = SailorLooks[i % SailorLooks.Length];
                // Suporte (o DeckCrew anda e gira ele) com o modelo animado dentro.
                var holder = new GameObject($"Marujo_{i}").transform;
                holder.SetParent(group, false);
                var visual = KitCharacter(model, holder, CivilianMaterial(model, palette));
                if (!visual.TryGetComponent(out Animator animator)) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = SailorAnimator(model);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animators[i] = animator;
            }
            player.gameObject.AddComponent<DeckCrew>().Configure(layouts.ToArray(), animators);
        }

        /// <summary>
        /// Convés de um casco no espaço do navio, em todos os níveis (principal, popa, proa: cada um uma área). Convés principal = a altura
        /// do canhão mais baixo; lá ficam as estações dos canhões e dos mastros (raio de lado acerta o casco perto do meio a 1,2 m do
        /// convés). Ponto livre = chão plano em volta, nada do casco encostando (esfera no joelho) e longe dos canhões.
        /// </summary>
        static DeckCrew.Layout MeasureDeck(Transform root, Transform hull, Transform cannonsRoot, ShipClassDefinition shipClass, int maxSailors)
        {
            var cannons = new List<Transform>();
            foreach (var t in cannonsRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Canhao_")) cannons.Add(t);
            var heights = new List<float>();
            foreach (var c in cannons) heights.Add(root.InverseTransformPoint(c.position).y);
            heights.Sort();
            // Convés principal = o mais baixo com canhão (na chalupa, os da popa e da proa ficam nos castelos, 0,7–0,85 m acima).
            float deckY = heights.Count > 0 ? heights[0] : ShipDeckY;

            var layout = new DeckCrew.Layout { shipClass = shipClass, maxSailors = maxSailors, deckY = deckY, cell = DeckCell };
            const float halfWidth = 2.3f, halfLength = 6f;
            layout.gridMin = new Vector2(-halfWidth, -halfLength);
            layout.cols = Mathf.CeilToInt(2f * halfWidth / DeckCell);
            layout.rows = Mathf.CeilToInt(2f * halfLength / DeckCell);
            int cells = layout.cols * layout.rows;
            var height = new float[cells];
            var clear = new bool[cells];
            var masts = new List<Vector3>();

            using (var probe = new DeckProbe(hull))
            {
                for (int cz = 0; cz < layout.rows; cz++)
                for (int cx = 0; cx < layout.cols; cx++)
                {
                    int i = cz * layout.cols + cx;
                    height[i] = float.NaN;
                    var local = new Vector3(layout.gridMin.x + (cx + 0.5f) * DeckCell, deckY, layout.gridMin.y + (cz + 0.5f) * DeckCell);
                    var m = hull.InverseTransformPoint(root.TransformPoint(local));
                    // De baixo da verga mais baixa; sem acerto, o raio nasceu dentro do castelo de popa/proa (mais alto): tenta de cima.
                    float h = probe.DeckHeight(m.x, m.z);
                    if (float.IsNaN(h)) h = probe.DeckHeight(m.x, m.z, 6f);
                    if (float.IsNaN(h)) continue;
                    float y = root.InverseTransformPoint(hull.TransformPoint(new Vector3(m.x, h, m.z))).y;
                    // Porão/escotilha abaixo do convés principal não vale.
                    if (y < deckY - 0.05f) continue;
                    height[i] = y;
                    // Nada encostando na altura do joelho (amurada, mastro, parede da cabine). Mais alto não: as vergas passam a ~1 m do convés.
                    var at = root.TransformPoint(new Vector3(local.x, y, local.z));
                    clear[i] = !probe.Touches(at + root.up * 0.5f, 0.3f);
                }

                // Mastros do convés principal (estações dos cabos): linhas de z onde o raio de lado a 1,2 m do convés bate perto do meio.
                float start = float.NaN, last = 0f;
                for (float z = -halfLength; z <= halfLength + 0.001f; z += 0.1f)
                {
                    var m = hull.InverseTransformPoint(root.TransformPoint(new Vector3(0f, deckY + 1.2f, z)));
                    bool hit = probe.HitsNearCenter(m.y, m.z, 0.45f);
                    if (hit && float.IsNaN(start)) start = z;
                    if (hit) last = z;
                    if (!hit && !float.IsNaN(start))
                    {
                        masts.Add(new Vector3(0f, deckY, (start + last) * 0.5f));
                        start = float.NaN;
                    }
                }
            }

            // Ponto livre: chão plano em volta (0,25 m em toda direção, mesma altura: longe da beira e dos degraus), nada encostando
            // (a esfera do joelho afasta ~0,3 m da amurada) e longe dos canhões. Vale qualquer nível (convés principal, popa, proa); cada nível é uma área à parte.
            float H(int cx, int cz) => cx >= 0 && cz >= 0 && cx < layout.cols && cz < layout.rows ? height[cz * layout.cols + cx] : float.NaN;
            layout.heights = height;
            layout.walkable = new bool[cells];
            for (int cz = 0; cz < layout.rows; cz++)
            for (int cx = 0; cx < layout.cols; cx++)
            {
                int i = cz * layout.cols + cx;
                float h0 = height[i];
                if (float.IsNaN(h0) || !clear[i]) continue;
                bool Same(int x, int z) => Mathf.Abs(H(x, z) - h0) < 0.06f;
                bool ok = true;
                for (int dz = -1; dz <= 1 && ok; dz++)
                for (int dx = -1; dx <= 1 && ok; dx++)
                    ok = Same(cx + dx, cz + dz);
                if (!ok) continue;
                var p = new Vector3(layout.gridMin.x + (cx + 0.5f) * DeckCell, h0, layout.gridMin.y + (cz + 0.5f) * DeckCell);
                foreach (var c in cannons)
                {
                    var o = root.InverseTransformPoint(c.position);
                    // Caixa do canhão (carreta + cano para fora), não um círculo: sobra o corredor entre um canhão e outro.
                    float along = (p.x - o.x) * Mathf.Sign(o.x);
                    if (Mathf.Abs(o.y - h0) < 0.3f && Mathf.Abs(p.z - o.z) < 0.35f && along > -0.25f && along < 0.95f) ok = false;
                }
                layout.walkable[i] = ok;
            }
            // Áreas pequenas demais (tampa de cabine, degrau largo) saem.
            var seen = new bool[cells];
            var area = new List<int>();
            for (int i = 0; i < cells; i++)
            {
                if (!layout.walkable[i] || seen[i]) continue;
                area.Clear();
                var stack = new Stack<int>();
                stack.Push(i);
                seen[i] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    area.Add(c);
                    int cx = c % layout.cols, cz = c / layout.cols;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= layout.cols || nz >= layout.rows) continue;
                        int n = nz * layout.cols + nx;
                        if (seen[n] || !layout.walkable[n] || Mathf.Abs(height[n] - height[c]) >= 0.06f) continue;
                        seen[n] = true;
                        stack.Push(n);
                    }
                }
                if (area.Count < 8)
                    foreach (int c in area) layout.walkable[c] = false;
            }

            Vector3? Snap(Vector3 want, float within)
            {
                Vector3? best = null;
                float bestDistance = within;
                for (int i = 0; i < cells; i++)
                {
                    if (!layout.walkable[i] || Mathf.Abs(height[i] - deckY) > 0.06f) continue;
                    var p = new Vector3(layout.gridMin.x + (i % layout.cols + 0.5f) * DeckCell, deckY, layout.gridMin.y + (i / layout.cols + 0.5f) * DeckCell);
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(want.x, want.z));
                    if (d < bestDistance) { bestDistance = d; best = p; }
                }
                return best;
            }

            var guns = new List<DeckCrew.Station>();
            foreach (var c in cannons)
            {
                var o = root.InverseTransformPoint(c.position);
                float side = Mathf.Sign(o.x);
                // Canhão dos castelos (popa/proa, mais alto): sem artilheiro, ninguém sobe lá neste passo.
                if (o.y - deckY > 0.1f) continue;
                if (Snap(o - new Vector3(side * 0.5f, 0f, 0f), 0.65f) is not { } p) continue;
                guns.Add(new DeckCrew.Station { position = p, yaw = side * 90f, side = (int)side });
            }
            var rigging = new List<DeckCrew.Station>();
            foreach (var mast in masts)
                foreach (float side in new[] { -1f, 1f })
                {
                    if (Snap(mast + new Vector3(side * 0.75f, 0f, 0f), 0.6f) is not { } p) continue;
                    var to = mast - p;
                    rigging.Add(new DeckCrew.Station { position = p, yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, side = 0 });
                }
            layout.guns = guns.ToArray();
            layout.rigging = rigging.ToArray();

            int free = 0, main = 0;
            for (int i = 0; i < cells; i++)
                if (layout.walkable[i])
                {
                    free++;
                    if (Mathf.Abs(height[i] - deckY) < 0.06f) main++;
                }
            Debug.Log($"[Pirates] Convés {shipClass?.displayName ?? hull.parent.name}: {free} pontos livres ({main} no convés principal), {masts.Count} mastro(s), " +
                      $"{guns.Count} de {cannons.Count} canhões com lugar, {rigging.Count} lugar(es) nos cabos, altura {deckY:0.00}.");
            return layout;
        }

        /// <summary>
        /// Animator dos marujos: locomoção (parado, andar 1,1, correr 3,2 pela "Speed") e as ações que voltam sozinhas para ela: "Puxar"
        /// (soco do kit: puxar cabo e socar a bala no canhão), "Abaixar" (coice do canhão), "Tranco" (tiro no casco), "Sim"/"Nao"
        /// (conversa); "Morte" fica no chão. Clipes do Henry; Anne ganha um override com os dela. Criados uma vez.
        /// </summary>
        static RuntimeAnimatorController SailorAnimator(string model)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SailorAnimatorPath);
            if (!controller)
            {
                EnsureFolder(Root, "Animation");
                controller = AnimatorController.CreateAnimatorControllerAtPath(SailorAnimatorPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                var machine = controller.layers[0].stateMachine;
                var locomotion = controller.CreateBlendTreeInController("Locomocao", out var tree);
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                tree.AddChild(KitClip(KitCharacters.Henry, "Idle"), 0f);
                tree.AddChild(KitClip(KitCharacters.Henry, "Walk"), 1.1f);
                tree.AddChild(KitClip(KitCharacters.Henry, "Run"), 3.2f);
                machine.defaultState = locomotion;
                foreach (var (state, clip, back) in new[]
                         {
                             ("Puxar", "Punch", true), ("Abaixar", "Duck", true), ("Tranco", "HitReact", true),
                             ("Sim", "Yes", true), ("Nao", "No", true), ("Morte", "Death", false),
                         })
                {
                    var s = machine.AddState(state);
                    s.motion = KitClip(KitCharacters.Henry, clip);
                    if (!back) continue;
                    var t = s.AddTransition(locomotion);
                    t.hasExitTime = true;
                    t.exitTime = 0.92f;
                    t.duration = 0.2f;
                }
                AssetDatabase.SaveAssets();
            }
            if (model == KitCharacters.Henry) return controller;

            string path = $"{AnimationFolder}/Marujo_{model.Replace("Characters_", "")}.overrideController";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (existing) return existing;
            var overrides = new AnimatorOverrideController(controller);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var clip in overrides.animationClips)
                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, KitClip(model, clip.name.Substring(clip.name.LastIndexOf('|') + 1))));
            overrides.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(overrides, path);
            return overrides;
        }
    }
}
