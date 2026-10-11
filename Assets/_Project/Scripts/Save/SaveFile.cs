using System;
using System.IO;
using UnityEngine;

namespace Pirates.Save
{
    /// <summary>
    /// O arquivo do jogo salvo: um só, save.json em Application.persistentDataPath. Grava num temporário e troca (um
    /// travamento no meio não estraga o save), deixando o anterior em save.json.bak. "Novo jogo" também só move o
    /// save para o .bak: dá para voltar renomeando.
    /// </summary>
    public static class SaveFile
    {
        public const string FileName = "save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        public static string BackupPath => FilePath + ".bak";
        public static bool Exists => File.Exists(FilePath);

        public static void Write(SaveData data)
        {
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint: true));
            if (File.Exists(path)) File.Replace(temp, path, BackupPath);
            else File.Move(temp, path);
        }

        /// <summary>Lê o save. false, com o motivo, se não existe, está danificado ou é de uma versão mais nova.</summary>
        public static bool TryRead(out SaveData data, out string error)
        {
            data = null;
            error = null;
            if (!Exists)
            {
                error = "nenhum jogo salvo";
                return false;
            }
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Não deu para ler {FilePath}: {e.Message}");
                error = "o arquivo está danificado";
                return false;
            }
            if (data == null)
            {
                error = "o arquivo está vazio";
                return false;
            }
            if (data.version > SaveData.CurrentVersion)
            {
                error = $"é de uma versão mais nova do jogo (v{data.version})";
                data = null;
                return false;
            }
            return true;
        }

        /// <summary>Jogo de antes do último "Novo jogo": fica fora da troca do .bak (cada salvamento sobrescreve o .bak).</summary>
        public static string PreviousGamePath => Path.Combine(Path.GetDirectoryName(FilePath), "save_anterior.json");

        /// <summary>
        /// Tira o save do caminho (Novo jogo). Vai para <see cref="PreviousGamePath"/> (11/10): antes ia para o .bak, que o segundo
        /// salvamento do jogo novo já sobrescrevia, e o progresso antigo se perdia. Depois disso não há jogo para continuar.
        /// </summary>
        public static void Archive()
        {
            if (!Exists) return;
            if (File.Exists(PreviousGamePath)) File.Delete(PreviousGamePath);
            File.Move(FilePath, PreviousGamePath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
        }
    }
}
