namespace ClayEditor.Rigging
{
    /// <summary>
    /// ボーンが属する部位の種類
    /// </summary>
    public enum BonePart
    {
        /// <summary>
        /// 胴体(ルート付近、どの枝にも属さない)
        /// </summary>
        Body,

        /// <summary>
        /// 脚(下方向に伸びる枝)
        /// </summary>
        Leg,

        /// <summary>
        /// 腕(左右方向に伸びる枝)
        /// </summary>
        Arm,

        /// <summary>
        /// 前(前方+Zに伸びる枝、頭など)
        /// </summary>
        Front,

        /// <summary>
        /// 後ろ(後方-Zに伸びる枝、尻尾など)
        /// </summary>
        Back
    }
}