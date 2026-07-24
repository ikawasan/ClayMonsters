namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 訓練と特訓で伸ばす主ステータス
    /// </summary>
    public enum TrainingFocus
    {
        /// <summary>
        /// HP
        /// </summary>
        Hp,

        /// <summary>
        /// 攻撃
        /// </summary>
        Attack,

        /// <summary>
        /// 防御
        /// </summary>
        Defense,

        /// <summary>
        /// 速さ
        /// </summary>
        Speed,

        /// <summary>
        /// 命中
        /// </summary>
        Hit
    }
}
