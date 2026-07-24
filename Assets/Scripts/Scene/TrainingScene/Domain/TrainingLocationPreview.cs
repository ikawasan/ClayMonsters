using System.Text;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 行き先ホバー時に表示する期待値
    /// </summary>
    public readonly struct TrainingLocationPreview
    {
        /// <summary>
        /// 行き先ホバー時に表示する期待値を生成する
        /// </summary>
        /// <param name="isRestAction">休憩行動か</param>
        /// <param name="expectedStaminaDelta">期待体力変化</param>
        /// <param name="expectedStatGain">期待ステータス上昇</param>
        /// <param name="failurePercent">失敗率</param>
        /// <param name="restRecoveryMax">休憩大成功時回復量</param>
        public TrainingLocationPreview(
            bool isRestAction,
            int expectedStaminaDelta,
            TrainingStatGain expectedStatGain,
            float failurePercent,
            int restRecoveryMax)
        {
            IsRestAction = isRestAction;
            ExpectedStaminaDelta = expectedStaminaDelta;
            ExpectedStatGain = expectedStatGain;
            FailurePercent = failurePercent;
            RestRecoveryMax = restRecoveryMax;
        }

        /// <summary>
        /// 休憩行動か
        /// </summary>
        public bool IsRestAction { get; }

        /// <summary>
        /// 期待体力変化
        /// </summary>
        public int ExpectedStaminaDelta { get; }

        /// <summary>
        /// 期待ステータス上昇
        /// </summary>
        public TrainingStatGain ExpectedStatGain { get; }

        /// <summary>
        /// 失敗率
        /// </summary>
        public float FailurePercent { get; }

        /// <summary>
        /// 休憩大成功時回復量
        /// </summary>
        public int RestRecoveryMax { get; }
    }

    /// <summary>
    /// 行き先の期待値を解決して表示文言へ変換する
    /// </summary>
    public static class TrainingLocationPreviewResolver
    {
        /// <summary>
        /// 行き先の期待値を返す
        /// </summary>
        /// <param name="location">行き先</param>
        /// <param name="currentStamina">現在体力</param>
        public static TrainingLocationPreview Resolve(TrainingLocation location, int currentStamina)
        {
            float failurePercent = TrainingActionResolver.ComputeFailurePercent(currentStamina);
            float successRate = 1f - failurePercent / 100f;
            TrainingStatGain expectedGain = ScaleGain(ResolveSuccessGain(location), successRate);
            int staminaDelta = -TrainingSettings.StaminaCostPerAction;

            return new TrainingLocationPreview(
                isRestAction: false,
                expectedStaminaDelta: staminaDelta,
                expectedStatGain: expectedGain,
                failurePercent: failurePercent,
                restRecoveryMax: 0);
        }

        /// <summary>
        /// 休憩の期待値を返す
        /// </summary>
        public static TrainingLocationPreview ResolveRest()
        {
            float greatRate = TrainingSettings.RestGreatSuccessPercent / 100f;
            int expectedRecovery = Mathf.RoundToInt(
                TrainingSettings.RestStaminaRecovery * (1f - greatRate)
                + TrainingSettings.RestGreatSuccessRecovery * greatRate);
            return new TrainingLocationPreview(
                isRestAction: true,
                expectedStaminaDelta: expectedRecovery,
                expectedStatGain: default,
                failurePercent: 0f,
                restRecoveryMax: TrainingSettings.RestGreatSuccessRecovery);
        }

        /// <summary>
        /// 行き先期待値の表示文言を返す
        /// </summary>
        /// <param name="location">行き先</param>
        /// <param name="currentStamina">現在体力</param>
        public static string FormatDisplayText(TrainingLocation location, int currentStamina)
        {
            TrainingLocationPreview preview = Resolve(location, currentStamina);
            string locationName = TrainingLocationCatalog.GetDisplayName(location);
            var builder = new StringBuilder();
            builder.Append(locationName);
            builder.Append('\n');
            builder.Append("期待値 ");
            builder.Append(FormatStatGain(preview.ExpectedStatGain));
            builder.Append($"\n体力 {preview.ExpectedStaminaDelta}");
            if (preview.FailurePercent > 0f)
            {
                builder.Append($"\n失敗率 {preview.FailurePercent:0.#}%");
            }

            return builder.ToString();
        }

        /// <summary>
        /// 休憩期待値の表示文言を返す
        /// </summary>
        public static string FormatRestDisplayText()
        {
            return "休憩\n体力を全回復する";
        }

        private static TrainingStatGain ResolveSuccessGain(TrainingLocation location)
        {
            TrainingStatGain gain = TrainingLocationCatalog.GetBaseGain(location);
            if (location != TrainingLocation.PrincipalOffice)
            {
                return gain;
            }

            return new TrainingStatGain(
                ComputePrincipalOfficeExpected(gain.Hp),
                ComputePrincipalOfficeExpected(gain.Attack),
                ComputePrincipalOfficeExpected(gain.Defense),
                ComputePrincipalOfficeExpected(gain.Speed),
                ComputePrincipalOfficeExpected(gain.Hit));
        }

        private static int ComputePrincipalOfficeExpected(int baseValue)
        {
            int bonusValue = Mathf.RoundToInt(baseValue * TrainingSettings.PrincipalOfficeBonusMultiplier);
            return Mathf.RoundToInt(0.25f * bonusValue + 0.75f * baseValue);
        }

        private static TrainingStatGain ScaleGain(TrainingStatGain gain, float multiplier)
        {
            return new TrainingStatGain(
                Mathf.RoundToInt(gain.Hp * multiplier),
                Mathf.RoundToInt(gain.Attack * multiplier),
                Mathf.RoundToInt(gain.Defense * multiplier),
                Mathf.RoundToInt(gain.Speed * multiplier),
                Mathf.RoundToInt(gain.Hit * multiplier));
        }

        private static string FormatStatGain(TrainingStatGain gain)
        {
            var builder = new StringBuilder();
            AppendStatPart(builder, "HP", gain.Hp);
            AppendStatPart(builder, "攻撃", gain.Attack);
            AppendStatPart(builder, "防御", gain.Defense);
            AppendStatPart(builder, "速度", gain.Speed);
            AppendStatPart(builder, "命中", gain.Hit);
            if (builder.Length == 0)
            {
                return "なし";
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendStatPart(StringBuilder builder, string label, int value)
        {
            if (value == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(label);
            builder.Append(value > 0 ? '+' : '-');
            builder.Append(Mathf.Abs(value));
        }
    }
}
