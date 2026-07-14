using System;
using UnityEngine;

namespace ClayEditor.Backend.Interface
{
    /// <summary>
    /// ボクセルデータとMarchingCubes処理の抽象化
    /// GPU ComputeShaderとCPU JobSystemの切替用
    /// </summary>
    public interface IClayVoxelBackend : IDisposable
    {
        /// <summary>
        /// バックエンドが利用可能か
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// バックエンド種別
        /// </summary>
        ClayVoxelBackendKind Kind { get; }

        /// <summary>
        /// グリッドパラメータを設定する
        /// </summary>
        void SetGridParams(int size, float scale, float isoLevel, Vector3 offset, Vector3 defaultColor);

        /// <summary>
        /// 全ボクセルを空相当の密度で初期化する
        /// </summary>
        void ClearAllVoxels();

        /// <summary>
        /// ボクセル色を既定色で初期化する
        /// </summary>
        void InitializeVoxelColors();

        /// <summary>
        /// ブラシでボクセル密度を変更する
        /// </summary>
        void Modify(Vector3 hitPosition, float modRadius, float modStrength);

        /// <summary>
        /// ブラシでボクセル色を塗る
        /// </summary>
        void Paint(Vector3 hitPosition, float paintRadius, Vector3 paintColor, Vector3 paintNormal);

        /// <summary>
        /// ボクセル密度配列を取得する
        /// </summary>
        float[] GetVoxelData();

        /// <summary>
        /// ボクセル密度配列を設定する
        /// </summary>
        void SetVoxelData(float[] data);

        /// <summary>
        /// ボクセル色配列を取得する
        /// </summary>
        Vector3[] GetVoxelColors();

        /// <summary>
        /// ボクセル色配列を設定する
        /// </summary>
        void SetVoxelColors(Vector3[] colors);

        /// <summary>
        /// 指定チャンク範囲のメッシュ三角形を生成する
        /// </summary>
        /// <returns>有効な三角形数</returns>
        int GenerateMeshChunk(
            int originX,
            int originY,
            int originZ,
            int sizeX,
            int sizeY,
            int sizeZ,
            ClayVoxelTriangle[] output,
            int outputOffset,
            int outputCapacity);
    }
}
