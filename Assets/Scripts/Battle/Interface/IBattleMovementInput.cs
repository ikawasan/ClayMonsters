namespace Battle.Interface
{
    /// <summary>
    /// 戦闘中のプレイヤー移動入力を提供する
    /// </summary>
    public interface IBattleMovementInput
    {
        /// <summary>
        /// 現在の移動意図を返す(-1=接近,0=停止,+1=後退)
        /// </summary>
        int MovementIntent { get; }

        /// <summary>
        /// フレームごとに入力状態を更新する
        /// </summary>
        void RefreshInput();
    }
}
