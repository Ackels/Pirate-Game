using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Loot;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.VFX
{
    /// <summary>
    /// Efeitos a pé (M6.11, passo 2), sem que o combate a pé ou o saque conheçam este módulo: ouve o
    /// <see cref="CharacterCombat"/> de cada personagem (rastro do cutelo, clarão e fumaça da pistola, faísca no acerto),
    /// acompanha os pés (poeira da cor do chão ao correr, pular e cair; respingo na água rasa; poeira do corpo caindo) e
    /// olha o saque (caixote quebrando, tesouro desenterrado, baú destrancado brilhando). Personagens longe da câmera ou
    /// dormindo (acampamento longe, O3) não são olhados.
    /// </summary>
    public partial class GameVfx
    {
        [Header("A pé")]
        [Tooltip("Velocidade (m/s) a partir da qual os pés levantam poeira (correndo).")]
        [SerializeField] float dustMinSpeed = 3.6f;
        [Tooltip("Personagens, baús e caixotes mais longe que isso da câmera não soltam efeito.")]
        [SerializeField] float footEffectsDistance = 70f;
        [Tooltip("Segundos que o rastro do cutelo leva para sumir.")]
        [SerializeField] float bladeTrailSeconds = 0.16f;
        [Tooltip("Velocidade da ponta da lâmina (m/s, em relação ao corpo) a partir da qual o rastro aparece.")]
        [SerializeField] float bladeTrailMinSpeed = 5f;
        [Tooltip("Brilhos por segundo do baú destrancado com coisa dentro.")]
        [SerializeField] float chestSparkleRate = 3f;

        FxSystem dust, stars;
        readonly List<FootTrack> walkers = new();
        readonly HashSet<Object> hookedFoot = new();
        readonly Dictionary<LandChest, bool> chestOpened = new();
        readonly Dictionary<BuriedTreasure, bool> diggable = new();
        float chestSparkle;
        Transform cameraTransform;

        enum Ground { None, Sand, Grass, Dirt, Rock, Wood }

        /// <summary>Um personagem a pé acompanhado: passada, queda e o rastro do cutelo.</summary>
        sealed class FootTrack
        {
            public bool dodging;
            public FootCharacter character;
            public CharacterCombat combat;
            public Health health;
            public BladeTrail trail;
            public float stride, fallSpeed, trailUntil, bodyFallAt;
            public bool wasGrounded = true, trailing, wasInWater;
            public Vector3 lastTipLocal;
        }

        void CreateFootSystems(Transform t)
        {
            dust = new FxSystem(t, "Poeira", materials.smoke, new FxSystem.Style
                { gravity = -0.01f, growth = 2.6f, drag = 2.4f, wind = wind * 0.4f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 800, fadeStart = 0.25f });
            stars = new FxSystem(t, "Estrelas", materials.stars, new FxSystem.Style
                { gravity = -0.05f, growth = 0.4f, drag = 1f, mode = ParticleSystemRenderMode.Billboard, maxParticles = 300, sortingFudge = -25f, fadeStart = 0.35f });
        }

        void DestroyFoot()
        {
            foreach (var w in walkers)
            {
                if (w.combat)
                {
                    w.combat.Fired -= OnPistolFired;
                    w.combat.PelletLanded -= OnPelletLanded;
                    w.combat.Hit -= OnWeaponHit;
                    w.combat.KickLanded -= OnKickLanded;
                }
                w.trail?.Destroy();
            }
            walkers.Clear();
        }

        // ---------- Personagens ----------

        void ScanFoot()
        {
            foreach (var character in FindObjectsByType<FootCharacter>(FindObjectsInactive.Include))
            {
                if (!hookedFoot.Add(character)) continue;
                var track = new FootTrack { character = character };
                character.TryGetComponent(out track.health);
                if (character.TryGetComponent(out track.combat))
                {
                    var combat = track.combat;
                    for (int i = 0; i < combat.Weapons.Count; i++)
                    {
                        var visual = combat.WeaponVisual(i);
                        if (!combat.Weapons[i] || combat.Weapons[i].kind != WeaponKind.Melee || !visual) continue;
                        track.trail = new BladeTrail(transform, visual, materials.trail, new Color(1f, 0.97f, 0.9f, 0.9f), bladeTrailSeconds);
                        break;
                    }
                    combat.Attacked += (weapon, _) => OnSwing(track, weapon);
                    combat.Fired += OnPistolFired;
                    combat.PelletLanded += OnPelletLanded;
                    combat.Hit += OnWeaponHit;
                    combat.KickLanded += OnKickLanded;
                }
                if (track.health) track.health.Died += () => track.bodyFallAt = Time.time + 0.75f;
                character.KnockedDown += () => track.bodyFallAt = Time.time + 0.6f;
                walkers.Add(track);
            }
            foreach (var crate in BreakableLoot.Active)
            {
                if (!hookedFoot.Add(crate) || !crate.TryGetComponent(out Health h)) continue;
                var at = crate.transform;
                h.Died += () => CrateDust(at.position);
            }
        }

        void OnSwing(FootTrack track, WeaponDefinition weapon)
        {
            if (!weapon || weapon.kind != WeaponKind.Melee || track.trail == null) return;
            // O golpe acerta em hitDelay; o rastro vale do começo da animação até um pouco depois do acerto.
            track.trailUntil = Time.time + weapon.hitDelay + 0.25f;
        }

        void OnPistolFired(WeaponDefinition weapon, Vector3 muzzle, Vector3 end)
        {
            if (Far(muzzle)) return;
            Vector3 shot = end - muzzle;
            float distance = shot.magnitude;
            Vector3 direction = distance > 0.01f ? shot / distance : Vector3.forward;
            // Bacamarte (vários chumbos): clarão, faíscas e fumaça maiores, e a fumaça abre em leque.
            bool scatter = weapon && weapon.pellets > 1;
            float size = scatter ? 1.7f : 1f;
            glow.Emit(muzzle + direction * 0.15f, direction * 0.5f, 1.4f * size, 0.09f, new Color(1f, 0.8f, 0.5f, 1f));
            blast.Emit(muzzle + direction * 0.35f * size, direction * 2f, 1.1f * size, 0.12f, new Color(1f, 0.65f, 0.3f, 1f), Random.Range(-90f, 90f));
            for (int i = 0; i < (scatter ? 10 : 4); i++)
                sparks.Emit(muzzle, direction * Random.Range(8f, 14f) + Random.insideUnitSphere * (scatter ? 4f : 2f), Random.Range(0.03f, 0.06f),
                    Random.Range(0.1f, 0.2f), new Color(1f, 0.82f, 0.5f, 1f));
            for (int i = 0; i < (scatter ? 10 : 6); i++)
            {
                float grey = Random.Range(0.9f, 1f);
                dust.Emit(muzzle + direction * Random.Range(0.1f, 0.6f), direction * Random.Range(1f, 3.5f) + Random.insideUnitSphere * 0.3f + Vector3.up * 0.2f,
                    Random.Range(0.3f, 0.45f), Random.Range(1.8f, 2.6f), new Color(grey, grey, grey, 0.8f), Random.Range(-30f, 30f));
            }
            // Bala que parou antes do alcance bateu em algo: poeirinha no ponto (no alvo, a faísca vem do Hit).
            if (weapon && distance < weapon.range - 0.05f)
                for (int i = 0; i < 3; i++)
                    dust.Emit(end - direction * 0.1f, (-direction + Random.insideUnitSphere * 0.6f) * Random.Range(0.6f, 1.6f),
                        Random.Range(0.2f, 0.32f), Random.Range(0.6f, 1f), new Color(0.75f, 0.68f, 0.58f, 0.7f), Random.Range(-40f, 40f));
        }

        /// <summary>
        /// Chumbo do leque do bacamarte (o tiro em si já fez o clarão): faíscas saindo da boca na direção dele (o leque abre na frente do
        /// cano) e, onde ele parou, poeira (bateu em algo) ou um fiapo de fumaça (acabou o alcance no ar): dá para ver a largura do tiro.
        /// </summary>
        void OnPelletLanded(WeaponDefinition weapon, Vector3 muzzle, Vector3 end)
        {
            if (!weapon || Far(muzzle)) return;
            Vector3 shot = end - muzzle;
            float distance = shot.magnitude;
            if (distance < 0.01f) return;
            Vector3 direction = shot / distance;
            for (int i = 0; i < 2; i++)
                sparks.Emit(muzzle + direction * 0.1f, direction * Random.Range(14f, 22f) + Random.insideUnitSphere * 0.8f, Random.Range(0.03f, 0.05f),
                    Random.Range(0.12f, 0.22f), new Color(1f, 0.85f, 0.55f, 1f));
            if (Far(end)) return;
            bool hitSomething = distance < weapon.range - 0.05f;
            for (int i = 0; i < 2; i++)
                dust.Emit(end - direction * 0.1f, (hitSomething ? -direction + Random.insideUnitSphere * 0.6f : Random.insideUnitSphere * 0.4f) * Random.Range(0.6f, 1.6f),
                    hitSomething ? Random.Range(0.2f, 0.3f) : Random.Range(0.18f, 0.26f), Random.Range(0.5f, 0.9f),
                    hitSomething ? new Color(0.75f, 0.68f, 0.58f, 0.7f) : new Color(0.92f, 0.92f, 0.9f, 0.6f), Random.Range(-40f, 40f));
        }

        void OnWeaponHit(WeaponDefinition weapon, Vector3 point)
        {
            if (Far(point)) return;
            bool melee = weapon && weapon.kind == WeaponKind.Melee;
            stars.Emit(point, Vector3.zero, melee ? 0.9f : 0.6f, 0.14f, new Color(1f, 0.95f, 0.8f, 1f), Random.Range(-30f, 30f));
            for (int i = 0; i < (melee ? 7 : 4); i++)
                sparks.Emit(point, (Random.insideUnitSphere + Vector3.up * 0.4f) * Random.Range(3f, 7f), Random.Range(0.03f, 0.06f),
                    Random.Range(0.15f, 0.3f), new Color(1f, 0.85f, 0.55f, 1f));
            // Caixote ou barril: lascas pequenas (as grandes já saem do próprio caixote quando quebra).
            if (NearCrate(point))
                for (int i = 0; i < 5; i++)
                {
                    float shade = Random.Range(0.8f, 1.15f);
                    splinters.EmitSized(point, (Random.insideUnitSphere + Vector3.up) * Random.Range(2f, 4f),
                        new Vector3(Random.Range(0.04f, 0.07f), Random.Range(0.03f, 0.05f), Random.Range(0.12f, 0.22f)),
                        Random.Range(0.8f, 1.2f), new Color(0.6f * shade, 0.42f * shade, 0.24f * shade, 1f), Random.Range(300f, 700f));
                }
        }

        void OnKickLanded(Vector3 point)
        {
            if (Far(point)) return;
            stars.Emit(point, Vector3.zero, 1f, 0.16f, new Color(1f, 0.95f, 0.8f, 1f), Random.Range(-30f, 30f));
            if (DustColor(point, out var color))
                for (int i = 0; i < 4; i++)
                    dust.Emit(point + Random.insideUnitSphere * 0.2f, Random.insideUnitSphere * 1.2f + Vector3.up * 0.4f, Random.Range(0.25f, 0.4f),
                        Random.Range(0.5f, 0.8f), color, Random.Range(-40f, 40f));
        }

        static bool NearCrate(Vector3 point)
        {
            for (int i = 0; i < BreakableLoot.Active.Count; i++)
                if ((BreakableLoot.Active[i].transform.position - point).sqrMagnitude < 1.5f * 1.5f) return true;
            return false;
        }

        bool Far(Vector3 p) => cameraTransform && (p - cameraTransform.position).sqrMagnitude > footEffectsDistance * footEffectsDistance;

        void UpdateFoot(float dt)
        {
            var cam = Camera.main;
            cameraTransform = cam ? cam.transform : null;
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                if (!w.character)
                {
                    w.trail?.Destroy();
                    walkers.RemoveAt(i);
                    continue;
                }
                if (w.bodyFallAt > 0f && Time.time >= w.bodyFallAt)
                {
                    w.bodyFallAt = 0f;
                    if (!Far(w.character.transform.position)) Burst(w.character.transform.position, 7, 0.45f, 1.1f);
                }
                // Dormindo (acampamento longe) ou desligado (o capitão no navio): nada a fazer.
                if (!w.character.isActiveAndEnabled || Far(w.character.transform.position))
                {
                    w.wasGrounded = true;
                    w.stride = 0f;
                    continue;
                }
                Feet(w, dt);
            }
            Loot(dt);
        }

        /// <summary>Poeira ao correr (uma nuvenzinha por passada), ao pular e ao cair; na água rasa, respingo.</summary>
        void Feet(FootTrack w, float dt)
        {
            var c = w.character;
            var p = c.transform.position;
            bool grounded = c.IsGrounded;
            bool dead = w.health && w.health.IsDead;
            if (!grounded) w.fallSpeed = Mathf.Min(w.fallSpeed, c.VerticalVelocity);
            if (grounded && !w.wasGrounded)
            {
                if (w.fallSpeed < -4.5f) Land(p, Mathf.InverseLerp(-4.5f, -12f, w.fallSpeed));
                w.fallSpeed = 0f;
            }
            if (!grounded && w.wasGrounded && c.VerticalVelocity > 1f) Land(p, 0f);   // pulo
            if (c.IsDodging && !w.dodging && grounded) Land(p, 0.3f);   // rolamento: poeira na saída (o rastro é o da corrida)
            w.dodging = c.IsDodging;
            w.wasGrounded = grounded;

            float speed = c.PlanarSpeed;
            float water = WaterHeight(p);
            bool inWater = p.y < water + 0.05f && !c.InDeepWater;
            if (!grounded || dead || speed < 1.2f || (!inWater && speed < dustMinSpeed))
            {
                w.stride = 0.5f;
                return;
            }
            w.stride += speed * dt;
            float length = inWater ? 0.9f : 1.6f;
            if (w.stride < length) return;
            w.stride -= length;
            var back = -c.PlanarVelocity / Mathf.Max(speed, 0.01f);
            if (inWater)
            {
                var at = new Vector3(p.x, water + 0.05f, p.z);
                for (int i = 0; i < 5; i++)
                    drops.Emit(at + Random.insideUnitSphere.WithY(0f) * 0.2f, (back * 0.6f + Random.insideUnitSphere.WithY(0f) * 0.8f) + Vector3.up * Random.Range(1.5f, 3f),
                        Random.Range(0.08f, 0.14f), Random.Range(0.4f, 0.6f), new Color(0.9f, 0.96f, 1f, 0.85f));
                foam.Emit(at + Vector3.up * 0.04f, Vector3.zero, Random.Range(0.6f, 0.9f), Random.Range(1.2f, 1.6f), new Color(1f, 1f, 1f, 0.45f), Random.Range(-20f, 20f));
                return;
            }
            if (!DustColor(p, out var color)) return;
            for (int i = 0; i < 2; i++)
                dust.Emit(p + back * 0.25f + Vector3.up * 0.1f + Random.insideUnitSphere.WithY(0f) * 0.15f,
                    back * Random.Range(0.4f, 1f) + Vector3.up * Random.Range(0.2f, 0.5f), Random.Range(0.3f, 0.45f), Random.Range(0.7f, 1f),
                    color, Random.Range(-40f, 40f));
        }

        /// <summary>Anel de poeira em volta dos pés (pulo, queda; <paramref name="strength"/> 0–1 pela altura).</summary>
        void Land(Vector3 p, float strength)
        {
            if (!DustColor(p, out var color)) return;
            int n = 5 + Mathf.RoundToInt(strength * 5f);
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.value * 0.5f) / n * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                dust.Emit(p + dir * 0.25f + Vector3.up * 0.08f, dir * Random.Range(1.2f, 2f + strength * 1.5f) + Vector3.up * 0.25f,
                    Random.Range(0.3f, 0.45f + strength * 0.2f), Random.Range(0.7f, 1.1f), color, Random.Range(-40f, 40f));
            }
        }

        /// <summary>Poeira de um corpo ou caixote caindo no chão.</summary>
        void Burst(Vector3 p, int n, float size, float life)
        {
            if (!DustColor(p, out var color)) color = new Color(0.92f, 0.86f, 0.74f, 0.6f);
            for (int i = 0; i < n; i++)
            {
                var dir = Random.insideUnitSphere.WithY(0f).normalized;
                dust.Emit(p + dir * 0.4f + Vector3.up * 0.15f, dir * Random.Range(0.6f, 1.6f) + Vector3.up * Random.Range(0.2f, 0.6f),
                    Random.Range(size * 0.8f, size * 1.2f), Random.Range(life * 0.8f, life * 1.2f), color, Random.Range(-40f, 40f));
            }
        }

        void CrateDust(Vector3 p)
        {
            if (Far(p)) return;
            Burst(p, 8, 0.5f, 1.2f);
        }

        /// <summary>
        /// Cor da poeira pelo chão debaixo do pé (a mesma leitura da paleta do som de passos): areia clara, terra marrom,
        /// grama esverdeada e fraca, pedra cinza; madeira (píer, convés) não levanta poeira. Cores claras: a textura da
        /// fumaça já escurece (com a cor da areia a poeira parecia terra).
        /// </summary>
        static bool DustColor(Vector3 p, out Color color)
        {
            color = default;
            var ground = GroundAt(p);
            switch (ground)
            {
                case Ground.Sand: color = new Color(1f, 0.95f, 0.8f, 0.65f); return true;
                case Ground.Grass: color = new Color(0.88f, 0.9f, 0.72f, 0.4f); return true;
                case Ground.Rock: color = new Color(0.9f, 0.9f, 0.88f, 0.55f); return true;
                case Ground.Dirt: color = new Color(0.85f, 0.72f, 0.55f, 0.65f); return true;
                default: return false;
            }
        }

        static Ground GroundAt(Vector3 p)
        {
            // Começa dentro da cápsula do personagem: o raio não acerta o próprio colisor.
            if (!Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return Ground.None;
            if (hit.collider is not MeshCollider) return Ground.Wood;
            Vector2 uv = hit.textureCoord;
            int column = Mathf.FloorToInt(uv.x * 16f);
            if (uv.y > 0.25f) return column <= 1 ? Ground.Sand : column <= 3 ? Ground.Grass : Ground.Dirt;
            return column >= 14 ? Ground.Dirt : column >= 2 && column <= 4 ? Ground.Rock : Ground.Dirt;
        }

        // ---------- Cutelo ----------

        void LateUpdate()
        {
            float now = Time.time;
            for (int i = 0; i < walkers.Count; i++)
            {
                var w = walkers[i];
                var trail = w.trail;
                if (trail == null || !w.character || !trail.Valid) continue;
                bool armed = now < w.trailUntil && w.character.isActiveAndEnabled;
                if (armed)
                {
                    var body = w.character.transform;
                    var tipLocal = body.InverseTransformPoint(trail.Tip);
                    float dt = Time.deltaTime;
                    float tipSpeed = dt > 0f ? (tipLocal - w.lastTipLocal).magnitude / dt : 0f;
                    w.lastTipLocal = tipLocal;
                    bool fast = tipSpeed >= bladeTrailMinSpeed;
                    if (fast && !w.trailing) trail.Break();
                    if (fast) trail.Sample();
                    w.trailing = fast;
                }
                else
                {
                    w.trailing = false;
                    w.lastTipLocal = w.character.transform.InverseTransformPoint(trail.Tip);
                }
                trail.Rebuild();
            }
        }

        // ---------- Saque ----------

        void Loot(float dt)
        {
            chestSparkle += dt * chestSparkleRate;
            bool sparkle = chestSparkle >= 1f;
            if (sparkle) chestSparkle -= 1f;
            for (int i = 0; i < LandChest.Active.Count; i++)
            {
                var chest = LandChest.Active[i];
                var p = chest.transform.position;
                bool open = chest.IsBeingLooted;
                if (chestOpened.TryGetValue(chest, out bool was) && open && !was && !Far(p)) GoldBurst(p + Vector3.up * 0.6f, 14);
                chestOpened[chest] = open;
                // Mesmo critério do facho dourado do baú: destrancado e com coisa dentro.
                if (sparkle && !chest.IsLocked && !chest.IsEmpty && !Far(p))
                    stars.Emit(p + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.1f), Random.Range(-0.45f, 0.45f)),
                        Vector3.up * 0.2f, Random.Range(0.45f, 0.75f), Random.Range(0.5f, 0.8f), new Color(1f, 0.85f, 0.4f, 1f), Random.Range(-60f, 60f));
            }
            for (int i = 0; i < BuriedTreasure.Active.Count; i++)
            {
                var spot = BuriedTreasure.Active[i];
                bool can = spot.CanDig;
                if (diggable.TryGetValue(spot, out bool was) && was && !can && !Far(spot.transform.position)) Dig(spot.transform.position);
                diggable[spot] = can;
            }
        }

        /// <summary>Tesouro desenterrado: torrões de terra voando, poeira marrom e brilho dourado.</summary>
        void Dig(Vector3 p)
        {
            for (int i = 0; i < 16; i++)
            {
                float shade = Random.Range(0.75f, 1.1f);
                float s = Random.Range(0.1f, 0.2f);
                splinters.EmitSized(p + Random.insideUnitSphere.WithY(0f) * 0.4f + Vector3.up * 0.1f,
                    Random.insideUnitSphere.WithY(0f) * Random.Range(1f, 2.5f) + Vector3.up * Random.Range(3f, 5.5f),
                    new Vector3(s, s * Random.Range(0.6f, 1f), s * Random.Range(0.8f, 1.2f)), Random.Range(1f, 1.4f),
                    new Color(0.5f * shade, 0.36f * shade, 0.22f * shade, 1f), Random.Range(100f, 400f));
            }
            Burst(p, 10, 0.6f, 1.4f);
            GoldBurst(p + Vector3.up * 0.4f, 12);
        }

        void GoldBurst(Vector3 p, int n)
        {
            glow.Emit(p, Vector3.zero, 1.6f, 0.25f, new Color(1f, 0.82f, 0.4f, 0.8f));
            for (int i = 0; i < n; i++)
                stars.Emit(p + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 1.5f + Vector3.up * Random.Range(1.5f, 3f),
                    Random.Range(0.35f, 0.65f), Random.Range(0.6f, 1.1f), new Color(1f, Random.Range(0.78f, 0.9f), 0.35f, 1f), Random.Range(-90f, 90f));
        }
    }
}
