using Pirates.Combat;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Sons de um navio (posto em cada navio pelo GameAudio): velas subindo/descendo, madeira rangendo de tempos em
    /// tempos (mais com vento e balanço) e o casco se partindo ao afundar. Canhões e impactos vêm do CombatEffects.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class ShipAudio : MonoBehaviour
    {
        GameSounds sounds;
        ShipController ship;
        Health health;
        float nextCreak;

        public void Init(GameSounds gameSounds)
        {
            sounds = gameSounds;
            nextCreak = Time.time + Random.Range(2f, 8f);
        }

        void Awake()
        {
            ship = GetComponent<ShipController>();
            TryGetComponent(out health);
        }

        void OnEnable()
        {
            ship.SailChanged += OnSailChanged;
            if (health) health.Died += OnDied;
        }

        void OnDisable()
        {
            ship.SailChanged -= OnSailChanged;
            if (health) health.Died -= OnDied;
        }

        void OnSailChanged(int level)
        {
            if (sounds == null) return;
            SoundPlayer.At(sounds.sails, transform.position + Vector3.up * 4f);
        }

        void OnDied()
        {
            if (sounds == null) return;
            SoundPlayer.At(sounds.shipSinking, transform.position);
            SoundPlayer.At(sounds.cannonImpact, transform.position + Vector3.up, 1f, 0.7f);
        }

        void Update()
        {
            if (sounds == null || !sounds.shipCreak || Time.time < nextCreak || (health && health.IsDead)) return;
            // Rangido mais frequente com o navio andando.
            float speed01 = Mathf.Clamp01(ship.SpeedKnots / 10f);
            nextCreak = Time.time + Random.Range(5f, 11f) * Mathf.Lerp(1.2f, 0.6f, speed01);
            var ear = ListenerAnchor.Current;
            if (ear && (ear.transform.position - transform.position).sqrMagnitude > sounds.shipCreak.maxDistance * sounds.shipCreak.maxDistance) return;
            SoundPlayer.At(sounds.shipCreak, transform.position + Vector3.up * 1.5f, Mathf.Lerp(0.6f, 1f, speed01));
        }
    }
}
