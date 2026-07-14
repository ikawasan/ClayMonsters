using Unity.Netcode;

namespace Scene.BattlePVPScene.Network
{
    /// <summary>
    /// ネットワーク同期用の攻撃結果
    /// </summary>
    public struct BattlePvpStrikeResult : INetworkSerializable
    {
        /// <summary>
        /// 同期番号
        /// </summary>
        public int Sequence;

        /// <summary>
        /// 攻撃技番号
        /// </summary>
        public int MoveIndex;

        /// <summary>
        /// 命中したか
        /// </summary>
        public bool Hit;

        /// <summary>
        /// ダメージ
        /// </summary>
        public int Damage;

        /// <summary>
        /// 部位欠損が発生したか
        /// </summary>
        public bool PartLost;

        /// <summary>
        /// 欠損した部位
        /// </summary>
        public int LostPart;

        /// <summary>
        /// とどめか
        /// </summary>
        public bool IsKnockout;

        /// <summary>
        /// 欠損したリム番号
        /// </summary>
        public int LostLimbIndex;

        /// <inheritdoc/>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref MoveIndex);
            serializer.SerializeValue(ref Hit);
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref PartLost);
            serializer.SerializeValue(ref LostPart);
            serializer.SerializeValue(ref IsKnockout);
            serializer.SerializeValue(ref LostLimbIndex);
        }
    }
}
