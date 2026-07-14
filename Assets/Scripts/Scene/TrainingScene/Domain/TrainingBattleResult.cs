namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 放課後戦闘の結果
    /// </summary>
    public readonly struct TrainingBattleResult
    {
        /// <summary>
        /// 放課後戦闘の結果を生成する
        /// </summary>
        /// <param name="played">戦闘が実行されたか</param>
        /// <param name="playerWon">プレイヤーが勝利したか</param>
        /// <param name="enemyName">敵の名前</param>
        public TrainingBattleResult(bool played, bool playerWon, string enemyName)
        {
            Played = played;
            PlayerWon = playerWon;
            EnemyName = enemyName ?? string.Empty;
        }

        /// <summary>
        /// 戦闘が実行されたか
        /// </summary>
        public bool Played { get; }

        /// <summary>
        /// プレイヤーが勝利したか
        /// </summary>
        public bool PlayerWon { get; }

        /// <summary>
        /// 敵の名前
        /// </summary>
        public string EnemyName { get; }
    }
}
