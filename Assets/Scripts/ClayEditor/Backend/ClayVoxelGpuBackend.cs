using System;
using ClayEditor.Backend.Interface;
using UnityEngine;

namespace ClayEditor.Backend
{
    /// <summary>
    /// ComputeShaderでボクセル処理を行うGPUバックエンド
    /// </summary>
    internal sealed class ClayVoxelGpuBackend : IClayVoxelBackend
    {
        private const string KernelGenerateMesh = "GenerateMesh";
        private const string KernelModifyVoxels = "ModifyVoxels";
        private const string KernelPaintVoxels = "PaintVoxels";
        private const string KernelInitializeVoxelColors = "InitializeVoxelColors";
        private const int ComputeThreads = 8;
        private const int FloatsPerTriangle = 27;

        private static class ShaderIDs
        {
            public static readonly int Size = Shader.PropertyToID("_Size");
            public static readonly int Scale = Shader.PropertyToID("_Scale");
            public static readonly int IsoLevel = Shader.PropertyToID("_IsoLevel");
            public static readonly int Offset = Shader.PropertyToID("_Offset");
            public static readonly int HitPosition = Shader.PropertyToID("_HitPosition");
            public static readonly int ModRadius = Shader.PropertyToID("_ModRadius");
            public static readonly int ModStrength = Shader.PropertyToID("_ModStrength");
            public static readonly int Voxels = Shader.PropertyToID("_Voxels");
            public static readonly int Triangles = Shader.PropertyToID("_Triangles");
            public static readonly int ChunkOrigin = Shader.PropertyToID("_ChunkOrigin");
            public static readonly int ChunkSize = Shader.PropertyToID("_ChunkSize");
            public static readonly int VoxelColors = Shader.PropertyToID("_VoxelColors");
            public static readonly int PaintColor = Shader.PropertyToID("_PaintColor");
            public static readonly int PaintRadius = Shader.PropertyToID("_PaintRadius");
            public static readonly int PaintNormal = Shader.PropertyToID("_PaintNormal");
            public static readonly int DefaultColor = Shader.PropertyToID("_DefaultColor");
        }

        private struct GpuTriangle
        {
            public Vector3 v1;
            public Vector3 v2;
            public Vector3 v3;
            public Vector3 n1;
            public Vector3 n2;
            public Vector3 n3;
            public Vector3 c1;
            public Vector3 c2;
            public Vector3 c3;
        }

        private ComputeShader marchingCubesCompute;
        private ComputeBuffer voxelBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer triCountBuffer;
        private ComputeBuffer voxelColorBuffer;

        private int kernelGenerateMesh;
        private int kernelModifyVoxels;
        private int kernelPaintVoxels;
        private int kernelInitializeVoxelColors;

        private int size;
        private int totalVoxelCount;
        private int maxTriangleCount;
        private float scale;
        private float isoLevel;
        private Vector3 offset;
        private Vector3 defaultColor;
        private bool shaderGridParamsDirty = true;

        private readonly int[] countData = new int[1];
        private GpuTriangle[] triangleCache;

        /// <inheritdoc/>
        public bool IsReady =>
            voxelBuffer != null
            && triangleBuffer != null
            && triCountBuffer != null
            && voxelColorBuffer != null
            && marchingCubesCompute != null;

        /// <inheritdoc/>
        public ClayVoxelBackendKind Kind => ClayVoxelBackendKind.Gpu;

        /// <summary>
        /// ComputeShaderバックエンドを初期化する
        /// </summary>
        public bool TryInitialize(ComputeShader computeShader, int gridSize)
        {
            Dispose();

            if (!SystemInfo.supportsComputeShaders || computeShader == null)
            {
                return false;
            }

            marchingCubesCompute = computeShader;
            size = gridSize;
            totalVoxelCount = (size + 1) * (size + 1) * (size + 1);
            maxTriangleCount = size * size * size * 5;

            try
            {
                voxelBuffer = new ComputeBuffer(totalVoxelCount, sizeof(float));
                triangleBuffer = new ComputeBuffer(
                    maxTriangleCount,
                    sizeof(float) * FloatsPerTriangle,
                    ComputeBufferType.Append);
                triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                voxelColorBuffer = new ComputeBuffer(totalVoxelCount, sizeof(float) * 3);

                kernelGenerateMesh = marchingCubesCompute.FindKernel(KernelGenerateMesh);
                kernelModifyVoxels = marchingCubesCompute.FindKernel(KernelModifyVoxels);
                kernelPaintVoxels = marchingCubesCompute.FindKernel(KernelPaintVoxels);
                kernelInitializeVoxelColors = marchingCubesCompute.FindKernel(KernelInitializeVoxelColors);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ClayVoxelGpuBackend] 初期化に失敗しました: {exception.Message}");
                Dispose();
                return false;
            }

