using Pirates.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pirates.Ambient
{
    /// <summary>
    /// Ciclo de dia e noite (M11): a hora anda com o jogo (parado no menu, parada também) e move a luz do sol, que de noite vira a lua
    /// (mesma luz, outra cor, outra direção: as sombras continuam). Acompanham a hora: a luz ambiente, a névoa, o céu, o reflexo do céu na
    /// água e as lanternas (<see cref="NightLight"/>). O dia claro fica como a cena já era: as cores do dia são lidas da cena ao começar.
    /// Registra <see cref="ITimeOfDay"/> no <see cref="Services"/> (IA de terra, save, HUD e sandbox leem por lá). O céu fechado do clima
    /// (<see cref="IWeather.Overcast"/>) escurece o sol, a luz ambiente e o céu, adensa a névoa e, na tempestade, acende as lanternas.
    /// </summary>
    public class DayNightCycle : MonoBehaviour, ITimeOfDay
    {
        [SerializeField] DayNightSettings settings;
        [Tooltip("A luz do sol da cena (de noite, a lua).")]
        [SerializeField] Light sun;
        [Tooltip("Luz ambiente do dia como o editor calculou do céu (27 números, gravados pelo gerador): no jogo compilado a da cena ainda " +
                 "é lixo no Awake (29/10: −3e13 = cena inteira em cores puras ao amanhecer).")]
        [SerializeField, HideInInspector] float[] sceneAmbient = new float[0];

        float hour;
        int day = 1;
        float appliedHour = -1f, appliedOvercast = -1f, appliedStorm = -1f;

        // Como a cena era de dia (lido no Awake): o dia claro continua igual.
        SphericalHarmonicsL2 dayAmbient;
        Color dayFog;
        float dayReflection = 1f, daySkyExposure = 1f, dayShadowStrength = 1f, dayFogDensity = 0.002f;
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

        /// <summary>Gerador: guarda a luz ambiente do dia que o editor calculou (só se ela for válida).</summary>
        public void StoreSceneAmbient(SphericalHarmonicsL2 ambient)
        {
            if (!Valid(ambient)) return;
            sceneAmbient = new float[27];
            for (int c = 0; c < 3; c++)
            for (int k = 0; k < 9; k++) sceneAmbient[c * 9 + k] = ambient[c, k];
        }

        /// <summary>A luz ambiente do dia: a da cena se for válida; senão a gravada pelo gerador; senão a das cores do céu.</summary>
        SphericalHarmonicsL2 DayAmbient()
        {
            var probe = RenderSettings.ambientProbe;
            if (Valid(probe)) return probe;
            var safe = new SphericalHarmonicsL2();
            if (sceneAmbient != null && sceneAmbient.Length == 27)
            {
                for (int c = 0; c < 3; c++)
                for (int k = 0; k < 9; k++) safe[c, k] = sceneAmbient[c * 9 + k];
                if (Valid(safe)) return safe;
                safe.Clear();
            }
            safe.AddAmbientLight(RenderSettings.ambientSkyColor);
            return safe;
        }

        static bool Valid(SphericalHarmonicsL2 sh)
        {
            for (int c = 0; c < 3; c++)
            for (int k = 0; k < 9; k++)
            {
                float v = sh[c, k];
                if (float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 20f) return false;
            }
            return true;
        }

        void Awake()
        {
            if (!sun) sun = RenderSettings.sun;
            dayAmbient = DayAmbient();
            dayFog = RenderSettings.fogColor;
            dayReflection = RenderSettings.reflectionIntensity;
            dayFogDensity = RenderSettings.fogDensity;
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

        void OnEnable()
        {
            Services.Register<ITimeOfDay>(this);
            CoveredArea.Changed += OnCoveredChanged;
        }

        void OnDisable()
        {
            Services.Unregister<ITimeOfDay>(this);
            CoveredArea.Changed -= OnCoveredChanged;
        }

        void OnCoveredChanged(bool inside)
        {
            SetCameraBackground(inside);
            Apply();
        }

        /// <summary>Debaixo da terra o fundo da câmera é preto (fora das paredes não há nada); fora, o céu de volta.</summary>
        static void SetCameraBackground(bool covered)
        {
            var cam = Camera.main;
            if (!cam) return;
            cam.clearFlags = covered ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
        }

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
            if (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, appliedHour * 15f)) > 0.045f || WeatherMoved()) Apply();
        }

        /// <summary>O céu fechou ou abriu o bastante para aparecer (o clima muda aos poucos).</summary>
        bool WeatherMoved()
        {
            Weather(out float overcast, out float storm);
            return Mathf.Abs(overcast - appliedOvercast) > 0.01f || Mathf.Abs(storm - appliedStorm) > 0.01f;
        }

        static void Weather(out float overcast, out float storm)
        {
            overcast = storm = 0f;
            if (!Services.TryGet(out IWeather weather)) return;
            overcast = weather.Overcast;
            storm = weather.Storm;
        }

        /// <summary>Luz do sol/lua, ambiente, névoa, céu, reflexo e lanternas para a hora atual (e o céu fechado do clima).</summary>
        void Apply()
        {
            appliedHour = hour;
            if (!settings) return;
            Weather(out float overcast, out float storm);
            appliedOvercast = overcast;
            appliedStorm = storm;
            float half = settings.twilightHours * 0.5f;
            // Claridade: sobe do nascer (−meia transição) até o dia claro e desce no pôr do sol.
            float daylight = Mathf.Min(Ramp(hour, settings.sunrise - half, settings.sunrise + half),
                                       1f - Ramp(hour, settings.sunset - half, settings.sunset + half));
            float night = 1f - daylight;
            float twilight = 4f * daylight * night;
            Night = night;

            PlaceSunOrMoon();
            if (sun)
            {
                sun.intensity *= Mathf.Lerp(1f, settings.overcastSun, overcast);
                sun.color = Color.Lerp(sun.color, sun.color * settings.overcastTint, overcast);
                sun.shadowStrength = Mathf.Lerp(sun.shadowStrength, settings.overcastShadow, overcast);
            }

            // Luz ambiente: a do céu da cena de dia (com tom de amanhecer/anoitecer), a da noite fechada de noite.
            var ambient = dayAmbient;
            Tint(ref ambient, Color.Lerp(Color.white, settings.twilightTint, twilight));
            var nightAmbient = new SphericalHarmonicsL2();
            nightAmbient.AddAmbientLight(settings.nightAmbient);
            var lit = (ambient * daylight + nightAmbient * night) * Mathf.Lerp(1f, settings.overcastAmbient, overcast);
            Tint(ref lit, Color.Lerp(Color.white, settings.overcastTint, overcast));
            RenderSettings.ambientProbe = lit;

            // Céu fechado: névoa cinza de dia (de noite a da noite manda) e mais densa.
            var fog = Color.Lerp(Color.Lerp(dayFog, settings.overcastFog, overcast), settings.nightFog, night);
            RenderSettings.fogColor = Color.Lerp(fog, settings.twilightFog, twilight * 0.55f * (1f - overcast));
            RenderSettings.fogDensity = dayFogDensity * Mathf.Lerp(1f, settings.overcastFogDensity, overcast);
            RenderSettings.reflectionIntensity = Mathf.Lerp(dayReflection, settings.nightReflection, night) * Mathf.Lerp(1f, settings.overcastReflection, overcast);
            if (sky && sky.HasProperty("_Exposure"))
                sky.SetFloat("_Exposure", Mathf.Lerp(daySkyExposure, settings.nightSkyExposure, night) * Mathf.Lerp(1f, settings.overcastSky, overcast));

            float lanterns = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.lanternsFrom, 0.85f, night));
            NightLight.Level = Mathf.Max(lanterns, settings.stormLanterns * storm);
            if (CoveredArea.Inside) ApplyCovered();
        }

        /// <summary>Debaixo da terra: luz fraca e fria de cima com sombra, ambiente escuro, sem névoa nem reflexo, tochas acesas.</summary>
        void ApplyCovered()
        {
            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler(settings.coveredLightAngles.x, settings.coveredLightAngles.y, 0f);
                sun.intensity = settings.coveredLightIntensity;
                sun.color = settings.coveredLightColor;
                sun.shadowStrength = settings.coveredShadow;
            }
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(settings.coveredAmbient);
            RenderSettings.ambientProbe = ambient;
            RenderSettings.fogDensity = 0f;
            RenderSettings.reflectionIntensity = 0.1f;
            NightLight.Level = 1f;
        }

        /// <summary>De dia o sol anda de leste a oeste; de noite a mesma luz vira a lua. Perto do horizonte a luz some (troca escondida).</summary>
        void PlaceSunOrMoon()
        {
            if (!sun) return;
            float dayLength = settings.sunset - settings.sunrise;
            bool daytime = hour >= settings.sunrise && hour < settings.sunset;
            float t = daytime ? (hour - settings.sunrise) / dayLength : Mathf.Repeat(hour - settings.sunset, 24f) / (24f - dayLength);
            // Curva "gorda" (raiz do seno): o sol fica mais tempo baixo-mas-acima do horizonte, a hora dourada dura e só mergulha no fim.
            // Max(0): perto do nascer o seno de π·t dá −0,0000001 (π em float é um pouco maior) e a raiz de negativo é NaN (luz estragada).
            float elevation = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.55f) * (daytime ? settings.sunMaxElevation : settings.moonMaxElevation);
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
