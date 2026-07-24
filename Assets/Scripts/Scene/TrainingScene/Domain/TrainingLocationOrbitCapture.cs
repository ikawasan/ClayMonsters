using System;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成カメラ構図のスナップショット
    /// 実行中カメラから設定へ書き戻すときに使う
    /// </summary>
    [Serializable]
    public readonly struct TrainingLocationOrbitCapture
    {
        /// <summary>
        /// 水平回転角
        /// </summary>
        public float HorizontalAngle { get; }

        /// <summary>
        /// 垂直回転角
        /// </summary>
        public float VerticalAngle { get; }

        /// <summary>
        /// オービット距離
        /// </summary>
        public float Distance { get; }

        /// <summary>
        /// 注視点の高さオフセット
        /// </summary>
        public float FocusHeightOffset { get; }

        /// <summary>
        /// 注視点の左右オフセット
        /// </summary>
        public float FocusSideOffset { get; }

        /// <summary>
        /// 注視点の奥行きオフセット
        /// </summary>
        public float FocusForwardOffset { get; }

        /// <summary>
        /// スナップショットを生成する
        /// </summary>
        public TrainingLocationOrbitCapture(
            float horizontalAngle,
            float verticalAngle,
            float distance,
            float focusHeightOffset,
            float focusSideOffset,
            float focusForwardOffset)
        {
            HorizontalAngle = horizontalAngle;
            VerticalAngle = verticalAngle;
            Distance = distance;
            FocusHeightOffset = focusHeightOffset;
            FocusSideOffset = focusSideOffset;
            FocusForwardOffset = focusForwardOffset;
        }
    }
}