            shaderGridParamsDirty = true;
            return true;
        }

        /// <inheritdoc/>
        public void SetGridParams(int gridSize, float gridScale, float gridIsoLevel, Vector3 gridOffset, Vector3 gridDefaultColor)
        {
            size = gridSize;
            scale = gridScale;
            isoLevel = gridIsoLevel;
            offset = gridOffset;
            defaultColor = gridDefaultColor;
            shaderGridParamsDirty = true;
            EnsureShaderGridParams();
        }

        /// <inheritdoc/>
        public void ClearAllVoxels()
        {
            float[] emptyData = new float[totalVoxelCount];
            for (int i = 0; i < emptyData.Length; i++)
            {
                emptyData[i] = -0.5f;
            }

            voxelBuffer.SetData(emptyData);
            shaderGridParamsDirty = true;
            EnsureShaderGridParams();
        }

        /// <inheritdoc/>
        public void InitializeVoxelColors()
        {
            EnsureShaderGridParams();
            marchingCubesCompute.SetBuffer(kernelInitializeVoxelColors, ShaderIDs.VoxelColors, voxelColorBuffer);
            marchingCubesCompute.SetVector(ShaderIDs.DefaultColor, defaultColor);

            int groups = Mathf.CeilToInt((size + 1) / (float)ComputeThreads);
            marchingCubesCompute.Dispatch(kernelInitializeVoxelColors, groups, groups, groups);
        }

        /// <inheritdoc/>
        public void Modify(Vector3 hitPosition, float modRadius, float modStrength)
        {
            EnsureShaderGridParams();
            marchingCubesCompute.SetVector(ShaderIDs.HitPosition, hitPosition);
            marchingCubesCompute.SetFloat(ShaderIDs.ModRadius, modRadius);
            marchingCubesCompute.SetFloat(ShaderIDs.ModStrength, modStrength);
            marchingCubesCompute.SetBuffer(kernelModifyVoxels, ShaderIDs.Voxels, voxelBuffer);

            int threadGroups = Mathf.CeilToInt((size + 1) / (float)ComputeThreads);
            marchingCubesCompute.Dispatch(kernelModifyVoxels, threadGroups, threadGroups, threadGroups);
        }

        /// <inheritdoc/>
        public void Paint(Vector3 hitPosition, float paintRadius, Vector3 paintColor, Vector3 paintNormal)
        {
            EnsureShaderGridParams();
            marchingCubesCompute.SetBuffer(kernelPaintVoxels, ShaderIDs.VoxelColors, voxelColorBuffer);
            marchingCubesCompute.SetVector(ShaderIDs.HitPosition, hitPosition);
            marchingCubesCompute.SetVector(ShaderIDs.PaintNormal, paintNormal);
            marchingCubesCompute.SetFloat(ShaderIDs.PaintRadius, paintRadius);
            marchingCubesCompute.SetVector(ShaderIDs.PaintColor, paintColor);

            int groups = Mathf.CeilToInt((size + 1) / (float)ComputeThreads);
            marchingCubesCompute.Dispatch(kernelPaintVoxels, groups, groups, groups);
        }

        /// <inheritdoc/>
        public float[] GetVoxelData()
        {
            float[] data = new float[totalVoxelCount];
            voxelBuffer.GetData(data);
            return data;
        }

