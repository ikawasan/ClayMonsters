using UnityEngine;

namespace Camera.Interface
{
    public interface IClayEditCameraView
    {

        /// <summary>
        /// Cinemachineカメラの表示優先度を切り替える
        /// OFF時は操作受付も止める
        /// </summary>
        void SetCameraEnable(bool isEnable);

        /// <summary>
        /// カメラ操作の受付のみを切り替える(表示やCinemachine優先度は変えない)。
        /// 保存UIなど一時的に操作を止めたいときに使う。
        /// </summary>
        void SetCameraOperatable(bool isOperatable);

        /// <summary>
        /// オービット回転にAltキーを必須にするか切り替える
        /// </summary>
        /// <param name="requiresAlt">trueならAlt+右ドラッグで回転する</param>
        void SetOrbitInputRequiresAlt(bool requiresAlt);

        /// <summary>
        /// 操作の中心となる注視点を設定
        /// </summary>
        void SetFocusPosition(Vector3 centerPosition);

        /// <summary>
        /// シーン入場時のカメラ位置を確定する
        /// </summary>
        void SetInitializeView();

        /// <summary>
        /// オービット角度と距離を即時反映する
        /// </summary>
        void SetOrbitView(float horizontalAngle, float verticalAngle, float distance);

        /// <summary>
        /// 現在のオービット構図を取得する
        /// </summary>
        /// <param name="horizontalAngle">水平回転角</param>
        /// <param name="verticalAngle">垂直回転角</param>
        /// <param name="distance">オービット距離</param>
        /// <param name="focusPoint">注視点</param>
        /// <returns>取得できたらtrue</returns>
        bool TryGetOrbitState(
            out float horizontalAngle,
            out float verticalAngle,
            out float distance,
            out Vector3 focusPoint);

        /// <summary>
        /// シーン明転前にブレンドなしで現在の構図へ即座に合わせる
        /// </summary>
        void PrepareSceneEntry();

        /// <summary>
        /// シーン入場時に抑えていたオービットダンピングを復元する
        /// </summary>
        void ReleaseSceneEntryDamping();
    }
}