namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成の進行方式
    /// </summary>
    public enum TrainingPlayMode
    {
        /// <summary>
        /// 毎日6時間目の行動を手動で選ぶ通常育成
        /// </summary>
        Manual,

        /// <summary>
        /// 全日程を自動で進行する育成
        /// </summary>
        Auto
    }
}
