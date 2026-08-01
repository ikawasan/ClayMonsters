using ClayEditor.Rigging;
using SaveData;
using System;
using System.Collections.Generic;

namespace UI.ModelGallery.Data
{
    /// <summary>
    /// 展示室へ投稿する育成前モデルのパッケージ本体
    /// </summary>
    [Serializable]
    public sealed class ModelGalleryPackageMeta
    {
        /// <summary>
        /// パッケージ形式バージョン
        /// </summary>
        public string packageVersion = "1";

        /// <summary>
        /// モデル名
        /// </summary>
        public string modelName;

        /// <summary>
        /// モデルステータス
        /// </summary>
        public ModelStatus status = new ModelStatus();

        /// <summary>
        /// 攻撃モーション構成
        /// </summary>
        public List<MotionType> attackMotions = new List<MotionType>();
    }
}
