using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClayEditor
{
    /// <summary>
    /// GLBインポート結果からボクセル化用メッシュを用意する
    /// </summary>
    internal static class ClayVoxelMeshImportSource
    {
        /// <summary>
        /// ボクセル化用メッシュのライフタイム管理
        /// </summary>
        internal sealed class Handle : System.IDisposable
        {
            /// <summary>
            /// サンプリング対象メッシュ
            /// </summary>
            public Mesh Mesh { get; }

            /// <summary>
            /// メッシュのTransform
            /// </summary>
            public Transform MeshTransform { get; }

            /// <summary>
            /// 頂点カラー頂点数不一致や未取得時はnull
            /// </summary>
            public Color[] VertexColors { get; }

            private readonly bool ownsMesh;

            private Handle(Mesh mesh, Transform meshTransform, Color[] vertexColors, bool ownsMesh)
            {
                Mesh = mesh;
                MeshTransform = meshTransform;
                VertexColors = vertexColors;
                this.ownsMesh = ownsMesh;
            }

            /// <summary>
            /// インポート結果からボクセル化用メッシュを生成する
            /// SkinnedMeshは表示姿勢のBakeMeshを使いサイズと色をGLB見た目に合わせる
            /// </summary>
            /// <param name="importedRoot">GLBインポート結果のルート</param>
            /// <param name="handle">生成結果</param>
            /// <param name="errorMessage">失敗理由</param>
            /// <returns>成功した場合true</returns>
            internal static bool TryCreate(GameObject importedRoot, out Handle handle, out string errorMessage)
            {
                handle = null;
                errorMessage = string.Empty;

                if (importedRoot == null)
                {
                    errorMessage = "インポート結果がnullです";
                    return false;
                }

                SkinnedMeshRenderer skinnedRenderer = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
                {
                    Mesh sourceMesh = skinnedRenderer.sharedMesh;
                    var bakedMesh = new Mesh
                    {
                        name = sourceMesh.name + "_RemakeBake",
                        indexFormat = sourceMesh.indexFormat
                    };
                    skinnedRenderer.BakeMesh(bakedMesh, true);
                    if (bakedMesh.vertexCount == 0)
                    {
                        Object.Destroy(bakedMesh);
                        errorMessage = "BakeMeshの頂点がありません";
                        return false;
                    }

                    Color[] vertexColors = TryReadVertexColors(sourceMesh, bakedMesh.vertexCount);
                    if (vertexColors == null)
                    {
                        vertexColors = TryReadVertexColors(bakedMesh, bakedMesh.vertexCount);
                    }

                    handle = new Handle(bakedMesh, skinnedRenderer.transform, vertexColors, ownsMesh: true);
                    return true;
                }

                MeshFilter meshFilter = importedRoot.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer meshRenderer = meshFilter != null
                    ? meshFilter.GetComponent<MeshRenderer>()
                    : importedRoot.GetComponentInChildren<MeshRenderer>(true);
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    errorMessage = "インポート結果に有効なメッシュがありません";
                    return false;
                }

                Transform meshTransform = meshRenderer != null ? meshRenderer.transform : meshFilter.transform;
                Mesh sharedMesh = meshFilter.sharedMesh;
                Color[] colors = TryReadVertexColors(sharedMesh, sharedMesh.vertexCount);
                handle = new Handle(sharedMesh, meshTransform, colors, ownsMesh: false);
                return true;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                if (ownsMesh && Mesh != null)
                {
                    Object.Destroy(Mesh);
                }
            }

            /// <summary>
            /// メッシュから頂点カラーを可能な手段で読み取る
            /// glTFastのストリーム不整合でもGetColorsやMeshDataを試す
            /// </summary>
            /// <param name="mesh">対象メッシュ</param>
            /// <param name="expectedVertexCount">必要な頂点数</param>
            /// <returns>色配列なければnull</returns>
            private static Color[] TryReadVertexColors(Mesh mesh, int expectedVertexCount)
            {
                if (mesh == null || expectedVertexCount <= 0)
                {
                    return null;
                }

                if (TryReadColorsViaList(mesh, expectedVertexCount, out Color[] fromList))
                {
                    return fromList;
                }

                if (TryReadColorsViaMeshData(mesh, expectedVertexCount, out Color[] fromMeshData))
                {
                    return fromMeshData;
                }

                Color[] colors = mesh.colors;
                if (colors != null && colors.Length == expectedVertexCount && HasColorVariance(colors))
                {
                    return colors;
                }

                Color32[] colors32 = mesh.colors32;
                if (colors32 != null && colors32.Length == expectedVertexCount)
                {
                    var converted = new Color[colors32.Length];
                    for (int i = 0; i < colors32.Length; i++)
                    {
                        converted[i] = colors32[i];
                    }

                    if (HasColorVariance(converted))
                    {
                        return converted;
                    }
                }

                return null;
            }

            private static bool TryReadColorsViaList(Mesh mesh, int expectedVertexCount, out Color[] colors)
            {
                colors = null;
                var list = new List<Color>(expectedVertexCount);
                mesh.GetColors(list);
                if (list.Count != expectedVertexCount)
                {
                    return false;
                }

                colors = list.ToArray();
                return HasColorVariance(colors);
            }

            private static bool TryReadColorsViaMeshData(Mesh mesh, int expectedVertexCount, out Color[] colors)
            {
                colors = null;
                if (!mesh.HasVertexAttribute(VertexAttribute.Color))
                {
                    return false;
                }

                Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(mesh);
                try
                {
                    Mesh.MeshData data = dataArray[0];
                    if (data.vertexCount != expectedVertexCount
                        || !data.HasVertexAttribute(VertexAttribute.Color))
                    {
                        return false;
                    }

                    var nativeColors = new NativeArray<Color32>(data.vertexCount, Allocator.Temp);
                    try
                    {
                        data.GetColors(nativeColors);
                        colors = new Color[nativeColors.Length];
                        for (int i = 0; i < nativeColors.Length; i++)
                        {
                            colors[i] = nativeColors[i];
                        }

                        return HasColorVariance(colors);
                    }
                    finally
                    {
                        nativeColors.Dispose();
                    }
                }
                finally
                {
                    dataArray.Dispose();
                }
            }

            private static bool HasColorVariance(Color[] colors)
            {
                if (colors == null || colors.Length == 0)
                {
                    return false;
                }

                Color first = colors[0];
                for (int i = 1; i < colors.Length; i++)
                {
                    if (Mathf.Abs(colors[i].r - first.r) > 0.002f
                        || Mathf.Abs(colors[i].g - first.g) > 0.002f
                        || Mathf.Abs(colors[i].b - first.b) > 0.002f)
                    {
                        return true;
                    }
                }

                // 単色でも有効な頂点カラーとして扱う(全白や単色塗装)
                return colors.Length > 0;
            }
        }
    }
}
