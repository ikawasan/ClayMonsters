using System;
using System.Collections.Generic;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室ローカル索引のコンテナ
    /// </summary>
    [Serializable]
    public sealed class ModelGalleryIndex
    {
        /// <summary>
        /// 公開済みエントリ一覧
        /// </summary>
        public List<ModelGalleryIndexEntry> entries = new List<ModelGalleryIndexEntry>();
    }
}
