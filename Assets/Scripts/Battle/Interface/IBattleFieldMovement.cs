namespace Battle.Interface
{
    /// <summary>
    /// 各ユニットの座標オフセットを直接更新し間合いは結果として算出する
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

        /// <summary>
        /// プレイヤーのステップ開始時にオフセット目標を確定する
        /// </summary>
        /// <param name="startDistance">開始時の間合い</param>
        /// <param name="targetDistance">目標間合い</param>
        /// <param name="maxDistance">最大間合い</param>
        void BeginPlayerStep(float startDistance, float targetDistance, float maxDistance);

        /// <summary>
        /// 敵のステップ開始時にオフセット目標を確定する
        /// </summary>
        /// <param name="startDistance">開始時の間合い</param>
        /// <param name="targetDistance">目標間合い</param>
        /// <param name="maxDistance">最大間合い</param>
        void BeginEnemyStep(float startDistance, float targetDistance, float maxDistance);

        /// <summary>
        /// プレイヤーのステップ進捗をオフセットへ反映する
        /// </summary>
        /// <param name="easedT01">イージング済み進捗</param>
        /// <param name="maxDistance">最大間合い</param>
        /// <param name="distance">更新後の間合い</param>
        void SetPlayerStepProgress(float easedT01, float maxDistance, out float distance);

        /// <summary>
        /// 敵のステップ進捗をオフセットへ反映する
        /// </summary>
        /// <param name="easedT01">イージング済み進捗</param>
        /// <param name="maxDistance">最大間合い</param>
        /// <param name="distance">更新後の間合い</param>
        void SetEnemyStepProgress(float easedT01, float maxDistance, out float distance);

        /// <summary>
        /// 敵だけをホーム方向へ押し間合いを広げる
        /// </summary>
        /// <param name="openDistanceAmount">広げたい間合い量</param>
        /// <param name="currentDistance">現在の間合い</param>
        /// <param name="maxDistance">最大間合い</param>
        /// <param name="distance">更新後の間合い</param>
        void PushEnemyAway(
            float openDistanceAmount,
            float currentDistance,
            float maxDistance,
            out float distance);

        /// <summary>
        /// プレイヤーだけをホーム方向へ押し間合いを広げる
        /// </summary>
        /// <param name="openDistanceAmount">広げたい間合い量</param>
        /// <param name="currentDistance">現在の間合い</param>
        /// <param name="maxDistance">最大間合い</param>
        /// <param name="distance">更新後の間合い</param>
        void PushPlayerAway(
            float openDistanceAmount,
            float currentDistance,
            float maxDistance,
            out float distance);

        /// <summary>
        /// 現在オフセットから間合いを算出する
        /// </summary>
        /// <param name="maxDistance">最大間合い</param>
        /// <returns>間合い</returns>
        float ComputeDistance(float maxDistance);

        /// <summary>
        /// 現在オフセットをモデル座標へ書き込む
        /// </summary>
        void ApplyModelTransforms();
    }
}
