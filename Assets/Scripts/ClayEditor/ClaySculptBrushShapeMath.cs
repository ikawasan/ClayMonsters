using Unity.Burst;
using Unity.Mathematics;

namespace ClayEditor
{
    /// <summary>
    /// 成形ブラシ形状の影響度と境界を計算する
    /// </summary>
    public static class ClaySculptBrushShapeMath
    {
        /// <summary>
        /// ブラシ形状内の影響度を返す
        /// </summary>
        /// <param name="localPos">ボクセルローカル座標</param>
        /// <param name="hitPosition">ブラシ中心</param>
        /// <param name="radius">ブラシ半径</param>
        /// <param name="shape">ブラシ形状</param>
        /// <param name="orientation">ブラシ向き</param>
        /// <returns>形状外なら0内側なら0から1の影響度</returns>
        [BurstCompile]
        public static float EvaluateInfluence(
            float3 localPos,
            float3 hitPosition,
            float radius,
            ClaySculptBrushShape shape,
            ClaySculptBrushOrientation orientation)
        {
            if (radius <= 1e-6f)
            {
                return 0f;
            }

            float3 position = localPos - hitPosition;
            if (shape != ClaySculptBrushShape.Sphere)
            {
                position = orientation.ToBrushSpace(position);
            }

            switch (shape)
            {
                case ClaySculptBrushShape.Cube:
                    return EvaluateCubeInfluence(position, radius);
                case ClaySculptBrushShape.Cone:
                    return EvaluateConeInfluence(position, radius);
                case ClaySculptBrushShape.SquarePyramid:
                    return EvaluateSquarePyramidInfluence(position, radius);
                case ClaySculptBrushShape.CircularRing:
                    return EvaluateCircularRingInfluence(position, radius);
                default:
                    return EvaluateSphereInfluence(position, radius);
            }
        }

        [BurstCompile]
        private static float EvaluateSphereInfluence(float3 position, float radius)
        {
            float radiusSq = radius * radius;
            float distSq = math.lengthsq(position);
            if (distSq >= radiusSq)
            {
                return 0f;
            }

            return 1f - distSq / radiusSq;
        }

        [BurstCompile]
        private static float EvaluateCubeInfluence(float3 position, float radius)
        {
            float3 absolute = math.abs(position);
            float maxAxis = math.cmax(absolute);
            if (maxAxis >= radius)
            {
                return 0f;
            }

            float normalized = maxAxis / radius;
            return 1f - normalized * normalized;
        }

        [BurstCompile]
        private static float EvaluateConeInfluence(float3 position, float radius)
        {
            if (position.y < -radius || position.y > radius)
            {
                return 0f;
            }

            float allowedRadius = math.saturate((radius - position.y) / (radius * 2f)) * radius;
            if (allowedRadius <= 1e-6f)
            {
                return 0f;
            }

            float radialDistance = math.length(position.xz);
            if (radialDistance >= allowedRadius)
            {
                return 0f;
            }

            float radialNormalized = radialDistance / allowedRadius;
            return 1f - radialNormalized * radialNormalized;
        }

        [BurstCompile]
        private static float EvaluateSquarePyramidInfluence(float3 position, float radius)
        {
            if (position.y < -radius || position.y > radius)
            {
                return 0f;
            }

            float allowedHalfExtent = math.saturate((radius - position.y) / (radius * 2f)) * radius;
            if (allowedHalfExtent <= 1e-6f)
            {
                return 0f;
            }

            float planarDistance = math.cmax(math.abs(position.xz));
            if (planarDistance >= allowedHalfExtent)
            {
                return 0f;
            }

            float planarNormalized = planarDistance / allowedHalfExtent;
            return 1f - planarNormalized * planarNormalized;
        }

        [BurstCompile]
        private static float EvaluateCircularRingInfluence(float3 position, float radius)
        {
            float tubeRadius = radius * 0.35f;
            float ringRadius = radius * 0.65f;
            float2 ringOffset = new float2(math.length(position.xy) - ringRadius, position.z);
            float distSq = math.lengthsq(ringOffset);
            float tubeRadiusSq = tubeRadius * tubeRadius;
            if (distSq >= tubeRadiusSq)
            {
                return 0f;
            }

            return 1f - distSq / tubeRadiusSq;
        }
    }
}
