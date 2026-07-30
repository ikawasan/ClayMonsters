using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// 作り直し用メッシュを造形グリッド中央へフィットさせる
    /// </summary>
    internal static class ClayVoxelMeshImportFitter
    {
        private const float FitMarginRatio = 0.85f;

        /// <summary>
        /// グリッドへのフィットパラメータ
        /// </summary>
        internal readonly struct FitParams
        {
            public readonly Vector3 MeshCenterEngine;
            public readonly float UniformScale;

            public FitParams(Vector3 meshCenterEngine, float uniformScale)
            {
                MeshCenterEngine = meshCenterEngine;
                UniformScale = uniformScale;
            }
        }

        /// <summary>
        /// 頂点群境界を造形グリッドへ収まるようスケール算出する
        /// </summary>
        /// <param name="engineVertices">造形ローカル頂点群</param>
        /// <param name="boundsSize">造形グリッドのワールドサイズ</param>
        /// <param name="fitToGrid">trueならグリッドへ拡大縮小してフィットするfalseなら元サイズを保ちはみ出すときだけ縮小する</param>
        /// <returns>フィットパラメータ</returns>
        internal static FitParams ComputeFromVertices(
            Vector3[] engineVertices,
            float boundsSize,
            bool fitToGrid = true)
        {
            if (!TryGetVertexBounds(engineVertices, out Vector3 center, out Vector3 extents))
            {
                return new FitParams(Vector3.zero, 1f);
            }

            float maxExtent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            float targetHalf = boundsSize * FitMarginRatio * 0.5f;
            if (maxExtent <= 1e-5f)
            {
                return new FitParams(center, 1f);
            }

            if (!fitToGrid)
            {
                // 元サイズを保ちグリッドをはみ出すときだけ縮小する
                float shrinkOnlyScale = maxExtent > targetHalf ? targetHalf / maxExtent : 1f;
                return new FitParams(center, shrinkOnlyScale);
            }

            float uniformScale = targetHalf / maxExtent;
            return new FitParams(center, uniformScale);
        }

        /// <summary>
        /// メッシュ境界を造形グリッドへ収まるようスケール算出する
        /// </summary>
        /// <param name="meshBounds">メッシュローカル境界</param>
        /// <param name="meshLocalToEngine">メッシュローカルから造形ローカルへの変換</param>
        /// <param name="boundsSize">造形グリッドのワールドサイズ</param>
        /// <returns>フィットパラメータ</returns>
        internal static FitParams Compute(
            Bounds meshBounds,
            Matrix4x4 meshLocalToEngine,
            float boundsSize)
        {
            if (!TryBuildEngineBounds(meshBounds, meshLocalToEngine, out Vector3 center, out Vector3 extents))
            {
                return new FitParams(Vector3.zero, 1f);
            }

            float maxExtent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            float targetHalf = boundsSize * FitMarginRatio * 0.5f;
            float uniformScale = maxExtent > 1e-5f ? targetHalf / maxExtent : 1f;
            return new FitParams(center, uniformScale);
        }

        /// <summary>
        /// 造形グリッド上の点を元メッシュローカル座標へ写す
        /// </summary>
        /// <param name="engineLocal">造形ローカル座標</param>
        /// <param name="fit">フィットパラメータ</param>
        /// <param name="engineLocalToWorld">造形ローカルからワールドへの変換</param>
        /// <param name="worldToMeshLocal">ワールドからメッシュローカルへの変換</param>
        /// <returns>メッシュローカル座標</returns>
        internal static Vector3 MapEngineLocalToMeshLocal(
            Vector3 engineLocal,
            FitParams fit,
            Matrix4x4 engineLocalToWorld,
            Matrix4x4 worldToMeshLocal)
        {
            float scale = Mathf.Max(fit.UniformScale, 1e-5f);
            Vector3 unfittedEngine = engineLocal / scale + fit.MeshCenterEngine;
            Vector3 world = engineLocalToWorld.MultiplyPoint3x4(unfittedEngine);
            return worldToMeshLocal.MultiplyPoint3x4(world);
        }

        private static bool TryBuildEngineBounds(
            Bounds meshBounds,
            Matrix4x4 meshLocalToEngine,
            out Vector3 center,
            out Vector3 extents)
        {
            Vector3 boundsMin = Vector3.positiveInfinity;
            Vector3 boundsMax = Vector3.negativeInfinity;
            Vector3 meshMin = meshBounds.min;
            Vector3 meshMax = meshBounds.max;

            for (int xi = 0; xi < 2; xi++)
            {
                for (int yi = 0; yi < 2; yi++)
                {
                    for (int zi = 0; zi < 2; zi++)
                    {
                        var corner = new Vector3(
                            xi == 0 ? meshMin.x : meshMax.x,
                            yi == 0 ? meshMin.y : meshMax.y,
                            zi == 0 ? meshMin.z : meshMax.z);
                        Vector3 engine = meshLocalToEngine.MultiplyPoint3x4(corner);
                        boundsMin = Vector3.Min(boundsMin, engine);
                        boundsMax = Vector3.Max(boundsMax, engine);
                    }
                }
            }

            center = (boundsMin + boundsMax) * 0.5f;
            extents = (boundsMax - boundsMin) * 0.5f;
            return extents.sqrMagnitude > 1e-10f;
        }

        private static bool TryGetVertexBounds(
            Vector3[] vertices,
            out Vector3 center,
            out Vector3 extents)
        {
            if (vertices == null || vertices.Length == 0)
            {
                center = Vector3.zero;
                extents = Vector3.zero;
                return false;
            }

            Vector3 boundsMin = Vector3.positiveInfinity;
            Vector3 boundsMax = Vector3.negativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                boundsMin = Vector3.Min(boundsMin, vertices[i]);
                boundsMax = Vector3.Max(boundsMax, vertices[i]);
            }

            center = (boundsMin + boundsMax) * 0.5f;
            extents = (boundsMax - boundsMin) * 0.5f;
            return extents.sqrMagnitude > 1e-10f;
        }
    }
}
