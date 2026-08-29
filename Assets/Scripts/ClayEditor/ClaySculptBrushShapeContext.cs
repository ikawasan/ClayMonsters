using R3;
using Unity.Mathematics;

namespace ClayEditor
{
    /// <summary>
    /// ClayEdit成形ブラシの現在形状と向きを保持する
    /// </summary>
    public sealed class ClaySculptBrushShapeContext
    {
        private readonly ReactiveProperty<ClaySculptBrushShape> currentShape =
            new(ClaySculptBrushShape.Sphere);

        private ClaySculptBrushOrientation orientation = ClaySculptBrushOrientation.Identity;

        /// <summary>
        /// 現在の成形ブラシ形状
        /// </summary>
        public ReadOnlyReactiveProperty<ClaySculptBrushShape> CurrentShape => currentShape;

        /// <summary>
        /// 現在の成形ブラシ形状値
        /// </summary>
        public ClaySculptBrushShape Shape => currentShape.Value;

        /// <summary>
        /// 現在の成形ブラシ向き
        /// </summary>
        public ClaySculptBrushOrientation Orientation => orientation;

        /// <summary>
        /// 成形ブラシ形状を変更する
        /// </summary>
        /// <param name="shape">新しい形状</param>
        public void SetShape(ClaySculptBrushShape shape)
        {
            currentShape.Value = shape;
        }

        /// <summary>
        /// 成形ブラシ形状と向きを既定値へ戻す
        /// </summary>
        public void Reset()
        {
            orientation = ClaySculptBrushOrientation.Identity;
            currentShape.Value = ClaySculptBrushShape.Sphere;
        }

        /// <summary>
        /// 造形ローカル空間でビュー平面に平行なブラシ向きを設定する
        /// </summary>
        /// <param name="localAxisY">カメラ上方向</param>
        /// <param name="localAxisZ">カメラ側方向</param>
        public void SetViewAlignedOrientation(float3 localAxisY, float3 localAxisZ)
        {
            orientation = ClaySculptBrushOrientation.CreateViewAligned(localAxisY, localAxisZ);
        }
    }
}
