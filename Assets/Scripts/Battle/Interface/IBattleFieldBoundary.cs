namespace Battle.Interface
{
    /// <summary>
    /// フィールド上の各ユニットがこれ以上間合いを広げられない境界かを返す
    /// </summary>
    public interface IBattleFieldBoundary
    {
        /// <summary>
        /// プレイヤーがホーム位置まで後退済みか
        /// </summary>
        bool IsPlayerAtHomeBoundary { get; }

        /// <summary>
        /// 敵がホーム位置まで後退済みか
        /// </summary>
        bool IsEnemyAtHomeBoundary { get; }
    }
}
