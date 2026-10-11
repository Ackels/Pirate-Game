using System.Collections;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Sons de um personagem a pé (jogador ou pirata; posto pelo GameAudio): passos pelo tipo de chão (areia, grama,
    /// terra, pedra, madeira, água rasa), pulo, golpe e acerto do cutelo, tiro e recarga da pistola, sacar/guardar,
    /// dor e morte. <c>voicePitch</c> deixa a voz dos piratas mais grossa que a do capitão.
    /// </summary>
    [RequireComponent(typeof(FootCharacter))]
    public class CharacterAudio : MonoBehaviour
    {
        public enum Ground { Sand, Grass, Dirt, Rock, Wood, Water }

        GameSounds sounds;
        float voicePitch = 1f, volumeScale = 1f;
        FootCharacter character;
        CharacterCombat combat;
        Health health;
        Pirates.LandCombat.EnemyFootAI ai;
        float stride;
        bool wasReloading;

        public void Init(GameSounds gameSounds, float voice, float volume)
        {
            sounds = gameSounds;
            voicePitch = voice;
            volumeScale = volume;
        }

        void Awake()
        {
            character = GetComponent<FootCharacter>();
            TryGetComponent(out combat);
            TryGetComponent(out health);
            TryGetComponent(out ai);
        }

        void OnEnable()
        {
            character.Jumped += OnJumped;
            character.Dodged += OnDodged;
            character.KnockedDown += OnKnockedDown;
            if (combat)
            {
                combat.Attacked += OnAttacked;
                combat.Fired += OnFired;
                combat.Hit += OnHit;
                combat.KickLanded += OnKickLanded;
                combat.Blocked += OnBlocked;
                combat.GuardBroken += OnGuardBroken;
                combat.Drawing += OnDrawing;
                combat.Holstering += OnDrawing;
            }
            if (health)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
            if (ai)
            {
                ai.Taunted += OnTaunted;
                ai.Risen += OnRisen;
            }
        }

        void OnDisable()
        {
            character.Jumped -= OnJumped;
            character.Dodged -= OnDodged;
            character.KnockedDown -= OnKnockedDown;
            if (combat)
            {
                combat.Attacked -= OnAttacked;
                combat.Fired -= OnFired;
                combat.Hit -= OnHit;
                combat.KickLanded -= OnKickLanded;
                combat.Blocked -= OnBlocked;
                combat.GuardBroken -= OnGuardBroken;
                combat.Drawing -= OnDrawing;
                combat.Holstering -= OnDrawing;
            }
            if (health)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            if (ai)
            {
                ai.Taunted -= OnTaunted;
                ai.Risen -= OnRisen;
            }
        }

        void Update()
        {
            if (sounds == null) return;
            if (combat)
            {
                bool reloading = combat.ReloadAnimating;
                if (reloading && !wasReloading) Play(sounds.pistolReload, Hand());
                wasReloading = reloading;
            }
            Footsteps();
        }

        /// <summary>Um passo a cada passada (mais longa correndo), com o som do chão debaixo do pé.</summary>
        void Footsteps()
        {
            if (!character.IsGrounded || (health && health.IsDead))
            {
                stride = 0f;
                return;
            }
            float speed = character.PlanarSpeed;
            if (speed < 0.3f)
            {
                stride = 0.6f;   // primeiro passo logo que começa a andar
                return;
            }
            stride += speed * Time.deltaTime;
            float length = Mathf.Lerp(1.05f, 1.9f, Mathf.InverseLerp(1.5f, 6f, speed));
            if (stride < length) return;
            stride -= length;
            var cue = GroundAt() switch
            {
                Ground.Sand => sounds.stepSand,
                Ground.Grass => sounds.stepGrass,
                Ground.Rock => sounds.stepRock,
                Ground.Wood => sounds.stepWood,
                Ground.Water => sounds.stepWater,
                _ => sounds.stepDirt,
            };
            Play(cue, transform.position, Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(1.5f, 6f, speed)));
        }

        /// <summary>
        /// Chão debaixo do pé: na malha da ilha, pela cor da paleta (coluna e linha do quadradinho do colormap do Kenney);
        /// colisor que não é malha (píer, cais, convés) = madeira; pé na água rasa = água.
        /// </summary>
        Ground GroundAt()
        {
            if (character.InDeepWater || transform.position.y < 0.15f) return Ground.Water;
            var origin = transform.position + Vector3.up * 0.4f;
            RaycastHit ground = default;
            float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                ground = hit;
            }
            if (nearest == float.MaxValue) return Ground.Dirt;
            if (ground.collider is not MeshCollider) return Ground.Wood;
            Vector2 uv = ground.textureCoord;
            int column = Mathf.FloorToInt(uv.x * 16f);
            if (uv.y > 0.25f) return column <= 1 ? Ground.Sand : column <= 3 ? Ground.Grass : Ground.Dirt;
            return column >= 14 ? Ground.Dirt : column >= 2 && column <= 4 ? Ground.Rock : Ground.Dirt;
        }

        void OnJumped() => Play(sounds?.jump, transform.position);

        void OnDodged() => Play(sounds?.dodge, transform.position);

        void OnKickLanded(Vector3 point) => Play(sounds?.kick, point);

        /// <summary>Lâmina na guarda: metal batendo; aparar tem um tinido próprio, mais alto.</summary>
        void OnBlocked(Vector3 point, bool parried) => Play(parried ? sounds?.parry : sounds?.block, point);

        /// <summary>Guarda quebrada (sem fôlego): o tinido da defesa, mais grave, e o golpe que passou soa por cima.</summary>
        // Grito de guerra (brutamontes): o grito de morte, bem mais grave e alto.
        /// <summary>Esqueleto saindo da cova: gemido grave e arrastado (a voz de morte bem lenta).</summary>
        void OnRisen() => Play(sounds?.death, transform.position + Vector3.up * 0.6f, 1.2f, voicePitch * 0.55f);

        void OnTaunted() => Play(sounds?.death, transform.position + Vector3.up * 1.8f, 1.3f, voicePitch * 0.72f);

        void OnGuardBroken() => Play(sounds?.block, Hand(), 1f, 0.72f);

        void OnKnockedDown()
        {
            if (sounds != null && isActiveAndEnabled) StartCoroutine(Fall());
        }

        void OnAttacked(WeaponDefinition weapon, int combo)
        {
            if (weapon && weapon.kind == WeaponKind.Melee) Play(sounds?.swordSwing, Hand(), 1f, 1f + combo * 0.04f);
        }

        void OnFired(WeaponDefinition weapon, Vector3 muzzle, Vector3 end)
        {
            // Bacamarte (vários chumbos): o mesmo estampido, mais grave e mais forte.
            bool scatter = weapon && weapon.pellets > 1;
            Play(sounds?.pistolShot, muzzle, scatter ? 1.3f : 1f, scatter ? 0.72f : 1f);
        }

        void OnHit(WeaponDefinition weapon, Vector3 point)
        {
            if (weapon && weapon.kind == WeaponKind.Melee) Play(sounds?.swordHit, point);
        }

        void OnDrawing(WeaponDefinition weapon)
        {
            if (!weapon || sounds == null) return;
            Play(weapon.kind == WeaponKind.Melee ? sounds.drawSword : sounds.drawPistol, Hand());
        }

        void OnDamaged(DamageInfo info)
        {
            if (sounds == null || health.IsDead) return;
            Play(sounds.pain, transform.position + Vector3.up * 1.5f, 1f, voicePitch);
        }

        void OnDied()
        {
            if (sounds == null) return;
            Play(sounds.death, transform.position + Vector3.up * 1.5f, 1f, voicePitch);
            if (isActiveAndEnabled) StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            yield return new WaitForSeconds(0.7f);
            Play(sounds.bodyFall, transform.position);
        }

        Vector3 Hand() => transform.position + Vector3.up * 1.1f + transform.forward * 0.4f;

        void Play(SoundCue cue, Vector3 at, float volume = 1f, float pitch = 1f)
        {
            if (cue) SoundPlayer.At(cue, at, volume * volumeScale, pitch);
        }
    }
}
