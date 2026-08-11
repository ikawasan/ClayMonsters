using Localization;
using SaveData;
using System.Text;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリー獲得効果の一覧文言を生成する
    /// </summary>
    public static class SkillTreeBonusSummaryFormatter
    {
        /// <summary>
        /// ボーナス一覧テキストを生成する
        /// 取得済み効果のみを載せる
        /// </summary>
        /// <param name="bonuses">集計ボーナス</param>
        public static string Format(SkillTreeBonuses bonuses)
        {
            var builder = new StringBuilder(256);
            builder.AppendLine(LocalizedText.Get(GameTextKeys.SkillTreeBonusHeader));
            int beforeCount = builder.Length;
            AppendStat(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartHp), bonuses.StartingHp);
            AppendStat(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartAtk), bonuses.StartingAttack);
            AppendStat(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartDef), bonuses.StartingDefense);
            AppendStat(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartSpd), bonuses.StartingSpeed);
            AppendStat(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartHit), bonuses.StartingHit);
            AppendStat(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartAll), bonuses.StartingAll);
            AppendPercent(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatGreatSuccess), bonuses.GreatSuccessBonusPercent);
            AppendMoney(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatStartMoney), bonuses.StartingMoney);
            AppendPercent(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatTrainMoney), bonuses.TrainingMoneyGainPercent);
            AppendPercent(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatPoints), bonuses.PointsGainPercent);
            AppendPercent(builder, LocalizedText.Get(GameTextKeys.SkillTreeStatInherit), bonuses.InheritancePercentBonus);

            if (builder.Length == beforeCount)
            {
                builder.Append(LocalizedText.Get(GameTextKeys.SkillTreeStatNone));
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendStat(StringBuilder builder, string label, int value)
        {
            if (value <= 0)
            {
                return;
            }

            builder.Append(label);
            builder.Append("　");
            builder.Append($"+{value}");
            builder.AppendLine();
        }

        private static void AppendMoney(StringBuilder builder, string label, int value)
        {
            if (value <= 0)
            {
                return;
            }

            builder.Append(label);
            builder.Append("　");
            builder.Append($"+{value}G");
            builder.AppendLine();
        }

        private static void AppendPercent(StringBuilder builder, string label, float value)
        {
            if (value <= 0.0001f)
            {
                return;
            }

            builder.Append(label);
            builder.Append("　");
            builder.Append($"+{value:0.#}%");
            builder.AppendLine();
        }
    }
}
