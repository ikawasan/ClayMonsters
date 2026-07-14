using System;
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
                if (IsHitStopActive)
                {
                    return 0f;
                }

                return Time.unscaledDeltaTime * SlowMotionScale;
            }
        }

        /// <summary>
        /// 演出用時間制御を通常状態へ戻す
        /// </summary>
        public static void Reset()
        {
            IsHitStopActive = false;
            SlowMotionScale = 1f;
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Time.timeScale = 1f;
            }
        }
    }
}
