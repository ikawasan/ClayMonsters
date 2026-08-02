using Localization;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 日次コマンドの表示名を解決する
    /// </summary>
    public static class TrainingCommandCatalog
    {
        /// <summary>
        /// 表示名を返す
        /// </summary>
        /// <param name="command">コマンド</param>
        public static string GetDisplayName(TrainingCommandType command)
        {
            return command switch
            {
                TrainingCommandType.Train => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingCommandTrain, "訓練"),
                TrainingCommandType.SpecialTrain => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingCommandSpecialTrain, "特訓"),
                TrainingCommandType.Rest => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingCommandRest, "休憩"),
                TrainingCommandType.Shop => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingCommandShop, "売店"),
                TrainingCommandType.UseItem => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingCommandUseItem, "アイテム"),
                TrainingCommandType.Tournament => LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingCommandTournament, "大会"),
                _ => command.ToString()
            };
        }

        /// <summary>
        /// 主ステ選択が必要か
        /// </summary>
        /// <param name="command">コマンド</param>
        public static bool RequiresFocus(TrainingCommandType command)
        {
            return command == TrainingCommandType.Train
                || command == TrainingCommandType.SpecialTrain;
        }

        /// <summary>
        /// 6時間目を消費するコマンドか
        /// </summary>
        /// <param name="command">コマンド</param>
        public static bool ConsumesWeek(TrainingCommandType command)
        {
            return command != TrainingCommandType.Shop
                && command != TrainingCommandType.UseItem;
        }
    }
}
