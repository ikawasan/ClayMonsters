namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 売店で扱うアイテム種別
    /// </summary>
    public enum TrainingShopItemType
    {
        /// <summary>
        /// 体力回復
        /// </summary>
        StaminaRecover,

        /// <summary>
        /// 体力全回復
        /// </summary>
        StaminaFullRecover,

        /// <summary>
        /// ステータス強化
        /// </summary>
        StatBoost,

        /// <summary>
        /// 訓練効率アップ
        /// </summary>
        TrainEfficiency,

        /// <summary>
        /// やる気上昇
        /// </summary>
        MotivationBoost
    }
}
