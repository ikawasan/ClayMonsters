using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// 保存済み表面メッシュを造形用ソフト密度場へ変換する
    /// MarchingCubesの等値面補間が効くよう符号付き距離ベースの連続密度を書く
    /// </summary>
    internal static class ClayVoxelMeshSurfaceImporter
    {
        private const int SpatialResolution = 24;
        private const int CancellationCheckInterval = 8;
        private const float SurfaceDistanceFactor = 0.9f;
        private const float SoftBandCells = 2.5f;
        private const int DensityBlurPasses = 1;

        /// <summary>
        /// フィット済み造形空間メッシュからボクセルデータを生成する
        /// </summary>
        /// <param name="request">変換リクエスト</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>生成結果</returns>
        internal static ClayVoxelMeshImporter.Result Compute(
            ClayVoxelMeshImporter.Request request,
            CancellationToken cancellationToken)
        {
            float[] voxels = new float[request.TotalVoxelCount];
            Vector3[] colors = new Vector3[request.TotalVoxelCount];
            for (int i = 0; i < voxels.Length; i++)
            {
                voxels[i] = -0.5f;
                colors[i] = request.DefaultColorVector;
            }

            float surfaceDistance = request.Scale * SurfaceDistanceFactor;
            float softBand = Mathf.Max(request.Scale * SoftBandCells, surfaceDistance);
            float surfaceDistanceSq = surfaceDistance * surfaceDistance;
            var triangleHash = new TriangleSpatialHash(request.Vertices, request.Triangles, softBand * 2f);
            var triangleCandidates = new List<int>(64);
            bool hasSolid = false;

            for (int z = 0; z < request.GridCount; z++)
            {
                if ((z & (CancellationCheckInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                for (int y = 0; y < request.GridCount; y++)
                {
                    for (int x = 0; x < request.GridCount; x++)
                    {
                        Vector3 engineLocal = new Vector3(x, y, z) * request.Scale - request.CenterOffset;
                        if (engineLocal.x < request.BoundsMinEngine.x || engineLocal.x > request.BoundsMaxEngine.x
                            || engineLocal.y < request.BoundsMinEngine.y || engineLocal.y > request.BoundsMaxEngine.y
                            || engineLocal.z < request.BoundsMinEngine.z || engineLocal.z > request.BoundsMaxEngine.z)
                        {
                            continue;
                        }

                        if (!triangleHash.TryIsNearSurface(engineLocal, surfaceDistanceSq, triangleCandidates))
                        {
                            continue;
                        }

                        int index = x + y * request.GridCount + z * request.GridCount * request.GridCount;
                        voxels[index] = 0.5f;
                        hasSolid = true;
                    }
                }
            }

            if (!hasSolid)
            {
                return new ClayVoxelMeshImporter.Result(voxels, colors, false);
            }

            // 列min-max塗りは穴を埋めるので外側洪水で内部だけ塗る
            FillSolidByExteriorFlood(request.GridCount, voxels);
            ApplySoftSignedDistanceField(
                request,
                voxels,
                triangleHash,
                triangleCandidates,
                softBand,
                cancellationToken);
            for (int blurPass = 0; blurPass < DensityBlurPasses; blurPass++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BlurDensities(request.GridCount, voxels);
            }

            AssignSurfaceColors(
                request,
                voxels,
                colors,
                softBand,
                cancellationToken);

            return new ClayVoxelMeshImporter.Result(voxels, colors, HasAnySolid(voxels));
        }

        /// <summary>
        /// 表面を壁としてグリッド外縁から洪水し外部と不通の内部だけを固体にする
        /// 外部に繋がる穴は塗らない
        /// </summary>
        /// <param name="gridCount">グリッド頂点数</param>
        /// <param name="voxels">密度配列表面シードは正</param>
        private static void FillSolidByExteriorFlood(int gridCount, float[] voxels)
        {
            int total = voxels.Length;
            var isSurface = new bool[total];
            for (int i = 0; i < total; i++)
            {
                isSurface[i] = voxels[i] > 0f;
            }

            var isExterior = new bool[total];
            var queue = new Queue<int>(gridCount * gridCount * 2);
            int strideY = gridCount;
            int strideZ = gridCount * gridCount;
            int last = gridCount - 1;

            for (int z = 0; z < gridCount; z++)
            {
                for (int y = 0; y < gridCount; y++)
                {
                    TryEnqueueExterior(0, y, z, gridCount, strideY, strideZ, isSurface, isExterior, queue);
                    TryEnqueueExterior(last, y, z, gridCount, strideY, strideZ, isSurface, isExterior, queue);
                }
            }

            for (int z = 0; z < gridCount; z++)
            {
                for (int x = 0; x < gridCount; x++)
                {
                    TryEnqueueExterior(x, 0, z, gridCount, strideY, strideZ, isSurface, isExterior, queue);
                    TryEnqueueExterior(x, last, z, gridCount, strideY, strideZ, isSurface, isExterior, queue);
                }
            }

            for (int y = 0; y < gridCount; y++)
            {
                for (int x = 0; x < gridCount; x++)
                {
                    TryEnqueueExterior(x, y, 0, gridCount, strideY, strideZ, isSurface, isExterior, queue);
                    TryEnqueueExterior(x, y, last, gridCount, strideY, strideZ, isSurface, isExterior, queue);
                }
            }

            int[] neighborOffsets =
            {
                1,
                -1,
                strideY,
                -strideY,
                strideZ,
                -strideZ
            };

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % gridCount;
                int y = (index / strideY) % gridCount;
                int z = index / strideZ;

                for (int n = 0; n < neighborOffsets.Length; n++)
                {
                    int next = index + neighborOffsets[n];
                    if (next < 0 || next >= total)
                    {
                        continue;
                    }

                    int nx = next % gridCount;
                    int ny = (next / strideY) % gridCount;
                    int nz = next / strideZ;
                    // 端を跨いだ隣接を除外する
                    if (Mathf.Abs(nx - x) + Mathf.Abs(ny - y) + Mathf.Abs(nz - z) != 1)
                    {
                        continue;
                    }

                    if (isSurface[next] || isExterior[next])
                    {
                        continue;
                    }

                    isExterior[next] = true;
                    queue.Enqueue(next);
                }
            }

            for (int i = 0; i < total; i++)
            {
                if (!isSurface[i] && !isExterior[i])
                {
                    voxels[i] = 0.5f;
                }
            }
        }

        private static void TryEnqueueExterior(
            int x,
            int y,
            int z,
            int gridCount,
            int strideY,
            int strideZ,
            bool[] isSurface,
            bool[] isExterior,
            Queue<int> queue)
        {
            int index = x + y * strideY + z * strideZ;
            if (isSurface[index] || isExterior[index])
            {
                return;
            }

            isExterior[index] = true;
            queue.Enqueue(index);
        }

        private static void ApplySoftSignedDistanceField(
            ClayVoxelMeshImporter.Request request,
            float[] voxels,
            TriangleSpatialHash triangleHash,
            List<int> triangleCandidates,
            float softBand,
            CancellationToken cancellationToken)
        {
            int gridCount = request.GridCount;
            float softBandSq = softBand * softBand;
            var solidMask = new bool[voxels.Length];
            for (int i = 0; i < voxels.Length; i++)
            {
                solidMask[i] = voxels[i] > 0f;
            }

            for (int z = 0; z < gridCount; z++)
            {
                if ((z & (CancellationCheckInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                for (int y = 0; y < gridCount; y++)
                {
                    for (int x = 0; x < gridCount; x++)
                    {
                        int index = x + y * gridCount + z * gridCount * gridCount;
                        Vector3 engineLocal = new Vector3(x, y, z) * request.Scale - request.CenterOffset;
                        bool inside = solidMask[index];
                        if (!inside
                            && !triangleHash.TryIsNearSurface(engineLocal, softBandSq, triangleCandidates))
                        {
                            voxels[index] = -0.5f;
                            continue;
                        }

                        if (!triangleHash.TryGetMinDistanceSquared(
                                engineLocal,
                                softBandSq,
                                triangleCandidates,
                                out float distanceSq))
                        {
                            voxels[index] = inside ? 0.5f : -0.5f;
                        }
                        else
                        {
                            float distance = Mathf.Sqrt(distanceSq);
                            float signedDistance = inside ? -distance : distance;
                            voxels[index] = Mathf.Clamp(-signedDistance / softBand, -0.5f, 0.5f);
                        }
                    }
                }
            }
        }

        private static void AssignSurfaceColors(
            ClayVoxelMeshImporter.Request request,
            float[] voxels,
            Vector3[] colors,
            float softBand,
            CancellationToken cancellationToken)
        {
            if (request.VertexColors == null
                || request.VertexColors.Length != request.Vertices.Length)
            {
                return;
            }

            int gridCount = request.GridCount;
            float scale = request.Scale;
            Vector3 centerOffset = request.CenterOffset;
            // ぼかし後の等値面ずれも拾うよう広めに塗る
            float paintRadius = softBand * 3f;
            float paintRadiusSq = paintRadius * paintRadius;
            var bestDistanceSq = new float[colors.Length];
            for (int i = 0; i < bestDistanceSq.Length; i++)
            {
                bestDistanceSq[i] = float.PositiveInfinity;
            }

            Vector3[] vertices = request.Vertices;
            int[] triangles = request.Triangles;
            Color[] vertexColors = request.VertexColors;
            int triangleCount = triangles.Length / 3;
            int paintedVoxelCount = 0;

            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                if ((triangleIndex & (CancellationCheckInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                int triangleOffset = triangleIndex * 3;
                int i0 = triangles[triangleOffset];
                int i1 = triangles[triangleOffset + 1];
                int i2 = triangles[triangleOffset + 2];
                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Color color0 = vertexColors[i0];
                Color color1 = vertexColors[i1];
                Color color2 = vertexColors[i2];

                Vector3 triMin = Vector3.Min(v0, Vector3.Min(v1, v2));
                Vector3 triMax = Vector3.Max(v0, Vector3.Max(v1, v2));
                triMin -= Vector3.one * paintRadius;
                triMax += Vector3.one * paintRadius;

                if (!TryGetGridRange(
                        triMin,
                        triMax,
                        scale,
                        centerOffset,
                        gridCount,
                        out int minX,
                        out int minY,
                        out int minZ,
                        out int maxX,
                        out int maxY,
                        out int maxZ))
                {
                    continue;
                }

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            int index = x + y * gridCount + z * gridCount * gridCount;
                            Vector3 engineLocal = new Vector3(x, y, z) * scale - centerOffset;
                            Vector3 closest = ClosestPointOnTriangle(engineLocal, v0, v1, v2);
                            float distanceSq = (closest - engineLocal).sqrMagnitude;
                            if (distanceSq > paintRadiusSq || distanceSq >= bestDistanceSq[index])
                            {
                                continue;
                            }

                            if (!TryGetBarycentric(closest, v0, v1, v2, out float u, out float v, out float w))
                            {
                                u = 1f;
                                v = 0f;
                                w = 0f;
                            }

                            // 辺上の微小負値を落とす
                            u = Mathf.Max(0f, u);
                            v = Mathf.Max(0f, v);
                            w = Mathf.Max(0f, w);
                            float weightSum = u + v + w;
                            if (weightSum < 1e-6f)
                            {
                                u = 1f;
                                v = 0f;
                                w = 0f;
                                weightSum = 1f;
                            }

                            float inv = 1f / weightSum;
                            u *= inv;
                            v *= inv;
                            w *= inv;

                            bool wasUnpainted = float.IsPositiveInfinity(bestDistanceSq[index]);
                            bestDistanceSq[index] = distanceSq;
                            // u=v0 w=v1 v=v2
                            colors[index] = new Vector3(
                                color0.r * u + color1.r * w + color2.r * v,
                                color0.g * u + color1.g * w + color2.g * v,
                                color0.b * u + color1.b * w + color2.b * v);
                            if (wasUnpainted)
                            {
                                paintedVoxelCount++;
                            }
                        }
                    }
                }
            }

            int dilatedCount = DilatePaintedColorsIntoSolid(
                gridCount,
                voxels,
                colors,
                request.DefaultColorVector);

            int unpaintedSurfaceCount = 0;
            for (int i = 0; i < voxels.Length; i++)
            {
                if (Mathf.Abs(voxels[i]) > 0.48f)
                {
                    continue;
                }

                if ((colors[i] - request.DefaultColorVector).sqrMagnitude <= 0.0004f)
                {
                    unpaintedSurfaceCount++;
                }
            }

            Debug.Log(
                $"[ClayVoxelMeshSurfaceImporter] 色付け painted={paintedVoxelCount} dilated={dilatedCount} unpaintedSurface={unpaintedSurfaceCount}");
        }

        private static bool TryGetGridRange(
            Vector3 worldMin,
            Vector3 worldMax,
            float scale,
            Vector3 centerOffset,
            int gridCount,
            out int minX,
            out int minY,
            out int minZ,
            out int maxX,
            out int maxY,
            out int maxZ)
        {
            minX = Mathf.FloorToInt((worldMin.x + centerOffset.x) / scale);
            minY = Mathf.FloorToInt((worldMin.y + centerOffset.y) / scale);
            minZ = Mathf.FloorToInt((worldMin.z + centerOffset.z) / scale);
            maxX = Mathf.CeilToInt((worldMax.x + centerOffset.x) / scale);
            maxY = Mathf.CeilToInt((worldMax.y + centerOffset.y) / scale);
            maxZ = Mathf.CeilToInt((worldMax.z + centerOffset.z) / scale);

            if (maxX < 0 || maxY < 0 || maxZ < 0
                || minX >= gridCount || minY >= gridCount || minZ >= gridCount)
            {
                minX = minY = minZ = maxX = maxY = maxZ = 0;
                return false;
            }

            minX = Mathf.Clamp(minX, 0, gridCount - 1);
            minY = Mathf.Clamp(minY, 0, gridCount - 1);
            minZ = Mathf.Clamp(minZ, 0, gridCount - 1);
            maxX = Mathf.Clamp(maxX, 0, gridCount - 1);
            maxY = Mathf.Clamp(maxY, 0, gridCount - 1);
            maxZ = Mathf.Clamp(maxZ, 0, gridCount - 1);
            return true;
        }

        private static int DilatePaintedColorsIntoSolid(
            int gridCount,
            float[] voxels,
            Vector3[] colors,
            Vector3 defaultColor)
        {
            float defaultThresholdSq = 0.02f * 0.02f;
            int strideY = gridCount;
            int strideZ = gridCount * gridCount;
            var nextColors = (Vector3[])colors.Clone();
            int totalDilated = 0;
            int maxPasses = Mathf.Max(3, gridCount);
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool anyFilled = false;
                for (int z = 1; z < gridCount - 1; z++)
                {
                    for (int y = 1; y < gridCount - 1; y++)
                    {
                        for (int x = 1; x < gridCount - 1; x++)
                        {
                            int index = x + y * strideY + z * strideZ;
                            if (voxels[index] < -0.48f)
                            {
                                continue;
                            }

                            if ((colors[index] - defaultColor).sqrMagnitude > defaultThresholdSq)
                            {
                                nextColors[index] = colors[index];
                                continue;
                            }

                            Vector3 sum = Vector3.zero;
                            int count = 0;
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        if (dx == 0 && dy == 0 && dz == 0)
                                        {
                                            continue;
                                        }

                                        int sampleIndex = (x + dx) + (y + dy) * strideY + (z + dz) * strideZ;
                                        if (voxels[sampleIndex] < -0.48f)
                                        {
                                            continue;
                                        }

                                        Vector3 sampleColor = colors[sampleIndex];
                                        if ((sampleColor - defaultColor).sqrMagnitude <= defaultThresholdSq)
                                        {
                                            continue;
                                        }

                                        sum += sampleColor;
                                        count++;
                                    }
                                }
                            }

                            if (count > 0)
                            {
                                nextColors[index] = sum / count;
                                anyFilled = true;
                                totalDilated++;
                            }
                        }
                    }
                }

                System.Array.Copy(nextColors, colors, colors.Length);
                if (!anyFilled)
                {
                    break;
                }
            }

            return totalDilated;
        }

        private static void BlurDensities(int gridCount, float[] voxels)
        {
            float[] source = (float[])voxels.Clone();
            int strideY = gridCount;
            int strideZ = gridCount * gridCount;
            for (int z = 1; z < gridCount - 1; z++)
            {
                for (int y = 1; y < gridCount - 1; y++)
                {
                    for (int x = 1; x < gridCount - 1; x++)
                    {
                        float sum = 0f;
                        float weightSum = 0f;
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    float weight = (dx == 0 && dy == 0 && dz == 0) ? 2f : 1f;
                                    int sampleIndex = (x + dx) + (y + dy) * strideY + (z + dz) * strideZ;
                                    sum += source[sampleIndex] * weight;
                                    weightSum += weight;
                                }
                            }
                        }

                        int index = x + y * strideY + z * strideZ;
                        voxels[index] = Mathf.Clamp(sum / weightSum, -0.5f, 0.5f);
                    }
                }
            }
        }

        private static bool HasAnySolid(float[] voxels)
        {
            for (int i = 0; i < voxels.Length; i++)
            {
                if (voxels[i] > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class TriangleSpatialHash
        {
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            private readonly List<int>[] cells;
            private readonly Vector3 origin;
            private readonly Vector3 cellSize;
            private readonly int resolutionX;
            private readonly int resolutionY;
            private readonly int resolutionZ;
            private readonly int cellSearchRadius;
            private readonly int[] visitStamp;
            private int stamp;

            public TriangleSpatialHash(Vector3[] vertices, int[] triangles, float surfaceDistance)
            {
                this.vertices = vertices;
                this.triangles = triangles;
                int triangleCount = triangles.Length / 3;
                visitStamp = new int[triangleCount];

                Bounds bounds = new Bounds(vertices[0], Vector3.zero);
                for (int i = 1; i < vertices.Length; i++)
                {
                    bounds.Encapsulate(vertices[i]);
                }

                Vector3 boundsSize = bounds.size;
                boundsSize.x = Mathf.Max(boundsSize.x, 1e-4f);
                boundsSize.y = Mathf.Max(boundsSize.y, 1e-4f);
                boundsSize.z = Mathf.Max(boundsSize.z, 1e-4f);
                origin = bounds.min;
                cellSize = boundsSize / SpatialResolution;
                resolutionX = SpatialResolution;
                resolutionY = SpatialResolution;
                resolutionZ = SpatialResolution;
                float minCell = Mathf.Min(cellSize.x, Mathf.Min(cellSize.y, cellSize.z));
                cellSearchRadius = Mathf.Max(1, Mathf.CeilToInt(surfaceDistance / minCell) + 1);
                cells = new List<int>[resolutionX * resolutionY * resolutionZ];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new List<int>(8);
                }

                for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                {
                    int triangleOffset = triangleIndex * 3;
                    Vector3 v0 = vertices[triangles[triangleOffset]];
                    Vector3 v1 = vertices[triangles[triangleOffset + 1]];
                    Vector3 v2 = vertices[triangles[triangleOffset + 2]];
                    Vector3 triMin = Vector3.Min(v0, Vector3.Min(v1, v2));
                    Vector3 triMax = Vector3.Max(v0, Vector3.Max(v1, v2));
                    AddTriangleToCells(triangleIndex, triMin, triMax);
                }
            }

            public bool TryIsNearSurface(Vector3 point, float surfaceDistanceSq, List<int> candidates)
            {
                CollectTriangles(point, candidates);
                for (int i = 0; i < candidates.Count; i++)
                {
                    int triangleOffset = candidates[i] * 3;
                    Vector3 v0 = vertices[triangles[triangleOffset]];
                    Vector3 v1 = vertices[triangles[triangleOffset + 1]];
                    Vector3 v2 = vertices[triangles[triangleOffset + 2]];
                    if (SquaredDistancePointTriangle(point, v0, v1, v2) <= surfaceDistanceSq)
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool TryGetMinDistanceSquared(
                Vector3 point,
                float maxDistanceSq,
                List<int> candidates,
                out float minDistanceSq)
            {
                minDistanceSq = float.MaxValue;
                CollectTriangles(point, candidates);
                if (candidates.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    int triangleOffset = candidates[i] * 3;
                    Vector3 v0 = vertices[triangles[triangleOffset]];
                    Vector3 v1 = vertices[triangles[triangleOffset + 1]];
                    Vector3 v2 = vertices[triangles[triangleOffset + 2]];
                    float distanceSq = SquaredDistancePointTriangle(point, v0, v1, v2);
                    if (distanceSq < minDistanceSq)
                    {
                        minDistanceSq = distanceSq;
                    }
                }

                return minDistanceSq <= maxDistanceSq;
            }

            public bool TrySampleInterpolatedColor(
                Vector3 point,
                float maxDistanceSq,
                Color[] vertexColors,
                List<int> candidates,
                out Vector3 color)
            {
                color = default;
                CollectTriangles(point, candidates);
                if (candidates.Count == 0 || vertexColors == null)
                {
                    return false;
                }

                float minDistanceSq = float.MaxValue;
                int bestTriangle = -1;
                Vector3 bestPoint = point;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int triangleIndex = candidates[i];
                    int triangleOffset = triangleIndex * 3;
                    int i0 = triangles[triangleOffset];
                    int i1 = triangles[triangleOffset + 1];
                    int i2 = triangles[triangleOffset + 2];
                    Vector3 v0 = vertices[i0];
                    Vector3 v1 = vertices[i1];
                    Vector3 v2 = vertices[i2];
                    Vector3 closest = ClosestPointOnTriangle(point, v0, v1, v2);
                    float distanceSq = (closest - point).sqrMagnitude;
                    if (distanceSq < minDistanceSq)
                    {
                        minDistanceSq = distanceSq;
                        bestTriangle = triangleIndex;
                        bestPoint = closest;
                    }
                }

                if (bestTriangle < 0 || minDistanceSq > maxDistanceSq)
                {
                    return false;
                }

                int bestOffset = bestTriangle * 3;
                int a = triangles[bestOffset];
                int b = triangles[bestOffset + 1];
                int c = triangles[bestOffset + 2];
                Vector3 vA = vertices[a];
                Vector3 vB = vertices[b];
                Vector3 vC = vertices[c];
                if (!TryGetBarycentric(bestPoint, vA, vB, vC, out float u, out float v, out float w))
                {
                    u = 1f;
                    v = 0f;
                    w = 0f;
                }

                Color colorA = vertexColors[a];
                Color colorB = vertexColors[b];
                Color colorC = vertexColors[c];
                // TryGetBarycentricのu=v0 v=v2 w=v1
                color = new Vector3(
                    colorA.r * u + colorB.r * w + colorC.r * v,
                    colorA.g * u + colorB.g * w + colorC.g * v,
                    colorA.b * u + colorB.b * w + colorC.b * v);
                return true;
            }

            private void CollectTriangles(Vector3 point, List<int> output)
            {
                stamp++;
                output.Clear();
                if (!TryGetCell(point, out int baseX, out int baseY, out int baseZ))
                {
                    return;
                }

                for (int z = baseZ - cellSearchRadius; z <= baseZ + cellSearchRadius; z++)
                {
                    for (int y = baseY - cellSearchRadius; y <= baseY + cellSearchRadius; y++)
                    {
                        for (int x = baseX - cellSearchRadius; x <= baseX + cellSearchRadius; x++)
                        {
                            if (x < 0 || y < 0 || z < 0
                                || x >= resolutionX || y >= resolutionY || z >= resolutionZ)
                            {
                                continue;
                            }

                            List<int> cell = cells[CellIndex(x, y, z)];
                            for (int i = 0; i < cell.Count; i++)
                            {
                                int triangleIndex = cell[i];
                                if (visitStamp[triangleIndex] == stamp)
                                {
                                    continue;
                                }

                                visitStamp[triangleIndex] = stamp;
                                output.Add(triangleIndex);
                            }
                        }
                    }
                }
            }

            private void AddTriangleToCells(int triangleIndex, Vector3 triMin, Vector3 triMax)
            {
                if (!TryGetCellRange(triMin, triMax, out int minX, out int minY, out int minZ, out int maxX, out int maxY, out int maxZ))
                {
                    return;
                }

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            cells[CellIndex(x, y, z)].Add(triangleIndex);
                        }
                    }
                }
            }

            private bool TryGetCell(Vector3 point, out int cellX, out int cellY, out int cellZ)
            {
                cellX = Mathf.FloorToInt((point.x - origin.x) / cellSize.x);
                cellY = Mathf.FloorToInt((point.y - origin.y) / cellSize.y);
                cellZ = Mathf.FloorToInt((point.z - origin.z) / cellSize.z);
                return cellX >= 0 && cellY >= 0 && cellZ >= 0
                    && cellX < resolutionX && cellY < resolutionY && cellZ < resolutionZ;
            }

            private bool TryGetCellRange(
                Vector3 min,
                Vector3 max,
                out int minX,
                out int minY,
                out int minZ,
                out int maxX,
                out int maxY,
                out int maxZ)
            {
                minX = Mathf.Clamp(Mathf.FloorToInt((min.x - origin.x) / cellSize.x), 0, resolutionX - 1);
                minY = Mathf.Clamp(Mathf.FloorToInt((min.y - origin.y) / cellSize.y), 0, resolutionY - 1);
                minZ = Mathf.Clamp(Mathf.FloorToInt((min.z - origin.z) / cellSize.z), 0, resolutionZ - 1);
                maxX = Mathf.Clamp(Mathf.FloorToInt((max.x - origin.x) / cellSize.x), 0, resolutionX - 1);
                maxY = Mathf.Clamp(Mathf.FloorToInt((max.y - origin.y) / cellSize.y), 0, resolutionY - 1);
                maxZ = Mathf.Clamp(Mathf.FloorToInt((max.z - origin.z) / cellSize.z), 0, resolutionZ - 1);
                return true;
            }

            private int CellIndex(int x, int y, int z) => x + y * resolutionX + z * resolutionX * resolutionY;
        }

        private static float SquaredDistancePointTriangle(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 closest = ClosestPointOnTriangle(point, v0, v1, v2);
            return (closest - point).sqrMagnitude;
        }

        private static Vector3 ClosestPointOnTriangle(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 edge0 = v1 - v0;
            Vector3 edge1 = v2 - v0;
            Vector3 normal = Vector3.Cross(edge0, edge1);
            float normalLengthSq = normal.sqrMagnitude;
            if (normalLengthSq > 1e-10f)
            {
                Vector3 planeNormal = normal / Mathf.Sqrt(normalLengthSq);
                float planeDistance = Vector3.Dot(point - v0, planeNormal);
                Vector3 projected = point - planeNormal * planeDistance;
                if (TryGetBarycentric(projected, v0, v1, v2, out float u, out float v, out float w)
                    && u >= 0f && v >= 0f && w >= 0f)
                {
                    return projected;
                }
            }

            Vector3 closest01 = ClosestPointOnSegment(point, v0, v1);
            Vector3 closest12 = ClosestPointOnSegment(point, v1, v2);
            Vector3 closest20 = ClosestPointOnSegment(point, v2, v0);
            float d01 = (closest01 - point).sqrMagnitude;
            float d12 = (closest12 - point).sqrMagnitude;
            float d20 = (closest20 - point).sqrMagnitude;
            if (d01 <= d12 && d01 <= d20)
            {
                return closest01;
            }

            if (d12 <= d20)
            {
                return closest12;
            }

            return closest20;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-10f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return a + ab * t;
        }

        private static bool TryGetBarycentric(
            Vector3 point,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float u,
            out float v,
            out float w)
        {
            Vector3 v0v1 = v1 - v0;
            Vector3 v0v2 = v2 - v0;
            Vector3 v0p = point - v0;
            float dot00 = Vector3.Dot(v0v2, v0v2);
            float dot01 = Vector3.Dot(v0v2, v0v1);
            float dot02 = Vector3.Dot(v0v2, v0p);
            float dot11 = Vector3.Dot(v0v1, v0v1);
            float dot12 = Vector3.Dot(v0v1, v0p);
            float denominator = dot00 * dot11 - dot01 * dot01;
            if (Mathf.Abs(denominator) < 1e-10f)
            {
                u = v = w = 0f;
                return false;
            }

            float inverseDenominator = 1f / denominator;
            v = (dot11 * dot02 - dot01 * dot12) * inverseDenominator;
            w = (dot00 * dot12 - dot01 * dot02) * inverseDenominator;
            u = 1f - v - w;
            return true;
        }

        private static float SquaredDistancePointSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 closest = ClosestPointOnSegment(point, a, b);
            return (point - closest).sqrMagnitude;
        }
    }
}
