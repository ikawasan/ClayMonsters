using System;
using System.IO;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// ClayEdit造形ボクセルのスナップショットファイル
    /// </summary>
    public static class ClayVoxelSnapshotFile
    {
        private const byte VersionWithColors = 2;
        private const byte VersionVoxelsOnly = 1;
        private static readonly byte[] Magic = { (byte)'C', (byte)'L', (byte)'A', (byte)'Y' };

        /// <summary>
        /// 読み込み結果
        /// </summary>
        public readonly struct Snapshot
        {
            public readonly int GridSize;
            public readonly float BoundsSize;
            public readonly float[] Voxels;
            public readonly Vector3[] Colors;

            public Snapshot(int gridSize, float boundsSize, float[] voxels, Vector3[] colors)
            {
                GridSize = gridSize;
                BoundsSize = boundsSize;
                Voxels = voxels;
                Colors = colors;
            }

            /// <summary>
            /// ペイント色が含まれているか
            /// </summary>
            public bool HasColors => Colors != null && Colors.Length > 0;
        }

        /// <summary>
        /// ボクセルスナップショットを書き込む
        /// </summary>
        /// <param name="filePath">出力先パス</param>
        /// <param name="voxels">ボクセル密度配列</param>
        /// <param name="colors">ボクセル色配列</param>
        /// <param name="gridSize">造形グリッドのセル数</param>
        /// <param name="boundsSize">造形グリッドのワールドサイズ</param>
        /// <returns>成功した場合true</returns>
        public static bool TryWrite(
            string filePath,
            float[] voxels,
            Vector3[] colors,
            int gridSize,
            float boundsSize)
        {
            if (string.IsNullOrEmpty(filePath) || voxels == null || colors == null || gridSize <= 0 || boundsSize <= 0f)
            {
                return false;
            }

            int expectedCount = GetVoxelCount(gridSize);
            if (voxels.Length != expectedCount || colors.Length != expectedCount)
            {
                return false;
            }

            try
            {
                string fileName = Path.GetFileName(filePath);
                using Stream stream = ModelSaveStorage.CreateCompressedWriteStream(fileName);
                using var writer = new BinaryWriter(stream);
                writer.Write(Magic);
                writer.Write(VersionWithColors);
                writer.Write(gridSize);
                writer.Write(boundsSize);
                writer.Write(expectedCount);
                for (int i = 0; i < voxels.Length; i++)
                {
                    writer.Write(voxels[i]);
                }

                for (int i = 0; i < colors.Length; i++)
                {
                    writer.Write(colors[i].x);
                    writer.Write(colors[i].y);
                    writer.Write(colors[i].z);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ClayVoxelSnapshotFile] 書き込みに失敗しました: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// ボクセルスナップショットを読み込む
        /// </summary>
        /// <param name="filePath">入力パス</param>
        /// <param name="snapshot">読み込み結果</param>
        /// <param name="errorMessage">失敗理由</param>
        /// <returns>成功した場合true</returns>
        public static bool TryRead(string filePath, out Snapshot snapshot, out string errorMessage)
        {
            snapshot = default;
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(filePath))
            {
                errorMessage = "ボクセルスナップショットが見つかりません";
                return false;
            }

            try
            {
                string fileName = Path.GetFileName(filePath);
                using Stream stream = ModelSaveStorage.OpenRead(fileName);
                if (stream == null)
                {
                    errorMessage = "ボクセルスナップショットが見つかりません";
                    return false;
                }

                using var reader = new BinaryReader(stream);
                byte[] magic = reader.ReadBytes(Magic.Length);
                for (int i = 0; i < Magic.Length; i++)
                {
                    if (magic[i] != Magic[i])
                    {
                        errorMessage = "ボクセルスナップショットの形式が不正です";
                        return false;
                    }
                }

                byte version = reader.ReadByte();
                if (version != VersionVoxelsOnly && version != VersionWithColors)
                {
                    errorMessage = "ボクセルスナップショットのバージョンが未対応です";
                    return false;
                }

                int gridSize = reader.ReadInt32();
                float boundsSize = reader.ReadSingle();
                int voxelCount = reader.ReadInt32();
                int expectedCount = GetVoxelCount(gridSize);
                if (gridSize <= 0 || boundsSize <= 0f || voxelCount != expectedCount)
                {
                    errorMessage = "ボクセルスナップショットのヘッダが不正です";
                    return false;
                }

                var voxels = new float[voxelCount];
                for (int i = 0; i < voxelCount; i++)
                {
                    voxels[i] = reader.ReadSingle();
                }

                Vector3[] colors = null;
                if (version >= VersionWithColors)
                {
                    colors = new Vector3[voxelCount];
                    for (int i = 0; i < voxelCount; i++)
                    {
                        colors[i] = new Vector3(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle());
                    }
                }

                snapshot = new Snapshot(gridSize, boundsSize, voxels, colors);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"ボクセルスナップショットの読み込みに失敗しました: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// グリッド設定からボクセル数を算出する
        /// </summary>
        /// <param name="gridSize">造形グリッドのセル数</param>
        /// <returns>ボクセル数</returns>
        public static int GetVoxelCount(int gridSize)
        {
            int cornerCount = gridSize + 1;
            return cornerCount * cornerCount * cornerCount;
        }
    }
}
