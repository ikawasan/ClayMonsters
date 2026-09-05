using SaveData;
using System.IO;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室パッケージ内voxelの圧縮と復号
    /// </summary>
    public static class ModelGalleryVoxelPackage
    {
        /// <summary>
        /// スロット保存済みvoxelを展示室投稿用GZipバイト列へ変換する
        /// </summary>
        /// <param name="voxelFileName">スロットvoxelの論理ファイル名</param>
        /// <param name="compressedBytes">GZipバイト列</param>
        /// <returns>成功した場合true</returns>
        public static bool TryReadCompressedFromSlot(string voxelFileName, out byte[] compressedBytes)
        {
            compressedBytes = ModelSaveStorage.TryReadCompressedStorageBytes(voxelFileName);
            if (compressedBytes != null && compressedBytes.Length > 0)
            {
                return true;
            }

            byte[] rawBytes = ModelSaveStorage.ReadAllBytes(voxelFileName);
            if (rawBytes == null || rawBytes.Length == 0)
            {
                compressedBytes = null;
                return false;
            }

            compressedBytes = ModelSaveStorage.CompressToGzipBytes(rawBytes);
            return compressedBytes != null && compressedBytes.Length > 0;
        }

        /// <summary>
        /// 展示室パッケージフォルダにvoxelが含まれているか
        /// </summary>
        /// <param name="folder">パッケージフォルダ</param>
        /// <returns>voxelがある場合true</returns>
        public static bool PackageContainsVoxel(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            return File.Exists(Path.Combine(folder, ModelGalleryPackageFiles.VoxelFileName))
                || File.Exists(Path.Combine(folder, ModelGalleryPackageFiles.LegacyVoxelFileName));
        }

        /// <summary>
        /// 展示室パッケージからスロット保存向けvoxelバイト列を読み込む
        /// </summary>
        /// <param name="folder">パッケージフォルダ</param>
        /// <param name="rawBytes">展開済みvoxelバイト列</param>
        /// <returns>成功した場合true</returns>
        public static bool TryReadRawFromPackageFolder(string folder, out byte[] rawBytes)
        {
            rawBytes = null;
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            string compressedPath = Path.Combine(folder, ModelGalleryPackageFiles.VoxelFileName);
            if (File.Exists(compressedPath))
            {
                return TryDecodePackageBytes(File.ReadAllBytes(compressedPath), out rawBytes);
            }

            string legacyPath = Path.Combine(folder, ModelGalleryPackageFiles.LegacyVoxelFileName);
            if (File.Exists(legacyPath))
            {
                return TryDecodePackageBytes(File.ReadAllBytes(legacyPath), out rawBytes);
            }

            return false;
        }

        private static bool TryDecodePackageBytes(byte[] packageBytes, out byte[] rawBytes)
        {
            rawBytes = null;
            if (packageBytes == null || packageBytes.Length == 0)
            {
                return false;
            }

            if (LooksLikeGzip(packageBytes))
            {
                rawBytes = ModelSaveStorage.DecompressGzipBytes(packageBytes);
                return rawBytes != null && rawBytes.Length > 0;
            }

            rawBytes = packageBytes;
            return true;
        }

        private static bool LooksLikeGzip(byte[] bytes)
        {
            return bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b;
        }
    }
}
