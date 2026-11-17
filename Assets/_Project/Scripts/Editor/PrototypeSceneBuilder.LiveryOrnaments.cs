using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Pintura do navio, passo 3 (16/11): carranca na proa e lanternas a mais. Cada casco ganha um grupo "Ornamentos" (filho do "Visual"
    /// dele) com todas as peças montadas e desligadas; o ShipLivery liga a escolhida. O lugar vem de raios contra a malha do casco: o pé
    /// da proa (o costado da frente na altura da água) e o bico (onde a frente avança de repente), os cantos do castelo de proa e da popa.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static readonly Color FigureheadPainted = new(0.86f, 0.8f, 0.68f);
        static readonly Color FigureheadGold = new(0.95f, 0.7f, 0.26f);
        static readonly Color GhostLantern = new(0.45f, 1f, 0.55f);

        static Transform LiveryOrnaments(Transform ship, Transform rig, Transform hull, Mesh hullMesh, float xs = 1f, float ys = 1f, float k = 1f)
        {
            // xs/ys = escala do casco (o palco da abordagem tem o navio 3× no comprimento e na largura e 2× na altura); k = tamanho da carranca.
            var old = rig.Find("Ornamentos");
            if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Ornamentos").transform;
            group.SetParent(rig, false);

            // ---------- Medidas (no espaço do navio) ----------
            var probe = new GameObject("MedirCasco");
            probe.transform.SetPositionAndRotation(hull.position, hull.rotation);
            probe.transform.localScale = hull.lossyScale;
            var mc = probe.AddComponent<MeshCollider>();
            mc.sharedMesh = hullMesh;
            Physics.SyncTransforms();
            float FrontZ(float y)
            {
                var o = ship.TransformPoint(new Vector3(0f, y, 40f));
                return mc.Raycast(new Ray(o, -ship.forward), out var h, 80f) ? ship.InverseTransformPoint(h.point).z : float.NegativeInfinity;
            }
            float stemZ = FrontZ(1f * ys);
            float beakY = 2f;
            beakY *= ys;
            for (float y = 1f * ys; y < 3.5f * ys; y += 0.05f * ys)
                if (FrontZ(y) > stemZ + 0.6f * xs) { beakY = y; break; }
            var rails = new List<Vector3>();
            for (float z = -12f * xs; z <= 12f * xs; z += 0.5f)
            {
                var o = ship.TransformPoint(new Vector3(12f * xs, 1.5f * ys, z));
                if (!mc.Raycast(new Ray(o, -ship.right), out var h, 24f * xs)) continue;
                float x = ship.InverseTransformPoint(h.point).x;
                var top = ship.TransformPoint(new Vector3(x - 0.1f, 9f * ys, z));
                if (mc.Raycast(new Ray(top, -ship.up), out var h2, 20f * ys)) rails.Add(new Vector3(x, ship.InverseTransformPoint(h2.point).y, z));
            }
            Object.DestroyImmediate(probe);
            Vector3 Corner(bool front)
            {
                float max = float.NegativeInfinity;
                foreach (var r in rails) if ((r.z > 0.5f * xs) == front && r.y > max) max = r.y;
                Vector3 best = default;
                bool found = false;
                foreach (var r in rails)
                {
                    if ((r.z > 0.5f * xs) != front || r.y < max - 0.05f) continue;
                    // proa: o mais para trás do castelo (o canto largo); popa: o mais para trás do tombadilho
                    if (!found || (front ? r.z < best.z : r.z < best.z)) { best = r; found = true; }
                }
                return found ? new Vector3(best.x - 0.22f, best.y, best.z + (front ? 0.2f : 0.35f)) : new Vector3(1.5f * xs, 2.3f * ys, (front ? 2f : -3f) * xs);
            }
            var bow = Corner(true);
            var stern = Corner(false);
            Vector3 Local(Vector3 shipPoint) => rig.InverseTransformPoint(ship.TransformPoint(shipPoint));

            // ---------- Carrancas ----------
            var painted = GetMaterial("M_Carranca_Pintada", FigureheadPainted, 0.35f);
            var gold = GetMaterial("M_Carranca_Dourada", FigureheadGold, 0.72f);
            if (gold.HasProperty("_Metallic") && gold.GetFloat("_Metallic") < 0.5f) { gold.SetFloat("_Metallic", 0.65f); EditorUtility.SetDirty(gold); }
            // A dama sai do pé da proa logo abaixo do bico, inclinada para a frente (as pernas ficam dentro do casco), na pose parada do
            // kit (braços junto do corpo; a de repouso tem os braços abertos) e com a cabeça erguida olhando o mar.
            Vector3 ladyBase = new(0f, beakY - 1.3f * k, stemZ - 0.12f * k);
            var idle = AssetDatabase.LoadAllAssetsAtPath($"{QuaterniusFolder}/{KitCharacters.Anne}.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name.EndsWith("|Idle"));
            foreach (var (name, mat) in new[] { ("Carranca_Dama", painted), ("Carranca_DamaDourada", gold) })
            {
                var holder = new GameObject(name).transform;
                holder.SetParent(group, false);
                holder.localPosition = Local(ladyBase);
                holder.localRotation = Quaternion.Inverse(rig.rotation) * ship.rotation * Quaternion.Euler(38f, 0f, 0f);
                holder.localScale = Vector3.one * 0.95f * k;
                var lady = KitCharacter(KitCharacters.Anne, holder, mat);
                if (idle) idle.SampleAnimation(lady, 0f);
                var neck = lady.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Neck");
                if (neck) neck.rotation = Quaternion.AngleAxis(-34f, holder.right) * neck.rotation;
                foreach (var c in holder.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var a in holder.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            }
            {
                var holder = new GameObject("Carranca_Caveira").transform;
                holder.SetParent(group, false);
                holder.localPosition = Local(new Vector3(0f, beakY - 0.62f * k, stemZ + 0.2f * k));
                holder.localRotation = Quaternion.Inverse(rig.rotation) * ship.rotation * Quaternion.Euler(14f, 0f, 0f);
                var skull = Model(Kit.Ruins, "Skull", holder, Vector3.zero, 0f, 1f, "Caveira");
                foreach (var c in skull.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var b = RenderBounds(skull);
                float s = 1.15f * k / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                skull.transform.localScale *= s;
                b = RenderBounds(skull);
                skull.transform.position += holder.position - b.center;
                foreach (var r in skull.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = gold;
                    r.sharedMaterials = slots;
                }
            }

            // ---------- Lanternas ----------
            var postMat = GetMaterial("M_Poste_Lanterna", new Color(0.33f, 0.21f, 0.14f), 0.2f);
            void Lanterns(string name, bool sternToo, Color? tint)
            {
                var g = new GameObject(name).transform;
                g.SetParent(group, false);
                var spots = new List<(Vector3 p, bool light)> { (bow, true), (new Vector3(-bow.x, bow.y, bow.z), true) };
                if (sternToo) { spots.Add((stern, false)); spots.Add((new Vector3(-stern.x, stern.y, stern.z), false)); }
                int k = 0;
                foreach (var (p, light) in spots)
                {
                    // Postinho de madeira na amurada com a lanterna de vidro em cima.
                    Part(PrimitiveType.Cube, $"Poste_{k}", g, Local(p + Vector3.up * 0.25f), new Vector3(0.1f, 0.5f, 0.1f), Quaternion.identity, postMat);
                    var lantern = Model(Kit.Graveyard, "lantern-glass", g, Local(p + Vector3.up * 0.5f), 0f, 1.4f, $"Lanterna_{k++}");
                    foreach (var c in lantern.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    NightLantern(lantern, light, range: 7f, intensity: 2.6f);
                    if (tint is not Color color) continue;
                    var glowMat = GetEmissiveMaterial("M_LanternaVerde", color, 2.4f);
                    foreach (var r in lantern.GetComponentsInChildren<Renderer>(true))
                        if (r.name == "BrilhoDaLanterna") { r.sharedMaterial = glowMat; r.shadowCastingMode = ShadowCastingMode.Off; }
                    foreach (var l in lantern.GetComponentsInChildren<Light>(true)) l.color = color;
                }
            }
            Lanterns("Lanternas_Proa", false, null);
            Lanterns("Lanternas_Todas", true, null);
            Lanterns("Lanternas_Verdes", true, GhostLantern);

            for (int i = 0; i < group.childCount; i++) group.GetChild(i).gameObject.SetActive(false);
            return group;
        }

        static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
