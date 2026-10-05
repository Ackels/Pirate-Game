using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Papagaio do capitão no ombro (05/10, pedido do Jhonata): no kit o papagaio ("Ernest") é uma malha presa 100% ao antebraço do
    /// gancho; com as animações do Starter/Mixamo o braço desce e ele ficava pendurado no gancho. Aqui ele vira uma malha fixa
    /// (Meshes/Papagaio_Ernest, cópia sem pesos) presa ao osso do ombro esquerdo, onde está na pose parada do kit (a do retrato):
    /// acompanha o corpo e o ombro, não o braço. A malha original fica desligada.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Ajuste da posição no ombro (m, eixos do osso do ombro), medido em Play.</summary>
        static Vector3 ParrotNudge = new(0.181f, 0.002f, -0.153f);

        static void PinParrotToShoulder(GameObject model, string kitModel)
        {
            SkinnedMeshRenderer parrot = null;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "Ernest") parrot = r;
            Transform shoulder = FindChild(model.transform, "Shoulder.L");
            if (!parrot || !shoulder || !parrot.sharedMesh) return;
            int lower = System.Array.FindIndex(parrot.bones, b => b && b.name == "LowerArm.L");
            if (lower < 0) return;

            // Pose de referência: o personagem do kit parado (rig genérico, clipe Idle), papagaio no gancho erguido junto do ombro.
            var reference = KitCharacter(kitModel, null);
            var idle = KitClip(kitModel, "Idle");
            if (idle) idle.SampleAnimation(reference, 0.4f);
            var refShoulder = FindChild(reference.transform, "Shoulder.L");
            var refLower = FindChild(reference.transform, "LowerArm.L");
            Matrix4x4 forearmToShoulder = refShoulder.worldToLocalMatrix * refLower.localToWorldMatrix;
            Object.DestroyImmediate(reference);

            // Vértice da malha (espaço de bind) → antebraço (bindpose) → ombro (pose de referência) → + ajuste.
            float unit = Mathf.Max(0.0001f, shoulder.lossyScale.y);
            Matrix4x4 local = Matrix4x4.Translate(ParrotNudge / unit) * forearmToShoulder * parrot.sharedMesh.bindposes[lower];
            var go = new GameObject("Papagaio");
            go.transform.SetParent(shoulder, false);
            go.transform.localPosition = local.GetColumn(3);
            go.transform.localRotation = local.rotation;
            go.transform.localScale = local.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = StaticCopy(parrot.sharedMesh, "Papagaio_Ernest");
            go.AddComponent<MeshRenderer>().sharedMaterials = parrot.sharedMaterials;
            parrot.enabled = false;
        }

        /// <summary>Cópia da malha sem pesos de osso (para um MeshRenderer), salva em Meshes/ (atualizada no lugar: mesmo GUID).</summary>
        static Mesh StaticCopy(Mesh source, string name)
        {
            EnsureFolder(Root, "Meshes");
            string path = $"{MeshesFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = !mesh;
            if (create) mesh = new Mesh();
            else mesh.Clear();
            mesh.name = name;
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.uv = source.uv;
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(source.GetTriangles(s), s);
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
