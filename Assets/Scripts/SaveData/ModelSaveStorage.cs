using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルセーブファイルの入出力先を解決する
    /// 書き込みはpersistentDataPath読み取りはpersistentDataPathを優先しStreamingAssetsへフォールバックする
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

        private static string ResolveStoredPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string writablePath = Path.Combine(WritableRoot, fileName);
            if (File.Exists(writablePath))
            {
                return writablePath;
            }

            string writableCompressedPath = GetCompressedPath(writablePath);
            if (File.Exists(writableCompressedPath))
            {
                return writableCompressedPath;
            }

            string streamingPath = Path.Combine(StreamingRoot, fileName);
            if (File.Exists(streamingPath))
            {
                return streamingPath;
            }

            string streamingCompressedPath = GetCompressedPath(streamingPath);
            return File.Exists(streamingCompressedPath) ? streamingCompressedPath : null;
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
