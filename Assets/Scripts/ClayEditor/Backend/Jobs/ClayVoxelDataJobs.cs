using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ClayEditor.Backend.Jobs
{
    /// <summary>
    /// 全ボクセル密度を既定の空値で初期化するJob
    /// </summary>
    [BurstCompile]
    internal struct ClayVoxelClearJob : IJobParallelFor
    {
        public NativeArray<float> Voxels;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            Voxels[index] = -0.5f;
        }
    }

    /// <summary>
    /// 全ボクセル色を既定色で初期化するJob
    /// </summary>
    [BurstCompile]
    internal struct ClayVoxelInitializeColorsJob : IJobParallelFor
    {
        public float3 DefaultColor;
        public NativeArray<float3> Colors;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            Colors[index] = DefaultColor;
        }
    }

    /// <summary>
    /// ブラシ範囲内のボクセル密度を変更するJob
    /// </summary>
    [BurstCompile]
    internal struct ClayVoxelModifyJob : IJobParallelFor
    {
        [ReadOnly] public int Size;
        [ReadOnly] public int GridCount;
        [ReadOnly] public float Scale;
        [ReadOnly] public float3 Offset;
        [ReadOnly] public float3 HitPosition;
        [ReadOnly] public float ModRadius;
        [ReadOnly] public float ModStrength;
        public NativeArray<float> Voxels;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            int z = index % GridCount;
            int y = (index / GridCount) % GridCount;
            int x = index / (GridCount * GridCount);

            if (x == 0 || x == Size || y == 0 || y == Size || z == 0 || z == Size)
            {
                return;
            }

            float3 localPos = new float3(x, y, z) * Scale - Offset;
            float dist = math.distance(localPos, HitPosition);
            if (dist >= ModRadius)
            {
                return;
            }

            float influence = (1f - math.pow(dist / ModRadius, 2f)) * ModStrength;
            Voxels[index] += influence;
        }
    }

    /// <summary>
    /// ブラシ範囲内のボクセル色を塗るJob
    /// </summary>
    [BurstCompile]
    internal struct ClayVoxelPaintJob : IJobParallelFor
    {
        [ReadOnly] public int Size;
        [ReadOnly] public int GridCount;
        [ReadOnly] public float Scale;
        [ReadOnly] public float3 Offset;
        [ReadOnly] public float3 HitPosition;
        [ReadOnly] public float PaintRadius;
        [ReadOnly] public float3 PaintColor;
        [ReadOnly] public float3 PaintNormal;
        public NativeArray<float3> Colors;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            int z = index % GridCount;
            int y = (index / GridCount) % GridCount;
            int x = index / (GridCount * GridCount);

            if (x > Size || y > Size || z > Size)
            {
                return;
            }

            float3 localPos = new float3(x, y, z) * Scale - Offset;
            if (math.distance(localPos, HitPosition) >= PaintRadius)
            {
                return;
            }

            if (math.dot(PaintNormal, PaintNormal) > 1e-6f)
            {
                float3 paintNormal = math.normalize(PaintNormal);
                float3 toVoxel = localPos - HitPosition;
                if (math.dot(toVoxel, paintNormal) < -Scale * 2f)
                {
                    return;
                }
            }

            Colors[index] = PaintColor;
        }
    }
}
