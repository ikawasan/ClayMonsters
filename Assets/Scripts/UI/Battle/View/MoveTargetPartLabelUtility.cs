using Localization;
using UI.Battle.Interface;

namespace UI.Battle.View
{
    /// <summary>
    /// 戦闘UI向け部位ラベル文字列を組み立てる
    /// </summary>
    public static class MoveTargetPartLabelUtility
    {
        /// <summary>
        /// 必要部位の行頭ラベル
        /// </summary>
        public static string RequiredRoleLabel =>
            LocalizedText.GetOrFallback(GameTextKeys.BattleRoleRequired, "要");

        /// <summary>
        /// 破壊部位の行頭ラベル
        /// </summary>
        public static string TargetRoleLabel =>
            LocalizedText.GetOrFallback(GameTextKeys.BattleRoleTarget, "破");

        /// <summary>
        /// 部位識別子の短い表示名を返す
        /// </summary>
        public static string FormatPartName(MoveTargetPartId partId)
        {
            switch (partId)
            {
                case MoveTargetPartId.Arm:
                    return LocalizedText.GetOrFallback(GameTextKeys.BattlePartArm, "腕");
                case MoveTargetPartId.Leg:
                    return LocalizedText.GetOrFallback(GameTextKeys.BattlePartLeg, "脚");
                case MoveTargetPartId.Front:
                    return LocalizedText.GetOrFallback(GameTextKeys.BattlePartFront, "前");
                case MoveTargetPartId.Back:
                    return LocalizedText.GetOrFallback(GameTextKeys.BattlePartBack, "後");
                case MoveTargetPartId.Body:
                case MoveTargetPartId.Any:
                case MoveTargetPartId.None:
                    return LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryNone, "なし");
                default:
                    return LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryNone, "なし");
            }
        }

        /// <summary>
        /// 必要部位行のラベル文字列を返す
        /// </summary>
        public static string FormatRequiredRowLabel(MoveTargetPartId partId)
        {
            string partName = FormatPartName(partId);
            return string.IsNullOrEmpty(partName)
                ? string.Empty
                : RequiredRoleLabel + ":" + partName;
        }

        /// <summary>
        /// 破壊部位行のラベル文字列を返す
        /// </summary>
        public static string FormatTargetRowLabel(MoveTargetPartId partId)
        {
            string partName = FormatPartName(partId);
            return string.IsNullOrEmpty(partName)
                ? string.Empty
                : TargetRoleLabel + ":" + partName;
        }
    }
}
