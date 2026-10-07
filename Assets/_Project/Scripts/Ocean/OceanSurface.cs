using Pirates.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.Ocean
{
    /// <summary>
    /// Malha de oceano que acompanha um alvo (a câmera). Com o shader "Pirates/Ocean" no material, as ondas são feitas
    /// na GPU (este script só manda as ondas e anda com a malha); com outro shader, a malha é deformada aqui na CPU.
    /// A física sempre pergunta a altura por <see cref="GetHeight"/> (mesma conta, na CPU).
    /// Costa (só com o shader do oceano): manda como globais o mapa da cena (altura do chão e distância até o litoral, feito
    /// pelo gerador), o ruído da espuma e as cores; o shader pinta turquesa, raso e espuma perto das ilhas. No mar aberto,
    /// crista da onda mais clara e espuma no alto das cristas (pela altura contra a soma das amplitudes).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OceanSurface : MonoBehaviour, IOceanSampler
    {
        [SerializeField] WaveSettings waves;
        [SerializeField] Transform followTarget;
        [SerializeField, Range(16, 254)] int resolution = 128;
        [SerializeField] float size = 500f;

        [Header("Costa (mapa feito pelo gerador)")]
        [SerializeField] Texture2D shoreMap;
        [Tooltip("Origem X, origem Z, largura e comprimento (m) do mapa no mundo.")]
        [SerializeField] Vector4 shoreRect;
        [Tooltip("Altura do chão no R: mínima e faixa (m). Distância até o litoral no G: máxima (m).")]
        [SerializeField] Vector3 shoreEncoding = new(-6f, 8f, 64f);
        [SerializeField] Texture2D foamNoise;
        [Header("Costa (visual)")]
        [Tooltip("Turquesa perto das ilhas (alfa = força).")]
        [SerializeField] Color shallowColor = new(0.12f, 0.56f, 0.55f, 0.85f);
        [Tooltip("Raso de verdade, perto da areia (alfa = força).")]
        [SerializeField] Color shoalColor = new(0.45f, 0.82f, 0.75f, 0.9f);
        [Tooltip("Espuma (alfa = força).")]
        [SerializeField] Color foamColor = new(0.95f, 0.98f, 1f, 1f);
        [Tooltip("Até quantos metros do litoral a água fica turquesa.")]
        [SerializeField] float shallowWidth = 28f;
        [Tooltip("Profundidade (m) até onde há espuma na linha d'água.")]
        [SerializeField] float foamDepth = 0.55f;
        [Tooltip("Tamanho das manchas de espuma (escala do ruído por metro).")]
        [SerializeField] float foamNoiseScale = 0.11f;
        [Tooltip("Até quantos metros do litoral chegam as faixas de espuma.")]
        [SerializeField] float foamWaveReach = 12f;
        [Tooltip("Metros entre uma faixa de espuma e outra.")]
        [SerializeField] float foamWaveSpacing = 4.5f;
        [Tooltip("Faixas por segundo chegando na praia.")]
        [SerializeField] float foamWaveSpeed = 0.22f;
        [Tooltip("Espessura das faixas (0–1).")]
        [SerializeField, Range(0f, 1f)] float foamWaveWidth = 0.4f;
        [Header("Mar aberto (visual)")]
        [Tooltip("Cor do alto da onda (alfa = força).")]
        [SerializeField] Color crestColor = new(0.16f, 0.5f, 0.52f, 0.55f);
        [Tooltip("A partir de que fração da altura máxima a crista começa a clarear.")]
        [SerializeField, Range(0f, 1f)] float crestTintStart = 0.25f;
        [Tooltip("A partir de que fração da altura máxima aparece espuma na crista.")]
        [SerializeField, Range(0f, 1f)] float whitecapStart = 0.58f;
        [Tooltip("Quanto de espuma nas cristas (0 = nenhuma).")]
        [SerializeField, Range(0f, 1.5f)] float whitecapAmount = 0.85f;
        [Tooltip("Espuma das cristas × isto com calmaria e com vento forte (M11: segue a força do IWind; sem vento, 1).")]
        [SerializeField, Range(0f, 2f)] float windFoamCalm = 0.55f, windFoamStrong = 1.35f;
        [Tooltip("Com vento forte a espuma começa mais embaixo na onda (fração da altura).")]
        [SerializeField, Range(0f, 0.3f)] float windFoamLower = 0.1f;

        float windStrength = -1f, nextWindCheck;

        Mesh mesh;
        Vector3[] baseVertices;
        Vector3[] vertices;
        Vector3[] normals;
        float spacing;

        // Senos/cossenos por coluna (x) e por linha (z) de cada onda: a fase é kx·x + kz·z − ωt, então
        // sen(fase) = sen(a)·cos(b) + cos(a)·sen(b) sai de duas tabelas de (resolution + 1) valores, sem trigonometria por vértice.
        float[] sinX, cosX, sinZ, cosZ;

        const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        // Ondas na GPU (Ocean.shader / OceanWaves.hlsl): até 8, como (kx, kz, amplitude, fase).
        const int MaxGpuWaves = 8;
        static readonly int GpuMarkerId = Shader.PropertyToID("_OceanGpuWaves");
        static readonly int WavesId = Shader.PropertyToID("_OceanWaves");
        static readonly int WaveCountId = Shader.PropertyToID("_OceanWaveCount");
        static readonly int SeaLevelId = Shader.PropertyToID("_OceanSeaLevel");
        static readonly int SpacingId = Shader.PropertyToID("_OceanSpacing");
        readonly Vector4[] gpuWaves = new Vector4[MaxGpuWaves];
        bool onGpu;

        static readonly int ShoreMapId = Shader.PropertyToID("_OceanShoreMap");
        static readonly int FoamNoiseId = Shader.PropertyToID("_OceanFoamNoise");
        static readonly int ShoreRectId = Shader.PropertyToID("_OceanShoreRect");
        static readonly int ShoreRangeId = Shader.PropertyToID("_OceanShoreRange");
        static readonly int ShallowColorId = Shader.PropertyToID("_OceanShallowColor");
        static readonly int ShoalColorId = Shader.PropertyToID("_OceanShoalColor");
        static readonly int FoamColorId = Shader.PropertyToID("_OceanFoamColor");
        static readonly int ShoreParamsId = Shader.PropertyToID("_OceanShoreParams");
        static readonly int FoamWavesId = Shader.PropertyToID("_OceanFoamWaves");
        static readonly int CrestId = Shader.PropertyToID("_OceanCrest");
        static readonly int CrestColorId = Shader.PropertyToID("_OceanCrestColor");

        public float SeaLevel => waves ? waves.seaLevel : 0f;

        public void Configure(WaveSettings waveSettings, Transform follow)
        {
            waves = waveSettings;
            followTarget = follow;
        }

        /// <summary>Mapa da costa da cena (gerador): R = altura do chão, G = distância até o litoral; ruído da espuma.</summary>
        public void ConfigureShore(Texture2D map, Vector4 rect, Vector3 encoding, Texture2D noise)
        {
            shoreMap = map;
            shoreRect = rect;
            shoreEncoding = encoding;
            foamNoise = noise;
        }

        public float GetHeight(Vector3 worldPosition) =>
            waves ? waves.Evaluate(worldPosition.x, worldPosition.z, Time.time) : 0f;

        void OnEnable()
        {
            Services.Register<IOceanSampler>(this);
            SendShore();
        }

        void OnValidate()
        {
            if (isActiveAndEnabled) SendShore();
        }

        /// <summary>Costa para o shader (só muda com o mapa ou ajuste no Inspector). Sem mapa, o shader pula a costa.</summary>
        void SendShore()
        {
            bool hasMap = shoreMap && foamNoise && shoreRect.z > 0f && shoreRect.w > 0f;
            Shader.SetGlobalTexture(ShoreMapId, hasMap ? shoreMap : Texture2D.blackTexture);
            Shader.SetGlobalTexture(FoamNoiseId, hasMap ? foamNoise : Texture2D.whiteTexture);
            Shader.SetGlobalVector(ShoreRectId, hasMap ? new Vector4(shoreRect.x, shoreRect.y, 1f / shoreRect.z, 1f / shoreRect.w) : Vector4.zero);
            Shader.SetGlobalVector(ShoreRangeId, new Vector4(shoreEncoding.x, shoreEncoding.y, shoreEncoding.z, waves ? waves.seaLevel : 0f));
            Shader.SetGlobalVector(ShallowColorId, shallowColor.linear.WithAlpha(shallowColor.a));
            Shader.SetGlobalVector(ShoalColorId, shoalColor.linear.WithAlpha(shoalColor.a));
            Shader.SetGlobalVector(FoamColorId, foamColor.linear.WithAlpha(foamColor.a));
            Shader.SetGlobalVector(ShoreParamsId, new Vector4(Mathf.Max(0.1f, shallowWidth), Mathf.Max(0.01f, foamDepth), foamNoiseScale, Mathf.Max(0.1f, foamWaveReach)));
            Shader.SetGlobalVector(FoamWavesId, new Vector4(Mathf.Max(0.1f, foamWaveSpacing), foamWaveSpeed, foamWaveWidth, 0f));
            SendCrest();
        }

        /// <summary>Crista e espuma do mar aberto; a espuma acompanha a força do vento (sem vento na cena, como no Inspector).</summary>
        void SendCrest()
        {
            float amplitude = 0f;
            if (waves)
                foreach (var term in waves.Terms) amplitude += term.amplitude;
            float foam = whitecapAmount, start = whitecapStart;
            if (windStrength >= 0f)
            {
                foam *= Mathf.Lerp(windFoamCalm, windFoamStrong, windStrength);
                start = Mathf.Clamp01(start + Mathf.Lerp(windFoamLower * 0.5f, -windFoamLower, windStrength));
            }
            Shader.SetGlobalVector(CrestId, new Vector4(Mathf.Max(0.01f, amplitude), crestTintStart, start, foam));
            Shader.SetGlobalVector(CrestColorId, crestColor.linear.WithAlpha(crestColor.a));
        }

        /// <summary>A força do vento mudou o bastante: manda a espuma de novo (confere duas vezes por segundo).</summary>
        void FollowWind()
        {
            if (Time.time < nextWindCheck) return;
            nextWindCheck = Time.time + 0.5f;
            float strength = Services.TryGet(out IWind wind) ? wind.Strength : -1f;
            if (Mathf.Abs(strength - windStrength) < 0.02f) return;
            windStrength = strength;
            SendCrest();
        }
        void OnDisable() => Services.Unregister<IOceanSampler>(this);

        void Awake()
        {
            if (!waves) Debug.LogError("[OceanSurface] WaveSettings não atribuído.", this);
            var material = GetComponent<MeshRenderer>().sharedMaterial;
            onGpu = material && material.HasProperty(GpuMarkerId);
            BuildMesh();
        }

        void BuildMesh()
        {
            spacing = size / resolution;
            int side = resolution + 1;
            baseVertices = new Vector3[side * side];
            var uvs = new Vector2[baseVertices.Length];
            var triangles = new int[resolution * resolution * 6];
            float half = size * 0.5f;

            for (int z = 0, i = 0; z < side; z++)
            for (int x = 0; x < side; x++, i++)
            {
                baseVertices[i] = new Vector3(x * spacing - half, 0f, z * spacing - half);
                uvs[i] = new Vector2((float)x / resolution, (float)z / resolution);
            }

            for (int z = 0, t = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int v = z * side + x;
                triangles[t++] = v;
                triangles[t++] = v + side;
                triangles[t++] = v + 1;
                triangles[t++] = v + 1;
                triangles[t++] = v + side;
                triangles[t++] = v + side + 1;
            }

            vertices = (Vector3[])baseVertices.Clone();
            normals = new Vector3[baseVertices.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh = new Mesh { name = "OceanSurface (runtime)" };
            mesh.indexFormat = IndexFormat.UInt32;
            if (!onGpu) mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 20f, size));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void LateUpdate()
        {
            if (!waves || mesh == null) return;
            FollowWind();

            // Segue o alvo em passos de "spacing" para os vértices não "nadarem".
            if (followTarget)
            {
                Vector3 p = followTarget.position;
                transform.position = new Vector3(
                    Mathf.Round(p.x / spacing) * spacing, 0f,
                    Mathf.Round(p.z / spacing) * spacing);
            }

            if (onGpu)
            {
                SendWavesToGpu(transform.position, Time.time);
                return;
            }
            UpdateHeights(transform.position, Time.time);
            UpdateNormals();
            mesh.SetVertices(vertices, 0, vertices.Length, UploadFlags);
            mesh.SetNormals(normals, 0, normals.Length, UploadFlags);
        }

        // A GPU recebe a fase de cada onda já com a posição da malha e o tempo (kx·ox + kz·oz − ω·t), somada em double e
        // reduzida a uma volta: lá só entra a posição local do vértice, sem números grandes.
        void SendWavesToGpu(Vector3 origin, float time)
        {
            var terms = waves.Terms;
            int count = Mathf.Min(terms.Length, MaxGpuWaves);
            for (int i = 0; i < count; i++)
            {
                var term = terms[i];
                double phase = (double)term.kx * origin.x + (double)term.kz * origin.z - (double)term.omega * time;
                phase %= 2.0 * System.Math.PI;
                gpuWaves[i] = new Vector4(term.kx, term.kz, term.amplitude, (float)phase);
            }
            for (int i = count; i < MaxGpuWaves; i++) gpuWaves[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(WavesId, gpuWaves);
            Shader.SetGlobalFloat(WaveCountId, count);
            Shader.SetGlobalFloat(SeaLevelId, waves.seaLevel - origin.y);
            Shader.SetGlobalFloat(SpacingId, spacing);
        }

        // Mesma altura de WaveSettings.Evaluate (a física usa aquela), só que separada em linha × coluna.
        void UpdateHeights(Vector3 origin, float time)
        {
            var terms = waves.Terms;
            int side = resolution + 1;
            int count = terms.Length * side;
            if (sinX == null || sinX.Length != count)
            {
                sinX = new float[count]; cosX = new float[count];
                sinZ = new float[count]; cosZ = new float[count];
            }

            float half = size * 0.5f;
            for (int w = 0; w < terms.Length; w++)
            {
                var term = terms[w];
                int o = w * side;
                for (int i = 0; i < side; i++)
                {
                    float x = origin.x + i * spacing - half;
                    float a = term.kx * x - term.omega * time;
                    sinX[o + i] = term.amplitude * Mathf.Sin(a);
                    cosX[o + i] = term.amplitude * Mathf.Cos(a);

                    float z = origin.z + i * spacing - half;
                    float b = term.kz * z;
                    sinZ[o + i] = Mathf.Sin(b);
                    cosZ[o + i] = Mathf.Cos(b);
                }
            }

            float baseHeight = waves.seaLevel - origin.y;
            for (int zi = 0, v = 0; zi < side; zi++)
            for (int xi = 0; xi < side; xi++, v++)
            {
                float h = baseHeight;
                for (int w = 0, o = 0; w < terms.Length; w++, o += side)
                    h += sinX[o + xi] * cosZ[o + zi] + cosX[o + xi] * sinZ[o + zi];
                vertices[v].y = h;
            }
        }

        // Normal pela inclinação da própria grade (diferença central), como o RecalculateNormals dava.
        void UpdateNormals()
        {
            int side = resolution + 1;
            for (int zi = 0, v = 0; zi < side; zi++)
            {
                int down = zi > 0 ? -side : 0, up = zi < side - 1 ? side : 0;
                float dzSpan = (up - down) / side * spacing;
                for (int xi = 0; xi < side; xi++, v++)
                {
                    int left = xi > 0 ? -1 : 0, right = xi < side - 1 ? 1 : 0;
                    float dx = (vertices[v + right].y - vertices[v + left].y) / ((right - left) * spacing);
                    float dz = (vertices[v + up].y - vertices[v + down].y) / dzSpan;
                    float inv = 1f / Mathf.Sqrt(dx * dx + dz * dz + 1f);
                    normals[v] = new Vector3(-dx * inv, inv, -dz * inv);
                }
            }
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }
    }

    static class ColorExtensions
    {
        public static Vector4 WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
