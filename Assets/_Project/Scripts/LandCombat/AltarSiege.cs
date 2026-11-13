using System;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Cerco do altar amaldiçoado (pacote H, passo 2, 06/11; Ilha Amaldiçoada, cena 07). O capitão a pé chega ao altar no alto da ilha e os
    /// mortos sobem da terra em ondas: em cada onda, os esqueletos de um <see cref="CursedGraveyard"/> (a qualquer hora, sem publicar) e,
    /// um pouco depois, as feras (<see cref="CursedBeast"/>). Onda limpa = todos caídos de vez (pólvora ou a Lâmina de São Telmo fazem cair
    /// na primeira); a próxima vem depois de <see cref="waveDelay"/>. Quebrado o cerco: o feito <see cref="siegeId"/> (História) e a fama.
    /// Capitão caído, embarcado ou longe do altar no meio do cerco: os mortos voltam para a terra e o cerco recomeça na próxima vez. Vencido,
    /// fica quieto até o capitão ir embora (embarcar ou se afastar).
    /// </summary>
    public class AltarSiege : MonoBehaviour, ISiegeCooldown
    {
        [Serializable]
        public struct Wave
        {
            public CursedGraveyard dead;
            public CursedBeast[] beasts;
        }

        enum State { Idle, Between, Fighting, Won }

        [SerializeField] FootCharacter player;
        [SerializeField] string siegeId = "altar_amaldicoado";
        [SerializeField] int fame = 40;
        [Tooltip("Capitão a pé mais perto que isto (m) do altar: o cerco começa.")]
        [SerializeField] float triggerRadius = 6f;
        [Tooltip("Mais longe que isto (m) do altar no meio do cerco: largou (os mortos voltam para a terra).")]
        [SerializeField] float leaveRadius = 34f;
        [Tooltip("Vencido: mais longe que isto (m), ou embarcado, o altar desperta de novo na próxima vez.")]
        [SerializeField] float resetRadius = 140f;
        [SerializeField] float firstDelay = 2f, waveDelay = 4f, beastDelay = 1.4f;
        [Tooltip("Depois de vencido, o altar só desperta de novo depois disto (s de jogo; 30 min). Não é salvo.")]
        [SerializeField] float cooldownSeconds = 1800f;
        [SerializeField] Wave[] waves = Array.Empty<Wave>();

        State state;
        int wave = -1;
        float timer;
        float cooldownUntil;
        bool beastsOut;
        Health playerHealth;

        public static AltarSiege Current { get; private set; }
        public string SiegeId => siegeId;
        public int WaveCount => waves.Length;
        /// <summary>Onda em andamento (0 em diante; −1 = parado).</summary>
        public int WaveIndex => state is State.Between or State.Fighting ? wave : -1;
        public bool IsActive => state is State.Between or State.Fighting;
        public bool IsWon => state == State.Won;
        /// <summary>Segundos até a próxima onda subir (0 = lutando ou parado).</summary>
        public float NextWaveIn => state == State.Between ? Mathf.Max(0f, timer - Time.time) : 0f;

        /// <summary>O cerco começou (o altar despertou).</summary>
        public event Action Started;
        /// <summary>Uma onda subiu (índice, total).</summary>
        public event Action<int, int> WaveRose;
        /// <summary>Largado (o capitão caiu, embarcou ou fugiu).</summary>
        public event Action Failed;
        public event Action Won;

        public void Configure(FootCharacter captain, Wave[] siegeWaves, int fameReward)
        {
            player = captain;
            waves = siegeWaves;
            fame = fameReward;
        }

        void Awake()
        {
            Current = this;
            Services.Register<ISiegeCooldown>(this);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Services.Unregister<ISiegeCooldown>(this);
        }

        public void Restore(float secondsLeft) => cooldownUntil = secondsLeft > 0f ? Time.time + secondsLeft : 0f;

        void Start() => BuryAll();

        void Update()
        {
            if (!player) return;
            if (!playerHealth) playerHealth = player.GetComponent<Health>();
            bool present = player.gameObject.activeInHierarchy && (!playerHealth || !playerHealth.IsDead);
            float distance = present ? Flat(player.transform.position - transform.position) : float.MaxValue;

            switch (state)
            {
                case State.Idle:
                    if (distance <= triggerRadius && Time.time >= cooldownUntil) Begin();
                    break;
                case State.Between:
                case State.Fighting:
                    if (distance > leaveRadius)
                    {
                        Fail();
                        return;
                    }
                    if (state == State.Between)
                    {
                        if (Time.time >= timer) RiseWave();
                        return;
                    }
                    if (!beastsOut && Time.time >= timer) ReleaseBeasts();
                    if (beastsOut && WaveDone()) NextOrWin();
                    break;
                case State.Won:
                    if (distance > resetRadius)
                    {
                        BuryAll();
                        state = State.Idle;
                    }
                    break;
            }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void Begin()
        {
            state = State.Between;
            wave = 0;
            timer = Time.time + firstDelay;
            Started?.Invoke();
        }

        void RiseWave()
        {
            state = State.Fighting;
            var w = waves[wave];
            if (w.dead) w.dead.RiseNow();
            beastsOut = false;
            timer = Time.time + beastDelay;
            WaveRose?.Invoke(wave, waves.Length);
        }

        void ReleaseBeasts()
        {
            beastsOut = true;
            var w = waves[wave];
            if (w.beasts == null) return;
            foreach (var b in w.beasts)
                if (b) b.Emerge(player);
        }

        bool WaveDone()
        {
            var w = waves[wave];
            if (w.dead && !w.dead.AllDown) return false;
            if (w.beasts != null)
                foreach (var b in w.beasts)
                    if (b && !b.IsGone && !(b.IsDead && !b.IsDownForNow)) return false;
            return true;
        }

        void NextOrWin()
        {
            wave++;
            if (wave < waves.Length)
            {
                state = State.Between;
                timer = Time.time + waveDelay;
                return;
            }
            state = State.Won;
            cooldownUntil = Time.time + cooldownSeconds;
            EventBus.Publish(new EnemyDefeatedEvent { targetId = siegeId, fame = fame });
            Won?.Invoke();
        }

        void Fail()
        {
            BuryAll();
            state = State.Idle;
            Failed?.Invoke();
        }

        void BuryAll()
        {
            wave = -1;
            beastsOut = false;
            foreach (var w in waves)
            {
                if (w.dead) w.dead.BuryNow();
                if (w.beasts == null) continue;
                foreach (var b in w.beasts)
                    if (b) b.Vanish();
            }
        }

        /// <summary>Mortos de pé agora (esqueletos fora da terra e feras na luta).</summary>
        public int Standing
        {
            get
            {
                if (wave < 0 || wave >= waves.Length) return 0;
                var w = waves[wave];
                int n = w.dead ? w.dead.Standing : 0;
                if (w.beasts != null)
                    foreach (var b in w.beasts)
                        if (b && !b.IsGone && !b.IsDead) n++;
                return n;
            }
        }

        /// <summary>Testes e sandbox: começa agora, pula a onda em andamento (derruba todos de vez) ou volta tudo para a terra.</summary>
        /// <summary>Segundos até o altar poder despertar de novo depois de vencido (0 = pronto).</summary>
        public float CooldownLeft => Mathf.Max(0f, cooldownUntil - Time.time);

        public void BeginNow()
        {
            if (state == State.Idle || state == State.Won) Begin();
        }

        public void ClearWaveNow()
        {
            if (state != State.Fighting) return;
            if (!beastsOut) ReleaseBeasts();
            var w = waves[wave];
            var kill = new DamageInfo { amount = 99999f, ignites = true };
            if (w.dead)
                foreach (var m in w.dead.Camp.Members)
                    if (m && m.gameObject.activeInHierarchy && !m.IsDead)
                    {
                        m.Health.Invulnerable = false;
                        m.Health.TakeDamage(kill);
                    }
            if (w.beasts != null)
                foreach (var b in w.beasts)
                    if (b && !b.IsGone && !b.IsDead)
                    {
                        b.Health.Invulnerable = false;
                        b.Health.TakeDamage(kill);
                    }
        }

        public void ResetNow()
        {
            BuryAll();
            state = State.Idle;
        }
    }
}
