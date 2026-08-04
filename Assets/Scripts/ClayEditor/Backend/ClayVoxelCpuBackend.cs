using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ClayEditor.Backend.Interface;
using ClayEditor.Backend.Jobs;

namespace ClayEditor.Backend
{
    /// <summary>
    /// JobSystemとBurstでボクセル処理を行うCPUバックエンド
    /// </summary>
    internal sealed class ClayVoxelCpuBackend : IClayVoxelBackend
    {
        private NativeArray<float> voxels;
        private NativeArray<float3> colors;
        private NativeArray<ClayVoxelTriangle> chunkTriangles;
        private NativeArray<int> chunkTriangleCounts;

        private int size;
        private int gridCount;
        private int totalVoxelCount;
        private float scale;
        private float isoLevel;
        private float3 offset;
        private float3 defaultColor;
        private int maxChunkCells;

        /// <inheritdoc/>
        public bool IsReady => voxels.IsCreated && colors.IsCreated;

        /// <inheritdoc/>
        public ClayVoxelBackendKind Kind => ClayVoxelBackendKind.Cpu;

        /// <summary>
        /// 指定グリッドサイズでCPUバックエンドを初期化する
        /// </summary>
        public bool TryInitialize(int gridSize, int maxChunkCellCount)
        {
            Dispose();

            size = gridSize;
            gridCount = size + 1;
            totalVoxelCount = gridCount * gridCount * gridCount;
            maxChunkCells = Mathf.Max(maxChunkCellCount, 1);

            try
            {
                voxels = new NativeArray<float>(totalVoxelCount, Allocator.Persistent);
                colors = new NativeArray<float3>(totalVoxelCount, Allocator.Persistent);
                chunkTriangles = new NativeArray<ClayVoxelTriangle>(
                    maxChunkCells * ClayVoxelMeshLimits.MaxTrianglesPerCell,
                    Allocator.Persistent);
                chunkTriangleCounts = new NativeArray<int>(maxChunkCells, Allocator.Persistent);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ClayVoxelCpuBackend] 初期化に失敗しました: {exception.Message}");
                Dispose();
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public void SetGridParams(int gridSize, float gridScale, float gridIsoLevel, Vector3 gridOffset, Vector3 gridDefaultColor)
        {
            size = gridSize;
            gridCount = size + 1;
            scale = gridScale;
            isoLevel = gridIsoLevel;
            offset = gridOffset;
            defaultColor = gridDefaultColor;
        }

        /// <inheritdoc/>
        public void ClearAllVoxels()
        {
            var job = new ClayVoxelClearJob { Voxels = voxels };
            job.Schedule(totalVoxelCount, 256).Complete();
        }

        /// <inheritdoc/>
        public void InitializeVoxelColors()
        {
            var job = new ClayVoxelInitializeColorsJob
            {
                DefaultColor = defaultColor,
                Colors = colors
            };
            job.Schedule(totalVoxelCount, 256).Complete();
        }

        /// <inheritdoc/>
        public void Modify(Vector3 hitPosition, float modRadius, float modStrength)
        {
            Vector3 voxelCenter = (hitPosition + (Vector3)offset) / scale;
            float voxelRadius = modRadius / scale + 1f;

            int minX = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.x - voxelRadius), 0, size);
            int minY = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.y - voxelRadius), 0, size);
            int minZ = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.z - voxelRadius), 0, size);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.x + voxelRadius), 0, size);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.y + voxelRadius), 0, size);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.z + voxelRadius), 0, size);

            int sizeX = Mathf.Max(0, maxX - minX + 1);
            int sizeY = Mathf.Max(0, maxY - minY + 1);
            int sizeZ = Mathf.Max(0, maxZ - minZ + 1);
            int voxelCount = sizeX * sizeY * sizeZ;
            if (voxelCount <= 0)
            {
                return;
            }

            var job = new ClayVoxelModifyJob
            {
                Size = size,
                GridCount = gridCount,
                Scale = scale,
                Offset = offset,
                HitPosition = hitPosition,
                ModRadius = modRadius,
                ModStrength = modStrength,
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                SizeX = sizeX,
                SizeY = sizeY,
                SizeZ = sizeZ,
                Voxels = voxels
            };
            job.Schedule(voxelCount, 128).Complete();
        }

        /// <inheritdoc/>
        public void Paint(Vector3 hitPosition, float paintRadius, Vector3 paintColor, Vector3 paintNormal)
        {
            Vector3 voxelCenter = (hitPosition + (Vector3)offset) / scale;
            float voxelRadius = paintRadius / scale + 1f;

            int minX = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.x - voxelRadius), 0, size);
            int minY = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.y - voxelRadius), 0, size);
            int minZ = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.z - voxelRadius), 0, size);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.x + voxelRadius), 0, size);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.y + voxelRadius), 0, size);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.z + voxelRadius), 0, size);

            int sizeX = Mathf.Max(0, maxX - minX + 1);
            int sizeY = Mathf.Max(0, maxY - minY + 1);
            int sizeZ = Mathf.Max(0, maxZ - minZ + 1);
            int voxelCount = sizeX * sizeY * sizeZ;
            if (voxelCount <= 0)
            {
                return;
            }

            var job = new ClayVoxelPaintJob
            {
                Size = size,
                GridCount = gridCount,
                Scale = scale,
                Offset = offset,
                HitPosition = hitPosition,
                PaintRadius = paintRadius,
                PaintColor = paintColor,
                PaintNormal = paintNormal,
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                SizeX = sizeX,
                SizeY = sizeY,
                SizeZ = sizeZ,
                Colors = colors
            };
            job.Schedule(voxelCount, 128).Complete();
        }

        /// <inheritdoc/>
        public float[] GetVoxelData()
        {
            var data = new float[totalVoxelCount];
            voxels.CopyTo(data);
            return data;
        }

        /// <inheritdoc/>
        public void SetVoxelData(float[] data)
        {
            if (data == null || data.Length != totalVoxelCount)
            {
                return;
            }

            voxels.CopyFrom(data);
        }

        /// <inheritdoc/>
        public Vector3[] GetVoxelColors()
        {
            var data = new Vector3[totalVoxelCount];
            for (int i = 0; i < totalVoxelCount; i++)
            {
                float3 color = colors[i];
                data[i] = new Vector3(color.x, color.y, color.z);
            }

            return data;
        }

        /// <inheritdoc/>
        public void SetVoxelColors(Vector3[] voxelColors)
        {
            if (voxelColors == null || voxelColors.Length != totalVoxelCount)
            {
                return;
            }

            for (int i = 0; i < totalVoxelCount; i++)
            {
                colors[i] = voxelColors[i];
            }
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
            int cellCount = sizeX * sizeY * sizeZ;
            if (cellCount <= 0 || output == null || outputCapacity <= 0)
            {
                return 0;
            }

            if (cellCount > maxChunkCells)
            {
                EnsureChunkBuffers(cellCount);
            }

            var job = new ClayVoxelGenerateMeshJob
            {
                Size = size,
                GridCount = gridCount,
                Scale = scale,
                IsoLevel = isoLevel,
                Offset = offset,
                DefaultColor = defaultColor,
                ChunkOrigin = new int3(originX, originY, originZ),
                ChunkSize = new int3(sizeX, sizeY, sizeZ),
                Voxels = voxels,
                Colors = colors,
                TrianglesOut = chunkTriangles,
                TriangleCounts = chunkTriangleCounts
            };
            job.Schedule(cellCount, 64).Complete();

            int triangleCount = 0;
            for (int cell = 0; cell < cellCount; cell++)
            {
                int count = chunkTriangleCounts[cell];
                int srcBase = cell * ClayVoxelMeshLimits.MaxTrianglesPerCell;
                for (int i = 0; i < count; i++)
                {
                    if (triangleCount >= outputCapacity)
                    {
                        return triangleCount;
                    }

                    output[outputOffset + triangleCount] = chunkTriangles[srcBase + i];
                    triangleCount++;
                }
            }

            return triangleCount;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (voxels.IsCreated)
            {
                voxels.Dispose();
            }

            if (colors.IsCreated)
            {
                colors.Dispose();
            }

            if (chunkTriangles.IsCreated)
            {
                chunkTriangles.Dispose();
            }

            if (chunkTriangleCounts.IsCreated)
            {
                chunkTriangleCounts.Dispose();
            }
        }

        private void EnsureChunkBuffers(int cellCount)
        {
            if (cellCount <= maxChunkCells)
            {
                return;
            }

            if (chunkTriangles.IsCreated)
            {
                chunkTriangles.Dispose();
            }

            if (chunkTriangleCounts.IsCreated)
            {
                chunkTriangleCounts.Dispose();
            }

            maxChunkCells = cellCount;
            chunkTriangles = new NativeArray<ClayVoxelTriangle>(
                maxChunkCells * ClayVoxelMeshLimits.MaxTrianglesPerCell,
                Allocator.Persistent);
            chunkTriangleCounts = new NativeArray<int>(maxChunkCells, Allocator.Persistent);
        }
    }
}
