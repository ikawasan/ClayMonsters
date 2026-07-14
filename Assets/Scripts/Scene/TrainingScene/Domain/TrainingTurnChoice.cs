namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成ターンで選んだ行き先または休憩
    /// </summary>
    public readonly struct TrainingTurnChoice
    {
        /// <summary>
        /// 行き先または休憩の選択を生成する
        /// </summary>
        /// <param name="isRest">休憩か</param>
        /// <param name="location">行き先</param>
        public TrainingTurnChoice(bool isRest, TrainingLocation location)
        {
            IsRest = isRest;
            Location = location;
        }

        /// <summary>
        /// 休憩を選んだか
        /// </summary>
        public bool IsRest { get; }

        /// <summary>
        /// 選んだ行き先
        /// </summary>
        public TrainingLocation Location { get; }

        /// <summary>
        /// 休憩選択を返す
        /// </summary>
        public static TrainingTurnChoice Rest()
        {
            return new TrainingTurnChoice(true, default);
        }

        /// <summary>
        /// 行き先選択を返す
        /// </summary>
        /// <param name="location">行き先</param>
        public static TrainingTurnChoice FromLocation(TrainingLocation location)
        {
            return new TrainingTurnChoice(false, location);
        }
    }
}
