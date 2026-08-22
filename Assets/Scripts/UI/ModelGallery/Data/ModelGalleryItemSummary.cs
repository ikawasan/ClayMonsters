using System;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室に公開された育成前モデル1件の要約
    /// </summary>
    [Serializable]
    public sealed class ModelGalleryItemSummary
    {
        /// <summary>
        /// 展示室アイテムID
        /// </summary>
        public string itemId;

        /// <summary>
        /// 表示タイトル
        /// </summary>
        public string title;

        /// <summary>
        /// 投稿者名
        /// </summary>
        public string authorName;

        /// <summary>
        /// 投稿時刻(Unix秒)
        /// </summary>
        public long publishedUnixTime;

        /// <summary>
        /// 最終更新時刻(Unix秒)
        /// </summary>
        public long updatedUnixTime;

        /// <summary>
        /// お気に入り数
        /// </summary>
        public int favoriteCount;

        /// <summary>
        /// 自分がお気に入り中か
        /// </summary>
        public bool isFavoritedByMe;
    }
}
