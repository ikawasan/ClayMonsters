namespace Battle.Interface
{
    /// <summary>
    /// 各ユニットの座標オフセットを直接更新し間合いを結果として算出する
    /// </summary>
    public interface IBattleFieldMovement : IBattleFieldBoundary
    {
        /// <summary>
        /// 移動意図に応じて各ユニットのオフセットだけを更新する
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        /// <param name="playerMovementIntent">プレイヤー移動意図</param>
        /// <param name="enemyMovementIntent">敵移動意図</param>
        /// <param name="playerMoveSpeed">プレイヤー移動速度</param>
        /// <param name="enemyMoveSpeed">敵移動速度</param>
        /// <param name="maxDistance">最大間合い</param>
        /// <param name="distance">更新後の間合い</param>
        void ApplyIndependentMovement(
            float deltaTime,
            int playerMovementIntent,
            int enemyMovementIntent,
            float playerMoveSpeed,
            float enemyMoveSpeed,
            float maxDistance,
            out float distance);
    }
}
