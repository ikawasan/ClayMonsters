using System;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 標準敵AIの重み付けと挙動パラメータ
    /// </summary>
    [Serializable]
    public sealed class BattleEnemyAiProfile
    {
        [Header("技評価")]
        [SerializeField] private float powerWeight = 1.2f;
        [SerializeField] private float hitRateWeight = 0.85f;
        [SerializeField] private float rangeFitWeight = 1.1f;
        [SerializeField] private float finisherWeight = 2.8f;
        [SerializeField] private float lowOpponentHpWeight = 1.4f;

        [Header("間合い")]
        [SerializeField] private float approachMargin = 0.35f;
        [SerializeField] private float retreatMargin = 0.25f;

        [Header("心理")]
        [SerializeField] private float gutsReserveRatio = 0.18f;
        [SerializeField] private float lowSelfHpAggression = 0.35f;
        [SerializeField] private float attackRandomness = 0.12f;
        [SerializeField] private float secondBestMoveChance = 0.18f;

        [Header("部位修復")]
        [Tooltip("この間合い比率以上に離れているとき欠損部位を修復する")]
        [SerializeField] private float repairSafeDistanceRatio = 0.55f;

        /// <summary>
        /// 威力の重み
        /// </summary>
        public float PowerWeight => powerWeight;

        /// <summary>
        /// 命中率の重み
        /// </summary>
        public float HitRateWeight => hitRateWeight;

        /// <summary>
        /// 射程適合の重み
        /// </summary>
        public float RangeFitWeight => rangeFitWeight;

        /// <summary>
        /// とどめ優先の重み
        /// </summary>
        public float FinisherWeight => finisherWeight;

        /// <summary>
        /// 相手低HP時の攻撃性
        /// </summary>
        public float LowOpponentHpWeight => lowOpponentHpWeight;

        /// <summary>
        /// 接近を始める余裕距離
        /// </summary>
        public float ApproachMargin => approachMargin;

        /// <summary>
        /// 後退を始める余裕距離
        /// </summary>
        public float RetreatMargin => retreatMargin;

        /// <summary>
        /// 温存するガッツ比率
        /// </summary>
        public float GutsReserveRatio => gutsReserveRatio;

        /// <summary>
        /// 自身低HP時の攻撃性補正
        /// </summary>
        public float LowSelfHpAggression => lowSelfHpAggression;

        /// <summary>
        /// 技選択のランダム幅
        /// </summary>
        public float AttackRandomness => attackRandomness;

        /// <summary>
        /// 2番手の技を選ぶ確率
        /// </summary>
        public float SecondBestMoveChance => secondBestMoveChance;

        /// <summary>
        /// 欠損部位を修復し始める安全間合い比率
        /// </summary>
        public float RepairSafeDistanceRatio => repairSafeDistanceRatio;

        /// <summary>
        /// 既定プロファイル
        /// </summary>
        public static BattleEnemyAiProfile Default => new BattleEnemyAiProfile();
    }
}
