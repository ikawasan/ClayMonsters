namespace Battle
{
    /// <summary>
    /// ネットワーク同期された部位修復完了
    /// </summary>
    public readonly struct BattleRemotePartRestorePayload
    {
        /// <summary>
        /// 同期修復完了を生成する
        /// </summary>
        public BattleRemotePartRestorePayload(int limbIndex, int sequence)
        {
            LimbIndex = limbIndex;
            Sequence = sequence;
        }

        /// <summary>
        /// 修復したリム番号
        /// </summary>
        public int LimbIndex { get; }

        /// <summary>
        /// 同期番号
        /// </summary>
        public int Sequence { get; }
    }
}
