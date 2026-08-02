using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using LighthouseExtends.TextTable;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// StreamingAssets上のTSVから言語別テキスト表を読み込む
    /// </summary>
    public sealed class StreamingAssetsTextTableLoader : ITextTableLoader
    {
        private const string FolderName = "TextTables";

        /// <inheritdoc/>
        public async UniTask<IReadOnlyDictionary<string, string>> LoadAsync(
            string languageCode,
            CancellationToken cancellationToken)
        {
            var table = new Dictionary<string, string>();
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            if (!Directory.Exists(folder))
            {
                Debug.LogError(
                    $"[StreamingAssetsTextTableLoader] TextTableフォルダがありません path={folder}");
                return table;
            }

            // ハイフン付き言語コードも対象に含める
            string[] files = Directory.GetFiles(folder, "*.tsv");
            int loadedFileCount = 0;
            for (int i = 0; i < files.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileNameWithoutExtension(files[i]);
                if (!MatchesLanguage(fileName, languageCode))
                {
                    continue;
                }

                LoadFile(files[i], table);
                loadedFileCount++;
            }

            if (loadedFileCount == 0)
            {
                Debug.LogError(
                    $"[StreamingAssetsTextTableLoader] 言語TSVがありません language={languageCode} path={folder}");
            }
            else
            {
                Debug.Log(
                    $"[StreamingAssetsTextTableLoader] 読込 language={languageCode} files={loadedFileCount} keys={table.Count}");
            }

            await UniTask.CompletedTask;
            return table;
        }

        private static bool MatchesLanguage(string fileNameWithoutExtension, string languageCode)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension) || string.IsNullOrEmpty(languageCode))
            {
                return false;
            }

            // Ui.ja / Ui.zh-Hans / Ui.pt-BR
            int lastDot = fileNameWithoutExtension.LastIndexOf('.');
            if (lastDot < 0 || lastDot >= fileNameWithoutExtension.Length - 1)
            {
                return false;
            }

            string fileLanguage = fileNameWithoutExtension.Substring(lastDot + 1);
            return string.Equals(fileLanguage, languageCode, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadFile(string filePath, Dictionary<string, string> table)
        {
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (i == 0 && IsHeader(line))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                int tab = line.IndexOf('\t');
                if (tab < 0)
                {
                    continue;
                }

                string key = line.Substring(0, tab).Trim('\uFEFF', '\0');
                string text = Unescape(line.Substring(tab + 1));
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                table[key] = text;
            }
        }

        private static bool IsHeader(string line)
        {
            return line.StartsWith("key\t", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(line, "key\ttext", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\r", "\r");
        }
    }
}
