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
        public const string RequiredRoleLabel = "要";

        /// <summary>
        /// 破壊部位の行頭ラベル
        /// </summary>
        public const string TargetRoleLabel = "破";

        /// <summary>
        /// 部位識別子の短い表示名を返す
        /// </summary>
        public static string FormatPartName(MoveTargetPartId partId)
        {
            switch (partId)
            {
                case MoveTargetPartId.Arm:
                    return "腕";
                case MoveTargetPartId.Leg:
                    return "脚";
                case MoveTargetPartId.Front:
                    return "前";
                case MoveTargetPartId.Back:
                    return "後";
                case MoveTargetPartId.Body:
                    return "体";
                case MoveTargetPartId.Any:
                    return "任意";
                default:
                    return string.Empty;
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
