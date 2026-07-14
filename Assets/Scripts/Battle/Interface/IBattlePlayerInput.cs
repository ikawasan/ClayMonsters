namespace Battle.Interface
{
    /// <summary>
    /// 戦闘中のプレイヤー操作入力を提供する
    /// </summary>
    public interface IBattlePlayerInput : IBattleMovementInput
    {
        /// <summary>
        /// ふきとばしがこのフレームで押されたかを返し消費する
        /// </summary>
        bool ConsumeKnockbackPressed();

        /// <summary>
        /// 攻撃ボタンがこのフレームで押された技番号を返し消費する未押下は-1
        /// </summary>
        int ConsumeAttackMoveIndex();

        /// <summary>
        /// ステップ移動がこのフレームで押された方向を返し消費する未押下は0
        /// </summary>
        int ConsumeStepIntent();

        /// <summary>
        /// 部位復旧用にマウス右ボタンを押し続けているか
        /// </summary>
        bool IsHoldingPartRepair { get; }
    }
}
