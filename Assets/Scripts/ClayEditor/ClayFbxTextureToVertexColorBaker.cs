#if UNITY_EDITOR
using System.Collections.Generic;
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
            combinedMesh = null;
            errorMessage = string.Empty;

            if (sourceRoot == null)
            {
                errorMessage = "変換元ルートがnullです";
                return false;
            }

            var vertices = new List<Vector3>(8192);
            var normals = new List<Vector3>(8192);
            var colors = new List<Color>(8192);
            var triangles = new List<int>(8192);
            var readableTextures = new Dictionary<Texture, Texture2D>(8);
            var createdReadableTextures = new List<Texture2D>(8);
            Color displayDefault = defaultColor;
            displayDefault.a = 1f;
            int sampledTextureCount = 0;
            int bakedTriangleCount = 0;
            int clampedDepth = Mathf.Clamp(subdivisionDepth, 0, 6);
            float clampedUvEdge = Mathf.Max(0.005f, maxUvEdgeLength);

            try
            {
                Matrix4x4 rootWorldToLocal = sourceRoot.transform.worldToLocalMatrix;

                SkinnedMeshRenderer[] skinnedRenderers = sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skinnedRenderers.Length; i++)
                {
                    AppendSkinnedMesh(
                        skinnedRenderers[i],
                        rootWorldToLocal,
                        displayDefault,
                        clampedDepth,
                        clampedUvEdge,
                        readableTextures,
                        createdReadableTextures,
                        vertices,
                        normals,
                        colors,
                        triangles,
                        ref sampledTextureCount,
                        ref bakedTriangleCount);
                }

                MeshFilter[] meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    MeshFilter meshFilter = meshFilters[i];
                    if (meshFilter.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        continue;
                    }

                    AppendMeshFilter(
                        meshFilter,
                        rootWorldToLocal,
                        displayDefault,
                        clampedDepth,
                        clampedUvEdge,
                        readableTextures,
                        createdReadableTextures,
                        vertices,
                        normals,
                        colors,
                        triangles,
                        ref sampledTextureCount,
                        ref bakedTriangleCount);
                }

                if (vertices.Count == 0 || triangles.Count < 3)
                {
                    errorMessage = "変換可能なメッシュが見つかりません";
                    return false;
                }

                combinedMesh = new Mesh
                {
                    name = sourceRoot.name + "_VertexColor",
                    indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                combinedMesh.SetVertices(vertices);
                combinedMesh.SetNormals(normals);
                combinedMesh.SetColors(colors);
                combinedMesh.SetTriangles(triangles, 0, false);
                combinedMesh.RecalculateBounds();

                if (sampledTextureCount == 0)
                {
                    Debug.LogWarning(
                        "[ClayFbxTextureToVertexColorBaker] アルベドテクスチャを取得できませんでしたマテリアル設定を確認してください");
                }

                int nearWhiteCount = 0;
                int paddingLikeCount = 0;
                float maxLuminance = 0f;
                for (int i = 0; i < colors.Count; i++)
                {
                    Color c = colors[i];
                    float luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    if (luminance > maxLuminance)
                    {
                        maxLuminance = luminance;
                    }

                    if (IsNearWhite(c))
                    {
                        nearWhiteCount++;
                    }

                    if (IsPaddingTexel(c))
                    {
                        paddingLikeCount++;
                    }
                }

                Debug.Log(
                    $"[ClayFbxTextureToVertexColorBaker] ベイク完了 triangles={bakedTriangleCount} vertices={vertices.Count} textures={sampledTextureCount} depth={clampedDepth} nearWhite={nearWhiteCount} paddingLike={paddingLikeCount} maxLum={maxLuminance:F3}");
                return true;
            }
            finally
            {
                for (int i = 0; i < createdReadableTextures.Count; i++)
                {
                    if (createdReadableTextures[i] != null)
                    {
                        Object.DestroyImmediate(createdReadableTextures[i]);
                    }
                }
            }
        }

        private static void AppendSkinnedMesh(
            SkinnedMeshRenderer renderer,
            Matrix4x4 rootWorldToLocal,
            Color displayDefault,
            int subdivisionDepth,
            float maxUvEdgeLength,
            Dictionary<Texture, Texture2D> readableTextures,
            List<Texture2D> createdReadableTextures,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            ref int sampledTextureCount,
            ref int bakedTriangleCount)
        {
            if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.vertexCount == 0)
            {
                return;
            }

            var bakedMesh = new Mesh();
            renderer.BakeMesh(bakedMesh, true);
            Matrix4x4 localToRoot = rootWorldToLocal * renderer.localToWorldMatrix;
            AppendExplodedMesh(
                bakedMesh,
                renderer.sharedMaterials,
                localToRoot,
                displayDefault,
                subdivisionDepth,
                maxUvEdgeLength,
                readableTextures,
                createdReadableTextures,
                vertices,
                normals,
                colors,
                triangles,
                ref sampledTextureCount,
                ref bakedTriangleCount);
            Object.DestroyImmediate(bakedMesh);
        }

        private static void AppendMeshFilter(
            MeshFilter meshFilter,
            Matrix4x4 rootWorldToLocal,
            Color displayDefault,
            int subdivisionDepth,
            float maxUvEdgeLength,
            Dictionary<Texture, Texture2D> readableTextures,
            List<Texture2D> createdReadableTextures,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            ref int sampledTextureCount,
            ref int bakedTriangleCount)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0)
            {
                return;
            }

            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            Material[] materials = meshRenderer != null ? meshRenderer.sharedMaterials : null;
            Matrix4x4 localToRoot = rootWorldToLocal * meshFilter.transform.localToWorldMatrix;
            AppendExplodedMesh(
                meshFilter.sharedMesh,
                materials,
                localToRoot,
                displayDefault,
                subdivisionDepth,
                maxUvEdgeLength,
                readableTextures,
                createdReadableTextures,
                vertices,
                normals,
                colors,
                triangles,
                ref sampledTextureCount,
                ref bakedTriangleCount);
        }

        private static void AppendExplodedMesh(
            Mesh sourceMesh,
            Material[] materials,
            Matrix4x4 localToRoot,
            Color displayDefault,
            int subdivisionDepth,
            float maxUvEdgeLength,
            Dictionary<Texture, Texture2D> readableTextures,
            List<Texture2D> createdReadableTextures,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            ref int sampledTextureCount,
            ref int bakedTriangleCount)
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

            bool hasUv = uvList.Count == sourceVertices.Length;
            bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
            Matrix4x4 normalMatrix = localToRoot.inverse.transpose;

            int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = materials != null && subMeshIndex < materials.Length
                    ? materials[subMeshIndex]
                    : null;
                Color tint = ResolveDisplayTint(material);
                Texture2D albedo = null;
                Vector2 textureScale = Vector2.one;
                Vector2 textureOffset = Vector2.zero;
                if (material != null
                    && TryResolveAlbedo(material, out Texture sourceTexture, out string propertyName))
                {
                    albedo = GetOrCreateReadableTexture(
                        sourceTexture,
                        readableTextures,
                        createdReadableTextures);
                    textureScale = material.GetTextureScale(propertyName);
                    textureOffset = material.GetTextureOffset(propertyName);
                    sampledTextureCount++;
                }

                int[] indices = sourceMesh.GetTriangles(subMeshIndex);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int i0 = indices[i];
                    int i1 = indices[i + 1];
                    int i2 = indices[i + 2];
                    if (i0 < 0 || i1 < 0 || i2 < 0
                        || i0 >= sourceVertices.Length
                        || i1 >= sourceVertices.Length
                        || i2 >= sourceVertices.Length)
                    {
                        continue;
                    }

                    CornerData c0 = CreateCornerData(
                        i0,
                        sourceVertices,
                        sourceNormals,
                        uvList,
                        hasUv,
                        hasNormals,
                        localToRoot,
                        normalMatrix);
                    CornerData c1 = CreateCornerData(
                        i1,
                        sourceVertices,
                        sourceNormals,
                        uvList,
                        hasUv,
                        hasNormals,
                        localToRoot,
                        normalMatrix);
                    CornerData c2 = CreateCornerData(
                        i2,
                        sourceVertices,
                        sourceNormals,
                        uvList,
                        hasUv,
                        hasNormals,
                        localToRoot,
                        normalMatrix);

                    SubdivideAndAppendTriangle(
                        c0,
                        c1,
                        c2,
                        0,
                        subdivisionDepth,
                        maxUvEdgeLength,
                        albedo,
                        tint,
                        textureScale,
                        textureOffset,
                        displayDefault,
                        hasUv,
                        vertices,
                        normals,
                        colors,
                        triangles,
                        ref bakedTriangleCount);
                }
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

        private static CornerData CreateCornerData(
            int vertexIndex,
            Vector3[] sourceVertices,
            Vector3[] sourceNormals,
            List<Vector2> uvs,
            bool hasUv,
            bool hasNormals,
            Matrix4x4 localToRoot,
            Matrix4x4 normalMatrix)
        {
            Vector3 position = localToRoot.MultiplyPoint3x4(sourceVertices[vertexIndex]);
            Vector3 normal = hasNormals
                ? normalMatrix.MultiplyVector(sourceNormals[vertexIndex]).normalized
                : Vector3.up;
            Vector2 uv = hasUv ? uvs[vertexIndex] : Vector2.zero;
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
            Vector2 delta = b - a;
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

            Vector2 mid = a + delta * 0.5f;
            mid.x -= Mathf.Floor(mid.x);
            mid.y -= Mathf.Floor(mid.y);
            return mid;
        }

        private static float UvEdgeLengthSeamless(Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
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

            return delta.magnitude;
        }

        private static void SubdivideAndAppendTriangle(
            CornerData c0,
            CornerData c1,
            CornerData c2,
            int depth,
            int maxDepth,
            float maxUvEdgeLength,
            Texture2D albedo,
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
            Texture2D albedo,
            Color tint,
            Vector2 textureScale,
            Vector2 textureOffset,
            Color displayDefault,
            bool hasUv)
        {
            Color sampled = displayDefault;
            if (albedo != null && hasUv)
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

            // 余白っぽいコーナーは同じ三角形の非余白色で置換する
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

            // 三角形全体が余白なら既定色より彩度のあるTintが無いので少し落とす
            return ClampColor01(new Color(
                displayDefault.r * 0.85f,
                displayDefault.g * 0.85f,
                displayDefault.b * 0.85f,
                1f));
        }

        private static Color SampleAlbedoColor(
            Texture2D albedo,
            Vector2 cornerUv,
            Vector2 uvCentroid,
            Color tint,
            Vector2 textureScale,
            Vector2 textureOffset,
            Color displayDefault)
        {
            // 島端の余白を避けるため最初から中央側UVで取る
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
                // コーナーだけ余白寄りの明るい無彩色なら中央を使う
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
            Texture2D albedo,
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
            Texture2D albedo,
            Vector2 sourceUv,
            Vector2 textureScale,
            Vector2 textureOffset)
        {
            float u = sourceUv.x * textureScale.x + textureOffset.x;
            float v = sourceUv.y * textureScale.y + textureOffset.y;
            u -= Mathf.Floor(u);
            v -= Mathf.Floor(v);
            int width = Mathf.Max(1, albedo.width);
            int height = Mathf.Max(1, albedo.height);
            // バイリニアは余白と混ざるためポイントサンプルする
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

            return origin + delta;
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

        private static bool IsNearWhite(Color color)
        {
            return IsPaddingTexel(color);
        }

        private static bool IsPaddingTexel(Color color)
        {
            // 純白だけでなく薄いグレー余白も検出する
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

            // mainTextureは未割当時でも白テクスチャを返すことがあるため最後に回す
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

            // Unity組み込みの白/黒/灰は未設定マテリアルのダミーなので除外する
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
            if (textureName.IndexOf("Default-Particle", System.StringComparison.OrdinalIgnoreCase) >= 0
                || textureName.IndexOf("Default-White", System.StringComparison.OrdinalIgnoreCase) >= 0
                || textureName.Equals("White", System.StringComparison.OrdinalIgnoreCase)
                || textureName.Equals("UnityWhite", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 1x1の純白テクスチャもダミー扱い
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
                catch (System.Exception)
                {
                    // 読み取り不可なら名前判定のみで継続する
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
            return propertyName.IndexOf("Normal", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Mask", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Metallic", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Occlusion", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Emission", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Bump", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Height", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Spec", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture2D GetOrCreateReadableTexture(
            Texture sourceTexture,
            Dictionary<Texture, Texture2D> cache,
            List<Texture2D> createdReadableTextures)
        {
            if (cache.TryGetValue(sourceTexture, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            int width = Mathf.Max(1, sourceTexture.width);
            int height = Mathf.Max(1, sourceTexture.height);

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

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            cache[sourceTexture] = readable;
            createdReadableTextures.Add(readable);
            return readable;
        }
    }
}
#endif
