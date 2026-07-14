using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// 保存済み表面メッシュを造形用ソリッドボクセルへ変換する
    /// </summary>
    internal static class ClayVoxelMeshSurfaceImporter
    {
        private const int SpatialResolution = 24;
        private const int CancellationCheckInterval = 8;
        private const float SurfaceDistanceFactor = 0.9f;

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
            float surfaceDistanceSq = surfaceDistance * surfaceDistance;
            var triangleHash = new TriangleSpatialHash(request.Vertices, request.Triangles, surfaceDistance);
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

            if (hasSolid)
            {
                FillSolidColumns(request.GridCount, voxels);
                hasSolid = HasAnySolid(voxels);
            }

            return new ClayVoxelMeshImporter.Result(voxels, colors, hasSolid);
        }

        private static void FillSolidColumns(int gridCount, float[] voxels)
        {
            int gridStride = gridCount * gridCount;
            for (int z = 0; z < gridCount; z++)
            {
                for (int x = 0; x < gridCount; x++)
                {
                    int minY = gridCount;
                    int maxY = -1;
                    int columnBase = x + z * gridStride;
                    for (int y = 0; y < gridCount; y++)
                    {
                        int index = columnBase + y * gridCount;
                        if (voxels[index] > 0f)
                        {
                            if (y < minY)
                            {
                                minY = y;
                            }

                            if (y > maxY)
                            {
                                maxY = y;
                            }
                        }
                    }

                    if (maxY < minY)
                    {
                        continue;
                    }

                    for (int y = minY; y <= maxY; y++)
                    {
                        voxels[columnBase + y * gridCount] = 0.5f;
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
                    return planeDistance * planeDistance;
                }
            }

            float distanceSq = SquaredDistancePointSegment(point, v0, v1);
            distanceSq = Mathf.Min(distanceSq, SquaredDistancePointSegment(point, v1, v2));
            distanceSq = Mathf.Min(distanceSq, SquaredDistancePointSegment(point, v2, v0));
            return distanceSq;
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
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-10f)
            {
                return (point - a).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            Vector3 closest = a + ab * t;
            return (point - closest).sqrMagnitude;
        }
    }
}
