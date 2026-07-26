namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成再開確認の選択結果
    /// </summary>
    public enum TrainingResumeChoice
    {
        /// <summary>
        /// 続きから育成する
        /// </summary>
        Continue,

        /// <summary>
        /// 最初から育成し直す
        /// </summary>
        Restart,

        /// <summary>
        /// 再開UIが使えず選択できない
        /// </summary>
        Unavailable
    }
}
