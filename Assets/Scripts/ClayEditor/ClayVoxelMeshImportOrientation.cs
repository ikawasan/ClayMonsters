using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// GLB再インポートメッシュをClayEdit造形空間のY-up ZFrontへ揃える
    /// </summary>
    internal static class ClayVoxelMeshImportOrientation
    {
        private const float DominantAxisRatio = 0.85f;

        /// <summary>
        /// 造形空間頂点をY-up ZFrontへ補正する
        /// </summary>
        /// <param name="engineVertices">造形ローカル頂点群</param>
        internal static void AlignToClayEditConvention(Vector3[] engineVertices)
        {
            if (engineVertices == null || engineVertices.Length == 0)
            {
                return;
            }

            if (!TryGetBounds(engineVertices, out Vector3 boundsMin, out Vector3 boundsMax))
            {
                return;
            }

            Vector3 size = boundsMax - boundsMin;
            if (size.y >= size.x * DominantAxisRatio && size.y >= size.z * DominantAxisRatio)
            {
                return;
            }

            if (size.y <= size.x && size.y <= size.z)
            {
                ApplyRotation(engineVertices, RotateMinus90X);
                return;
            }

            if (size.x <= size.y && size.x <= size.z)
            {
                ApplyRotation(engineVertices, RotatePlus90Z);
            }
        }

        private static Vector3 RotateMinus90X(Vector3 value)
        {
            return new Vector3(value.x, value.z, -value.y);
        }

        private static Vector3 RotatePlus90Z(Vector3 value)
        {
            return new Vector3(-value.y, value.x, value.z);
        }

        private static void ApplyRotation(Vector3[] vertices, System.Func<Vector3, Vector3> rotate)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotate(vertices[i]);
            }
        }

        private static bool TryGetBounds(Vector3[] vertices, out Vector3 boundsMin, out Vector3 boundsMax)
        {
            boundsMin = Vector3.positiveInfinity;
            boundsMax = Vector3.negativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                boundsMin = Vector3.Min(boundsMin, vertices[i]);
                boundsMax = Vector3.Max(boundsMax, vertices[i]);
            }

            Vector3 extents = (boundsMax - boundsMin) * 0.5f;
            return extents.sqrMagnitude > 1e-10f;
        }
    }
}
