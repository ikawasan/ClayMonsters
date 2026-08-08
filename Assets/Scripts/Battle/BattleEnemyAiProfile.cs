using System;
using SaveData;
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
        [Tooltip("この距離差以上ならステップで間合いを調整する")]
        [SerializeField] private float stepGapThreshold = 1.6f;

        [Header("ふきとばし")]
        [Tooltip("推奨技射程下限よりこの距離内側ならふきとばしを検討する")]
        [SerializeField] private float knockbackRangeInside = 0.4f;
        [Tooltip("低HP時にふきとばしを優先する間合い上限比率")]
        [SerializeField] private float knockbackEscapeDistanceRatio = 0.4f;
        [Tooltip("条件を満たしたときふきとばしする確率")]
        [Range(0f, 1f)]
        [SerializeField] private float knockbackChance = 0.72f;

        [Header("心理")]
        [SerializeField] private float gutsReserveRatio = 0.18f;
        [SerializeField] private float lowSelfHpAggression = 0.35f;
        [SerializeField] private float attackRandomness = 0.12f;
        [SerializeField] private float secondBestMoveChance = 0.18f;

        [Header("部位修復")]
        [Tooltip("この間合い比率以上に離れているとき欠損部位を修復する")]
        [SerializeField] private float repairSafeDistanceRatio = 0.55f;

        [Header("攻撃反応")]
        [Tooltip("攻撃可能になってから実際に撃つまでの最短待ち(秒)")]
        [SerializeField] private float attackCommitDelayMin = 0.45f;
        [Tooltip("攻撃可能になってから実際に撃つまでの最長待ち(秒)")]
        [SerializeField] private float attackCommitDelayMax = 0.95f;

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
        /// ステップで補う距離差のしきい値
        /// </summary>
        public float StepGapThreshold => Mathf.Max(0.5f, stepGapThreshold);

        /// <summary>
        /// 推奨技の下限より内側とみなす距離
        /// </summary>
        public float KnockbackRangeInside => Mathf.Max(0f, knockbackRangeInside);

        /// <summary>
        /// 低HP時のふきとばし優先間合い比率
        /// </summary>
        public float KnockbackEscapeDistanceRatio => Mathf.Clamp01(knockbackEscapeDistanceRatio);

        /// <summary>
        /// 条件成立時にふきとばしする確率
        /// </summary>
        public float KnockbackChance => Mathf.Clamp01(knockbackChance);

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
        /// 攻撃コミット待ちの最短秒数
        /// </summary>
        public float AttackCommitDelayMin => Mathf.Max(0f, attackCommitDelayMin);

        /// <summary>
        /// 攻撃コミット待ちの最長秒数
        /// </summary>
        public float AttackCommitDelayMax =>
            Mathf.Max(AttackCommitDelayMin, attackCommitDelayMax);

        /// <summary>
        /// 既定プロファイル(普通相当)
        /// </summary>
        public static BattleEnemyAiProfile Default => FromStrengthTier(EnemyStrengthTier.Normal);

        /// <summary>
        /// 敵強さ段階に応じたAIプロファイルを作る
        /// 強いほど間合い制御と技選択が適切になる
        /// </summary>
        /// <param name="tier">敵の強さ段階</param>
        public static BattleEnemyAiProfile FromStrengthTier(EnemyStrengthTier tier)
        {
            var profile = new BattleEnemyAiProfile();
            profile.ApplyStrengthTier(tier);
            return profile;
        }

        /// <summary>
        /// 育成専用強さに応じたAIプロファイルを作る
        /// NPC対戦より反応と技選択を控えめにする
        /// </summary>
        /// <param name="tier">育成強さ段階</param>
        public static BattleEnemyAiProfile FromTrainingStrengthTier(TrainingEnemyStrengthTier tier)
        {
            // 育成最強でもNPCの強い相当まで
            EnemyStrengthTier mapped = TrainingEnemyStrengthStatusCatalog.ToAiTier(tier);
            return FromStrengthTier(mapped);
        }

        private void ApplyStrengthTier(EnemyStrengthTier tier)
        {
            // 0=弱 1=普通 2=強 3=超強 4=最強
            float skill01 = Mathf.Clamp01((int)tier / 4f);

            // 弱い:ゆるくランダムが強い:タイトで迅速
            powerWeight = Mathf.Lerp(0.95f, 1.45f, skill01);
            hitRateWeight = Mathf.Lerp(0.55f, 1.15f, skill01);
            rangeFitWeight = Mathf.Lerp(0.7f, 1.55f, skill01);
            finisherWeight = Mathf.Lerp(1.4f, 4.0f, skill01);
            lowOpponentHpWeight = Mathf.Lerp(0.7f, 2.0f, skill01);

            approachMargin = Mathf.Lerp(0.7f, 0.12f, skill01);
            retreatMargin = Mathf.Lerp(0.55f, 0.08f, skill01);
            stepGapThreshold = Mathf.Lerp(2.4f, 1.0f, skill01);

            knockbackRangeInside = Mathf.Lerp(0.15f, 0.55f, skill01);
            knockbackEscapeDistanceRatio = Mathf.Lerp(0.28f, 0.5f, skill01);
            knockbackChance = Mathf.Lerp(0.28f, 0.95f, skill01);

            gutsReserveRatio = Mathf.Lerp(0.08f, 0.28f, skill01);
            lowSelfHpAggression = Mathf.Lerp(0.5f, 0.28f, skill01);
            attackRandomness = Mathf.Lerp(0.45f, 0.02f, skill01);
            secondBestMoveChance = Mathf.Lerp(0.45f, 0.04f, skill01);

            repairSafeDistanceRatio = Mathf.Lerp(0.7f, 0.42f, skill01);

            attackCommitDelayMin = Mathf.Lerp(0.85f, 0.08f, skill01);
            attackCommitDelayMax = Mathf.Lerp(1.55f, 0.22f, skill01);

            // ティア固有の微調整
            switch (tier)
            {
                case EnemyStrengthTier.Weak:
                    // 不用意に近づきがち
                    approachMargin = 0.85f;
                    knockbackChance = 0.18f;
                    attackCommitDelayMin = 0.95f;
                    attackCommitDelayMax = 1.7f;
                    break;
                case EnemyStrengthTier.Strongest:
                    // ほぼ最適挙動
                    approachMargin = 0.1f;
                    retreatMargin = 0.06f;
                    stepGapThreshold = 0.9f;
                    attackRandomness = 0f;
                    secondBestMoveChance = 0.02f;
                    attackCommitDelayMin = 0.05f;
                    attackCommitDelayMax = 0.18f;
                    knockbackChance = 1f;
                    rangeFitWeight = 1.7f;
                    finisherWeight = 4.4f;
                    break;
            }
        }
    }
}
