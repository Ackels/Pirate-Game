using System;
using Pirates.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Ambient
{
    /// <summary>
    /// Clima (M11, passo 2): céu limpo, nublado, chuva e tempestade mudam devagar com a hora do jogo (ruído pela hora do
    /// <see cref="ITimeOfDay"/>, como o vento: o save do relógio já traz o tempo). Registra <see cref="IWeather"/>; quem escurece o dia é o
    /// <see cref="DayNightCycle"/>, quem chove é o <see cref="RainEffect"/>, o vento sobe sozinho (<see cref="WindSystem"/>). Na tempestade
    /// caem raios: clarão (uma luz direcional ligada por um instante), o risco do raio ao longe e o evento para o trovão.
    /// </summary>
    public class WeatherSystem : MonoBehaviour, IWeather
    {
        [SerializeField] WeatherSettings settings;
        [Tooltip("Luz do clarão (direcional, desligada; liga só no raio).")]
        [SerializeField] Light flash;
        [Tooltip("Risco do raio (LineRenderer desligado).")]
        [SerializeField] LineRenderer bolt;

        WeatherKind? fixedKind;
        float nextStrike, strikeAge = float.PositiveInfinity;
        readonly Vector3[] boltPoints = new Vector3[12];
        Camera cam;

        public float Overcast { get; private set; }
        public float Rain { get; private set; }
        public float Storm { get; private set; }
        public bool Overridden => fixedKind.HasValue;
        public WeatherSettings Settings => settings;
        public event Action<Vector3, float> LightningStruck;

        public string Label => Storm > 0.5f ? "tempestade" : Rain > 0.4f ? "chuva" : Overcast > 0.35f ? "nublado" : "céu limpo";

        public void Configure(WeatherSettings weatherSettings, Light flashLight, LineRenderer boltLine)
        {
            settings = weatherSettings;
            flash = flashLight;
            bolt = boltLine;
        }

        void OnEnable()
        {
            Services.Register<IWeather>(this);
            // Começa já no tempo da hora (sem 20 s de transição ao carregar o jogo).
            Targets(out float o, out float r, out float s);
            Overcast = o;
            Rain = r;
            Storm = s;
        }

        void OnDisable() => Services.Unregister<IWeather>(this);

        public void SetOverride(WeatherKind? kind) => fixedKind = kind;

        public void StrikeNow()
        {
            if (settings) Strike();
        }

        void Update()
        {
            if (!settings) return;
            Targets(out float overcast, out float rain, out float storm);
            float step = Time.deltaTime / settings.transitionSeconds;
            Overcast = Mathf.MoveTowards(Overcast, overcast, step);
            Rain = Mathf.MoveTowards(Rain, rain, step);
            Storm = Mathf.MoveTowards(Storm, storm, step);
            UpdateLightning();
        }

        /// <summary>Tempo pedido agora: o fixado no sandbox ou o do ruído pela hora corrida.</summary>
        void Targets(out float overcast, out float rain, out float storm)
        {
            overcast = rain = storm = 0f;
            if (!settings) return;
            if (fixedKind.HasValue)
            {
                var kind = fixedKind.Value;
                overcast = kind switch { WeatherKind.Cloudy => 0.55f, WeatherKind.Rain => 0.85f, WeatherKind.Storm => 1f, _ => 0f };
                rain = kind == WeatherKind.Rain ? 0.8f : kind == WeatherKind.Storm ? 1f : 0f;
                storm = kind == WeatherKind.Storm ? 1f : 0f;
                return;
            }
            bool hasClock = Services.TryGet(out ITimeOfDay time);
            float hours = hasClock ? (time.Day - 1) * 24f + time.Hour : Time.time / 60f;
            // Começo do jogo novo (dia 1, 8h) sempre limpo; o ruído vai entrando em 2 horas de jogo depois.
            float start = hasClock && time.Day == 1 ? Mathf.InverseLerp(8f + settings.clearStartHours, 10f + settings.clearStartHours, time.Hour) : 1f;
            float n =Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(hours / settings.changeHours, 53.9f))) * start;
            float b = settings.blend;
            float cloudy = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.cloudyFrom - b, settings.cloudyFrom + b, n));
            rain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.rainFrom - b, settings.rainFrom + b, n));
            storm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.stormFrom - b, settings.stormFrom + b, n));
            overcast = Mathf.Max(cloudy * 0.55f, Mathf.Max(rain * 0.85f, storm));
            rain = Mathf.Max(rain * 0.8f, storm);
        }

        // ---------- Raios ----------

        void UpdateLightning()
        {
            float dt = Time.deltaTime;
            if (strikeAge < 1f)
            {
                strikeAge += dt;
                // Dois clarões: um curto, um respiro e um maior (o raio "pisca").
                bool lit = strikeAge < 0.07f || (strikeAge > 0.13f && strikeAge < 0.26f);
                // Debaixo da terra o raio não aparece (o trovão continua, abafado).
                if (flash) flash.enabled = lit && !CoveredArea.Inside;
                if (bolt) bolt.enabled = strikeAge < 0.28f && !CoveredArea.Inside;
            }
            if (Storm < 0.5f || !settings)
            {
                if (nextStrike > 0f) nextStrike = Mathf.Max(nextStrike, Time.time + 2f);
                return;
            }
            if (Time.time < nextStrike) return;
            float gap = Random.Range(settings.lightningInterval.x, settings.lightningInterval.y) / Mathf.Max(0.5f, Storm);
            nextStrike = Time.time + gap;
            Strike();
        }

        void Strike()
        {
            if (!cam) cam = Camera.main;
            if (!cam) return;
            var focus = ViewFocus.Of(cam, out _);
            // À frente da câmera, para um lado (o raio aparece na tela).
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            float distance = Random.Range(settings.boltDistance.x, settings.boltDistance.y);
            // Para um dos lados do ponto olhado (a câmera olha para baixo: à frente, o raio cairia acima da tela).
            float side = Random.value < 0.5f ? -1f : 1f;
            var ground = focus + Quaternion.Euler(0f, side * Random.Range(20f, 55f), 0f) * forward * distance;
            ground.y = 0f;

            if (bolt)
            {
                // Zigue-zague do céu até a água, desviando mais no meio.
                // Até ~45 m de altura: mais alto passa por cima da câmera e o raio vira um risco atravessando a tela.
                var top = ground + new Vector3(Random.Range(-8f, 8f), 45f, Random.Range(-8f, 8f));
                for (int i = 0; i < boltPoints.Length; i++)
                {
                    float k = i / (float)(boltPoints.Length - 1);
                    var p = Vector3.Lerp(top, ground, k);
                    if (i > 0 && i < boltPoints.Length - 1) p += new Vector3(Random.Range(-3.5f, 3.5f), Random.Range(-1.5f, 1.5f), Random.Range(-3.5f, 3.5f));
                    boltPoints[i] = p;
                }
                bolt.positionCount = boltPoints.Length;
                bolt.SetPositions(boltPoints);
                float width = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(settings.boltDistance.x, settings.boltDistance.y, distance));
                bolt.widthMultiplier = width;
                bolt.enabled = !CoveredArea.Inside;
            }
            if (flash)
            {
                flash.color = settings.flashColor;
                flash.intensity = settings.flashIntensity;
                flash.transform.rotation = Quaternion.LookRotation((focus - ground).normalized * 0.4f + Vector3.down);
            }
            strikeAge = 0f;
            LightningStruck?.Invoke(ground, distance);
        }
    }
}
