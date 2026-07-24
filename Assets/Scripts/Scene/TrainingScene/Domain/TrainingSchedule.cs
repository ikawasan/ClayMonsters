using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 日次スケジュールと参加可能コマンドを解決する
    /// </summary>
    public static class TrainingSchedule
    {
        /// <summary>
        /// 特訓開催日か
        /// </summary>
        /// <param name="day">曜日</param>
        public static bool IsSpecialTrainDay(TrainingDayOfWeek day)
        {
            return day == TrainingDayOfWeek.Friday;
        }

        /// <summary>
        /// HUDへ出す授業時間コマンド一覧を返す
        /// 売店は昼休み専用のため含めない
        /// 休憩は別ボタンのため含めない
        /// </summary>
        /// <param name="session">育成セッション</param>
        public static List<TrainingCommandType> BuildHudCommands(TrainingSession session)
        {
            var commands = new List<TrainingCommandType>(3)
            {
                TrainingCommandType.Train
            };

            TrainingDayOfWeek day = session != null
                ? session.CurrentDay
                : TrainingDayOfWeek.Monday;
            if (IsSpecialTrainDay(day))
            {
                commands.Add(TrainingCommandType.SpecialTrain);
            }

            commands.Add(TrainingCommandType.UseItem);
            return commands;
        }
    }
}
