using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Peso do impacto do capitão (revisão das animações, passo 5, 21/10): parada de impacto (<see cref="HitStop"/>) e tremida de câmera
    /// (<see cref="ICameraShake"/>) quando o golpe dele acerta (maior na finalização, no golpe pesado e no contra-ataque), quando apara,
    /// quando a guarda segura um golpe, quando leva golpe e quando cai. Só o capitão do jogador: os golpes da IA entre si não param o jogo.
    /// </summary>
    [RequireComponent(typeof(CharacterCombat))]
    public class ImpactFeel : MonoBehaviour
    {
        [System.Serializable]
        public struct Feel
        {
            [Tooltip("Segundos de parada (tempo real) e tremida (m, segundos).")]
            public float stop, shake, shakeSeconds;
            public Feel(float stop, float shake, float shakeSeconds)
            {
                this.stop = stop;
                this.shake = shake;
                this.shakeSeconds = shakeSeconds;
            }
        }

        [SerializeField] Feel hit = new(0.045f, 0.08f, 0.12f);
        [Tooltip("Finalização do combo, golpe que derruba, contra-ataque depois de aparar.")]
        [SerializeField] Feel heavyHit = new(0.085f, 0.2f, 0.2f);
        [SerializeField] Feel ranged = new(0f, 0.06f, 0.1f);
        [SerializeField] Feel parry = new(0.12f, 0.18f, 0.2f);
        [Tooltip("Golpe segurado na guarda.")]
        [SerializeField] Feel guarded = new(0.03f, 0.08f, 0.12f);
        [SerializeField] Feel hurt = new(0.055f, 0.25f, 0.2f);
        [SerializeField] Feel knockedDown = new(0f, 0.3f, 0.25f);
        [Tooltip("Velocidade do jogo durante a parada (fração).")]
        [SerializeField, Range(0f, 0.5f)] float stopScale = 0.05f;

        CharacterCombat combat;
        Health health;
        FootCharacter character;

        void Awake()
        {
            combat = GetComponent<CharacterCombat>();
            health = GetComponent<Health>();
            character = GetComponent<FootCharacter>();
        }

        void OnEnable()
        {
            CharacterCombat.Struck += OnStruck;
            combat.Blocked += OnBlocked;
            if (health) health.Damaged += OnHurt;
            if (character) character.KnockedDown += OnKnockedDown;
        }

        void OnDisable()
        {
            CharacterCombat.Struck -= OnStruck;
            combat.Blocked -= OnBlocked;
            if (health) health.Damaged -= OnHurt;
            if (character) character.KnockedDown -= OnKnockedDown;
        }

        void OnStruck(CharacterCombat.StrikeReport report)
        {
            if (report.attacker != combat) return;
            var flags = report.flags;
            if ((flags & CharacterCombat.StrikeFlags.Blocked) != 0) Play(guarded);
            else if ((flags & CharacterCombat.StrikeFlags.Ranged) != 0) Play(ranged);
            else if ((flags & (CharacterCombat.StrikeFlags.Heavy | CharacterCombat.StrikeFlags.Finisher | CharacterCombat.StrikeFlags.Riposte)) != 0) Play(heavyHit);
            else Play(hit);
        }

        void OnBlocked(Vector3 point, bool parried) => Play(parried ? parry : guarded);

        void OnHurt(DamageInfo info)
        {
            if (info.blocked || info.amount <= 0f) return;
            Play(hurt);
        }

        void OnKnockedDown() => Play(knockedDown);

        void Play(in Feel feel)
        {
            if (feel.stop > 0f) HitStop.Request(feel.stop, stopScale);
            if (feel.shake > 0f && Services.TryGet(out ICameraShake shake)) shake.Shake(feel.shake, feel.shakeSeconds);
        }
    }
}
