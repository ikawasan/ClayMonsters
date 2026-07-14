namespace ClayEditor.Backend.Jobs
{
    /// <summary>
    /// ボクセルグリッドの線形インデックス計算
    /// </summary>
    internal static class ClayVoxelGridIndex
    {
        /// <summary>
        /// 3D座標から線形インデックスへ変換する
        /// </summary>
        public static int ToIndex(int x, int y, int z, int gridCount)
        {
            return x * gridCount * gridCount + y * gridCount + z;
        }
    }
}
