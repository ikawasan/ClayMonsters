namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 1日の時間割スロット
    /// </summary>
    public enum TrainingPeriod
    {
        /// <summary>
        /// 1時間目
        /// </summary>
        FirstHour,

        /// <summary>
        /// 2時間目
        /// </summary>
        SecondHour,

        /// <summary>
        /// 中休み
        /// </summary>
        MorningBreak,

        /// <summary>
        /// 3時間目
        /// </summary>
        ThirdHour,

        /// <summary>
        /// 4時間目
        /// </summary>
        FourthHour,

        /// <summary>
        /// 昼休み
        /// </summary>
        LunchBreak,

        /// <summary>
        /// 5時間目
        /// </summary>
        FifthHour,

        /// <summary>
        /// 6時間目
        /// </summary>
        SixthHour,

        /// <summary>
        /// 放課後
        /// </summary>
        AfterSchool
    }
}
