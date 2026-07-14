using UnityEngine;

namespace Camera.Interface
{
    /// <summary>
    /// 粘土編集カメラの状態とロジックを担うModel
    /// </summary>
    public interface IClayEditCameraModel
    {
        /// <summary>
        /// カメラ操作が有効か
        /// </summary>
        bool IsOperatable { get; }

        /// <summary>
        /// 水平回転角（度）
        /// </summary>
        float HorizontalAngle { get; }

        /// <summary>
        /// 垂直回転角（度）
        /// </summary>
        float VerticalAngle { get; }

        /// <summary>
        /// 注視点からのカメラ距離
        /// </summary>
        float Distance { get; }

        /// <summary>
        /// 周回の中心となる注視点
        /// </summary>
        Vector3 FocusPoint { get; }

        /// <summary>
        /// 補間速度・ステップ角・距離範囲などのパラメータを設定する
        /// </summary>
        /// <param name="stepAngle">プリセットのステップ回転角（度）</param>
        /// <param name="distanceRange">距離の最小値(x)・最大値(y)</param>
        /// <param name="verticalRange">垂直角の最小値(x)・最大値(y)</param>
        void Configure(float stepAngle, Vector2 distanceRange, Vector2 verticalRange);

        /// <summary>
        /// カメラ操作の有効・無効を設定する
        /// </summary>
        void SetEnabled(bool isEnabled);

        /// <summary>
        /// 注視点を設定する
        /// </summary>
        void SetFocus(Vector3 focusPoint);

        /// <summary>
        /// 現在の回転・距離を即時同期する
        /// </summary>
        void Synchronize(float horizontalAngle, float verticalAngle, float distance);

        /// <summary>
        /// ドラッグによる自由回転を加える
        /// </summary>
        void Rotate(Vector2 scaledDelta);

        /// <summary>
        /// ズーム量を加える
        /// </summary>
        void Zoom(float zoomAmount);

        /// <summary>
        /// 画面上のドラッグ量に応じて注視点を移動する
        /// </summary>
        /// <param name="screenDelta">マウス移動量</param>
        /// <param name="panSpeed">移動速度係数</param>
        void PanFocus(Vector2 screenDelta, float panSpeed);

        /// <summary>
        /// 正面ビューにする
        /// </summary>
        void SetFrontView();

        /// <summary>
        /// 右ビューにする
        /// </summary>
        void SetRightView();

        /// <summary>
        /// 真上ビューにする
        /// </summary>
        void SetTopView();

        /// <summary>
        /// 現在の向きから180度反転する
        /// </summary>
        void FlipView();

        /// <summary>
        /// ヨー方向にステップ回転する
        /// </summary>
        /// <param name="direction">回転方向（+1 / -1）</param>
        void StepYaw(int direction);

        /// <summary>
        /// ピッチ方向にステップ回転する
        /// </summary>
        /// <param name="direction">回転方向（+1 / -1）</param>
        void StepPitch(int direction);
    }
}