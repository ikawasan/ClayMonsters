#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClayEditor
{
    /// <summary>
    /// FBX等のメッシュとマテリアルテクスチャを頂点カラーへ焼き込むEditor専用処理
    /// 三角形コーナーを分解しUV単位で正確に色を載せる
    /// </summary>
    public static class ClayFbxTextureToVertexColorBaker
    {
        private const int MaxBakeTextureSize = 1024;
        private const int ParallelTriangleChunkSize = 256;
        private const int ParallelTriangleThreshold = 64;

        private static readonly string[] AlbedoPropertyNames =
        {
            "_BaseMap",
            "_MainTex",
            "_BaseColorMap",
            "_Albedo",
            "_Diffuse",
            "_BaseColorTexture"
        };

        private static readonly string[] ColorPropertyNames =
        {
            "_BaseColor",
            "_Color",
            "_TintColor"
        };

        /// <summary>
        /// ルート配下のメッシュとテクスチャを結合し頂点カラー付きメッシュを生成する
        /// </summary>
        /// <param name="sourceRoot">FBXインスタンス等のルート</param>
        /// <param name="defaultColor">テクスチャが無い頂点の色</param>
        /// <param name="subdivisionDepth">色解像度用分割深度</param>
        /// <param name="maxUvEdgeLength">分割判定のUV辺長上限</param>
        /// <param name="combinedMesh">生成メッシュ呼び出し側でDestroyする</param>
        /// <param name="errorMessage">失敗理由</param>
        /// <returns>成功した場合true</returns>
        public static bool TryBakeCombinedMesh(
            GameObject sourceRoot,
            Color defaultColor,
            int subdivisionDepth,
            float maxUvEdgeLength,
            out Mesh combinedMesh,
            out string errorMessage)
        {
            if (!TryExtractBakeSources(
                    sourceRoot,
                    defaultColor,
                    subdivisionDepth,
                    maxUvEdgeLength,
                    out BakeSources sources,
                    out errorMessage))
            {
                combinedMesh = null;
                return false;
            }

            BakeGeometry geometry = BakeGeometryFromSources(sources, CancellationToken.None);
            return TryCreateMeshFromGeometry(sources.RootName, geometry, out combinedMesh, out errorMessage);
        }

        /// <summary>
        /// ルート配下のメッシュとテクスチャを非同期で頂点カラーへ焼き込む
        /// メインスレッドでは抽出のみ行い重い分割と色サンプルはワーカーで実行する
        /// </summary>
        public static async UniTask<(bool success, Mesh combinedMesh, string errorMessage)> TryBakeCombinedMeshAsync(
            GameObject sourceRoot,
            Color defaultColor,
            int subdivisionDepth,
            float maxUvEdgeLength,
            CancellationToken cancellationToken)
        {
            if (!TryExtractBakeSources(
                    sourceRoot,
                    defaultColor,
                    subdivisionDepth,
                    maxUvEdgeLength,
                    out BakeSources sources,
                    out string errorMessage))
            {
                return (false, null, errorMessage);
            }

            BakeGeometry geometry = await UniTask.Run(
                () => BakeGeometryFromSources(sources, cancellationToken),
                cancellationToken: cancellationToken);

            if (!TryCreateMeshFromGeometry(sources.RootName, geometry, out Mesh combinedMesh, out errorMessage))
            {
                return (false, null, errorMessage);
            }

            return (true, combinedMesh, string.Empty);
        }

        private static bool TryExtractBakeSources(
            GameObject sourceRoot,
            Color defaultColor,
            int subdivisionDepth,
            float maxUvEdgeLength,
            out BakeSources sources,
            out string errorMessage)
        {
            sources = default;
            errorMessage = string.Empty;

            if (sourceRoot == null)
            {
                errorMessage = "変換元ルートがnullです";
                return false;
            }

            Color displayDefault = defaultColor;
            displayDefault.a = 1f;
            int clampedDepth = Mathf.Clamp(subdivisionDepth, 0, 6);
            float clampedUvEdge = Mathf.Max(0.005f, maxUvEdgeLength);
            Matrix4x4 rootWorldToLocal = sourceRoot.transform.worldToLocalMatrix;
            var meshSources = new List<MeshSource>(8);
            var albedoCache = new Dictionary<Texture, AlbedoSampler>(8);
            int sampledTextureCount = 0;

            SkinnedMeshRenderer[] skinnedRenderers = sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[i];
                if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.vertexCount == 0)
                {
                    continue;
                }

                var bakedMesh = new Mesh();
                renderer.BakeMesh(bakedMesh, true);
                Matrix4x4 localToRoot = rootWorldToLocal * renderer.localToWorldMatrix;
                AppendMeshSource(
                    bakedMesh,
                    renderer.sharedMaterials,
                    localToRoot,
                    displayDefault,
                    albedoCache,
                    meshSources,
                    ref sampledTextureCount,
                    destroySourceMesh: true);
            }

            MeshFilter[] meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null
                    || meshFilter.sharedMesh == null
                    || meshFilter.sharedMesh.vertexCount == 0
                    || meshFilter.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    continue;
                }

                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                Material[] materials = meshRenderer != null ? meshRenderer.sharedMaterials : null;
                Matrix4x4 localToRoot = rootWorldToLocal * meshFilter.transform.localToWorldMatrix;
                AppendMeshSource(
                    meshFilter.sharedMesh,
                    materials,
                    localToRoot,
                    displayDefault,
                    albedoCache,
                    meshSources,
                    ref sampledTextureCount,
                    destroySourceMesh: false);
            }

            if (meshSources.Count == 0)
            {
                errorMessage = "変換可能なメッシュが見つかりません";
                return false;
            }

            if (sampledTextureCount == 0)
            {
                Debug.LogWarning(
                    "[ClayFbxTextureToVertexColorBaker] アルベドテクスチャを取得できませんでしたマテリアル設定を確認してください");
            }

            sources = new BakeSources(
                sourceRoot.name,
                displayDefault,
                clampedDepth,
                clampedUvEdge,
                meshSources,
                sampledTextureCount);
            return true;
        }

        private static void AppendMeshSource(
            Mesh sourceMesh,
            Material[] materials,
            Matrix4x4 localToRoot,
            Color displayDefault,
            Dictionary<Texture, AlbedoSampler> albedoCache,
            List<MeshSource> meshSources,
            ref int sampledTextureCount,
            bool destroySourceMesh)
        {
            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            var uvList = new List<Vector2>(sourceVertices.Length);
            sourceMesh.GetUVs(0, uvList);
            if (uvList.Count != sourceVertices.Length)
            {
                uvList.Clear();
                sourceMesh.GetUVs(1, uvList);
            }

            Vector2[] uvs = uvList.Count == sourceVertices.Length ? uvList.ToArray() : Array.Empty<Vector2>();
            bool hasUv = uvs.Length == sourceVertices.Length;
            bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
            Matrix4x4 normalMatrix = localToRoot.inverse.transpose;

            int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = materials != null && subMeshIndex < materials.Length
                    ? materials[subMeshIndex]
                    : null;
                Color tint = ResolveDisplayTint(material);
                AlbedoSampler albedo = default;
                Vector2 textureScale = Vector2.one;
                Vector2 textureOffset = Vector2.zero;
                if (material != null
                    && TryResolveAlbedo(material, out Texture sourceTexture, out string propertyName))
                {
                    albedo = GetOrCreateAlbedoSampler(sourceTexture, albedoCache);
                    textureScale = material.GetTextureScale(propertyName);
                    textureOffset = material.GetTextureOffset(propertyName);
                    if (albedo.HasData)
                    {
                        sampledTextureCount++;
                    }
                }

                int[] indices = sourceMesh.GetTriangles(subMeshIndex);
                if (indices.Length < 3)
                {
                    continue;
                }

                meshSources.Add(new MeshSource(
                    sourceVertices,
                    hasNormals ? sourceNormals : null,
                    uvs,
                    indices,
                    localToRoot,
                    normalMatrix,
                    hasUv,
                    hasNormals,
                    tint,
                    albedo,
                    textureScale,
                    textureOffset,
                    displayDefault));
            }

            if (destroySourceMesh)
            {
                UnityEngine.Object.DestroyImmediate(sourceMesh);
            }
        }

        private static BakeGeometry BakeGeometryFromSources(BakeSources sources, CancellationToken cancellationToken)
        {
            int estimatedTriangles = 0;
            for (int i = 0; i < sources.Meshes.Count; i++)
            {
                estimatedTriangles += sources.Meshes[i].Indices.Length / 3;
            }

            int estimatedLeafCapacity = Mathf.Max(1024, estimatedTriangles * Mathf.Max(1, 1 << sources.SubdivisionDepth));
            var vertices = new List<Vector3>(estimatedLeafCapacity * 3);
            var normals = new List<Vector3>(estimatedLeafCapacity * 3);
            var colors = new List<Color>(estimatedLeafCapacity * 3);
            var triangles = new List<int>(estimatedLeafCapacity * 3);
            int bakedTriangleCount = 0;

            for (int meshIndex = 0; meshIndex < sources.Meshes.Count; meshIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendExplodedMeshSource(
                    sources.Meshes[meshIndex],
                    sources.SubdivisionDepth,
                    sources.MaxUvEdgeLength,
                    vertices,
                    normals,
                    colors,
                    triangles,
                    ref bakedTriangleCount,
                    cancellationToken);
            }

            return new BakeGeometry(vertices, normals, colors, triangles, bakedTriangleCount, sources.SampledTextureCount);
        }

        private static bool TryCreateMeshFromGeometry(
            string rootName,
            BakeGeometry geometry,
            out Mesh combinedMesh,
            out string errorMessage)
        {
            combinedMesh = null;
            errorMessage = string.Empty;
            if (geometry.Vertices.Count == 0 || geometry.Triangles.Count < 3)
            {
                errorMessage = "変換可能なメッシュが見つかりません";
                return false;
            }

            combinedMesh = new Mesh
            {
                name = rootName + "_VertexColor",
                indexFormat = geometry.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            combinedMesh.SetVertices(geometry.Vertices);
            combinedMesh.SetNormals(geometry.Normals);
            combinedMesh.SetColors(geometry.Colors);
            combinedMesh.SetTriangles(geometry.Triangles, 0, false);
            combinedMesh.RecalculateBounds();

            Debug.Log(
                $"[ClayFbxTextureToVertexColorBaker] ベイク完了 triangles={geometry.BakedTriangleCount} vertices={geometry.Vertices.Count} textures={geometry.SampledTextureCount}");
            return true;
        }

        private static void AppendExplodedMeshSource(
            MeshSource source,
            int subdivisionDepth,
            float maxUvEdgeLength,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            ref int bakedTriangleCount,
            CancellationToken cancellationToken)
        {
            int triangleCount = source.Indices.Length / 3;
            if (triangleCount <= 0)
            {
                return;
            }

            if (triangleCount < ParallelTriangleThreshold)
            {
                for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendSourceTriangle(
                        source,
                        triangleIndex,
                        subdivisionDepth,
                        maxUvEdgeLength,
                        vertices,
                        normals,
                        colors,
                        triangles,
                        ref bakedTriangleCount);
                }

                return;
            }

            int chunkCount = (triangleCount + ParallelTriangleChunkSize - 1) / ParallelTriangleChunkSize;
            var chunkGeometries = new BakeGeometry[chunkCount];
            Parallel.For(
                0,
                chunkCount,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Mathf.Max(1, Environment.ProcessorCount),
                    CancellationToken = cancellationToken
                },
                chunkIndex =>
                {
                    int startTriangle = chunkIndex * ParallelTriangleChunkSize;
                    int endTriangle = Mathf.Min(startTriangle + ParallelTriangleChunkSize, triangleCount);
                    int capacity = Mathf.Max(64, (endTriangle - startTriangle) * Mathf.Max(1, 1 << subdivisionDepth));
                    var localVertices = new List<Vector3>(capacity * 3);
                    var localNormals = new List<Vector3>(capacity * 3);
                    var localColors = new List<Color>(capacity * 3);
                    var localTriangles = new List<int>(capacity * 3);
                    int localBaked = 0;

                    for (int triangleIndex = startTriangle; triangleIndex < endTriangle; triangleIndex++)
                    {
                        AppendSourceTriangle(
                            source,
                            triangleIndex,
                            subdivisionDepth,
                            maxUvEdgeLength,
                            localVertices,
                            localNormals,
                            localColors,
                            localTriangles,
                            ref localBaked);
                    }

                    chunkGeometries[chunkIndex] = new BakeGeometry(
                        localVertices,
                        localNormals,
                        localColors,
                        localTriangles,
                        localBaked,
                        0);
                });

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                BakeGeometry chunk = chunkGeometries[chunkIndex];
                int vertexOffset = vertices.Count;
                vertices.AddRange(chunk.Vertices);
                normals.AddRange(chunk.Normals);
                colors.AddRange(chunk.Colors);
                for (int i = 0; i < chunk.Triangles.Count; i++)
                {
                    triangles.Add(chunk.Triangles[i] + vertexOffset);
                }

                bakedTriangleCount += chunk.BakedTriangleCount;
            }
        }

        private static void AppendSourceTriangle(
            MeshSource source,
            int triangleIndex,
            int subdivisionDepth,
            float maxUvEdgeLength,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            ref int bakedTriangleCount)
        {
            int indexOffset = triangleIndex * 3;
            int i0 = source.Indices[indexOffset];
            int i1 = source.Indices[indexOffset + 1];
            int i2 = source.Indices[indexOffset + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0
                || i0 >= source.Vertices.Length
                || i1 >= source.Vertices.Length
                || i2 >= source.Vertices.Length)
            {
                return;
            }

            CornerData c0 = CreateCornerData(i0, source);
            CornerData c1 = CreateCornerData(i1, source);
            CornerData c2 = CreateCornerData(i2, source);
            SubdivideAndAppendTriangle(
                c0,
                c1,
                c2,
                0,
                subdivisionDepth,
                maxUvEdgeLength,
                source.Albedo,
                source.Tint,
                source.TextureScale,
                source.TextureOffset,
                source.DisplayDefault,
                source.HasUv,
                vertices,
                normals,
                colors,
                triangles,
                ref bakedTriangleCount);
        }

        private readonly struct BakeSources
        {
            public readonly string RootName;
            public readonly Color DisplayDefault;
            public readonly int SubdivisionDepth;
            public readonly float MaxUvEdgeLength;
            public readonly List<MeshSource> Meshes;
            public readonly int SampledTextureCount;

            public BakeSources(
                string rootName,
                Color displayDefault,
                int subdivisionDepth,
                float maxUvEdgeLength,
                List<MeshSource> meshes,
                int sampledTextureCount)
            {
                RootName = rootName;
                DisplayDefault = displayDefault;
                SubdivisionDepth = subdivisionDepth;
                MaxUvEdgeLength = maxUvEdgeLength;
                Meshes = meshes;
                SampledTextureCount = sampledTextureCount;
            }
        }

        private readonly struct BakeGeometry
        {
            public readonly List<Vector3> Vertices;
            public readonly List<Vector3> Normals;
            public readonly List<Color> Colors;
            public readonly List<int> Triangles;
            public readonly int BakedTriangleCount;
            public readonly int SampledTextureCount;

            public BakeGeometry(
                List<Vector3> vertices,
                List<Vector3> normals,
                List<Color> colors,
                List<int> triangles,
                int bakedTriangleCount,
                int sampledTextureCount)
            {
                Vertices = vertices;
                Normals = normals;
                Colors = colors;
                Triangles = triangles;
                BakedTriangleCount = bakedTriangleCount;
                SampledTextureCount = sampledTextureCount;
            }
        }

        private readonly struct MeshSource
        {
            public readonly Vector3[] Vertices;
            public readonly Vector3[] Normals;
            public readonly Vector2[] Uvs;
            public readonly int[] Indices;
            public readonly Matrix4x4 LocalToRoot;
            public readonly Matrix4x4 NormalMatrix;
            public readonly bool HasUv;
            public readonly bool HasNormals;
            public readonly Color Tint;
            public readonly AlbedoSampler Albedo;
            public readonly Vector2 TextureScale;
            public readonly Vector2 TextureOffset;
            public readonly Color DisplayDefault;

            public MeshSource(
                Vector3[] vertices,
                Vector3[] normals,
                Vector2[] uvs,
                int[] indices,
                Matrix4x4 localToRoot,
                Matrix4x4 normalMatrix,
                bool hasUv,
                bool hasNormals,
                Color tint,
                AlbedoSampler albedo,
                Vector2 textureScale,
                Vector2 textureOffset,
                Color displayDefault)
            {
                Vertices = vertices;
                Normals = normals;
                Uvs = uvs;
                Indices = indices;
                LocalToRoot = localToRoot;
                NormalMatrix = normalMatrix;
                HasUv = hasUv;
                HasNormals = hasNormals;
                Tint = tint;
                Albedo = albedo;
                TextureScale = textureScale;
                TextureOffset = textureOffset;
                DisplayDefault = displayDefault;
            }
        }

        private readonly struct AlbedoSampler
        {
            public readonly Color[] Pixels;
            public readonly int Width;
            public readonly int Height;

            public AlbedoSampler(Color[] pixels, int width, int height)
            {
                Pixels = pixels;
                Width = width;
                Height = height;
            }

            public bool HasData => Pixels != null && Width > 0 && Height > 0;

            public Color GetPixel(int x, int y)
            {
                return Pixels[y * Width + x];
            }
        }

        private readonly struct CornerData
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector2 Uv;

            public CornerData(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                Uv = uv;
            }
        }

        private static CornerData CreateCornerData(int vertexIndex, MeshSource source)
        {
            Vector3 position = source.LocalToRoot.MultiplyPoint3x4(source.Vertices[vertexIndex]);
            Vector3 normal = source.HasNormals
                ? source.NormalMatrix.MultiplyVector(source.Normals[vertexIndex]).normalized
                : Vector3.up;
            Vector2 uv = source.HasUv ? source.Uvs[vertexIndex] : Vector2.zero;
            return new CornerData(position, normal, uv);
        }

        private static CornerData LerpCorner(CornerData a, CornerData b)
        {
            return new CornerData(
                Vector3.Lerp(a.Position, b.Position, 0.5f),
                Vector3.Normalize(Vector3.Lerp(a.Normal, b.Normal, 0.5f)),
                LerpUvSeamless(a.Uv, b.Uv));
        }

        private static Vector2 LerpUvSeamless(Vector2 a, Vector2 b)
        {
            return LerpUvSeamless(a, b, 0.5f);
        }

        private static float UvEdgeLengthSeamless(Vector2 a, Vector2 b)
        {
            Vector2 delta = UnwrapDelta(a, b);
            return delta.magnitude;
        }

        private static void SubdivideAndAppendTriangle(
            CornerData c0,
            CornerData c1,
            CornerData c2,
            int depth,
            int maxDepth,
            float maxUvEdgeLength,
            AlbedoSampler albedo,
            Color tint,
            Vector2 textureScale,
            Vector2 textureOffset,
            Color displayDefault,
            bool hasUv,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            ref int bakedTriangleCount)
        {
            float maxUvEdge = Mathf.Max(
                UvEdgeLengthSeamless(c0.Uv, c1.Uv),
                Mathf.Max(
                    UvEdgeLengthSeamless(c1.Uv, c2.Uv),
                    UvEdgeLengthSeamless(c2.Uv, c0.Uv)));
            bool shouldSplit = hasUv
                && depth < maxDepth
                && maxUvEdge > maxUvEdgeLength;

            if (!shouldSplit)
            {
                int baseIndex = vertices.Count;
                Vector2 uvCentroid = UvCentroidSeamless(c0.Uv, c1.Uv, c2.Uv);
                Color color0 = SampleCornerColor(
                    c0,
                    uvCentroid,
                    albedo,
                    tint,
                    textureScale,
                    textureOffset,
                    displayDefault,
                    hasUv);
                Color color1 = SampleCornerColor(
                    c1,
                    uvCentroid,
                    albedo,
                    tint,
                    textureScale,
                    textureOffset,
                    displayDefault,
                    hasUv);
                Color color2 = SampleCornerColor(
                    c2,
                    uvCentroid,
                    albedo,
                    tint,
                    textureScale,
                    textureOffset,
                    displayDefault,
                    hasUv);
                ResolveTrianglePaddingColors(ref color0, ref color1, ref color2, displayDefault);

                vertices.Add(c0.Position);
                normals.Add(c0.Normal);
                colors.Add(color0);
                vertices.Add(c1.Position);
                normals.Add(c1.Normal);
                colors.Add(color1);
                vertices.Add(c2.Position);
                normals.Add(c2.Normal);
                colors.Add(color2);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                bakedTriangleCount++;
                return;
            }

            CornerData m01 = LerpCorner(c0, c1);
            CornerData m12 = LerpCorner(c1, c2);
            CornerData m20 = LerpCorner(c2, c0);
            int nextDepth = depth + 1;
            SubdivideAndAppendTriangle(
                c0, m01, m20, nextDepth, maxDepth, maxUvEdgeLength,
                albedo, tint, textureScale, textureOffset, displayDefault, hasUv,
                vertices, normals, colors, triangles, ref bakedTriangleCount);
            SubdivideAndAppendTriangle(
                c1, m12, m01, nextDepth, maxDepth, maxUvEdgeLength,
                albedo, tint, textureScale, textureOffset, displayDefault, hasUv,
                vertices, normals, colors, triangles, ref bakedTriangleCount);
            SubdivideAndAppendTriangle(
                c2, m20, m12, nextDepth, maxDepth, maxUvEdgeLength,
                albedo, tint, textureScale, textureOffset, displayDefault, hasUv,
                vertices, normals, colors, triangles, ref bakedTriangleCount);
            SubdivideAndAppendTriangle(
                m01, m12, m20, nextDepth, maxDepth, maxUvEdgeLength,
                albedo, tint, textureScale, textureOffset, displayDefault, hasUv,
                vertices, normals, colors, triangles, ref bakedTriangleCount);
        }

        private static Color SampleCornerColor(
            CornerData corner,
            Vector2 uvCentroid,
            AlbedoSampler albedo,
            Color tint,
            Vector2 textureScale,
            Vector2 textureOffset,
            Color displayDefault,
            bool hasUv)
        {
            Color sampled = displayDefault;
            if (albedo.HasData && hasUv)
            {
                sampled = SampleAlbedoColor(
                    albedo,
                    corner.Uv,
                    uvCentroid,
                    tint,
                    textureScale,
                    textureOffset,
                    displayDefault);
            }
            else if (!IsApproximatelyWhite(tint))
            {
                sampled = tint;
            }

            sampled.a = 1f;
            return ClampColor01(sampled);
        }

        private static void ResolveTrianglePaddingColors(
            ref Color color0,
            ref Color color1,
            ref Color color2,
            Color displayDefault)
        {
            bool p0 = IsPaddingTexel(color0);
            bool p1 = IsPaddingTexel(color1);
            bool p2 = IsPaddingTexel(color2);
            if (!p0 && !p1 && !p2)
            {
                return;
            }

            if (p0)
            {
                color0 = PickNonPaddingColor(color1, color2, p1, p2, displayDefault);
            }

            if (p1)
            {
                color1 = PickNonPaddingColor(color0, color2, IsPaddingTexel(color0), p2, displayDefault);
            }

            if (p2)
            {
                color2 = PickNonPaddingColor(color0, color1, IsPaddingTexel(color0), IsPaddingTexel(color1), displayDefault);
            }
        }

        private static Color PickNonPaddingColor(
            Color a,
            Color b,
            bool aIsPadding,
            bool bIsPadding,
            Color displayDefault)
        {
            if (!aIsPadding && !bIsPadding)
            {
                return ClampColor01(new Color(
                    (a.r + b.r) * 0.5f,
                    (a.g + b.g) * 0.5f,
                    (a.b + b.b) * 0.5f,
                    1f));
            }

            if (!aIsPadding)
            {
                return a;
            }

            if (!bIsPadding)
            {
                return b;
            }

            return ClampColor01(new Color(
                displayDefault.r * 0.85f,
                displayDefault.g * 0.85f,
                displayDefault.b * 0.85f,
                1f));
        }

        private static Color SampleAlbedoColor(
            AlbedoSampler albedo,
            Vector2 cornerUv,
            Vector2 uvCentroid,
            Color tint,
            Vector2 textureScale,
            Vector2 textureOffset,
            Color displayDefault)
        {
            Vector2 inwardUv = LerpUvSeamless(cornerUv, uvCentroid, 0.55f);
            Color texel = SampleTexel(albedo, inwardUv, textureScale, textureOffset);
            Color centroidTexel = SampleTexel(albedo, uvCentroid, textureScale, textureOffset);

            if (IsPaddingTexel(texel))
            {
                if (!IsPaddingTexel(centroidTexel))
                {
                    texel = centroidTexel;
                }
                else if (!TryRecoverNonWhiteTexel(
                             albedo,
                             cornerUv,
                             uvCentroid,
                             textureScale,
                             textureOffset,
                             out Color recovered))
                {
                    return ResolveFallbackWithoutWhite(tint, displayDefault);
                }
                else
                {
                    texel = recovered;
                }
            }
            else if (IsBrighterPaddingOutlier(texel, centroidTexel))
            {
                texel = centroidTexel;
            }

            if (IsPaddingTexel(texel))
            {
                return ResolveFallbackWithoutWhite(tint, displayDefault);
            }

            if (IsApproximatelyWhite(tint))
            {
                return texel;
            }

            return new Color(
                texel.r * tint.r,
                texel.g * tint.g,
                texel.b * tint.b,
                1f);
        }

        private static bool TryRecoverNonWhiteTexel(
            AlbedoSampler albedo,
            Vector2 cornerUv,
            Vector2 uvCentroid,
            Vector2 textureScale,
            Vector2 textureOffset,
            out Color recovered)
        {
            recovered = default;
            float[] pullAmounts = { 0.35f, 0.55f, 0.75f, 0.9f, 1f };
            for (int i = 0; i < pullAmounts.Length; i++)
            {
                Vector2 pulledUv = LerpUvSeamless(cornerUv, uvCentroid, pullAmounts[i]);
                Color pulled = SampleTexel(albedo, pulledUv, textureScale, textureOffset);
                if (!IsPaddingTexel(pulled))
                {
                    recovered = pulled;
                    return true;
                }
            }

            float offset = 0.015f;
            Vector2[] neighborOffsets =
            {
                new Vector2(offset, 0f),
                new Vector2(-offset, 0f),
                new Vector2(0f, offset),
                new Vector2(0f, -offset),
                new Vector2(offset, offset),
                new Vector2(-offset, -offset),
                new Vector2(offset, -offset),
                new Vector2(-offset, offset)
            };
            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector2 probeUv = uvCentroid + neighborOffsets[i];
                Color probed = SampleTexel(albedo, probeUv, textureScale, textureOffset);
                if (!IsPaddingTexel(probed))
                {
                    recovered = probed;
                    return true;
                }
            }

            return false;
        }

        private static Color ResolveFallbackWithoutWhite(Color tint, Color displayDefault)
        {
            if (!IsApproximatelyWhite(tint))
            {
                return tint;
            }

            return displayDefault;
        }

        private static Color SampleTexel(
            AlbedoSampler albedo,
            Vector2 sourceUv,
            Vector2 textureScale,
            Vector2 textureOffset)
        {
            float u = sourceUv.x * textureScale.x + textureOffset.x;
            float v = sourceUv.y * textureScale.y + textureOffset.y;
            u -= Mathf.Floor(u);
            v -= Mathf.Floor(v);
            int width = Mathf.Max(1, albedo.Width);
            int height = Mathf.Max(1, albedo.Height);
            int x = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * height), 0, height - 1);
            return albedo.GetPixel(x, y);
        }

        private static Vector2 UvCentroidSeamless(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ub = UnwrapUvRelative(a, b);
            Vector2 uc = UnwrapUvRelative(a, c);
            Vector2 average = (a + ub + uc) / 3f;
            average.x -= Mathf.Floor(average.x);
            average.y -= Mathf.Floor(average.y);
            return average;
        }

        private static Vector2 UnwrapUvRelative(Vector2 origin, Vector2 target)
        {
            return origin + UnwrapDelta(origin, target);
        }

        private static Vector2 UnwrapDelta(Vector2 origin, Vector2 target)
        {
            Vector2 delta = target - origin;
            if (delta.x > 0.5f)
            {
                delta.x -= 1f;
            }
            else if (delta.x < -0.5f)
            {
                delta.x += 1f;
            }

            if (delta.y > 0.5f)
            {
                delta.y -= 1f;
            }
            else if (delta.y < -0.5f)
            {
                delta.y += 1f;
            }

            return delta;
        }

        private static Vector2 LerpUvSeamless(Vector2 a, Vector2 b, float t)
        {
            Vector2 unwrapped = UnwrapUvRelative(a, b);
            Vector2 lerped = Vector2.Lerp(a, unwrapped, t);
            lerped.x -= Mathf.Floor(lerped.x);
            lerped.y -= Mathf.Floor(lerped.y);
            return lerped;
        }

        private static bool IsApproximatelyWhite(Color color)
        {
            return color.r > 0.97f && color.g > 0.97f && color.b > 0.97f;
        }

        private static bool IsPaddingTexel(Color color)
        {
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            float chroma = max - min;
            float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
            return luminance > 0.72f && chroma < 0.08f;
        }

        private static bool IsBrighterPaddingOutlier(Color candidate, Color reference)
        {
            float candidateLum = 0.2126f * candidate.r + 0.7152f * candidate.g + 0.0722f * candidate.b;
            float referenceLum = 0.2126f * reference.r + 0.7152f * reference.g + 0.0722f * reference.b;
            float candidateChroma = Mathf.Max(candidate.r, Mathf.Max(candidate.g, candidate.b))
                - Mathf.Min(candidate.r, Mathf.Min(candidate.g, candidate.b));
            float referenceChroma = Mathf.Max(reference.r, Mathf.Max(reference.g, reference.b))
                - Mathf.Min(reference.r, Mathf.Min(reference.g, reference.b));
            return candidateLum > referenceLum + 0.12f
                && candidateChroma < 0.1f
                && candidateChroma <= referenceChroma + 0.02f;
        }

        private static Color ClampColor01(Color color)
        {
            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = 1f;
            return color;
        }

        private static Color ResolveDisplayTint(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            for (int i = 0; i < ColorPropertyNames.Length; i++)
            {
                if (!material.HasProperty(ColorPropertyNames[i]))
                {
                    continue;
                }

                Color color = material.GetColor(ColorPropertyNames[i]);
                color.a = 1f;
                return color;
            }

            return Color.white;
        }

        private static bool TryResolveAlbedo(
            Material material,
            out Texture texture,
            out string propertyName)
        {
            texture = null;
            propertyName = string.Empty;

            for (int i = 0; i < AlbedoPropertyNames.Length; i++)
            {
                string candidate = AlbedoPropertyNames[i];
                if (!material.HasProperty(candidate))
                {
                    continue;
                }

                Texture candidateTexture = material.GetTexture(candidate);
                if (!IsUsableAlbedoTexture(candidateTexture))
                {
                    continue;
                }

                texture = candidateTexture;
                propertyName = candidate;
                return true;
            }

            string[] texturePropertyNames = material.GetTexturePropertyNames();
            for (int i = 0; i < texturePropertyNames.Length; i++)
            {
                string candidate = texturePropertyNames[i];
                if (IsNonAlbedoTextureProperty(candidate))
                {
                    continue;
                }

                Texture candidateTexture = material.GetTexture(candidate);
                if (!IsUsableAlbedoTexture(candidateTexture))
                {
                    continue;
                }

                texture = candidateTexture;
                propertyName = candidate;
                return true;
            }

            Texture mainTexture = material.mainTexture;
            if (IsUsableAlbedoTexture(mainTexture))
            {
                texture = mainTexture;
                propertyName = FindMainTexturePropertyName(material);
                return true;
            }

            return false;
        }

        private static bool IsUsableAlbedoTexture(Texture texture)
        {
            if (texture == null)
            {
                return false;
            }

            if (texture == Texture2D.whiteTexture
                || texture == Texture2D.blackTexture
                || texture == Texture2D.grayTexture
                || texture == Texture2D.normalTexture
                || texture == Texture2D.redTexture
                || texture == Texture2D.linearGrayTexture)
            {
                return false;
            }

            string textureName = texture.name ?? string.Empty;
            if (textureName.IndexOf("Default-Particle", StringComparison.OrdinalIgnoreCase) >= 0
                || textureName.IndexOf("Default-White", StringComparison.OrdinalIgnoreCase) >= 0
                || textureName.Equals("White", StringComparison.OrdinalIgnoreCase)
                || textureName.Equals("UnityWhite", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (texture.width <= 1 && texture.height <= 1 && texture is Texture2D tiny)
            {
                try
                {
                    if (tiny.isReadable)
                    {
                        Color pixel = tiny.GetPixel(0, 0);
                        if (IsApproximatelyWhite(pixel))
                        {
                            return false;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return true;
        }

        private static string FindMainTexturePropertyName(Material material)
        {
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == material.mainTexture)
            {
                return "_BaseMap";
            }

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == material.mainTexture)
            {
                return "_MainTex";
            }

            string[] names = material.GetTexturePropertyNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (material.GetTexture(names[i]) == material.mainTexture)
                {
                    return names[i];
                }
            }

            return "_MainTex";
        }

        private static bool IsNonAlbedoTextureProperty(string propertyName)
        {
            return propertyName.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Occlusion", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Bump", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Height", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Spec", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AlbedoSampler GetOrCreateAlbedoSampler(
            Texture sourceTexture,
            Dictionary<Texture, AlbedoSampler> cache)
        {
            if (cache.TryGetValue(sourceTexture, out AlbedoSampler cached) && cached.HasData)
            {
                return cached;
            }

            int sourceWidth = Mathf.Max(1, sourceTexture.width);
            int sourceHeight = Mathf.Max(1, sourceTexture.height);
            float scale = 1f;
            int maxSide = Mathf.Max(sourceWidth, sourceHeight);
            if (maxSide > MaxBakeTextureSize)
            {
                scale = MaxBakeTextureSize / (float)maxSide;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));

            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(sourceTexture, temporary);
            RenderTexture.active = temporary;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            readable.wrapMode = TextureWrapMode.Clamp;
            readable.filterMode = FilterMode.Point;
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply(false, false);
            Color[] pixels = readable.GetPixels();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            UnityEngine.Object.DestroyImmediate(readable);

            var sampler = new AlbedoSampler(pixels, width, height);
            cache[sourceTexture] = sampler;
            return sampler;
        }
    }
}
#endif
