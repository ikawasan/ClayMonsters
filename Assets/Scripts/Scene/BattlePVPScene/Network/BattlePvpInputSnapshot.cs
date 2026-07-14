using Unity.Netcode;

namespace Scene.BattlePVPScene.Network
{
    /// <summary>
    /// ネットワーク同期用の戦闘入力スナップショット
    /// </summary>
    public struct BattlePvpInputSnapshot : INetworkSerializable
    {
        /// <summary>
        /// 移動意図
        /// </summary>
        public int MovementIntent;

        /// <summary>
        /// ステップ移動意図
        /// </summary>
        public int PendingStepIntent;

        /// <summary>
        /// ノックバック入力
        /// </summary>
        public bool KnockbackPressed;

        /// <summary>
        /// 攻撃技番号
        /// </summary>
        public int PendingAttackIndex;

        /// <summary>
        /// 部位修復入力を保持中か
        /// </summary>
        public bool IsHoldingPartRepair;

        /// <inheritdoc/>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MovementIntent);
            serializer.SerializeValue(ref PendingStepIntent);
            serializer.SerializeValue(ref KnockbackPressed);
            serializer.SerializeValue(ref PendingAttackIndex);
            serializer.SerializeValue(ref IsHoldingPartRepair);
        }
    }
}
