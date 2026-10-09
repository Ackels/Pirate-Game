using Pirates.Core;
using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Chuva (M11, clima): gotas finas caindo numa caixa em cima do que a câmera olha, inclinadas só pelo vento (<see cref="IWind"/>),
    /// tanto mais quanto mais chove (<see cref="IWeather.Rain"/>; a tempestade engrossa). Onde as gotas batem: na água um anel que se abre
    /// e um pinguinho que pula; no chão, no convés e nos telhados só o pinguinho. Os respingos não vêm de colisão das gotas (caro com
    /// ~1300 no ar): são sorteados em pontos da tela, com um raio da câmera até o mar (o que ele achar antes, chão ou convés, recebe o
    /// pinguinho) e a altura da onda pelo <see cref="IOceanSampler"/>. Três ParticleSystems em espaço de mundo, montados no Awake, com
    /// limite de partículas; a caixa e a inclinação só mudam quando o vento ou a câmera mudam o bastante. Mais apagada de noite.
    /// </summary>
    public class RainEffect : MonoBehaviour
    {
        [SerializeField] Material material;
        [Tooltip("Anel na água (textura de anel, alfa).")]
        [SerializeField] Material ringMaterial;
        [Tooltip("Pinguinho que pula onde a gota bate (alfa).")]
        [SerializeField] Material splashMaterial;

        [Header("Gotas")]
        [Tooltip("Gotas por segundo com chuva cheia e a mais com a tempestade cheia.")]
        [SerializeField] float rainRate = 1300f, stormExtra = 1000f;
        [SerializeField, Range(100, 4000)] int maxDrops = 2800;
        [Tooltip("Velocidade da queda (m/s) e quanto o vento forte empurra de lado (m/s).")]
        [SerializeField] float fallSpeed = 20f, windPush = 6f;
        [Tooltip("Altura de onde as gotas caem acima do ponto que a câmera olha (m).")]
        [SerializeField] float dropHeight = 16f;
        [Tooltip("Espessura das gotas (m) e comprimento do risco por m/s de queda.")]
        [SerializeField] Vector2 dropWidth = new(0.05f, 0.08f);
        [SerializeField] float streak = 0.08f;
        [Tooltip("Cor das gotas (alfa = força); de noite cai para a metade.")]
        [SerializeField] Color color = new(0.82f, 0.88f, 0.97f, 0.5f);

        [Header("Respingos")]
        [Tooltip("Respingos por segundo com chuva cheia e a mais com a tempestade cheia (pontos sorteados na tela).")]
        [SerializeField] float splashRate = 380f, stormSplashExtra = 240f;
        [SerializeField, Range(50, 1000)] int maxRings = 400, maxSplashes = 300;
        [Tooltip("Respingo só até esta distância da câmera (m): mais longe ficaria menor que um pixel.")]
        [SerializeField] float splashDistance = 80f;
        [Tooltip("Tamanho do anel ao nascer e ao sumir (m) e quanto ele dura (s).")]
        [SerializeField] Vector2 ringSize = new(0.3f, 1.4f);
        [SerializeField] Vector2 ringLife = new(0.5f, 0.75f);
        [SerializeField] Color ringColor = new(0.85f, 0.92f, 1f, 0.55f);
        [SerializeField] Color splashColor = new(0.85f, 0.92f, 1f, 0.6f);

        ParticleSystem drops, rings, splashes;
        ParticleSystem.EmissionModule emission;
        Camera cam;
        IOceanSampler ocean;
        Vector3 shownWind = new(999f, 0f, 0f);
        float shownReach = -1f, shownNight = -1f, splashDebt, alpha = 1f;

        public void Configure(Material dropMaterial, Material ring, Material splash)
        {
            material = dropMaterial;
            ringMaterial = ring;
            splashMaterial = splash;
        }

        void Awake()
        {
            drops = NewSystem("GotasDeChuva", material, maxDrops);
            var main = drops.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(fallSpeed * 0.9f, fallSpeed * 1.1f);
            main.startLifetime = dropHeight / fallSpeed * 1.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(dropWidth.x, dropWidth.y);
            main.startColor = color;
            emission = drops.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var shape = drops.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 40f, 0.5f);
            // Entra suave no alto da caixa (sem gota "acendendo" no ar).
            Fade(drops, 0.12f, 1f);
            var r = drops.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = streak;
            r.lengthScale = 2f;

            rings = NewSystem("Aneis", ringMaterial, maxRings);
            Fade(rings, 0.05f, 0.35f);
            var size = rings.sizeOverLifetime;
            size.enabled = true;
            // Abre rápido e desacelera, como o anel de verdade.
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, ringSize.x / ringSize.y, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
            var rr = rings.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            rr.sortingFudge = 30f;

            splashes = NewSystem("Respingos", splashMaterial, maxSplashes);
            var sm = splashes.main;
            sm.gravityModifier = 1.2f;
            Fade(splashes, 0.05f, 0.5f);
            var sr = splashes.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.05f;
            sr.lengthScale = 1.5f;
        }

        // Objeto desligado e religado (troca de cena, sandbox): o ParticleSystem para ao desligar e não volta sozinho (sem playOnAwake).
        void OnEnable()
        {
            if (drops && !drops.isPlaying) drops.Play();
            if (rings && !rings.isPlaying) rings.Play();
            if (splashes && !splashes.isPlaying) splashes.Play();
        }

        ParticleSystem NewSystem(string name, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = max;
            var emissionModule = system.emission;
            emissionModule.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            system.Play();
            return system;
        }

        static void Fade(ParticleSystem system, float fadeIn, float fadeOut)
        {
            var col = system.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        void LateUpdate()
        {
            if (!cam) cam = Camera.main;
            if (!cam || !Services.TryGet(out IWeather weather)) return;
            float rain = weather.Rain;
            emission.rateOverTime = rain <= 0.01f ? 0f : rainRate * rain + stormExtra * weather.Storm;
            if (rain <= 0.01f)
            {
                splashDebt = 0f;
                if (drops.particleCount == 0) return;
            }

            // A caixa fica em cima do ponto olhado; de perto (a pé) fica menor, para a chuva não ficar rala.
            var focus = ViewFocus.Of(cam, out float reach);
            var t = drops.transform;
            Vector3 wind = Services.TryGet(out IWind w) ? w.Direction * (w.Strength * windPush) : Vector3.zero;
            var fall = (Vector3.down * fallSpeed + wind).normalized;
            // A caixa nasce vento acima, para as gotas inclinadas caírem em cima do ponto.
            t.position = focus - fall * dropHeight;
            if ((wind - shownWind).sqrMagnitude > 0.04f)
            {
                shownWind = wind;
                // A frente (+Z) da caixa = a direção da queda: as gotas saem por ela.
                t.rotation = Quaternion.LookRotation(fall, Vector3.forward);
            }
            if (Mathf.Abs(reach - shownReach) > 2f)
            {
                shownReach = reach;
                var shape = drops.shape;
                float side = Mathf.Clamp(reach * 1.3f, 18f, 75f);
                shape.scale = new Vector3(side, side, 0.5f);
            }
            float night = Services.TryGet(out ITimeOfDay time) ? time.Night : 0f;
            if (Mathf.Abs(night - shownNight) > 0.02f)
            {
                shownNight = night;
                alpha = Mathf.Lerp(1f, 0.5f, night);
                var main = drops.main;
                main.startColor = new Color(color.r, color.g, color.b, color.a * alpha);
            }

            if (rain > 0.01f) Splash(rain, weather.Storm);
        }

        // ---------- Respingos ----------

        void Splash(float rain, float storm)
        {
            if (ocean == null) Services.TryGet(out ocean);
            splashDebt += (splashRate * rain + stormSplashExtra * storm) * Time.deltaTime;
            // Jogo parado (menu) ou quadro muito longo: não despeja tudo de uma vez.
            splashDebt = Mathf.Min(splashDebt, 40f);
            float sea = ocean != null ? ocean.SeaLevel : 0f;
            while (splashDebt >= 1f)
            {
                splashDebt -= 1f;
                // Ponto sorteado na tela; o raio da câmera até o mar diz onde a gota bateu.
                var ray = cam.ViewportPointToRay(new Vector3(Random.value, Random.value, 0f));
                if (ray.direction.y > -0.03f) continue;
                float toSea = (sea - ray.origin.y) / ray.direction.y;
                if (toSea > splashDistance || toSea < 0f) continue;
                if (Physics.Raycast(ray, out var hit, toSea + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.normal.y > 0.5f && hit.point.y > sea - 0.2f)
                {
                    // Chão, convés ou telhado: só o pinguinho.
                    EmitSplash(hit.point + Vector3.up * 0.03f, 0.75f);
                    continue;
                }
                var p = ray.origin + ray.direction * toSea;
                if (ocean != null) p.y = ocean.GetHeight(p);
                EmitRing(p + Vector3.up * 0.06f);
                if (Random.value < 0.8f) EmitSplash(p + Vector3.up * 0.05f, 1f);
            }
        }

        void EmitRing(Vector3 at)
        {
            var c = ringColor;
            c.a *= alpha;
            rings.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                velocity = Vector3.zero,
                startSize = ringSize.y * Random.Range(0.75f, 1.15f),
                startLifetime = Random.Range(ringLife.x, ringLife.y),
                startColor = c,
                rotation = Random.value * 360f,
                applyShapeToPosition = false,
            }, 1);
        }

        void EmitSplash(Vector3 at, float strength)
        {
            var c = splashColor;
            c.a *= alpha * strength;
            var side = Random.insideUnitCircle * 0.5f;
            splashes.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                velocity = new Vector3(side.x, Random.Range(1.6f, 2.8f) * strength, side.y),
                startSize = Random.Range(0.07f, 0.12f),
                startLifetime = Random.Range(0.25f, 0.35f),
                startColor = c,
                applyShapeToPosition = false,
            }, 1);
        }
    }
}
