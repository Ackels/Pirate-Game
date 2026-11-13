using System;
using Pirates.Combat;
using Pirates.Core;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Lázaro Negro no templo do alto da Ilha Amaldiçoada (pacote H, passo 3, 06/11). Escondido (desligado) até o cerco do altar ser
    /// quebrado (<see cref="AltarSiege"/>); aí se ergue de trás do altar (a mesma animação dos mortos que saem da terra, invencível) e luta
    /// com o <see cref="LandBoss"/> (o jeito do Barba-Negra). Na fase 2 a névoa chama mortos: um grupo de esqueletos
    /// (<see cref="CursedGraveyard"/>) sobe da terra em volta do templo e, de tempos em tempos, de novo se já caíram todos. Fora da luta
    /// (o capitão caiu ou fugiu), os mortos dele voltam para a terra e o <see cref="LandBoss"/> se cura. Cerco recomeçado (o capitão foi
    /// embora da ilha) = some de novo, e volta depois do próximo cerco.
    /// </summary>
    public class LazarusCurse : MonoBehaviour, ILockSource
    {
        /// <summary>
        /// O tesouro de Lázaro, atrás do altar (passo 4): destranca com Lázaro derrotado e tranca de novo quando ele some (o cerco
        /// recomeça), e então o baú esvazia e sorteia outra vez na próxima vitória. A Coroa de Prata vem nele enquanto o jogador não a tem.
        /// </summary>
        public bool IsLocked => !(shown && lazarus && lazarus.IsDead);
        public string LockedReason => shown ? "derrote Lázaro Negro" : "quebre o cerco do altar e derrote Lázaro Negro";

        [SerializeField] AltarSiege siege;
        [SerializeField] EnemyFootAI lazarus;
        [SerializeField] CursedGraveyard dead;
        [Tooltip("Segundos entre o cerco quebrado e Lázaro se erguer.")]
        [SerializeField] float appearDelay = 2.5f;
        [Tooltip("Sair da terra: início deitado (s do clipe), segundos até ficar de pé, animação (Erguer0..).")]
        [SerializeField] float riseStart = 0.9f, riseSeconds = 1.6f;
        [SerializeField] int riseVariant;
        [Tooltip("Fase 2: o primeiro chamado sai depois de firstCall s; os seguintes a cada callEvery s, se os mortos já caíram.")]
        [SerializeField] float firstCall = 1.5f, callEvery = 22f;
        [Tooltip("Fora da luta por estes segundos: os mortos voltam para a terra.")]
        [SerializeField] float buryAfter = 4f;

        LandBoss boss;
        CombatEffects effects;
        float appearAt = -1f, nextCall = float.MaxValue, idleSince = -1f;
        bool shown, forced;

        public static LazarusCurse Current { get; private set; }
        public bool IsShown => shown;
        public LandBoss Boss => boss;
        /// <summary>Lázaro se ergueu do altar.</summary>
        public static event Action<LazarusCurse> Appeared;
        /// <summary>A névoa chamou os mortos (fase 2).</summary>
        public static event Action<LazarusCurse> Called;

        public void Configure(AltarSiege altarSiege, EnemyFootAI boss, CursedGraveyard summoned, int variant, float start, float seconds)
        {
            siege = altarSiege;
            lazarus = boss;
            dead = summoned;
            riseVariant = variant;
            riseStart = start;
            riseSeconds = seconds;
        }

        void Awake()
        {
            Current = this;
            if (lazarus) boss = lazarus.GetComponent<LandBoss>();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void OnEnable()
        {
            if (siege) siege.Won += OnSiegeWon;
            LandBoss.Enraged += OnEnraged;
            LandBoss.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (siege) siege.Won -= OnSiegeWon;
            LandBoss.Enraged -= OnEnraged;
            LandBoss.Defeated -= OnDefeated;
        }

        void Start() => Hide();

        void OnSiegeWon() => appearAt = Time.time + appearDelay;

        void OnEnraged(LandBoss b)
        {
            if (b == boss) nextCall = Time.time + firstCall;
        }

        void OnDefeated(LandBoss b)
        {
            if (b != boss) return;
            nextCall = float.MaxValue;
            if (dead) dead.BuryNow();
        }

        void Update()
        {
            if (appearAt > 0f && Time.time >= appearAt)
            {
                appearAt = -1f;
                Appear();
            }
            if (!shown) return;
            // O cerco recomeçou (o capitão foi embora da ilha): Lázaro some até o próximo cerco.
            if (siege && !siege.IsWon && appearAt < 0f && !forced)
            {
                Hide();
                return;
            }
            if (!boss || !lazarus) return;
            bool fighting = boss.IsEngaged;
            if (!fighting && !lazarus.IsDead)
            {
                if (idleSince < 0f) idleSince = Time.time;
                if (Time.time - idleSince >= buryAfter && dead && dead.IsRisen) dead.BuryNow();
                nextCall = boss.Phase >= 2 ? Time.time + firstCall : float.MaxValue;
                return;
            }
            idleSince = -1f;
            if (fighting && boss.Phase >= 2 && Time.time >= nextCall && dead && (!dead.IsRisen || dead.AllDown)) Call();
        }

        void Appear()
        {
            if (!lazarus) return;
            shown = true;
            idleSince = -1f;
            nextCall = float.MaxValue;
            lazarus.gameObject.SetActive(true);
            lazarus.ResetToPost();
            if (boss) boss.ResetFightNow();
            if (lazarus.Camp) lazarus.Camp.WakeUp();
            lazarus.Rise(riseVariant, riseStart, riseSeconds);
            if (!effects) Services.TryGet(out effects);
            if (effects)
            {
                effects.Unearth(lazarus.transform.position, true);
                effects.Roar(lazarus.transform.position);
            }
            Appeared?.Invoke(this);
        }

        void Call()
        {
            nextCall = Time.time + callEvery;
            dead.BuryNow();
            dead.RiseNow();
            Called?.Invoke(this);
        }

        void Hide()
        {
            shown = false;
            forced = false;
            appearAt = -1f;
            nextCall = float.MaxValue;
            if (dead) dead.BuryNow();
            if (lazarus) lazarus.gameObject.SetActive(false);
        }

        /// <summary>Testes e sandbox: Lázaro se ergue agora (sem o cerco) ou volta a sumir.</summary>
        public void AppearNow()
        {
            forced = true;
            Appear();
        }

        public void HideNow() => Hide();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Appeared = null;
            Called = null;
        }
    }
}
