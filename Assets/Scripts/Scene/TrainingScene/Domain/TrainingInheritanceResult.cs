using ClayEditor.Rigging;
using SaveData;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成開始時の継承結果
    /// </summary>
    public sealed class TrainingInheritanceResult
    {
        /// <summary>
        /// 継承適用後のステータス
        /// </summary>
        public ModelStatus Status { get; }

        /// <summary>
        /// 継承適用後の攻撃構成
        /// </summary>
        public IReadOnlyList<MotionType> AttackMotions { get; }

        /// <summary>
        /// ステータス上昇量
        /// </summary>
        public TrainingStatGain StatGain { get; }

        /// <summary>
        /// 親1から継承した技
        /// </summary>
        public MotionType? InheritedAttackFromParentA { get; }

        /// <summary>
        /// 親2から継承した技
        /// </summary>
        public MotionType? InheritedAttackFromParentB { get; }

        /// <summary>
        /// 継承を適用したか
        /// </summary>
        public bool Applied { get; }

        /// <summary>
        /// 継承結果を生成する
        /// </summary>
        public TrainingInheritanceResult(
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            TrainingStatGain statGain,
            MotionType? inheritedAttackFromParentA,
            MotionType? inheritedAttackFromParentB,
            bool applied)
        {
            Status = status;
            AttackMotions = attackMotions;
            StatGain = statGain;
            InheritedAttackFromParentA = inheritedAttackFromParentA;
            InheritedAttackFromParentB = inheritedAttackFromParentB;
            Applied = applied;
        }

        /// <summary>
        /// 継承なしの結果を返す
        /// </summary>
        /// <param name="baseStatus">開始ステータス</param>
        /// <param name="baseAttacks">開始攻撃</param>
        public static TrainingInheritanceResult None(
            ModelStatus baseStatus,
            IReadOnlyList<MotionType> baseAttacks)
        {
            return new TrainingInheritanceResult(
                ModelStatus.CloneOrDefault(baseStatus),
                ModelAttackMotionUtility.Normalize(
                    baseAttacks,
                    TrainingSettings.AttackSlotCount),
                new TrainingStatGain(0, 0, 0, 0, 0),
                null,
                null,
                false);
        }
    }
}
