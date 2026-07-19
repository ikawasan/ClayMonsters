using UnityEngine;

namespace Extensions
{
    /// <summary>
    /// ヒットストップとスローモーション中のゲームプレイ用デルタ時間を提供する
    /// </summary>
    public static class GameplayTime
    {
        /// <summary>
        /// ヒットストップ中か
        /// </summary>
        public static bool IsHitStopActive { get; set; }

        /// <summary>
        /// Tipsなどによる戦闘一時停止中か
        /// </summary>
        public static bool IsPaused { get; set; }

        /// <summary>
        /// スローモーション倍率(1=通常)
        /// </summary>
        public static float SlowMotionScale { get; set; } = 1f;

        /// <summary>
        /// モーション等に使うデルタ時間
        /// </summary>
        public static float DeltaTime
        {
            get
            {
                if (IsPaused || IsHitStopActive)
                {
                    return 0f;
                }

                return Time.unscaledDeltaTime * SlowMotionScale;
            }
        }

        /// <summary>
        /// ヒット演出とSEに使うデルタ時間
        /// ヒットストップ中だけ非スケール時間で進めスローモーション時はゲーム側と同じ倍率にする
        /// </summary>
        public static float PresentationDeltaTime =>
            IsHitStopActive ? Time.unscaledDeltaTime : DeltaTime;

        /// <summary>
        /// パーティクルを非スケール時間で再生するか
        /// ヒットストップ中のみtrue
        /// </summary>
        public static bool UseUnscaledParticleTime => IsHitStopActive;

        /// <summary>
        /// 演出用時間制御を通常状態へ戻す
        /// </summary>
        public static void Reset()
        {
            IsHitStopActive = false;
            IsPaused = false;
            SlowMotionScale = 1f;
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Time.timeScale = 1f;
            }
        }
    }
}
