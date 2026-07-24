namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 1週で選んだ育成コマンド
    /// </summary>
    public readonly struct TrainingWeekChoice
    {
        /// <summary>
        /// 週次コマンド選択を生成する
        /// </summary>
        /// <param name="command">コマンド</param>
        /// <param name="focus">訓練系の主ステ</param>
        public TrainingWeekChoice(TrainingCommandType command, TrainingFocus focus)
        {
            Command = command;
            Focus = focus;
        }

        /// <summary>
        /// 選んだコマンド
        /// </summary>
        public TrainingCommandType Command { get; }

        /// <summary>
        /// 訓練系の主ステ
        /// </summary>
        public TrainingFocus Focus { get; }

        /// <summary>
        /// 休憩選択を返す
        /// </summary>
        public static TrainingWeekChoice Rest()
        {
            return new TrainingWeekChoice(TrainingCommandType.Rest, default);
        }

        /// <summary>
        /// 主ステ不要なコマンド選択を返す
        /// </summary>
        /// <param name="command">コマンド</param>
        public static TrainingWeekChoice FromCommand(TrainingCommandType command)
        {
            return new TrainingWeekChoice(command, default);
        }

        /// <summary>
        /// 訓練または特訓の選択を返す
        /// </summary>
        /// <param name="command">訓練または特訓</param>
        /// <param name="focus">主ステ</param>
        public static TrainingWeekChoice FromFocus(
            TrainingCommandType command,
            TrainingFocus focus)
        {
            return new TrainingWeekChoice(command, focus);
        }
    }
}
