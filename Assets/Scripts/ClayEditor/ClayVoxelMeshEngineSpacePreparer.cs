using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// インポートメッシュを造形グリッド座標へ変換してフィットさせる
    /// </summary>
    internal static class ClayVoxelMeshEngineSpacePreparer
    {
        /// <summary>
        /// 造形グリッド上で内外判定に使うメッシュ
        /// </summary>
        internal readonly struct PreparedMesh
        {
            public readonly Vector3[] FittedEngineVertices;
            public readonly int[] Triangles;

            public PreparedMesh(Vector3[] fittedEngineVertices, int[] triangles)
            {
                FittedEngineVertices = fittedEngineVertices;
                Triangles = triangles;
            }
        }

        /// <summary>
        /// メッシュローカル頂点を造形グリッド中央へフィットした座標へ変換する
        /// </summary>
        /// <param name="meshVertices">メッシュローカル頂点</param>
        /// <param name="triangles">三角形インデックス</param>
        /// <param name="meshLocalToEngine">メッシュローカルから造形ローカルへの変換</param>
        /// <param name="boundsSize">造形グリッドのワールドサイズ</param>
        /// <param name="applyAutoOrientation">trueのときY-up自動補正を行う</param>
        /// <returns>フィット済み造形ローカルメッシュ</returns>
        internal static PreparedMesh Prepare(
            Vector3[] meshVertices,
            int[] triangles,
            Matrix4x4 meshLocalToEngine,
            float boundsSize,
            bool applyAutoOrientation = true)
        {
            var engineVertices = new Vector3[meshVertices.Length];
            for (int i = 0; i < meshVertices.Length; i++)
            {
                engineVertices[i] = meshLocalToEngine.MultiplyPoint3x4(meshVertices[i]);
            }

            if (applyAutoOrientation)
            {
                ClayVoxelMeshImportOrientation.AlignToClayEditConvention(engineVertices);
            }

            ClayVoxelMeshImportFitter.FitParams fitParams = ClayVoxelMeshImportFitter.ComputeFromVertices(
                engineVertices,
                boundsSize);

            float scale = Mathf.Max(fitParams.UniformScale, 1e-5f);
            var fittedVertices = new Vector3[engineVertices.Length];
            for (int i = 0; i < engineVertices.Length; i++)
            {
                fittedVertices[i] = (engineVertices[i] - fitParams.MeshCenterEngine) * scale;
            }

            return new PreparedMesh(fittedVertices, triangles);
        }
    }
}
