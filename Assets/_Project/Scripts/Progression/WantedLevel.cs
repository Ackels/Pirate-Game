using System;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Nível de procurado pela Coroa (G, passo 2, 03/11; plano de 16/10: atacar mercante = pirataria). Atacar um mercante soma
    /// <see cref="attackHeat"/> e afundá-lo <see cref="sinkHeat"/> à procura (<see cref="Heat"/>); as estrelas (0–5) saem dos limites
    /// em <see cref="starAt"/>. Sem crime há <see cref="calmSeconds"/>, a procura cai <see cref="decayPerMinute"/> por minuto de jogo
    /// (parado no menu ou na pausa não anda). O Almirante Teodoro perdoa por uma multa (<see cref="Fine"/>, pela conversa: NpcTalker).
    /// Piratas da Âncora Negra continuam livres: só os mercantes contam. A Esquadra Real (passo 3) vai caçar pelas estrelas.
    /// </summary>
    public class WantedLevel : MonoBehaviour, IWanted
    {
        [SerializeField] ItemDefinition gold;
        [Tooltip("Procura somada no primeiro tiro num mercante (uma vez por viagem dele).")]
        [SerializeField, Min(0f)] float attackHeat = 12f;
        [Tooltip("Procura somada ao afundar um mercante.")]
        [SerializeField, Min(0f)] float sinkHeat = 30f;
        [Tooltip("Procura somada ao afundar um navio da Esquadra Real (passo 3).")]
        [SerializeField, Min(0f)] float crownSinkHeat = 20f;
        [Tooltip("Procura mínima para 1, 2, 3, 4 e 5 estrelas.")]
        [SerializeField] float[] starAt = { 10f, 35f, 70f, 120f, 180f };
        [Tooltip("Teto da procura.")]
        [SerializeField, Min(1f)] float maxHeat = 240f;
        [Tooltip("Segundos de jogo sem crime antes de a procura começar a cair.")]
        [SerializeField, Min(0f)] float calmSeconds = 120f;
        [Tooltip("Quanto a procura cai por minuto de jogo, depois da calma.")]
        [SerializeField, Min(0f)] float decayPerMinute = 6f;
        [Tooltip("A queda é multiplicada por isto com a Esquadra Real sem a sua pista (despistou).")]
        [SerializeField, Min(1f)] float lostTrailDecay = 2f;
        [Tooltip("Multa do perdão: base + por estrela.")]
        [SerializeField, Min(0)] int fineBase = 50;
        [SerializeField, Min(0)] int finePerStar = 150;

        float heat, sinceCrime;

        public static WantedLevel Current { get; private set; }

        public float Heat => heat;
        public float SecondsSinceCrime => sinceCrime;
        public int Stars { get; private set; }
        public int MaxStars => starAt.Length;
        public int Fine => Stars > 0 ? fineBase + finePerStar * Stars : 0;
        public int GoldOwned => gold ? PlayerStorage.Count(gold) : 0;
        /// <summary>Segundos de jogo até perder uma estrela (0 sem estrelas).</summary>
        public float SecondsToNextDrop
        {
            get
            {
                if (Stars <= 0 || decayPerMinute <= 0f) return 0f;
                float wait = Mathf.Max(0f, calmSeconds - sinceCrime);
                float below = Stars - 1 < 0 ? 0f : starAt[Stars - 1];
                var navy = RoyalNavy.Current;
                float rate = decayPerMinute * (navy && navy.IsTrailLost ? lostTrailDecay : 1f);
                return wait + (heat - below) / rate * 60f;
            }
        }

        /// <summary>Estrelas mudaram (antes, agora).</summary>
        public event Action<int, int> StarsChanged;
        /// <summary>Multa paga (ouro).</summary>
        public event Action<int> Pardoned;

        public void Configure(ItemDefinition goldItem) => gold = goldItem;

        void Awake() => Current = this;

        void OnEnable()
        {
            Services.Register<IWanted>(this);
            MerchantShip.AttackedByPlayer += OnMerchantAttacked;
            MerchantShip.Sunk += OnMerchantSunk;
            RoyalNavy.ShipSunk += OnCrownSunk;
        }

        void OnDisable()
        {
            Services.Unregister<IWanted>(this);
            MerchantShip.AttackedByPlayer -= OnMerchantAttacked;
            MerchantShip.Sunk -= OnMerchantSunk;
            RoyalNavy.ShipSunk -= OnCrownSunk;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (heat <= 0f) return;
            sinceCrime += Time.deltaTime;
            if (sinceCrime < calmSeconds) return;
            // Despistar (04/11): com a Esquadra Real te vendo, a procura não cai; com a pista perdida, cai mais rápido.
            var navy = RoyalNavy.Current;
            if (navy && navy.SeesPlayer) return;
            float rate = decayPerMinute * (navy && navy.IsTrailLost ? lostTrailDecay : 1f);
            Set(heat - rate / 60f * Time.deltaTime);
        }

        void OnMerchantAttacked(MerchantShip ship) => Crime(attackHeat);

        void OnMerchantSunk(MerchantShip ship, bool byPlayer)
        {
            if (byPlayer) Crime(sinkHeat);
        }

        void OnCrownSunk(GameObject ship, bool byPlayer)
        {
            if (byPlayer) Crime(crownSinkHeat);
        }

        /// <summary>Um crime contra a Coroa (também o F1).</summary>
        public void Crime(float amount)
        {
            sinceCrime = 0f;
            Set(heat + amount);
        }

        public bool PayFine()
        {
            int fine = Fine;
            if (fine <= 0 || !gold || GoldOwned < fine) return false;
            if (PlayerStorage.Take(gold, fine) < fine) return false;
            // Zera sem o aviso de "a Coroa esqueceu" (o do perdão diz isso).
            heat = 0f;
            Stars = 0;
            StarsChanged?.Invoke(-1, 0);
            Pardoned?.Invoke(fine);
            return true;
        }

        public void Restore(float savedHeat, float secondsSinceCrime)
        {
            sinceCrime = Mathf.Max(0f, secondsSinceCrime);
            int before = Stars;
            heat = Mathf.Clamp(savedHeat, 0f, maxHeat);
            Stars = StarsFor(heat);
            // Carregar não avisa ("procurado!") de novo: só atualiza.
            if (Stars != before) StarsChanged?.Invoke(-1, Stars);
        }

        /// <summary>Sandbox (F1): uma estrela a mais (até o máximo) ou a menos.</summary>
        public void AddStar(int delta)
        {
            int target = Mathf.Clamp(Stars + delta, 0, starAt.Length);
            if (target == 0) Set(0f);
            else if (delta > 0) Crime(Mathf.Max(0f, starAt[target - 1] + 1f - heat));
            else Set(starAt[target - 1]);
        }

        void Set(float value)
        {
            heat = Mathf.Clamp(value, 0f, maxHeat);
            int stars = StarsFor(heat);
            if (stars == Stars) return;
            int before = Stars;
            Stars = stars;
            StarsChanged?.Invoke(before, stars);
        }

        int StarsFor(float value)
        {
            int stars = 0;
            for (int i = 0; i < starAt.Length; i++)
                if (value >= starAt[i]) stars = i + 1;
            return stars;
        }
    }
}
