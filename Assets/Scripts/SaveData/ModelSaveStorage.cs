using System.IO;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルセーブファイルの入出力先を解決する
    /// 書き込みはpersistentDataPath読み取りはpersistentDataPathを優先しStreamingAssetsへフォールバックする
    /// </summary>
    public static class ModelSaveStorage
    {
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
            string writablePath = Path.Combine(WritableRoot, fileName);
            if (File.Exists(writablePath))
            {
                return writablePath;
            }

            string streamingPath = Path.Combine(StreamingRoot, fileName);
            if (File.Exists(streamingPath))
            {
                return streamingPath;
            }

            return writablePath;
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

            return File.Exists(Path.Combine(WritableRoot, fileName))
                || File.Exists(Path.Combine(StreamingRoot, fileName));
        }

        /// <summary>
        /// 読み取り可能な場所からテキストを読み込む
        /// 見つからない場合はnullを返す
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>読み込んだテキストなければnull</returns>
        public static string ReadAllText(string fileName)
        {
            string path = ResolveReadPath(fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>
        /// 読み取り可能な場所からバイト列を読み込む
        /// 見つからない場合はnullを返す
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>読み込んだバイト列なければnull</returns>
        public static byte[] ReadAllBytes(string fileName)
        {
            string path = ResolveReadPath(fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
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
    }
}