        /// <inheritdoc/>
        public void SetVoxelData(float[] data)
        {
            if (data == null || data.Length != totalVoxelCount)
            {
                return;
            }

            voxelBuffer.SetData(data);
            shaderGridParamsDirty = true;
            EnsureShaderGridParams();
        }

        /// <inheritdoc/>
        public Vector3[] GetVoxelColors()
        {
            Vector3[] data = new Vector3[totalVoxelCount];
            voxelColorBuffer.GetData(data);
            return data;
        }

        /// <inheritdoc/>
        public void SetVoxelColors(Vector3[] voxelColors)
        {
            if (voxelColors == null || voxelColors.Length != totalVoxelCount)
            {
                return;
            }

            voxelColorBuffer.SetData(voxelColors);
        }

        /// <inheritdoc/>
        public int GenerateMeshChunk(
            int originX,
            int originY,
            int originZ,
            int sizeX,
            int sizeY,
            int sizeZ,
            ClayVoxelTriangle[] output,
            int outputOffset,
            int outputCapacity)
        {
            if (!IsReady || output == null || outputCapacity <= 0)
            {
                return 0;
            }

            EnsureShaderGridParams();
            triangleBuffer.SetCounterValue(0);

            marchingCubesCompute.SetInts(ShaderIDs.ChunkOrigin, originX, originY, originZ);
            marchingCubesCompute.SetInts(ShaderIDs.ChunkSize, sizeX, sizeY, sizeZ);
            marchingCubesCompute.SetBuffer(kernelGenerateMesh, ShaderIDs.Voxels, voxelBuffer);
            marchingCubesCompute.SetBuffer(kernelGenerateMesh, ShaderIDs.Triangles, triangleBuffer);
            marchingCubesCompute.SetBuffer(kernelGenerateMesh, ShaderIDs.VoxelColors, voxelColorBuffer);

            int gx = Mathf.CeilToInt(sizeX / (float)ComputeThreads);
            int gy = Mathf.CeilToInt(sizeY / (float)ComputeThreads);
            int gz = Mathf.CeilToInt(sizeZ / (float)ComputeThreads);
            marchingCubesCompute.Dispatch(kernelGenerateMesh, gx, gy, gz);

            countData[0] = 0;
            ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
            triCountBuffer.GetData(countData);

            int triangleCount = countData[0];
            if (triangleCount <= 0)
            {
                return 0;
            }

            int copyCount = Mathf.Min(triangleCount, outputCapacity);
            EnsureTriangleCache(copyCount);
            triangleBuffer.GetData(triangleCache, 0, 0, copyCount);

            for (int i = 0; i < copyCount; i++)
            {
                GpuTriangle tri = triangleCache[i];
                output[outputOffset + i] = new ClayVoxelTriangle
                {
                    v1 = tri.v1,
                    v2 = tri.v2,
                    v3 = tri.v3,
                    n1 = tri.n1,
                    n2 = tri.n2,
                    n3 = tri.n3,
                    c1 = tri.c1,
                    c2 = tri.c2,
                    c3 = tri.c3
                };
            }

            return copyCount;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            voxelBuffer?.Release();
            triangleBuffer?.Release();
            triCountBuffer?.Release();
            voxelColorBuffer?.Release();

            voxelBuffer = null;
            triangleBuffer = null;
            triCountBuffer = null;
            voxelColorBuffer = null;
            marchingCubesCompute = null;
        }

        private void EnsureShaderGridParams()
        {
            if (!shaderGridParamsDirty || marchingCubesCompute == null)
            {
                return;
            }

            marchingCubesCompute.SetInt(ShaderIDs.Size, size);
            marchingCubesCompute.SetFloat(ShaderIDs.Scale, scale);
            marchingCubesCompute.SetFloat(ShaderIDs.IsoLevel, isoLevel);
            marchingCubesCompute.SetVector(ShaderIDs.Offset, offset);
            marchingCubesCompute.SetVector(ShaderIDs.DefaultColor, defaultColor);
            shaderGridParamsDirty = false;
        }

        private void EnsureTriangleCache(int count)
        {
            if (triangleCache == null || triangleCache.Length < count)
            {
                triangleCache = new GpuTriangle[count];
            }
        }
    }
}
