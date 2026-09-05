using System.IO;
using UnityEngine;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室投稿用パッケージフォルダへモデル一式を書き出す
    /// </summary>
    public static class ModelGalleryPackageWriter
    {
        /// <summary>
        /// 展示室パッケージを書き出す
        /// </summary>
        /// <param name="folder">出力先フォルダ</param>
        /// <param name="meta">パッケージメタデータ</param>
        /// <param name="glbBytes">GLBバイナリ</param>
        /// <param name="voxelCompressedBytes">GZip圧縮済みvoxelバイナリ</param>
        /// <param name="previewPng">サムネイルPNG null可</param>
        /// <returns>成功した場合true</returns>
        public static bool TryWrite(
            string folder,
            ModelGalleryPackageMeta meta,
            byte[] glbBytes,
            byte[] voxelCompressedBytes,
            byte[] previewPng)
        {
            if (string.IsNullOrEmpty(folder)
                || meta == null
                || glbBytes == null
                || glbBytes.Length == 0
                || voxelCompressedBytes == null
                || voxelCompressedBytes.Length == 0)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(
                    Path.Combine(folder, ModelGalleryPackageFiles.MetaFileName),
                    JsonUtility.ToJson(meta, true));
                File.WriteAllBytes(Path.Combine(folder, ModelGalleryPackageFiles.ModelFileName), glbBytes);
                File.WriteAllBytes(
                    Path.Combine(folder, ModelGalleryPackageFiles.VoxelFileName),
                    voxelCompressedBytes);

                if (previewPng != null && previewPng.Length > 0)
                {
                    File.WriteAllBytes(
                        Path.Combine(folder, ModelGalleryPackageFiles.PreviewFileName),
                        previewPng);
                }

                return true;
            }
            catch (IOException exception)
            {
                Debug.LogError($"[ModelGalleryPackageWriter] 書き込みに失敗しました: {exception.Message}");
                return false;
            }
        }
    }
}
