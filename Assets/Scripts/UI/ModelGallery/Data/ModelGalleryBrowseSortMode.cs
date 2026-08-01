namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室閲覧一覧の並び替えモード
    /// </summary>
    public enum ModelGalleryBrowseSortMode
    {
        /// <summary>
        /// ランダム表示
        /// </summary>
        Random = 0,

        /// <summary>
        /// 月間お気に入り数ランキング
        /// </summary>
        MonthlyRanking = 1,

        /// <summary>
        /// 総合お気に入り数ランキング
        /// </summary>
        OverallRanking = 2
    }
}
