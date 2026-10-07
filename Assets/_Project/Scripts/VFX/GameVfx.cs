using System;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.VFX
{
    /// <summary>
    /// Efeitos visuais do combate naval (M6.11), sem que o combate conheça este módulo: ouve o <see cref="CombatEffects"/>
    /// (tiro: clarão, faíscas e fumaça que fica no ar e vai com o vento; bala na água: coluna, gotas, anel e espuma; bala
    /// no casco: lascas de madeira, faísca e poeira) e acompanha cada navio (procura de novo a cada 2 s: inimigos
    /// reaparecem como cópias novas): rastro de espuma conforme a velocidade, fumaça com o casco avariado e fogo quando
    /// está quase afundando, e o naufrágio (espirro, anel, bolhas e espuma onde ele some).
    /// Cada tipo de partícula é um único ParticleSystem alimentado por Emit: nada é criado por tiro nem por navio.
    /// Os efeitos a pé (passo 2) ficam em GameVfx.Foot.cs.
    /// </summary>
    public partial class GameVfx : MonoBehaviour
    {
        [Serializable]
        public class Materials
        {
            [Tooltip("Fumaça (alfa).")] public Material smoke;
            [Tooltip("Clarão do disparo (aditivo).")] public Material blast;
            [Tooltip("Brilho redondo e faíscas (aditivo).")] public Material glow;
            [Tooltip("Água e espuma (alfa, textura de nuvem).")] public Material water;
            [Tooltip("Gotas (alfa, bolinha).")] public Material drops;
            [Tooltip("Anel de espuma na água (alfa).")] public Material ring;
            [Tooltip("Chamas (alfa).")] public Material fire;
            [Tooltip("Lascas de madeira (malha, com luz).")] public Material splinters;
            public Mesh splinterMesh;
            [Tooltip("Brilho em estrela: acerto, ouro do baú (aditivo).")] public Material stars;
            [Tooltip("Rastro do cutelo (alfa, cor por vértice, dois lados).")] public Material trail;
        }

        [SerializeField] Materials materials = new();
        [Tooltip("Vento (m/s²) que leva a fumaça (sem IWind na cena; com ele, segue a direção e a força do vento).")]
        [SerializeField] Vector3 wind = new(0.9f, 0.15f, 0.45f);
        [Tooltip("Empurrão da fumaça (m/s²) com calmaria e com vento forte (M11).")]
        [SerializeField] float windPushCalm = 0.4f, windPushStrong = 1.8f;
        [Header("Rastro")]
        [Tooltip("Velocidade (m/s) a partir da qual o navio deixa espuma.")]
        [SerializeField] float wakeMinSpeed = 1.2f;
        [Tooltip("Metros andados entre uma mancha de espuma e outra.")]
        [SerializeField] float wakeSpacing = 1.2f;
        [Header("Avaria")]
        [Tooltip("Abaixo desta fração do casco sai fumaça do convés.")]
        [SerializeField, Range(0f, 1f)] float smokeBelow = 0.6f;
        [Tooltip("Abaixo desta fração do casco o convés pega fogo.")]
        [SerializeField, Range(0f, 1f)] float fireBelow = 0.3f;
        [Tooltip("Segundos de bolhas e espuma depois que o navio afunda.")]
        [SerializeField] float sinkBubbleSeconds = 8f;

        FxSystem smoke, blast, glow, sparks, water, drops, ring, foam, fire, splinters;
        CombatEffects effects;
        IOceanSampler ocean;
        float nextScan, nextWind;
        Vector3 shownWind = new(float.NaN, 0f, 0f);
        readonly List<ShipTrack> ships = new();
        readonly HashSet<ShipController> known = new();

        /// <summary>Um navio acompanhado: medidas do casco (no espaço dele) e o que já foi emitido.</summary>
        sealed class ShipTrack
        {
            public ShipController ship;
            public Rigidbody body;
            public Health health;
            public Vector3 stern, bow, deckA, deckB;
            public float halfWidth;
            public float wake, smoke, fire, bubbles, sinkUntil;
            public bool dead;
        }

        public void Configure(Materials fxMaterials) => materials = fxMaterials;

        void Awake()
        {
            var t = transform;
            smoke = new FxSystem(t, "Fumaca", materials.smoke, new FxSystem.Style
                { gravity = -0.02f, growth = 4f, drag = 1.6f, wind = wind, mode = ParticleSystemRenderMode.Billboard, maxParticles = 1500, fadeStart = 0.5f });
            blast = new FxSystem(t, "Clarao", materials.blast, new FxSystem.Style
                { growth = 1.4f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 200, sortingFudge = -20f, fadeStart = 0.2f });
            glow = new FxSystem(t, "Brilho", materials.glow, new FxSystem.Style
                { growth = 1.2f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 200, sortingFudge = -25f, fadeStart = 0.1f });
            sparks = new FxSystem(t, "Faiscas", materials.glow, new FxSystem.Style
                { gravity = 1f, growth = 0.5f, drag = 0.8f, stretch = 0.05f, maxParticles = 600, sortingFudge = -25f, fadeStart = 0.5f });
            water = new FxSystem(t, "Agua", materials.water, new FxSystem.Style
                { gravity = 1.4f, growth = 1.7f, drag = 0.3f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 800, fadeStart = 0.45f });
            drops = new FxSystem(t, "Gotas", materials.drops, new FxSystem.Style
                { gravity = 1.6f, growth = 0.8f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 800, fadeStart = 0.6f });
            ring = new FxSystem(t, "Anel", materials.ring, new FxSystem.Style
                { growth = 5f, mode = ParticleSystemRenderMode.HorizontalBillboard, maxParticles = 200, sortingFudge = 30f, fadeStart = 0.15f });
            foam = new FxSystem(t, "Espuma", materials.water, new FxSystem.Style
                { growth = 2.6f, drag = 1.2f, mode = ParticleSystemRenderMode.HorizontalBillboard, maxParticles = 1500, sortingFudge = 40f, fadeStart = 0.25f });
            fire = new FxSystem(t, "Fogo", materials.fire, new FxSystem.Style
                { gravity = -0.25f, growth = 0.5f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 600, sortingFudge = -10f, fadeStart = 0.4f, upright = true });
            splinters = new FxSystem(t, "Lascas", materials.splinters, new FxSystem.Style
                { gravity = 1.5f, growth = 1f, drag = 0.2f, mesh = materials.splinterMesh, maxParticles = 600, fadeStart = 0.8f });
            CreateFootSystems(t);
        }

        void Start()
        {
            Services.TryGet(out ocean);
            if (Services.TryGet(out effects))
            {
                effects.Blasted += OnBlasted;
                effects.Splashed += OnSplashed;
                effects.Impacted += OnImpacted;
            }
            Scan();
        }

        void OnDestroy()
        {
            foreach (var ship in known)
                if (ship) ship.HullChanged -= Remeasure;
            DestroyFoot();
            if (!effects) return;
            effects.Blasted -= OnBlasted;
            effects.Splashed -= OnSplashed;
            effects.Impacted -= OnImpacted;
        }

        float WaterHeight(Vector3 p) => ocean != null ? ocean.GetHeight(p) : 0f;

        // ---------- Canhões ----------

        void OnBlasted(Vector3 muzzle, Vector3 direction)
        {
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            glow.Emit(muzzle + direction * 0.6f, direction * 2f, 3.2f, 0.16f, new Color(1f, 0.78f, 0.45f, 1f));
            blast.Emit(muzzle + direction * 1.3f, direction * 4f, 2.8f, 0.2f, new Color(1f, 0.62f, 0.28f, 1f), Random.Range(-90f, 90f));
            for (int i = 0; i < 6; i++)
                sparks.Emit(muzzle, direction * Random.Range(14f, 24f) + Random.insideUnitSphere * 3f, Random.Range(0.1f, 0.18f),
                    Random.Range(0.18f, 0.32f), new Color(1f, 0.8f, 0.45f, 1f));
            // Nuvem que fica no ar alguns segundos e vai com o vento (a cara de uma bordada).
            for (int i = 0; i < 9; i++)
            {
                float grey = Random.Range(0.85f, 0.96f);
                smoke.Emit(muzzle + direction * Random.Range(0.4f, 3f) + Random.insideUnitSphere * 0.3f,
                    direction * Random.Range(2f, 9f) + Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f,
                    Random.Range(1.4f, 2.2f), Random.Range(5f, 7f), new Color(grey, grey, grey, 0.8f), Random.Range(-20f, 20f));
            }
        }

        void OnSplashed(Vector3 point)
        {
            float y = WaterHeight(point);
            var at = new Vector3(point.x, y, point.z);
            for (int i = 0; i < 13; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                water.Emit(at + new Vector3(r.x * 0.5f, 0.2f, r.y * 0.5f), new Vector3(r.x * 1.6f, Random.Range(6f, 12f), r.y * 1.6f),
                    Random.Range(1.2f, 2f), Random.Range(1f, 1.4f), new Color(0.93f, 0.97f, 1f, 0.95f), Random.Range(-40f, 40f));
            }
            for (int i = 0; i < 20; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4.5f);
                drops.Emit(at + Vector3.up * 0.3f, new Vector3(r.x, Random.Range(4f, 9f), r.y), Random.Range(0.25f, 0.45f),
                    Random.Range(0.9f, 1.4f), new Color(0.9f, 0.96f, 1f, 0.95f));
            }
            // Um pouco acima da água: a onda passando cortava o anel ao meio.
            ring.Emit(at + Vector3.up * 0.3f, Vector3.zero, 1.5f, 1.8f, new Color(1f, 1f, 1f, 0.7f));
            for (int i = 0; i < 4; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.8f;
                foam.Emit(at + new Vector3(r.x, 0.12f, r.y), new Vector3(r.x, 0f, r.y), Random.Range(1.2f, 1.8f), Random.Range(2.5f, 3.5f),
                    new Color(1f, 1f, 1f, 0.55f), Random.Range(-15f, 15f));
            }
        }

        void OnImpacted(Vector3 point, Vector3 normal)
        {
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            glow.Emit(point + normal * 0.3f, Vector3.zero, 2f, 0.1f, new Color(1f, 0.8f, 0.5f, 1f));
            for (int i = 0; i < 5; i++)
                sparks.Emit(point, (normal + Random.insideUnitSphere) * Random.Range(6f, 12f), Random.Range(0.08f, 0.14f),
                    Random.Range(0.2f, 0.35f), new Color(1f, 0.85f, 0.5f, 1f));
            for (int i = 0; i < 14; i++)
            {
                float shade = Random.Range(0.75f, 1.15f);
                splinters.EmitSized(point + normal * 0.2f, (normal + Random.insideUnitSphere * 0.8f) * Random.Range(4f, 10f) + Vector3.up * 2f,
                    new Vector3(Random.Range(0.15f, 0.28f), Random.Range(0.14f, 0.24f), Random.Range(0.5f, 1.1f)),
                    Random.Range(1.2f, 1.8f), new Color(0.55f * shade, 0.36f * shade, 0.2f * shade, 1f), Random.Range(300f, 800f));
            }
            for (int i = 0; i < 4; i++)
            {
                float grey = Random.Range(0.5f, 0.65f);
                smoke.Emit(point + normal * 0.5f, normal * Random.Range(0.5f, 2f) + Random.insideUnitSphere * 0.5f,
                    Random.Range(1.2f, 1.7f), Random.Range(2f, 3f), new Color(grey, grey * 0.95f, grey * 0.9f, 0.6f), Random.Range(-30f, 30f));
            }
        }

        /// <summary>Fumaça (tiros, avaria) e poeira com o vento do <see cref="IWind"/>; confere duas vezes por segundo.</summary>
        void FollowWind()
        {
            if (Time.time < nextWind) return;
            nextWind = Time.time + 0.5f;
            if (!Services.TryGet(out IWind current)) return;
            var push = current.Direction * Mathf.Lerp(windPushCalm, windPushStrong, current.Strength);
            push.y = wind.y;
            if ((push - shownWind).sqrMagnitude < 0.0025f) return;
            shownWind = push;
            smoke.SetWind(push);
            dust?.SetWind(push * 0.4f);
        }

        // ---------- Navios ----------

        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 2f;
                Scan();
            }
            FollowWind();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = ships.Count - 1; i >= 0; i--)
            {
                var track = ships[i];
                if (!track.ship || !track.ship.gameObject.activeInHierarchy)
                {
                    known.Remove(track.ship);
                    ships.RemoveAt(i);
                    continue;
                }
                UpdateShip(track, dt);
            }
            UpdateFoot(dt);
        }

        /// <summary>Navios novos (inimigos reaparecem como cópias) ganham medidas e passam a ser acompanhados.</summary>
        void Scan()
        {
            foreach (var ship in FindObjectsByType<ShipController>())
            {
                if (!known.Add(ship)) continue;
                ships.Add(Measure(ship));
                ship.HullChanged += Remeasure;
            }
            ScanFoot();
        }

        /// <summary>Navio que trocou de casco (navio novo do jogador): mede de novo.</summary>
        void Remeasure()
        {
            for (int i = 0; i < ships.Count; i++)
                if (ships[i].ship) ships[i] = Measure(ships[i].ship);
        }

        /// <summary>Casco pelo colisor (sem mastro e velas): popa, proa, largura e dois pontos do convés.</summary>
        static ShipTrack Measure(ShipController ship)
        {
            var t = ship.transform;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var col in ship.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                var b = col.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                    var local = t.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
            if (float.IsInfinity(min.x))
            {
                min = new Vector3(-2f, -1f, -6f);
                max = new Vector3(2f, 2f, 6f);
            }
            float length = max.z - min.z;
            float deck = Mathf.Max(max.y * 0.85f, 0.8f);
            return new ShipTrack
            {
                ship = ship,
                body = ship.GetComponent<Rigidbody>(),
                health = ship.GetComponent<Health>(),
                stern = new Vector3((min.x + max.x) * 0.5f, 0f, min.z + length * 0.04f),
                bow = new Vector3((min.x + max.x) * 0.5f, 0f, max.z - length * 0.12f),
                halfWidth = Mathf.Max(0.8f, (max.x - min.x) * 0.5f),
                // Perto das bordas: vista de cima, o meio do convés fica embaixo das velas.
                deckA = new Vector3(min.x + (max.x - min.x) * 0.2f, deck, min.z + length * Random.Range(0.25f, 0.45f)),
                deckB = new Vector3(max.x - (max.x - min.x) * 0.2f, deck, min.z + length * Random.Range(0.55f, 0.75f)),
            };
        }

        void UpdateShip(ShipTrack s, float dt)
        {
            var t = s.ship.transform;
            Vector3 velocity = s.body ? s.body.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool dead = s.health && s.health.IsDead;

            if (dead && !s.dead) OnSank(s, t);
            s.dead = dead;

            // Rastro: só com o casco na água (afundando fundo, para).
            Vector3 stern = t.TransformPoint(s.stern);
            float sternWater = WaterHeight(stern);
            if (speed > wakeMinSpeed && t.position.y > sternWater - 2.5f) Wake(s, t, speed, dt, stern, sternWater);
            else s.wake = 0f;

            if (s.health) Damage(s, t, dt, dead);
            if (dead && Time.time < s.sinkUntil) Bubbles(s, t, dt);
        }

        void Wake(ShipTrack s, Transform t, float speed, float dt, Vector3 stern, float sternWater)
        {
            s.wake += speed * dt;
            float strength = Mathf.Clamp01((speed - wakeMinSpeed) / 6f);
            while (s.wake >= wakeSpacing)
            {
                s.wake -= wakeSpacing;
                var at = new Vector3(stern.x, sternWater + 0.15f, stern.z) + Random.insideUnitSphere.WithY(0f) * 0.4f;
                foam.Emit(at, -t.forward * 0.4f + Random.insideUnitSphere.WithY(0f) * 0.3f, s.halfWidth * Random.Range(1.3f, 1.7f),
                    Random.Range(3.5f, 4.5f), new Color(1f, 1f, 1f, 0.25f + 0.35f * strength), Random.Range(-10f, 10f));
                for (int side = -1; side <= 1; side += 2)
                {
                    var bow = t.TransformPoint(s.bow + new Vector3(side * s.halfWidth * 0.95f, 0f, 0f));
                    bow.y = WaterHeight(bow) + 0.15f;
                    foam.Emit(bow, t.right * (side * Random.Range(1f, 1.8f)) - t.forward * 0.3f, Random.Range(0.8f, 1.2f),
                        Random.Range(2.2f, 3f), new Color(1f, 1f, 1f, 0.2f + 0.3f * strength), Random.Range(-10f, 10f));
                }
                // Proa cortando a água em velocidade: borrifo.
                if (speed > 6f && Random.value < 0.5f)
                {
                    var tip = t.TransformPoint(s.bow + Vector3.forward * 0.6f);
                    tip.y = WaterHeight(tip) + 0.3f;
                    drops.Emit(tip, t.forward * speed * 0.3f + Vector3.up * Random.Range(2f, 4f) + Random.insideUnitSphere * 1.5f,
                        Random.Range(0.15f, 0.28f), Random.Range(0.6f, 0.9f), new Color(0.92f, 0.97f, 1f, 0.8f));
                }
            }
        }

        void Damage(ShipTrack s, Transform t, float dt, bool dead)
        {
            float hull = s.health.Normalized;
            if (hull >= smokeBelow && !dead) return;
            float danger = dead ? 1f : Mathf.InverseLerp(smokeBelow, 0f, hull);
            bool burning = dead || hull < fireBelow;

            s.smoke += dt * Mathf.Lerp(2f, 7f, danger);
            while (s.smoke >= 1f)
            {
                s.smoke -= 1f;
                var p = t.TransformPoint(Random.value < 0.5f ? s.deckA : s.deckB);
                if (p.y < WaterHeight(p)) continue;
                // Cinza médio (preto puro some no mar escuro visto de cima); com fogo, mais escuro e mais grosso.
                float grey = burning ? Random.Range(0.28f, 0.36f) : Random.Range(0.5f, 0.62f);
                smoke.Emit(p + Random.insideUnitSphere * 0.4f, Vector3.up * Random.Range(2f, 3.5f) + Random.insideUnitSphere * 0.4f,
                    Random.Range(1.3f, 2f), Random.Range(4f, 5.5f), new Color(grey, grey * 0.97f, grey * 0.94f, 0.8f), Random.Range(-20f, 20f));
            }
            if (!burning) return;
            s.fire += dt * 20f;
            while (s.fire >= 1f)
            {
                s.fire -= 1f;
                var p = t.TransformPoint(Random.value < 0.5f ? s.deckA : s.deckB);
                if (p.y < WaterHeight(p)) continue;
                var at = p + Random.insideUnitSphere * 0.6f;
                fire.Emit(at, Vector3.up * Random.Range(2.5f, 4.5f) + Random.insideUnitSphere * 0.4f,
                    Random.Range(2.4f, 3.8f), Random.Range(0.6f, 1f), new Color(1f, Random.Range(0.42f, 0.6f), 0.12f, 0.95f));
                // Núcleo amarelo, menor, por cima.
                if (Random.value < 0.5f)
                    fire.Emit(at, Vector3.up * Random.Range(2f, 3.5f), Random.Range(1.4f, 2.2f), Random.Range(0.4f, 0.6f), new Color(1f, 0.88f, 0.35f, 1f));
                if (Random.value < 0.08f)
                    sparks.Emit(p, Vector3.up * Random.Range(3f, 6f) + Random.insideUnitSphere * 1.5f, 0.08f, Random.Range(0.8f, 1.4f),
                        new Color(1f, 0.7f, 0.3f, 1f));
            }
        }

        /// <summary>Casco zerado: espirro e anel grandes, fumaça escura, e daí em diante bolhas.</summary>
        void OnSank(ShipTrack s, Transform t)
        {
            s.sinkUntil = Time.time + sinkBubbleSeconds;
            var center = t.position;
            center.y = WaterHeight(center);
            ring.Emit(center + Vector3.up * 0.35f, Vector3.zero, s.halfWidth * 1.6f, 2.2f, new Color(1f, 1f, 1f, 0.45f));
            for (int i = 0; i < 22; i++)
            {
                Vector2 r = Random.insideUnitCircle * s.halfWidth * 1.6f;
                water.Emit(center + new Vector3(r.x, 0.3f, r.y), new Vector3(r.x * 0.6f, Random.Range(4f, 9f), r.y * 0.6f),
                    Random.Range(1.8f, 2.8f), Random.Range(1.1f, 1.6f), new Color(0.92f, 0.97f, 1f, 0.9f), Random.Range(-40f, 40f));
            }
            for (int i = 0; i < 8; i++)
            {
                var p = t.TransformPoint(Random.value < 0.5f ? s.deckA : s.deckB);
                smoke.Emit(p, Vector3.up * Random.Range(2f, 4f) + Random.insideUnitSphere, Random.Range(1.5f, 2.2f), Random.Range(4f, 6f),
                    new Color(0.15f, 0.15f, 0.15f, 0.7f), Random.Range(-20f, 20f));
            }
        }

        void Bubbles(ShipTrack s, Transform t, float dt)
        {
            s.bubbles += dt * 10f;
            while (s.bubbles >= 1f)
            {
                s.bubbles -= 1f;
                Vector2 r = Random.insideUnitCircle * s.halfWidth * 1.6f;
                var p = t.position + new Vector3(r.x, 0f, r.y);
                p.y = WaterHeight(p) + 0.12f;
                foam.Emit(p, Vector3.zero, Random.Range(0.9f, 1.8f), Random.Range(2f, 3f), new Color(1f, 1f, 1f, 0.6f), Random.Range(-20f, 20f));
                if (Random.value < 0.3f)
                    drops.Emit(p, Vector3.up * Random.Range(1f, 2.5f), Random.Range(0.12f, 0.22f), 0.6f, new Color(0.9f, 0.96f, 1f, 0.8f));
            }
        }
    }

    static class VectorExtensions
    {
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
    }
}
