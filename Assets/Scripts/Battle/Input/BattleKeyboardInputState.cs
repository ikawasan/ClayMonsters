namespace Battle.Input
{
    /// <summary>
    /// キーボード入力のスナップショット
    /// </summary>
    public readonly struct BattleKeyboardInputState
    {
        /// <summary>
        /// 入力スナップショットを生成する
        /// </summary>
        public BattleKeyboardInputState(
            int movementIntent,
            int pendingStepIntent,
            bool knockbackPressed,
            int pendingAttackIndex,
            bool isHoldingPartRepair)
        {
            MovementIntent = movementIntent;
            PendingStepIntent = pendingStepIntent;
            KnockbackPressed = knockbackPressed;
            PendingAttackIndex = pendingAttackIndex;
            IsHoldingPartRepair = isHoldingPartRepair;
        }

        /// <summary>
        /// 移動意図
        /// </summary>
        public int MovementIntent { get; }

        /// <summary>
        /// ステップ移動意図
        /// </summary>
        public int PendingStepIntent { get; }

        /// <summary>
        /// ノックバック入力
        /// </summary>
        public bool KnockbackPressed { get; }

        /// <summary>
        /// 攻撃技番号
        /// </summary>
        public int PendingAttackIndex { get; }

        /// <summary>
        /// 部位修復入力を保持中か
        /// </summary>
        public bool IsHoldingPartRepair { get; }
    }
}
