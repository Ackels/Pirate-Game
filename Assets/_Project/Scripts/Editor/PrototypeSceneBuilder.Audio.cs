using System.Collections.Generic;
using System.Linq;
using Pirates.Ambient;
using Pirates.Audio;
using Pirates.CameraSystem;
using Pirates.Controls;
using Pirates.Ships;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Áudio das cenas (M6.8): sons em Data/Audio (um SoundCue por som, preservados se já existem: são ajuste de game
    /// design; só ganham os clipes se estiverem vazios), o objeto "Audio" (SoundPlayer, GameAudio, MusicDirector), o
    /// ouvido no alvo da câmera (ListenerAnchor; o AudioListener sai da câmera) e o ambiente: ondas nas praias de cada
    /// ilha, gaivotas, fogo nas fogueiras e na forja, pássaros nas matas. Arquivos em Assets/ThirdParty/Audio (CC0,
    /// CREDITOS.txt), importados pelo ThirdPartyImportSettings. Volumes equilibrados pelo RMS medido de cada clipe (efeitos
    /// entre −16 e −26 dB efetivos; passos e interface abaixo da luta; ambiente bem abaixo).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string AudioRoot = ThirdPartyImportSettings.AudioFolder;
        const string AudioDataFolder = DataFolder + "/Audio";

        static AudioClip[] Clips(string folder, params string[] files) =>
            files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/{folder}/{f}")).Where(c => c).ToArray();

        /// <summary>Clipes numerados: <paramref name="pattern"/> com {0} (ex.: "footstep_grass_{0:000}.ogg"), de a até b.</summary>
        static AudioClip[] Numbered(string folder, string pattern, int from, int to) =>
            Clips(folder, Enumerable.Range(from, to - from + 1).Select(i => string.Format(pattern, i)).ToArray());

        static AudioClip[] Join(params AudioClip[][] sets) => sets.SelectMany(s => s).ToArray();

        static SoundCue Cue(string name, AudioClip[] clips, SoundChannel channel, Vector2 volume, Vector2 pitch, bool spatial,
            float minDistance, float maxDistance, float cooldown = 0f)
        {
            var cue = LoadOrCreateAsset<SoundCue>($"{AudioDataFolder}/Som_{name}.asset", c =>
            {
                c.channel = channel;
                c.volume = volume;
                c.pitch = pitch;
                c.spatial = spatial;
                c.minDistance = minDistance;
                c.maxDistance = maxDistance;
                c.cooldown = cooldown;
            });
            if (!cue.HasClips && clips.Length > 0)
            {
                cue.clips = clips;
                EditorUtility.SetDirty(cue);
            }
            if (clips.Length == 0) Debug.LogWarning($"[Pirates] Som_{name}: nenhum clipe encontrado em {AudioRoot}.");
            return cue;
        }

        /// <summary>
        /// Som que antes usava clipes feitos pelo gerador (em <c>_Project/Audio</c>, chuva e trovão até 09/10): troca pelas gravações. Um
        /// som com clipes escolhidos à mão fica como está.
        /// </summary>
        static SoundCue Recorded(SoundCue cue, AudioClip[] clips)
        {
            if (clips.Length == 0 || cue.clips == null) return cue;
            bool generated = cue.clips.Length == 0 || cue.clips.Any(c => !c || AssetDatabase.GetAssetPath(c).StartsWith(GeneratedAudioFolder + "/"));
            if (!generated) return cue;
            cue.clips = clips;
            EditorUtility.SetDirty(cue);
            return cue;
        }

        /// <summary>
        /// Estampidos mais fortes (09/10, pedido do Jhonata: "mais impacto"): troca os clipes originais pelas versões de
        /// <c>Efeitos/Forte</c> (os mesmos CC0 com bum grave, eco curto e compressão, feitos por script) e ajusta tom/alcance uma vez.
        /// Depois disso o som fica como estiver no asset.
        /// </summary>
        static SoundCue Strong(SoundCue cue, AudioClip[] clips, Vector2 pitch, float maxDistance)
        {
            if (clips.Length == 0 || cue.clips == null) return cue;
            if (cue.clips.Length > 0 && cue.clips.All(c => c && AssetDatabase.GetAssetPath(c).Contains("/Efeitos/Forte/"))) return cue;
            cue.clips = clips;
            cue.volume = new Vector2(0.9f, 1f);
            cue.pitch = pitch;
            cue.maxDistance = maxDistance;
            EditorUtility.SetDirty(cue);
            return cue;
        }

        /// <summary>Troca uma vez os clipes do som pelos tinidos sintetizados em <c>_Project/Audio/Espadas/{prefixo}_N.wav</c> (volume e tom juntos).</summary>
        static SoundCue Blades(SoundCue cue, string prefix, int count, Vector2 volume, Vector2 pitch)
        {
            var clips = Enumerable.Range(1, count)
                .Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>($"{GeneratedAudioFolder}/Espadas/{prefix}_{i}.wav")).Where(c => c).ToArray();
            if (clips.Length == 0 || cue.clips == null) return cue;
            if (cue.clips.Length > 0 && cue.clips.All(c => c && AssetDatabase.GetAssetPath(c).Contains("/Espadas/"))) return cue;
            cue.clips = clips;
            cue.volume = volume;
            cue.pitch = pitch;
            EditorUtility.SetDirty(cue);
            return cue;
        }

        const string PiratePack = "Musica/PirateMusicPack", InnPack = "Musica/FC_PirateMusicLite";

        /// <summary>
        /// Música com várias faixas (11/10): põe as faixas e o volume de cada uma uma vez só (enquanto o som não tem volumes por
        /// faixa). Depois o asset é do Jhonata: trocar, tirar ou pôr faixas e volumes no Inspector.
        /// </summary>
        static SoundCue Playlist(SoundCue cue, params (string folder, string file, float volume)[] tracks)
        {
            if (cue.clipVolumes != null && cue.clipVolumes.Length > 0) return cue;
            var found = tracks.Select(t => (clip: Clips(t.folder, t.file).FirstOrDefault(), t.volume)).Where(t => t.clip).ToArray();
            if (found.Length == 0) return cue;
            cue.clips = found.Select(t => t.clip).ToArray();
            cue.clipVolumes = found.Select(t => t.volume).ToArray();
            cue.volume = new Vector2(1f, 1f);
            EditorUtility.SetDirty(cue);
            return cue;
        }

        static GameSounds LoadSounds()
        {
            EnsureFolder(DataFolder, "Audio");
            const SoundChannel fx = SoundChannel.Effects, amb = SoundChannel.Ambient, ui = SoundChannel.Interface, music = SoundChannel.Music;
            const string imp = "Kenney_ImpactSounds", rpg = "Kenney_RPGAudio", gui = "Kenney_InterfaceSounds", jin = "Kenney_MusicJingles";
            var waves = Numbered("Ambiente", "wave_0{0}_cc0-18363__jasinski__alkaibeach.wav", 1, 4);
            var screams = Numbered("Efeitos", "slightscream-{0:00}.wav", 1, 15);
            Vector2 V(float a, float b) => new(a, b);
            return new GameSounds
            {
                cannonFire = Strong(Cue("CanhaoTiro", Join(Clips("Efeitos", "cannon_fire.ogg"), Numbered("Efeitos", "cannon_{0:00}.ogg", 1, 5)), fx,
                    V(0.85f, 1f), V(0.88f, 1.06f), true, 20f, 300f), Numbered("Efeitos/Forte", "Canhao_{0}.wav", 1, 6), V(0.88f, 1.06f), 380f),
                cannonSplash = Cue("CanhaoAgua", waves, fx, V(0.85f, 1f), V(1.35f, 1.6f), true, 10f, 160f),
                cannonImpact = Cue("CanhaoImpacto", Join(Numbered(imp, "impactWood_heavy_{0:000}.ogg", 0, 4), Numbered(imp, "impactPlank_medium_{0:000}.ogg", 0, 4)), fx,
                    V(0.85f, 1f), V(0.8f, 1f), true, 10f, 200f),
                // Barril de pólvora (pacote A, 09/10): os estouros mais graves do pacote CC0 + o canhão, um pouco mais grave.
                // Tom menos grave que antes (0,7–0,85): o bum novo já dá o peso, e grave demais some na caixinha do notebook.
                explosion = Strong(Cue("Explosao", Join(Clips("Efeitos", "bang_06.ogg", "bang_08.ogg", "bang_10.ogg"), Clips("Efeitos", "cannon_fire.ogg")), fx,
                    V(0.95f, 1f), V(0.7f, 0.85f), true, 15f, 220f), Numbered("Efeitos/Forte", "Explosao_{0}.wav", 1, 4), V(0.8f, 0.92f), 280f),
                shipSinking = Cue("NavioAfundando", Clips("Efeitos", "bang_06.ogg", "bang_10.ogg", "fw_04.ogg"), fx, V(0.9f, 1f), V(0.75f, 0.9f), true, 25f, 300f),
                sails = Cue("Velas", Join(Numbered(rpg, "cloth{0}.ogg", 1, 4), Clips(rpg, "clothBelt.ogg", "clothBelt2.ogg")), fx, V(0.55f, 0.7f), V(0.75f, 0.9f), true, 6f, 60f),
                anchor = Cue("Ancora", Join(Numbered(imp, "impactMetal_heavy_{0:000}.ogg", 0, 4), Clips(rpg, "metalPot1.ogg")), fx, V(0.7f, 0.85f), V(0.7f, 0.85f), true, 6f, 60f),
                shipCreak = Cue("Rangido", Clips("Ambiente", "tree_creak.wav"), fx, V(0.85f, 1f), V(0.75f, 1f), true, 4f, 35f),
                harborBell = Cue("SinoPorto", Numbered(imp, "impactBell_heavy_{0:000}.ogg", 0, 1), fx, V(0.65f, 0.75f), V(0.95f, 1f), true, 15f, 160f),
                seaWaves = Cue("MarAberto", waves, amb, V(0.4f, 0.55f), V(0.85f, 1f), false, 1f, 2f),

                stepSand = Cue("PassoAreia", Numbered(imp, "footstep_snow_{0:000}.ogg", 0, 4), fx, V(0.28f, 0.35f), V(0.9f, 1.1f), true, 1.5f, 22f),
                stepGrass = Cue("PassoGrama", Numbered(imp, "footstep_grass_{0:000}.ogg", 0, 4), fx, V(0.45f, 0.55f), V(0.9f, 1.1f), true, 1.5f, 22f),
                stepDirt = Cue("PassoTerra", Numbered(rpg, "footstep{0:00}.ogg", 0, 9), fx, V(0.38f, 0.45f), V(0.9f, 1.1f), true, 1.5f, 22f),
                stepRock = Cue("PassoPedra", Numbered(imp, "footstep_concrete_{0:000}.ogg", 0, 4), fx, V(0.45f, 0.55f), V(0.9f, 1.1f), true, 1.5f, 22f),
                stepWood = Cue("PassoMadeira", Numbered(imp, "footstep_wood_{0:000}.ogg", 0, 4), fx, V(0.38f, 0.45f), V(0.9f, 1.1f), true, 1.5f, 22f),
                stepWater = Cue("PassoAgua", waves, fx, V(0.12f, 0.2f), V(1.6f, 1.9f), true, 1.5f, 18f),
                // Rolamento (pacote A, 09/10): roupa e o baque do corpo no chão.
                dodge = Cue("Rolamento", Join(Numbered(rpg, "cloth{0}.ogg", 1, 4), Numbered(imp, "impactSoft_medium_{0:000}.ogg", 0, 4)), fx,
                    V(0.5f, 0.6f), V(0.85f, 1f), true, 1.5f, 20f),
                jump = Cue("Pulo", Numbered(rpg, "cloth{0}.ogg", 1, 4), fx, V(0.4f, 0.5f), V(1f, 1.2f), true, 1.5f, 15f),
                // Chute (pacote A, 09/10): baque forte de soco.
                kick = Cue("Chute", Numbered(imp, "impactPunch_heavy_{0:000}.ogg", 0, 4), fx, V(0.75f, 0.9f), V(0.8f, 0.95f), true, 2f, 35f),
                // Defesa (pacote A, passos 5 e 6): tinidos de lâmina sintetizados (Tools/Audio/espadas.py → _Project/Audio/Espadas); sem os
                // arquivos, o metal do Kenney (pancada surda: "não parece espadas se batendo").
                block = Blades(Cue("Defesa", Numbered(imp, "impactMetal_medium_{0:000}.ogg", 0, 4), fx, V(0.75f, 0.9f), V(1f, 1.15f), true, 2f, 35f),
                    "Defesa", 5, V(0.8f, 0.95f), V(0.94f, 1.08f)),
                parry = Blades(Cue("Aparar", Numbered(imp, "impactMetal_heavy_{0:000}.ogg", 0, 4), fx, V(0.95f, 1f), V(1.25f, 1.4f), true, 3f, 45f),
                    "Aparar", 4, V(0.95f, 1f), V(0.97f, 1.04f)),
                swordSwing = Cue("CuteloGolpe", Clips(rpg, "knifeSlice.ogg", "knifeSlice2.ogg"), fx, V(0.65f, 0.8f), V(0.85f, 1.05f), true, 2f, 30f),
                swordHit = Cue("CuteloAcerto", Join(Numbered(imp, "impactPunch_medium_{0:000}.ogg", 0, 4), Numbered(imp, "impactPunch_heavy_{0:000}.ogg", 0, 4)), fx,
                    V(0.6f, 0.75f), V(0.9f, 1.05f), true, 2f, 35f),
                pistolShot = Strong(Cue("PistolaTiro", Join(Numbered("Efeitos", "shot_{0:00}.ogg", 1, 3), Numbered("Efeitos", "bang_{0:00}.ogg", 1, 3)), fx,
                    V(0.8f, 0.95f), V(0.95f, 1.12f), true, 6f, 140f), Numbered("Efeitos/Forte", "Tiro_{0}.wav", 1, 6), V(0.95f, 1.12f), 160f),
                pistolReload = Cue("PistolaRecarga", Clips(rpg, "metalClick.ogg", "metalLatch.ogg"), fx, V(0.55f, 0.65f), V(0.95f, 1.05f), true, 1.5f, 18f),
                drawSword = Cue("SacarCutelo", Numbered(rpg, "drawKnife{0}.ogg", 1, 3), fx, V(0.55f, 0.65f), V(0.85f, 1f), true, 1.5f, 18f),
                drawPistol = Cue("SacarPistola", Clips(rpg, "beltHandle1.ogg", "beltHandle2.ogg", "handleSmallLeather.ogg"), fx, V(0.55f, 0.65f), V(0.95f, 1.05f), true, 1.5f, 18f),
                pain = Cue("Dor", screams.Where((_, i) => i is not (4 or 9 or 14)).ToArray(), fx, V(0.45f, 0.55f), V(0.95f, 1.05f), true, 3f, 40f, 0.12f),
                death = Cue("Morte", Numbered("Efeitos", "slightscream-{0:00}.wav", 5, 5).Concat(Numbered("Efeitos", "slightscream-{0:00}.wav", 10, 10))
                    .Concat(Numbered("Efeitos", "slightscream-{0:00}.wav", 15, 15)).ToArray(), fx, V(0.6f, 0.7f), V(0.8f, 0.9f), true, 4f, 50f),
                bodyFall = Cue("Queda", Numbered(imp, "impactSoft_heavy_{0:000}.ogg", 0, 4), fx, V(0.55f, 0.65f), V(0.85f, 1f), true, 2f, 25f),
                crateBreak = Cue("CaixoteQuebra", Join(Numbered(imp, "impactWood_heavy_{0:000}.ogg", 0, 4), Numbered(imp, "impactPlank_medium_{0:000}.ogg", 0, 4)), fx,
                    V(0.75f, 0.9f), V(1f, 1.15f), true, 3f, 40f),
                dummyHit = Cue("BonecoAcerto", Numbered(imp, "impactSoft_medium_{0:000}.ogg", 0, 4), fx, V(0.45f, 0.55f), V(0.9f, 1.05f), true, 2f, 30f),
                chestOpen = Cue("BauAbre", Clips(rpg, "doorOpen_2.ogg", "creak2.ogg"), fx, V(0.65f, 0.75f), V(0.9f, 1f), true, 2f, 25f),
                dig = Cue("Cavar", Numbered(imp, "impactMining_{0:000}.ogg", 0, 4), fx, V(0.7f, 0.8f), V(0.85f, 1f), true, 2f, 30f),
                pickup = Cue("Saque", Clips(rpg, "handleCoins.ogg", "handleCoins2.ogg", "handleSmallLeather.ogg", "dropLeather.ogg"), ui,
                    V(0.45f, 0.55f), V(0.95f, 1.05f), false, 1f, 2f, 0.06f),
                backpackFull = Cue("MochilaCheia", Numbered(gui, "error_{0:000}.ogg", 4, 4), ui, V(0.5f, 0.5f), V(1f, 1f), false, 1f, 2f, 1f),

                doorOpen = Cue("PortaAbre", Numbered(rpg, "doorOpen_{0}.ogg", 1, 2), ui, V(0.5f, 0.6f), V(0.95f, 1.05f), false, 1f, 2f),
                doorClose = Cue("PortaFecha", Numbered(rpg, "doorClose_{0}.ogg", 1, 4), ui, V(0.45f, 0.55f), V(0.95f, 1.05f), false, 1f, 2f),
                cargoMoved = Cue("CargaMovida", Clips(rpg, "handleSmallLeather.ogg", "handleSmallLeather2.ogg", "dropLeather.ogg", "bookPlace1.ogg"), ui,
                    V(0.4f, 0.5f), V(0.95f, 1.05f), false, 1f, 2f, 0.07f),
                repair = Cue("Conserto", Numbered(imp, "impactWood_light_{0:000}.ogg", 0, 4), ui, V(0.55f, 0.65f), V(0.9f, 1.1f), false, 1f, 2f),
                purchase = Cue("Compra", Clips(rpg, "handleCoins.ogg", "handleCoins2.ogg"), ui, V(0.55f, 0.65f), V(0.95f, 1.05f), false, 1f, 2f),
                buildDone = Cue("ObraPronta", Clips(jin, "jingles_PIZZI02.ogg", "jingles_PIZZI06.ogg"), ui, V(0.6f, 0.6f), V(1f, 1f), false, 1f, 2f, 1f),
                missionAccepted = Cue("MissaoAceita", Clips(jin, "jingles_STEEL00.ogg"), ui, V(0.6f, 0.6f), V(1f, 1f), false, 1f, 2f, 1f),
                missionDone = Cue("MissaoCumprida", Clips(jin, "jingles_STEEL05.ogg"), ui, V(0.65f, 0.65f), V(1f, 1f), false, 1f, 2f, 1f),
                missionPaid = Cue("MissaoPaga", Clips(jin, "jingles_STEEL10.ogg"), ui, V(0.65f, 0.65f), V(1f, 1f), false, 1f, 2f, 1f),
                gameSaved = Cue("JogoSalvo", Numbered(gui, "confirmation_{0:000}.ogg", 1, 1), ui, V(0.2f, 0.2f), V(1f, 1f), false, 1f, 2f, 2f),

                fire = Cue("Fogo", Clips("Ambiente", "fire-1.wav"), amb, V(0.75f, 0.85f), V(0.9f, 1.1f), true, 1.5f, 14f),
                jungleBirds = Cue("Passaros", Clips("Ambiente", "birds-isaiah658.ogg"), amb, V(0.45f, 0.55f), V(0.95f, 1.05f), true, 8f, 45f),
                seagulls = Cue("Gaivotas", Join(Numbered("Ambiente", "Seagull Ambient {0}.wav", 1, 7), Clips("Ambiente", "Seagull Big Ambient.wav", "Seagull Big Ambient 2.wav")), amb,
                    V(0.45f, 0.6f), V(0.92f, 1.08f), true, 10f, 70f),
                shoreWaves = Cue("OndasPraia", waves, amb, V(0.5f, 0.65f), V(0.85f, 1.05f), true, 6f, 32f),
                // M11: loop feito pelo gerador (PrototypeSceneBuilder.Weather.cs); volume e tom no WindAudio, pela força do vento.
                wind = Cue("Vento", new[] { WindLoopClip() }, amb, V(0.55f, 0.55f), V(1f, 1f), false, 1f, 2f),
                // M11 (clima): chuva e tempestade em loop (gravações CC0 cortadas e emendadas) e trovões; volume no WeatherAudio.
                rain = Recorded(Cue("Chuva", Clips("Clima", "Chuva_Loop.wav"), amb, V(0.6f, 0.6f), V(1f, 1f), false, 1f, 2f), Clips("Clima", "Chuva_Loop.wav")),
                storm = Cue("Tempestade", Clips("Clima", "Tempestade_Loop.wav"), amb, V(0.6f, 0.6f), V(1f, 1f), false, 1f, 2f),
                thunder = Recorded(Cue("Trovao", Numbered("Clima", "Trovao_{0}.wav", 1, 5), fx, V(0.85f, 1f), V(0.9f, 1.05f), false, 1f, 2f),
                    Numbered("Clima", "Trovao_{0}.wav", 1, 5)),
                // C5: ar das catacumbas feito pelo gerador (PrototypeSceneBuilder.Catacombs.cs); volume no WeatherAudio.
                crypt = Cue("Catacumba", new[] { CryptLoopClip() }, amb, V(0.7f, 0.7f), V(1f, 1f), false, 1f, 2f),
                ghostMoan = Cue("AlmaPenada", new[] { GhostMoanClip() }, fx, V(0.75f, 0.85f), V(0.92f, 1.08f), true, 2f, 26f),
                batScreech = Cue("Morcego", new[] { BatScreechClip() }, fx, V(0.6f, 0.7f), V(0.9f, 1.12f), true, 1.5f, 20f),

                // 11/10: músicas do Jhonata (Asset Store, grátis) somadas às de antes; cada som vira uma lista (MusicDirector) e o
                // volume de cada faixa iguala o RMS medido (mar −29 dB, porto/ilha/menu −28, combate −26; pirate1uf já fica abaixo).
                musicTitle = Playlist(Cue("MusicaMenu", Clips(InnPack, "Adventure Inn Full Mix.wav"), music, V(1f, 1f), V(1f, 1f), false, 1f, 2f),
                    (InnPack, "Adventure Inn Full Mix.wav", 0.92f)),
                musicPort = Playlist(Cue("MusicaPorto", Clips("Musica", "TownTheme.mp3"), music, V(0.8f, 0.8f), V(1f, 1f), false, 1f, 2f),
                    ("Musica", "TownTheme.mp3", 0.24f), (PiratePack, "Pirate 7.wav", 0.83f), (PiratePack, "Pirate 3.wav", 0.71f)),
                musicSea = Playlist(Cue("MusicaMar", Clips("Musica", "pirate1uf.ogg"), music, V(0.7f, 0.7f), V(1f, 1f), false, 1f, 2f),
                    ("Musica", "pirate1uf.ogg", 1f), (PiratePack, "Pirate 1.wav", 0.65f), (PiratePack, "Pirate 2.wav", 0.51f),
                    (PiratePack, "Pirate 6.wav", 0.59f)),
                musicIsland = Playlist(Cue("MusicaIlha", Clips("Musica", "Exploration Theme.ogg"), music, V(0.9f, 0.9f), V(1f, 1f), false, 1f, 2f),
                    ("Musica", "Exploration Theme.ogg", 0.9f), (PiratePack, "Pirate 5.wav", 0.84f)),
                musicCombat = Playlist(Cue("MusicaCombate", Clips("Musica", "Battle.mp3"), music, V(0.8f, 0.8f), V(1f, 1f), false, 1f, 2f),
                    ("Musica", "Battle.mp3", 0.29f), (PiratePack, "Pirate 8.wav", 0.75f), (PiratePack, "Pirate 4.wav", 1f)),
            };
        }

        /// <summary>Objeto "Audio" da cena, o ouvido no alvo da câmera e os sons de ambiente. Vai por último (acha tudo pronto).</summary>
        static void AddAudio(Camera camera, ShipController playerShip)
        {
            var sounds = LoadSounds();
            var settings = LoadOrCreateAsset<SoundSettings>(AudioDataFolder + "/SoundSettings.asset");

            var root = new GameObject("Audio");
            root.AddComponent<SoundPlayer>();
            root.AddComponent<GameAudio>().Configure(sounds, settings);
            root.AddComponent<WindAudio>().Configure(sounds.wind);
            root.AddComponent<WeatherAudio>().Configure(sounds.rain, sounds.storm, sounds.thunder, sounds.crypt);
            root.AddComponent<MusicDirector>().Configure(sounds.musicTitle, sounds.musicPort, sounds.musicSea, sounds.musicIsland, sounds.musicCombat,
                Object.FindAnyObjectByType<PlayerModeSwitcher>(), playerShip ? playerShip.transform : null);

            // Um ouvido só: sai da câmera e passa a seguir o navio/personagem.
            if (camera.TryGetComponent(out AudioListener old)) Object.DestroyImmediate(old);
            var ear = new GameObject("Ouvido");
            ear.AddComponent<AudioListener>();
            ear.AddComponent<ListenerAnchor>().Configure(camera.GetComponent<ElevatedCameraRig>());

            AddAmbience(sounds);
        }

        static void AddAmbience(GameSounds sounds)
        {
            var ambience = new GameObject("Ambiente_Sons").transform;

            // Ondas quebrando nas praias de cada ilha (a cada ~24 m de litoral).
            foreach (var name in new[] { "IlhaGrande", "Ilha_Sul", "Ilha_Norte", "Ilha_Oeste" })
            {
                var island = GameObject.Find(name)?.transform;
                if (!island) continue;
                var placed = new List<Vector3>();
                for (int k = 0; k < 96; k++)
                {
                    float a = k * Mathf.PI * 2f / 96f;
                    var shore = ShoreAlong(island, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    if (shore == null || placed.Any(p => (p - shore.Value).sqrMagnitude < 24f * 24f)) continue;
                    placed.Add(shore.Value);
                    var go = new GameObject($"Ondas_{name}_{placed.Count}");
                    go.transform.SetParent(ambience, false);
                    go.transform.position = shore.Value + Vector3.up * 0.4f;
                    go.AddComponent<AmbientEmitter>().Configure(sounds.shoreWaves, false, new Vector2(2.5f, 6f), 1f);
                }
            }

            // Gaivotas: cada uma grasna de vez em quando.
            foreach (var gull in Object.FindObjectsByType<CirclingBird>())
                gull.gameObject.AddComponent<AmbientEmitter>().Configure(sounds.seagulls, false, new Vector2(7f, 20f), 1f);

            // Fogo: cada sistema "Fogo" (fogueiras, forja, braseiros) crepita em loop.
            foreach (var fire in Object.FindObjectsByType<ParticleSystem>().Where(p => p.name == "Fogo"))
                fire.gameObject.AddComponent<AmbientEmitter>().Configure(sounds.fire, true, Vector2.one, 1f);

            // Pássaros nas matas: Caveira (pela ilha toda) e os morros do porto.
            var skull = GameObject.Find("IlhaGrande")?.transform;
            if (skull)
                foreach (var p in new[] { new Vector2(-40f, 40f), new Vector2(45f, 50f), new Vector2(-70f, 5f), new Vector2(60f, -30f),
                             new Vector2(-30f, -60f), new Vector2(20f, -80f), new Vector2(95f, 40f), new Vector2(-95f, -20f), new Vector2(10f, 60f) })
                    Birds(ambience, sounds, skull, p, "Caveira");
            var port = GameObject.Find("Ilha_Sul")?.transform;
            if (port)
                foreach (var p in new[] { new Vector2(46f, -20f), new Vector2(-46f, -30f), new Vector2(-14f, -48f), new Vector2(15f, -15f) })
                    Birds(ambience, sounds, port, p, "Porto");
            var west = GameObject.Find("Ilha_Oeste")?.transform;
            if (west)
                foreach (var p in new[] { new Vector2(-8f, 2f), new Vector2(-24f, -6f), new Vector2(18f, -12f) })
                    Birds(ambience, sounds, west, p, "Oeste");
            var north = GameObject.Find("Ilha_Norte")?.transform;
            if (north)
                foreach (var p in new[] { new Vector2(-15f, 12f), new Vector2(22f, 18f), new Vector2(-34f, -10f), new Vector2(5f, 28f) })
                    Birds(ambience, sounds, north, p, "Norte");
        }

        static void Birds(Transform parent, GameSounds sounds, Transform island, Vector2 local, string where)
        {
            var world = island.TransformPoint(new Vector3(local.x, 0f, local.y));
            if (!IslandGround(island, world, out var ground, out _)) return;
            var go = new GameObject($"Passaros_{where}_{local.x:0}_{local.y:0}");
            go.transform.SetParent(parent, false);
            go.transform.position = ground + Vector3.up * 6f;
            go.AddComponent<AmbientEmitter>().Configure(sounds.jungleBirds, true, Vector2.one, 1f);
        }

        /// <summary>Litoral na direção dada a partir do centro da ilha: o último ponto com chão ≥ 0,3 m (null se não achou).</summary>
        static Vector3? ShoreAlong(Transform island, Vector2 direction)
        {
            Vector3? last = null;
            for (float r = 0f; r < 220f; r += 1f)
            {
                var world = island.position + new Vector3(direction.x, 0f, direction.y) * r;
                bool land = IslandGround(island, world, out var ground, out _) && ground.y - island.position.y >= 0.3f;
                if (land) last = ground;
                else if (last != null) return new Vector3(last.Value.x, island.position.y, last.Value.z);
            }
            return null;
        }
    }
}
