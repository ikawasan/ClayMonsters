namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室投稿の結果
    /// </summary>
    public sealed class ModelGalleryPublishResult
    {
        /// <summary>
        /// 結果種別
        /// </summary>
        public ModelGalleryOperationStatus Status;

        /// <summary>
        /// 成功時のアイテムID
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 成功後にWorkshop利用規約同意が必要か
        /// </summary>
        public bool NeedsLegalAgreement;

        /// <summary>
        /// 成功結果を作る
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="needsLegalAgreement">規約同意が必要か</param>
        /// <returns>成功結果</returns>
        public static ModelGalleryPublishResult Success(string itemId, bool needsLegalAgreement = false)
        {
            return new ModelGalleryPublishResult
            {
                Status = ModelGalleryOperationStatus.Success,
                ItemId = itemId,
                NeedsLegalAgreement = needsLegalAgreement
            };
        }

        /// <summary>
        /// 失敗結果を作る
        /// </summary>
        /// <param name="status">失敗種別</param>
        /// <returns>失敗結果</returns>
        public static ModelGalleryPublishResult FromStatus(ModelGalleryOperationStatus status)
        {
            return new ModelGalleryPublishResult
            {
                Status = status,
                ItemId = null,
                NeedsLegalAgreement = status == ModelGalleryOperationStatus.NeedsWorkshopLegalAgreement
            };
        }
    }
}
