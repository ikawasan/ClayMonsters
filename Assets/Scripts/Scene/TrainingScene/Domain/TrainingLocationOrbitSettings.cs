using System;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成行き先ごとのオービットカメラ構図
    /// 水平角・垂直角・距離と注視点オフセットを保持する
    /// </summary>
    [Serializable]
    public sealed class TrainingLocationOrbitSettings
    {
        [Header("オービット")]
        [SerializeField] private float horizontalAngle;
        [SerializeField] private float verticalAngle = 20f;
        [SerializeField] private float distance = 15f;

        [Header("注視点オフセット")]
        [SerializeField] private float focusHeightOffset;
        [Tooltip("モデル中心から画面上の左右へずらす距離。正で右寄りの空間を注視する")]
        [SerializeField] private float focusSideOffset;
        [Tooltip("モデル中心から画面奥行きへずらす距離。正でカメラから遠ざかる")]
        [SerializeField] private float focusForwardOffset;

        /// <summary>
        /// 水平回転角
        /// </summary>
        public float HorizontalAngle => horizontalAngle;

        /// <summary>
        /// 垂直回転角
        /// </summary>
        public float VerticalAngle => verticalAngle;

        /// <summary>
        /// オービット距離
        /// </summary>
        public float Distance => distance;

        /// <summary>
        /// 注視点の高さオフセット
        /// </summary>
        public float FocusHeightOffset => focusHeightOffset;

        /// <summary>
        /// 注視点の左右オフセット
        /// </summary>
        public float FocusSideOffset => focusSideOffset;

        /// <summary>
        /// 注視点の奥行きオフセット
        /// </summary>
        public float FocusForwardOffset => focusForwardOffset;

        /// <summary>
        /// スナップショットの値を書き込む
        /// </summary>
        /// <param name="capture">反映する構図</param>
        public void ApplyCapture(TrainingLocationOrbitCapture capture)
        {
            horizontalAngle = capture.HorizontalAngle;
            verticalAngle = capture.VerticalAngle;
            distance = capture.Distance;
            focusHeightOffset = capture.FocusHeightOffset;
            focusSideOffset = capture.FocusSideOffset;
            focusForwardOffset = capture.FocusForwardOffset;
        }
    }
}
