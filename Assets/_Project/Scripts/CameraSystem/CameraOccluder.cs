using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.CameraSystem
{
    /// <summary>
    /// Algo grande do cenário que pode tapar o personagem (casa, árvore, torre, fumaça, ruína): quando fica entre a câmera e
    /// o alvo, o CameraOcclusionFader deixa ele translúcido. Os materiais translúcidos vêm prontos do gerador (cópias
    /// "_Fade" dos originais, transparentes); a transparência de cada um é pelo MaterialPropertyBlock (o asset não muda).
    /// A sombra continua.
    /// </summary>
    public class CameraOccluder : MonoBehaviour
    {
        static readonly List<CameraOccluder> active = new();
        public static IReadOnlyList<CameraOccluder> Active => active;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Materiais originais e as cópias translúcidas (mesma ordem).")]
        [SerializeField] Material[] originals = Array.Empty<Material>();
        [SerializeField] Material[] faded = Array.Empty<Material>();
        [Tooltip("Partículas ou peças que se mexem: a caixa é recalculada enquanto está perto da câmera.")]
        [SerializeField] bool dynamicBounds;

        Renderer[] renderers = Array.Empty<Renderer>();
        Material[][] normalSlots, fadedSlots;
        Color[][] baseColors;
        MaterialPropertyBlock block;
        Bounds bounds;
        float alpha = 1f;
        bool swapped;

        public Bounds Bounds
        {
            get
            {
                if (dynamicBounds) RecalculateBounds();
                return bounds;
            }
        }

        public void Configure(Material[] originalMaterials, Material[] fadedMaterials, bool moving)
        {
            originals = originalMaterials;
            faded = fadedMaterials;
            dynamicBounds = moving;
        }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            normalSlots = new Material[renderers.Length][];
            fadedSlots = new Material[renderers.Length][];
            baseColors = new Color[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                var slots = renderers[r].sharedMaterials;
                normalSlots[r] = slots;
                fadedSlots[r] = new Material[slots.Length];
                baseColors[r] = new Color[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    int k = Array.IndexOf(originals, slots[i]);
                    var m = k >= 0 && k < faded.Length && faded[k] ? faded[k] : slots[i];
                    fadedSlots[r][i] = m;
                    baseColors[r][i] = m && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                }
            }
            RecalculateBounds();
        }

        void OnEnable() => active.Add(this);

        void OnDisable()
        {
            active.Remove(this);
            alpha = 1f;
            Restore();
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>Aproxima a opacidade de <paramref name="target"/> (1 = normal) em até <paramref name="step"/>.</summary>
        public void FadeTowards(float target, float step)
        {
            if (Mathf.Approximately(alpha, target) && (swapped || target >= 1f)) return;
            alpha = Mathf.MoveTowards(alpha, target, step);
            if (alpha >= 1f)
            {
                Restore();
                return;
            }
            if (!swapped)
            {
                for (int r = 0; r < renderers.Length; r++) if (renderers[r]) renderers[r].sharedMaterials = fadedSlots[r];
                swapped = true;
            }
            block ??= new MaterialPropertyBlock();
            for (int r = 0; r < renderers.Length; r++)
            {
                if (!renderers[r]) continue;
                for (int i = 0; i < fadedSlots[r].Length; i++)
                {
                    var c = baseColors[r][i];
                    c.a *= alpha;
                    block.Clear();
                    block.SetColor(BaseColorId, c);
                    renderers[r].SetPropertyBlock(block, i);
                }
            }
        }

        void Restore()
        {
            if (!swapped) return;
            swapped = false;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (!renderers[r]) continue;
                renderers[r].sharedMaterials = normalSlots[r];
                for (int i = 0; i < normalSlots[r].Length; i++) renderers[r].SetPropertyBlock(null, i);
            }
        }

        void RecalculateBounds()
        {
            bool first = true;
            foreach (var r in renderers)
            {
                if (!r || r is ParticleSystemRenderer { enabled: false }) continue;
                if (first) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                first = false;
            }
            if (first) bounds = new Bounds(transform.position, Vector3.zero);
        }
    }
}
