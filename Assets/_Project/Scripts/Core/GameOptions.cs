using System;
using System.IO;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Opções do jogador (menu Opções): volumes, tela e gráficos, câmera. Ficam em <c>opcoes.json</c> ao lado do save (não
    /// fazem parte do progresso: "Novo jogo" não mexe nelas). Quem usa lê <see cref="Current"/> e ouve <see cref="Changed"/>
    /// (o som aplica os volumes; a UI aplica tela e gráficos; a câmera lê a sensibilidade). Sem o arquivo, os volumes vêm do
    /// SoundSettings e os gráficos ficam como o projeto está (qualidade Alta = sem mudança).
    /// </summary>
    [Serializable]
    public class GameOptions
    {
        public enum Quality { Low, Medium, High }

        public const int DefaultScreenMode = -1;

        [Tooltip("Falso: os volumes ainda não foram mexidos (vêm do SoundSettings).")]
        public bool hasAudio;
        public float master = 1f, music = 0.3f, effects = 1f, ambient = 0.5f, interfaceVolume = 0.7f;

        [Tooltip("FullScreenMode (0 tela cheia exclusiva, 1 janela sem borda, 3 janela); −1 = como o jogo abriu.")]
        public int screenMode = DefaultScreenMode;
        [Tooltip("Resolução escolhida (0 = a da tela).")]
        public int width, height;
        public bool vSync = true;
        [Tooltip("Limite de quadros com o VSync desligado (0 = sem limite).")]
        public int fpsLimit;
        public Quality quality = Quality.High;

        public float cameraSensitivity = 1f;
        public bool invertCamera;

        static GameOptions current;

        /// <summary>Opções em uso (lidas do disco na primeira vez).</summary>
        public static GameOptions Current => current ??= Load();

        /// <summary>Alguma opção mudou (já aplicada em <see cref="Current"/>).</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "opcoes.json");

        static GameOptions Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<GameOptions>(File.ReadAllText(FilePath));
                    if (loaded != null) return loaded.Clamped();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Pirates] Opções não lidas ({e.Message}): usando as padrão.");
            }
            return new GameOptions();
        }

        GameOptions Clamped()
        {
            master = Mathf.Clamp01(master);
            music = Mathf.Clamp01(music);
            effects = Mathf.Clamp01(effects);
            ambient = Mathf.Clamp01(ambient);
            interfaceVolume = Mathf.Clamp01(interfaceVolume);
            cameraSensitivity = Mathf.Clamp(cameraSensitivity, 0.25f, 2.5f);
            fpsLimit = Mathf.Max(0, fpsLimit);
            return this;
        }

        /// <summary>Avisa quem aplica (a cada passo do botão, sem gravar).</summary>
        public static void Notify()
        {
            Current.Clamped();
            Changed?.Invoke();
        }

        /// <summary>Grava no disco (ao sair da tela de opções).</summary>
        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Pirates] Opções não gravadas: {e.Message}");
            }
        }

        // Suporte a "Enter Play Mode" sem domain reload: relê do disco.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            Changed = null;
        }
    }

    /// <summary>
    /// A tela de título está na frente (menu inicial ou prólogo): a música do jogo dá lugar à do menu. Quem mostra liga; quem
    /// esconde desliga.
    /// </summary>
    public static class TitleScreen
    {
        public static bool Showing { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Showing = false;
    }
}
