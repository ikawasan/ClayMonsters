using ClayEditor.Rigging;
using Localization;
using SaveData;
using System.Collections.Generic;
using UI.ClayEditor.View;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成再開確認ウィンドウ向けの表示データ
    /// 表示文字列はプロパティ参照時に現在言語で組み立てる
    /// </summary>
    public readonly struct TrainingResumeProgressPresentation
    {
        /// <summary>
        /// 表示データを生成する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="day">曜日</param>
        /// <param name="turnIndexInDay">日内ターン位置</param>
        /// <param name="money">所持金</param>
        /// <param name="stamina">体力</param>
        /// <param name="motivation">やる気</param>
        /// <param name="status">ステータス</param>
        /// <param name="attacks">技構成</param>
        /// <param name="thumbnailPng">モデルサムネイルPNG</param>
        public TrainingResumeProgressPresentation(
            string modelName,
            TrainingDayOfWeek day,
            int turnIndexInDay,
            int money,
            int stamina,
            TrainingMotivation motivation,
            ModelStatus status,
            IReadOnlyList<MotionType> attacks,
            byte[] thumbnailPng)
        {
            ModelName = modelName ?? string.Empty;
            Day = day;
            TurnIndexInDay = turnIndexInDay;
            Money = money;
            Stamina = stamina;
            Motivation = TrainingMotivationCatalog.Clamp((int)motivation);
            Status = status;
            Attacks = attacks ?? System.Array.Empty<MotionType>();
            ThumbnailPng = thumbnailPng;
        }

        /// <summary>
        /// モデル名
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// 曜日
        /// </summary>
        public TrainingDayOfWeek Day { get; }

        /// <summary>
        /// 日内ターン位置
        /// </summary>
        public int TurnIndexInDay { get; }

        /// <summary>
        /// 所持金
        /// </summary>
        public int Money { get; }

        /// <summary>
        /// 体力
        /// </summary>
        public int Stamina { get; }

        /// <summary>
        /// ステータス
        /// </summary>
        public ModelStatus Status { get; }

        /// <summary>
        /// 技構成
        /// </summary>
        public IReadOnlyList<MotionType> Attacks { get; }

        /// <summary>
        /// モデルサムネイルPNG
        /// </summary>
        public byte[] ThumbnailPng { get; }

        /// <summary>
        /// やる気
        /// </summary>
        public TrainingMotivation Motivation { get; }

        /// <summary>
        /// 曜日・時間割・所持金
        /// </summary>
        public string ScheduleMoneyLabel
        {
            get
            {
                string periodLabel = ResolvePeriodDisplayName(TurnIndexInDay);
                string schedule =
                    $"{TrainingDayCatalog.GetDisplayName(Day)}"
                    + (string.IsNullOrEmpty(periodLabel) ? string.Empty : $" {periodLabel}");
                return LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingResumeScheduleWithMoney,
                    "{schedule}, 所持金 {money}G",
                    new Dictionary<string, object>
                    {
                        { "schedule", schedule },
                        { "money", Money },
                    });
            }
        }

        /// <summary>
        /// やる気ラベル
        /// </summary>
        public string MotivationLabel =>
            LocalizedText.GetOrFallback(
                GameTextKeys.TrainingResumeMotivationLabel,
                "やる気　");

        /// <summary>
        /// 体力ラベル
        /// </summary>
        public string StaminaLabel =>
            LocalizedText.GetOrFallback(
                GameTextKeys.TrainingResumeStaminaLabel,
                ", 体力 {stamina} / {max}",
                new Dictionary<string, object>
                {
                    { "stamina", Stamina },
                    { "max", TrainingSettings.MaxStamina },
                });

        /// <summary>
        /// ステータス表示
        /// </summary>
        public string StatsText =>
            ModelSaveSummaryFormatter.FormatTrainingStatusParameters(Status);

        private static string ResolvePeriodDisplayName(int turnIndexInDay)
        {
            TrainingPeriod[] periods = TrainingDailySchedule.AllPeriods;
            if (turnIndexInDay < 0 || turnIndexInDay >= periods.Length)
            {
                return string.Empty;
            }

            return TrainingPeriodCatalog.GetDisplayName(periods[turnIndexInDay]);
        }
    }
}
