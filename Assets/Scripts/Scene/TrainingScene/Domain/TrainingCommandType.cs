namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// マモンキング風の週次育成コマンド
    /// </summary>
    public enum TrainingCommandType
    {
        /// <summary>
        /// 訓練
        /// </summary>
        Train,

        /// <summary>
        /// 特訓
        /// </summary>
        SpecialTrain,

        /// <summary>
        /// 休憩
        /// </summary>
        Rest,

        /// <summary>
        /// 売店
        /// </summary>
        Shop,

        /// <summary>
        /// 所持アイテム使用
        /// </summary>
        UseItem,

        /// <summary>
        /// 大会
        /// </summary>
        Tournament
    }
}
