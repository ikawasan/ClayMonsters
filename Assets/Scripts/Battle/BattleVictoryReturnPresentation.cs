namespace Battle
{
    /// <summary>
    /// 勝利演出後の戻りボタン表示形態
    /// </summary>
    public enum BattleVictoryReturnPresentation
    {
        /// <summary>
        /// タイトルへ戻ると再戦
        /// </summary>
        TitleAndRematch = 0,

        /// <summary>
        /// 続けると中断
        /// </summary>
        ContinueAndAbort = 1,

        /// <summary>
        /// タイトルへ戻るのみ
        /// </summary>
        TitleOnly = 2,
    }
}
