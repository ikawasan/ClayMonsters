using System;
using System.Collections.Generic;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// ローカルプレイヤーのお気に入りID一覧
    /// </summary>
    [Serializable]
    public sealed class ModelGalleryLocalFavorites
    {
        /// <summary>
        /// お気に入り中のアイテムID
        /// </summary>
        public List<string> itemIds = new List<string>();
    }
}
