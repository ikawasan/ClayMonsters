namespace Battle
{
    /// <summary>
    /// 勝利演出後にプレイヤーが選ぶ遷移先
    /// </summary>
    public enum BattleVictoryReturnChoice
    {
        /// <summary>
        /// タイトル画面へ戻る
        /// </summary>
        Title,

        /// <summary>
        /// セーブスロット選択へ戻って再戦する
        /// </summary>
        Rematch
    }
}
