using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// メッシュを造形グリッド用ボクセルへ変換するCPU処理
    /// </summary>
    internal static class ClayVoxelMeshImporter
    {
        private const int SpatialResolution = 24;
        private const int CancellationCheckInterval = 8;

        /// <summary>
        /// ボクセル化リクエスト
        /// </summary>
        internal readonly struct Request
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;
            public readonly Color[] VertexColors;
            public readonly Matrix4x4 EngineLocalToWorld;
            public readonly Matrix4x4 WorldToMeshLocal;
            public readonly int GridCount;
            public readonly int TotalVoxelCount;
            public readonly float Scale;
            public readonly Vector3 CenterOffset;
            public readonly Vector3 BoundsMinEngine;
            public readonly Vector3 BoundsMaxEngine;
            public readonly Vector3 DefaultColorVector;
            public readonly ClayVoxelMeshImportFitter.FitParams FitParams;
            public readonly bool UseFittedEngineVertices;

            public Request(
                Vector3[] vertices,
                int[] triangles,
                Color[] vertexColors,
                int gridCount,
                int totalVoxelCount,
                float scale,
                Vector3 centerOffset,
                Vector3 boundsMinEngine,
                Vector3 boundsMaxEngine,
                Vector3 defaultColorVector,
                bool useFittedEngineVertices)
            {
                Vertices = vertices;
                Triangles = triangles;
                VertexColors = vertexColors;
                EngineLocalToWorld = Matrix4x4.identity;
                WorldToMeshLocal = Matrix4x4.identity;
                FitParams = new ClayVoxelMeshImportFitter.FitParams(Vector3.zero, 1f);
                GridCount = gridCount;
                TotalVoxelCount = totalVoxelCount;
                Scale = scale;
                CenterOffset = centerOffset;
                BoundsMinEngine = boundsMinEngine;
                BoundsMaxEngine = boundsMaxEngine;
                DefaultColorVector = defaultColorVector;
                UseFittedEngineVertices = useFittedEngineVertices;
            }

            public Request(
                Vector3[] vertices,
                int[] triangles,
                Color[] vertexColors,
                Matrix4x4 engineLocalToWorld,
                Matrix4x4 worldToMeshLocal,
                int gridCount,
                int totalVoxelCount,
                float scale,
                Vector3 centerOffset,
                Vector3 boundsMinEngine,
                Vector3 boundsMaxEngine,
                Vector3 defaultColorVector,
                ClayVoxelMeshImportFitter.FitParams fitParams)
            {
                Vertices = vertices;
                Triangles = triangles;
                VertexColors = vertexColors;
                EngineLocalToWorld = engineLocalToWorld;
                WorldToMeshLocal = worldToMeshLocal;
                GridCount = gridCount;
                TotalVoxelCount = totalVoxelCount;
                Scale = scale;
                CenterOffset = centerOffset;
                BoundsMinEngine = boundsMinEngine;
                BoundsMaxEngine = boundsMaxEngine;
                DefaultColorVector = defaultColorVector;
                FitParams = fitParams;
                UseFittedEngineVertices = false;
            }
        }

        /// <summary>
        /// ボクセル化結果
        /// </summary>
        internal readonly struct Result
        {
            public readonly float[] Voxels;
            public readonly Vector3[] Colors;
            public readonly bool HasSolid;

            public Result(float[] voxels, Vector3[] colors, bool hasSolid)
            {
                Voxels = voxels;
                Colors = colors;
                HasSolid = hasSolid;
            }
        }

        /// <summary>
        /// ワーカースレッド上でボクセルデータを生成する
        /// </summary>
        /// <param name="request">変換リクエスト</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>生成結果</returns>
        internal static Result Compute(Request request, CancellationToken cancellationToken)
        {
            float[] voxels = new float[request.TotalVoxelCount];
            Vector3[] colors = new Vector3[request.TotalVoxelCount];
            for (int i = 0; i < voxels.Length; i++)
            {
                voxels[i] = -0.5f;
                colors[i] = request.DefaultColorVector;
            }

            var triangleHash = new TriangleSpatialHash(request.Vertices, request.Triangles);
            var vertexHash = new VertexSpatialHash(request.Vertices);
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

                        Vector3 samplePoint = request.UseFittedEngineVertices
                            ? engineLocal
                            : ClayVoxelMeshImportFitter.MapEngineLocalToMeshLocal(
                                engineLocal,
                                request.FitParams,
                                request.EngineLocalToWorld,
                                request.WorldToMeshLocal);
                        if (!triangleHash.IsInside(samplePoint, triangleCandidates))
                        {
                            continue;
                        }

                        int index = x + y * request.GridCount + z * request.GridCount * request.GridCount;
                        voxels[index] = 0.5f;
                        colors[index] = SampleNearestVertexColor(
                            request.Vertices,
                            request.VertexColors,
                            samplePoint,
                            request.DefaultColorVector,
                            vertexHash);
                        hasSolid = true;
                    }
                }
            }

            return new Result(voxels, colors, hasSolid);
        }

        private static Vector3 SampleNearestVertexColor(
            Vector3[] vertices,
            Color[] meshVertexColors,
            Vector3 meshLocalPoint,
            Vector3 defaultColorVector,
            VertexSpatialHash vertexHash)
        {
            if (meshVertexColors == null || meshVertexColors.Length != vertices.Length)
            {
                return defaultColorVector;
            }

            if (!vertexHash.TryFindNearest(meshLocalPoint, out int nearestIndex))
            {
                return defaultColorVector;
            }

            Color storageColor = meshVertexColors[nearestIndex];
            return new Vector3(storageColor.r, storageColor.g, storageColor.b);
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
            private readonly int[] visitStamp;
            private int stamp;

            public TriangleSpatialHash(Vector3[] vertices, int[] triangles)
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

            public bool IsInside(Vector3 meshLocalPoint, List<int> candidates)
            {
                CollectTriangles(meshLocalPoint, candidates);
                int hits = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int triangleOffset = candidates[i] * 3;
                    if (RayIntersectsTriangle(
                            meshLocalPoint,
                            Vector3.right,
                            vertices[triangles[triangleOffset]],
                            vertices[triangles[triangleOffset + 1]],
                            vertices[triangles[triangleOffset + 2]]))
                    {
                        hits++;
                    }
                }

                return (hits & 1) == 1;
            }

            private void CollectTriangles(Vector3 point, List<int> output)
            {
                stamp++;
                output.Clear();
                if (!TryGetCell(point, out int baseX, out int baseY, out int baseZ))
                {
                    return;
                }

                for (int z = baseZ - 1; z <= baseZ + 1; z++)
                {
                    for (int y = baseY - 1; y <= baseY + 1; y++)
                    {
                        for (int x = baseX - 1; x <= baseX + 1; x++)
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

        private sealed class VertexSpatialHash
        {
            private readonly Vector3[] vertices;
            private readonly List<int>[] cells;
            private readonly Vector3 origin;
            private readonly Vector3 cellSize;
            private readonly int resolutionX;
            private readonly int resolutionY;
            private readonly int resolutionZ;

            public VertexSpatialHash(Vector3[] vertices)
            {
                this.vertices = vertices;
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
                cells = new List<int>[resolutionX * resolutionY * resolutionZ];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new List<int>(4);
                }

                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    if (!TryGetCell(vertices[vertexIndex], out int cellX, out int cellY, out int cellZ))
                    {
                        continue;
                    }

                    cells[CellIndex(cellX, cellY, cellZ)].Add(vertexIndex);
                }
            }

            public bool TryFindNearest(Vector3 point, out int nearestIndex)
            {
                nearestIndex = -1;
                if (!TryGetCell(point, out int baseX, out int baseY, out int baseZ))
                {
                    return false;
                }

                float nearestDistance = float.MaxValue;
                for (int z = baseZ - 1; z <= baseZ + 1; z++)
                {
                    for (int y = baseY - 1; y <= baseY + 1; y++)
                    {
                        for (int x = baseX - 1; x <= baseX + 1; x++)
                        {
                            if (x < 0 || y < 0 || z < 0
                                || x >= resolutionX || y >= resolutionY || z >= resolutionZ)
                            {
                                continue;
                            }

                            List<int> cell = cells[CellIndex(x, y, z)];
                            for (int i = 0; i < cell.Count; i++)
                            {
                                int vertexIndex = cell[i];
                                float distance = (vertices[vertexIndex] - point).sqrMagnitude;
                                if (distance < nearestDistance)
                                {
                                    nearestDistance = distance;
                                    nearestIndex = vertexIndex;
                                }
                            }
                        }
                    }
                }

                return nearestIndex >= 0;
            }

            private bool TryGetCell(Vector3 point, out int cellX, out int cellY, out int cellZ)
            {
                cellX = Mathf.FloorToInt((point.x - origin.x) / cellSize.x);
                cellY = Mathf.FloorToInt((point.y - origin.y) / cellSize.y);
                cellZ = Mathf.FloorToInt((point.z - origin.z) / cellSize.z);
                return cellX >= 0 && cellY >= 0 && cellZ >= 0
                    && cellX < resolutionX && cellY < resolutionY && cellZ < resolutionZ;
            }

            private int CellIndex(int x, int y, int z) => x + y * resolutionX + z * resolutionX * resolutionY;
        }

        private static bool RayIntersectsTriangle(
            Vector3 origin,
            Vector3 direction,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2)
        {
            const float epsilon = 1e-6f;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 cross = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, cross);
            if (determinant > -epsilon && determinant < epsilon)
            {
                return false;
            }

            float inverseDeterminant = 1f / determinant;
            Vector3 distanceVector = origin - v0;
            float u = inverseDeterminant * Vector3.Dot(distanceVector, cross);
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 crossDistance = Vector3.Cross(distanceVector, edge1);
            float v = inverseDeterminant * Vector3.Dot(direction, crossDistance);
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            float t = inverseDeterminant * Vector3.Dot(edge2, crossDistance);
            return t > epsilon;
        }
    }
}
