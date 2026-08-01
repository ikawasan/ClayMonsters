using System;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室ローカル索引の1エントリ
    /// </summary>
    [Serializable]
    public sealed class ModelGalleryIndexEntry
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
        /// お気に入り数
        /// </summary>
        public int favoriteCount;
    }
}
