using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルセーブファイルの入出力先を解決する
    /// 書き込みはpersistentDataPath
    /// 敵カタログはStreamingAssetsから書込先へ展開して読む
    /// </summary>
    public static class ModelSaveStorage
    {
        private const string CompressedExtension = ".gz";

        /// <summary>
        /// StreamingAssets内でモデルセーブを格納するサブフォルダ名
        /// </summary>
        public const string StreamingSubFolder = "ModelSave";

        /// <summary>
        /// 書き込み先のルート(persistentDataPath)
        /// </summary>
        public static string WritableRoot => Application.persistentDataPath;

        /// <summary>
        /// ビルド同梱の読み取り専用ルート(StreamingAssets内)
        /// </summary>
        public static string StreamingRoot =>
            Path.Combine(Application.streamingAssetsPath, StreamingSubFolder);

        /// <summary>
        /// 書き込み用のフルパスを返す
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>persistentDataPath内のフルパス</returns>
        public static string GetWritablePath(string fileName) =>
            Path.Combine(WritableRoot, fileName);

        /// <summary>
        /// 読み取り用のフルパスを解決する
        /// persistentDataPathに存在すればそれをなければStreamingAssetsを返す
        /// どちらも無い場合はpersistentDataPath上の既定パスを返す
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>読み取りに使うフルパス</returns>
        public static string ResolveReadPath(string fileName)
        {
            string sourcePath = ResolveStoredPath(fileName);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return Path.Combine(WritableRoot, fileName);
            }

            if (!IsCompressedPath(sourcePath))
            {
                return sourcePath;
            }

            string sourceKind = sourcePath.StartsWith(WritableRoot, System.StringComparison.OrdinalIgnoreCase)
                ? "Writable"
                : "Streaming";
            string cacheRoot = Path.Combine(Application.temporaryCachePath, StreamingSubFolder, sourceKind);
            string cachePath = Path.Combine(cacheRoot, fileName);
            if (File.Exists(cachePath)
                && File.GetLastWriteTimeUtc(cachePath) == File.GetLastWriteTimeUtc(sourcePath))
            {
                return cachePath;
            }

            Directory.CreateDirectory(cacheRoot);
            using (Stream source = OpenStoredReadStream(sourcePath))
            using (var destination = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(destination);
            }

            File.SetLastWriteTimeUtc(cachePath, File.GetLastWriteTimeUtc(sourcePath));
            return cachePath;
        }

        /// <summary>
        /// スタンプ比較用に保存実体パスを返す(なければnull)
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <returns>存在する保存パス</returns>
        public static string ResolveStoredPathForStamp(string fileName) =>
            ResolveStoredPath(fileName);

        /// <summary>
        /// 読み取り可能な場所にファイルが存在するか判定する
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>persistentDataPathかStreamingAssetsに存在するか</returns>
        public static bool Exists(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            return !string.IsNullOrEmpty(ResolveStoredPath(fileName));
        }

        /// <summary>
        /// 読み取り可能な場所からテキストを読み込む
        /// 見つからない場合はnullを返す
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>読み込んだテキストなければnull</returns>
        public static string ReadAllText(string fileName)
        {
            using Stream stream = OpenRead(fileName);
            if (stream == null)
            {
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// 読み取り可能な場所からバイト列を読み込む
        /// 見つからない場合はnullを返す
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>読み込んだバイト列なければnull</returns>
        public static byte[] ReadAllBytes(string fileName)
        {
            using Stream stream = OpenRead(fileName);
            if (stream == null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        /// <summary>
        /// GZip保存されている場合のみ圧縮バイト列をそのまま返す
        /// 対人戦の回線転送向け
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <returns>圧縮バイト列なければnull</returns>
        public static byte[] TryReadCompressedStorageBytes(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string path = ResolveStoredPath(fileName);
            if (string.IsNullOrEmpty(path) || !IsCompressedPath(path) || !File.Exists(path))
            {
                return null;
            }

            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// 生バイト列をGZip圧縮する
        /// </summary>
        /// <param name="rawBytes">未圧縮バイト列</param>
        /// <returns>GZipバイト列</returns>
        public static byte[] CompressToGzipBytes(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
            {
                return null;
            }

            using var output = new MemoryStream();
            using (var gzip = new GZipStream(
                output,
                System.IO.Compression.CompressionLevel.Optimal,
                leaveOpen: true))
            {
                gzip.Write(rawBytes, 0, rawBytes.Length);
            }

            return output.ToArray();
        }

        /// <summary>
        /// GZipバイト列を展開する
        /// </summary>
        /// <param name="compressedBytes">GZipバイト列</param>
        /// <returns>展開済みバイト列</returns>
        public static byte[] DecompressGzipBytes(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
            {
                return null;
            }

            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>
        /// 読み取り可能な保存データを展開ストリームとして開く
        /// 呼び出し側でDisposeする
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <returns>展開済み読込ストリームなければnull</returns>
        public static Stream OpenRead(string fileName)
        {
            string path = ResolveStoredPath(fileName);
            return string.IsNullOrEmpty(path) ? null : OpenStoredReadStream(path);
        }

        /// <summary>
        /// バイト列をGZip可逆圧縮して書き込む
        /// 同名の旧未圧縮ファイルは削除する
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <param name="bytes">保存するバイト列</param>
        public static void WriteAllBytes(string fileName, byte[] bytes)
        {
            if (string.IsNullOrEmpty(fileName) || bytes == null)
            {
                return;
            }

            using Stream stream = CreateCompressedWriteStream(fileName);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 文字列をUTF8へ変換しGZip可逆圧縮して書き込む
        /// 同名の旧未圧縮ファイルは削除する
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <param name="text">保存する文字列</param>
        public static void WriteAllText(string fileName, string text)
        {
            WriteAllBytes(fileName, Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        /// <summary>
        /// 書込先のGZip圧縮ストリームを生成する
        /// 呼び出し側でDisposeして圧縮を完了する
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <returns>圧縮書込ストリーム</returns>
        public static Stream CreateCompressedWriteStream(string fileName)
        {
            string rawPath = GetWritablePath(fileName);
            string compressedPath = GetCompressedPath(rawPath);
            Directory.CreateDirectory(Path.GetDirectoryName(compressedPath));

            if (File.Exists(rawPath))
            {
                File.Delete(rawPath);
            }

            DeleteDecompressedCache(fileName);
            var fileStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write, FileShare.None);
            return new GZipStream(fileStream, System.IO.Compression.CompressionLevel.Optimal);
        }

        /// <summary>
        /// 書込済み未圧縮ファイルをGZipへ置き換える
        /// 元ファイルは圧縮成功後に削除する
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        /// <returns>圧縮に成功した場合true</returns>
        public static bool CompressWritableFile(string fileName)
        {
            string rawPath = GetWritablePath(fileName);
            if (!File.Exists(rawPath))
            {
                return false;
            }

            string compressedPath = GetCompressedPath(rawPath);
            try
            {
                using (var source = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destination = new FileStream(compressedPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var gzip = new GZipStream(destination, System.IO.Compression.CompressionLevel.Optimal))
                {
                    source.CopyTo(gzip);
                }

                File.Delete(rawPath);
                DeleteDecompressedCache(fileName);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[ModelSaveStorage] 圧縮に失敗しました file={fileName} error={exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 旧未圧縮ファイルが存在するときだけGZipへ移行する
        /// 圧縮済みまたは未保存なら何もしない
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        public static void MigrateLegacyWritableFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            string rawPath = GetWritablePath(fileName);
            if (File.Exists(rawPath))
            {
                CompressWritableFile(fileName);
            }
        }

        /// <summary>
        /// 論理ファイル名に対応する未圧縮圧縮済みキャッシュを削除する
        /// 存在しないファイルは無視する
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        public static void Delete(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            DeleteIfExists(GetWritablePath(fileName));
            DeleteIfExists(GetCompressedPath(GetWritablePath(fileName)));
            DeleteDecompressedCache(fileName);
        }

        /// <summary>
        /// 書込先の内容をStreamingAssetsへ反映する
        /// 書込先に無い場合はStreamingAssets側も削除する
        /// </summary>
        /// <param name="fileName">論理ファイル名</param>
        public static void MirrorWritableToStreaming(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            Directory.CreateDirectory(StreamingRoot);
            string writableRaw = GetWritablePath(fileName);
            string writableCompressed = GetCompressedPath(writableRaw);
            string streamingRaw = Path.Combine(StreamingRoot, fileName);
            string streamingCompressed = GetCompressedPath(streamingRaw);

            if (File.Exists(writableCompressed))
            {
                File.Copy(writableCompressed, streamingCompressed, true);
                DeleteIfExists(streamingRaw);
                return;
            }

            if (File.Exists(writableRaw))
            {
                File.Copy(writableRaw, streamingRaw, true);
                DeleteIfExists(streamingCompressed);
                return;
            }

            DeleteIfExists(streamingRaw);
            DeleteIfExists(streamingCompressed);
        }

        /// <summary>
        /// 敵プールのメタと実体ファイルをStreamingAssetsへ同期する
        /// ROM配布時はStreamingAssetsのみが同梱されるため保存後に呼ぶ
        /// </summary>
        /// <param name="data">敵セーブメタ</param>
        public static void MirrorEnemyPoolToStreaming(ClayModelSaveData data)
        {
            MirrorWritableToStreaming(ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy));
            if (data?.slots == null)
            {
                return;
            }

            int slotCount = ModelSavePoolSettings.EnemySlotCount;
            for (int i = 0; i < slotCount; i++)
            {
                MirrorWritableToStreaming(ModelSavePoolSettings.GetGlbFileName(ModelSavePool.Enemy, i));
                MirrorWritableToStreaming(ModelSavePoolSettings.GetThumbnailFileName(ModelSavePool.Enemy, i));
                MirrorWritableToStreaming(ModelSavePoolSettings.GetVoxelFileName(ModelSavePool.Enemy, i));
            }
        }

        /// <summary>
        /// スロットのサムネイルPNGを読み込む
        /// </summary>
        /// <param name="slot">対象スロット</param>
        /// <returns>PNGバイト列なければnull</returns>
        public static byte[] ReadThumbnailPng(ModelSaveSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                return null;
            }

            return ReadAllBytes(slot.thumbnailFileName);
        }

        /// <summary>
        /// 同梱の敵カタログを読める状態にする
        /// StreamingAssetsの同梱数が書込先より多ければ書込先へ展開する
        /// 以降の敵読込は展開済み書込先を優先する
        /// </summary>
        public static void EnsureEnemyCatalogReadable()
        {
            if (enemyCatalogReady)
            {
                return;
            }

            enemyCatalogReady = true;

            int streamingFileCount = CountEnemyFilesInRoot(StreamingRoot);
            int streamingUsable = CountUsableEnemySlots(StreamingRoot);
            int writableUsable = CountUsableEnemySlots(WritableRoot);

            Debug.Log(
                "[ModelSaveStorage] 敵カタログ確認"
                    + $" streamingFiles={streamingFileCount}"
                    + $" streamingUsable={streamingUsable}"
                    + $" writableUsable={writableUsable}"
                    + $" streamingRoot={StreamingRoot}"
                    + $" writableRoot={WritableRoot}");

            if (streamingFileCount <= 0 && streamingUsable <= 0)
            {
                Debug.LogError(
                    "[ModelSaveStorage] StreamingAssetsに敵カタログがありません"
                        + $" path={StreamingRoot}"
                        + " ビルド成果物のStreamingAssets/ModelSaveを確認してください");
                return;
            }

            // 同梱の方が多ければ常に展開する不完全な書込先でくれもんだけになるのを防ぐ
            if (streamingUsable > writableUsable || streamingFileCount > CountEnemyFilesInRoot(WritableRoot))
            {
                InstallEnemyCatalogFromStreaming();
                writableUsable = CountUsableEnemySlots(WritableRoot);
                Debug.Log(
                    "[ModelSaveStorage] 敵カタログ展開後"
                        + $" writableUsable={writableUsable}"
                        + $" writableFiles={CountEnemyFilesInRoot(WritableRoot)}");
            }

            if (writableUsable <= 1 && streamingUsable > 1)
            {
                Debug.LogError(
                    "[ModelSaveStorage] 敵カタログ展開後もusableが不足しています"
                        + $" writableUsable={writableUsable}"
                        + $" streamingUsable={streamingUsable}");
            }
        }

        private static bool enemyCatalogReady;

        private static string ResolveStoredPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            // 敵カタログは展開後の書込先を優先する
            if (IsEnemyCatalogFile(fileName))
            {
                string writableHit = ResolvePathInRoot(WritableRoot, fileName);
                if (!string.IsNullOrEmpty(writableHit))
                {
                    return writableHit;
                }

                return ResolvePathInRoot(StreamingRoot, fileName);
            }

            string writableOther = ResolvePathInRoot(WritableRoot, fileName);
            if (!string.IsNullOrEmpty(writableOther))
            {
                return writableOther;
            }

            return ResolvePathInRoot(StreamingRoot, fileName);
        }

        private static string ResolvePathInRoot(string root, string fileName)
        {
            string rawPath = Path.Combine(root, fileName);
            if (File.Exists(rawPath))
            {
                return rawPath;
            }

            string compressedPath = GetCompressedPath(rawPath);
            return File.Exists(compressedPath) ? compressedPath : null;
        }

        private static bool IsEnemyCatalogFile(string fileName)
        {
            return fileName.StartsWith("EnemyModel", System.StringComparison.Ordinal);
        }

        private static int CountEnemyFilesInRoot(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return 0;
            }

            int count = 0;
            string[] files = Directory.GetFiles(root, "EnemyModel*");
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (string.IsNullOrEmpty(name)
                    || name.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static int CountUsableEnemySlots(string root)
        {
            string metaName = ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy);
            string metaPath = ResolvePathInRoot(root, metaName);
            if (string.IsNullOrEmpty(metaPath))
            {
                return 0;
            }

            string json;
            try
            {
                using Stream stream = OpenStoredReadStream(metaPath);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                json = reader.ReadToEnd();
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"[ModelSaveStorage] 敵メタ読込に失敗しました root={root} error={exception.Message}");
                return 0;
            }

            if (string.IsNullOrEmpty(json))
            {
                return 0;
            }

            ClayModelSaveData data = JsonUtility.FromJson<ClayModelSaveData>(json);
            if (data?.slots == null)
            {
                return 0;
            }

            int usable = 0;
            for (int i = 0; i < data.slots.Count; i++)
            {
                ModelSaveSlot slot = data.slots[i];
                if (slot == null || !slot.isUsed || string.IsNullOrEmpty(slot.glbFileName))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(ResolvePathInRoot(root, slot.glbFileName)))
                {
                    usable++;
                }
            }

            return usable;
        }

        private static void InstallEnemyCatalogFromStreaming()
        {
            if (!Directory.Exists(StreamingRoot))
            {
                Debug.LogError(
                    $"[ModelSaveStorage] 展開元StreamingAssetsがありません path={StreamingRoot}");
                return;
            }

            Directory.CreateDirectory(WritableRoot);
            string[] files = Directory.GetFiles(StreamingRoot, "EnemyModel*");
            int copied = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string source = files[i];
                string name = Path.GetFileName(source);
                if (string.IsNullOrEmpty(name)
                    || name.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string destination = Path.Combine(WritableRoot, name);
                File.Copy(source, destination, true);
                string logicalName = name.EndsWith(CompressedExtension, System.StringComparison.OrdinalIgnoreCase)
                    ? name.Substring(0, name.Length - CompressedExtension.Length)
                    : name;
                DeleteDecompressedCache(logicalName);
                copied++;
            }

            Debug.Log(
                $"[ModelSaveStorage] StreamingAssetsの敵カタログを書込先へ展開しました"
                    + $" copied={copied}"
                    + $" from={StreamingRoot}"
                    + $" to={WritableRoot}");
        }

        private static Stream OpenStoredReadStream(string path)
        {
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return IsCompressedPath(path)
                ? new GZipStream(fileStream, CompressionMode.Decompress)
                : fileStream;
        }

        private static bool IsCompressedPath(string path)
        {
            return path.EndsWith(CompressedExtension, System.StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCompressedPath(string path)
        {
            return path + CompressedExtension;
        }

        private static void DeleteDecompressedCache(string fileName)
        {
            string cacheRoot = Path.Combine(Application.temporaryCachePath, StreamingSubFolder);
            DeleteIfExists(Path.Combine(cacheRoot, "Writable", fileName));
            DeleteIfExists(Path.Combine(cacheRoot, "Streaming", fileName));
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
