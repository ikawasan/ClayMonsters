namespace SaveData
{
    /// <summary>
    /// モデルセーブ完了時の情報
    /// </summary>
    public readonly struct SaveCompletedInfo
    {
        /// <summary>
        /// セーブ完了情報を生成する
        /// </summary>
        public SaveCompletedInfo(ModelSavePool pool, int slotIndex)
        {
            Pool = pool;
            SlotIndex = slotIndex;
        }

        /// <summary>
        /// 保存先プール
        /// </summary>
        public ModelSavePool Pool { get; }

        /// <summary>
        /// 保存したスロット番号
        /// </summary>
        public int SlotIndex { get; }
    }
}
