using Pirates.Core;
using UnityEngine;

namespace Pirates.Ambient
{
    /// <summary>
    /// Riscos de vento (M11, clima; à moda do Wind Waker): fitas brancas finas que passam voando na direção do vento perto do que a câmera
    /// olha, ondulando, e algumas dão um laço no meio do caminho. Mais riscos, mais rápidos e mais compridos com vento forte; quase nenhum na
    /// calmaria; mais fracos de noite. Um punhado de TrailRenderer reaproveitados (nada é criado durante o jogo).
    /// </summary>
    public class WindStreaks : MonoBehaviour
    {
        [SerializeField] Material material;
        [Tooltip("Riscos ao mesmo tempo, no máximo.")]
        [SerializeField, Range(1, 32)] int poolSize = 14;
        [Tooltip("Riscos por segundo com calmaria e com vento forte.")]
        [SerializeField] float rateCalm = 0.25f, rateStrong = 4.5f;
        [Tooltip("Velocidade (m/s) com calmaria e com vento forte.")]
        [SerializeField] float speedCalm = 7f, speedStrong = 15f;
        [Tooltip("Segundos de vida de um risco (sorteado entre x e y).")]
        [SerializeField] Vector2 lifetime = new(1.4f, 2.2f);
        [Tooltip("Altura sobre o chão/mar (sorteada entre x e y).")]
        [SerializeField] Vector2 height = new(1.5f, 6f);
        [Tooltip("Largura da fita: metros por metro de distância da câmera (fica com a mesma grossura na tela), com mínimo e máximo.")]
        [SerializeField] float widthPerMeter = 0.0075f;
        [SerializeField] Vector2 widthRange = new(0.1f, 0.4f);
        [Tooltip("Chance de um risco dar um laço.")]
        [SerializeField, Range(0f, 1f)] float loopChance = 0.35f;
        [Tooltip("Raio do laço (m).")]
        [SerializeField] float loopRadius = 1.1f;
        [Tooltip("Transparência máxima (de dia; de noite cai para a metade).")]
        [SerializeField, Range(0f, 1f)] float alpha = 0.75f;

        struct Streak
        {
            public TrailRenderer trail;
            public bool alive;
            public Vector3 start, direction, side, loopAxis;
            public float age, life, speed, wave, wavePhase, loopAt, loopSeconds, loopRadius;
        }

        Streak[] streaks;
        float spawnDebt;
        Camera cam;

        public void Configure(Material trailMaterial) => material = trailMaterial;

        void Awake()
        {
            streaks = new Streak[poolSize];
            var widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("RiscoDeVento");
                go.transform.SetParent(transform, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.widthCurve = widthCurve;
                trail.minVertexDistance = 0.35f;
                trail.alignment = LineAlignment.View;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                trail.enabled = false;
                streaks[i].trail = trail;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!cam) cam = Camera.main;
            bool hasWind = Services.TryGet(out IWind wind);
            float strength = hasWind ? wind.Strength : 0f;

            // Novos riscos pela força do vento (com vento fraco, um de vez em quando).
            if (hasWind && cam && !CoveredArea.Inside)
            {
                spawnDebt += Mathf.Lerp(rateCalm, rateStrong, strength * strength) * dt;
                while (spawnDebt >= 1f)
                {
                    spawnDebt -= 1f;
                    Spawn(wind, strength);
                }
            }

            float night = Services.TryGet(out ITimeOfDay time) ? time.Night : 0f;
            float a = alpha * Mathf.Lerp(1f, 0.5f, night);
            for (int i = 0; i < streaks.Length; i++)
            {
                ref var s = ref streaks[i];
                if (!s.alive) continue;
                s.age += dt;
                if (s.age >= s.life)
                {
                    // Para de soltar fita; o rastro some sozinho e o risco volta para a fila.
                    s.trail.emitting = false;
                    if (s.age >= s.life + s.trail.time)
                    {
                        s.alive = false;
                        s.trail.enabled = false;
                    }
                    continue;
                }
                s.trail.transform.position = PathPoint(s, s.age);
                // Min aninhado: com 3 argumentos cai no "params float[]" e aloca um array por risco a cada quadro.
                float fade = Mathf.Min(1f, Mathf.Min(s.age / 0.3f, (s.life - s.age) / 0.4f));
                var color = new Color(1f, 1f, 1f, a * fade);
                s.trail.startColor = color;
                s.trail.endColor = new Color(1f, 1f, 1f, 0f);
            }
        }

        /// <summary>Onde o risco está: em frente na direção do vento, ondulando de lado, com um laço (se tiver) no meio do caminho.</summary>
        static Vector3 PathPoint(in Streak s, float t)
        {
            var p = s.start + s.direction * (s.speed * t) + s.side * (Mathf.Sin(t * 2.6f + s.wavePhase) * s.wave);
            if (s.loopSeconds > 0f && t > s.loopAt)
            {
                // Volta inteira num plano de pé ao longo do vento: soma com o avanço e vira um cacho.
                float k = Mathf.Clamp01((t - s.loopAt) / s.loopSeconds);
                float angle = k * Mathf.PI * 2f;
                p += s.loopAxis * ((1f - Mathf.Cos(angle)) * s.loopRadius) - s.direction * (Mathf.Sin(angle) * s.loopRadius);
            }
            return p;
        }

        void Spawn(IWind wind, float strength)
        {
            int free = -1;
            for (int i = 0; i < streaks.Length; i++)
                if (!streaks[i].alive) { free = i; break; }
            if (free < 0) return;

            // Centro: onde a câmera olha (no nível do mar); a área cresce com a distância da câmera.
            var focus = ViewFocus.Of(cam, out float reach);
            var dir = wind.Direction;
            var side = Vector3.Cross(Vector3.up, dir);
            float speed = Mathf.Lerp(speedCalm, speedStrong, strength) * Random.Range(0.85f, 1.15f);
            float life = Random.Range(lifetime.x, lifetime.y);
            // Nasce vento acima para atravessar o meio da tela.
            var start = focus - dir * (speed * life * 0.5f) + dir * Random.Range(-0.35f, 0.35f) * reach
                        + side * Random.Range(-0.6f, 0.6f) * reach;
            start.y = Mathf.Max(0f, focus.y) + Random.Range(height.x, height.y);

            ref var s = ref streaks[free];
            s.alive = true;
            s.age = 0f;
            s.life = life;
            s.speed = speed;
            s.start = start;
            s.direction = dir;
            s.side = side;
            s.wave = Random.Range(0.25f, 0.7f);
            s.wavePhase = Random.Range(0f, Mathf.PI * 2f);
            s.loopSeconds = Random.value < loopChance ? Random.Range(0.45f, 0.65f) : 0f;
            s.loopAt = life * Random.Range(0.3f, 0.5f);
            // A volta tem de girar mais rápido que o risco avança, senão vira só uma onda (não cruza o próprio caminho).
            s.loopRadius = Mathf.Max(loopRadius, speed * s.loopSeconds / (Mathf.PI * 2f) * 1.3f);
            // Laço em pé, um pouco inclinado de lado (de cima também se vê a volta).
            s.loopAxis = (Vector3.up + side * Random.Range(-0.6f, 0.6f)).normalized;
            s.trail.time = Mathf.Lerp(0.5f, 0.85f, strength);
            s.trail.widthMultiplier = Mathf.Clamp(reach * widthPerMeter, widthRange.x, widthRange.y);
            s.trail.enabled = true;
            s.trail.Clear();
            s.trail.transform.position = start;
            s.trail.Clear();
            s.trail.emitting = true;
        }
    }
}
