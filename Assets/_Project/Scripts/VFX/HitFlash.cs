using System.Collections.Generic;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.VFX
{
    /// <summary>
    /// Clarão em quem leva golpe (revisão das animações, passo 5, 21/10): por ~0,09 s (tempo real, aparece durante a parada de impacto) as
    /// malhas do alvo trocam para cópias claras e brilhando dos próprios materiais e voltam. MaterialPropertyBlock não serve (GPU Resident
    /// Drawer); as cópias são feitas uma vez por material. Golpe segurado na guarda não pisca (a faísca é dele). Ouve o
    /// <see cref="CharacterCombat.Struck"/>.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] float seconds = 0.09f;
        [SerializeField] Color tint = new(1f, 0.93f, 0.82f);
        [SerializeField, Range(0f, 1f)] float whiten = 0.55f;
        [SerializeField] float emission = 0.9f;
        [Tooltip("Mais longe da câmera que isto, não pisca.")]
        [SerializeField] float maxDistance = 60f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");

        sealed class Flash
        {
            public Renderer[] renderers;
            public Material[][] original, shown;
            public float until;
        }

        readonly Dictionary<Component, Flash> active = new();
        readonly Dictionary<Component, Renderer[]> renderersOf = new();
        readonly Dictionary<Material, Material> copies = new();
        readonly List<Component> ended = new();

        void OnEnable() => CharacterCombat.Struck += OnStruck;

        void OnDisable()
        {
            CharacterCombat.Struck -= OnStruck;
            foreach (var pair in active) Restore(pair.Value);
            active.Clear();
        }

        void OnDestroy()
        {
            foreach (var copy in copies.Values) if (copy) Destroy(copy);
        }

        void OnStruck(CharacterCombat.StrikeReport report)
        {
            if ((report.flags & CharacterCombat.StrikeFlags.Blocked) != 0 || !report.victim) return;
            var cam = Camera.main;
            if (cam && (report.point - cam.transform.position).sqrMagnitude > maxDistance * maxDistance) return;
            if (active.TryGetValue(report.victim, out var running))
            {
                running.until = Time.unscaledTime + seconds;
                return;
            }
            if (!renderersOf.TryGetValue(report.victim, out var renderers))
            {
                var found = new List<Renderer>();
                foreach (var r in report.victim.GetComponentsInChildren<Renderer>())
                    if (r is SkinnedMeshRenderer || r is MeshRenderer) found.Add(r);
                renderersOf[report.victim] = renderers = found.ToArray();
            }
            if (renderers.Length == 0) return;
            var flash = new Flash { renderers = renderers, original = new Material[renderers.Length][], shown = new Material[renderers.Length][] };
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var mats = r.sharedMaterials;
                var lit = new Material[mats.Length];
                for (int m = 0; m < mats.Length; m++) lit[m] = Bright(mats[m]);
                flash.original[i] = mats;
                flash.shown[i] = lit;
                r.sharedMaterials = lit;
            }
            flash.until = Time.unscaledTime + seconds;
            active[report.victim] = flash;
        }

        void Update()
        {
            if (active.Count == 0) return;
            foreach (var pair in active)
                if (!pair.Key || Time.unscaledTime >= pair.Value.until) ended.Add(pair.Key);
            for (int i = 0; i < ended.Count; i++)
            {
                if (active.TryGetValue(ended[i], out var flash)) Restore(flash);
                active.Remove(ended[i]);
            }
            ended.Clear();
        }

        /// <summary>Volta os materiais de antes (se ninguém trocou no meio, como o fader da câmera).</summary>
        static void Restore(Flash flash)
        {
            for (int i = 0; i < flash.renderers.Length; i++)
            {
                var r = flash.renderers[i];
                if (!r || flash.original[i] == null) continue;
                var now = r.sharedMaterials;
                if (now.Length == flash.shown[i].Length && (now.Length == 0 || now[0] == flash.shown[i][0])) r.sharedMaterials = flash.original[i];
            }
        }

        Material Bright(Material source)
        {
            if (!source) return source;
            if (copies.TryGetValue(source, out var copy)) return copy;
            copy = new Material(source) { name = source.name + "_Clarao" };
            if (copy.HasProperty(BaseColor)) copy.SetColor(BaseColor, Color.Lerp(source.GetColor(BaseColor), tint, whiten));
            if (copy.HasProperty(EmissionColor))
            {
                copy.EnableKeyword("_EMISSION");
                copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                copy.SetColor(EmissionColor, tint * emission);
            }
            copies[source] = copy;
            return copy;
        }
    }
}
