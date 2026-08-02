using Battle;
using ClayEditor.Rigging;
using Localization;
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
        /// 攻撃の技名と強さランクを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackName(MotionType motion)
        {
            return MotionPartRequirement.FormatDisplayNameWithStrengthRank(motion);
        }

        /// <summary>
        /// 攻撃のUI表示ラベルを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackLabel(MotionType motion)
        {
            return $"{FormatAttackName(motion)}"
                + $" {LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPower, "威力")}"
                + $"{MotionPartRequirement.GetPowerDisplayValue(motion)}"
                + $" [{FormatRequiredPartLabel(motion)}]";
        }

        /// <summary>
        /// 攻撃の詳細情報をUI表示用テキストで返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatAttackDetail(MotionType motion)
        {
            return FormatAttackName(motion)
                + " [" + FormatRequiredPartLabel(motion) + "]"
                + "\n" + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackDestroy, "破壊")
                + ":" + MotionPartRequirement.FormatTargetDestroyPartLabel(motion)
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
            string seconds = LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackSeconds, "秒");
            return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPower, "威力")
                + ":" + MotionPartRequirement.GetPowerDisplayValue(motion)
                + "  " + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackCost, "コスト")
                + ":" + MotionPartRequirement.GetGutsCostDisplayValue(motion)
                + "  " + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackHit, "命中")
                + ":" + BattleCombatRules.ToHitRateLabel(accuracy)
                + "  " + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackRecovery, "硬直")
                + ":" + MotionPartRequirement.GetRecovery(motion).ToString("0.##") + seconds
                + "  " + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackWindUp, "溜め")
                + ":" + MotionPartRequirement.GetWindUpDuration(motion).ToString("0.##") + seconds;
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
            string header = LocalizedText.GetOrFallback(
                GameTextKeys.TrainingSwapSlotResult,
                "スロット{slot}を入れ替え",
                new Dictionary<string, object>
                {
                    { "slot", slotNumber },
                });
            string current = LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackCurrent, "現在");
            string learn = LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackLearn, "習得");
            return header
                + "\n【" + current + "】" + FormatAttackDetail(currentAttack)
                + "\n【" + learn + "】" + FormatAttackDetail(learnedAttack);
        }

        /// <summary>
        /// 再開確認向けの技情報テキストを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public static string FormatResumeAttackDetail(MotionType motion)
        {
            return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackNameLabel, "攻撃名")
                + " " + FormatAttackName(motion)
                + "\n" + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackReqPart, "必要部位")
                + " " + FormatRequiredPartLabel(motion)
                + "\n" + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackDestroyPart, "破壊部位")
                + " " + MotionPartRequirement.FormatTargetDestroyPartLabel(motion)
                + "\n" + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackDamage, "ダメージ")
                + " " + MotionPartRequirement.GetPowerDisplayValue(motion)
                + "\n" + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackCost, "コスト")
                + " " + MotionPartRequirement.GetGutsCostDisplayValue(motion)
                + "\n" + LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackRange, "間合い")
                + " " + MotionPartRequirement.FormatRangeLabel(motion);
        }

        /// <summary>
        /// 再開確認向けの範囲ラベル
        /// </summary>
        public static string ResumeAttackRangeLabel => ModelSaveSummaryFormatter.TrainingAttackRangeLabel;

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
            if (ProceduralMotionCharacter.IsMagicAttack(motion))
            {
                return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPartMagic, "汎用技");
            }

            switch (MotionPartRequirement.GetRequiredPart(motion))
            {
                case BonePart.Arm:
                    return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPartArm, "腕技");
                case BonePart.Leg:
                    return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPartLeg, "脚技");
                case BonePart.Front:
                    return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPartFront, "前技");
                case BonePart.Back:
                    return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPartBack, "後技");
                default:
                    return LocalizedText.GetOrFallback(GameTextKeys.TrainingAttackPartBody, "体技");
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
