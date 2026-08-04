using UnityEngine;

namespace Setting
{
    /// <summary>
    /// 起動時にフレームレートを60固定し垂直同期を無効にする
    /// </summary>
    public sealed class FPSController : MonoBehaviour
    {
        /// <summary>
        /// 固定フレームレート
        /// </summary>
        public const int TargetFps = 60;

        private void Awake()
        {
            ApplyFixedFrameRate();
        }

        private void Start()
        {
            // 他システムのAwake後に上書きされても戻す
            ApplyFixedFrameRate();
        }

        /// <summary>
        /// 60fps固定と垂直同期オフを適用する
        /// </summary>
        public static void ApplyFixedFrameRate()
        {
            // モニターリフレッシュ率へ張り付かないようVSyncを切る
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
        }
    }
}
