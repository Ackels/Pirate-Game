using System.Collections.Generic;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Progression;
using Pirates.Save;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Audio
{
    /// <summary>
    /// Liga os sons ao jogo sem que os sistemas conheçam o áudio: ouve os eventos que eles já têm (canhões, porto,
    /// casas, missões, saque, jogo salvo, troca navio ↔ terra) e põe ShipAudio em cada navio e CharacterAudio em cada
    /// personagem a pé (procura de novo a cada 2 s: navios inimigos reaparecem como cópias novas). O personagem começa
    /// desligado (o jogador está no navio): as buscas incluem objetos desligados. Também o mar aberto
    /// (ondas 2D enquanto navega). Os volumes de cada canal vêm do SoundSettings.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] GameSounds sounds = new();
        [SerializeField] SoundSettings settings;
        [Tooltip("Tom da voz dos piratas (o capitão é 1).")]
        [SerializeField] float pirateVoicePitch = 0.82f;
        [Tooltip("Segundos entre ondas do mar aberto (sorteado entre x e y).")]
        [SerializeField] Vector2 seaInterval = new(1.6f, 3.4f);

        readonly HashSet<Object> hooked = new();
        PlayerModeSwitcher switcher;
        float nextScan, nextWave;
        readonly Dictionary<LandChest, bool> chestOpen = new();
        readonly Dictionary<BuriedTreasure, bool> canDig = new();

        public GameSounds Sounds => sounds;

        public void Configure(GameSounds gameSounds, SoundSettings soundSettings)
        {
            sounds = gameSounds;
            settings = soundSettings;
        }

        void Awake()
        {
            SoundMix.Apply(settings);
            SoundMix.Apply(GameOptions.Current);
        }

        void OnEnable()
        {
            GameOptions.Changed += OnOptionsChanged;
            EventBus.Subscribe<TutorialStepEvent>(OnTutorialStep);
            // Ruína das alavancas (C4, 19/10): eventos estáticos (saem no OnDisable: senão a cena recarregada tocaria dobrado).
            Pirates.LandCombat.RuinTrap.Triggered += OnTrapTriggered;
            Pirates.LandCombat.RuinTrap.HitSomeone += OnTrapHit;
            Pirates.LandCombat.RuinLever.Pulled += OnLeverPulled;
            Pirates.LandCombat.RuinPassage.Used += OnPassageUsed;
            Pirates.LandCombat.CryptMonster.Cued += OnCryptCue;
            // Pesca (D2, 27/10): zunido da linha saindo, a carretilha e o peixe chegando.
            FishingRod.CastOut += OnFishCast;
            FishingRod.Hooking += OnFishHooked;
            FishingRod.Landing += OnFishHooked;
            FishingRod.Caught += OnFishCaught;
            // Cortar árvores (D1, 28/10): machadada, o tronco rangendo ao tombar e a pancada no chão.
            ResourceNode.Struck += OnChopped;
            ResourceNode.Felled += OnTreeFelled;
            ResourceNode.Landed += OnTreeLanded;
        }

        void OnChopped(ResourceNode node, Vector3 point, Vector3 direction)
        {
            if (node && node.Type == ResourceNode.Kind.Ore)
            {
                // Picaretada: a pancada na pedra e um tinido curto de ferro.
                SoundPlayer.At(sounds.dig, point, 1f, 1.1f);
                SoundPlayer.At(sounds.block, point, 0.35f, 1.6f);
                return;
            }
            SoundPlayer.At(sounds.chop, point);
        }

        void OnTreeFelled(ResourceNode node, Vector3 foot, Vector3 direction)
        {
            // Veio: o minério se solta e rola (pedras batendo, mais grave).
            if (node && node.Type == ResourceNode.Kind.Ore) SoundPlayer.At(sounds.dig, foot + Vector3.up * 0.3f, 1f, 0.7f);
            else SoundPlayer.At(sounds.shipCreak, foot + Vector3.up * 2f, 1f, 0.8f);
        }

        void OnTreeLanded(ResourceNode node, Vector3 point)
        {
            if (node && node.Type == ResourceNode.Kind.Ore)
            {
                SoundPlayer.At(sounds.bodyFall, point, 0.6f, 1.2f);
                return;
            }
            SoundPlayer.At(sounds.crateBreak, point, 1f, 0.6f);
            SoundPlayer.At(sounds.bodyFall, point, 1f, 0.7f);
        }

        void OnFishCast(FishingRod rod) => SoundPlayer.At(sounds.dodge, rod.transform.position + Vector3.up, 0.45f, 1.6f);

        void OnFishHooked(FishingRod rod) => SoundPlayer.At(sounds.shipCreak, rod.transform.position + Vector3.up, 0.5f, 2.2f);

        void OnFishCaught(FishingRod rod, Pirates.Economy.ItemDefinition item)
        {
            SoundPlayer.At(sounds.pickup, rod.transform.position + Vector3.up, 1f, 1.1f);
            if (item && item.rarity >= Pirates.Economy.ItemRarity.Rare) SoundPlayer.Ui(sounds.missionDone);
        }

        void OnTrapTriggered(Pirates.LandCombat.RuinTrap trap, bool warning)
        {
            var p = trap.transform.position + Vector3.up * 0.5f;
            switch (trap.TrapKind)
            {
                // Espinhos: estalo de metal no aviso e o "shing" grave ao subir.
                case Pirates.LandCombat.RuinTrap.Kind.Spikes:
                    if (warning) SoundPlayer.At(sounds.pistolReload, p, 0.6f, 0.7f);
                    else SoundPlayer.At(sounds.swordSwing, p, 1f, 0.6f);
                    break;
                // Dardo: clique agudo no aviso e o assobio.
                case Pirates.LandCombat.RuinTrap.Kind.Dart:
                    if (warning) SoundPlayer.At(sounds.pistolReload, p, 0.55f, 1.35f);
                    else SoundPlayer.At(sounds.swordSwing, p, 0.9f, 1.45f);
                    break;
                // Fogo: o "fuum" (explosão baixinha e aguda) ao acender.
                case Pirates.LandCombat.RuinTrap.Kind.Fire:
                    if (!warning) SoundPlayer.At(sounds.explosion, p, 0.3f, 1.7f);
                    break;
            }
        }

        void OnTrapHit(Pirates.LandCombat.RuinTrap trap, Vector3 p) => SoundPlayer.At(sounds.swordHit, p);

        // Alçapão/escada das catacumbas (C5): a porta rangendo, mais grave, na tela que escurece.
        void OnPassageUsed(Pirates.LandCombat.RuinPassage passage) =>
            SoundPlayer.At(sounds.doorOpen, passage.transform.position + Vector3.up * 0.5f, 1f, 0.75f);

        // Almas penadas: gemido ao aparecer (agudo ao levar golpe, grave ao morrer) e o sopro da cabeçada; morcegos: guincho no mergulho.
        void OnCryptCue(Pirates.LandCombat.CryptMonster monster, Pirates.LandCombat.CryptMonster.Cue cue)
        {
            var p = monster.transform.position + Vector3.up;
            if (monster.IsGhost)
            {
                // O Capelão (C5, passo 3) usa os mesmos sons, mais graves.
                float v = monster.VoicePitch;
                switch (cue)
                {
                    case Pirates.LandCombat.CryptMonster.Cue.Appear: SoundPlayer.At(sounds.ghostMoan, p, 0.9f, 1f * v); break;
                    // Começo do preparo: guincho agudo (o aviso); o sopro sai com o golpe.
                    case Pirates.LandCombat.CryptMonster.Cue.Attack: SoundPlayer.At(sounds.ghostMoan, p, 0.8f, 1.65f * v); break;
                    case Pirates.LandCombat.CryptMonster.Cue.Strike: SoundPlayer.At(sounds.swordSwing, p, 0.8f, 0.6f * v); break;
                    case Pirates.LandCombat.CryptMonster.Cue.Hurt: SoundPlayer.At(sounds.ghostMoan, p, 0.6f, 1.4f * v); break;
                    case Pirates.LandCombat.CryptMonster.Cue.Die: SoundPlayer.At(sounds.ghostMoan, p, 1f, 0.7f * v); break;
                    case Pirates.LandCombat.CryptMonster.Cue.Vanish: SoundPlayer.At(sounds.swordSwing, p, 0.35f, 0.45f * v); break;
                    // As almas estourando do chão: estrondo abafado e o gemido.
                    case Pirates.LandCombat.CryptMonster.Cue.Burst:
                        SoundPlayer.At(sounds.explosion, p, 0.45f, 0.6f);
                        SoundPlayer.At(sounds.ghostMoan, p, 0.9f, 0.85f * v);
                        break;
                }
                return;
            }
            switch (cue)
            {
                case Pirates.LandCombat.CryptMonster.Cue.Attack: SoundPlayer.At(sounds.batScreech, p, 1f, 1f); break;
                case Pirates.LandCombat.CryptMonster.Cue.Hurt: SoundPlayer.At(sounds.batScreech, p, 0.7f, 1.25f); break;
                case Pirates.LandCombat.CryptMonster.Cue.Die:
                    SoundPlayer.At(sounds.batScreech, p, 0.8f, 0.75f);
                    SoundPlayer.At(sounds.bodyFall, p, 0.4f, 1.6f);
                    break;
            }
        }

        void OnLeverPulled(Pirates.LandCombat.RuinLever lever)
        {
            var p = lever.transform.position + Vector3.up;
            SoundPlayer.At(sounds.chestOpen, p, 1f, 0.6f);
            SoundPlayer.At(sounds.block, p, 0.7f, 0.7f);
        }

        void OnRuinOpened(Pirates.LandCombat.RuinPuzzle puzzle)
        {
            var p = puzzle.Gate ? puzzle.Gate.position : puzzle.transform.position;
            SoundPlayer.At(sounds.dig, p, 1.4f, 0.5f);
            SoundPlayer.At(sounds.chestOpen, p, 1.2f, 0.45f);
            SoundPlayer.Ui(sounds.missionDone);
        }

        void OnDisable()
        {
            GameOptions.Changed -= OnOptionsChanged;
            EventBus.Unsubscribe<TutorialStepEvent>(OnTutorialStep);
            Pirates.LandCombat.RuinTrap.Triggered -= OnTrapTriggered;
            Pirates.LandCombat.RuinTrap.HitSomeone -= OnTrapHit;
            Pirates.LandCombat.RuinLever.Pulled -= OnLeverPulled;
            Pirates.LandCombat.RuinPassage.Used -= OnPassageUsed;
            Pirates.LandCombat.CryptMonster.Cued -= OnCryptCue;
            FishingRod.CastOut -= OnFishCast;
            FishingRod.Hooking -= OnFishHooked;
            FishingRod.Landing -= OnFishHooked;
            FishingRod.Caught -= OnFishCaught;
            ResourceNode.Struck -= OnChopped;
            ResourceNode.Felled -= OnTreeFelled;
            ResourceNode.Landed -= OnTreeLanded;
        }

        // Etapa do tutorial cumprida: a vinheta de missão aceita; o fim do tutorial, a de missão cumprida.
        void OnTutorialStep(TutorialStepEvent e) => SoundPlayer.Ui(e.finished ? sounds.missionDone : sounds.missionAccepted);

        static void OnOptionsChanged() => SoundMix.Apply(GameOptions.Current);

        void Start()
        {
            switcher = FindAnyObjectByType<PlayerModeSwitcher>();
            if (switcher) switcher.ModeChanged += OnModeChanged;
            if (Services.TryGet(out CombatEffects effects))
            {
                effects.Blasted += (p, _) => SoundPlayer.At(sounds.cannonFire, p);
                effects.Splashed += p => SoundPlayer.At(sounds.cannonSplash, p);
                effects.Impacted += (p, _) => SoundPlayer.At(sounds.cannonImpact, p);
                effects.Exploded += (p, _) => SoundPlayer.At(sounds.explosion, p);
                // Lula gigante: água estourando (respingo grave, mais forte quanto maior) e o urro (voz de morte bem grave + estrondo).
                effects.Surged += (p, size) => SoundPlayer.At(sounds.cannonSplash, p, Mathf.Clamp(0.6f + size * 0.35f, 0.6f, 1.6f), Mathf.Clamp(0.95f - size * 0.12f, 0.55f, 0.95f));
                effects.Roared += p =>
                {
                    SoundPlayer.At(sounds.death, p, 1.6f, 0.32f);
                    SoundPlayer.At(sounds.explosion, p, 0.5f, 0.45f);
                };
                // Cova abrindo (terra cavada, mais grave) ou ossos virando pó (queda do corpo, mais aguda).
                effects.Unearthed += (p, rising) => SoundPlayer.At(rising ? sounds.dig : sounds.bodyFall, p, 1.2f, rising ? 0.75f : 1.35f);
                // Pesca: o "plop" da boia (passo na água, mais grave quanto maior o respingo).
                effects.Plopped += (p, size) => SoundPlayer.At(sounds.stepWater, p, Mathf.Clamp(0.5f + size * 0.4f, 0.4f, 1.3f), Mathf.Clamp(1.35f - size * 0.25f, 0.8f, 1.4f));
            }
            foreach (var visitor in FindObjectsByType<HarborVisitor>(FindObjectsInactive.Include))
            {
                visitor.Docked += d => SoundPlayer.At(sounds.harborBell, d.transform.position + Vector3.up * 6f);
                visitor.Undocked += d => SoundPlayer.At(sounds.harborBell, d.transform.position + Vector3.up * 6f, 0.6f, 1.15f);
                visitor.Transferred += (_, _) => SoundPlayer.Ui(sounds.cargoMoved);
                visitor.Repaired += (_, _, _) => SoundPlayer.Ui(sounds.repair);
                visitor.Traded += (_, _) => SoundPlayer.Ui(sounds.purchase);
                visitor.CraftOrdered += _ => SoundPlayer.Ui(sounds.purchase);
                visitor.Crafted += _ => SoundPlayer.Ui(sounds.cargoMoved);
            }
            foreach (var building in FindObjectsByType<HarborBuildingVisitor>(FindObjectsInactive.Include))
            {
                building.Entered += _ => SoundPlayer.Ui(sounds.doorOpen);
                building.Left += _ => SoundPlayer.Ui(sounds.doorClose);
            }
            foreach (var dock in HarborDock.Active)
            {
                dock.ConstructionStarted += _ => SoundPlayer.Ui(sounds.purchase);
                dock.ConstructionFinished += _ => SoundPlayer.Ui(sounds.buildDone);
                dock.SmithyReady += _ => SoundPlayer.Ui(sounds.buildDone);
                if (dock.Buildings) dock.Buildings.Built += (_, _) => SoundPlayer.Ui(sounds.buildDone);
            }
            foreach (var looter in FindObjectsByType<LandLooter>(FindObjectsInactive.Include))
            {
                looter.Transferred += (_, _) => SoundPlayer.Ui(sounds.pickup);
                looter.BackpackFull += () => SoundPlayer.Ui(sounds.backpackFull);
            }
            foreach (var salvager in FindObjectsByType<WreckSalvager>(FindObjectsInactive.Include)) salvager.Transferred += (_, _) => SoundPlayer.Ui(sounds.pickup);
            foreach (var unloader in FindObjectsByType<BackpackUnloader>(FindObjectsInactive.Include)) unloader.Unloaded += (_, _) => SoundPlayer.Ui(sounds.cargoMoved);
            if (Services.TryGet(out MissionBoard board))
            {
                board.Accepted += _ => SoundPlayer.Ui(sounds.missionAccepted);
                board.ObjectiveCompleted += _ => SoundPlayer.Ui(sounds.missionDone);
                board.TurnedIn += (_, _) => SoundPlayer.Ui(sounds.missionPaid);
            }
            foreach (var save in FindObjectsByType<GameSave>()) save.Saved += _ => SoundPlayer.Ui(sounds.gameSaved);
            // Patente nova (M10): a fanfarra da missão paga.
            if (CaptainFame.Current) CaptainFame.Current.RankUp += _ => SoundPlayer.Ui(sounds.missionPaid);
            foreach (var puzzle in FindObjectsByType<Pirates.LandCombat.RuinPuzzle>(FindObjectsInactive.Include)) puzzle.Opened += OnRuinOpened;
            foreach (var dummy in FindObjectsByType<TrainingDummy>())
                if (dummy.TryGetComponent(out Health h)) h.Damaged += info => SoundPlayer.At(sounds.dummyHit, info.point);
            Scan();
        }

        void OnDestroy()
        {
            if (switcher) switcher.ModeChanged -= OnModeChanged;
        }

        void OnModeChanged(bool onFoot)
        {
            if (!switcher.Ship) return;
            // Âncora descendo (desembarcou) ou subindo (embarcou).
            SoundPlayer.At(sounds.anchor, switcher.Ship.transform.position, onFoot ? 1f : 0.8f, onFoot ? 1f : 1.12f);
        }

        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 2f;
                Scan();
            }
            WatchChests();
            // Mar aberto: ondas 2D enquanto navega (em terra quem soa são as ondas da praia).
            bool sailing = !switcher || !switcher.IsOnFoot;
            if (sailing && Time.time >= nextWave)
            {
                nextWave = Time.time + Random.Range(seaInterval.x, seaInterval.y);
                SoundPlayer.Ui(sounds.seaWaves);
            }
        }

        /// <summary>Navios, personagens e caixotes novos ganham seus sons.</summary>
        void Scan()
        {
            foreach (var ship in FindObjectsByType<ShipController>())
            {
                if (!hooked.Add(ship)) continue;
                if (!ship.TryGetComponent(out ShipAudio audio)) audio = ship.gameObject.AddComponent<ShipAudio>();
                audio.Init(sounds);
            }
            foreach (var character in FindObjectsByType<FootCharacter>(FindObjectsInactive.Include))
            {
                if (!hooked.Add(character)) continue;
                bool player = character.TryGetComponent(out PlayerFootInput _);
                if (!character.TryGetComponent(out CharacterAudio audio)) audio = character.gameObject.AddComponent<CharacterAudio>();
                audio.Init(sounds, player ? 1f : pirateVoicePitch, 1f);
            }
            foreach (var crate in BreakableLoot.Active)
            {
                if (!hooked.Add(crate) || !crate.TryGetComponent(out Health h)) continue;
                var at = crate.transform;
                h.Died += () => SoundPlayer.At(sounds.crateBreak, at.position + Vector3.up * 0.4f);
            }
        }

        /// <summary>Baú abrindo (começou a saquear) e tesouro desenterrado (deixou de dar para cavar).</summary>
        void WatchChests()
        {
            for (int i = 0; i < LandChest.Active.Count; i++)
            {
                var chest = LandChest.Active[i];
                bool open = chest.IsBeingLooted;
                if (chestOpen.TryGetValue(chest, out bool was) && open && !was)
                {
                    SoundPlayer.At(sounds.chestOpen, chest.transform.position);
                    // Prêmio raro ou melhor (E1): a fanfarra da missão cumprida por cima.
                    if (chest.BestRarity >= Pirates.Economy.ItemRarity.Rare) SoundPlayer.Ui(sounds.missionDone);
                }
                chestOpen[chest] = open;
            }
            for (int i = 0; i < BuriedTreasure.Active.Count; i++)
            {
                var spot = BuriedTreasure.Active[i];
                bool can = spot.CanDig;
                if (canDig.TryGetValue(spot, out bool was) && was && !can) SoundPlayer.At(sounds.dig, spot.transform.position);
                canDig[spot] = can;
            }
        }
    }
}
