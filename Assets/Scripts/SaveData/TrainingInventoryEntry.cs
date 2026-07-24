namespace SaveData
{
    /// <summary>
    /// 育成中の所持アイテム1件
    /// </summary>
    [System.Serializable]
    public class TrainingInventoryEntry
    {
        /// <summary>
        /// 商品ID
        /// </summary>
        public string itemId;

        /// <summary>
        /// 所持数
        /// </summary>
        public int count;
    }
}
