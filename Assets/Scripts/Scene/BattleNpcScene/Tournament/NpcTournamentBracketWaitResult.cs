namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント表待機の結果
    /// </summary>
    public enum NpcTournamentBracketWaitResult
    {
        /// <summary>
        /// 左クリックで試合進行
        /// </summary>
        Advance = 0,

        /// <summary>
        /// タイトルへ中断
        /// </summary>
        AbortToTitle = 1,
    }
}
