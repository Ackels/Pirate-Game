using System.Linq;
using Pirates.Audio;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Som ambiente da Ilha Amaldiçoada (pacote H, passo 1, ajuste de 06/11, pedido do Jhonata: "som ambiente próprio da ilha"). Sem pássaros:
    /// vento uivante em loop (feito aqui) espalhado pela ilha, corvos grasnando nas árvores secas (feitos aqui: não há gravação no projeto),
    /// gemidos ao longe (o gemido das almas das catacumbas, mais grave e baixo), rangido de árvore seca (a gravação do navio, mais grave), o
    /// crepitar nos braseiros verdes e as ondas na praia. Tudo com <see cref="AmbientEmitter"/>: longe, fica mudo e não gasta voz.
    /// Eu não ouço: os sons foram feitos pelos números (RMS, faixa de frequência que a caixinha do notebook toca).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string CursedWindPath = GeneratedAudioFolder + "/IlhaAmaldicoada_Vento_Gerado.wav";
        const string CrowPath = GeneratedAudioFolder + "/Corvo_{0}_Gerado.wav";

        static void CursedAmbience(Transform ambience, GameSounds sounds)
        {
            var island = GameObject.Find("Ilha_Amaldicoada")?.transform;
            if (!island) return;
            const SoundChannel amb = SoundChannel.Ambient;
            var wind = Cue("IlhaVento", new[] { CursedWindClip() }, amb, new Vector2(0.75f, 0.75f), new Vector2(1f, 1f), true, 25f, 120f);
            var crows = Cue("Corvo", new[] { CrowClip(1, 2, 0.55f), CrowClip(2, 3, 0.48f), CrowClip(3, 1, 0.6f) }, amb, new Vector2(0.5f, 0.7f), new Vector2(0.9f, 1.08f),
                true, 8f, 75f);
            var moan = Cue("GemidoLonge", new[] { GhostMoanClip() }, amb, new Vector2(0.3f, 0.42f), new Vector2(0.5f, 0.66f), true, 15f, 95f);
            var creak = Cue("RangidoArvore", Clips("Ambiente", "tree_creak.wav"), amb, new Vector2(0.45f, 0.6f), new Vector2(0.55f, 0.75f), true, 5f, 40f);

            var root = new GameObject("Sons_IlhaAmaldicoada").transform;
            root.SetParent(ambience, false);
            GameObject Emitter(string name, Vector3 at)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = at;
                return go;
            }

            // Vento: no meio e em três pontos em volta (o loop começa num ponto sorteado em cada um).
            var windSpots = new[] { Vector2.zero, new Vector2(-40f, 20f), new Vector2(40f, 18f), new Vector2(0f, -42f) };
            for (int k = 0; k < windSpots.Length; k++)
                Emitter($"Vento_{k}", SkullGround(island, windSpots[k]) + Vector3.up * 8f).AddComponent<AmbientEmitter>().Configure(wind, true, Vector2.one, 1f);

            // Corvos e rangidos nas árvores secas (umas espalhadas, em ordem fixa).
            var trees = island.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("ArvoreSeca_") || t.name.StartsWith("Arvore_"))
                .OrderBy(t => t.name).ToArray();
            for (int k = 0; k < 6 && trees.Length > 0; k++)
            {
                var tree = trees[(k * 13) % trees.Length];
                Emitter($"Corvo_{k}", tree.position + Vector3.up * 5f).AddComponent<AmbientEmitter>().Configure(crows, false, new Vector2(14f, 34f), 1f);
            }
            for (int k = 0; k < 4 && trees.Length > 0; k++)
            {
                var tree = trees[(k * 17 + 5) % trees.Length];
                Emitter($"Rangido_{k}", tree.position + Vector3.up * 2f).AddComponent<AmbientEmitter>().Configure(creak, false, new Vector2(12f, 28f), 1f);
            }

            // Gemidos ao longe: do alto e do cemitério.
            Emitter("Gemido_Alto", SkullGround(island, CursedSummit) + Vector3.up * 3f).AddComponent<AmbientEmitter>().Configure(moan, false, new Vector2(18f, 40f), 1f);
            Emitter("Gemido_Cemiterio", SkullGround(island, CursedCemetery) + Vector3.up * 3f).AddComponent<AmbientEmitter>()
                .Configure(moan, false, new Vector2(22f, 48f), 1f);

            // Braseiros verdes crepitam (o mesmo fogo das fogueiras, mais baixo).
            foreach (var flame in island.GetComponentsInChildren<ParticleSystem>().Where(p => p.name == "ChamaVerde"))
                flame.gameObject.AddComponent<AmbientEmitter>().Configure(sounds.fire, true, Vector2.one, 0.6f);
        }

        /// <summary>
        /// Vento uivante (20 s em loop, feito aqui): dois uivos de ruído num passa-faixa estreito que desliza devagar (380–760 Hz e uma quinta
        /// acima, mais baixo), o sopro largo por baixo e um ronco grave. Ondulações com período que divide os 20 s; fim misturado com o começo.
        /// </summary>
        static AudioClip CursedWindClip() => GeneratedClip(CursedWindPath, "ilha-vento-v1", 0.1f, () =>
        {
            const int rate = AudioRate, seconds = 20;
            int n = rate * seconds, fade = rate * 2;
            var raw = new float[n + fade];
            var rng = new System.Random(6106);
            float l1 = 0f, b1 = 0f, l2 = 0f, b2 = 0f, l3 = 0f, b3 = 0f, lp = 0f;
            float lpA = Mathf.Exp(-2f * Mathf.PI * 120f / rate);
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Uivo: o centro sobe e desce em ondas de 10 e 5 s (dividem os 20 s).
                float howl = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 10f) * Mathf.Sin(2f * Mathf.PI * t / 20f + 0.7f);
                float c1 = 380f + 380f * howl + 30f * Mathf.Sin(2f * Mathf.PI * t / 2.5f);
                Band(ref l1, ref b1, white, c1, 0.06f);
                Band(ref l2, ref b2, white, c1 * 1.5f, 0.08f);
                Band(ref l3, ref b3, white, 700f, 0.9f);
                lp = lpA * lp + (1f - lpA) * white;
                float gust = 0.55f + 0.45f * howl;
                raw[i] = (b1 * 1.6f * gust + b2 * 0.6f * gust + b3 * 0.35f * (0.6f + 0.4f * gust) + lp * 3f);
            }
            return SeamlessLoop(raw, n, fade);

            static void Band(ref float low, ref float band, float input, float center, float damping)
            {
                float f = 2f * Mathf.Sin(Mathf.PI * center / AudioRate);
                low += f * band;
                float high = input - low - damping * band;
                band += f * high;
            }
        });

        /// <summary>
        /// Corvo (feito aqui): <paramref name="caws"/> grasnados roucos de ~0,24 s, cada um um tom dente de serra caindo de ~620 para ~430 Hz
        /// (× <paramref name="pitch"/> / 0,55) com aspereza (modulação a ~70 Hz e ruído), passando por um "bico" (passa-faixa em ~1,3 kHz).
        /// </summary>
        static AudioClip CrowClip(int variant, int caws, float pitch) => GeneratedClip(string.Format(CrowPath, variant), "corvo-v1", 0.14f, () =>
        {
            const int rate = AudioRate;
            float gap = 0.36f, length = 0.24f;
            int n = (int)((caws * gap + 0.3f) * rate);
            var s = new float[n];
            var rng = new System.Random(4400 + variant);
            float scale = pitch / 0.55f;
            for (int k = 0; k < caws; k++)
            {
                int start = (int)((0.05f + k * gap + (float)rng.NextDouble() * 0.05f) * rate), len = (int)(length * rate);
                double phase = 0.0;
                float low = 0f, band = 0f, amp = k == caws - 1 && caws > 1 ? 0.8f : 1f;
                for (int j = 0; j < len && start + j < n; j++)
                {
                    float tt = (float)j / len;
                    float f = Mathf.Lerp(620f, 430f, tt) * scale;
                    phase += f / rate;
                    float saw = 0f;
                    for (int h = 1; h <= 9; h++) saw += Mathf.Sin(2f * Mathf.PI * (float)(phase * h)) / h;
                    float rasp = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 70f * j / rate);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.45f;
                    float input = saw * rasp + noise;
                    float fc = 2f * Mathf.Sin(Mathf.PI * 1300f / rate);
                    low += fc * band;
                    float high = input - low - 0.7f * band;
                    band += fc * high;
                    float env = Mathf.Clamp01(tt / 0.06f) * Mathf.Clamp01((1f - tt) / 0.35f);
                    s[start + j] += (band * 0.8f + input * 0.25f) * env * amp;
                }
            }
            return s;
        }, loop: false);
    }
}
