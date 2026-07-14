namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 1日の時間割を定義する
    /// 全9ターンで行き先を選択する
    /// </summary>
    public static class TrainingDailySchedule
    {
        private static readonly TrainingPeriod[] Periods =
        {
            TrainingPeriod.FirstHour,
            TrainingPeriod.SecondHour,
            TrainingPeriod.MorningBreak,
            TrainingPeriod.ThirdHour,
            TrainingPeriod.FourthHour,
            TrainingPeriod.LunchBreak,
            TrainingPeriod.FifthHour,
            TrainingPeriod.SixthHour,
            TrainingPeriod.AfterSchool
        };

        /// <summary>
        /// 1日のターン数
        /// </summary>
        public static int TurnsPerDay => Periods.Length;

        /// <summary>
        /// 1日の時間割を返す
        /// </summary>
        public static TrainingPeriod[] AllPeriods => Periods;
    }
}
