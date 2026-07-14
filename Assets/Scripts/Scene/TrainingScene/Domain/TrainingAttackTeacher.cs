using Battle;
using ClayEditor.Rigging;
using System.Collections.Generic;
using UI.ClayEditor.View;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成イベント用の習得候補攻撃を選ぶ
    /// </summary>
    public static class TrainingAttackTeacher
    {
        /// <summary>
        /// 骨格で使用可能かつ未習得の攻撃を1つ返す
        /// </summary>
        /// <param name="currentAttacks">現在の攻撃</param>
        /// <param name="usableAttacks">骨格で使用可能な攻撃</param>
        /// <param name="random">乱数</param>
        /// <param name="attack">選ばれた攻撃</param>
        /// <returns>候補があればtrue</returns>
        public static bool TryPickLearnableAttack(
            IReadOnlyList<MotionType> currentAttacks,
            IReadOnlyList<MotionType> usableAttacks,
            System.Random random,
            out MotionType attack)
        {
            attack = default;
            if (currentAttacks == null
                || currentAttacks.Count == 0
                || usableAttacks == null
                || usableAttacks.Count == 0
                || random == null)
            {
                return false;
            }

            var candidates = new List<MotionType>();
            for (int i = 0; i < usableAttacks.Count; i++)
            {
                MotionType motion = usableAttacks[i];
                if (!ContainsAttack(currentAttacks, motion))
                {
                    candidates.Add(motion);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            attack = candidates[random.Next(candidates.Count)];
            return true;
        }

        /// <summary>
        /// 攻撃の技名のみを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackName(MotionType motion)
        {
            return MotionPartRequirement.GetDisplayName(motion);
        }

        /// <summary>
        /// 攻撃のUI表示ラベルを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackLabel(MotionType motion)
        {
            return $"{MotionPartRequirement.GetDisplayName(motion)}"
                + $" 威力{MotionPartRequirement.GetPowerDisplayValue(motion)}"
                + $" [{FormatRequiredPartLabel(motion)}]";
        }

        /// <summary>
        /// 攻撃の詳細情報をUI表示用テキストで返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackDetail(MotionType motion)
        {
            return MotionPartRequirement.GetDisplayName(motion)
                + " [" + FormatRequiredPartLabel(motion) + "]"
                + "\n破壊:" + MotionPartRequirement.FormatTargetDestroyPartLabel(motion)
                + "  " + FormatAttackStatsLine(motion);
        }

        /// <summary>
        /// 攻撃の数値情報行をUI表示用テキストで返す
        /// 部位情報はアイコン表示用に除外する
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackStatsLine(MotionType motion)
        {
            float accuracy = MotionPartRequirement.GetAccuracy(motion);
            return "威力:" + MotionPartRequirement.GetPowerDisplayValue(motion)
                + "  コスト:" + MotionPartRequirement.GetGutsCostDisplayValue(motion)
                + "  命中:" + BattleCombatRules.ToHitRateLabel(accuracy)
                + "  硬直:" + MotionPartRequirement.GetRecovery(motion).ToString("0.##") + "秒"
                + "  溜め:" + MotionPartRequirement.GetWindUpDuration(motion).ToString("0.##") + "秒";
        }

        /// <summary>
        /// 攻撃入れ替えボタン用の比較テキストを返す
        /// </summary>
        /// <param name="slotNumber">スロット番号(1始まり)</param>
        /// <param name="currentAttack">現在の攻撃</param>
        /// <param name="learnedAttack">習得する攻撃</param>
        public static string FormatAttackSwapButtonLabel(
            int slotNumber,
            MotionType currentAttack,
            MotionType learnedAttack)
        {
            return "スロット" + slotNumber + "を入れ替え"
                + "\n【現在】" + FormatAttackDetail(currentAttack)
                + "\n【習得】" + FormatAttackDetail(learnedAttack);
        }

        /// <summary>
        /// 再開確認向けの技情報テキストを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatResumeAttackDetail(MotionType motion)
        {
            return "攻撃名 " + MotionPartRequirement.GetDisplayName(motion)
                + "\n必要部位 " + FormatRequiredPartLabel(motion)
                + "\n破壊部位 " + MotionPartRequirement.FormatTargetDestroyPartLabel(motion)
                + "\nダメージ " + MotionPartRequirement.GetPowerDisplayValue(motion)
                + "\nコスト " + MotionPartRequirement.GetGutsCostDisplayValue(motion)
                + "\n間合い " + MotionPartRequirement.FormatRangeLabel(motion);
        }

        /// <summary>
        /// 再開確認向けの範囲ラベル
        /// </summary>
        public const string ResumeAttackRangeLabel = ModelSaveSummaryFormatter.TrainingAttackRangeLabel;

        /// <summary>
        /// 再開確認向けの数値情報テキストを返す
        /// 部位情報はアイコン表示用に除外する
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatResumeAttackStatsLine(MotionType motion)
        {
            return ModelSaveSummaryFormatter.FormatTrainingAttackStatsText(motion);
        }

        /// <summary>
        /// 再開確認向けのダメージテキストを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatResumeAttackDamageText(MotionType motion)
        {
            return ModelSaveSummaryFormatter.FormatTrainingAttackDamageText(motion);
        }

        /// <summary>
        /// 再開確認向けのコストテキストを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatResumeAttackCostText(MotionType motion)
        {
            return ModelSaveSummaryFormatter.FormatTrainingAttackCostText(motion);
        }

        /// <summary>
        /// 攻撃の必要部位ラベルを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatRequiredPartLabel(MotionType motion)
        {
            switch (MotionPartRequirement.GetRequiredPart(motion))
            {
                case BonePart.Arm:
                    return "腕技";
                case BonePart.Leg:
                    return "脚技";
                case BonePart.Front:
                    return "前技";
                case BonePart.Back:
                    return "後技";
                default:
                    return "体技";
            }
        }

        private static bool ContainsAttack(IReadOnlyList<MotionType> attacks, MotionType motion)
        {
            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] == motion)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
