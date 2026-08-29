using Unity.Burst;
using Unity.Mathematics;

namespace ClayEditor
{
    /// <summary>
    /// 成形ブラシのローカル座標軸
    /// Z軸がカメラ側Y軸が画面上方向X軸が画面右方向
    /// </summary>
    public readonly struct ClaySculptBrushOrientation
    {
        /// <summary>
        /// ブラシ右方向
        /// </summary>
        public float3 AxisX { get; }

        /// <summary>
        /// ブラシ上方向
        /// </summary>
        public float3 AxisY { get; }

        /// <summary>
        /// ブラシ前方向(カメラ側)
        /// </summary>
        public float3 AxisZ { get; }

        /// <summary>
        /// 単位直交軸を構築する
        /// </summary>
        /// <param name="axisX">右方向</param>
        /// <param name="axisY">上方向</param>
        /// <param name="axisZ">前方向</param>
        public ClaySculptBrushOrientation(float3 axisX, float3 axisY, float3 axisZ)
        {
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
        }

        /// <summary>
        /// ワールド軸と一致する向き
        /// </summary>
        public static ClaySculptBrushOrientation Identity =>
            new(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), new float3(0f, 0f, 1f));

        /// <summary>
        /// 造形ローカル空間でビュー平面に平行な軸を構築する
        /// </summary>
        /// <param name="localAxisY">カメラ上方向</param>
        /// <param name="localAxisZ">カメラ側方向</param>
        public static ClaySculptBrushOrientation CreateViewAligned(
            float3 localAxisY,
            float3 localAxisZ)
        {
            if (math.lengthsq(localAxisZ) < 1e-8f
                || math.lengthsq(localAxisY) < 1e-8f)
            {
                return Identity;
            }

            float3 axisZ = math.normalize(localAxisZ);
            float3 axisY = math.normalize(localAxisY);
            float3 axisX = math.normalize(math.cross(axisY, axisZ));
            axisY = math.cross(axisZ, axisX);
            return new ClaySculptBrushOrientation(axisX, axisY, axisZ);
        }

        /// <summary>
        /// 造形ローカルオフセットをブラシローカル座標へ変換する
        /// </summary>
        /// <param name="localOffset">造形ローカルオフセット</param>
        [BurstCompile]
        public float3 ToBrushSpace(float3 localOffset)
        {
            return new float3(
                math.dot(localOffset, AxisX),
                math.dot(localOffset, AxisY),
                math.dot(localOffset, AxisZ));
        }
    }
}
