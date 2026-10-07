using Pirates.Audio;
using Pirates.Base;
using Pirates.Combat;
using Pirates.Controls;
using Pirates.Core;
using Pirates.Economy;
using Pirates.LandCombat;
using Pirates.Loot;
using Pirates.Missions;
using Pirates.OnFoot;
using Pirates.Progression;
using Pirates.Save;
using Pirates.Ships;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates.Sandbox
{
    /// <summary>
    /// Modo sandbox (F1 abre o painel; F2 teleporta para o porto a qualquer momento): painel de debug para testar progressão rápido — dar itens, mexer no casco,
    /// afundar ou trazer inimigos, ir ao porto, zerar upgrades e acelerar o tempo. Em terra (cena 06): vida do personagem,
    /// invencível, levantar/derrotar o acampamento, piratas distraídos, ir direto para o acampamento, esvaziar a mochila
    /// e repor o saque da ilha (baús e caixotes). No mundo (cena 07): cumprir os objetivos das missões ativas, zerar as missões
    /// e o jogo salvo (salvar agora, carregar, começar de novo).
    /// Só existe no editor e em builds de desenvolvimento.
    /// </summary>
    public class SandboxPanel : MonoBehaviour
    {
        [SerializeField] ShipController player;
        [Tooltip("Itens que o painel oferece (o gerador preenche com tudo de Data/Items).")]
        [SerializeField] ItemDefinition[] items = System.Array.Empty<ItemDefinition>();
        [Tooltip("Quantidade de cada recurso no botão \"Encher tudo\".")]
        [SerializeField] int fillAmount = 2000;
        [SerializeField] int fillCrew = 5;

        static readonly float[] TimeScales = { 1f, 2f, 4f };
        static readonly string[] TimeLabels = { "1×", "2×", "4×" };
        static readonly string[] Targets = { "Porão", "Armazém" };

        bool open;
        int target;
        Rect window = new(16f, 16f, 470f, 640f);
        Vector2 scroll;
        Inventory hold;
        Health hull;
        ShipUpgrades upgrades;
        ShipCrew crew;
        PlayerShipClass shipClass;
        float originalCapacity;
        GUIStyle small;
        PlayerModeSwitcher modes;
        Health walkerHealth;
        FootHealthReactions walkerReactions;
        Inventory backpack;
        GameSave save;

        public void Configure(ShipController playerShip, ItemDefinition[] sandboxItems)
        {
            player = playerShip;
            items = sandboxItems;
        }

        void Awake()
        {
            if (!Debug.isDebugBuild) Destroy(this);
        }

        void Start()
        {
            if (!player) return;
            player.TryGetComponent(out hold);
            player.TryGetComponent(out hull);
            player.TryGetComponent(out upgrades);
            player.TryGetComponent(out crew);
            player.TryGetComponent(out shipClass);
            if (hold) originalCapacity = hold.Capacity;
            modes = FindAnyObjectByType<PlayerModeSwitcher>();
            save = FindAnyObjectByType<GameSave>();
            if (modes && modes.Character)
            {
                walkerHealth = modes.Character.GetComponent<Health>();
                walkerReactions = modes.Character.GetComponent<FootHealthReactions>();
                backpack = modes.Character.GetComponent<Inventory>();
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) open = !open;
            if (keyboard.f2Key.wasPressedThisFrame && Dock) GoToHarbor();
            // Fechado, só há um texto no canto: sem a passada de Layout do IMGUI (que aloca a cada quadro).
            useGUILayout = open;
        }

        void OnDisable() => Time.timeScale = 1f;

        void OnGUI()
        {
            if (!player) return;
            if (!open || !useGUILayout)
            {
                GUI.Label(new Rect(Screen.width - 330f, Screen.height - 26f, 320f, 22f), Dock ? "F1  modo sandbox   ·   F2  ir para o porto" : "F1  modo sandbox");
                return;
            }
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            window = GUILayout.Window(GetHashCode(), window, DrawWindow, "Modo sandbox (F1 fecha)");
        }

        HarborDock Dock => HarborDock.Active.Count > 0 ? HarborDock.Active[0] : null;

        Inventory Destination => target == 1 && Dock ? Dock.Warehouse : hold;

        void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            DrawItems();
            GUILayout.Space(8f);
            DrawShip();
            GUILayout.Space(8f);
            DrawOnFoot();
            DrawEnemies();
            DrawMissions();
            DrawFame();
            DrawDayNight();
            DrawWind();
            DrawSave();
            DrawSound();
            DrawTime();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        // ---------- Itens ----------

        void DrawItems()
        {
            GUILayout.Label("<b>Itens</b>", small);
            if (!hold)
            {
                GUILayout.Label("<i>o navio não tem porão nesta cena</i>", small);
                return;
            }

            if (Dock) target = GUILayout.Toolbar(target, Targets);
            else target = 0;

            bool unlimited = GUILayout.Toggle(hold.Unlimited, " Porão sem limite de espaço");
            if (unlimited != hold.Unlimited) hold.Configure(originalCapacity, unlimited);
            GUILayout.Label($"<size=11>Porão: {hold.UsedSpace:0.#} / {(hold.Unlimited ? "∞" : hold.Capacity.ToString("0"))}</size>", small);

            var dest = Destination;
            foreach (var item in items)
            {
                if (!item) continue;
                // Tripulação só existe a bordo (não fica no armazém).
                var to = item.category == ItemCategory.Crew ? hold : dest;
                GUILayout.BeginHorizontal();
                string color = ColorUtility.ToHtmlStringRGB(item.color);
                GUILayout.Label($"<color=#{color}>{item.displayName}</color>  <size=11>({to.Count(item)})</size>", small, GUILayout.Width(200f));
                if (item.IsUnique)
                {
                    if (to.Has(item)) GUILayout.Label("<i>já tem</i>", small);
                    else if (GUILayout.Button("Ganhar", GUILayout.Width(80f))) to.Add(item, 1);
                }
                else
                {
                    if (GUILayout.Button("+1", GUILayout.Width(44f))) to.Add(item, 1);
                    if (GUILayout.Button("+10", GUILayout.Width(48f))) to.Add(item, 10);
                    if (GUILayout.Button("+100", GUILayout.Width(54f))) to.Add(item, 100);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Encher tudo")) FillEverything();
            if (GUILayout.Button("Esvaziar porão")) EmptyHold();
            GUILayout.EndHorizontal();
            GUILayout.Label($"<size=11>Encher tudo: +{fillAmount} de cada recurso no destino, +{fillCrew} sobreviventes a bordo e todos os esquemas.</size>", small);
        }

        void FillEverything()
        {
            var dest = Destination;
            if (dest == hold && !hold.Unlimited) hold.Configure(originalCapacity, true);
            foreach (var item in items)
            {
                if (!item) continue;
                if (item.category == ItemCategory.Crew) hold.Add(item, fillCrew);
                else if (item.IsUnique) dest.Add(item, 1);
                else dest.Add(item, fillAmount);
            }
        }

        void EmptyHold() => EmptyInventory(hold);

        // ---------- Navio ----------

        void DrawShip()
        {
            GUILayout.Label("<b>Navio</b>", small);
            if (hull)
            {
                GUILayout.Label($"Casco: {hull.Current:0} / {hull.Max:0}{(hull.IsDead ? "  <color=#ff7a66>afundando (F5 recomeça)</color>" : "")}", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Levar 50 de dano")) Damage(50f);
                if (GUILayout.Button("Consertar tudo")) hull.Heal(hull.Max);
                GUILayout.EndHorizontal();
                hull.Invulnerable = GUILayout.Toggle(hull.Invulnerable, " Casco invencível");
            }
            if (crew && hold && crew.CrewItem)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Tripulação: {crew.Count} / {crew.Berths}", small, GUILayout.Width(150f));
                if (GUILayout.Button("-1", GUILayout.Width(44f))) hold.Remove(crew.CrewItem, 1);
                if (GUILayout.Button("+1", GUILayout.Width(44f))) hold.Add(crew.CrewItem, 1);
                if (GUILayout.Button("Encher")) hold.Add(crew.CrewItem, crew.FreeBerths);
                if (GUILayout.Button("Zerar")) hold.Remove(crew.CrewItem, crew.Count);
                GUILayout.EndHorizontal();
            }
            if (shipClass && shipClass.Classes.Count > 1)
            {
                // Troca na hora, sem esquema nem obra (como no estaleiro: casco cheio e upgrades no zero).
                GUILayout.BeginHorizontal();
                GUILayout.Label("Navio:", small, GUILayout.Width(60f));
                foreach (var c in shipClass.Classes)
                {
                    if (!c) continue;
                    GUI.enabled = c != shipClass.Current;
                    if (GUILayout.Button(c.displayName)) shipClass.SetClass(c, newShip: true);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            var buildings = Dock ? Dock.Buildings : null;
            bool busy = Dock && (Dock.IsBusy || (buildings && buildings.IsBusy));
            GUILayout.BeginHorizontal();
            GUI.enabled = busy;
            if (GUILayout.Button(busy ? "Concluir obras agora (navio e base)" : "Concluir obras agora (nenhuma)"))
            {
                Dock.FinishNow();
                if (buildings) buildings.FinishNow();
            }
            GUI.enabled = buildings;
            if (GUILayout.Button("Derrubar construções")) buildings.ResetAll();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = Dock;
            if (GUILayout.Button("Teleportar para o porto (F2)")) GoToHarbor();
            GUI.enabled = upgrades;
            if (GUILayout.Button("Zerar upgrades")) upgrades.ResetLevels();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void Damage(float amount)
        {
            bool wasInvulnerable = hull.Invulnerable;
            hull.Invulnerable = false;
            hull.TakeDamage(new Pirates.Core.DamageInfo { amount = amount, point = player.transform.position, source = gameObject });
            hull.Invulnerable = wasInvulnerable;
        }

        /// <summary>Teleporta para a vaga do cais, parado e com as velas recolhidas: é só apertar F.</summary>
        void GoToHarbor()
        {
            var berth = Dock.Berth;
            player.Teleport(new Vector3(berth.position.x, player.transform.position.y, berth.position.z), berth.rotation);
        }

        // ---------- A pé ----------

        void DrawOnFoot()
        {
            if (!walkerHealth) return;
            bool onFoot = modes.IsOnFoot;
            GUILayout.Label("<b>A pé</b>", small);
            GUILayout.Label($"Vida: {walkerHealth.Current:0} / {walkerHealth.Max:0}{(walkerHealth.IsDead ? "  <color=#ff7a66>derrotado</color>" : "")}", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Levar 30 de dano")) HurtWalker(30f);
            if (GUILayout.Button("Vida cheia"))
            {
                if (walkerHealth.IsDead && walkerReactions) walkerReactions.Revive();
                else walkerHealth.Heal(walkerHealth.Max);
            }
            GUILayout.EndHorizontal();
            walkerHealth.Invulnerable = GUILayout.Toggle(walkerHealth.Invulnerable, " Invencível a pé");
            EnemyFootAI.Distracted = GUILayout.Toggle(EnemyFootAI.Distracted, " Piratas distraídos (não veem você)");

            foreach (var camp in EnemyCamp.Active)
            {
                GUILayout.Label($"<size=11>{camp.DisplayName}: {camp.Alive} / {camp.Total} piratas vivos</size>", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Levantar piratas")) camp.ResetAll();
                if (GUILayout.Button("Derrotar todos")) DefeatCamp(camp);
                GUI.enabled = onFoot && !walkerHealth.IsDead;
                if (GUILayout.Button("Ir até lá")) GoToCamp(camp);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            if (backpack)
            {
                GUILayout.Label($"<size=11>Mochila: {backpack.UsedSpace:0.#} / {backpack.Capacity:0} de espaço</size>", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Esvaziar mochila")) EmptyInventory(backpack);
                if (GUILayout.Button("Repor saque da ilha")) RestoreLandLoot();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8f);
        }

        static void EmptyInventory(Inventory inventory)
        {
            foreach (var item in new System.Collections.Generic.List<ItemDefinition>(inventory.Items))
                inventory.Remove(item, inventory.Count(item));
        }

        /// <summary>Baús fechados de novo (sorteiam outra vez), caixotes e barris inteiros, sacos no chão somem.</summary>
        static void RestoreLandLoot()
        {
            foreach (var chest in LandChest.Active) chest.ResetChest();
            foreach (var crate in BreakableLoot.Active) if (crate.IsBroken) crate.Restore();
            foreach (var sack in new System.Collections.Generic.List<LootSack>(LootSack.Active)) Destroy(sack.gameObject);
        }

        void HurtWalker(float amount)
        {
            bool wasInvulnerable = walkerHealth.Invulnerable;
            walkerHealth.Invulnerable = false;
            walkerHealth.TakeDamage(new Pirates.Core.DamageInfo { amount = amount, point = walkerHealth.transform.position, source = gameObject });
            walkerHealth.Invulnerable = wasInvulnerable;
        }

        static void DefeatCamp(EnemyCamp camp)
        {
            foreach (var ai in camp.Members)
                if (ai && !ai.IsDead) ai.Health.TakeDamage(new Pirates.Core.DamageInfo { amount = ai.Health.Max * 10f, point = ai.transform.position });
        }

        /// <summary>Põe o personagem a ~20 m ao norte do acampamento ou posto, virado para ele (os guardas já te veem).</summary>
        void GoToCamp(EnemyCamp camp)
        {
            Vector3 entrance = camp.transform.forward;
            Vector3 pos = camp.Center + entrance * 20f;
            modes.Character.Teleport(pos, Quaternion.LookRotation(-entrance));
        }

        // ---------- Missões ----------

        void DrawMissions()
        {
            if (!Pirates.Core.Services.TryGet(out MissionBoard board)) return;
            GUILayout.Label("<b>Missões</b>", small);
            GUILayout.Label($"<size=11>{board.Active.Count} ativa(s). Cumprir: afundar/acampamento/tesouro contam como feitos; entregas ganham a carga no porão.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cumprir objetivos ativos")) board.CompleteAll();
            if (GUILayout.Button("Zerar missões")) board.ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        // ---------- Fama do capitão (M10) ----------

        void DrawFame()
        {
            var fame = CaptainFame.Current;
            if (!fame) return;
            GUILayout.Label("<b>Fama do capitão</b>", small);
            GUILayout.Label($"<size=11>{fame.CaptainName}: {fame.Fame} de fama, {fame.RankTitle(fame.Rank)}; {fame.Perks.Count} bônus, " +
                            $"{fame.PendingPerks} para escolher (tecla C).</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+50 fama")) fame.Add(50, "sandbox");
            if (GUILayout.Button("+200 fama")) fame.Add(200, "sandbox");
            if (GUILayout.Button("Zerar fama")) fame.ResetAll(fame.CaptainName);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Dia e noite (M11): pular para uma hora e acelerar o relógio.</summary>
        void DrawDayNight()
        {
            if (!Services.TryGet(out ITimeOfDay time)) return;
            GUILayout.Label("<b>Dia e noite</b>", small);
            int minutes = Mathf.FloorToInt(time.Hour * 60f);
            GUILayout.Label($"<size=11>Dia {time.Day}, {minutes / 60:00}:{minutes % 60:00} (noite {time.Night:0.00}); relógio ×{time.Speed:0}.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Amanhecer")) time.SetTime(time.Day, 5.6f);
            if (GUILayout.Button("Meio-dia")) time.SetTime(time.Day, 12f);
            if (GUILayout.Button("Pôr do sol")) time.SetTime(time.Day, 18.6f);
            if (GUILayout.Button("Noite")) time.SetTime(time.Day, 23f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (float speed in new[] { 1f, 10f, 60f })
                if (GUILayout.Button($"×{speed:0}")) time.Speed = speed;
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        static readonly string[] CompassNames = { "norte", "nordeste", "leste", "sudeste", "sul", "sudoeste", "oeste", "noroeste" };

        /// <summary>Vento (M11): mostra de onde vem e a força; fixa a direção (de onde vem) e a força, ou volta ao vento que muda sozinho.</summary>
        void DrawWind()
        {
            if (!Services.TryGet(out IWind wind)) return;
            GUILayout.Label("<b>Vento</b>", small);
            // Vento se diz pelo lado de onde vem: soprando para o sul = vento norte.
            int from = Mathf.RoundToInt(Mathf.Repeat(wind.Degrees + 180f, 360f) / 45f) % 8;
            GUILayout.Label($"<size=11>Vem do {CompassNames[from]}, força {wind.Strength:0.00}{(wind.Overridden ? " (fixado aqui)" : " (muda sozinho)")}.</size>", small);
            GUILayout.BeginHorizontal();
            // Botão = de onde vem; o vento sopra para o lado oposto.
            if (GUILayout.Button("Do norte")) wind.SetOverride(180f, FixedStrength(wind));
            if (GUILayout.Button("Do leste")) wind.SetOverride(270f, FixedStrength(wind));
            if (GUILayout.Button("Do sul")) wind.SetOverride(0f, FixedStrength(wind));
            if (GUILayout.Button("Do oeste")) wind.SetOverride(90f, FixedStrength(wind));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fraco")) wind.SetOverride(wind.Degrees, 0.15f);
            if (GUILayout.Button("Forte")) wind.SetOverride(wind.Degrees, 1f);
            if (GUILayout.Button("Muda sozinho")) wind.SetOverride(null, null);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        static float? FixedStrength(IWind wind) => wind.Overridden ? wind.Strength : null;

        // ---------- Jogo salvo ----------

        void DrawSave()
        {
            if (!save) return;
            GUILayout.Label("<b>Jogo salvo</b>", small);
            string last = save.LastSaved.HasValue ? $"último às {save.LastSaved.Value:HH:mm:ss}" : save.HasSave ? "há um save (deste jogo ainda não)" : "nenhum save";
            GUILayout.Label($"<size=11>Salva sozinho ao atracar e ao sair do porto ({last}). Carregar recarrega a cena e põe o navio no porto. " +
                            $"Arquivo: {save.FilePath}</size>", small);
            GUILayout.BeginHorizontal();
            GUI.enabled = save.Started;
            if (GUILayout.Button("Salvar agora")) save.SaveNow();
            GUI.enabled = save.HasSave;
            if (GUILayout.Button("Carregar o save")) save.ReloadScene(GameSave.StartMode.Continue);
            GUI.enabled = true;
            if (GUILayout.Button("Novo jogo (apaga o save)")) save.ReloadScene(GameSave.StartMode.NewGame);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        // ---------- Inimigos ----------

        void DrawEnemies()
        {
            int alive = 0;
            bool brig = false;
            var ships = FindObjectsByType<EnemyShipAI>();
            if (ships.Length == 0) return;
            foreach (var ai in ships)
            {
                if (!ai.TryGetComponent(out Health h) || h.IsDead) continue;
                alive++;
                brig |= !string.IsNullOrEmpty(ai.TargetId);
            }
            GUILayout.Label("<b>Inimigos</b>", small);
            GUILayout.Label($"<size=11>{alive} inimigo(s) vivo(s). Afundada, a chalupa volta em ~25 s; o brigue (cena 07), em 150 s " +
                            "ou ao aceitar a missão dele.</size>", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Afundar inimigos agora")) SinkEnemies();
            if (GUILayout.Button("Trazer chalupa para perto")) BringEnemy(brig: false);
            GUI.enabled = brig;
            if (GUILayout.Button(brig ? "Trazer brigue" : "Brigue afundado")) BringEnemy(brig: true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            // Fortes inimigos (Ilha Norte, cena 07): calar na hora, restaurar, levar o navio para a frente dele.
            for (int i = 0; i < EnemyFort.Active.Count; i++)
            {
                var fort = EnemyFort.Active[i];
                GUILayout.Label($"<size=11>{fort.DisplayName}: {(fort.IsSilenced ? "<color=#7dffa0>calado</color>" : $"muralhas {fort.Health.Current:0} / {fort.Health.Max:0}{(fort.Engaging ? "  <color=#ff7a66>atirando</color>" : "")}")}</size>", small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Calar o forte")) fort.Silence();
                if (GUILayout.Button("Restaurar")) fort.Restore();
                GUI.enabled = modes == null || !modes.IsOnFoot;
                if (GUILayout.Button("Navio na frente dele")) ShipInFrontOf(fort.transform);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8f);
        }

        /// <summary>Navio parado a ~110 m ao sul do forte, de lado para ele (ao alcance dos canhões).</summary>
        void ShipInFrontOf(Transform fort)
        {
            var pos = fort.position + new Vector3(0f, 0f, -110f);
            player.Teleport(new Vector3(pos.x, player.transform.position.y, pos.z), Quaternion.Euler(0f, 90f, 0f));
        }

        static void SinkEnemies()
        {
            foreach (var ai in FindObjectsByType<EnemyShipAI>())
                if (ai.TryGetComponent(out Health h))
                    h.TakeDamage(new Pirates.Core.DamageInfo { amount = h.Max * 10f, point = ai.transform.position });
        }

        /// <summary>Põe o inimigo vivo (chalupa ou o brigue, que tem id de missão) a ~90 m na frente do jogador, virado para ele.</summary>
        void BringEnemy(bool brig)
        {
            foreach (var ai in FindObjectsByType<EnemyShipAI>())
            {
                if (!ai.TryGetComponent(out Health h) || h.IsDead) continue;
                if (string.IsNullOrEmpty(ai.TargetId) == brig) continue;
                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                Vector3 pos = player.transform.position + forward.normalized * 90f;
                pos.y = ai.transform.position.y;
                var rotation = Quaternion.LookRotation(-forward.normalized);
                var body = ai.GetComponent<Rigidbody>();
                body.linearVelocity = Vector3.zero;
                body.position = pos;
                body.rotation = rotation;
                ai.transform.SetPositionAndRotation(pos, rotation);
                return;
            }
        }

        // ---------- Som ----------

        MusicDirector music;

        void DrawSound()
        {
            if (!music) music = FindAnyObjectByType<MusicDirector>();
            GUILayout.Label($"<b>Som</b>{(music ? $"  <size=11>(música: {MusicName(music.Current)})</size>" : "")}", small);
            SoundMix.Master = VolumeSlider("Geral", SoundMix.Master);
            SoundMix.Music = VolumeSlider("Música", SoundMix.Music);
            SoundMix.Effects = VolumeSlider("Efeitos", SoundMix.Effects);
            SoundMix.Ambient = VolumeSlider("Ambiente", SoundMix.Ambient);
            SoundMix.Interface = VolumeSlider("Interface", SoundMix.Interface);
            GUILayout.Space(8f);
        }

        static string MusicName(MusicDirector.Mood mood) => mood switch
        {
            MusicDirector.Mood.Port => "porto", MusicDirector.Mood.Island => "ilha", MusicDirector.Mood.Combat => "combate", _ => "mar",
        };

        float VolumeSlider(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<size=11>{label}</size>", small, GUILayout.Width(70f));
            value = GUILayout.HorizontalSlider(value, 0f, 1f);
            GUILayout.Label($"<size=11>{value * 100f:0}%</size>", small, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return value;
        }

        // ---------- Tempo ----------

        void DrawTime()
        {
            GUILayout.Label("<b>Velocidade do jogo</b>", small);
            int current = Mathf.Max(0, System.Array.IndexOf(TimeScales, Time.timeScale));
            int chosen = GUILayout.Toolbar(current, TimeLabels);
            if (chosen != current) Time.timeScale = TimeScales[chosen];
        }
    }
}
