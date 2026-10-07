using Pirates.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.Ambient
{
    /// <summary>
    /// Ciclo de dia e noite (M11): a hora anda com o jogo (parado no menu, parada também) e move a luz do sol, que de noite vira a lua
    /// (mesma luz, outra cor, outra direção: as sombras continuam). Acompanham a hora: a luz ambiente, a névoa, o céu, o reflexo do céu na
    /// água e as lanternas (<see cref="NightLight"/>). O dia claro fica como a cena já era: as cores do dia são lidas da cena ao começar.
    /// Registra <see cref="ITimeOfDay"/> no <see cref="Services"/> (IA de terra, save, HUD e sandbox leem por lá).
    /// </summary>
    public class DayNightCycle : MonoBehaviour, ITimeOfDay
    {
        [SerializeField] DayNightSettings settings;
        [Tooltip("A luz do sol da cena (de noite, a lua).")]
        [SerializeField] Light sun;

        float hour;
        int day = 1;
        float appliedHour = -1f;

        // Como a cena era de dia (lido no Awake): o dia claro continua igual.
        SphericalHarmonicsL2 dayAmbient;
        Color dayFog;
        float dayReflection = 1f, daySkyExposure = 1f, dayShadowStrength = 1f;
        Material sky, sceneSky;

        public float Hour => hour;
        public int Day => day;
        public float Night { get; private set; }
        public float Speed { get; set; } = 1f;
        public DayNightSettings Settings => settings;

        public void Configure(DayNightSettings daySettings, Light sunLight)
        {
            settings = daySettings;
            sun = sunLight;
        }

        void Awake()
        {
            if (!sun) sun = RenderSettings.sun;
            dayAmbient = RenderSettings.ambientProbe;
            dayFog = RenderSettings.fogColor;
            dayReflection = RenderSettings.reflectionIntensity;
            if (sun) dayShadowStrength = sun.shadowStrength;
            // Cópia do céu: mexer na exposição do material da cena mudaria o asset (fica salvo depois do Play).
            sceneSky = RenderSettings.skybox;
            if (sceneSky)
            {
                sky = new Material(sceneSky) { name = sceneSky.name + " (ciclo)" };
                if (sky.HasProperty("_Exposure")) daySkyExposure = sky.GetFloat("_Exposure");
                RenderSettings.skybox = sky;
            }
            ResetToStart();
        }

        void OnEnable() => Services.Register<ITimeOfDay>(this);
        void OnDisable() => Services.Unregister<ITimeOfDay>(this);

        void OnDestroy()
        {
            if (sky && RenderSettings.skybox == sky) RenderSettings.skybox = sceneSky;
            if (sky) Destroy(sky);
            NightLight.Level = 0f;
        }

        public void ResetToStart() => SetTime(1, settings ? settings.startHour : 8f);

        public void SetTime(int newDay, float newHour)
        {
            day = Mathf.Max(1, newDay);
            hour = Mathf.Repeat(newHour, 24f);
            Apply();
        }

        void Update()
        {
            if (!settings) return;
            float dt = Time.deltaTime * Mathf.Max(0f, Speed);
            if (dt <= 0f) return;
            bool daytime = hour >= settings.sunrise && hour < settings.sunset;
            float dayLength = settings.sunset - settings.sunrise;
            // Horas de jogo por segundo real: o dia e a noite têm durações próprias.
            float rate = daytime ? dayLength / (settings.dayMinutes * 60f) : (24f - dayLength) / (settings.nightMinutes * 60f);
            hour += rate * dt;
            if (hour >= 24f)
            {
                hour -= 24f;
                day++;
            }
            // Mudou o bastante para aparecer (~10 s de jogo): reaplica (o sol anda ~0,04°; ninguém vê os degraus).
            if (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, appliedHour * 15f)) > 0.045f) Apply();
        }

        /// <summary>Luz do sol/lua, ambiente, névoa, céu, reflexo e lanternas para a hora atual.</summary>
        void Apply()
        {
            appliedHour = hour;
            if (!settings) return;
            float half = settings.twilightHours * 0.5f;
            // Claridade: sobe do nascer (−meia transição) até o dia claro e desce no pôr do sol.
            float daylight = Mathf.Min(Ramp(hour, settings.sunrise - half, settings.sunrise + half),
                                       1f - Ramp(hour, settings.sunset - half, settings.sunset + half));
            float night = 1f - daylight;
            float twilight = 4f * daylight * night;
            Night = night;

            PlaceSunOrMoon();

            // Luz ambiente: a do céu da cena de dia (com tom de amanhecer/anoitecer), a da noite fechada de noite.
            var ambient = dayAmbient;
            Tint(ref ambient, Color.Lerp(Color.white, settings.twilightTint, twilight));
            var nightAmbient = new SphericalHarmonicsL2();
            nightAmbient.AddAmbientLight(settings.nightAmbient);
            RenderSettings.ambientProbe = ambient * daylight + nightAmbient * night;

            var fog = Color.Lerp(dayFog, settings.nightFog, night);
            RenderSettings.fogColor = Color.Lerp(fog, settings.twilightFog, twilight * 0.55f);
            RenderSettings.reflectionIntensity = Mathf.Lerp(dayReflection, settings.nightReflection, night);
            if (sky && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(daySkyExposure, settings.nightSkyExposure, night));

            NightLight.Level = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.lanternsFrom, 0.85f, night));
        }

        /// <summary>De dia o sol anda de leste a oeste; de noite a mesma luz vira a lua. Perto do horizonte a luz some (troca escondida).</summary>
        void PlaceSunOrMoon()
        {
            if (!sun) return;
            float dayLength = settings.sunset - settings.sunrise;
            bool daytime = hour >= settings.sunrise && hour < settings.sunset;
            float t = daytime ? (hour - settings.sunrise) / dayLength : Mathf.Repeat(hour - settings.sunset, 24f) / (24f - dayLength);
            // Curva "gorda" (raiz do seno): o sol fica mais tempo baixo-mas-acima do horizonte, a hora dourada dura e só mergulha no fim.
            float elevation = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.55f) * (daytime ? settings.sunMaxElevation : settings.moonMaxElevation);
            float azimuth = (daytime ? settings.sunNoonAzimuth : settings.moonAzimuth) + (t - 0.5f) * 2f * settings.sunAzimuthSwing;
            sun.transform.rotation = Quaternion.Euler(Mathf.Max(elevation, 1f), azimuth, 0f);
            float up = Mathf.SmoothStep(0f, 1f, elevation / 10f);
            if (daytime)
            {
                sun.intensity = settings.sunIntensity * up;
                sun.color = settings.sunColor.Evaluate(Mathf.Clamp01(elevation / settings.sunMaxElevation));
                sun.shadowStrength = dayShadowStrength;
            }
            else
            {
                sun.intensity = settings.moonIntensity * up;
                sun.color = settings.moonColor;
                sun.shadowStrength = settings.moonShadowStrength;
            }
        }

        static float Ramp(float x, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

        static void Tint(ref SphericalHarmonicsL2 sh, Color tint)
        {
            for (int c = 0; c < 9; c++)
            {
                sh[0, c] *= tint.r;
                sh[1, c] *= tint.g;
                sh[2, c] *= tint.b;
            }
        }
    }
}
