using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SaveData;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペット用スプライトのディスクキャッシュ
    /// </summary>
    public static class DesktopPetSpriteCache
    {
        public const int FormatVersion = 12;
        public const int RequiredClipCount = 8;
        private const string RootFolderName = "DesktopPetCache";
        private const string ManifestFileName = "manifest.txt";
        private const string ActiveMarkerFileName = "active.txt";
        private const string LanguageMarkerFileName = "language.txt";
        private const string LauncherKeepAliveFileName = "launcher_keepalive.txt";

        /// <summary>
        /// キャッシュルートディレクトリ
        /// </summary>
        public static string RootDirectory =>
            Path.Combine(ModelSaveStorage.WritableRoot, RootFolderName);

        /// <summary>
        /// スロット用キャッシュディレクトリ
        /// </summary>
        public static string GetSlotDirectory(int playerSlotIndex) =>
            Path.Combine(RootDirectory, "slot_" + playerSlotIndex.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// 指定スロットのキャッシュを削除する
        /// </summary>
        public static void DeleteSlotCache(int playerSlotIndex)
        {
            if (playerSlotIndex < 0)
            {
                return;
            }

            string directory = GetSlotDirectory(playerSlotIndex);
            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetSpriteCache] キャッシュ削除に失敗しました slot="
                    + playerSlotIndex
                    + " "
                    + exception.Message);
            }
        }

        /// <summary>
        /// ディスク上のキャッシュが外部ビューア起動に使えるか(画像は読まない)
        /// </summary>
        public static bool IsReady(int playerSlotIndex, string glbFileName)
        {
            if (string.IsNullOrEmpty(glbFileName))
            {
                return false;
            }

            if (!TryResolveSourceStamp(glbFileName, out long sourceTicks, out long sourceLength))
            {
                return false;
            }

            string directory = GetSlotDirectory(playerSlotIndex);
            string manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            if (!TryReadManifest(
                    manifestPath,
                    out int version,
                    out int slot,
                    out string cachedGlb,
                    out long cachedTicks,
                    out long cachedLength,
                    out Dictionary<(int facing, int action), List<string>> clips))
            {
                return false;
            }

            if (version != FormatVersion
                || slot != playerSlotIndex
                || !string.Equals(cachedGlb, glbFileName, StringComparison.Ordinal)
                || cachedTicks != sourceTicks
                || cachedLength != sourceLength)
            {
                return false;
            }

            return AreRequiredClipFilesReady(directory, clips);
        }

        /// <summary>
        /// キャッシュが有効ならシートを読み込む
        /// </summary>
        public static bool TryLoad(
            int playerSlotIndex,
            string glbFileName,
            out DesktopPetSpriteSheet sheet)
        {
            sheet = null;
            if (!TryResolveSourceStamp(glbFileName, out long sourceTicks, out long sourceLength))
            {
                return false;
            }

            string directory = GetSlotDirectory(playerSlotIndex);
            string manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            if (!TryReadManifest(
                    manifestPath,
                    out int version,
                    out int slot,
                    out string cachedGlb,
                    out long cachedTicks,
                    out long cachedLength,
                    out Dictionary<(int facing, int action), List<string>> clips))
            {
                return false;
            }

            if (version != FormatVersion
                || slot != playerSlotIndex
                || !string.Equals(cachedGlb, glbFileName, StringComparison.Ordinal)
                || cachedTicks != sourceTicks
                || cachedLength != sourceLength)
            {
                return false;
            }

            DesktopPetSpriteSheet loaded = new DesktopPetSpriteSheet();
            for (int facing = 0; facing < DesktopPetSpriteSheet.FacingCount; facing++)
            {
                for (int action = 0; action < DesktopPetSpriteSheet.ActionCount; action++)
                {
                    if (!clips.TryGetValue((facing, action), out List<string> files)
                        || files == null
                        || files.Count == 0)
                    {
                        if (DesktopPetSpriteSheet.IsRequiredClip(facing, action))
                        {
                            loaded.Dispose();
                            return false;
                        }

                        continue;
                    }

                    Sprite[] frames = new Sprite[files.Count];
                    for (int i = 0; i < files.Count; i++)
                    {
                        string path = Path.Combine(directory, files[i]);
                        if (!File.Exists(path))
                        {
                            loaded.Dispose();
                            return false;
                        }

                        byte[] bytes = File.ReadAllBytes(path);
                        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(bytes, false))
                        {
                            UnityEngine.Object.Destroy(texture);
                            loaded.Dispose();
                            return false;
                        }

                        frames[i] = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                    }

                    loaded.SetClip((DesktopPetFacing)facing, (DesktopPetAction)action, frames);
                }
            }

            sheet = loaded;
            return true;
        }

        /// <summary>
        /// シートをディスクへ保存する
        /// </summary>
        public static bool TrySave(
            int playerSlotIndex,
            string glbFileName,
            DesktopPetSpriteSheet sheet)
        {
            if (sheet == null || string.IsNullOrEmpty(glbFileName))
            {
                return false;
            }

            if (!TryResolveSourceStamp(glbFileName, out long sourceTicks, out long sourceLength))
            {
                // 外部ビューア引き継ぎ用にスタンプ無しでも保存する
                sourceTicks = 0;
                sourceLength = 0;
                Debug.LogWarning(
                    "[DesktopPetSpriteCache] glbスタンプを取得できないため0で保存します glb="
                    + glbFileName);
            }

            string directory = GetSlotDirectory(playerSlotIndex);
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                Directory.CreateDirectory(directory);

                StringBuilder manifest = new StringBuilder(512);
                manifest.Append("version=").Append(FormatVersion).Append('\n');
                manifest.Append("slot=").Append(playerSlotIndex).Append('\n');
                manifest.Append("glb=").Append(glbFileName).Append('\n');
                manifest.Append("ticks=").Append(sourceTicks).Append('\n');
                manifest.Append("length=").Append(sourceLength).Append('\n');

                for (int facing = 0; facing < DesktopPetSpriteSheet.FacingCount; facing++)
                {
                    for (int action = 0; action < DesktopPetSpriteSheet.ActionCount; action++)
                    {
                        if (!DesktopPetSpriteSheet.IsRequiredClip(facing, action))
                        {
                            continue;
                        }

                        int count = sheet.GetFrameCount(
                            (DesktopPetFacing)facing,
                            (DesktopPetAction)action);
                        if (count <= 0)
                        {
                            Directory.Delete(directory, true);
                            return false;
                        }

                        manifest.Append("clip=")
                            .Append(facing)
                            .Append(',')
                            .Append(action)
                            .Append(',')
                            .Append(count)
                            .Append('\n');

                        for (int i = 0; i < count; i++)
                        {
                            Sprite sprite = sheet.GetFrame(
                                (DesktopPetFacing)facing,
                                (DesktopPetAction)action,
                                i);
                            if (sprite == null || sprite.texture == null)
                            {
                                Directory.Delete(directory, true);
                                return false;
                            }

                            string fileName = BuildFrameFileName(facing, action, i);
                            byte[] png = sprite.texture.EncodeToPNG();
                            if (png == null || png.Length == 0)
                            {
                                Directory.Delete(directory, true);
                                return false;
                            }

                            File.WriteAllBytes(Path.Combine(directory, fileName), png);
                            manifest.Append("frame=")
                                .Append(facing)
                                .Append(',')
                                .Append(action)
                                .Append(',')
                                .Append(i)
                                .Append(',')
                                .Append(fileName)
                                .Append('\n');
                        }
                    }
                }

                File.WriteAllText(Path.Combine(directory, ManifestFileName), manifest.ToString(), Encoding.UTF8);
                WriteActiveMarker(directory);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DesktopPetSpriteCache] 保存に失敗しました: " + exception.Message);
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch
                {
                    // 削除失敗時は呼び出し側へfalseを返す
                }

                return false;
            }
        }

        /// <summary>
        /// 外部ビューア向けに直近キャッシュパスを書く
        /// </summary>
        public static void WriteActiveMarker(string cacheDirectory)
        {
            if (string.IsNullOrEmpty(cacheDirectory))
            {
                return;
            }

            WriteActiveMarker(new[] { cacheDirectory });
        }

        /// <summary>
        /// 外部ビューア向けに直近キャッシュパス一覧を書く
        /// </summary>
        public static void WriteActiveMarker(IReadOnlyList<string> cacheDirectories)
        {
            try
            {
                Directory.CreateDirectory(RootDirectory);
                StringBuilder builder = new StringBuilder(256);
                if (cacheDirectories != null)
                {
                    for (int i = 0; i < cacheDirectories.Count; i++)
                    {
                        string directory = cacheDirectories[i];
                        if (string.IsNullOrEmpty(directory))
                        {
                            continue;
                        }

                        if (builder.Length > 0)
                        {
                            builder.Append('\n');
                        }

                        builder.Append(directory);
                    }
                }

                File.WriteAllText(
                    Path.Combine(RootDirectory, ActiveMarkerFileName),
                    builder.ToString(),
                    Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetSpriteCache] active marker書き込み失敗: " + exception.Message);
            }
        }

        /// <summary>
        /// 外部ビューア向けに起動時の言語コードを書く
        /// </summary>
        public static void WriteLanguageMarker(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(RootDirectory);
                File.WriteAllText(
                    Path.Combine(RootDirectory, LanguageMarkerFileName),
                    languageCode.Trim(),
                    Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetSpriteCache] language marker書き込み失敗: " + exception.Message);
            }
        }

        /// <summary>
        /// Steamランチャー向けにペット引き継ぎ中マーカーを書く
        /// </summary>
        public static void WriteLauncherKeepAliveMarker()
        {
            try
            {
                Directory.CreateDirectory(RootDirectory);
                File.WriteAllText(
                    Path.Combine(RootDirectory, LauncherKeepAliveFileName),
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture),
                    Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetSpriteCache] launcher keepalive書き込み失敗: " + exception.Message);
            }
        }

        /// <summary>
        /// Steamランチャー向け引き継ぎマーカーを消す
        /// </summary>
        public static void ClearLauncherKeepAliveMarker()
        {
            try
            {
                string path = Path.Combine(RootDirectory, LauncherKeepAliveFileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetSpriteCache] launcher keepalive削除失敗: " + exception.Message);
            }
        }

        /// <summary>
        /// 直近キャッシュディレクトリを読む
        /// </summary>
        public static bool TryReadActiveCacheDirectory(out string cacheDirectory)
        {
            cacheDirectory = null;
            if (!TryReadActiveCacheDirectories(out IReadOnlyList<string> directories) || directories.Count == 0)
            {
                return false;
            }

            cacheDirectory = directories[0];
            return true;
        }

        /// <summary>
        /// 直近キャッシュディレクトリ一覧を読む
        /// </summary>
        public static bool TryReadActiveCacheDirectories(out IReadOnlyList<string> cacheDirectories)
        {
            cacheDirectories = Array.Empty<string>();
            string path = Path.Combine(RootDirectory, ActiveMarkerFileName);
            if (!File.Exists(path))
            {
                return false;
            }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            List<string> directories = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i].Trim();
                if (!string.IsNullOrEmpty(text) && Directory.Exists(text))
                {
                    directories.Add(text);
                }
            }

            if (directories.Count == 0)
            {
                return false;
            }

            cacheDirectories = directories;
            return true;
        }

        private static string BuildFrameFileName(int facing, int action, int frameIndex)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "f{0}_a{1}_{2:000}.png",
                facing,
                action,
                frameIndex);
        }

        private static bool TryResolveSourceStamp(
            string glbFileName,
            out long sourceTicks,
            out long sourceLength)
        {
            sourceTicks = 0;
            sourceLength = 0;
            string path = ModelSaveStorage.ResolveReadPath(glbFileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            FileInfo info = new FileInfo(path);
            sourceTicks = info.LastWriteTimeUtc.Ticks;
            sourceLength = info.Length;
            return true;
        }

        private static bool TryReadManifest(
            string manifestPath,
            out int version,
            out int slot,
            out string glbFileName,
            out long sourceTicks,
            out long sourceLength,
            out Dictionary<(int facing, int action), List<string>> clips)
        {
            version = 0;
            slot = -1;
            glbFileName = null;
            sourceTicks = 0;
            sourceLength = 0;
            clips = new Dictionary<(int, int), List<string>>();
            Dictionary<(int, int), int> expectedCounts = new Dictionary<(int, int), int>();

            string[] lines = File.ReadAllLines(manifestPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    return false;
                }

                string key = line.Substring(0, eq);
                string value = line.Substring(eq + 1);
                switch (key)
                {
                    case "version":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
                        {
                            return false;
                        }

                        break;
                    case "slot":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot))
                        {
                            return false;
                        }

                        break;
                    case "glb":
                        glbFileName = value;
                        break;
                    case "ticks":
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceTicks))
                        {
                            return false;
                        }

                        break;
                    case "length":
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceLength))
                        {
                            return false;
                        }

                        break;
                    case "clip":
                    {
                        string[] parts = value.Split(',');
                        if (parts.Length != 3
                            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int facing)
                            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int action)
                            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                            || facing < 0
                            || facing >= DesktopPetSpriteSheet.FacingCount
                            || action < 0
                            || action >= DesktopPetSpriteSheet.ActionCount
                            || count <= 0)
                        {
                            return false;
                        }

                        expectedCounts[(facing, action)] = count;
                        clips[(facing, action)] = new List<string>(count);
                        break;
                    }
                    case "frame":
                    {
                        string[] parts = value.Split(',');
                        if (parts.Length != 4
                            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int facing)
                            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int action)
                            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frameIndex)
                            || facing < 0
                            || facing >= DesktopPetSpriteSheet.FacingCount
                            || action < 0
                            || action >= DesktopPetSpriteSheet.ActionCount)
                        {
                            return false;
                        }

                        if (!clips.TryGetValue((facing, action), out List<string> list))
                        {
                            return false;
                        }

                        while (list.Count <= frameIndex)
                        {
                            list.Add(null);
                        }

                        list[frameIndex] = parts[3];
                        break;
                    }
                    default:
                        return false;
                }
            }

            foreach (KeyValuePair<(int facing, int action), int> pair in expectedCounts)
            {
                if (!clips.TryGetValue(pair.Key, out List<string> list)
                    || list == null
                    || list.Count != pair.Value)
                {
                    return false;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (string.IsNullOrEmpty(list[i]))
                    {
                        return false;
                    }
                }
            }

            int requiredCount = 0;
            for (int facing = 0; facing < DesktopPetSpriteSheet.FacingCount; facing++)
            {
                for (int action = 0; action < DesktopPetSpriteSheet.ActionCount; action++)
                {
                    if (!DesktopPetSpriteSheet.IsRequiredClip(facing, action))
                    {
                        continue;
                    }

                    if (!expectedCounts.ContainsKey((facing, action)))
                    {
                        return false;
                    }

                    requiredCount++;
                }
            }

            if (requiredCount != RequiredClipCount)
            {
                return false;
            }

            return version == FormatVersion
                && slot >= 0
                && !string.IsNullOrEmpty(glbFileName);
        }

        private static bool AreRequiredClipFilesReady(
            string directory,
            Dictionary<(int facing, int action), List<string>> clips)
        {
            for (int facing = 0; facing < DesktopPetSpriteSheet.FacingCount; facing++)
            {
                for (int action = 0; action < DesktopPetSpriteSheet.ActionCount; action++)
                {
                    if (!DesktopPetSpriteSheet.IsRequiredClip(facing, action))
                    {
                        continue;
                    }

                    if (!clips.TryGetValue((facing, action), out List<string> files)
                        || files == null
                        || files.Count == 0)
                    {
                        return false;
                    }

                    for (int i = 0; i < files.Count; i++)
                    {
                        if (string.IsNullOrEmpty(files[i]))
                        {
                            return false;
                        }

                        string path = Path.Combine(directory, files[i]);
                        if (!File.Exists(path))
                        {
                            return false;
                        }

                        FileInfo info = new FileInfo(path);
                        if (info.Length < 64)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
