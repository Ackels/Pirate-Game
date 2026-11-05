using System;
using System.Collections;
using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.OnFoot;
using Pirates.Progression;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Abordagem (04/11, passo 1): com um navio inimigo avariado (casco até <see cref="maxHull"/>) colado ao seu (até <see cref="range"/>)
    /// e o seu navio devagar, F "Lançar ganchos": a tela escurece, os dois navios ficam presos e parados no mar, e o capitão aparece no
    /// palco (<see cref="BoardingStage"/>), os dois conveses grandes ligados por uma prancha. F perto do ponto de chegada volta ao navio.
    /// Por enquanto só as chalupas piratas comuns (sem alvo de missão, sem chefe, sem a Coroa); ainda sem inimigos no convés.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class ShipBoarding : MonoBehaviour, IInteraction
    {
        public const string SloopKind = "chalupa", BrigKind = "brigue", TormentaKind = "tormenta", HunterKind = "cacador",
            CorvetteKind = "coroa_corveta", FrigateKind = "coroa_fragata";
        /// <summary>Ids de missão dos navios com nome (os mesmos do gerador e das missões).</summary>
        public const string BrigTargetId = "brigue_santa_clara", TormentaTargetId = "galeao_tormenta";

        /// <summary>O que muda por tipo de navio abordado (passo 4: o brigue Santa Clara; depois o Tormenta, caçadores e a Esquadra Real).</summary>
        [Serializable]
        public class Kind
        {
            public string id;
            [Tooltip("Nome na faixa e nos avisos (\"Brigue Santa Clara\").")]
            public string label;
            [Tooltip("Com artigo, nos textos (\"a chalupa\", \"o brigue\").")]
            public string noun;
            [Tooltip("Piratas no convés; a metralha recente tira até maxCrewLoss.")]
            public int crew = 7, maxCrewLoss = 2;
            [Tooltip("Fama pela abordagem vencida (afundar o navio tomado depois dá 0).")]
            public int fame = 10;
            [Tooltip("Soma a fama do navio (EnemyShipAI.Fame) à de cima: caçadores e navios da Coroa variam pela força.")]
            public bool fameFromShip;
            public Vector2Int recruits = new(2, 3);
            [Range(0f, 1f)] public float prisonerChance = 0.25f;
            [Tooltip("Dá para recrutar os rendidos (a tripulação amaldiçoada do Tormenta não).")]
            public bool canRecruit = true;
            [Tooltip("Frase a mais na rendição e no aviso (o Lázaro fugindo do Tormenta).")]
            public string note;
        }

        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] BoardingStage stage;
        [Tooltip("Distância (m, centro a centro) para lançar os ganchos.")]
        [SerializeField] float range = 24f;
        [Tooltip("A partir de quanto o aviso mostra o que falta (× o alcance).")]
        [SerializeField] float noticeRange = 2.2f;
        [Tooltip("Velocidade máxima (nós) do seu navio para lançar.")]
        [SerializeField] float maxSpeedKnots = 4f;
        [Tooltip("Casco do inimigo até esta fração para dar para abordar.")]
        [SerializeField, Range(0f, 1f)] float maxHull = 0.5f;
        [SerializeField] Kind[] kinds =
        {
            new() { id = SloopKind, label = "Chalupa pirata", noun = "a chalupa", crew = 7, maxCrewLoss = 2, fame = 10 },
            // Brigue (25 de fama afundado): 10 a bordo com o mestre brutamontes; muitos marujos da Coroa presos no porão.
            new()
            {
                id = BrigKind, label = "Brigue Santa Clara", noun = "o brigue", crew = 10, maxCrewLoss = 3, fame = 35,
                recruits = new Vector2Int(3, 4), prisonerChance = 0.6f,
            },
            // Galeão Tormenta (80 de fama afundado): a tripulação amaldiçoada (esqueletos) e o imediato como chefe; o Lázaro foge no bote.
            new()
            {
                id = TormentaKind, label = "Galeão Tormenta", noun = "o Tormenta", crew = 10, maxCrewLoss = 3, fame = 100,
                recruits = new Vector2Int(0, 0), prisonerChance = 0.5f, canRecruit = false,
                note = "Lázaro Negro pulou num bote e fugiu na névoa, jurando vingança.",
            },
            // Caçador de recompensas (passo 6): mercenários do KayKit e o caçador-chefe, que luta como a duelista.
            new()
            {
                id = HunterKind, label = "Caçador de recompensas", noun = "o caçador", crew = 10, maxCrewLoss = 2, fame = 15, fameFromShip = true,
                recruits = new Vector2Int(2, 3), prisonerChance = 0.35f,
                note = "No camarote do caçador, um cartaz com o seu rosto: \"procurado, vivo ou morto\".",
            },
            // Esquadra Real (passo 6): fuzileiros cavaleiros e o oficial (sabre, como a Guarda Negra). Afundar depois = crime contra a Coroa.
            new()
            {
                id = CorvetteKind, label = "Corveta da Coroa", noun = "a corveta", crew = 8, maxCrewLoss = 2, fame = 10, fameFromShip = true,
                recruits = new Vector2Int(1, 2), prisonerChance = 0.3f,
                note = "Tomar um navio da Coroa é crime: a procura sobe quando ele afundar.",
            },
            new()
            {
                id = FrigateKind, label = "Navio de guerra da Coroa", noun = "o navio da Coroa", crew = 10, maxCrewLoss = 3, fame = 10, fameFromShip = true,
                recruits = new Vector2Int(1, 2), prisonerChance = 0.3f,
                note = "Tomar um navio da Coroa é crime: a procura sobe quando ele afundar.",
            },
        };
        [Tooltip("Navio tomado e largado à deriva: segundos até afundar sozinho (o respawner traz outro).")]
        [SerializeField] float adriftSeconds = 90f;
        [Header("Rendição (passo 3)")]
        [Tooltip("Saquear o porão: a carga da tabela do navio × isto, e o baú com a chance × chestBonus.")]
        [SerializeField] float plunderCargo = 1.6f, plunderChestBonus = 1.5f;
        [Tooltip("Recrutar os rendidos: a carga × isto (eles levam o resto embora no bote; quantos vêm é do tipo do navio).")]
        [SerializeField] float recruitCargo = 0.5f;
        [Tooltip("Prisioneiro no porão (a chance é do tipo do navio): vira tripulante, com um agradecimento em ouro.")]
        [SerializeField] int prisonerGold = 30;
        [Tooltip("Caído no convés inimigo: segundos até voltar ao navio.")]
        [SerializeField] float fallenSeconds = 2f;
        [Tooltip("Escurecer, segurar no preto e clarear (s).")]
        [SerializeField] float fadeOutSeconds = 0.6f, holdSeconds = 0.35f, fadeInSeconds = 0.7f;

        ShipController ship;
        Rigidbody body;
        PlayerShipClass shipClass;
        Coroutine travel;
        EnemyShipAI target;
        Rigidbody targetBody;
        bool targetAiWasOn;
        bool rewarded;
        Inventory hold;
        ShipCrew crew;
        readonly List<ItemStack> leftover = new();
        Health captainHealth;

        /// <summary>O inimigo mais perto que dá (ou daria) para abordar.</summary>
        public EnemyShipAI Candidate { get; private set; }
        /// <summary>O que falta para abordar o <see cref="Candidate"/> (null = pode).</summary>
        public string BlockReason { get; private set; }
        public float CandidateDistance { get; private set; }
        /// <summary>O casco do <see cref="Candidate"/> já está avariado o bastante.</summary>
        public bool CandidateDamaged { get; private set; }
        /// <summary>O capitão está no palco da abordagem.</summary>
        public bool Active { get; private set; }
        public EnemyShipAI Target => target;
        /// <summary>Tipo do navio abordado agora (ou do candidato, fora da abordagem).</summary>
        public Kind TargetKind => FindKind(target ? KindOf(target) : Candidate ? KindOf(Candidate) : null) ?? kinds[0];
        public Kind CandidateKind => Candidate ? FindKind(KindOf(Candidate)) : null;
        public float AdriftSeconds => adriftSeconds;
        public BoardingStage Stage => stage;
        public bool IsTraveling => travel != null;
        /// <summary>0 = tela normal, 1 = preta.</summary>
        public float Fade { get; private set; }
        public float Range => range;

        public bool IsOpen => false;
        public bool CanOpen => !Active && !IsTraveling && Candidate && BlockReason == null && switcher && !switcher.IsOnFoot;

        /// <summary>Chegou ao palco (o nome do navio inimigo para a faixa).</summary>
        public event Action<string> Boarded;
        public event Action Returned;
        /// <summary>Tripulação rendida (passo 3): a escolha da recompensa está aberta.</summary>
        public bool AwaitingChoice => Active && !IsTraveling && stage && stage.CrewDefeated && !rewarded;
        /// <summary>Rendidos que dá para recrutar (sorteados na rendição) e quantos cabem nos beliches agora.</summary>
        public int Surrendered { get; private set; }
        public int RecruitsThatFit => TargetKind.canRecruit ? Mathf.Min(Surrendered, crew ? crew.FreeBerths : 0) : 0;
        public bool HasPrisoner { get; private set; }
        /// <summary>Recompensa escolhida: linhas do que entrou no porão (para o HUD).</summary>
        public event Action<string, List<ItemStack>, int, bool> Rewarded;

        /// <summary>O capitão caiu no convés inimigo: volta ao navio e a chalupa se solta (a tripulação dela fica como estava).</summary>
        public event Action Repelled;

        public void Configure(PlayerModeSwitcher modeSwitcher, BoardingStage boardingStage)
        {
            switcher = modeSwitcher;
            stage = boardingStage;
        }

        void Awake()
        {
            ship = GetComponent<ShipController>();
            body = GetComponent<Rigidbody>();
            shipClass = GetComponent<PlayerShipClass>();
            hold = GetComponent<Inventory>();
            crew = GetComponent<ShipCrew>();
            if (switcher && switcher.Character) captainHealth = switcher.Character.GetComponent<Health>();
        }

        void OnEnable()
        {
            if (captainHealth) captainHealth.Died += OnCaptainDied;
            if (stage) stage.Defeated += OnCrewSurrendered;
        }

        void OnDisable()
        {
            if (captainHealth) captainHealth.Died -= OnCaptainDied;
            if (stage) stage.Defeated -= OnCrewSurrendered;
        }

        void OnCrewSurrendered()
        {
            var kind = TargetKind;
            Surrendered = UnityEngine.Random.Range(kind.recruits.x, kind.recruits.y + 1);
            HasPrisoner = UnityEngine.Random.value < kind.prisonerChance;
        }

        public enum Reward { Plunder, Recruit }

        /// <summary>
        /// A escolha da rendição. Saquear: a carga do navio (mais que nos destroços) e o baú com mais chance, direto no seu porão. Recrutar:
        /// os rendidos nos beliches livres e metade da carga. Nos dois, o prisioneiro (se houver) e a fama; o navio fica marcado como
        /// saqueado (afundado, só madeira). O que não cabe no porão boia ao lado do seu navio quando você volta.
        /// </summary>
        public bool Choose(Reward reward)
        {
            if (!AwaitingChoice || !hold) return false;
            rewarded = true;
            var table = target && target.TryGetComponent(out LootDropper dropper) ? dropper.Table : null;
            var cargo = new List<ItemStack>();
            if (table)
            {
                float scale = reward == Reward.Plunder ? plunderCargo : recruitCargo;
                foreach (var s in table.RollCargo())
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(s.amount * scale));
                    cargo.Add(new ItemStack(s.item, n));
                }
                float chest = table.chestChance * (reward == Reward.Plunder ? plunderChestBonus : 1f);
                if (UnityEngine.Random.value < chest)
                {
                    var prize = table.RollSchematic(hold);
                    if (prize.item) cargo.Add(prize);
                    var gear = table.RollGear(hold);
                    if (gear.item) cargo.Add(gear);
                }
            }
            int recruited = 0;
            if (reward == Reward.Recruit && crew && crew.CrewItem)
            {
                recruited = hold.Add(crew.CrewItem, RecruitsThatFit);
            }
            if (HasPrisoner && crew && crew.CrewItem && crew.FreeBerths > 0) recruited += hold.Add(crew.CrewItem, 1);
            if (HasPrisoner && table && table.chestFallback.item) cargo.Add(new ItemStack(table.chestFallback.item, prisonerGold));
            // Junta o que se repete (o ouro do prisioneiro com o da carga).
            var merged = new List<ItemStack>();
            foreach (var s in cargo)
            {
                int k = merged.FindIndex(m => m.item == s.item);
                if (k >= 0) merged[k] = new ItemStack(s.item, merged[k].amount + s.amount);
                else merged.Add(s);
            }
            leftover.Clear();
            foreach (var s in merged)
            {
                int put = hold.Add(s.item, Mathf.Min(s.amount, hold.MaxFit(s.item)));
                if (put < s.amount) leftover.Add(new ItemStack(s.item, s.amount - put));
            }
            if (target && target.TryGetComponent(out LootDropper loot)) loot.MarkPlundered();
            int fame = TargetKind.fame + (TargetKind.fameFromShip && target ? target.Fame : 0);
            if (fame > 0 && CaptainFame.Current) CaptainFame.Current.Add(fame, "Abordagem: " + TargetKind.label);
            Rewarded?.Invoke(reward == Reward.Plunder ? "Porão saqueado" : "Rendidos recrutados", merged, recruited, leftover.Count > 0);
            return true;
        }

        // Caiu na abordagem (04/11): sem isto, acordava no próprio convés com os piratas ainda em alerta e os marujos caídos, e morria
        // de novo. O PlayerFootRespawn (no objeto do switcher, sempre ligado) revive o capitão no navio.
        void OnCaptainDied()
        {
            if (Active && !IsTraveling) StartCoroutine(Fallen());
        }

        IEnumerator Fallen()
        {
            yield return new WaitForSeconds(fallenSeconds);
            if (!Active || IsTraveling) yield break;
            Repelled?.Invoke();
            travel = StartCoroutine(Leave());
        }

        void Update()
        {
            Candidate = null;
            BlockReason = null;
            if (Active || IsTraveling || !stage || (switcher && switcher.IsOnFoot)) return;
            float best = range * noticeRange;
            var list = EnemyShipAI.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (!Boardable(ai)) continue;
                float d = Vector3.Distance(ai.transform.position, transform.position);
                if (d >= best) continue;
                best = d;
                Candidate = ai;
            }
            if (!Candidate) return;
            CandidateDistance = best;
            var health = Candidate.GetComponent<Health>();
            CandidateDamaged = !health || health.Current <= health.Max * maxHull;
            if (!CandidateDamaged) BlockReason = "avarie o casco dele (até a metade)";
            else if (best > range) BlockReason = $"chegue mais perto ({best - range:0} m)";
            else if (ship.SpeedKnots > maxSpeedKnots) BlockReason = "diminua a vela";
        }

        bool Boardable(EnemyShipAI ai)
        {
            if (!ai || !ai.isActiveAndEnabled) return false;
            string kind = KindOf(ai);
            if (kind == null || FindKind(kind) == null) return false;
            var health = ai.GetComponent<Health>();
            if (health && health.IsDead) return false;
            return stage.Has(kind);
        }

        /// <summary>
        /// Quantos defendem: a tripulação da chalupa menos o estrago recente da metralha (ShipDebuffs.CrewDamage vai até 0,6 e se
        /// recupera em ~30 s): metralha a bordo logo antes de abordar = menos piratas no convés.
        /// </summary>
        int CrewFor(EnemyShipAI enemy)
        {
            var debuffs = enemy ? enemy.GetComponent<ShipDebuffs>() : null;
            var kind = FindKind(KindOf(enemy)) ?? kinds[0];
            int lost = debuffs ? Mathf.Clamp(Mathf.RoundToInt(debuffs.CrewDamage / 0.6f * kind.maxCrewLoss), 0, kind.maxCrewLoss) : 0;
            return Mathf.Max(1, kind.crew - lost);
        }

        /// <summary>
        /// Tipo do convés no palco: chalupa pirata comum (sem alvo de missão, sem chefe), o brigue Santa Clara pelo id dele; o Tormenta, os
        /// caçadores e a Esquadra Real ainda não (null = não dá para abordar).
        /// </summary>
        public static string KindOf(EnemyShipAI ai)
        {
            if (!ai) return null;
            // Esquadra Real: a corveta no casco médio; fragata e nau no grande.
            if (ai.Crown) return ai.name.StartsWith("Corveta") ? CorvetteKind : FrigateKind;
            if (ai.TargetId == BrigTargetId) return BrigKind;
            if (ai.TargetId == TormentaTargetId) return TormentaKind;
            if (ai.GetComponent<ShipBoss>()) return ai.name.StartsWith("Cacador") ? HunterKind : null;
            return string.IsNullOrEmpty(ai.TargetId) ? SloopKind : null;
        }

        public Kind FindKind(string id)
        {
            if (id == null) return null;
            foreach (var k in kinds) if (k != null && k.id == id) return k;
            return null;
        }

        public bool Open()
        {
            if (!CanOpen) return false;
            travel = StartCoroutine(Board(Candidate));
            return true;
        }

        public void Close() { }

        /// <summary>
        /// Sandbox (F1): traz o navio para o lado do seu (20 m a estibordo), põe o casco abaixo da metade e aborda na hora, sem conferir
        /// distância e velocidade. Falso se não dá para abordar esse navio (ou o capitão está a pé, ou já está numa abordagem).
        /// </summary>
        public bool BoardNow(EnemyShipAI enemy)
        {
            if (!enemy || Active || IsTraveling || !stage || (switcher && switcher.IsOnFoot)) return false;
            string kind = KindOf(enemy);
            if (kind == null || FindKind(kind) == null || !stage.Has(kind)) return false;
            if (enemy.TryGetComponent(out ShipController enemyShip))
                enemyShip.Teleport(transform.position + transform.right * 20f, transform.rotation);
            if (enemy.TryGetComponent(out Health health) && health.Current > health.Max * maxHull)
                health.SetCurrent(health.Max * maxHull * 0.8f);
            travel = StartCoroutine(Board(enemy));
            return true;
        }

        /// <summary>A pé no palco, perto do ponto de chegada: F volta ao navio.</summary>
        public bool CanReturn(Vector3 position, float reach) =>
            Active && !IsTraveling && !AwaitingChoice && stage && stage.ReturnDistance(position) <= reach;

        public bool Return()
        {
            if (!Active || IsTraveling) return false;
            travel = StartCoroutine(Leave());
            return true;
        }

        IEnumerator Board(EnemyShipAI enemy)
        {
            target = enemy;
            rewarded = false;
            Surrendered = 0;
            HasPrisoner = false;
            leftover.Clear();
            for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
            {
                Fade = t / fadeOutSeconds;
                yield return null;
            }
            Fade = 1f;
            string kind = KindOf(enemy);
            var point = enemy ? stage.Show(shipClass && shipClass.Current ? shipClass.Current.name : "", kind, CrewFor(enemy)) : null;
            if (!point)
            {
                // Sumiu no meio (afundou) ou não há convés: volta a clarear sem abordar.
                target = null;
                yield return FadeIn();
                travel = null;
                yield break;
            }
            Freeze();
            switcher.Disembark(point);
            if (Services.TryGet(out ICameraFollow cam)) cam.SnapToTarget();
            Active = true;
            yield return new WaitForSeconds(holdSeconds);
            Boarded?.Invoke((FindKind(kind) ?? kinds[0]).label);
            // A tripulação inimiga vem para cima (a abordagem é um ataque) enquanto a tela clareia.
            stage.RouseCrew();
            yield return FadeIn();
            travel = null;
        }

        IEnumerator Leave()
        {
            for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
            {
                Fade = t / fadeOutSeconds;
                yield return null;
            }
            Fade = 1f;
            switcher.Embark();
            Release();
            if (Services.TryGet(out ICameraFollow cam)) cam.SnapToTarget();
            yield return new WaitForSeconds(holdSeconds);
            Returned?.Invoke();
            yield return FadeIn();
            travel = null;
        }

        IEnumerator FadeIn()
        {
            for (float t = 0f; t < fadeInSeconds; t += Time.deltaTime)
            {
                Fade = 1f - t / fadeInSeconds;
                yield return null;
            }
            Fade = 0f;
        }

        /// <summary>Os dois navios presos pelos ganchos: parados, o inimigo sem a IA.</summary>
        void Freeze()
        {
            Stop(body);
            SetShielded(true);
            if (!target) return;
            targetAiWasOn = target.enabled;
            target.enabled = false;
            targetBody = target.GetComponent<Rigidbody>();
            Stop(targetBody);
        }

        void Release()
        {
            Active = false;
            SetShielded(false);
            // Tripulação derrotada (passo 2): o navio fica à deriva, velas recolhidas e sem leme (a rendição e o saque são o passo 3).
            bool taken = stage.CrewDefeated;
            stage.Hide();
            if (body) body.isKinematic = false;
            if (target)
            {
                if (targetBody) targetBody.isKinematic = false;
                if (taken && target.TryGetComponent(out ShipController enemyShip))
                {
                    enemyShip.SetSail(enemyShip.FurledSailIndex);
                    enemyShip.enabled = false;
                    if (target.TryGetComponent(out Pirates.Loot.LootDropper loot)) loot.MarkPlundered();
                    // À deriva, afunda sozinho depois de um tempo (conta como afundado; o respawner traz outro).
                    var hulk = target.GetComponent<AdriftHulk>() ?? target.gameObject.AddComponent<AdriftHulk>();
                    hulk.Begin(adriftSeconds);
                }
                else target.enabled = targetAiWasOn;
            }
            SpillLeftover();
            target = null;
            targetBody = null;
        }

        /// <summary>O que não coube no porão boia ao lado do seu navio (os destroços de sempre: F para pegar, somem com o tempo).</summary>
        void SpillLeftover()
        {
            if (leftover.Count == 0) return;
            var table = target && target.TryGetComponent(out LootDropper dropper) ? dropper.Table : null;
            if (table && table.wreckPrefab)
            {
                var at = transform.position + transform.right * 9f;
                at.y = Services.TryGet(out IOceanSampler ocean) ? ocean.GetHeight(at) : 0f;
                var wreck = Instantiate(table.wreckPrefab, at, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                wreck.name = "Destrocos_Abordagem";
                wreck.Setup(new List<ItemStack>(leftover), null, table.lifetime);
            }
            leftover.Clear();
        }

        Health ownHealth, targetHealth;
        bool ownWasInvulnerable, targetWasInvulnerable;

        /// <summary>
        /// Com o capitão no palco os dois cascos não levam dano: a escolta do Tormenta (e quem mais estiver perto) continuaria atirando no
        /// seu navio parado, que afundaria com você lá no convés inimigo.
        /// </summary>
        void SetShielded(bool on)
        {
            if (on)
            {
                ownHealth = GetComponent<Health>();
                targetHealth = target ? target.GetComponent<Health>() : null;
                if (ownHealth) { ownWasInvulnerable = ownHealth.Invulnerable; ownHealth.Invulnerable = true; }
                if (targetHealth) { targetWasInvulnerable = targetHealth.Invulnerable; targetHealth.Invulnerable = true; }
                return;
            }
            if (ownHealth) ownHealth.Invulnerable = ownWasInvulnerable;
            if (targetHealth) targetHealth.Invulnerable = targetWasInvulnerable;
            ownHealth = targetHealth = null;
        }

        static void Stop(Rigidbody rb)
        {
            if (!rb || rb.isKinematic) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        void OnDestroy()
        {
            if (Active) Release();
        }
    }
}
