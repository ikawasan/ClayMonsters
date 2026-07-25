using System;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘間合いに応じたカメラ構図の設定値
    /// </summary>
    [Serializable]
    public sealed class BattleFieldCameraProfile
    {
        [Header("オービット角度")]
        [SerializeField] private float horizontalAngle = 0f;
        [SerializeField] private float verticalAngle = 10f;

        [Header("間合い別カメラ距離")]
        [SerializeField] private float orbitDistanceAtClose = 4.5f;
        [SerializeField] private float orbitDistanceAtFar = 7.5f;

        [Header("フレーミング")]
        [Tooltip("両モデルの左右端から画面端までの余白(ワールド単位)")]
        [SerializeField] private float framingSideMargin = 1.1f;
        [Tooltip("注視点の高さオフセット")]
        [SerializeField] private float focusHeightOffset = 0.75f;
        [Tooltip("距離計算に使う垂直視野角(シーンのCinemachineと揃える)")]
        [SerializeField] private float verticalFovDegrees = 46f;
        [SerializeField] private float minOrbitDistance = 4f;
        [SerializeField] private float maxOrbitDistance = 12f;

        [Header("追従")]
        [SerializeField] private float focusSmoothing = 12f;
        [SerializeField] private float distanceSmoothing = 10f;
        [SerializeField] private float horizontalAngleSmoothing = 8f;

        [Header("扇状オービット")]
        [Tooltip("戦闘中心からの最大ずれ時に加える水平角(度)")]
        [SerializeField] private float maxFanAngleDegrees = 14f;

        [Header("待機オービット")]
        [Tooltip("移動していなくてもカメラが回り続ける水平振幅(度)")]
        [SerializeField] private float idleOrbitAmplitudeDegrees = 6f;
        [Tooltip("待機オービット1周期の秒数")]
        [SerializeField] private float idleOrbitPeriodSeconds = 8f;
        [Tooltip("待機中の垂直角の微小振幅(度)")]
        [SerializeField] private float idleVerticalAmplitudeDegrees = 1.2f;

        [Header("攻撃演出")]
        [SerializeField] private float attackOrbitDistance = 3.8f;
        [SerializeField] private float attackVerticalAngle = 8f;
        [SerializeField] private float attackFocusHeightOffset = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float attackFocusTowardTargetRatio = 0.32f;
        [Tooltip("攻撃側正面からの横偏り角度(度・小さいほど正面寄り)")]
        [SerializeField] private float attackSideBiasDegrees = 28f;
        [SerializeField] private float attackFocusSmoothing = 20f;
        [SerializeField] private float attackDistanceSmoothing = 18f;
        [SerializeField] private float attackAngleSmoothing = 16f;
        [SerializeField] private float attackHoldRecoveryMultiplier = 0.9f;
        [SerializeField] private float attackMinimumHoldSeconds = 0.35f;
        [Tooltip("攻撃カメラのフレーミング余白係数(小さいほど寄る1以上で余白増)")]
        [SerializeField] private float attackFramePadding = 1.08f;
        [SerializeField] private float attackMinOrbitDistance = 3.4f;
        [SerializeField] private float attackMaxOrbitDistance = 14f;
        [SerializeField] private float enemyTelegraphDurationEstimate = 0.65f;
        [SerializeField] private float enemyTelegraphHoldPadding = 0.15f;

        [Header("画面揺れ")]
        [Tooltip("通常ヒット時の揺れ幅(ワールド単位)")]
        [SerializeField] private float hitShakeAmplitude = 0.12f;
        [Tooltip("通常ヒット時の揺れ秒数(実時間)")]
        [SerializeField] private float hitShakeDuration = 0.18f;
        [Tooltip("通常ヒット時の揺れ周波数")]
        [SerializeField] private float hitShakeFrequency = 28f;
        [Tooltip("部位破壊時の揺れ幅(ワールド単位)")]
        [SerializeField] private float partBreakShakeAmplitude = 0.32f;
        [Tooltip("部位破壊時の揺れ秒数(実時間)")]
        [SerializeField] private float partBreakShakeDuration = 0.4f;
        [Tooltip("部位破壊時の揺れ周波数")]
        [SerializeField] private float partBreakShakeFrequency = 38f;

        /// <summary>
        /// 水平回転角
        /// </summary>
        public float HorizontalAngle => horizontalAngle;

        /// <summary>
        /// 垂直回転角
        /// </summary>
        public float VerticalAngle => verticalAngle;

        /// <summary>
        /// 密着時のオービット距離
        /// </summary>
        public float OrbitDistanceAtClose => orbitDistanceAtClose;

        /// <summary>
        /// 最大間合い時のオービット距離
        /// </summary>
        public float OrbitDistanceAtFar => orbitDistanceAtFar;

        /// <summary>
        /// 注視点の追従速度
        /// </summary>
        public float FocusSmoothing => focusSmoothing;

        /// <summary>
        /// カメラ距離の追従速度
        /// </summary>
        public float DistanceSmoothing => distanceSmoothing;

        /// <summary>
        /// 画面左右の余白
        /// </summary>
        public float FramingSideMargin => framingSideMargin;

        /// <summary>
        /// 注視点の高さオフセット
        /// </summary>
        public float FocusHeightOffset => focusHeightOffset;

        /// <summary>
        /// フレーミング計算用の垂直視野角
        /// </summary>
        public float VerticalFovDegrees => verticalFovDegrees;

        /// <summary>
        /// オービット距離の下限
        /// </summary>
        public float MinOrbitDistance => minOrbitDistance;

        /// <summary>
        /// オービット距離の上限
        /// </summary>
        public float MaxOrbitDistance => maxOrbitDistance;

        /// <summary>
        /// 水平角の追従速度
        /// </summary>
        public float HorizontalAngleSmoothing => horizontalAngleSmoothing;

        /// <summary>
        /// 扇状オービットの最大水平角
        /// </summary>
        public float MaxFanAngleDegrees => maxFanAngleDegrees;

        /// <summary>
        /// 待機オービットの水平振幅
        /// </summary>
        public float IdleOrbitAmplitudeDegrees => idleOrbitAmplitudeDegrees;

        /// <summary>
        /// 待機オービットの周期秒数
        /// </summary>
        public float IdleOrbitPeriodSeconds => idleOrbitPeriodSeconds;

        /// <summary>
        /// 待機オービットの垂直振幅
        /// </summary>
        public float IdleVerticalAmplitudeDegrees => idleVerticalAmplitudeDegrees;

        /// <summary>
        /// 攻撃演出時のオービット距離
        /// </summary>
        public float AttackOrbitDistance => attackOrbitDistance;

        /// <summary>
        /// 攻撃演出時の垂直角
        /// </summary>
        public float AttackVerticalAngle => attackVerticalAngle;

        /// <summary>
        /// 攻撃演出時の注視点高さオフセット
        /// </summary>
        public float AttackFocusHeightOffset => attackFocusHeightOffset;

        /// <summary>
        /// 攻撃演出の注視点をターゲット方向へ寄せる比率
        /// </summary>
        public float AttackFocusTowardTargetRatio => attackFocusTowardTargetRatio;

        /// <summary>
        /// 攻撃演出時の水平角オフセット
        /// </summary>
        public float AttackSideBiasDegrees => attackSideBiasDegrees;

        /// <summary>
        /// 攻撃演出の注視点追従速度
        /// </summary>
        public float AttackFocusSmoothing => attackFocusSmoothing;

        /// <summary>
        /// 攻撃演出の距離追従速度
        /// </summary>
        public float AttackDistanceSmoothing => attackDistanceSmoothing;

        /// <summary>
        /// 攻撃演出の水平角追従速度
        /// </summary>
        public float AttackAngleSmoothing => attackAngleSmoothing;

        /// <summary>
        /// 攻撃硬直に対する演出保持倍率
        /// </summary>
        public float AttackHoldRecoveryMultiplier => attackHoldRecoveryMultiplier;

        /// <summary>
        /// 攻撃演出の最短保持秒数
        /// </summary>
        public float AttackMinimumHoldSeconds => attackMinimumHoldSeconds;

        /// <summary>
        /// 攻撃カメラのフレーミング余白係数
        /// </summary>
        public float AttackFramePadding => attackFramePadding > 0.01f ? attackFramePadding : 1.08f;

        /// <summary>
        /// 攻撃カメラ距離の下限
        /// </summary>
        public float AttackMinOrbitDistance => attackMinOrbitDistance > 0.01f ? attackMinOrbitDistance : 3.4f;

        /// <summary>
        /// 攻撃カメラ距離の上限
        /// </summary>
        public float AttackMaxOrbitDistance =>
            attackMaxOrbitDistance > AttackMinOrbitDistance ? attackMaxOrbitDistance : 14f;

        /// <summary>
        /// 敵予告時間の推定秒数
        /// </summary>
        public float EnemyTelegraphDurationEstimate => enemyTelegraphDurationEstimate;

        /// <summary>
        /// 敵予告演出の余白秒数
        /// </summary>
        public float EnemyTelegraphHoldPadding => enemyTelegraphHoldPadding;

        /// <summary>
        /// 通常ヒット時の画面揺れ幅
        /// </summary>
        public float HitShakeAmplitude => hitShakeAmplitude;

        /// <summary>
        /// 通常ヒット時の画面揺れ秒数
        /// </summary>
        public float HitShakeDuration => hitShakeDuration;

        /// <summary>
        /// 通常ヒット時の画面揺れ周波数
        /// </summary>
        public float HitShakeFrequency => hitShakeFrequency;

        /// <summary>
        /// 部位破壊時の画面揺れ幅
        /// </summary>
        public float PartBreakShakeAmplitude => partBreakShakeAmplitude;

        /// <summary>
        /// 部位破壊時の画面揺れ秒数
        /// </summary>
        public float PartBreakShakeDuration => partBreakShakeDuration;

        /// <summary>
        /// 部位破壊時の画面揺れ周波数
        /// </summary>
        public float PartBreakShakeFrequency => partBreakShakeFrequency;

        /// <summary>
        /// 既定のプロファイルを返す
        /// </summary>
        public static BattleFieldCameraProfile Default => new BattleFieldCameraProfile();
    }
}
