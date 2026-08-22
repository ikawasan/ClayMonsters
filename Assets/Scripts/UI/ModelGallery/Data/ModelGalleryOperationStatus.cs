namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室操作の結果種別
    /// </summary>
    public enum ModelGalleryOperationStatus
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success = 0,

        /// <summary>
        /// 一般失敗
        /// </summary>
        Failed = 1,

        /// <summary>
        /// Steam未初期化または未ログイン
        /// </summary>
        SteamUnavailable = 2,

        /// <summary>
        /// Steam応答タイムアウト
        /// </summary>
        TimedOut = 3,

        /// <summary>
        /// Workshop利用規約への同意が必要
        /// </summary>
        NeedsWorkshopLegalAgreement = 4
    }
}
