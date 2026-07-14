using Cysharp.Threading.Tasks;
using Extensions;
using System.Threading;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// ヒットストップとスローモーションをUpdateの早い段階で適用しGameplayTimeを制御する
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class BattleHitStopClock : MonoBehaviour
    {
        private enum EffectMode
        {
            None,
            HitStop,
            SlowMotion
        }

        private static BattleHitStopClock instance;

        [Header("ヒットストップ")]
        [SerializeField]
        [Tooltip("通常命中時に止める秒数(実時間)")]
        private float hitStopDuration = 0.2f;

        [Header("スローモーション")]
        [SerializeField]
        [Tooltip("部位破壊命中時にスローにする秒数(実時間)")]
        private float slowMotionDuration = 0.8f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("部位破壊時の再生速度倍率")]
        private float slowMotionScale = 0.25f;

        [Header("とどめ")]
        [SerializeField]
        [Tooltip("とどめ命中時にスローにする秒数(実時間)")]
        private float finishSlowMotionDuration = 2.8f;

        [SerializeField]
        [Range(0.03f, 1f)]
        [Tooltip("とどめ命中時の再生速度倍率")]
        private float finishSlowMotionScale = 0.05f;

        private EffectMode currentMode = EffectMode.None;
        private float remaining;
        private float activeSlowMotionScale = 1f;
        private static bool effectApplied;

        /// <summary>
        /// ヒットストップまたはスローモーション中か
        /// </summary>
        public static bool IsActive => instance != null && instance.remaining > 0f;

        /// <summary>
        /// Inspectorで設定したヒットストップ秒数
        /// </summary>
        public float HitStopDuration => hitStopDuration;

        /// <summary>
        /// ヒットストップを要求する(Inspectorの秒数を使う)
        /// </summary>
        public static void Request()
        {
            RequestHitStop();
        }

        /// <summary>
        /// ヒットストップを要求する(Inspectorの秒数を使う)
        /// </summary>
        public static void RequestHitStop()
        {
            float duration = instance != null ? instance.hitStopDuration : 0.2f;
            RequestHitStop(duration);
        }

        /// <summary>
        /// ヒットストップを要求する
        /// </summary>
        public static void RequestHitStop(float duration)
        {
            RequestEffect(EffectMode.HitStop, duration);
        }

        /// <summary>
        /// スローモーションを要求する(Inspectorの秒数と倍率を使う)
        /// </summary>
        public static void RequestSlowMotion()
        {
            if (instance == null)
            {
                RequestEffect(EffectMode.SlowMotion, 0.8f, 0.25f);
                return;
            }

            RequestEffect(EffectMode.SlowMotion, instance.slowMotionDuration, instance.slowMotionScale);
        }

        /// <summary>
        /// スローモーションを要求する
        /// </summary>
        public static void RequestSlowMotion(float duration, float timeScale)
        {
            RequestEffect(EffectMode.SlowMotion, duration, timeScale);
        }

        /// <summary>
        /// とどめ命中時のスローモーションを要求する(Inspectorの秒数と倍率を使う)
        /// </summary>
        public static void RequestFinishSlowMotion()
        {
            if (instance == null)
            {
                RequestEffect(EffectMode.SlowMotion, 2.8f, 0.05f);
                return;
            }

            RequestEffect(
                EffectMode.SlowMotion,
                instance.finishSlowMotionDuration,
                instance.finishSlowMotionScale,
                forceOverride: true);
        }

        /// <summary>
        /// ヒットストップが終わるまで待機する
        /// </summary>
        public static async UniTask WaitUntilFinishedAsync(CancellationToken cancellationToken)
        {
            while (IsActive && !cancellationToken.IsCancellationRequested)
            {
                Tick(Time.unscaledDeltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        /// <summary>
        /// ヒットストップとスローモーションを解除する
        /// </summary>
        public static void Clear()
        {
            if (instance != null)
            {
                instance.remaining = 0f;
                instance.currentMode = EffectMode.None;
                instance.activeSlowMotionScale = 1f;
            }

            ReleaseEffect();
        }

        /// <summary>
        /// 演出時間を進める
        /// Updateでも進めるため未登録時のフォールバック用
        /// </summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (instance == null || instance.remaining <= 0f)
            {
                return;
            }

            // Updateが動いている場合は二重減算しない
            if (instance.isActiveAndEnabled)
            {
                ApplyEffect();
                return;
            }

            ApplyEffect();
            instance.remaining -= unscaledDeltaTime;

            if (instance.remaining <= 0f)
            {
                instance.remaining = 0f;
                instance.currentMode = EffectMode.None;
                ReleaseEffect();
            }
        }

        private static void RequestEffect(
            EffectMode mode,
            float duration,
            float timeScale = 1f,
            bool forceOverride = false)
        {
            if (duration <= 0f)
            {
                return;
            }

            EnsureInstance();

            if (mode == EffectMode.SlowMotion)
            {
                // とどめは部位破壊スローより優先し上書きする
                if (forceOverride
                    || instance.currentMode != EffectMode.SlowMotion
                    || timeScale < instance.activeSlowMotionScale)
                {
                    instance.activeSlowMotionScale = Mathf.Clamp(timeScale, 0.03f, 1f);
                }

                instance.currentMode = EffectMode.SlowMotion;
            }
            else if (instance.currentMode != EffectMode.SlowMotion)
            {
                instance.currentMode = EffectMode.HitStop;
            }

            instance.remaining = forceOverride
                ? Mathf.Max(duration, instance.remaining)
                : Mathf.Max(instance.remaining, duration);
            ApplyEffect();
        }

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            instance = Object.FindFirstObjectByType<BattleHitStopClock>(FindObjectsInactive.Include);
            if (instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(BattleHitStopClock));
            instance = host.AddComponent<BattleHitStopClock>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void Update()
        {
            if (remaining <= 0f || currentMode == EffectMode.None)
            {
                return;
            }

            // 他処理がtimeScaleを戻しても毎フレーム再適用する
            ApplyEffect();
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                currentMode = EffectMode.None;
                ReleaseEffect();
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            instance = null;
            remaining = 0f;
            currentMode = EffectMode.None;
            ReleaseEffect();
        }

        private static void ApplyEffect()
        {
            if (instance == null || instance.currentMode == EffectMode.None)
            {
                return;
            }

            effectApplied = true;

            if (instance.currentMode == EffectMode.HitStop)
            {
                GameplayTime.IsHitStopActive = true;
                GameplayTime.SlowMotionScale = 1f;
                return;
            }

            GameplayTime.IsHitStopActive = false;
            GameplayTime.SlowMotionScale = instance.activeSlowMotionScale;
            Time.timeScale = instance.activeSlowMotionScale;
        }

        private static void ReleaseEffect()
        {
            GameplayTime.Reset();
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Time.timeScale = 1f;
            }

            effectApplied = false;
        }
    }
}
