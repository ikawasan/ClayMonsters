using ClayEditor.Backend;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ClayEditor.Backend.Jobs
{
    /// <summary>
    /// 指定チャンク範囲のMarchingCubesメッシュを生成するJob
    /// </summary>
    [BurstCompile]
    internal struct ClayVoxelGenerateMeshJob : IJobParallelFor
    {
        internal const int MaxTrianglesPerCell = ClayVoxelMeshLimits.MaxTrianglesPerCell;

        [ReadOnly] public int Size;
        [ReadOnly] public int GridCount;
        [ReadOnly] public float Scale;
        [ReadOnly] public float IsoLevel;
        [ReadOnly] public float3 Offset;
        [ReadOnly] public float3 DefaultColor;
        [ReadOnly] public int3 ChunkOrigin;
        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public NativeArray<float> Voxels;
        [ReadOnly] public NativeArray<float3> Colors;
        [NativeDisableParallelForRestriction]
        public NativeArray<ClayVoxelTriangle> TrianglesOut;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> TriangleCounts;

        /// <inheritdoc/>
        public void Execute(int cellIndex)
        {
            int localZ = cellIndex % ChunkSize.z;
            int localY = (cellIndex / ChunkSize.z) % ChunkSize.y;
            int localX = cellIndex / (ChunkSize.y * ChunkSize.z);

            if (localX >= ChunkSize.x || localY >= ChunkSize.y || localZ >= ChunkSize.z)
            {
                TriangleCounts[cellIndex] = 0;
                return;
            }

            int3 id = ChunkOrigin + new int3(localX, localY, localZ);
            if (id.x >= Size || id.y >= Size || id.z >= Size)
            {
                TriangleCounts[cellIndex] = 0;
                return;
            }

            float vals0 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[0]);
            float vals1 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[1]);
            float vals2 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[2]);
            float vals3 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[3]);
            float vals4 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[4]);
            float vals5 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[5]);
            float vals6 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[6]);
            float vals7 = SampleVoxel(id + MarchingCubesTables.CornerOffsets[7]);

            int cubeIndex = 0;
            cubeIndex |= (vals0 > IsoLevel ? 1 : 0) << 0;
            cubeIndex |= (vals1 > IsoLevel ? 1 : 0) << 1;
            cubeIndex |= (vals2 > IsoLevel ? 1 : 0) << 2;
            cubeIndex |= (vals3 > IsoLevel ? 1 : 0) << 3;
            cubeIndex |= (vals4 > IsoLevel ? 1 : 0) << 4;
            cubeIndex |= (vals5 > IsoLevel ? 1 : 0) << 5;
            cubeIndex |= (vals6 > IsoLevel ? 1 : 0) << 6;
            cubeIndex |= (vals7 > IsoLevel ? 1 : 0) << 7;

            int triRowBase = cubeIndex * MarchingCubesTables.TriTableStride;
            int triCount = 0;
            int outBase = cellIndex * MaxTrianglesPerCell;

            for (int i = 0; i < MarchingCubesTables.TriTableStride && MarchingCubesTables.TriTableFlat[triRowBase + i] != -1; i += 3)
            {
                ClayVoxelTriangle tri = BuildTriangle(
                    id,
                    MarchingCubesTables.TriTableFlat[triRowBase + i],
                    MarchingCubesTables.TriTableFlat[triRowBase + i + 1],
                    MarchingCubesTables.TriTableFlat[triRowBase + i + 2],
                    vals0,
                    vals1,
                    vals2,
                    vals3,
                    vals4,
                    vals5,
                    vals6,
                    vals7);

                TrianglesOut[outBase + triCount] = tri;
                triCount++;
            }

            TriangleCounts[cellIndex] = triCount;
        }

        private float SampleVoxel(int3 p)
        {
            int px = math.clamp(p.x, 0, Size);
            int py = math.clamp(p.y, 0, Size);
            int pz = math.clamp(p.z, 0, Size);
            return Voxels[ClayVoxelGridIndex.ToIndex(px, py, pz, GridCount)];
        }

        private float3 Interpolate(float3 p1, float v1, float3 p2, float v2)
        {
            float delta = v2 - v1;
            if (math.abs(delta) < 1e-5f)
            {
                return (p1 + p2) * 0.5f;
            }

            float t = math.saturate((IsoLevel - v1) / delta);
            return p1 + t * (p2 - p1);
        }

        private float3 GetNormalAt(int3 p)
        {
            float nx = SampleVoxel(new int3(p.x + 1, p.y, p.z)) - SampleVoxel(new int3(p.x - 1, p.y, p.z));
            float ny = SampleVoxel(new int3(p.x, p.y + 1, p.z)) - SampleVoxel(new int3(p.x, p.y - 1, p.z));
            float nz = SampleVoxel(new int3(p.x, p.y, p.z + 1)) - SampleVoxel(new int3(p.x, p.y, p.z - 1));
            float3 n = new float3(nx, ny, nz);
            float lengthSq = math.lengthsq(n);
            if (lengthSq < 1e-10f)
            {
                return new float3(0f, 1f, 0f);
            }

            return -n * math.rsqrt(lengthSq);
        }

        private bool IsDefaultColor(float3 color)
        {
            return math.distance(color, DefaultColor) < 0.02f;
        }

        private float3 InterpolateColor(int3 cornerA, int3 cornerB, float valA, float valB)
        {
            float t = 0.5f;
            float delta = valB - valA;
            if (math.abs(delta) >= 1e-5f)
            {
                t = math.saturate((IsoLevel - valA) / delta);
            }

            float3 colorA = Colors[ClayVoxelGridIndex.ToIndex(cornerA.x, cornerA.y, cornerA.z, GridCount)];
            float3 colorB = Colors[ClayVoxelGridIndex.ToIndex(cornerB.x, cornerB.y, cornerB.z, GridCount)];

            bool paintedA = !IsDefaultColor(colorA);
            bool paintedB = !IsDefaultColor(colorB);
            // 未塗装の既定色(白っぽいクリーム)を表面に出さない
            if (paintedA && !paintedB)
            {
                return colorA;
            }

            if (!paintedA && paintedB)
            {
                return colorB;
            }

            return math.lerp(colorA, colorB, t);
        }

        private ClayVoxelTriangle BuildTriangle(
            int3 id,
            int edge1,
            int edge2,
            int edge3,
            float v0,
            float v1,
            float v2,
            float v3,
            float v4,
            float v5,
            float v6,
            float v7)
        {
            ClayVoxelTriangle tri = default;
            tri = SetVertex(tri, 1, id, edge1, v0, v1, v2, v3, v4, v5, v6, v7);
            tri = SetVertex(tri, 2, id, edge2, v0, v1, v2, v3, v4, v5, v6, v7);
            tri = SetVertex(tri, 3, id, edge3, v0, v1, v2, v3, v4, v5, v6, v7);
            return tri;
        }

        private float GetCornerValue(int cornerIndex, float v0, float v1, float v2, float v3, float v4, float v5, float v6, float v7)
        {
            switch (cornerIndex)
            {
                case 0: return v0;
                case 1: return v1;
                case 2: return v2;
                case 3: return v3;
                case 4: return v4;
                case 5: return v5;
                case 6: return v6;
                default: return v7;
            }
        }

        private ClayVoxelTriangle SetVertex(
            ClayVoxelTriangle tri,
            int vertexIndex,
            int3 id,
            int edge,
            float v0,
            float v1,
            float v2,
            float v3,
            float v4,
            float v5,
            float v6,
            float v7)
        {
            int c1 = MarchingCubesTables.EdgeTable[edge].x;
            int c2 = MarchingCubesTables.EdgeTable[edge].y;
            int3 cornerA = id + MarchingCubesTables.CornerOffsets[c1];
            int3 cornerB = id + MarchingCubesTables.CornerOffsets[c2];
            float valC1 = GetCornerValue(c1, v0, v1, v2, v3, v4, v5, v6, v7);
            float valC2 = GetCornerValue(c2, v0, v1, v2, v3, v4, v5, v6, v7);
            float3 p1 = new float3(cornerA) * Scale;
            float3 p2 = new float3(cornerB) * Scale;
            float3 vertex = Interpolate(p1, valC1, p2, valC2) - Offset;
            float t = 0.5f;
            float edgeDelta = valC2 - valC1;
            if (math.abs(edgeDelta) >= 1e-5f)
            {
                t = math.saturate((IsoLevel - valC1) / edgeDelta);
            }

            float3 normalC1 = GetNormalAt(id + MarchingCubesTables.CornerOffsets[c1]);
            float3 normalC2 = GetNormalAt(id + MarchingCubesTables.CornerOffsets[c2]);
            // math.normalizeはゼロでNaNになるので安全正規化する
            float3 normal = math.lerp(normalC1, normalC2, t);
            float normalLenSq = math.lengthsq(normal);
            if (normalLenSq < 1e-10f)
            {
                normal = new float3(0f, 1f, 0f);
            }
            else
            {
                normal *= math.rsqrt(normalLenSq);
            }

            float3 color = InterpolateColor(cornerA, cornerB, valC1, valC2);

            switch (vertexIndex)
            {
                case 1:
                    tri.v1 = vertex;
                    tri.n1 = normal;
                    tri.c1 = color;
                    break;
                case 2:
                    tri.v2 = vertex;
                    tri.n2 = normal;
                    tri.c2 = color;
                    break;
                default:
                    tri.v3 = vertex;
                    tri.n3 = normal;
                    tri.c3 = color;
                    break;
            }

            return tri;
        }
    }
}
