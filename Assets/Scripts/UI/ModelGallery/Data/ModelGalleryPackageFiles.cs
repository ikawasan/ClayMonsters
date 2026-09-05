namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室パッケージに含めるファイル名と形式バージョン
    /// </summary>
    public static class ModelGalleryPackageFiles
    {
        /// <summary>
        /// メタデータJSON
        /// </summary>
        public const string MetaFileName = "package.json";

        /// <summary>
        /// リグ付きモデルGLB
        /// </summary>
        public const string ModelFileName = "model.glb";

        /// <summary>
        /// ClayEdit造形ボクセルスナップショット(GZip)
        /// </summary>
        public const string VoxelFileName = "model.voxel.gz";

        /// <summary>
        /// 旧展示室パッケージの未圧縮voxel
        /// </summary>
        public const string LegacyVoxelFileName = "model.voxel";

        /// <summary>
        /// サムネイルPNG
        /// </summary>
        public const string PreviewFileName = "preview.png";

        /// <summary>
        /// voxel同梱パッケージの形式バージョン
        /// </summary>
        public const string PackageVersionWithVoxel = "2";
    }
}
