using UnityEngine;

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

            private Handle(Mesh mesh, Transform meshTransform)
            {
                Mesh = mesh;
                MeshTransform = meshTransform;
            }

            /// <summary>
            /// インポート結果からボクセル化用メッシュを生成する
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
                    return TryCreateFromMesh(skinnedRenderer.sharedMesh, skinnedRenderer.transform, out handle, out errorMessage);
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
                return TryCreateFromMesh(meshFilter.sharedMesh, meshTransform, out handle, out errorMessage);
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            private static bool TryCreateFromMesh(
                Mesh sharedMesh,
                Transform meshTransform,
                out Handle handle,
                out string errorMessage)
            {
                handle = null;
                errorMessage = string.Empty;

                if (sharedMesh.vertexCount == 0)
                {
                    errorMessage = "メッシュの頂点がありません";
                    return false;
                }

                handle = new Handle(sharedMesh, meshTransform);
                return true;
            }
        }
    }
}
