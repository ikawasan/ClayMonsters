namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成の進行方式
    /// </summary>
    public enum TrainingPlayMode
    {
        /// <summary>
        /// 手動で行き先を選ぶ通常育成
        /// </summary>
        Manual,

        /// <summary>
        /// 自動で5日間を進行する育成
        /// </summary>
        Auto
    }
}
