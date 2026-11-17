using System;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Ships
{
    /// <summary>
    /// As velas do modelo seguem o nível de vela do ShipController: recolhidas ficam enroladas no alto da verga, cheias
    /// ficam abertas, e trocar o nível sobe ou baixa o pano aos poucos. Cada vela é um eixo no topo dela (o gerador
    /// cria) que encolhe na vertical. Só visual: a velocidade continua vindo do ShipController.
    /// Vento (clima, 17/11): o pano enche (a barriga da vela cresce para a frente, Z do eixo) com o vento por trás ou de lado e fica frouxo,
    /// batendo, com o vento pela proa ou em calmaria; rajadas fazem a barriga respirar. Pelo vento verdadeiro do <see cref="IWind"/> (as
    /// bandeiras, <see cref="FlagWave"/>, usam o aparente).
    /// </summary>
    public class SailVisual : MonoBehaviour
    {
        [SerializeField] ShipController ship;
        [Tooltip("Eixos no topo de cada vela (encolhem em Y).")]
        [SerializeField] Transform[] sails = Array.Empty<Transform>();
        [Tooltip("Altura da vela recolhida (fração da aberta).")]
        [SerializeField, Range(0.02f, 1f)] float furled = 0.12f;
        [Tooltip("Quanto da vela abre ou fecha por segundo.")]
        [SerializeField, Min(0.05f)] float speed = 0.8f;
        [Header("Vento no pano")]
        [Tooltip("Barriga da vela (escala em Z): frouxa (vento contra/calmaria) e cheia (vento forte por trás).")]
        [SerializeField] float slackBelly = 0.35f, fullBelly = 1.2f;
        [Tooltip("Batida do pano frouxo (fração da barriga) e quantas vezes por segundo.")]
        [SerializeField] float luffAmount = 0.35f, luffRate = 2.2f;
        [Tooltip("Distância máxima da câmera para o pano mexer com o vento.")]
        [SerializeField] float windDistance = 220f;

        float open = -1f;
        float belly = 1f, luff, shown = 1f, seed;

        public void Configure(ShipController controller, Transform[] sailPivots)
        {
            ship = controller;
            sails = sailPivots;
        }

        void Awake()
        {
            seed = UnityEngine.Random.value * 100f;
        }

        void Update()
        {
            if (!ship || !ship.Stats) return;
            float max = 0f;
            foreach (var level in ship.Stats.sailLevels) max = Mathf.Max(max, Mathf.Abs(level.throttle));
            // Ré também abre um pouco de pano (a ré é vela contra o vento).
            float target = max > 0f ? Mathf.Clamp01(Mathf.Abs(ship.Throttle) / max) : 1f;
            open = open < 0f ? target : Mathf.MoveTowards(open, target, speed * Time.deltaTime);
            float height = Mathf.Lerp(furled, 1f, open);
            float depth = Belly();
            for (int i = 0; i < sails.Length; i++)
            {
                var sail = sails[i];
                if (!sail) continue;
                // Cada vela respira um pouco fora de compasso com a outra.
                float own = depth * (1f + Mathf.Sin(Time.time * 1.3f + seed + i * 1.7f) * 0.03f);
                sail.localScale = new Vector3(1f, height, Mathf.Lerp(1f, own, open));
            }
        }

        /// <summary>Barriga da vela pelo vento aparente (1 = como o modelo).</summary>
        float Belly()
        {
            var cam = Camera.main;
            if (!cam || (transform.position - cam.transform.position).sqrMagnitude > windDistance * windDistance) return shown;
            if (!Services.TryGet(out IWind wind)) return shown = 1f;
            Services.TryGet(out IWeather weather);
            float storm = weather != null ? weather.Storm : 0f;
            // Pelo vento verdadeiro, como o bônus de velocidade (ShipController.WindFactor): de través e por trás o pano enche; com o navio
            // rápido o vento aparente vem sempre pela proa e a vela ficaria frouxa no melhor rumo.
            float strength = wind.Strength;
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            // 1 = vento por trás, 0 = de lado, −1 = pela proa. Até ~50° da proa o pano não enche.
            float from = Vector3.Dot(wind.Direction, forward);
            float fill = Mathf.Clamp01(Mathf.InverseLerp(-0.65f, -0.2f, from)) * Mathf.SmoothStep(0.25f, 1f, Mathf.InverseLerp(0f, 0.6f, strength));
            float t = Time.time;
            float gust = (Mathf.PerlinNoise(seed, t * 0.5f) - 0.5f) * 0.25f * strength;
            float target = Mathf.Lerp(slackBelly, fullBelly, Mathf.Clamp01(fill + gust));
            // Pano frouxo bate (mais com vento forte e na tempestade); cheio só treme.
            float luffTarget = (1f - fill) * luffAmount * Mathf.Lerp(0.3f, 1f, strength) * (1f + storm);
            float k = 1f - Mathf.Exp(-Time.deltaTime * 3f);
            belly = Mathf.Lerp(belly, target, k);
            luff = Mathf.Lerp(luff, luffTarget, k);
            float flap = Mathf.Sin(t * luffRate * Mathf.PI * 2f + seed) * 0.6f + Mathf.Sin(t * luffRate * 4.7f + seed * 2f) * 0.4f;
            return shown = belly * (1f + flap * luff);
        }
    }
}
