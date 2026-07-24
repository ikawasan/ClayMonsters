namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 所持アイテム表示用の1件
    /// </summary>
    public readonly struct TrainingInventoryEntryView
    {
        /// <summary>
        /// 所持アイテム表示を生成する
        /// </summary>
        public TrainingInventoryEntryView(
            string itemId,
            string displayName,
            string description,
            int count)
        {
            ItemId = itemId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Count = count;
        }

        /// <summary>
        /// 商品ID
        /// </summary>
        public string ItemId { get; }

        /// <summary>
        /// 表示名
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 説明
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 所持数
        /// </summary>
        public int Count { get; }
    }
}
