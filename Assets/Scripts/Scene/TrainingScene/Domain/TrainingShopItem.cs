namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 売店の商品定義
    /// </summary>
    public readonly struct TrainingShopItem
    {
        /// <summary>
        /// 売店商品を生成する
        /// </summary>
        public TrainingShopItem(
            string id,
            string displayName,
            string description,
            int price,
            TrainingShopItemType itemType,
            TrainingStatGain statGain,
            int staminaRecover,
            float greatSuccessBonusPercent,
            int greatSuccessBonusWeeks)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Price = price;
            ItemType = itemType;
            StatGain = statGain;
            StaminaRecover = staminaRecover;
            GreatSuccessBonusPercent = greatSuccessBonusPercent;
            GreatSuccessBonusWeeks = greatSuccessBonusWeeks;
        }

        /// <summary>
        /// 商品ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 表示名
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 説明文
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 価格
        /// </summary>
        public int Price { get; }

        /// <summary>
        /// アイテム種別
        /// </summary>
        public TrainingShopItemType ItemType { get; }

        /// <summary>
        /// ステ上昇量
        /// </summary>
        public TrainingStatGain StatGain { get; }

        /// <summary>
        /// 体力回復量(全回復はMaxStamina以上)
        /// </summary>
        public int StaminaRecover { get; }

        /// <summary>
        /// 大成功率加算(百分率)
        /// </summary>
        public float GreatSuccessBonusPercent { get; }

        /// <summary>
        /// 大成功ボーナスの継続週数
        /// </summary>
        public int GreatSuccessBonusWeeks { get; }
    }
}
