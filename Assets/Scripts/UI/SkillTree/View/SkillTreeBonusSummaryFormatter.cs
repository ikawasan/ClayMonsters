using SaveData;
using System.Text;
using UnityEngine;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリー獲得効果の一覧文言を生成する
    /// </summary>
    public static class SkillTreeBonusSummaryFormatter
    {
        /// <summary>
        /// ボーナス一覧テキストを生成する
        /// </summary>
        /// <param name="bonuses">集計ボーナス</param>
        public static string Format(SkillTreeBonuses bonuses)
        {
            var builder = new StringBuilder(256);
            builder.AppendLine("【獲得効果】");
            AppendStat(builder, "開始HP", bonuses.StartingHp);
            AppendStat(builder, "開始攻撃", bonuses.StartingAttack);
            AppendStat(builder, "開始防御", bonuses.StartingDefense);
            AppendStat(builder, "開始速度", bonuses.StartingSpeed);
            AppendStat(builder, "開始命中", bonuses.StartingHit);
            AppendStat(builder, "開始全ステ", bonuses.StartingAll);
            AppendPercent(builder, "大成功率", bonuses.GreatSuccessBonusPercent);
            AppendMoney(builder, "開始所持金", bonuses.StartingMoney);
            AppendPercent(builder, "育成獲得金", bonuses.TrainingMoneyGainPercent);
            AppendPercent(builder, "対戦ポイント", bonuses.PointsGainPercent);
            AppendPercent(builder, "継承上昇", bonuses.InheritancePercentBonus);
            return builder.ToString().TrimEnd();
        }

        private static void AppendStat(StringBuilder builder, string label, int value)
        {
            builder.Append(label);
            builder.Append("　");
            builder.Append(value > 0 ? $"+{value}" : "─");
            builder.AppendLine();
        }

        private static void AppendMoney(StringBuilder builder, string label, int value)
        {
            builder.Append(label);
            builder.Append("　");
            builder.Append(value > 0 ? $"+{value}G" : "─");
            builder.AppendLine();
        }

        private static void AppendPercent(StringBuilder builder, string label, float value)
        {
            builder.Append(label);
            builder.Append("　");
            if (value > 0.001f)
            {
                builder.Append('+');
                builder.Append(FormatPercent(value));
                builder.Append('%');
            }
            else
            {
                builder.Append('─');
            }

            builder.AppendLine();
        }

        private static string FormatPercent(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;
            if (Mathf.Approximately(rounded, Mathf.Round(rounded)))
            {
                return Mathf.RoundToInt(rounded).ToString();
            }

            return rounded.ToString("0.#");
        }
    }
}
