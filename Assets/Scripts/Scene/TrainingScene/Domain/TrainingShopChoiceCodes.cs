namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 売店UIの選択コード
    /// </summary>
    public static class TrainingShopChoiceCodes
    {
        /// <summary>
        /// 売店を閉じる
        /// </summary>
        public const int Back = -1;

        /// <summary>
        /// 次の商品ページ
        /// </summary>
        public const int NextPage = -2;

        /// <summary>
        /// 所持アイテムウィンドウを開く
        /// </summary>
        public const int OpenInventory = -3;

        /// <summary>
        /// 売店の陳列を入れ替える
        /// </summary>
        public const int RefreshOffer = -4;
    }
}
