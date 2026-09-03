using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// ClayEditセーブ時に書き込むボクセルスナップショット
    /// </summary>
    public readonly struct ClayVoxelSnapshotWriteRequest
    {
        /// <summary>
        /// ボクセル密度配列
        /// </summary>
        public readonly float[] Voxels;

        /// <summary>
        /// ボクセル色配列
        /// </summary>
        public readonly Vector3[] Colors;

        /// <summary>
        /// 造形グリッドのセル数
        /// </summary>
        public readonly int GridSize;

        /// <summary>
        /// 造形グリッドのワールドサイズ
        /// </summary>
        public readonly float BoundsSize;

        /// <summary>
        /// 書き込み対象データがあるか
        /// </summary>
        public bool HasData =>
            Voxels != null
            && Colors != null
            && GridSize > 0
            && BoundsSize > 0f;

        /// <summary>
        /// ボクセルスナップショット書き込み要求を生成する
        /// </summary>
        /// <param name="voxels">ボクセル密度配列</param>
        /// <param name="colors">ボクセル色配列</param>
        /// <param name="gridSize">造形グリッドのセル数</param>
        /// <param name="boundsSize">造形グリッドのワールドサイズ</param>
        public ClayVoxelSnapshotWriteRequest(
            float[] voxels,
            Vector3[] colors,
            int gridSize,
            float boundsSize)
        {
            Voxels = voxels;
            Colors = colors;
            GridSize = gridSize;
            BoundsSize = boundsSize;
        }
    }
}
